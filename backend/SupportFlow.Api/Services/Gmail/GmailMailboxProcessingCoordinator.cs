using SupportFlow.Api.Services.Gmail.Models;

namespace SupportFlow.Api.Services.Gmail;

public sealed class GmailMailboxProcessingCoordinator : IGmailMailboxProcessingCoordinator
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IGmailApiRateLimitState _rateLimitState;
    private readonly SemaphoreSlim _processingLock = new(1, 1);
    private int _isProcessing;

    public GmailMailboxProcessingCoordinator(
        IServiceScopeFactory scopeFactory,
        IGmailApiRateLimitState rateLimitState)
    {
        _scopeFactory = scopeFactory;
        _rateLimitState = rateLimitState;
    }

    public bool IsProcessing => Volatile.Read(ref _isProcessing) == 1;

    public async Task<GmailMailboxProcessingSummary> ProcessAsync(
        CancellationToken cancellationToken = default,
        GmailMailboxProcessingMode mode = GmailMailboxProcessingMode.Manual)
    {
        if (_rateLimitState.TryGetRateLimitedUntil(out var retryAt))
        {
            throw new GmailRateLimitedException(retryAt);
        }

        await _processingLock.WaitAsync(cancellationToken);
        Interlocked.Exchange(ref _isProcessing, 1);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<IGmailMailboxProcessor>();
            return await processor.ProcessAsync(cancellationToken, mode);
        }
        finally
        {
            Interlocked.Exchange(ref _isProcessing, 0);
            _processingLock.Release();
        }
    }
}
