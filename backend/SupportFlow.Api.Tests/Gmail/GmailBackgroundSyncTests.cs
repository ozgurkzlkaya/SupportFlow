using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SupportFlow.Api.Services.Gmail;
using SupportFlow.Api.Services.Gmail.Models;
using SupportFlow.Api.Settings;

namespace SupportFlow.Api.Tests.Gmail;

public sealed class GmailBackgroundSyncTests
{
    [Fact]
    public async Task DisabledService_DoesNotProcessGmail()
    {
        var coordinator = new FakeCoordinator();
        var service = CreateService(enabled: false, coordinator, out _);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(0, coordinator.CallCount);
    }

    [Fact]
    public async Task Cycle_UsesExistingMailboxPipelineAndUpdatesSuccessfulStatus()
    {
        var expectedSummary = CreateSummary(
            processed: 4,
            newTickets: 1,
            newExternalTickets: 1,
            messagesAdded: 1,
            alreadyProcessed: 1);
        var coordinator = new FakeCoordinator { Result = expectedSummary };
        var service = CreateService(enabled: true, coordinator, out var status);

        await service.ProcessCycleAsync();

        var snapshot = status.GetSnapshot(enabled: true, intervalSeconds: 60);
        Assert.Equal(1, coordinator.CallCount);
        Assert.False(snapshot.IsRunning);
        Assert.NotNull(snapshot.LastStartedAt);
        Assert.NotNull(snapshot.LastCompletedAt);
        Assert.NotNull(snapshot.LastSuccessfulAt);
        Assert.Null(snapshot.LastError);
        Assert.NotNull(snapshot.LastSummary);
        Assert.Equal(4, snapshot.LastSummary.Examined);
        Assert.Equal(1, snapshot.LastSummary.AlreadyProcessed);
        Assert.Equal(GmailMailboxProcessingMode.Background, coordinator.LastMode);
    }

    [Fact]
    public void Settings_DefaultToConservativeDisabledTenMinutePolling()
    {
        var settings = new GmailBackgroundSyncSettings();

        Assert.False(settings.Enabled);
        Assert.Equal(600, settings.IntervalSeconds);
        Assert.Equal(30, settings.InitialDelaySeconds);
    }

    [Fact]
    public async Task Cycle_FailureDoesNotStopFutureCyclesAndExposesOnlySafeError()
    {
        var coordinator = new FakeCoordinator
        {
            Exception = new InvalidOperationException("Sensitive Gmail implementation detail.")
        };
        var service = CreateService(enabled: true, coordinator, out var status);

        await service.ProcessCycleAsync();
        coordinator.Exception = null;
        coordinator.Result = CreateSummary(processed: 1, alreadyProcessed: 1);
        await service.ProcessCycleAsync();

        var snapshot = status.GetSnapshot(enabled: true, intervalSeconds: 60);
        Assert.Equal(2, coordinator.CallCount);
        Assert.Null(snapshot.LastError);
        Assert.NotNull(snapshot.LastSuccessfulAt);
        Assert.Equal(1, snapshot.LastSummary!.AlreadyProcessed);
    }

    [Fact]
    public async Task Cycle_WhenAuthorizationIsRequired_RecordsSafeErrorWithoutThrowing()
    {
        var coordinator = new FakeCoordinator
        {
            Exception = new GmailAuthorizationRequiredException("Raw authorization failure.")
        };
        var service = CreateService(enabled: true, coordinator, out var status);

        await service.ProcessCycleAsync();

        var snapshot = status.GetSnapshot(enabled: true, intervalSeconds: 60);
        Assert.Equal("Gmail yetkilendirmesi gerekli.", snapshot.LastError);
        Assert.False(snapshot.IsRunning);
    }

    [Fact]
    public async Task Cycle_DuringRateLimit_DoesNotCallTheCoordinator()
    {
        var clock = new ManualTimeProvider();
        var rateLimitState = new GmailApiRateLimitState(clock);
        _ = rateLimitState.RecordRateLimit(clock.GetUtcNow().AddMinutes(2).UtcDateTime);
        var coordinator = new FakeCoordinator();
        var status = new GmailBackgroundSyncRuntimeStatus(clock, rateLimitState);
        var service = new GmailInboxBackgroundService(
            Options.Create(new GmailBackgroundSyncSettings { Enabled = true, IntervalSeconds = 60 }),
            coordinator,
            status,
            rateLimitState,
            NullLogger<GmailInboxBackgroundService>.Instance,
            clock);

        await service.ProcessCycleAsync();

        var snapshot = status.GetSnapshot(enabled: true, intervalSeconds: 60);
        Assert.Equal(0, coordinator.CallCount);
        Assert.True(snapshot.IsRateLimited);
        Assert.NotNull(snapshot.RateLimitedUntil);
    }

    [Fact]
    public async Task Cycle_AfterRateLimitExpires_ResumesProcessing()
    {
        var clock = new ManualTimeProvider();
        var rateLimitState = new GmailApiRateLimitState(clock);
        _ = rateLimitState.RecordRateLimit(clock.GetUtcNow().AddMinutes(2).UtcDateTime);
        var coordinator = new FakeCoordinator { Result = CreateSummary(processed: 1) };
        var status = new GmailBackgroundSyncRuntimeStatus(clock, rateLimitState);
        var service = new GmailInboxBackgroundService(
            Options.Create(new GmailBackgroundSyncSettings { Enabled = true, IntervalSeconds = 60 }),
            coordinator,
            status,
            rateLimitState,
            NullLogger<GmailInboxBackgroundService>.Instance,
            clock);

        clock.Advance(TimeSpan.FromMinutes(2));
        await service.ProcessCycleAsync();

        Assert.Equal(1, coordinator.CallCount);
        Assert.False(status.GetSnapshot(enabled: true, intervalSeconds: 60).IsRateLimited);
    }

