using Microsoft.EntityFrameworkCore;
using SupportFlow.Api.Data;
using SupportFlow.Api.Models;

namespace SupportFlow.Api.Services.Gmail;

public interface IInboundEmailSenderMatcher
{
    Task<User?> FindByNormalizedEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default);

    static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}

public sealed class InboundEmailSenderMatcher(AppDbContext dbContext) : IInboundEmailSenderMatcher
{
    public Task<User?> FindByNormalizedEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default) =>
        dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);
}
