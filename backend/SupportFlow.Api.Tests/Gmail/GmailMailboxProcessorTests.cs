using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SupportFlow.Api.Services.Gmail;
using SupportFlow.Api.Services.Gmail.Models;
using SupportFlow.Api.Settings;

namespace SupportFlow.Api.Tests.Gmail;

public sealed class GmailMailboxProcessorTests
{
    [Fact]
    public async Task UnreadNewEmail_IsProcessedIntoNewTicket()
    {
        var processor = CreateProcessor(
            ["unread-new-message"],
            new Dictionary<string, InboundEmailProcessingResult>
            {
                ["unread-new-message"] = InboundEmailProcessingResult.NewTicket(
                    customerId: 1,
                    ticketId: 10,
                    ticketNumber: "SUP-2026-000010")
            });

        var summary = await processor.ProcessAsync();

        Assert.Equal(1, summary.Processed);
        Assert.Equal(1, summary.NewTickets);
        Assert.Equal(0, summary.AlreadyProcessed);
    }

    [Fact]
    public async Task AlreadyReadNewEmail_IsStillDiscoveredAndProcessed()
    {
        var processor = CreateProcessor(
            ["read-new-message"],
            new Dictionary<string, InboundEmailProcessingResult>
            {
                ["read-new-message"] = InboundEmailProcessingResult.NewTicket(
                    customerId: 1,
                    ticketId: 11,
                    ticketNumber: "SUP-2026-000011")
            });

        var summary = await processor.ProcessAsync();

        Assert.Equal(1, summary.Processed);
        Assert.Equal(1, summary.NewTickets);
        Assert.Equal(0, summary.AlreadyProcessed);
    }

    [Fact]
    public async Task PreviouslyProcessedEmail_IsCountedAsAlreadyProcessed()
    {
        var processor = CreateProcessor(
            ["processed-message"],
            new Dictionary<string, InboundEmailProcessingResult>
            {
                ["processed-message"] = InboundEmailProcessingResult.AlreadyProcessed(
                    ticketId: 12,
                    ticketNumber: "SUP-2026-000012")
            });

        var summary = await processor.ProcessAsync();

        Assert.Equal(1, summary.Processed);
        Assert.Equal(0, summary.NewTickets);
        Assert.Equal(1, summary.AlreadyProcessed);
        var item = Assert.Single(summary.Results!);
        Assert.Equal(InboundEmailProcessingStatus.AlreadyProcessed, item.Status);
        Assert.Equal(12, item.TicketId);
        Assert.Equal("SUP-2026-000012", item.TicketNumber);
        Assert.Equal("The Gmail message was already processed.", item.Reason);
    }

    [Fact]
    public async Task PreviouslyProcessedSystemMail_RemainsBatchDeduplicatedWithoutFullRead()
    {
        var inbox = new FakeGmailInboxService(["previously-processed-bounce"]);
        var lookup = new FakeProcessedMessageLookup(
            new ProcessedGmailMessageMatch("previously-processed-bounce", 12, "SUP-2026-000012"));
        var processor = CreateProcessor(
            inbox,
            lookup,
            new Dictionary<string, InboundEmailProcessingResult>());

        var summary = await processor.ProcessAsync();

        Assert.Equal(1, summary.AlreadyProcessed);
        Assert.Equal(0, summary.SystemMessagesSkipped);
        Assert.Empty(inbox.MessageGetIds);
    }

    [Fact]
    public async Task SystemGeneratedMessage_IsCountedSeparatelyInTheSummary()
    {
        var processor = CreateProcessor(
            ["delivery-status"],
            new Dictionary<string, InboundEmailProcessingResult>
            {
                ["delivery-status"] = InboundEmailProcessingResult.SystemMessageSkipped()
            });

        var summary = await processor.ProcessAsync();

        Assert.Equal(1, summary.Processed);
        Assert.Equal(1, summary.SystemMessagesSkipped);
        Assert.Equal(0, summary.NewTickets);
        Assert.Equal(0, summary.MessagesAdded);
    }

