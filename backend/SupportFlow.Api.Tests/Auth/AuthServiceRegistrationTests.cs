using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SupportFlow.Api.Data;
using SupportFlow.Api.DTOs.Auth;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;
using SupportFlow.Api.Services;
using SupportFlow.Api.Settings;

namespace SupportFlow.Api.Tests.Auth;

public sealed class AuthServiceRegistrationTests
{
    [Theory]
    [InlineData("Customer")]
    [InlineData("Admin")]
    [InlineData("TechnicalStaff")]
    public async Task PublicRegistration_AlwaysPersistsCustomer_WhenPayloadContainsAnyRole(string attemptedRole)
    {
        await using var harness = await AuthHarness.CreateAsync();
        var json = $$"""
        {"firstName":"Public","lastName":"Registrant","email":"{{attemptedRole.ToLowerInvariant()}}@example.test","password":"Password123!","companyId":null,"role":"{{attemptedRole}}"}
        """;
        var request = JsonSerializer.Deserialize<RegisterRequest>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(request);
        var result = await harness.Service.RegisterAsync(request!);

        Assert.Equal(RegisterUserFailure.None, result.Failure);
        Assert.Equal(UserRole.Customer, result.User!.Role);
        var persisted = await harness.DbContext.Users.SingleAsync(user => user.Email == result.User.Email);
        Assert.Equal(UserRole.Customer, persisted.Role);
    }

    [Fact]
    public async Task LoginAsync_RejectsInactiveUser_AndCurrentUserLookupRejectsDeactivatedSubject()
    {
        await using var harness = await AuthHarness.CreateAsync();
        var user = harness.NewUser("inactive@example.test", UserRole.Customer, false);
        harness.DbContext.Users.Add(user);
        await harness.DbContext.SaveChangesAsync();

        var login = await harness.Service.LoginAsync(new LoginRequest
        {
            Email = user.Email,
            Password = "Password123!"
        });

        Assert.Null(login);

        user.IsActive = true;
        await harness.DbContext.SaveChangesAsync();
        var activeLogin = await harness.Service.LoginAsync(new LoginRequest
        {
            Email = user.Email,
            Password = "Password123!"
        });
        Assert.NotNull(activeLogin);

        user.IsActive = false;
        await harness.DbContext.SaveChangesAsync();
        Assert.Null(await harness.Service.GetCurrentUserAsync(user.Id));
    }

    private sealed class AuthHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private AuthHarness(SqliteConnection connection, AppDbContext dbContext, AuthService service, PasswordHasher<User> passwordHasher)
        {
            _connection = connection;
            DbContext = dbContext;
            Service = service;
            PasswordHasher = passwordHasher;
        }

        public AppDbContext DbContext { get; }
        public AuthService Service { get; }
        public PasswordHasher<User> PasswordHasher { get; }

        public static async Task<AuthHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var dbContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options);
            await dbContext.Database.EnsureCreatedAsync();
            var passwordHasher = new PasswordHasher<User>();
            var service = new AuthService(
                dbContext,
                passwordHasher,
                Options.Create(new JwtSettings
                {
                    Key = "test-signing-key-with-at-least-thirty-two-characters",
                    Issuer = "SupportFlow.Tests",
                    Audience = "SupportFlow.Tests",
                    ExpirationMinutes = 30
                }));
            return new AuthHarness(connection, dbContext, service, passwordHasher);
        }

        public User NewUser(string email, UserRole role, bool isActive)
        {
            var user = new User
            {
                FirstName = "Test",
                LastName = "User",
                Email = email,
                Role = role,
                IsActive = isActive,
                CreatedAt = new DateTime(2026, 8, 24, 10, 0, 0, DateTimeKind.Utc)
            };
            user.PasswordHash = PasswordHasher.HashPassword(user, "Password123!");
            return user;
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
