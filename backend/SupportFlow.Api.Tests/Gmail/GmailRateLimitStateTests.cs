using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportFlow.Api.Data;
using SupportFlow.Api.Services.Gmail;
using SupportFlow.Api.Services.Gmail.Models;

namespace SupportFlow.Api.Tests.Gmail;

public sealed class GmailRateLimitStateTests
{
    [Fact]
    public void ProviderRetryTimestamp_ActivatesRateLimitAndStoresProviderTime()
    {
        var clock = new ManualTimeProvider();
        var state = new GmailApiRateLimitState(clock);
        var retryAt = clock.GetUtcNow().AddMinutes(12).UtcDateTime;

        var snapshot = state.RecordRateLimit(retryAt);

        Assert.True(snapshot.IsRateLimited);
        Assert.Equal(retryAt, snapshot.RateLimitedUntil);
        Assert.Equal(1, snapshot.ConsecutiveFailures);
        Assert.True(state.TryGetRateLimitedUntil(out var storedRetryAt));
        Assert.Equal(retryAt, storedRetryAt);
    }

    [Fact]
    public void MissingProviderRetry_UsesBoundedExponentialBackoff()
    {
        var clock = new ManualTimeProvider();
        var state = new GmailApiRateLimitState(clock);

        var first = state.RecordRateLimit(null);
        var second = state.RecordRateLimit(null);
        var third = state.RecordRateLimit(null);
        var fourth = state.RecordRateLimit(null);
        var fifth = state.RecordRateLimit(null);

        var now = clock.GetUtcNow().UtcDateTime;
        Assert.Equal(now.AddMinutes(2), first.RateLimitedUntil);
        Assert.Equal(now.AddMinutes(5), second.RateLimitedUntil);
        Assert.Equal(now.AddMinutes(10), third.RateLimitedUntil);
        Assert.Equal(now.AddMinutes(30), fourth.RateLimitedUntil);
        Assert.Equal(now.AddMinutes(30), fifth.RateLimitedUntil);
    }

    [Fact]
    public void SuccessfulGmailCall_ResetsRateLimitState()
    {
        var clock = new ManualTimeProvider();
        var state = new GmailApiRateLimitState(clock);
        _ = state.RecordRateLimit(clock.GetUtcNow().AddMinutes(5).UtcDateTime);

        state.MarkSuccessfulCall();

        var snapshot = state.GetSnapshot();
        Assert.False(snapshot.IsRateLimited);
        Assert.Null(snapshot.RateLimitedUntil);
        Assert.Equal(0, snapshot.ConsecutiveFailures);
    }

    [Fact]
    public void ProviderRetryTimestampParser_ParsesGoogleRateLimitMessage()
    {
        var retryAt = GmailApiRateLimitDetector.ParseProviderRetryAt(
            "User-rate limit exceeded. Retry after 2026-08-23T15:21:04.618Z [429]");

        Assert.Equal(
            new DateTime(2026, 8, 23, 15, 21, 4, 618, DateTimeKind.Utc),
            retryAt);
    }

    [Fact]
    public async Task DiagnosticsDuringCooldown_DoesNotCallGmail()
    {
        await using var harness = await DiagnosticsHarness.CreateAsync();
        _ = harness.RateLimitState.RecordRateLimit(
            harness.Clock.GetUtcNow().AddMinutes(2).UtcDateTime);

        await Assert.ThrowsAsync<GmailRateLimitedException>(
            () => harness.Service.GetRecentMessagesAsync());

        Assert.Equal(0, harness.Inbox.CallCount);
    }

    [Fact]
    public async Task DiagnosticsCache_PreventsRepeatedGmailCallsAndMarksCachedResponse()
    {
        await using var harness = await DiagnosticsHarness.CreateAsync();

        var first = await harness.Service.GetRecentMessagesAsync();
        var second = await harness.Service.GetRecentMessagesAsync();

        Assert.Equal(1, harness.Inbox.CallCount);
        Assert.False(first[0].IsCached);
        Assert.True(second[0].IsCached);
        Assert.NotNull(second[0].CachedAt);
    }

    [Fact]
    public async Task ExpiredDiagnosticsCache_AllowsTheNextRead()
    {
        await using var harness = await DiagnosticsHarness.CreateAsync();

        _ = await harness.Service.GetRecentMessagesAsync();
        harness.Clock.Advance(GmailDiagnosticsService.DiagnosticsCacheDuration.Add(TimeSpan.FromSeconds(1)));
        var refreshed = await harness.Service.GetRecentMessagesAsync();

        Assert.Equal(2, harness.Inbox.CallCount);
        Assert.False(refreshed[0].IsCached);
    }

    private sealed class DiagnosticsHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private DiagnosticsHarness(
            SqliteConnection connection,
            AppDbContext dbContext,
            ManualTimeProvider clock,
            GmailApiRateLimitState rateLimitState,
            FakeInbox inbox,
            GmailDiagnosticsService service)
        {
            _connection = connection;
            DbContext = dbContext;
            Clock = clock;
            RateLimitState = rateLimitState;
            Inbox = inbox;
            Service = service;
        }

        public AppDbContext DbContext { get; }
        public ManualTimeProvider Clock { get; }
        public GmailApiRateLimitState RateLimitState { get; }
        public FakeInbox Inbox { get; }
        public GmailDiagnosticsService Service { get; }

        public static async Task<DiagnosticsHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var dbContext = new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlite(connection)
                    .Options);
            await dbContext.Database.EnsureCreatedAsync();

            var clock = new ManualTimeProvider();
            var rateLimitState = new GmailApiRateLimitState(clock);
            var inbox = new FakeInbox();
            var service = new GmailDiagnosticsService(
                inbox,
                new NullSenderMatcher(),
                dbContext,
                new GmailDiagnosticsCache(clock),
                rateLimitState);

            return new DiagnosticsHarness(
                connection,
                dbContext,
                clock,
                rateLimitState,
                inbox,
                service);
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class FakeInbox : IGmailInboxService
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<GmailMessageDiagnostic>> GetRecentMessageDiagnosticsAsync(
            int maximumResults = 25,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult<IReadOnlyList<GmailMessageDiagnostic>>(
            [
                new GmailMessageDiagnostic(
                    "message-1",
                    "thread-1",
                    "Test",
                    "Requester <requester@example.test>",
                    "requester@example.test",
                    new DateTime(2026, 8, 23, 9, 0, 0, DateTimeKind.Utc),
                    "2026-08-23 12:00:00",
                    false,
                    0,
                    [])
            ]);
        }

        public Task<IReadOnlyList<string>> ListRecentInboxMessageIdsAsync(
            int maximumResults = 25,
            CancellationToken cancellationToken = default,
            GmailInboxDiscoveryQuery? discoveryQuery = null) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<InboundEmailMessage>> GetRecentInboxMessagesAsync(
            int maximumResults = 25,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<InboundEmailMessage>>([]);

        public Task<InboundEmailMessage?> GetMessageAsync(
            string gmailMessageId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<InboundEmailMessage?>(null);

        public Task<IReadOnlyList<InboundEmailAttachment>> ResolveMessageAttachmentsAsync(
            InboundEmailMessage message,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<InboundEmailAttachment>>([]);
    }

    private sealed class NullSenderMatcher : IInboundEmailSenderMatcher
    {
        public Task<SupportFlow.Api.Models.User?> FindByNormalizedEmailAsync(
            string normalizedEmail,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SupportFlow.Api.Models.User?>(null);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 8, 23, 9, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }
}
