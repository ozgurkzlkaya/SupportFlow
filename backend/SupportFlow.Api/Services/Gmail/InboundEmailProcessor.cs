using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SupportFlow.Api.Data;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;
using SupportFlow.Api.Services.Email;
using SupportFlow.Api.Services.Gmail.Models;
using SupportFlow.Api.Services.TicketActivities;
using SupportFlow.Api.Settings;
using Microsoft.Extensions.Options;

namespace SupportFlow.Api.Services.Gmail;

public class InboundEmailProcessor : IInboundEmailProcessor
{
    private readonly AppDbContext _dbContext;
    private readonly TicketService _ticketService;
    private readonly AttachmentService _attachmentService;
    private readonly NotificationService _notificationService;
    private readonly ITicketAcknowledgmentService _ticketAcknowledgmentService;
    private readonly IInboundEmailCorrelationService _correlationService;
    private readonly IInboundEmailSenderMatcher _senderMatcher;
    private readonly IExternalRequesterService _externalRequesterService;
    private readonly ITicketActivityWriter _ticketActivityWriter;
    private readonly IGmailInboxService? _gmailInboxService;
    private readonly string _mailboxAddress;
    private readonly ILogger<InboundEmailProcessor> _logger;

    public InboundEmailProcessor(
        AppDbContext dbContext,
        TicketService ticketService,
        AttachmentService attachmentService,
        NotificationService notificationService,
        ITicketAcknowledgmentService ticketAcknowledgmentService,
        IInboundEmailCorrelationService correlationService,
        IInboundEmailSenderMatcher senderMatcher,
        IExternalRequesterService externalRequesterService,
        ITicketActivityWriter ticketActivityWriter,
        IOptions<GmailSettings> settings,
        ILogger<InboundEmailProcessor> logger,
        IGmailInboxService? gmailInboxService = null)
    {
        _dbContext = dbContext;
        _ticketService = ticketService;
        _attachmentService = attachmentService;
        _notificationService = notificationService;
        _ticketAcknowledgmentService = ticketAcknowledgmentService;
        _correlationService = correlationService;
        _senderMatcher = senderMatcher;
        _externalRequesterService = externalRequesterService;
        _ticketActivityWriter = ticketActivityWriter;
        _gmailInboxService = gmailInboxService;
        _mailboxAddress = settings.Value.MailboxAddress.Trim().ToLowerInvariant();
        _logger = logger;
    }

    public async Task<InboundEmailProcessingResult> ProcessAsync(
        InboundEmailMessage message,
        CancellationToken cancellationToken = default)
    {
        if (message.IsSystemGenerated)
        {
            _logger.LogInformation(
                "Skipped system-generated Gmail message {GmailMessageId} in thread {GmailThreadId}.",
                message.GmailMessageId,
                message.GmailThreadId);
            return InboundEmailProcessingResult.SystemMessageSkipped();
        }

        if (string.IsNullOrWhiteSpace(message.GmailMessageId)
            || string.IsNullOrWhiteSpace(message.GmailThreadId)
            || string.IsNullOrWhiteSpace(message.FromEmail)
            || string.IsNullOrWhiteSpace(message.Body))
        {
            return InboundEmailProcessingResult.Failed(
                "The Gmail message is missing required metadata or a usable body.");
        }

        var normalizedEmail = IInboundEmailSenderMatcher.NormalizeEmail(message.FromEmail);
        if (!string.IsNullOrWhiteSpace(_mailboxAddress)
            && normalizedEmail == _mailboxAddress)
        {
            _logger.LogInformation(
                "Ignored self-sent Gmail message {GmailMessageId} from mailbox {MailboxAddress}.",
                message.GmailMessageId,
                _mailboxAddress);
            return InboundEmailProcessingResult.SelfMessageIgnored();
        }

        var sender = await _senderMatcher.FindByNormalizedEmailAsync(
            normalizedEmail,
            cancellationToken);

        if (sender is not null && !sender.IsActive)
        {
            return InboundEmailProcessingResult.UnknownCustomer(
                "The sender account is inactive.");
        }

        if (sender is not null && sender.Role != UserRole.Customer)
        {
            return InboundEmailProcessingResult.UnknownCustomer(
                "The sender account does not have the Customer role.");
        }

        var customer = sender;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var duplicate = await FindProcessedMessageAsync(
                message.GmailMessageId,
                cancellationToken);

            if (duplicate is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return InboundEmailProcessingResult.AlreadyProcessed(
                    duplicate.TicketId,
                    duplicate.TicketNumber);
            }

            var externalRequesterResolution = customer is null
                ? await _externalRequesterService.GetOrCreateAsync(
                    message.FromEmail,
                    message.FromName,
                    cancellationToken)
                : null;

            var correlation = await _correlationService.ResolveAsync(
                message,
                cancellationToken);

            var requesterMatchesCorrelatedTicket = correlation.Ticket is null
                || customer is not null
                    ? correlation.Ticket?.CustomerId == customer?.Id
                    : correlation.Ticket.ExternalRequesterId == externalRequesterResolution!.Requester.Id;
            if (correlation.Ticket is not null && !requesterMatchesCorrelatedTicket)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogWarning(
                    "Rejected inbound sender mismatch for ticket {TicketId} using {CorrelationStrategy} correlation.",
                    correlation.Ticket.Id,
                    correlation.Strategy);

                return InboundEmailProcessingResult.SenderMismatch(
                    correlation.Ticket.Id,
                    correlation.Ticket.TicketNumber);
            }

