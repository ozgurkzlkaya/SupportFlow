using SupportFlow.Api.Services.Gmail.Models;

namespace SupportFlow.Api.Services.Gmail;

public interface IGmailMailboxProcessingCoordinator
{
    bool IsProcessing { get; }

    Task<GmailMailboxProcessingSummary> ProcessAsync(
        CancellationToken cancellationToken = default,
        GmailMailboxProcessingMode mode = GmailMailboxProcessingMode.Manual);
}
