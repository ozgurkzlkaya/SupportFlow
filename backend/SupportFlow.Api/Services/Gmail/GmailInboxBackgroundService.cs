using Microsoft.Extensions.Options;
using SupportFlow.Api.Services.Gmail.Models;
using SupportFlow.Api.Settings;

namespace SupportFlow.Api.Services.Gmail;

public sealed class GmailInboxBackgroundService : BackgroundService
{
    private readonly GmailBackgroundSyncSettings _settings;
    private readonly IGmailMailboxProcessingCoordinator _processingCoordinator;
    private readonly GmailBackgroundSyncRuntimeStatus _runtimeStatus;
    private readonly IGmailApiRateLimitState _rateLimitState;
    private readonly ILogger<GmailInboxBackgroundService> _logger;
    private readonly TimeProvider _timeProvider;

    public GmailInboxBackgroundService(
        IOptions<GmailBackgroundSyncSettings> settings,
        IGmailMailboxProcessingCoordinator processingCoordinator,
        GmailBackgroundSyncRuntimeStatus runtimeStatus,
        IGmailApiRateLimitState rateLimitState,
        ILogger<GmailInboxBackgroundService> logger,
        TimeProvider timeProvider)
    {
        _settings = settings.Value;
        _processingCoordinator = processingCoordinator;
        _runtimeStatus = runtimeStatus;
        _rateLimitState = rateLimitState;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Gmail background synchronization is disabled.");
            return;
        }

        _logger.LogInformation(
            "Gmail background synchronization started with interval {IntervalSeconds} seconds.",
            _settings.IntervalSeconds);

        try
        {
            if (_settings.InitialDelaySeconds > 0)
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(_settings.InitialDelaySeconds),
                    _timeProvider,
                    stoppingToken);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                await ProcessCycleAsync(stoppingToken);
                await Task.Delay(
                    TimeSpan.FromSeconds(_settings.IntervalSeconds),
                    _timeProvider,
                    stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Gmail background synchronization stopped.");
        }
    }

    internal async Task ProcessCycleAsync(CancellationToken cancellationToken = default)
    {
        if (_rateLimitState.TryGetRateLimitedUntil(out var retryAt))
        {
            _runtimeStatus.MarkRateLimited(retryAt);
            _logger.LogInformation(
                "Skipped Gmail background synchronization because the provider rate limit is active until {RetryAtUtc}.",
                retryAt);
            return;
        }

        _runtimeStatus.MarkStarted();
        _logger.LogInformation("Gmail background synchronization cycle started.");

        try
        {
            var summary = await _processingCoordinator.ProcessAsync(
                cancellationToken,
                GmailMailboxProcessingMode.Background);
            _runtimeStatus.MarkSucceeded(summary);
            _logger.LogInformation(
                "Gmail background synchronization completed. ListedCandidates: {ListedCandidates}; AlreadyProcessedSkipped: {AlreadyProcessedSkipped}; UnseenFetched: {UnseenFetched}; AttachmentsFetched: {AttachmentsFetched}; NewTickets: {NewTickets}; NewExternalTickets: {NewExternalTickets}; MessagesAdded: {MessagesAdded}; Failed: {Failed}.",
                summary.ListedCandidates,
                summary.AlreadyProcessedSkipped,
                summary.UnseenFetched,
                summary.AttachmentsFetched,
                summary.NewTickets,
                summary.NewExternalTickets,
                summary.MessagesAdded,
                summary.Failed);
        }
        catch (OperationCanceledException)
        {
            _runtimeStatus.MarkFailed("Otomatik senkronizasyon iptal edildi.");
            throw;
        }
        catch (GmailAuthorizationRequiredException exception)
        {
            _runtimeStatus.MarkFailed("Gmail yetkilendirmesi gerekli.");
            _logger.LogWarning(exception, "Gmail background synchronization requires authorization.");
        }
        catch (GmailIntegrationUnavailableException exception)
        {
            _runtimeStatus.MarkFailed("Gmail entegrasyonu kullanılamıyor.");
            _logger.LogWarning(exception, "Gmail background synchronization is unavailable.");
        }
        catch (GmailRateLimitedException exception)
        {
            _runtimeStatus.MarkRateLimited(exception.RetryAtUtc);
            _logger.LogWarning(
                "Gmail background synchronization was rate limited until {RetryAtUtc}.",
                exception.RetryAtUtc);
        }
        catch (Exception exception)
        {
            _runtimeStatus.MarkFailed("Otomatik senkronizasyon sırasında hata oluştu.");
            _logger.LogError(exception, "Gmail background synchronization cycle failed.");
        }
    }
}