            var now = DateTime.UtcNow;
            var receivedAt = EnsureUtc(message.ReceivedAt);
            if (correlation.Ticket is null)
            {
                var ticket = customer is not null
                    ? await _ticketService.CreateTicketRecordAsync(
                        customer,
                        message.Subject,
                        message.Body,
                        TicketPriority.Normal,
                        receivedAt,
                        cancellationToken)
                    : await _ticketService.CreateExternalRequesterTicketRecordAsync(
                        externalRequesterResolution!.Requester,
                        message.Subject,
                        message.Body,
                        TicketPriority.Normal,
                        receivedAt,
                        cancellationToken);

                var emailThread = new TicketEmailThread
                {
                    TicketId = ticket.Id,
                    GmailThreadId = message.GmailThreadId,
                    CreatedAt = now
                };

                _dbContext.TicketEmailThreads.Add(emailThread);
                _dbContext.ProcessedEmailMessages.Add(new ProcessedEmailMessage
                {
                    TicketEmailThread = emailThread,
                    GmailMessageId = message.GmailMessageId,
                    InternetMessageId = message.InternetMessageId,
                    TicketMessageId = null,
                    ProcessedAt = now
                });

                AppendGmailTicketCreatedActivity(
                    ticket,
                    customer,
                    externalRequesterResolution?.Requester,
                    receivedAt);

                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                if (customer is not null)
                {
                    var attachmentMessage = await ResolveAttachmentsAfterPersistenceAsync(message, cancellationToken);
                    await ImportAttachmentsAsync(ticket.Id, customer.Id, attachmentMessage, cancellationToken);
                    await _ticketAcknowledgmentService.TrySendNewTicketAcknowledgmentAsync(
                        ticket,
                        TicketEmailRecipient.From(customer),
                        cancellationToken);
                    await _ticketService.TryNotifyNewTicketAsync(
                        ticket,
                        customer,
                        cancellationToken);
                    await _notificationService.NotifyInboundGmailNewTicketAsync(
                        ticket,
                        cancellationToken);

                    return InboundEmailProcessingResult.NewTicket(
                        customer.Id,
                        ticket.Id,
                        ticket.TicketNumber) with
                    {
                        AttachmentsFetched = attachmentMessage.Attachments.Count
                    };
                }

                await _ticketService.TryNotifyNewTicketAsync(
                    ticket,
                    externalRequesterResolution!.Requester,
                    cancellationToken);
                var newExternalAttachmentMessage = await ResolveAttachmentsAfterPersistenceAsync(message, cancellationToken);
                await ImportExternalRequesterAttachmentsAsync(
                    ticket.Id,
                    null,
                    externalRequesterResolution.Requester.Id,
                    newExternalAttachmentMessage,
                    cancellationToken);
                await _ticketAcknowledgmentService.TrySendNewTicketAcknowledgmentAsync(
                    ticket,
                    TicketEmailRecipient.From(externalRequesterResolution.Requester),
                    cancellationToken);
                await _notificationService.NotifyInboundGmailNewTicketAsync(
                    ticket,
                    cancellationToken);

                return InboundEmailProcessingResult.NewExternalTicket(
                    externalRequesterResolution!.Requester.Id,
                    ticket.Id,
                    ticket.TicketNumber,
                    externalRequesterResolution.WasCreated) with
                {
                    AttachmentsFetched = newExternalAttachmentMessage.Attachments.Count
                };
            }

            var correlatedTicket = correlation.Ticket;
            var correlatedThread = correlation.EmailThread;
            if (correlatedThread is null)
            {
                correlatedThread = new TicketEmailThread
                {
                    TicketId = correlatedTicket.Id,
                    GmailThreadId = message.GmailThreadId,
                    CreatedAt = now
                };
                _dbContext.TicketEmailThreads.Add(correlatedThread);
            }

