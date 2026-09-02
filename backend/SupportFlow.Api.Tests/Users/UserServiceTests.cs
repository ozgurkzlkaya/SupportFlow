using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportFlow.Api.Data;
using SupportFlow.Api.DTOs.Users;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;
using SupportFlow.Api.Services;

namespace SupportFlow.Api.Tests.Users;

public sealed class UserServiceTests
{
    [Fact]
    public async Task GetProfileAsync_ReturnsSafeProfileData()
    {
        await using var harness = await UserServiceHarness.CreateAsync();

        var profile = await harness.Service.GetProfileAsync(harness.User.Id);

        Assert.NotNull(profile);
        Assert.Equal(harness.User.Id, profile.Id);
        Assert.Equal("Ayşe", profile.FirstName);
        Assert.Equal("Yılmaz", profile.LastName);
        Assert.Equal("customer@example.test", profile.Email);
        Assert.Equal(UserRole.Customer, profile.Role);
        Assert.Equal(7, profile.CompanyId);
        Assert.True(profile.IsActive);
    }

    [Fact]
    public async Task UpdateProfileAsync_UpdatesNamesOnly()
    {
        await using var harness = await UserServiceHarness.CreateAsync();

        var result = await harness.Service.UpdateProfileAsync(
            harness.User.Id,
            new UpdateUserProfileRequest
            {
                FirstName = "Elif",
                LastName = "Demir"
            });

        Assert.Null(result.Error);
        Assert.NotNull(result.Profile);

        var persisted = await harness.DbContext.Users.SingleAsync(user => user.Id == harness.User.Id);
        Assert.Equal("Elif", persisted.FirstName);
        Assert.Equal("Demir", persisted.LastName);
        Assert.Equal("customer@example.test", persisted.Email);
        Assert.Equal(UserRole.Customer, persisted.Role);
        Assert.Equal(7, persisted.CompanyId);
        Assert.True(persisted.IsActive);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithCorrectCurrentPassword_UpdatesPasswordHash()
    {
        await using var harness = await UserServiceHarness.CreateAsync();
        var oldHash = harness.User.PasswordHash;

        var result = await harness.Service.ChangePasswordAsync(
            harness.User.Id,
            new ChangePasswordRequest
            {
                CurrentPassword = "Current123!",
                NewPassword = "Updated123!",
                ConfirmNewPassword = "Updated123!"
            });

        Assert.Null(result.Error);

        var persisted = await harness.DbContext.Users.SingleAsync(user => user.Id == harness.User.Id);
        Assert.NotEqual(oldHash, persisted.PasswordHash);
        var verification = harness.PasswordHasher.VerifyHashedPassword(
            persisted,
            persisted.PasswordHash,
            "Updated123!");
        Assert.NotEqual(PasswordVerificationResult.Failed, verification);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithWrongCurrentPassword_Fails()
    {
        await using var harness = await UserServiceHarness.CreateAsync();
        var oldHash = harness.User.PasswordHash;

        var result = await harness.Service.ChangePasswordAsync(
            harness.User.Id,
            new ChangePasswordRequest
            {
                CurrentPassword = "Wrong123!",
                NewPassword = "Updated123!",
                ConfirmNewPassword = "Updated123!"
            });

        Assert.Equal("Mevcut şifre doğrulanamadı.", result.Error);

        var persisted = await harness.DbContext.Users.SingleAsync(user => user.Id == harness.User.Id);
        Assert.Equal(oldHash, persisted.PasswordHash);
    }

    private sealed class UserServiceHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private UserServiceHarness(
            SqliteConnection connection,
            AppDbContext dbContext,
            PasswordHasher<User> passwordHasher,
            User user,
            UserService service)
        {
            _connection = connection;
            DbContext = dbContext;
            PasswordHasher = passwordHasher;
            User = user;
            Service = service;
        }

        public AppDbContext DbContext { get; }
        public PasswordHasher<User> PasswordHasher { get; }
        public User User { get; }
        public UserService Service { get; }

        public static async Task<UserServiceHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            var dbContext = new AppDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();

            var passwordHasher = new PasswordHasher<User>();
            dbContext.Companies.Add(new Company
            {
                Id = 7,
                Name = "SupportFlow Test Company",
                CreatedAt = DateTime.UtcNow
            });

            var user = new User
            {
                FirstName = "Ayşe",
                LastName = "Yılmaz",
                Email = "customer@example.test",
                CompanyId = 7,
                Role = UserRole.Customer,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            user.PasswordHash = passwordHasher.HashPassword(user, "Current123!");

            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();

            var service = new UserService(dbContext, passwordHasher);
            return new UserServiceHarness(connection, dbContext, passwordHasher, user, service);
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
