using Microsoft.EntityFrameworkCore;
using SupportFlow.Api.Data;
using SupportFlow.Api.Services.Gmail.Models;

namespace SupportFlow.Api.Services.Gmail;

public sealed class GmailDiagnosticsService(
    IGmailInboxService gmailInboxService,
    IInboundEmailSenderMatcher senderMatcher,
    AppDbContext dbContext,
    GmailDiagnosticsCache diagnosticsCache,
    IGmailApiRateLimitState rateLimitState)
{
    private const int MaximumMessages = 25;

    public async Task<IReadOnlyList<GmailMessageDiagnosticResponse>> GetRecentMessagesAsync(
        CancellationToken cancellationToken = default)
    {
        var isCached = diagnosticsCache.TryGet(out IReadOnlyList<GmailMessageDiagnostic>? messages, out var cachedAt);

        if (!isCached)
        {
            if (rateLimitState.TryGetRateLimitedUntil(out var retryAt))
            {
                throw new GmailRateLimitedException(retryAt);
            }

            messages = await gmailInboxService.GetRecentMessageDiagnosticsAsync(
                MaximumMessages,
                cancellationToken);
            diagnosticsCache.Set(messages);
            _ = diagnosticsCache.TryGet(out _, out cachedAt);
        }

        var results = new List<GmailMessageDiagnosticResponse>(messages!.Count);
        foreach (var message in messages)
        {
            var user = string.IsNullOrWhiteSpace(message.ParsedSenderEmail)
                ? null
                : await senderMatcher.FindByNormalizedEmailAsync(
                    IInboundEmailSenderMatcher.NormalizeEmail(message.ParsedSenderEmail),
                    cancellationToken);
            var isAlreadyProcessed = await dbContext.ProcessedEmailMessages
                .AsNoTracking()
                .AnyAsync(
                    processed => processed.GmailMessageId == message.GmailMessageId,
                    cancellationToken);
            var externalRequester = user is null && !string.IsNullOrWhiteSpace(message.ParsedSenderEmail)
                ? await dbContext.ExternalRequesters
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        requester => requester.NormalizedEmail == IInboundEmailSenderMatcher.NormalizeEmail(message.ParsedSenderEmail),
                        cancellationToken)
                : null;

            results.Add(new GmailMessageDiagnosticResponse(
                message.GmailMessageId,
                message.GmailThreadId,
                message.Subject,
                message.RawFrom,
                message.ParsedSenderEmail,
                message.InternalDateUtc,
                message.InternalDateEuropeIstanbul,
                message.HasAttachments,
                message.AttachmentCount,
                message.AttachmentFileNames,
                isAlreadyProcessed,
                user is not null,
                user?.Id,
                user?.Role,
                user?.IsActive,
                user is not null,
                externalRequester is not null,
                externalRequester?.Id,
                user is not null
                    ? "RegisteredUser"
                    : externalRequester is not null
                        ? "ExternalRequester"
                        : "Unmatched",
                isCached,
                cachedAt));
        }

        return results;
    }

    internal static TimeSpan DiagnosticsCacheDuration => GmailDiagnosticsCache.TimeToLive;
}