            var writeResult = customer is not null
                ? _ticketService.AddTicketMessageRecord(
                    correlatedTicket,
                    customer,
                    message.Body,
                    false,
                    receivedAt)
                : _ticketService.AddExternalRequesterTicketMessageRecord(
                    correlatedTicket,
                    externalRequesterResolution!.Requester,
                    message.Body,
                    false,
                    receivedAt);

            _dbContext.ProcessedEmailMessages.Add(new ProcessedEmailMessage
            {
                TicketEmailThread = correlatedThread,
                GmailMessageId = message.GmailMessageId,
                InternetMessageId = message.InternetMessageId,
                TicketMessage = writeResult.Message,
                ProcessedAt = now
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            AppendGmailMessageActivities(
                correlatedTicket,
                writeResult,
                customer,
                externalRequesterResolution?.Requester,
                receivedAt);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            if (customer is not null)
            {
                var attachmentMessage = await ResolveAttachmentsAfterPersistenceAsync(message, cancellationToken);
                await ImportAttachmentsAsync(
                    correlatedTicket.Id,
                    customer.Id,
                    attachmentMessage,
                    cancellationToken);
                await _ticketService.TryNotifyTechnicalStaffOfCustomerReplyAsync(
                    correlatedTicket,
                    customer,
                    writeResult.Message.Message,
                    writeResult.WasReopened,
                    cancellationToken);
                await _notificationService.NotifyInboundGmailReplyAsync(
                    correlatedTicket,
                    cancellationToken);

                return InboundEmailProcessingResult.MessageAdded(
                    customer.Id,
                    correlatedTicket.Id,
                    correlatedTicket.TicketNumber,
                    writeResult.Message.Id) with
                {
                    AttachmentsFetched = attachmentMessage.Attachments.Count
                };
            }

            var externalAttachmentMessage = await ResolveAttachmentsAfterPersistenceAsync(message, cancellationToken);
            await ImportExternalRequesterAttachmentsAsync(
                correlatedTicket.Id,
                writeResult.Message.Id,
                externalRequesterResolution!.Requester.Id,
                externalAttachmentMessage,
                cancellationToken);
            await _ticketService.TryNotifyTechnicalStaffOfExternalRequesterReplyAsync(
                correlatedTicket,
                externalRequesterResolution.Requester,
                writeResult.Message.Message,
                writeResult.WasReopened,
                cancellationToken);
            await _notificationService.NotifyInboundGmailReplyAsync(
                correlatedTicket,
                cancellationToken);

            return InboundEmailProcessingResult.ExternalRequesterMessageAdded(
                externalRequesterResolution.Requester.Id,
                correlatedTicket.Id,
                correlatedTicket.TicketNumber,
                writeResult.Message.Id) with
            {
                AttachmentsFetched = externalAttachmentMessage.Attachments.Count
            };
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();

            var duplicate = await FindProcessedMessageAsync(
                message.GmailMessageId,
                cancellationToken);

            if (duplicate is not null)
            {
                return InboundEmailProcessingResult.AlreadyProcessed(
                    duplicate.TicketId,
                    duplicate.TicketNumber);
            }

            _logger.LogWarning(
                "Concurrent Gmail processing conflict for message {GmailMessageId}; a later sync can retry it.",
                message.GmailMessageId);
            return InboundEmailProcessingResult.Failed(
                "A concurrent processing conflict occurred; retry the mailbox sync.");
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
            await transaction.RollbackAsync(CancellationToken.None);
            _logger.LogError(
                exception,
                "Failed to persist Gmail message {GmailMessageId}.",
                message.GmailMessageId);
            return InboundEmailProcessingResult.Failed(
                "The message could not be processed.");
        }
    }

    private void AppendGmailTicketCreatedActivity(
        Ticket ticket,
        User? customer,
        ExternalRequester? externalRequester,
        DateTime occurredAt) =>
        AppendGmailActivity(
            ticket,
            TicketActivityType.TicketCreated,
            TicketActivityVisibility.RequesterVisible,
            occurredAt,
            customer,
            externalRequester);

    private void AppendGmailMessageActivities(
        Ticket ticket,
        TicketMessageWriteResult writeResult,
        User? customer,
        ExternalRequester? externalRequester,
        DateTime occurredAt)
    {
        AppendGmailActivity(
            ticket,
            TicketActivityType.MessageCreated,
            TicketActivityVisibility.RequesterVisible,
            occurredAt,
            customer,
            externalRequester,
            relatedMessageId: writeResult.Message.Id);

        if (!writeResult.ReopenedFromStatus.HasValue)
        {
            return;
        }

        AppendGmailActivity(
            ticket,
            TicketActivityType.StatusChanged,
            TicketActivityVisibility.RequesterVisible,
            occurredAt,
            customer,
            externalRequester,
            new StatusChangedTicketActivityMetadata(writeResult.ReopenedFromStatus.Value, ticket.Status),
            writeResult.StatusHistory?.Id);
    }

