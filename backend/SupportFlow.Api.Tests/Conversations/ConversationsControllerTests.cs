using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SupportFlow.Api.Controllers;
using SupportFlow.Api.Data;
using SupportFlow.Api.DTOs.Conversations;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;
using SupportFlow.Api.Services;
using SupportFlow.Api.Services.QueueAging;
using SupportFlow.Api.Services.Sla;
using SupportFlow.Api.Settings;

namespace SupportFlow.Api.Tests.Conversations;

public sealed class ConversationsControllerTests
{
    [Fact]
    public void GetAndDetailEndpoints_RequireAdminOrTechnicalStaff_AndExcludeCustomer()
    {
        var attribute = Assert.Single(typeof(ConversationsController).GetCustomAttributes<AuthorizeAttribute>());

        Assert.Equal("Admin,TechnicalStaff", attribute.Roles);
        Assert.DoesNotContain(nameof(UserRole.Customer), attribute.Roles);
        Assert.Empty(typeof(ConversationsController).GetMethod(nameof(ConversationsController.GetSummaries))!.GetCustomAttributes<AllowAnonymousAttribute>());
        Assert.Empty(typeof(ConversationsController).GetMethod(nameof(ConversationsController.GetDetail))!.GetCustomAttributes<AllowAnonymousAttribute>());
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.TechnicalStaff)]
    public async Task GetSummaries_AllowsAdminAndTechnicalStaff(UserRole role)
    {
        await using var harness = await ConversationsControllerHarness.CreateAsync();
        var user = role == UserRole.Admin ? harness.Admin : harness.Staff;
        var controller = harness.CreateController(user);

        var result = await controller.GetSummaries(null, ConversationFilter.All, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ConversationSummaryListResponse>(ok.Value);
        Assert.NotEmpty(response.Items);
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.TechnicalStaff)]
    public async Task MarkRead_AllowsAdminAndTechnicalStaff(UserRole role)
    {
        await using var harness = await ConversationsControllerHarness.CreateAsync();
        var user = role == UserRole.Admin ? harness.Admin : harness.Staff;
        var controller = harness.CreateController(user);

        var result = await controller.MarkRead(
            ConversationRequesterType.Registered,
            harness.Customer.Id,
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(1, await harness.DbContext.ConversationReadStates.CountAsync());
    }

    [Fact]
    public async Task MarkRead_CustomerReturnsForbidden()
    {
        await using var harness = await ConversationsControllerHarness.CreateAsync();
        var controller = harness.CreateController(harness.Customer);

        var result = await controller.MarkRead(
            ConversationRequesterType.Registered,
            harness.Customer.Id,
            CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(0, await harness.DbContext.ConversationReadStates.CountAsync());
    }

    [Fact]
    public async Task MarkRead_InvalidRequesterTypeReturnsBadRequest()
    {
        await using var harness = await ConversationsControllerHarness.CreateAsync();
        var controller = harness.CreateController(harness.Admin);

        var result = await controller.MarkRead(
            (ConversationRequesterType)999,
            harness.Customer.Id,
            CancellationToken.None);

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task MarkRead_MissingRequesterReturnsNotFound()
    {
        await using var harness = await ConversationsControllerHarness.CreateAsync();
        var controller = harness.CreateController(harness.Admin);

        var result = await controller.MarkRead(
            ConversationRequesterType.Registered,
            999,
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetSummaries_InvalidFilterReturnsBadRequest()
    {
        var controller = ConversationsControllerHarness.CreateController(null!, UserRole.Admin, 1);

        var result = await controller.GetSummaries(null, (ConversationFilter)999, CancellationToken.None);

        Assert.IsType<BadRequestResult>(result.Result);
    }

    [Fact]
    public async Task MarkRead_IsIdempotentAndRetainsOneReadState()
    {
        await using var harness = await ConversationsControllerHarness.CreateAsync();
        var controller = harness.CreateController(harness.Admin);

        var first = await controller.MarkRead(ConversationRequesterType.Registered, harness.Customer.Id, CancellationToken.None);
        var stateAfterFirst = await harness.DbContext.ConversationReadStates.SingleAsync();
        var firstLastReadAt = stateAfterFirst.LastReadAt;
        var second = await controller.MarkRead(ConversationRequesterType.Registered, harness.Customer.Id, CancellationToken.None);
        var stateAfterSecond = await harness.DbContext.ConversationReadStates.SingleAsync();

        Assert.IsType<NoContentResult>(first);
        Assert.IsType<NoContentResult>(second);
        Assert.Equal(1, await harness.DbContext.ConversationReadStates.CountAsync());
        Assert.True(stateAfterSecond.LastReadAt >= firstLastReadAt);
    }

    private sealed class ConversationsControllerHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private ConversationsControllerHarness(SqliteConnection connection, AppDbContext dbContext, User admin, User staff, User customer)
        {
            _connection = connection;
            DbContext = dbContext;
            Admin = admin;
            Staff = staff;
            Customer = customer;
            Service = new ConversationService(
                dbContext,
                CreateSlaCalculator(),
                new QueueAgingCalculator(TimeProvider.System));
        }

        public AppDbContext DbContext { get; }
        public User Admin { get; }
        public User Staff { get; }
        public User Customer { get; }
        public ConversationService Service { get; }

        private static ITicketSlaCalculator CreateSlaCalculator()
        {
            var targetResolver = new SlaTargetResolver(Options.Create(new SlaSettings()));
            var lifecycle = new TicketSlaService(targetResolver);
            return new TicketSlaCalculator(lifecycle, targetResolver, TimeProvider.System);
        }

        public static async Task<ConversationsControllerHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var dbContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options);
            await dbContext.Database.EnsureCreatedAsync();

            var admin = new User { FirstName = "Admin", LastName = "User", Email = "admin@example.test", PasswordHash = "hash", Role = UserRole.Admin, IsActive = true };
            var staff = new User { FirstName = "Staff", LastName = "User", Email = "staff@example.test", PasswordHash = "hash", Role = UserRole.TechnicalStaff, IsActive = true };
            var customer = new User { FirstName = "Customer", LastName = "User", Email = "customer@example.test", PasswordHash = "hash", Role = UserRole.Customer, IsActive = true };
            dbContext.Users.AddRange(admin, staff, customer);
            await dbContext.SaveChangesAsync();

            var ticket = new Ticket
            {
                TicketNumber = "SUP-2026-5001",
                CustomerId = customer.Id,
                Title = "Conversation test",
                Description = "Conversation request",
                Status = TicketStatus.New,
                Priority = TicketPriority.Normal,
                CreatedAt = new DateTime(2024, 8, 23, 9, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 8, 23, 9, 0, 0, DateTimeKind.Utc)
            };
            dbContext.Tickets.Add(ticket);
            await dbContext.SaveChangesAsync();

            return new ConversationsControllerHarness(connection, dbContext, admin, staff, customer);
        }

        public ConversationsController CreateController(User user) =>
            CreateController(Service, user.Role, user.Id);

        public static ConversationsController CreateController(ConversationService service, UserRole role, int userId) =>
            new(service)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(
                        [
                            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                            new Claim(ClaimTypes.Role, role.ToString())
                        ],
                        "Test"))
                    }
                }
            };

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
