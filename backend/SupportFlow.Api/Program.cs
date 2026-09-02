using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using SupportFlow.Api.Data;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;
using SupportFlow.Api.Serialization;
using SupportFlow.Api.Services;
using SupportFlow.Api.Services.Email;
using SupportFlow.Api.Services.Gmail;
using SupportFlow.Api.Services.QueueAging;
using SupportFlow.Api.Services.Sla;
using SupportFlow.Api.Services.TicketActivities;
using SupportFlow.Api.Settings;

var builder = WebApplication.CreateBuilder(args);

const string developmentCorsPolicy = "FrontendDevelopment";

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
    });
builder.Services.AddEndpointsApiExplorer();

if (builder.Environment.IsDevelopment())
{
    var frontendOrigin = builder.Configuration["Frontend:DevelopmentOrigin"]
        ?? throw new InvalidOperationException("Frontend development origin is missing.");

    builder.Services.AddCors(options =>
    {
        options.AddPolicy(developmentCorsPolicy, policy =>
            policy.WithOrigins(frontendOrigin)
                .AllowAnyHeader()
                .AllowAnyMethod());
    });
}

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter the JWT access token only. Swagger adds the Bearer prefix automatically."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var jwtSection = builder.Configuration.GetSection(JwtSettings.SectionName);
var jwtSettings = jwtSection.Get<JwtSettings>()
    ?? throw new InvalidOperationException("JWT configuration is missing.");

if (string.IsNullOrWhiteSpace(jwtSettings.Issuer)
    || string.IsNullOrWhiteSpace(jwtSettings.Audience)
    || Encoding.UTF8.GetByteCount(jwtSettings.Key) < 32
    || jwtSettings.ExpirationMinutes <= 0)
{
    throw new InvalidOperationException("JWT configuration is invalid.");
}

builder.Services.Configure<JwtSettings>(jwtSection);
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection(EmailSettings.SectionName));
builder.Services.Configure<ApplicationSettings>(
    builder.Configuration.GetSection(ApplicationSettings.SectionName));
builder.Services.Configure<GmailSettings>(
    builder.Configuration.GetSection(GmailSettings.SectionName));
builder.Services.Configure<QueueAgingSettings>(
    builder.Configuration.GetSection(QueueAgingSettings.SectionName));
builder.Services.AddSingleton<IValidateOptions<SlaSettings>, SlaSettingsValidator>();
builder.Services.AddOptions<SlaSettings>()
    .Bind(builder.Configuration.GetSection(SlaSettings.SectionName))
    .ValidateOnStart();
builder.Services.AddOptions<GmailBackgroundSyncSettings>()
    .Bind(builder.Configuration.GetSection(GmailBackgroundSyncSettings.SectionName))
    .Validate(
        settings => settings.IntervalSeconds >= 30,
        "GmailBackgroundSync:IntervalSeconds must be at least 30.")
    .Validate(
        settings => settings.InitialDelaySeconds >= 0,
        "GmailBackgroundSync:InitialDelaySeconds cannot be negative.")
    .ValidateOnStart();
builder.Services.AddMemoryCache();
builder.Services.AddDataProtection();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var roleValue = context.Principal?.FindFirstValue(ClaimTypes.Role);

                if (!int.TryParse(userIdValue, out var userId)
                    || !Enum.TryParse<UserRole>(roleValue, out var role)
                    || !Enum.IsDefined(role))
                {
                    context.Fail("Token subject is invalid.");
                    return;
                }

                var dbContext = context.HttpContext.RequestServices
                    .GetRequiredService<AppDbContext>();
                var subjectIsCurrent = await dbContext.Users
                    .AsNoTracking()
                    .AnyAsync(
                        user => user.Id == userId
                            && user.IsActive
                            && user.Role == role,
                        context.HttpContext.RequestAborted);

                if (!subjectIsCurrent)
                {
                    context.Fail("Token subject is no longer valid.");
                }
            }
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<TicketService>();
builder.Services.AddScoped<TicketTimelineService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<IAgentWorkloadService, AgentWorkloadService>();
builder.Services.AddSingleton<ISlaTargetResolver, SlaTargetResolver>();
builder.Services.AddSingleton<ITicketSlaService, TicketSlaService>();
builder.Services.AddSingleton<ITicketSlaCalculator, TicketSlaCalculator>();
builder.Services.AddSingleton<IQueueAgingCalculator, QueueAgingCalculator>();
builder.Services.AddScoped<ConversationService>();
builder.Services.AddScoped<AttachmentService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<AdminUserManagementService>();
builder.Services.AddScoped<TicketTaxonomyService>();
builder.Services.AddSingleton<ITicketActivityMetadataCodec, TicketActivityMetadataCodec>();
builder.Services.AddScoped<ITicketActivityWriter, TicketActivityWriter>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<TicketEmailComposer>();
builder.Services.AddScoped<IGmailAuthorizationService, GmailAuthorizationService>();
builder.Services.AddScoped<IGmailInboxService, GmailInboxService>();
builder.Services.AddScoped<IProcessedGmailMessageLookup, ProcessedGmailMessageLookup>();
builder.Services.AddScoped<IInboundEmailSenderMatcher, InboundEmailSenderMatcher>();
builder.Services.AddScoped<IExternalRequesterService, ExternalRequesterService>();
builder.Services.AddSingleton<GmailDiagnosticsCache>();
builder.Services.AddScoped<GmailDiagnosticsService>();
builder.Services.AddScoped<IGmailSendService, GmailSendService>();
builder.Services.AddScoped<ITicketAcknowledgmentService, TicketAcknowledgmentService>();
builder.Services.AddScoped<IInboundEmailCorrelationService, InboundEmailCorrelationService>();
builder.Services.AddScoped<IInboundEmailProcessor, InboundEmailProcessor>();
builder.Services.AddScoped<IGmailMailboxProcessor, GmailMailboxProcessor>();
builder.Services.AddSingleton<IGmailApiRateLimitState, GmailApiRateLimitState>();
builder.Services.AddSingleton<GmailInboxDiscoveryState>();
builder.Services.AddSingleton<GmailBackgroundSyncRuntimeStatus>();
builder.Services.AddSingleton<IGmailMailboxProcessingCoordinator, GmailMailboxProcessingCoordinator>();
builder.Services.AddHostedService<GmailInboxBackgroundService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.UseCors(developmentCorsPolicy);
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
