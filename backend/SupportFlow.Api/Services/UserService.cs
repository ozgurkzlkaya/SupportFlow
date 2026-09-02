using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SupportFlow.Api.Data;
using SupportFlow.Api.DTOs.Users;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;

namespace SupportFlow.Api.Services;

public class UserService
{
    private readonly AppDbContext _dbContext;
    private readonly IPasswordHasher<User> _passwordHasher;

    public UserService(
        AppDbContext dbContext,
        IPasswordHasher<User> passwordHasher)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
    }

    public async Task<IReadOnlyList<TechnicalStaffResponse>> GetActiveTechnicalStaffAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.IsActive && user.Role == UserRole.TechnicalStaff)
            .OrderBy(user => user.FirstName)
            .ThenBy(user => user.LastName)
            .Select(user => new TechnicalStaffResponse
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<UserProfileResponse?> GetProfileAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId && user.IsActive)
            .Select(user => new UserProfileResponse
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Role = user.Role,
                CompanyId = user.CompanyId,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt
            })
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<UpdateUserProfileResult> UpdateProfileAsync(
        int userId,
        UpdateUserProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            return UpdateUserProfileResult.Failed("Ad ve soyad zorunludur.");
        }

        if (firstName.Length > 100 || lastName.Length > 100)
        {
            return UpdateUserProfileResult.Failed("Ad ve soyad en fazla 100 karakter olabilir.");
        }

        var user = await _dbContext.Users
            .SingleOrDefaultAsync(item => item.Id == userId && item.IsActive, cancellationToken);

        if (user is null)
        {
            return UpdateUserProfileResult.NotFound();
        }

        user.FirstName = firstName;
        user.LastName = lastName;
        user.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return UpdateUserProfileResult.Succeeded(new UserProfileResponse
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Role = user.Role,
            CompanyId = user.CompanyId,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt
        });
    }

    public async Task<ChangePasswordResult> ChangePasswordAsync(
        int userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword)
            || string.IsNullOrWhiteSpace(request.NewPassword)
            || string.IsNullOrWhiteSpace(request.ConfirmNewPassword))
        {
            return ChangePasswordResult.Failed("Tüm şifre alanları zorunludur.");
        }

        if (request.NewPassword.Length < 8)
        {
            return ChangePasswordResult.Failed("Yeni şifre en az 8 karakter olmalıdır.");
        }

        if (request.NewPassword == request.CurrentPassword)
        {
            return ChangePasswordResult.Failed("Yeni şifre mevcut şifre ile aynı olamaz.");
        }

        if (request.NewPassword != request.ConfirmNewPassword)
        {
            return ChangePasswordResult.Failed("Yeni şifre alanları eşleşmiyor.");
        }

        var user = await _dbContext.Users
            .SingleOrDefaultAsync(item => item.Id == userId && item.IsActive, cancellationToken);

        if (user is null)
        {
            return ChangePasswordResult.NotFound();
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.CurrentPassword);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return ChangePasswordResult.Failed("Mevcut şifre doğrulanamadı.");
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ChangePasswordResult.Succeeded();
    }
}

public sealed record UpdateUserProfileResult(
    UserProfileResponse? Profile,
    bool Found,
    string? Error)
{
    public static UpdateUserProfileResult Succeeded(UserProfileResponse profile) =>
        new(profile, true, null);

    public static UpdateUserProfileResult Failed(string error) =>
        new(null, true, error);

    public static UpdateUserProfileResult NotFound() =>
        new(null, false, null);
}

public sealed record ChangePasswordResult(
    bool Found,
    string? Error)
{
    public static ChangePasswordResult Succeeded() => new(true, null);

    public static ChangePasswordResult Failed(string error) => new(true, error);

    public static ChangePasswordResult NotFound() => new(false, null);
}