    private void AppendGmailActivity(
        Ticket ticket,
        TicketActivityType activityType,
        TicketActivityVisibility visibility,
        DateTime occurredAt,
        User? customer,
        ExternalRequester? externalRequester,
        TicketActivityMetadata? metadata = null,
        int? relatedStatusHistoryId = null,
        int? relatedMessageId = null)
    {
        if (customer is not null)
        {
            _ticketActivityWriter.Append(new TicketActivityAppendRequest(
                ticket.Id,
                activityType,
                TicketActivitySource.GmailInbound,
                visibility,
                occurredAt,
                TicketActivityActorKind.User,
                customer.Id,
                null,
                GetDisplayName(customer),
                customer.Role,
                relatedStatusHistoryId,
                relatedMessageId,
                null,
                metadata));
            return;
        }

        if (externalRequester is null)
        {
            throw new InvalidOperationException("A Gmail activity requires a resolved requester.");
        }

        _ticketActivityWriter.Append(new TicketActivityAppendRequest(
            ticket.Id,
            activityType,
            TicketActivitySource.GmailInbound,
            visibility,
            occurredAt,
            TicketActivityActorKind.ExternalRequester,
            null,
            externalRequester.Id,
            externalRequester.DisplayName ?? externalRequester.Email,
            null,
            relatedStatusHistoryId,
            relatedMessageId,
            null,
            metadata));
    }

    private static string GetDisplayName(User user) =>
        $"{user.FirstName} {user.LastName}".Trim();

    private async Task<InboundEmailMessage> ResolveAttachmentsAfterPersistenceAsync(
        InboundEmailMessage message,
        CancellationToken cancellationToken)
    {
        if (message.Attachments.Count > 0
            || message.AttachmentDescriptors is not { Count: > 0 }
            || _gmailInboxService is null)
        {
            return message;
        }

        var attachments = await _gmailInboxService.ResolveMessageAttachmentsAsync(
            message,
            cancellationToken);
        return message with { Attachments = attachments };
    }

    private async Task ImportAttachmentsAsync(
        int ticketId,
        int uploadedByUserId,
        InboundEmailMessage message,
        CancellationToken cancellationToken)
    {
        if (message.Attachments.Count == 0)
        {
            return;
        }

        foreach (var attachment in message.Attachments)
        {
            try
            {
                var result = await _attachmentService.SaveInboundAttachmentAsync(
                    ticketId,
                    uploadedByUserId,
                    attachment.FileName,
                    attachment.ContentBytes,
                    message.ReceivedAt,
                    cancellationToken);

                if (result.Failure != TicketOperationFailure.None)
                {
                    _logger.LogWarning(
                        "Skipped Gmail attachment {FileName} for ticket {TicketId}: {Reason}",
                        attachment.FileName,
                        ticketId,
                        result.Error ?? result.Failure.ToString());
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to import Gmail attachment {FileName} for ticket {TicketId}.",
                    attachment.FileName,
                    ticketId);
            }
        }
    }

    private async Task ImportExternalRequesterAttachmentsAsync(
        int ticketId,
        int? messageId,
        int externalRequesterId,
        InboundEmailMessage message,
        CancellationToken cancellationToken)
    {
        foreach (var attachment in message.Attachments)
        {
            try
            {
                var result = await _attachmentService.SaveInboundExternalRequesterAttachmentAsync(
                    ticketId,
                    messageId,
                    externalRequesterId,
                    attachment.FileName,
                    attachment.ContentBytes,
                    message.ReceivedAt,
                    cancellationToken);

                if (result.Failure != TicketOperationFailure.None)
                {
                    _logger.LogWarning(
                        "Skipped Gmail attachment {FileName} for ticket {TicketId}: {Reason}",
                        attachment.FileName,
                        ticketId,
                        result.Error ?? result.Failure.ToString());
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to import Gmail attachment {FileName} for ticket {TicketId}.",
                    attachment.FileName,
                    ticketId);
            }
        }
    }

    private async Task<ProcessedMessageLookup?> FindProcessedMessageAsync(
        string gmailMessageId,
        CancellationToken cancellationToken) =>
        await _dbContext.ProcessedEmailMessages
            .AsNoTracking()
            .Where(message => message.GmailMessageId == gmailMessageId)
            .Select(message => new ProcessedMessageLookup(
                message.TicketEmailThread.TicketId,
                message.TicketEmailThread.Ticket.TicketNumber))
            .SingleOrDefaultAsync(cancellationToken);

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sqlException
            && sqlException.Number is 2601 or 2627;

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.ToUniversalTime();

    private sealed record ProcessedMessageLookup(int TicketId, string TicketNumber);
}
