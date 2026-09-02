using Microsoft.EntityFrameworkCore;
using SupportFlow.Api.Data;
using SupportFlow.Api.Models;

namespace SupportFlow.Api.Services.Gmail;

public interface IExternalRequesterService
{
    Task<ExternalRequesterResolution> GetOrCreateAsync(
        string email,
        string? displayName,
        CancellationToken cancellationToken = default);
}

public sealed record ExternalRequesterResolution(
    ExternalRequester Requester,
    bool WasCreated);

public sealed class ExternalRequesterService(AppDbContext dbContext) : IExternalRequesterService
{
    public async Task<ExternalRequesterResolution> GetOrCreateAsync(
        string email,
        string? displayName,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = IInboundEmailSenderMatcher.NormalizeEmail(email);
        var requester = await dbContext.ExternalRequesters
            .SingleOrDefaultAsync(
                item => item.NormalizedEmail == normalizedEmail,
                cancellationToken);

        if (requester is not null)
        {
            if (string.IsNullOrWhiteSpace(requester.DisplayName)
                && !string.IsNullOrWhiteSpace(displayName))
            {
                requester.DisplayName = NormalizeDisplayName(displayName);
                requester.UpdatedAt = DateTime.UtcNow;
            }

            return new ExternalRequesterResolution(requester, false);
        }

        requester = new ExternalRequester
        {
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail,
            DisplayName = NormalizeDisplayName(displayName),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.ExternalRequesters.Add(requester);
        return new ExternalRequesterResolution(requester, true);
    }

    private static string? NormalizeDisplayName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= 200 ? normalized : normalized[..200];
    }
}
