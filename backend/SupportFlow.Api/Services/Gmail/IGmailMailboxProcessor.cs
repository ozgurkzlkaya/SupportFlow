using SupportFlow.Api.Services.Gmail.Models;

namespace SupportFlow.Api.Services.Gmail;

public interface IGmailMailboxProcessor
{
    Task<GmailMailboxProcessingSummary> ProcessAsync(
        CancellationToken cancellationToken = default,
        GmailMailboxProcessingMode mode = GmailMailboxProcessingMode.Manual);
}
