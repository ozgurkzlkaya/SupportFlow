using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SupportFlow.Api.Data;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;
using SupportFlow.Api.Services;
using SupportFlow.Api.Services.Email;
using SupportFlow.Api.Services.Gmail;
using SupportFlow.Api.Services.Gmail.Models;
using SupportFlow.Api.Services.QueueAging;
using SupportFlow.Api.Services.Sla;
using SupportFlow.Api.Services.TicketActivities;
using SupportFlow.Api.Settings;

namespace SupportFlow.Api.Tests.Gmail;

public sealed class InboundEmailProcessorTests
{
    private static readonly byte[] MinimalPngBytes =
        Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aF9sAAAAASUVORK5CYII=");

    [Fact]
    public async Task NewCustomerGmailTicket_AppendsRequesterVisibleTicketCreatedActivity()
    {
        await using var harness = await TestHarness.CreateAsync();

        var result = await harness.Processor.ProcessAsync(CreateMessage("activity-new-customer", "activity-thread-customer"));

        var activity = Assert.Single(await harness.DbContext.TicketActivities.ToListAsync());
        Assert.Equal(InboundEmailProcessingStatus.NewTicketCreated, result.Status);
        Assert.Equal(TicketActivityType.TicketCreated, activity.ActivityType);
        Assert.Equal(TicketActivitySource.GmailInbound, activity.Source);
        Assert.Equal(TicketActivityVisibility.RequesterVisible, activity.Visibility);
        Assert.Equal(harness.Customer.Id, activity.ActorUserId);
        Assert.Equal(UserRole.Customer, activity.ActorRole);
    }

    [Fact]
    public async Task NewExternalGmailTicket_AppendsExternalRequesterTicketCreatedActivity()
    {
        await using var harness = await TestHarness.CreateAsync();

        var result = await harness.Processor.ProcessAsync(CreateMessage(
            "activity-new-external",
            "activity-thread-external",
            fromEmail: "activity-external@example.test",
            fromName: "Activity External"));

        var activity = Assert.Single(await harness.DbContext.TicketActivities.ToListAsync());
        Assert.Equal(InboundEmailProcessingStatus.NewExternalTicketCreated, result.Status);
        Assert.Equal(TicketActivityActorKind.ExternalRequester, activity.ActorKind);
        Assert.NotNull(activity.ExternalRequesterId);
        Assert.Null(activity.ActorUserId);
        Assert.Null(activity.ActorRole);
    }

    [Fact]
    public async Task CorrelatedGmailReply_AppendsOneMessageActivityAndDuplicateAddsNone()
    {
        await using var harness = await TestHarness.CreateAsync();
        var ticket = await harness.AddTicketAsync(harness.Customer);
        await harness.AddProcessedMessageAsync(ticket, "activity-thread", "activity-original", "<activity-original@example.test>");

        var message = CreateMessage(
            "activity-reply",
            "activity-thread",
            inReplyTo: "<activity-original@example.test>");
        var first = await harness.Processor.ProcessAsync(message);
        var duplicate = await harness.Processor.ProcessAsync(message);

        var activity = Assert.Single(await harness.DbContext.TicketActivities.ToListAsync());
        Assert.Equal(InboundEmailProcessingStatus.MessageAdded, first.Status);
        Assert.Equal(InboundEmailProcessingStatus.AlreadyProcessed, duplicate.Status);
        Assert.Equal(TicketActivityType.MessageCreated, activity.ActivityType);
        Assert.Equal(TicketActivitySource.GmailInbound, activity.Source);
        Assert.NotNull(activity.RelatedMessageId);
        Assert.Equal("{}", activity.MetadataJson);
    }

    [Fact]
    public async Task ExternalGmailReplyReopen_AppendsMessageAndStatusActivitiesWithoutFakeUser()
    {
        await using var harness = await TestHarness.CreateAsync();
        var initial = await harness.Processor.ProcessAsync(CreateMessage(
            "activity-external-original",
            "activity-external-thread",
            fromEmail: "activity-external-reply@example.test",
            fromName: "External Reply"));
        var ticket = await harness.DbContext.Tickets.SingleAsync(item => item.Id == initial.TicketId);
        ticket.Status = TicketStatus.Resolved;
        await harness.DbContext.SaveChangesAsync();

        var reply = await harness.Processor.ProcessAsync(CreateMessage(
            "activity-external-reply",
            "activity-external-thread",
            fromEmail: "activity-external-reply@example.test",
            fromName: "External Reply"));

        var activities = await harness.DbContext.TicketActivities
            .Where(activity => activity.TicketId == ticket.Id)
            .OrderBy(activity => activity.Id)
            .ToListAsync();
        Assert.Equal(InboundEmailProcessingStatus.MessageAdded, reply.Status);
        Assert.Contains(activities, activity => activity.ActivityType == TicketActivityType.MessageCreated
            && activity.ActorKind == TicketActivityActorKind.ExternalRequester
            && activity.ActorUserId == null);
        Assert.Contains(activities, activity => activity.ActivityType == TicketActivityType.StatusChanged
            && activity.ActorKind == TicketActivityActorKind.ExternalRequester
            && activity.RelatedStatusHistoryId == null);
    }

    [Fact]
    public async Task GmailActivityWriterFailure_RollsBackNewTicketAndProcessedMessage()
    {
        await using var harness = await TestHarness.CreateAsync(new ThrowingTicketActivityWriter());

        var result = await harness.Processor.ProcessAsync(CreateMessage("activity-failure", "activity-failure-thread"));

        Assert.Equal(InboundEmailProcessingStatus.Failed, result.Status);
        Assert.Empty(await harness.DbContext.Tickets.ToListAsync());
        Assert.Empty(await harness.DbContext.ProcessedEmailMessages.ToListAsync());
        Assert.Empty(await harness.DbContext.TicketActivities.ToListAsync());
    }