    [Fact]
    public async Task UnknownCustomer_ExposesTheExactRejectionReasonInTheSummary()
    {
        var processor = CreateProcessor(
            ["unknown-sender"],
            new Dictionary<string, InboundEmailProcessingResult>
            {
                ["unknown-sender"] = InboundEmailProcessingResult.UnknownCustomer(
                    "The sender email is not registered in SupportFlow.")
            });

        var summary = await processor.ProcessAsync();

        Assert.Equal(1, summary.UnknownCustomers);
        var item = Assert.Single(summary.Results!);
        Assert.Equal(InboundEmailProcessingStatus.UnknownCustomer, item.Status);
        Assert.Equal("The sender email is not registered in SupportFlow.", item.Reason);
    }

    [Fact]
    public async Task SelfSentEmail_IsIgnoredWithoutCreatingTicket()
    {
        var processor = CreateProcessor(
            ["self-sent-message"],
            new Dictionary<string, InboundEmailProcessingResult>
            {
                ["self-sent-message"] = InboundEmailProcessingResult.SelfMessageIgnored()
            });

        var summary = await processor.ProcessAsync();

        Assert.Equal(1, summary.Processed);
        Assert.Equal(0, summary.NewTickets);
        Assert.Equal(1, summary.SelfMessagesIgnored);
    }

    [Fact]
    public async Task ReadUnreadStateDoesNotControlAlreadyProcessedDeduplication()
    {
        var processor = CreateProcessor(
            ["read-duplicate", "unread-duplicate"],
            new Dictionary<string, InboundEmailProcessingResult>
            {
                ["read-duplicate"] = InboundEmailProcessingResult.AlreadyProcessed(
                    ticketId: 13,
                    ticketNumber: "SUP-2026-000013"),
                ["unread-duplicate"] = InboundEmailProcessingResult.AlreadyProcessed(
                    ticketId: 13,
                    ticketNumber: "SUP-2026-000013")
            });

        var summary = await processor.ProcessAsync();

        Assert.Equal(2, summary.Processed);
        Assert.Equal(2, summary.AlreadyProcessed);
        Assert.Equal(0, summary.NewTickets);
    }

    [Fact]
    public async Task UsesBoundedRecentInboxFetchLimit()
    {
        var inbox = new FakeGmailInboxService(["recent-message"]);
        var inboundProcessor = new FakeInboundEmailProcessor(
            new Dictionary<string, InboundEmailProcessingResult>
            {
                ["recent-message"] = InboundEmailProcessingResult.NewTicket(
                    customerId: 1,
                    ticketId: 14,
                    ticketNumber: "SUP-2026-000014")
            });
        var processor = new GmailMailboxProcessor(
            inbox,
            inboundProcessor,
            new FakeProcessedMessageLookup(),
            CreateDiscoveryState(),
            TimeProvider.System,
            NullLogger<GmailMailboxProcessor>.Instance);

        _ = await processor.ProcessAsync();

        Assert.Equal(100, inbox.LastMaximumResults);
    }

    [Fact]
    public async Task ProcessAsync_BatchSkipsKnownProcessedMessagesBeforeFullRead()
    {
        var inbox = new FakeGmailInboxService(["processed", "unseen"]);
        var lookup = new FakeProcessedMessageLookup(
            new ProcessedGmailMessageMatch("processed", 18, "SUP-2026-000018"));
        var processor = CreateProcessor(inbox, lookup, new Dictionary<string, InboundEmailProcessingResult>
        {
            ["unseen"] = InboundEmailProcessingResult.NewTicket(1, 19, "SUP-2026-000019")
        });

        var summary = await processor.ProcessAsync();

        Assert.Equal(["unseen"], inbox.MessageGetIds);
        Assert.Equal(2, summary.ListedCandidates);
        Assert.Equal(1, summary.AlreadyProcessedSkipped);
        Assert.Equal(1, summary.UnseenFetched);
        Assert.Equal(1, summary.AlreadyProcessed);
        Assert.Equal(1, summary.NewTickets);
        Assert.Equal(1, lookup.CallCount);
    }

