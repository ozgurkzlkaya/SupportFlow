using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SupportFlow.Api.Data;
using SupportFlow.Api.DTOs.Auth;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;
using SupportFlow.Api.Settings;

namespace SupportFlow.Api.Services;

public class AuthService
{
    private readonly AppDbContext _dbContext;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly JwtSettings _jwtSettings;

    public AuthService(
        AppDbContext dbContext,
        IPasswordHasher<User> passwordHasher,
        IOptions<JwtSettings> jwtOptions)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _jwtSettings = jwtOptions.Value;
    }

    public async Task<RegisterUserResult> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var emailExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Email == normalizedEmail, cancellationToken);

        if (emailExists)
        {
            return RegisterUserResult.Failed(
                RegisterUserFailure.DuplicateEmail,
                "A user with this email address already exists.");
        }

        if (request.CompanyId.HasValue)
        {
            var companyExists = await _dbContext.Companies
                .AsNoTracking()
                .AnyAsync(company => company.Id == request.CompanyId.Value, cancellationToken);

            if (!companyExists)
            {
                return RegisterUserResult.Failed(
                    RegisterUserFailure.CompanyNotFound,
                    "The selected company does not exist.");
            }
        }

        var user = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = normalizedEmail,
            CompanyId = request.CompanyId,
            // Public registration never accepts a privilege level from the client.
            Role = UserRole.Customer,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        _dbContext.Users.Add(user);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            _dbContext.Entry(user).State = EntityState.Detached;
            return RegisterUserResult.Failed(
                RegisterUserFailure.DuplicateEmail,
                "A user with this email address already exists.");
        }

        return RegisterUserResult.Succeeded(ToAuthUserResponse(user));
    }

    public async Task<LoginResponse?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return null;
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return null;
        }

        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new LoginResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAt = expiresAt,
            User = ToAuthUserResponse(user)
        };
    }

    public async Task<AuthUserResponse?> GetCurrentUserAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                user => user.Id == userId && user.IsActive,
                cancellationToken);

        return user is null ? null : ToAuthUserResponse(user);
    }

    private static AuthUserResponse ToAuthUserResponse(User user)
    {
        return new AuthUserResponse
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Role = user.Role,
            CompanyId = user.CompanyId,
            CreatedAt = user.CreatedAt
        };
    }
}

public enum RegisterUserFailure
{
    None,
    DuplicateEmail,
    CompanyNotFound
}

public sealed record RegisterUserResult(
    AuthUserResponse? User,
    RegisterUserFailure Failure,
    string? Error)
{
    public static RegisterUserResult Succeeded(AuthUserResponse user) =>
        new(user, RegisterUserFailure.None, null);

    public static RegisterUserResult Failed(RegisterUserFailure failure, string error) =>
        new(null, failure, error);
}