    [Fact]
    public async Task SameGmailThreadId_AddsMessageToExistingTicketWithoutAcknowledgment()
    {
        await using var harness = await TestHarness.CreateAsync();
        var ticket = await harness.AddTicketAsync(harness.Customer);
        await harness.AddProcessedMessageAsync(
            ticket,
            "gmail-thread-1",
            "gmail-message-1",
            "<original@example.test>");

        var result = await harness.Processor.ProcessAsync(
            CreateMessage("gmail-message-2", "gmail-thread-1"));

        Assert.Equal(InboundEmailProcessingStatus.MessageAdded, result.Status);
        Assert.Equal(ticket.Id, result.TicketId);
        Assert.Equal(1, await harness.DbContext.TicketMessages.CountAsync());
        Assert.Equal(1, await harness.DbContext.TicketEmailThreads.CountAsync());
        Assert.Empty(harness.GmailSendService.SentMessages);
    }

    [Fact]
    public async Task CorrelatedCustomerReply_PersistsProcessedMessageWithExistingTicketMessage()
    {
        await using var harness = await TestHarness.CreateAsync();
        var ticket = await harness.AddTicketAsync(harness.Customer);
        await harness.AddProcessedMessageAsync(
            ticket,
            "gmail-thread-1",
            "gmail-message-1",
            "<original@example.test>");

        var result = await harness.Processor.ProcessAsync(
            CreateMessage("gmail-message-2", "gmail-thread-1"));

        Assert.Equal(InboundEmailProcessingStatus.MessageAdded, result.Status);
        var processed = await harness.DbContext.ProcessedEmailMessages
            .SingleAsync(item => item.GmailMessageId == "gmail-message-2");
        Assert.NotNull(processed.TicketMessageId);
        Assert.Equal(result.TicketMessageId, processed.TicketMessageId);
        var ticketMessage = await harness.DbContext.TicketMessages.FindAsync(processed.TicketMessageId);
        Assert.NotNull(ticketMessage);
        Assert.Equal(ticket.Id, ticketMessage!.TicketId);
    }

    [Fact]
    public async Task CorrelatedCustomerReply_UsesGmailReceivedAtForPersistedTicketMessage()
    {
        await using var harness = await TestHarness.CreateAsync();
        var ticket = await harness.AddTicketAsync(harness.Customer);
        await harness.AddProcessedMessageAsync(
            ticket,
            "gmail-thread-1",
            "gmail-message-1",
            "<original@example.test>");
        var receivedAt = new DateTime(2026, 8, 22, 9, 45, 12, DateTimeKind.Utc);

        var result = await harness.Processor.ProcessAsync(CreateMessage(
            "gmail-message-2",
            "gmail-thread-1",
            receivedAt: receivedAt));

        Assert.Equal(InboundEmailProcessingStatus.MessageAdded, result.Status);
        var message = await harness.DbContext.TicketMessages.SingleAsync();
        Assert.Equal(receivedAt, message.CreatedAt);
    }

    [Fact]
    public async Task CorrelatedCustomerReply_QueueAgingStartsAtGmailReceivedAtNotProcessorTime()
    {
        await using var harness = await TestHarness.CreateAsync();
        var receivedAt = new DateTime(2026, 8, 22, 9, 45, 12, DateTimeKind.Utc);
        var ticket = await harness.AddTicketAsync(harness.Customer);
        ticket.CreatedAt = receivedAt.AddHours(-2);
        ticket.UpdatedAt = ticket.CreatedAt;
        await harness.AddProcessedMessageAsync(
            ticket,
            "gmail-thread-1",
            "gmail-message-1",
            "<original@example.test>");
        harness.DbContext.TicketMessages.Add(new TicketMessage
        {
            TicketId = ticket.Id,
            SenderId = harness.TechnicalStaff.Id,
            Message = "Prior delivered reply",
            OutboundDeliveryStatus = OutboundDeliveryStatus.Sent,
            CreatedAt = receivedAt.AddHours(-1),
            DeliveredAt = receivedAt.AddHours(-1)
        });
        await harness.DbContext.SaveChangesAsync();

        await harness.Processor.ProcessAsync(CreateMessage(
            "gmail-message-2",
            "gmail-thread-1",
            receivedAt: receivedAt));

        var messages = await harness.DbContext.TicketMessages
            .Include(item => item.Sender)
            .Where(item => item.TicketId == ticket.Id)
            .ToListAsync();
        var now = receivedAt.AddHours(3);
        var result = new QueueAgingCalculator(new FixedTimeProvider(now)).Calculate(
            new QueueAgingTicketInput(ticket.Status, ticket.CreatedAt),
            messages.Select(message => new QueueAgingMessageInput(
                message.Sender?.Role == UserRole.Customer,
                message.Sender?.Role is UserRole.Admin or UserRole.TechnicalStaff,
                message.IsInternal,
                message.OutboundDeliveryStatus,
                message.CreatedAt,
                message.DeliveredAt)).ToArray());

        Assert.True(result.IsAwaitingStaffReply);
        Assert.Equal(receivedAt, result.AwaitingStaffSince);
        Assert.Equal(180, result.AwaitingStaffMinutes);
    }

