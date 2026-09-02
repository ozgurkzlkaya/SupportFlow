using SupportFlow.Api.DTOs.Gmail;
using SupportFlow.Api.Services.Gmail.Models;

namespace SupportFlow.Api.Services.Gmail;

public sealed class GmailBackgroundSyncRuntimeStatus
{
    private readonly object _syncRoot = new();
    private readonly TimeProvider _timeProvider;
    private readonly IGmailApiRateLimitState _rateLimitState;
    private bool _isRunning;
    private DateTime? _lastStartedAt;
    private DateTime? _lastCompletedAt;
    private DateTime? _lastSuccessfulAt;
    private string? _lastError;
    private GmailBackgroundSyncSummaryResponse? _lastSummary;

    public GmailBackgroundSyncRuntimeStatus(
        TimeProvider timeProvider,
        IGmailApiRateLimitState rateLimitState)
    {
        _timeProvider = timeProvider;
        _rateLimitState = rateLimitState;
    }

    public void MarkStarted()
    {
        lock (_syncRoot)
        {
            _isRunning = true;
            _lastStartedAt = GetUtcNow();
            _lastError = null;
        }
    }

    public void MarkSucceeded(GmailMailboxProcessingSummary summary)
    {
        lock (_syncRoot)
        {
            var completedAt = GetUtcNow();
            _isRunning = false;
            _lastCompletedAt = completedAt;
            _lastSuccessfulAt = completedAt;
            _lastError = null;
            _lastSummary = new GmailBackgroundSyncSummaryResponse(
                summary.Processed,
                summary.NewTickets,
                summary.NewExternalTickets,
                summary.MessagesAdded,
                summary.AlreadyProcessed,
                summary.Failed);
        }
    }

    public void MarkFailed(string safeError)
    {
        lock (_syncRoot)
        {
            _isRunning = false;
            _lastCompletedAt = GetUtcNow();
            _lastError = safeError;
        }
    }

    public void MarkRateLimited(DateTime retryAtUtc)
    {
        lock (_syncRoot)
        {
            _isRunning = false;
            _lastCompletedAt = GetUtcNow();
            _lastError = "Gmail API geçici olarak hız sınırında.";
        }
    }

    public GmailBackgroundSyncStatusResponse GetSnapshot(bool enabled, int intervalSeconds)
    {
        var rateLimit = _rateLimitState.GetSnapshot();

        lock (_syncRoot)
        {
            return new GmailBackgroundSyncStatusResponse
            {
                Enabled = enabled,
                IntervalSeconds = intervalSeconds,
                IsRunning = _isRunning,
                LastStartedAt = _lastStartedAt,
                LastCompletedAt = _lastCompletedAt,
                LastSuccessfulAt = _lastSuccessfulAt,
                LastError = _lastError,
                LastSummary = _lastSummary,
                IsRateLimited = rateLimit.IsRateLimited,
                RateLimitedUntil = rateLimit.RateLimitedUntil,
                ConsecutiveFailures = rateLimit.ConsecutiveFailures
            };
        }
    }

    private DateTime GetUtcNow() => _timeProvider.GetUtcNow().UtcDateTime;
}