    [Fact]
    public async Task ProcessAsync_ReportsAttachmentFetchesForUnseenMessagesOnly()
    {
        var inbox = new FakeGmailInboxService(["known", "with-attachment"])
        {
            AttachmentsByMessageId = new Dictionary<string, IReadOnlyCollection<InboundEmailAttachment>>
            {
                ["with-attachment"] =
                [
                    new InboundEmailAttachment("proof.png", "image/png", "attachment-1", [1, 2, 3], 3)
                ]
            }
        };
        var lookup = new FakeProcessedMessageLookup(
            new ProcessedGmailMessageMatch("known", 20, "SUP-2026-000020"));
        var processor = CreateProcessor(inbox, lookup, new Dictionary<string, InboundEmailProcessingResult>
        {
            ["with-attachment"] = InboundEmailProcessingResult.NewTicket(1, 21, "SUP-2026-000021") with
            {
                AttachmentsFetched = 1
            }
        });

        var summary = await processor.ProcessAsync();

        Assert.Equal(1, summary.AttachmentsFetched);
        Assert.Equal(["with-attachment"], inbox.MessageGetIds);
    }

    [Fact]
    public async Task BackgroundFirstCycle_UsesOneDayRestartCatchUpQuery()
    {
        var inbox = new FakeGmailInboxService([]);
        var processor = CreateProcessor(inbox, new FakeProcessedMessageLookup(), new Dictionary<string, InboundEmailProcessingResult>());

        _ = await processor.ProcessAsync(mode: GmailMailboxProcessingMode.Background);

        Assert.Equal(
            "in:inbox newer_than:1d -from:support11flow@gmail.com",
            inbox.LastDiscoveryQuery?.Query);
        Assert.True(inbox.LastDiscoveryQuery?.IsRestartCatchUp);
    }