    [Fact]
    public async Task DifferentThreadWithMatchingInReplyTo_AddsMessageToExistingTicket()
    {
        await using var harness = await TestHarness.CreateAsync();
        var ticket = await harness.AddTicketAsync(harness.Customer);
        await harness.AddProcessedMessageAsync(
            ticket,
            "gmail-thread-1",
            "gmail-message-1",
            "<original@example.test>");

        var result = await harness.Processor.ProcessAsync(CreateMessage(
            "gmail-message-2",
            "gmail-thread-2",
            inReplyTo: "<original@example.test>"));

        Assert.Equal(InboundEmailProcessingStatus.MessageAdded, result.Status);
        Assert.Equal(ticket.Id, result.TicketId);
        Assert.Equal(1, await harness.DbContext.Tickets.CountAsync());
        Assert.Equal(2, await harness.DbContext.TicketEmailThreads.CountAsync());
        Assert.Empty(harness.GmailSendService.SentMessages);
    }

    [Fact]
    public async Task DifferentThreadWithMatchingReferences_AddsMessageToExistingTicket()
    {
        await using var harness = await TestHarness.CreateAsync();
        var ticket = await harness.AddTicketAsync(harness.Customer);
        await harness.AddProcessedMessageAsync(
            ticket,
            "gmail-thread-1",
            "gmail-message-1",
            "<original@example.test>");

        var result = await harness.Processor.ProcessAsync(CreateMessage(
            "gmail-message-2",
            "gmail-thread-2",
            references: ["<older@example.test>", "<original@example.test>"]));

        Assert.Equal(InboundEmailProcessingStatus.MessageAdded, result.Status);
        Assert.Equal(ticket.Id, result.TicketId);
        Assert.Empty(harness.GmailSendService.SentMessages);
    }

    [Fact]
    public async Task SubjectTicketNumberInReplyPrefix_AddsMessageToExistingCustomerTicket()
    {
        await using var harness = await TestHarness.CreateAsync();
        var ticket = await harness.AddTicketAsync(harness.Customer);

        var result = await harness.Processor.ProcessAsync(CreateMessage(
            "gmail-message-2",
            "gmail-thread-2",
            subject: $"Re: [{ticket.TicketNumber}] VPN ba\u011flant\u0131 problemi"));

        Assert.Equal(InboundEmailProcessingStatus.MessageAdded, result.Status);
        Assert.Equal(ticket.Id, result.TicketId);
        Assert.Equal(1, await harness.DbContext.Tickets.CountAsync());
        Assert.Empty(harness.GmailSendService.SentMessages);
    }

    [Fact]
    public async Task SubjectTicketNumberOwnedByAnotherCustomer_ReturnsSenderMismatchWithoutAcknowledgment()
    {
        await using var harness = await TestHarness.CreateAsync();
        var ticket = await harness.AddTicketAsync(harness.OtherCustomer);

        var result = await harness.Processor.ProcessAsync(CreateMessage(
            "gmail-message-2",
            "gmail-thread-2",
            subject: ticket.TicketNumber));

        Assert.Equal(InboundEmailProcessingStatus.SenderMismatch, result.Status);
        Assert.Empty(await harness.DbContext.TicketMessages.ToListAsync());
        Assert.Empty(await harness.DbContext.TicketEmailThreads.ToListAsync());
        Assert.Empty(await harness.DbContext.ProcessedEmailMessages.ToListAsync());
        Assert.Empty(harness.GmailSendService.SentMessages);
    }

    [Fact]
    public async Task NoCorrelation_CreatesNewTicketAndRequestsOneAcknowledgmentAfterPersistence()
    {
        await using var harness = await TestHarness.CreateAsync();
        harness.GmailSendService.OnSendAsync = async _ =>
        {
            Assert.Single(await harness.DbContext.Tickets.ToListAsync());
            Assert.Single(await harness.DbContext.TicketEmailThreads.ToListAsync());
            Assert.Single(await harness.DbContext.ProcessedEmailMessages.ToListAsync());
            Assert.Null(harness.DbContext.Database.CurrentTransaction);
        };

        var result = await harness.Processor.ProcessAsync(CreateMessage(
            "gmail-message-1",
            "gmail-thread-1"));

        Assert.Equal(InboundEmailProcessingStatus.NewTicketCreated, result.Status);
        var ticket = Assert.Single(await harness.DbContext.Tickets.ToListAsync());
        Assert.Single(await harness.DbContext.TicketEmailThreads.ToListAsync());
        var processed = Assert.Single(
            await harness.DbContext.ProcessedEmailMessages.ToListAsync());
        Assert.Null(processed.TicketMessageId);

        var sent = Assert.Single(harness.GmailSendService.SentMessages);
        Assert.Equal(harness.Customer.Email, sent.ToEmail);
        Assert.Equal($"[{ticket.TicketNumber}] {ticket.Title}", sent.Subject);
        Assert.Contains(ticket.TicketNumber, sent.Body, StringComparison.Ordinal);
        Assert.Contains(
            "Bu talebe ek bilgi g\u00f6ndermek veya yan\u0131t vermek i\u00e7in l\u00fctfen bu e-postay\u0131 yan\u0131tlay\u0131n.",
            sent.Body,
            StringComparison.Ordinal);
        Assert.Contains(
            await harness.DbContext.Notifications.ToListAsync(),
            notification => notification.Type == NotificationType.GmailInbound);
    }

    [Fact]
    public async Task NoCorrelation_UsesGmailReceivedAtForTicketCreatedAt()
    {
        await using var harness = await TestHarness.CreateAsync();
        var receivedAt = new DateTime(2026, 8, 21, 9, 45, 12, DateTimeKind.Utc);

        var result = await harness.Processor.ProcessAsync(CreateMessage(
            "gmail-message-1",
            "gmail-thread-1",
            receivedAt: receivedAt));

        Assert.Equal(InboundEmailProcessingStatus.NewTicketCreated, result.Status);
        var ticket = Assert.Single(await harness.DbContext.Tickets.ToListAsync());
        Assert.Equal(receivedAt, ticket.CreatedAt);
        Assert.Equal(receivedAt, ticket.UpdatedAt);
        Assert.Equal(receivedAt.AddHours(8), ticket.FirstResponseDueAt);
        Assert.Equal(receivedAt.AddHours(48), ticket.ResolutionDueAt);
    }