    [Fact]
    public async Task Coordinator_DuringRateLimit_DoesNotResolveTheMailboxProcessor()
    {
        var processor = new BlockingMailboxProcessor();
        var clock = new ManualTimeProvider();
        var rateLimitState = new GmailApiRateLimitState(clock);
        _ = rateLimitState.RecordRateLimit(clock.GetUtcNow().AddMinutes(2).UtcDateTime);
        await using var provider = new ServiceCollection()
            .AddScoped<IGmailMailboxProcessor>(_ => processor)
            .BuildServiceProvider();
        var coordinator = new GmailMailboxProcessingCoordinator(
            provider.GetRequiredService<IServiceScopeFactory>(),
            rateLimitState);

        await Assert.ThrowsAsync<GmailRateLimitedException>(() => coordinator.ProcessAsync());

        Assert.Equal(0, processor.CallCount);
    }

    [Fact]
    public async Task ServiceCancellationDuringInitialDelay_StopsCleanlyWithoutProcessing()
    {
        var coordinator = new FakeCoordinator();
        var service = CreateService(enabled: true, coordinator, out _, initialDelaySeconds: 30);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(0, coordinator.CallCount);
    }

    [Fact]
    public async Task Coordinator_SerializesManualAndBackgroundRequests()
    {
        var processor = new BlockingMailboxProcessor();
        await using var provider = new ServiceCollection()
            .AddScoped<IGmailMailboxProcessor>(_ => processor)
            .BuildServiceProvider();
        var coordinator = new GmailMailboxProcessingCoordinator(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new GmailApiRateLimitState(TimeProvider.System));

        var automaticCycle = coordinator.ProcessAsync();
        await processor.FirstInvocationStarted.Task;
        var manualRequest = coordinator.ProcessAsync();

        Assert.True(coordinator.IsProcessing);
        Assert.Equal(1, processor.CallCount);

        processor.ReleaseFirstInvocation.SetResult();
        await Task.WhenAll(automaticCycle, manualRequest);

        Assert.Equal(2, processor.CallCount);
        Assert.Equal(1, processor.MaximumConcurrentCalls);
        Assert.False(coordinator.IsProcessing);
    }

    private static GmailInboxBackgroundService CreateService(
        bool enabled,
        FakeCoordinator coordinator,
        out GmailBackgroundSyncRuntimeStatus status,
        int initialDelaySeconds = 0)
    {
        var rateLimitState = new GmailApiRateLimitState(TimeProvider.System);
        status = new GmailBackgroundSyncRuntimeStatus(TimeProvider.System, rateLimitState);
        return new GmailInboxBackgroundService(
            Options.Create(new GmailBackgroundSyncSettings
            {
                Enabled = enabled,
                IntervalSeconds = 60,
                InitialDelaySeconds = initialDelaySeconds
            }),
            coordinator,
            status,
            rateLimitState,
            NullLogger<GmailInboxBackgroundService>.Instance,
            TimeProvider.System);
    }

    private static GmailMailboxProcessingSummary CreateSummary(
        int processed = 0,
        int newTickets = 0,
        int newExternalTickets = 0,
        int messagesAdded = 0,
        int alreadyProcessed = 0) =>
        new(
            processed,
            newTickets,
            messagesAdded,
            alreadyProcessed,
            UnknownCustomers: 0,
            SenderMismatches: 0,
            SelfMessagesIgnored: 0,
            Failed: 0,
            Results: [],
            NewExternalTickets: newExternalTickets);

    private sealed class FakeCoordinator : IGmailMailboxProcessingCoordinator
    {
        public int CallCount { get; private set; }
        public GmailMailboxProcessingMode? LastMode { get; private set; }
        public Exception? Exception { get; set; }
        public GmailMailboxProcessingSummary Result { get; set; } = CreateSummary();
        public bool IsProcessing => false;

        public Task<GmailMailboxProcessingSummary> ProcessAsync(
            CancellationToken cancellationToken = default,
            GmailMailboxProcessingMode mode = GmailMailboxProcessingMode.Manual)
        {
            CallCount++;
            LastMode = mode;
            if (Exception is not null)
            {
                return Task.FromException<GmailMailboxProcessingSummary>(Exception);
            }

            return Task.FromResult(Result);
        }
    }

    private sealed class BlockingMailboxProcessor : IGmailMailboxProcessor
    {
        private int _concurrentCalls;

        public int CallCount { get; private set; }
        public int MaximumConcurrentCalls { get; private set; }
        public TaskCompletionSource FirstInvocationStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirstInvocation { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<GmailMailboxProcessingSummary> ProcessAsync(
            CancellationToken cancellationToken = default,
            GmailMailboxProcessingMode mode = GmailMailboxProcessingMode.Manual)
        {
            CallCount++;
            var concurrentCalls = Interlocked.Increment(ref _concurrentCalls);
            MaximumConcurrentCalls = Math.Max(MaximumConcurrentCalls, concurrentCalls);

            try
            {
                if (CallCount == 1)
                {
                    FirstInvocationStarted.SetResult();
                    await ReleaseFirstInvocation.Task.WaitAsync(cancellationToken);
                }

                return CreateSummary(processed: 1, alreadyProcessed: 1);
            }
            finally
            {
                Interlocked.Decrement(ref _concurrentCalls);
            }
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 8, 23, 9, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }
}