    [Fact]
    public async Task BackgroundSubsequentCycle_UsesIncrementalDateQuery()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 8, 23, 9, 0, 0, TimeSpan.Zero));
        var inbox = new FakeGmailInboxService([]);
        var processor = CreateProcessor(inbox, new FakeProcessedMessageLookup(), new Dictionary<string, InboundEmailProcessingResult>(), clock);

        _ = await processor.ProcessAsync(mode: GmailMailboxProcessingMode.Background);
        clock.Advance(TimeSpan.FromHours(1));
        _ = await processor.ProcessAsync(mode: GmailMailboxProcessingMode.Background);

        Assert.Equal(
            "in:inbox after:2026/08/22 -from:support11flow@gmail.com",
            inbox.LastDiscoveryQuery?.Query);
        Assert.False(inbox.LastDiscoveryQuery?.IsRestartCatchUp);
    }

    [Fact]
    public async Task ManualCycle_PreservesTheExistingSevenDayDiscoveryQuery()
    {
        var inbox = new FakeGmailInboxService([]);
        var processor = CreateProcessor(inbox, new FakeProcessedMessageLookup(), new Dictionary<string, InboundEmailProcessingResult>());

        _ = await processor.ProcessAsync();

        Assert.Null(inbox.LastDiscoveryQuery);
    }

    [Fact]
    public async Task RateLimitedFullRead_StopsTheCycleWithoutReadingLaterMessages()
    {
        var inbox = new FakeGmailInboxService(["first", "second"])
        {
            ExceptionByMessageId = new Dictionary<string, Exception>
            {
                ["first"] = new GmailRateLimitedException(new DateTime(2026, 8, 23, 9, 2, 0, DateTimeKind.Utc))
            }
        };
        var processor = CreateProcessor(inbox, new FakeProcessedMessageLookup(), new Dictionary<string, InboundEmailProcessingResult>());

        await Assert.ThrowsAsync<GmailRateLimitedException>(() => processor.ProcessAsync());

        Assert.Equal(["first"], inbox.MessageGetIds);
    }

    private static GmailMailboxProcessor CreateProcessor(
        FakeGmailInboxService inbox,
        IProcessedGmailMessageLookup lookup,
        IReadOnlyDictionary<string, InboundEmailProcessingResult> resultsById,
        TimeProvider? timeProvider = null) =>
        new(
            inbox,
            new FakeInboundEmailProcessor(resultsById),
            lookup,
            CreateDiscoveryState(),
            timeProvider ?? TimeProvider.System,
            NullLogger<GmailMailboxProcessor>.Instance);

    private static GmailMailboxProcessor CreateProcessor(
        IReadOnlyList<string> messageIds,
        IReadOnlyDictionary<string, InboundEmailProcessingResult> resultsById)
    {
        var inbox = new FakeGmailInboxService(messageIds);
        var inboundProcessor = new FakeInboundEmailProcessor(resultsById);
        return new GmailMailboxProcessor(
            inbox,
            inboundProcessor,
            new FakeProcessedMessageLookup(),
            CreateDiscoveryState(),
            TimeProvider.System,
            NullLogger<GmailMailboxProcessor>.Instance);
    }

    private static GmailInboxDiscoveryState CreateDiscoveryState() => new(
        Options.Create(new GmailSettings { MailboxAddress = "support11flow@gmail.com" }));

    private sealed class FakeGmailInboxService(IReadOnlyList<string> messageIds) : IGmailInboxService
    {
        public int? LastMaximumResults { get; private set; }
        public GmailInboxDiscoveryQuery? LastDiscoveryQuery { get; private set; }
        public List<string> MessageGetIds { get; } = [];
        public IReadOnlyDictionary<string, Exception>? ExceptionByMessageId { get; init; }
        public IReadOnlyDictionary<string, IReadOnlyCollection<InboundEmailAttachment>>? AttachmentsByMessageId { get; init; }

        public Task<IReadOnlyList<string>> ListRecentInboxMessageIdsAsync(
            int maximumResults = 25,
            CancellationToken cancellationToken = default,
            GmailInboxDiscoveryQuery? discoveryQuery = null)
        {
            LastMaximumResults = maximumResults;
            LastDiscoveryQuery = discoveryQuery;
            return Task.FromResult(messageIds);
        }

        public Task<IReadOnlyList<InboundEmailMessage>> GetRecentInboxMessagesAsync(
            int maximumResults = 25,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<InboundEmailMessage>>([]);

        public Task<IReadOnlyList<GmailMessageDiagnostic>> GetRecentMessageDiagnosticsAsync(
            int maximumResults = 25,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<GmailMessageDiagnostic>>([]);

        public Task<InboundEmailMessage?> GetMessageAsync(
            string gmailMessageId,
            CancellationToken cancellationToken = default)
        {
            MessageGetIds.Add(gmailMessageId);
            if (ExceptionByMessageId?.TryGetValue(gmailMessageId, out var exception) is true)
            {
                return Task.FromException<InboundEmailMessage?>(exception);
            }
            var message = new InboundEmailMessage(
                gmailMessageId,
                $"thread-{gmailMessageId}",
                $"<{gmailMessageId}@example.test>",
                null,
                [],
                "customer@example.test",
                "Customer",
                "Printer issue",
                "Body",
                DateTime.UtcNow,
                AttachmentsByMessageId?.GetValueOrDefault(gmailMessageId) ?? []);

            return Task.FromResult<InboundEmailMessage?>(message);
        }

        public Task<IReadOnlyList<InboundEmailAttachment>> ResolveMessageAttachmentsAsync(
            InboundEmailMessage message,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<InboundEmailAttachment>>([]);
    }

    private sealed class FakeProcessedMessageLookup(params ProcessedGmailMessageMatch[] matches) : IProcessedGmailMessageLookup
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyDictionary<string, ProcessedGmailMessageMatch>> FindProcessedAsync(
            IReadOnlyCollection<string> gmailMessageIds,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult<IReadOnlyDictionary<string, ProcessedGmailMessageMatch>>(
                matches.ToDictionary(match => match.GmailMessageId, StringComparer.Ordinal));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now = _now.Add(duration);
    }

    private sealed class FakeInboundEmailProcessor(
        IReadOnlyDictionary<string, InboundEmailProcessingResult> resultsById)
        : IInboundEmailProcessor
    {
        public Task<InboundEmailProcessingResult> ProcessAsync(
            InboundEmailMessage message,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(resultsById[message.GmailMessageId]);
    }
}