    [Fact]
    public async Task UnregisteredSender_CreatesExternalRequesterAndExternalTicket()
    {
        await using var harness = await TestHarness.CreateAsync();
        var receivedAt = new DateTime(2026, 8, 21, 10, 30, 0, DateTimeKind.Utc);

        var result = await harness.Processor.ProcessAsync(CreateMessage(
            "gmail-message-unknown",
            "gmail-thread-unknown",
            fromEmail: "missing@example.test",
            fromName: "External Sender",
            receivedAt: receivedAt));

        Assert.Equal(InboundEmailProcessingStatus.NewExternalTicketCreated, result.Status);
        Assert.True(result.ExternalRequesterWasCreated);

        var requester = Assert.Single(await harness.DbContext.ExternalRequesters.ToListAsync());
        Assert.Equal("missing@example.test", requester.Email);
        Assert.Equal("missing@example.test", requester.NormalizedEmail);
        Assert.Equal("External Sender", requester.DisplayName);

        var ticket = Assert.Single(await harness.DbContext.Tickets.ToListAsync());
        Assert.Null(ticket.CustomerId);
        Assert.Equal(requester.Id, ticket.ExternalRequesterId);
        Assert.Equal(receivedAt, ticket.CreatedAt);
        Assert.Equal(receivedAt.AddHours(8), ticket.FirstResponseDueAt);
        Assert.Equal(receivedAt.AddHours(48), ticket.ResolutionDueAt);
        Assert.Single(await harness.DbContext.TicketEmailThreads.ToListAsync());
        var processed = Assert.Single(await harness.DbContext.ProcessedEmailMessages.ToListAsync());
        Assert.Null(processed.TicketMessageId);
        Assert.Equal(3, await harness.DbContext.Notifications.CountAsync());
        var acknowledgment = Assert.Single(harness.GmailSendService.SentMessages);
        Assert.Equal("missing@example.test", acknowledgment.ToEmail);
        Assert.Equal($"[{ticket.TicketNumber}] {ticket.Title}", acknowledgment.Subject);
        Assert.DoesNotContain("customer/tickets", acknowledgment.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExternalRequester_IsReusedForCaseAndWhitespaceVariants()
    {
        await using var harness = await TestHarness.CreateAsync();

        var first = await harness.Processor.ProcessAsync(CreateMessage(
            "gmail-message-external-1",
            "gmail-thread-external-1",
            fromEmail: "External@example.test"));
        var second = await harness.Processor.ProcessAsync(CreateMessage(
            "gmail-message-external-2",
            "gmail-thread-external-2",
            fromEmail: "  external@EXAMPLE.test  "));

        Assert.Equal(InboundEmailProcessingStatus.NewExternalTicketCreated, first.Status);
        Assert.True(first.ExternalRequesterWasCreated);
        Assert.Equal(InboundEmailProcessingStatus.NewExternalTicketCreated, second.Status);
        Assert.False(second.ExternalRequesterWasCreated);
        Assert.Single(await harness.DbContext.ExternalRequesters.ToListAsync());
        Assert.Equal(2, await harness.DbContext.Tickets.CountAsync());
    }

    [Fact]
    public async Task ExternalRequesterMessage_IsDeduplicatedWithoutCreatingAnotherRequester()
    {
        await using var harness = await TestHarness.CreateAsync();
        var message = CreateMessage(
            "gmail-message-external-duplicate",
            "gmail-thread-external-duplicate",
            fromEmail: "external@example.test");

        var first = await harness.Processor.ProcessAsync(message);
        var second = await harness.Processor.ProcessAsync(message);

        Assert.Equal(InboundEmailProcessingStatus.NewExternalTicketCreated, first.Status);
        Assert.Equal(InboundEmailProcessingStatus.AlreadyProcessed, second.Status);
        Assert.Single(await harness.DbContext.ExternalRequesters.ToListAsync());
        Assert.Single(await harness.DbContext.Tickets.ToListAsync());
        Assert.Single(await harness.DbContext.TicketEmailThreads.ToListAsync());
        Assert.Single(await harness.DbContext.ProcessedEmailMessages.ToListAsync());
    }

    [Fact]
    public async Task ExternalTicket_IsVisibleToStaffButNotToRegisteredCustomers()
    {
        await using var harness = await TestHarness.CreateAsync();

        var processing = await harness.Processor.ProcessAsync(CreateMessage(
            "gmail-message-external-visibility",
            "gmail-thread-external-visibility",
            fromEmail: "external@example.test",
            fromName: "External Sender"));

        var staffView = await harness.TicketService.GetByIdAsync(
            processing.TicketId!.Value,
            harness.Admin.Id,
            UserRole.Admin);
        var customerView = await harness.TicketService.GetByIdAsync(
            processing.TicketId.Value,
            harness.Customer.Id,
            UserRole.Customer);

        Assert.NotNull(staffView);
        Assert.Null(staffView!.CustomerId);
        Assert.Equal("External Sender", staffView.CustomerName);
        Assert.Equal("external@example.test", staffView.CustomerEmail);
        Assert.Null(customerView);
    }

    [Fact]
    public async Task ExternalRequesterReply_CorrelatesAndLinksAttachmentToTheCreatedMessage()
    {
        await using var harness = await TestHarness.CreateAsync();
        var initial = await harness.Processor.ProcessAsync(CreateMessage(
            "gmail-message-external-initial",
            "gmail-thread-external",
            fromEmail: "external@example.test"));

        var reply = await harness.Processor.ProcessAsync(CreateMessage(
            "gmail-message-external-reply",
            "gmail-thread-external",
            fromEmail: "external@example.test",
            attachments:
            [
                new InboundEmailAttachment(
                    "reply.png",
                    "image/png",
                    "external-att-1",
                    MinimalPngBytes,
                    MinimalPngBytes.Length)
            ]));

        Assert.Equal(InboundEmailProcessingStatus.MessageAdded, reply.Status);
        Assert.Equal(initial.TicketId, reply.TicketId);
        var message = Assert.Single(await harness.DbContext.TicketMessages.ToListAsync());
        var processed = await harness.DbContext.ProcessedEmailMessages
            .SingleAsync(item => item.GmailMessageId == "gmail-message-external-reply");
        Assert.Null(message.SenderId);
        Assert.NotNull(message.ExternalRequesterId);
        Assert.Equal(message.Id, processed.TicketMessageId);
        Assert.Equal(reply.TicketMessageId, processed.TicketMessageId);
        var attachment = Assert.Single(await harness.DbContext.Attachments.ToListAsync());
        Assert.Equal(message.Id, attachment.MessageId);
        Assert.Null(attachment.UploadedByUserId);
        Assert.Equal(message.ExternalRequesterId, attachment.UploadedByExternalRequesterId);
    }

    [Fact]
    public async Task ExternalRequesterReply_FromAnotherRequester_IsRejected()
    {
        await using var harness = await TestHarness.CreateAsync();
        var initial = await harness.Processor.ProcessAsync(CreateMessage(
            "gmail-message-external-owner",
            "gmail-thread-external-owner",
            fromEmail: "owner@example.test"));

        var mismatch = await harness.Processor.ProcessAsync(CreateMessage(
            "gmail-message-external-mismatch",
            "gmail-thread-external-mismatch",
            fromEmail: "other@example.test",
            subject: initial.TicketNumber!));

        Assert.Equal(InboundEmailProcessingStatus.SenderMismatch, mismatch.Status);
        Assert.Equal(initial.TicketId, mismatch.TicketId);
        Assert.Empty(await harness.DbContext.TicketMessages.ToListAsync());
        Assert.Single(await harness.DbContext.ExternalRequesters.ToListAsync());
    }

    [Fact]
    public async Task ExternalRequesterReply_ReopensResolvedTicketWithoutFakeHistoryActor()
    {
        await using var harness = await TestHarness.CreateAsync();
        var initial = await harness.Processor.ProcessAsync(CreateMessage(
            "gmail-message-external-resolved",
            "gmail-thread-external-resolved",
            fromEmail: "external@example.test"));
        var ticket = await harness.DbContext.Tickets.SingleAsync(item => item.Id == initial.TicketId);
        ticket.Status = TicketStatus.Resolved;
        await harness.DbContext.SaveChangesAsync();

        var reply = await harness.Processor.ProcessAsync(CreateMessage(
            "gmail-message-external-reopened",
            "gmail-thread-external-resolved",
            fromEmail: "external@example.test"));

        Assert.Equal(InboundEmailProcessingStatus.MessageAdded, reply.Status);
        var reopened = await harness.DbContext.Tickets.SingleAsync();
        Assert.Equal(TicketStatus.Reopened, reopened.Status);
        Assert.Null(reopened.ClosedAt);
        Assert.Empty(await harness.DbContext.TicketStatusHistories.ToListAsync());
    }

    [Fact]
    public async Task InactiveSender_ReturnsSpecificUnknownCustomerReason()
    {
        await using var harness = await TestHarness.CreateAsync();
        harness.Customer.IsActive = false;
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Processor.ProcessAsync(CreateMessage(
            "gmail-message-inactive",
            "gmail-thread-inactive"));

        Assert.Equal(InboundEmailProcessingStatus.UnknownCustomer, result.Status);
        Assert.Equal("The sender account is inactive.", result.Reason);
        Assert.Empty(await harness.DbContext.ProcessedEmailMessages.ToListAsync());
        Assert.Empty(await harness.DbContext.ExternalRequesters.ToListAsync());
    }

    [Fact]
    public async Task NonCustomerSender_ReturnsSpecificUnknownCustomerReason()
    {
        await using var harness = await TestHarness.CreateAsync();

        var result = await harness.Processor.ProcessAsync(CreateMessage(
            "gmail-message-staff",
            "gmail-thread-staff",
            fromEmail: harness.TechnicalStaff.Email));

        Assert.Equal(InboundEmailProcessingStatus.UnknownCustomer, result.Status);
        Assert.Equal(
            "The sender account does not have the Customer role.",
            result.Reason);
        Assert.Empty(await harness.DbContext.ProcessedEmailMessages.ToListAsync());
    }

    [Fact]
    public async Task NoCorrelation_WithValidAttachment_PersistsAttachment()
    {
        await using var harness = await TestHarness.CreateAsync();

        var result = await harness.Processor.ProcessAsync(CreateMessage(
            "gmail-message-1",
            "gmail-thread-1",
            attachments:
            [
                new InboundEmailAttachment(
                    "vpn.png",
                    "image/png",
                    "att-1",
                    MinimalPngBytes,
                    MinimalPngBytes.Length)
            ]));

        Assert.Equal(InboundEmailProcessingStatus.NewTicketCreated, result.Status);
        var attachment = Assert.Single(await harness.DbContext.Attachments.ToListAsync());
        Assert.Equal("vpn.png", attachment.OriginalFileName);
        Assert.Equal("image/png", attachment.ContentType);
        Assert.Equal(MinimalPngBytes.Length, attachment.FileSize);
        Assert.True(File.Exists(Path.Combine(
            harness.ContentRootPath,
            attachment.FilePath.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public async Task NoCorrelation_WithUnsupportedAttachment_StillCreatesTicket()
    {
        await using var harness = await TestHarness.CreateAsync();
        var invalidBytes = System.Text.Encoding.UTF8.GetBytes("not-a-valid-file");

        var result = await harness.Processor.ProcessAsync(CreateMessage(
            "gmail-message-1",
            "gmail-thread-1",
            attachments:
            [
                new InboundEmailAttachment(
                    "zararli.exe",
                    "application/octet-stream",
                    "att-1",
                    invalidBytes,
                    invalidBytes.LongLength)
            ]));

        Assert.Equal(InboundEmailProcessingStatus.NewTicketCreated, result.Status);
        Assert.Single(await harness.DbContext.Tickets.ToListAsync());
        Assert.Empty(await harness.DbContext.Attachments.ToListAsync());
        Assert.Single(await harness.DbContext.ProcessedEmailMessages.ToListAsync());
    }

    [Fact]
    public async Task AcknowledgmentSendFailure_DoesNotRollbackCreatedTicket()
    {
        await using var harness = await TestHarness.CreateAsync();
        harness.GmailSendService.ThrowOnSend = new InvalidOperationException("Simulated Gmail failure.");

        var result = await harness.Processor.ProcessAsync(CreateMessage(
            "gmail-message-1",
            "gmail-thread-1"));

        Assert.Equal(InboundEmailProcessingStatus.NewTicketCreated, result.Status);
        Assert.Single(await harness.DbContext.Tickets.ToListAsync());
        Assert.Single(await harness.DbContext.TicketEmailThreads.ToListAsync());
        Assert.Single(await harness.DbContext.ProcessedEmailMessages.ToListAsync());
        Assert.Equal(1, harness.GmailSendService.SendCount);
    }

    [Fact]
    public async Task ReprocessingSameGmailMessageId_ReturnsAlreadyProcessedWithoutAcknowledgment()
    {
        await using var harness = await TestHarness.CreateAsync();
        var ticket = await harness.AddTicketAsync(harness.Customer);
        await harness.AddProcessedMessageAsync(
            ticket,
            "gmail-thread-1",
            "gmail-message-1",
            "<original@example.test>");

        var result = await harness.Processor.ProcessAsync(CreateMessage(
            "gmail-message-1",
            "gmail-thread-1"));

        Assert.Equal(InboundEmailProcessingStatus.AlreadyProcessed, result.Status);
        Assert.Empty(await harness.DbContext.TicketMessages.ToListAsync());
        Assert.Single(await harness.DbContext.ProcessedEmailMessages.ToListAsync());
        Assert.Empty(harness.GmailSendService.SentMessages);
    }

    [Fact]
    public async Task FallbackWithDifferentThread_CreatesAliasWithoutCreatingTicket()
    {
        await using var harness = await TestHarness.CreateAsync();
        var ticket = await harness.AddTicketAsync(harness.Customer);
        await harness.AddProcessedMessageAsync(
            ticket,
            "gmail-thread-1",
            "gmail-message-1",
            "<original@example.test>");

        var result = await harness.Processor.ProcessAsync(CreateMessage(
            "gmail-message-2",
            "gmail-thread-alias",
            inReplyTo: "<original@example.test>"));

        Assert.Equal(InboundEmailProcessingStatus.MessageAdded, result.Status);
        Assert.Equal(1, await harness.DbContext.Tickets.CountAsync());
        Assert.Equal(2, await harness.DbContext.TicketEmailThreads
            .CountAsync(thread => thread.TicketId == ticket.Id));
        Assert.Equal(2, await harness.DbContext.ProcessedEmailMessages.CountAsync());
        Assert.Equal(1, await harness.DbContext.TicketMessages.CountAsync());
        Assert.Empty(harness.GmailSendService.SentMessages);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public async Task CorrelatedReply_ReopensResolvedOrClosedTicket(TicketStatus status)
    {
        await using var harness = await TestHarness.CreateAsync();
        var ticket = await harness.AddTicketAsync(harness.Customer, status);
        await harness.AddProcessedMessageAsync(
            ticket,
            "gmail-thread-1",
            "gmail-message-1",
            "<original@example.test>");

        var result = await harness.Processor.ProcessAsync(CreateMessage(
            "gmail-message-2",
            "gmail-thread-2",
            inReplyTo: "<original@example.test>"));

        Assert.Equal(InboundEmailProcessingStatus.MessageAdded, result.Status);
        var updated = await harness.DbContext.Tickets.SingleAsync();
        Assert.Equal(TicketStatus.Reopened, updated.Status);
        Assert.Null(updated.ClosedAt);
        Assert.Single(await harness.DbContext.TicketStatusHistories.ToListAsync());
        Assert.Empty(harness.GmailSendService.SentMessages);
    }

    [Fact]
    public async Task CorrelatedReply_WithAttachment_AddsMessageAndPersistsAttachment()
    {
        await using var harness = await TestHarness.CreateAsync();
        var ticket = await harness.AddTicketAsync(harness.Customer);
        await harness.AddProcessedMessageAsync(
            ticket,
            "gmail-thread-1",
            "gmail-message-1",
            "<original@example.test>");

        var result = await harness.Processor.ProcessAsync(CreateMessage(
            "gmail-message-2",
            "gmail-thread-2",
            inReplyTo: "<original@example.test>",
            attachments:
            [
                new InboundEmailAttachment(
                    "vpn.png",
                    "image/png",
                    "att-2",
                    MinimalPngBytes,
                    MinimalPngBytes.Length)
            ]));

        Assert.Equal(InboundEmailProcessingStatus.MessageAdded, result.Status);
        Assert.Single(await harness.DbContext.TicketMessages.ToListAsync());
        var attachment = Assert.Single(await harness.DbContext.Attachments.ToListAsync());
        Assert.Equal(ticket.Id, attachment.TicketId);
        Assert.Null(attachment.MessageId);
    }

    [Fact]
    public async Task SelfMailboxMessage_IsIgnoredAndDoesNotCreateTicket()
    {
        await using var harness = await TestHarness.CreateAsync();

        var result = await harness.Processor.ProcessAsync(CreateMessage(
            "gmail-message-9",
            "gmail-thread-9",
            fromEmail: "support11flow@gmail.com"));

        Assert.Equal(InboundEmailProcessingStatus.SelfMessageIgnored, result.Status);
        Assert.Empty(await harness.DbContext.Tickets.ToListAsync());
        Assert.Empty(await harness.DbContext.TicketEmailThreads.ToListAsync());
        Assert.Empty(await harness.DbContext.ProcessedEmailMessages.ToListAsync());
        Assert.Empty(harness.GmailSendService.SentMessages);
    }

    [Fact]
    public async Task SystemGeneratedMessage_IsSkippedWithoutCreatingBusinessDataOrAcknowledgment()
    {
        await using var harness = await TestHarness.CreateAsync();

        var result = await harness.Processor.ProcessAsync(CreateMessage(
            "bounce-message",
            "bounce-thread",
            fromEmail: "mailer-daemon@googlemail.com",
            isSystemGenerated: true));

        Assert.Equal(InboundEmailProcessingStatus.SystemMessageSkipped, result.Status);
        Assert.Empty(await harness.DbContext.Tickets.ToListAsync());
        Assert.Empty(await harness.DbContext.ExternalRequesters.ToListAsync());
        Assert.Empty(await harness.DbContext.TicketMessages.ToListAsync());
        Assert.Empty(await harness.DbContext.TicketEmailThreads.ToListAsync());
        Assert.Empty(await harness.DbContext.ProcessedEmailMessages.ToListAsync());
        Assert.Empty(await harness.DbContext.Notifications.ToListAsync());
        Assert.Empty(await harness.DbContext.TicketActivities.ToListAsync());
        Assert.Empty(harness.GmailSendService.SentMessages);
    }

    [Fact]
    public async Task DeliveryStatusSubjectWithoutSystemMetadata_IsProcessedNormally()
    {
        await using var harness = await TestHarness.CreateAsync();

        var result = await harness.Processor.ProcessAsync(CreateMessage(
            "customer-delivery-status-subject",
            "customer-delivery-status-thread",
            subject: "Delivery Status Notification"));

        Assert.Equal(InboundEmailProcessingStatus.NewTicketCreated, result.Status);
        Assert.Single(await harness.DbContext.Tickets.ToListAsync());
        Assert.Empty(await harness.DbContext.ExternalRequesters.ToListAsync());
        Assert.Single(harness.GmailSendService.SentMessages);
    }

    private static InboundEmailMessage CreateMessage(
        string gmailMessageId,
        string gmailThreadId,
        string? inReplyTo = null,
        IReadOnlyCollection<string>? references = null,
        string subject = "Printer issue",
        string fromEmail = "customer@example.test",
        string? fromName = "Customer",
        IReadOnlyCollection<InboundEmailAttachment>? attachments = null,
        DateTime? receivedAt = null,
        bool isSystemGenerated = false) =>
        new(
            gmailMessageId,
            gmailThreadId,
            $"<{gmailMessageId}@example.test>",
            inReplyTo,
            references ?? [],
            fromEmail,
            fromName,
            subject,
            "A real reply body.",
            receivedAt ?? DateTime.UtcNow,
            attachments ?? [],
            IsSystemGenerated: isSystemGenerated);

    private sealed class ThrowingTicketActivityWriter : ITicketActivityWriter
    {
        public TicketActivity Append(TicketActivityAppendRequest request) =>
            throw new InvalidOperationException("Simulated activity persistence failure.");
    }

    private sealed class TestHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private TestHarness(
            SqliteConnection connection,
            AppDbContext dbContext,
            string contentRootPath,
            User customer,
            User otherCustomer,
            User technicalStaff,
            User admin,
            RecordingGmailSendService gmailSendService,
        IInboundEmailProcessor processor,
        TicketService ticketService)
        {
            _connection = connection;
            DbContext = dbContext;
            ContentRootPath = contentRootPath;
            Customer = customer;
            OtherCustomer = otherCustomer;
            TechnicalStaff = technicalStaff;
            Admin = admin;
            GmailSendService = gmailSendService;
            Processor = processor;
            TicketService = ticketService;
        }

        public AppDbContext DbContext { get; }
        public string ContentRootPath { get; }
        public User Customer { get; }
        public User OtherCustomer { get; }
        public User TechnicalStaff { get; }
        public User Admin { get; }
        public RecordingGmailSendService GmailSendService { get; }
        public IInboundEmailProcessor Processor { get; }
        public TicketService TicketService { get; }

        public static async Task<TestHarness> CreateAsync(ITicketActivityWriter? activityWriter = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var dbContext = new AppDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();

            var customer = CreateCustomer("customer@example.test");
            var otherCustomer = CreateCustomer("other@example.test");
            var technicalStaff = new User
            {
                FirstName = "Tech",
                LastName = "User",
                Email = "tech@example.test",
                PasswordHash = "hash",
                Role = UserRole.TechnicalStaff,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var admin = new User
            {
                FirstName = "Admin",
                LastName = "User",
                Email = "admin@example.test",
                PasswordHash = "hash",
                Role = UserRole.Admin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.Users.AddRange(customer, otherCustomer, technicalStaff, admin);
            await dbContext.SaveChangesAsync();

            var contentRootPath = Path.Combine(
                Path.GetTempPath(),
                "supportflow-gmail-attachment-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(contentRootPath);

            var emailComposer = new TicketEmailComposer(
                Options.Create(new ApplicationSettings()));
            var gmailSendService = new RecordingGmailSendService();
            var ticketActivityWriter = activityWriter ?? new TicketActivityWriter(
                dbContext,
                new TicketActivityMetadataCodec(),
                TimeProvider.System);
            var attachmentService = new AttachmentService(
                dbContext,
                new TestHostEnvironment(contentRootPath),
                NullLogger<AttachmentService>.Instance,
                ticketActivityWriter);
            var slaTargetResolver = new SlaTargetResolver(Options.Create(new SlaSettings()));
            var ticketSlaService = new TicketSlaService(slaTargetResolver);
            var ticketService = new TicketService(
                dbContext,
                new NotificationService(dbContext),
                new NullEmailService(),
                emailComposer,
                gmailSendService,
                ticketSlaService,
                new TicketSlaCalculator(ticketSlaService, slaTargetResolver, TimeProvider.System),
                new QueueAgingCalculator(TimeProvider.System),
                ticketActivityWriter,
                NullLogger<TicketService>.Instance,
                TimeProvider.System,
                Options.Create(new QueueAgingSettings()));
            var ticketAcknowledgmentService = new TicketAcknowledgmentService(
                gmailSendService,
                new NotificationService(dbContext),
                NullLogger<TicketAcknowledgmentService>.Instance);
            var correlationService = new InboundEmailCorrelationService(dbContext);
            var processor = new InboundEmailProcessor(
                dbContext,
                ticketService,
                attachmentService,
                new NotificationService(dbContext),
                ticketAcknowledgmentService,
                correlationService,
                new InboundEmailSenderMatcher(dbContext),
                new ExternalRequesterService(dbContext),
                ticketActivityWriter,
                Options.Create(new GmailSettings
                {
                    MailboxAddress = "support11flow@gmail.com"
                }),
                NullLogger<InboundEmailProcessor>.Instance);

            return new TestHarness(
                connection,
                dbContext,
                contentRootPath,
                customer,
                otherCustomer,
                technicalStaff,
                admin,
                gmailSendService,
                processor,
                ticketService);
        }

        public async Task<Ticket> AddTicketAsync(
            User customer,
            TicketStatus status = TicketStatus.New)
        {
            var ticket = new Ticket
            {
                TicketNumber = $"TMP-{Guid.NewGuid():N}"[..30],
                CustomerId = customer.Id,
                Title = "Printer issue",
                Description = "Initial description",
                Status = status,
                Priority = TicketPriority.Normal,
                CreatedAt = DateTime.UtcNow.AddHours(-1),
                UpdatedAt = DateTime.UtcNow.AddHours(-1),
                ClosedAt = status == TicketStatus.Closed ? DateTime.UtcNow : null
            };
            DbContext.Tickets.Add(ticket);
            await DbContext.SaveChangesAsync();
            ticket.TicketNumber = $"SUP-2026-{ticket.Id:D6}";
            await DbContext.SaveChangesAsync();
            return ticket;
        }

        public async Task AddProcessedMessageAsync(
            Ticket ticket,
            string gmailThreadId,
            string gmailMessageId,
            string? internetMessageId)
        {
            var thread = new TicketEmailThread
            {
                TicketId = ticket.Id,
                GmailThreadId = gmailThreadId,
                CreatedAt = DateTime.UtcNow.AddMinutes(-30)
            };
            DbContext.TicketEmailThreads.Add(thread);
            DbContext.ProcessedEmailMessages.Add(new ProcessedEmailMessage
            {
                TicketEmailThread = thread,
                GmailMessageId = gmailMessageId,
                InternetMessageId = internetMessageId,
                ProcessedAt = DateTime.UtcNow.AddMinutes(-30)
            });
            await DbContext.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _connection.DisposeAsync();

            if (Directory.Exists(ContentRootPath))
            {
                Directory.Delete(ContentRootPath, recursive: true);
            }
        }

        private static User CreateCustomer(string email) =>
            new()
            {
                FirstName = "Test",
                LastName = "Customer",
                Email = email,
                PasswordHash = "not-used",
                Role = UserRole.Customer,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);
            ApplicationName = "SupportFlow.Api.Tests";
            EnvironmentName = Environments.Development;
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; }
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
    }

    private sealed class RecordingGmailSendService : IGmailSendService
    {
        public List<SentMessage> SentMessages { get; } = [];
        public int SendCount { get; private set; }
        public Exception? ThrowOnSend { get; set; }
        public Func<SentMessage, Task>? OnSendAsync { get; set; }

        public async Task SendPlainTextAsync(
            string toEmail,
            string subject,
            string body,
            CancellationToken cancellationToken = default)
        {
            var message = new SentMessage(toEmail, subject, body);
            SendCount++;
            SentMessages.Add(message);

            if (OnSendAsync is not null)
            {
                await OnSendAsync(message);
            }

            if (ThrowOnSend is not null)
            {
                throw ThrowOnSend;
            }
        }
    }

    private sealed record SentMessage(string ToEmail, string Subject, string Body);

    private sealed class NullEmailService : IEmailService
    {
        public Task SendAsync(
            string toEmail,
            string subject,
            string body,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
