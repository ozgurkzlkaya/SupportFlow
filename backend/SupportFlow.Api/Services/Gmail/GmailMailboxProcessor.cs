using SupportFlow.Api.Services.Gmail.Models;

namespace SupportFlow.Api.Services.Gmail;

public class GmailMailboxProcessor : IGmailMailboxProcessor
{
    private const int MaximumMessagesPerCycle = 100;

    private readonly IGmailInboxService _gmailInboxService;
    private readonly IInboundEmailProcessor _inboundEmailProcessor;
    private readonly IProcessedGmailMessageLookup _processedMessageLookup;
    private readonly GmailInboxDiscoveryState _discoveryState;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GmailMailboxProcessor> _logger;

    public GmailMailboxProcessor(
        IGmailInboxService gmailInboxService,
        IInboundEmailProcessor inboundEmailProcessor,
        IProcessedGmailMessageLookup processedMessageLookup,
        GmailInboxDiscoveryState discoveryState,
        TimeProvider timeProvider,
        ILogger<GmailMailboxProcessor> logger)
    {
        _gmailInboxService = gmailInboxService;
        _inboundEmailProcessor = inboundEmailProcessor;
        _processedMessageLookup = processedMessageLookup;
        _discoveryState = discoveryState;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<GmailMailboxProcessingSummary> ProcessAsync(
        CancellationToken cancellationToken = default,
        GmailMailboxProcessingMode mode = GmailMailboxProcessingMode.Manual)
    {
        var cycleStartedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var discoveryQuery = mode == GmailMailboxProcessingMode.Background
            ? _discoveryState.GetBackgroundDiscoveryQuery()
            : null;
        IReadOnlyList<string> messageIds;
        try
        {
            messageIds = await _gmailInboxService.ListRecentInboxMessageIdsAsync(
                MaximumMessagesPerCycle,
                cancellationToken,
                discoveryQuery);
        }
        catch (GmailIntegrationUnavailableException)
        {
            throw;
        }
        catch (GmailAuthorizationRequiredException)
        {
            throw;
        }
        catch (GmailRateLimitedException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "The Gmail mailbox cycle could not list messages.");
            return new GmailMailboxProcessingSummary(0, 0, 0, 0, 0, 0, 0, 1, [], ListedCandidates: 0);
        }

        var processedMatches = await _processedMessageLookup.FindProcessedAsync(
            messageIds,
            cancellationToken);
        var results = new List<InboundEmailProcessingResult>(messageIds.Count);
        foreach (var match in processedMatches.Values)
        {
            results.Add(InboundEmailProcessingResult.AlreadyProcessed(
                match.TicketId,
                match.TicketNumber));
            _logger.LogDebug(
                "Skipped already processed Gmail message {GmailMessageId} before full message retrieval for ticket {TicketId}.",
                match.GmailMessageId,
                match.TicketId);
        }

        var unseenMessageIds = messageIds
            .Where(messageId => !processedMatches.ContainsKey(messageId))
            .ToArray();
        var attachmentsFetched = 0;

        foreach (var messageId in unseenMessageIds)
        {
            try
            {
                var message = await _gmailInboxService.GetMessageAsync(
                    messageId,
                    cancellationToken);
                var result = message is null
                    ? InboundEmailProcessingResult.Failed(
                        "The Gmail message could not be retrieved.")
                    : await _inboundEmailProcessor.ProcessAsync(
                        message,
                        cancellationToken);

                results.Add(result);
                attachmentsFetched += result.AttachmentsFetched;
                if (result.Status == InboundEmailProcessingStatus.AlreadyProcessed)
                {
                    _logger.LogDebug(
                        "Gmail message {GmailMessageId} in thread {GmailThreadId} was already processed for ticket {TicketId}.",
                        messageId,
                        message?.GmailThreadId,
                        result.TicketId);
                }
                else
                {
                    _logger.LogInformation(
                        "Gmail message {GmailMessageId} in thread {GmailThreadId} processing result: {Status}; ticket {TicketId}.",
                        messageId,
                        message?.GmailThreadId,
                        result.Status,
                        result.TicketId);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (GmailRateLimitedException)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Gmail message {GmailMessageId} failed during mailbox processing.",
                    messageId);
                results.Add(InboundEmailProcessingResult.Failed(
                    "The Gmail message could not be processed."));
            }
        }

        var processingItems = results
            .Select(result => new GmailMailboxProcessingItem(
                result.Status,
                result.TicketId,
                result.TicketNumber,
                result.Reason,
                result.ExternalRequesterId,
                result.ExternalRequesterWasCreated))
            .ToArray();

        if (mode == GmailMailboxProcessingMode.Background)
        {
            _discoveryState.MarkBackgroundCycleSucceeded(cycleStartedAtUtc);
        }

        var summary = new GmailMailboxProcessingSummary(
            results.Count,
            results.Count(result => result.Status is InboundEmailProcessingStatus.NewTicketCreated
                or InboundEmailProcessingStatus.NewExternalTicketCreated),
            Count(results, InboundEmailProcessingStatus.MessageAdded),
            Count(results, InboundEmailProcessingStatus.AlreadyProcessed),
            Count(results, InboundEmailProcessingStatus.UnknownCustomer),
            Count(results, InboundEmailProcessingStatus.SenderMismatch),
            Count(results, InboundEmailProcessingStatus.SelfMessageIgnored),
            Count(results, InboundEmailProcessingStatus.Failed),
            processingItems,
            Count(results, InboundEmailProcessingStatus.NewExternalTicketCreated),
            results.Count(result => result.ExternalRequesterWasCreated is true),
            results.Count(result => result.Status == InboundEmailProcessingStatus.NewExternalTicketCreated
                && result.ExternalRequesterWasCreated is false),
            messageIds.Count,
            processedMatches.Count,
            unseenMessageIds.Length,
            attachmentsFetched,
            Count(results, InboundEmailProcessingStatus.SystemMessageSkipped));

        _logger.LogInformation(
            "Gmail mailbox discovery metrics. ListedCandidates: {ListedCandidates}; AlreadyProcessedSkipped: {AlreadyProcessedSkipped}; UnseenFetched: {UnseenFetched}; AttachmentsFetched: {AttachmentsFetched}.",
            summary.ListedCandidates,
            summary.AlreadyProcessedSkipped,
            summary.UnseenFetched,
            summary.AttachmentsFetched);

        return summary;
    }

    private static int Count(
        IEnumerable<InboundEmailProcessingResult> results,
        InboundEmailProcessingStatus status) =>
        results.Count(result => result.Status == status);
}
