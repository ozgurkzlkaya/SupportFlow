using SupportFlow.Api.Services.Gmail.Models;

namespace SupportFlow.Api.Services.Gmail;

public interface IGmailInboxService
{
    Task<IReadOnlyList<string>> ListRecentInboxMessageIdsAsync(
        int maximumResults = 25,
        CancellationToken cancellationToken = default,
        GmailInboxDiscoveryQuery? discoveryQuery = null);

    Task<IReadOnlyList<InboundEmailMessage>> GetRecentInboxMessagesAsync(
        int maximumResults = 25,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GmailMessageDiagnostic>> GetRecentMessageDiagnosticsAsync(
        int maximumResults = 25,
        CancellationToken cancellationToken = default);

    Task<InboundEmailMessage?> GetMessageAsync(
        string gmailMessageId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InboundEmailAttachment>> ResolveMessageAttachmentsAsync(
        InboundEmailMessage message,
        CancellationToken cancellationToken = default);
}
