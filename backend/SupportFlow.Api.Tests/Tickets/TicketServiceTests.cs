using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Data.Common;
using System.Text.RegularExpressions;
using SupportFlow.Api.Data;
using SupportFlow.Api.DTOs.Tickets;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;
using SupportFlow.Api.Services;
using SupportFlow.Api.Services.QueueAging;
using SupportFlow.Api.Services.Email;
using SupportFlow.Api.Services.Gmail;
using SupportFlow.Api.Services.Sla;
using SupportFlow.Api.Services.TicketActivities;
using SupportFlow.Api.Settings;

namespace SupportFlow.Api.Tests.Tickets;

public sealed class TicketServiceTests
{
    [Fact]
    public async Task CustomerCreateAsync_AppendsRequesterVisibleTicketCreatedActivity()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();

        var response = await harness.Service.CreateAsync(
            harness.Customer.Id,
            new CreateTicketRequest
            {
                Title = "Activity ticket",
                Description = "Description",
                Priority = TicketPriority.Normal
            });

        var activity = Assert.Single(await harness.DbContext.TicketActivities.ToListAsync());
        Assert.Equal(response!.Id, activity.TicketId);
        Assert.Equal(TicketActivityType.TicketCreated, activity.ActivityType);
        Assert.Equal(TicketActivitySource.Portal, activity.Source);
        Assert.Equal(TicketActivityVisibility.RequesterVisible, activity.Visibility);
        Assert.Equal(harness.Customer.Id, activity.ActorUserId);
        Assert.Equal(UserRole.Customer, activity.ActorRole);
        Assert.Equal("{}", activity.MetadataJson);
    }

    [Fact]
    public async Task PortalMessages_AppendPublicAndInternalActivitiesWithoutMessageBodiesInMetadata()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();

        await harness.Service.AddMessageAsync(
            harness.Ticket.Id,
            harness.Customer.Id,
            UserRole.Customer,
            new CreateTicketMessageRequest { Message = "Customer public body", IsInternal = false });
        await harness.Service.AddMessageAsync(
            harness.Ticket.Id,
            harness.TechnicalStaff.Id,
            UserRole.TechnicalStaff,
            new CreateTicketMessageRequest { Message = "Internal body", IsInternal = true });

        var activities = await harness.DbContext.TicketActivities
            .OrderBy(activity => activity.Id)
            .ToListAsync();
        Assert.Collection(
            activities,
            activity =>
            {
                Assert.Equal(TicketActivityType.MessageCreated, activity.ActivityType);
                Assert.Equal(TicketActivityVisibility.RequesterVisible, activity.Visibility);
                Assert.DoesNotContain("Customer public body", activity.MetadataJson);
            },
            activity =>
            {
                Assert.Equal(TicketActivityType.InternalNoteCreated, activity.ActivityType);
                Assert.Equal(TicketActivityVisibility.Internal, activity.Visibility);
                Assert.DoesNotContain("Internal body", activity.MetadataJson);
            });
        Assert.All(activities, activity => Assert.NotNull(activity.RelatedMessageId));
    }

    [Fact]
    public async Task CustomerReplyReopen_AppendsMessageAndLinkedStatusChangedActivities()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();
        harness.Ticket.Status = TicketStatus.Resolved;
        harness.Ticket.ClosedAt = new DateTime(2026, 8, 25, 9, 0, 0, DateTimeKind.Utc);
        await harness.DbContext.SaveChangesAsync();

        await harness.Service.AddMessageAsync(
            harness.Ticket.Id,
            harness.Customer.Id,
            UserRole.Customer,
            new CreateTicketMessageRequest { Message = "Please reopen", IsInternal = false });

        var activities = await harness.DbContext.TicketActivities
            .OrderBy(activity => activity.Id)
            .ToListAsync();
        var history = Assert.Single(await harness.DbContext.TicketStatusHistories.ToListAsync());
        Assert.Equal(TicketStatus.Reopened, (await harness.DbContext.Tickets.SingleAsync()).Status);
        Assert.Contains(activities, activity => activity.ActivityType == TicketActivityType.MessageCreated
            && activity.RelatedMessageId.HasValue);
        Assert.Contains(activities, activity => activity.ActivityType == TicketActivityType.StatusChanged
            && activity.RelatedStatusHistoryId == history.Id);
    }

    [Fact]
    public async Task StatusChange_AppendsLinkedActivityAndNoOpDoesNotAppendAnother()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();

        var changed = await harness.Service.UpdateStatusAsync(
            harness.Ticket.Id,
            harness.Admin.Id,
            UserRole.Admin,
            new UpdateTicketStatusRequest { Status = TicketStatus.WaitingForCustomer });
        var duplicate = await harness.Service.UpdateStatusAsync(
            harness.Ticket.Id,
            harness.Admin.Id,
            UserRole.Admin,
            new UpdateTicketStatusRequest { Status = TicketStatus.WaitingForCustomer });

        var history = Assert.Single(await harness.DbContext.TicketStatusHistories.ToListAsync());
        var activity = Assert.Single(await harness.DbContext.TicketActivities.ToListAsync());
        Assert.Equal(TicketOperationFailure.None, changed.Failure);
        Assert.Equal(TicketOperationFailure.InvalidRequest, duplicate.Failure);
        Assert.Equal(TicketActivityType.StatusChanged, activity.ActivityType);
        Assert.Equal(history.Id, activity.RelatedStatusHistoryId);
        var metadata = Assert.IsType<StatusChangedTicketActivityMetadata>(
            new TicketActivityMetadataCodec().Deserialize(
                activity.ActivityType,
                activity.MetadataVersion,
                activity.MetadataJson));
        Assert.Equal(TicketStatus.InProgress, metadata.OldStatus);
        Assert.Equal(TicketStatus.WaitingForCustomer, metadata.NewStatus);
    }

    [Fact]
    public async Task PriorityAssignmentAndClassification_AppendInternalTypedActivities()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();
        var category = new TicketCategory { Name = "Network", IsActive = true };
        var tag = new TicketTag { Name = "VPN", IsActive = true };
        harness.DbContext.AddRange(category, tag);
        harness.Ticket.Status = TicketStatus.New;
        await harness.DbContext.SaveChangesAsync();

        await harness.Service.UpdatePriorityAsync(
            harness.Ticket.Id, harness.Admin.Id, UserRole.Admin,
            new UpdateTicketPriorityRequest { Priority = TicketPriority.High });
        await harness.Service.AssignAsync(
            harness.Ticket.Id, harness.Admin.Id, UserRole.Admin,
            new AssignTicketRequest { AssignedUserId = harness.TechnicalStaff.Id });
        await harness.Service.UpdateClassificationAsync(
            harness.Ticket.Id, harness.Admin.Id, UserRole.Admin,
            new UpdateTicketClassificationRequest { CategoryId = category.Id, TagIds = [tag.Id] });

        var activities = await harness.DbContext.TicketActivities.ToListAsync();
        Assert.Contains(activities, activity => activity.ActivityType == TicketActivityType.PriorityChanged
            && activity.Visibility == TicketActivityVisibility.Internal);
        Assert.Contains(activities, activity => activity.ActivityType == TicketActivityType.AssignmentChanged
            && activity.Visibility == TicketActivityVisibility.Internal);
        Assert.Contains(activities, activity => activity.ActivityType == TicketActivityType.ClassificationChanged
            && activity.Visibility == TicketActivityVisibility.Internal
            && activity.MetadataJson.Contains("VPN", StringComparison.Ordinal));
        Assert.Contains(activities, activity => activity.ActivityType == TicketActivityType.StatusChanged
            && activity.RelatedStatusHistoryId.HasValue);
    }

    [Theory]
    [InlineData("status")]
    [InlineData("priority")]
    [InlineData("assignment")]
    [InlineData("classification")]
    [InlineData("message")]
    public async Task ActivityWriterFailure_RollsBackPortalBusinessMutation(string operation)
    {
        await using var harness = await TicketServiceHarness.CreateAsync(
            ticketActivityWriter: new ThrowingTicketActivityWriter());
        var category = new TicketCategory { Name = "Network", IsActive = true };
        harness.DbContext.TicketCategories.Add(category);
        await harness.DbContext.SaveChangesAsync();

        switch (operation)
        {
            case "status":
                await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Service.UpdateStatusAsync(
                    harness.Ticket.Id, harness.Admin.Id, UserRole.Admin,
                    new UpdateTicketStatusRequest { Status = TicketStatus.WaitingForCustomer }));
                harness.DbContext.ChangeTracker.Clear();
                Assert.Equal(TicketStatus.InProgress, (await harness.DbContext.Tickets.SingleAsync()).Status);
                break;
            case "priority":
                await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Service.UpdatePriorityAsync(
                    harness.Ticket.Id, harness.Admin.Id, UserRole.Admin,
                    new UpdateTicketPriorityRequest { Priority = TicketPriority.High }));
                harness.DbContext.ChangeTracker.Clear();
                Assert.Equal(TicketPriority.Normal, (await harness.DbContext.Tickets.SingleAsync()).Priority);
                break;
            case "assignment":
                await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Service.AssignAsync(
                    harness.Ticket.Id, harness.Admin.Id, UserRole.Admin,
                    new AssignTicketRequest { AssignedUserId = harness.TechnicalStaff.Id }));
                harness.DbContext.ChangeTracker.Clear();
                Assert.Null((await harness.DbContext.Tickets.SingleAsync()).AssignedUserId);
                break;
            case "classification":
                await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Service.UpdateClassificationAsync(
                    harness.Ticket.Id, harness.Admin.Id, UserRole.Admin,
                    new UpdateTicketClassificationRequest { CategoryId = category.Id, TagIds = [] }));
                harness.DbContext.ChangeTracker.Clear();
                Assert.Null((await harness.DbContext.Tickets.SingleAsync()).CategoryId);
                break;
            default:
                await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Service.AddMessageAsync(
                    harness.Ticket.Id, harness.Customer.Id, UserRole.Customer,
                    new CreateTicketMessageRequest { Message = "Rollback", IsInternal = false }));
                harness.DbContext.ChangeTracker.Clear();
                Assert.Empty(await harness.DbContext.TicketMessages.ToListAsync());
                break;
        }

        Assert.Empty(await harness.DbContext.TicketActivities.ToListAsync());
    }

    [Fact]
    public async Task InternalTechnicalNote_DoesNotSendAnyCustomerEmail()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();

        var result = await harness.Service.AddMessageAsync(
            harness.Ticket.Id,
            harness.TechnicalStaff.Id,
            UserRole.TechnicalStaff,
            new CreateTicketMessageRequest
            {
                Message = "Only an internal note.",
                IsInternal = true
            });

        Assert.NotNull(result.Value);
        Assert.Equal(TicketOperationFailure.None, result.Failure);
        Assert.Empty(harness.GmailSendService.Messages);
        Assert.Empty(await harness.DbContext.Notifications.Where(item => item.UserId == harness.Customer.Id).ToListAsync());
    }

    [Fact]
    public async Task CustomerCreateAsync_CreatesStaffAndAdminNotifications()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();

        var response = await harness.Service.CreateAsync(
            harness.Customer.Id,
            new CreateTicketRequest
            {
                Title = "Yeni talep",
                Description = "Açıklama",
                Priority = TicketPriority.Normal
            });

        Assert.NotNull(response);
        Assert.True(response.Sla.IsTracked);
        Assert.Equal(response.CreatedAt, response.Sla.ResolutionCycleStartedAt);
        Assert.Equal(response.CreatedAt.AddHours(8), response.Sla.FirstResponse.DueAt);
        Assert.Equal(response.CreatedAt.AddHours(48), response.Sla.Resolution.DueAt);
        var notifications = await harness.DbContext.Notifications
            .Where(item => item.Type == NotificationType.TicketCreated)
            .OrderBy(item => item.UserId)
            .ToListAsync();
        Assert.Equal(2, notifications.Count);
        Assert.Equal(new[] { harness.TechnicalStaff.Id, harness.Admin.Id }, notifications.Select(item => item.UserId).ToArray());
    }

    [Fact]
    public async Task CreateTicketRecordAsync_InitializesSlaFromTheProvidedCreationTimestamp()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();
        var createdAt = new DateTime(2026, 8, 23, 9, 0, 0, DateTimeKind.Utc);

        var ticket = await harness.Service.CreateTicketRecordAsync(
            harness.Customer,
            "SLA portal ticket",
            "Description",
            TicketPriority.High,
            createdAt,
            CancellationToken.None);

        Assert.Equal(createdAt, ticket.ResolutionCycleStartedAt);
        Assert.Equal(createdAt.AddHours(2), ticket.FirstResponseDueAt);
        Assert.Equal(createdAt.AddHours(16), ticket.ResolutionDueAt);
    }

    [Fact]
    public async Task TicketResponses_ExposeListSummaryAndDetailSlaProjection()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();
        var createdAt = new DateTime(2026, 8, 23, 9, 0, 0, DateTimeKind.Utc);
        var ticket = await harness.Service.CreateTicketRecordAsync(
            harness.Customer,
            "SLA projection",
            "Description",
            TicketPriority.Normal,
            createdAt,
            CancellationToken.None);

        var detail = await harness.Service.GetByIdAsync(
            ticket.Id,
            harness.Admin.Id,
            UserRole.Admin);
        var listItem = Assert.Single(
            (await harness.Service.GetAllAsync(harness.Admin.Id, UserRole.Admin)).Items,
            item => item.Id == ticket.Id);

        Assert.NotNull(detail);
        Assert.True(detail.Sla.IsTracked);
        Assert.Equal(ticket.FirstResponseDueAt, detail.Sla.FirstResponse.DueAt);
        Assert.Equal(ticket.ResolutionCycleStartedAt, detail.Sla.ResolutionCycleStartedAt);
        Assert.Equal(ticket.ResolutionDueAt, detail.Sla.Resolution.DueAt);
        Assert.Equal(detail.Sla.FirstResponse.State, listItem.Sla.FirstResponseState);
        Assert.Equal(detail.Sla.Resolution.State, listItem.Sla.ResolutionState);
    }

    [Fact]
    public async Task AssignAsync_CreatesAssignmentNotificationForNewAssignee()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();

        var result = await harness.Service.AssignAsync(
            harness.Ticket.Id,
            harness.Admin.Id,
            UserRole.Admin,
        new AssignTicketRequest { AssignedUserId = harness.TechnicalStaff.Id });

        Assert.Equal(TicketOperationFailure.None, result.Failure);
        var notification = Assert.Single(await harness.DbContext.Notifications.ToListAsync());
        Assert.Equal(NotificationType.Assignment, notification.Type);
        Assert.Equal(harness.TechnicalStaff.Id, notification.UserId);
    }

    [Fact]
    public async Task TechnicalPublicReply_CreatesCustomerNotification()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();

        var result = await harness.Service.AddMessageAsync(
            harness.Ticket.Id,
            harness.TechnicalStaff.Id,
            UserRole.TechnicalStaff,
            new CreateTicketMessageRequest
            {
                Message = "Genel yanıt",
                IsInternal = false
            });

        Assert.Equal(TicketOperationFailure.None, result.Failure);
        var email = Assert.Single(harness.GmailSendService.Messages);
        Assert.Equal(harness.Customer.Email, email.ToEmail);
        Assert.Equal(OutboundDeliveryStatus.Sent, result.Value!.DeliveryStatus);
        Assert.Equal(1, result.Value.DeliveryAttemptCount);
        Assert.NotNull(result.Value.DeliveredAt);
        var notification = Assert.Single(await harness.DbContext.Notifications.ToListAsync());
        Assert.Equal(NotificationType.TechnicalReply, notification.Type);
        Assert.Equal(harness.Customer.Id, notification.UserId);
    }

    [Fact]
    public async Task TrackedTicket_SuccessfulPublicReplyCompletesFirstResponseAtDeliveryTime()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();
        var ticket = await harness.Service.CreateTicketRecordAsync(
            harness.Customer,
            "Tracked reply",
            "Description",
            TicketPriority.Normal,
            new DateTime(2026, 8, 23, 9, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        var result = await harness.Service.AddMessageAsync(
            ticket.Id,
            harness.TechnicalStaff.Id,
            UserRole.TechnicalStaff,
            new CreateTicketMessageRequest { Message = "Delivered reply", IsInternal = false });

        Assert.Equal(TicketOperationFailure.None, result.Failure);
        var persistedTicket = await harness.DbContext.Tickets.SingleAsync(item => item.Id == ticket.Id);
        Assert.Equal(result.Value!.DeliveredAt, persistedTicket.FirstRespondedAt);
    }

    [Fact]
    public async Task TechnicalPublicReply_SendsExternalRequesterAnEmailWithoutPortalLink()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();
        var requester = new ExternalRequester
        {
            Email = "external@example.test",
            NormalizedEmail = "external@example.test",
            DisplayName = "External Requester"
        };
        harness.DbContext.ExternalRequesters.Add(requester);
        await harness.DbContext.SaveChangesAsync();

        var ticket = new Ticket
        {
            TicketNumber = "SUP-2026-009001",
            ExternalRequesterId = requester.Id,
            Title = "External request",
            Description = "desc",
            Status = TicketStatus.New,
            Priority = TicketPriority.Normal
        };
        harness.DbContext.Tickets.Add(ticket);
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.AddMessageAsync(
            ticket.Id,
            harness.TechnicalStaff.Id,
            UserRole.TechnicalStaff,
            new CreateTicketMessageRequest { Message = "Public response", IsInternal = false });

        Assert.Equal(TicketOperationFailure.None, result.Failure);
        var email = Assert.Single(harness.GmailSendService.Messages);
        Assert.Equal(requester.Email, email.ToEmail);
        Assert.Equal($"[{ticket.TicketNumber}] {ticket.Title}", email.Subject);
        Assert.DoesNotContain("customer/tickets", email.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await harness.DbContext.Notifications.ToListAsync());
    }

    [Fact]
    public async Task TechnicalInternalNote_DoesNotEmailExternalRequester()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();
        var requester = new ExternalRequester
        {
            Email = "external@example.test",
            NormalizedEmail = "external@example.test"
        };
        harness.DbContext.ExternalRequesters.Add(requester);
        await harness.DbContext.SaveChangesAsync();

        var ticket = new Ticket
        {
            TicketNumber = "SUP-2026-009002",
            ExternalRequesterId = requester.Id,
            Title = "External request",
            Description = "desc",
            Status = TicketStatus.New,
            Priority = TicketPriority.Normal
        };
        harness.DbContext.Tickets.Add(ticket);
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.AddMessageAsync(
            ticket.Id,
            harness.TechnicalStaff.Id,
            UserRole.TechnicalStaff,
            new CreateTicketMessageRequest { Message = "Internal only", IsInternal = true });

        Assert.Equal(TicketOperationFailure.None, result.Failure);
        Assert.Empty(harness.GmailSendService.Messages);
        var message = Assert.Single(await harness.DbContext.TicketMessages.ToListAsync());
        Assert.Equal(OutboundDeliveryStatus.NotApplicable, message.OutboundDeliveryStatus);
    }

    [Fact]
    public async Task TechnicalPublicReply_GmailDeliveryFailure_PersistsMessageAndReturnsPartialSuccess()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();
        harness.GmailSendService.ThrowOnSend = new InvalidOperationException("Gmail send failed.");

        var result = await harness.Service.AddMessageAsync(
            harness.Ticket.Id,
            harness.TechnicalStaff.Id,
            UserRole.TechnicalStaff,
            new CreateTicketMessageRequest { Message = "Delivery failure", IsInternal = false });

        Assert.Equal(TicketOperationFailure.DeliveryFailed, result.Failure);
        Assert.NotNull(result.Value);
        Assert.Single(harness.GmailSendService.Messages);
        var message = Assert.Single(await harness.DbContext.TicketMessages.ToListAsync());
        Assert.Equal("Delivery failure", message.Message);
        Assert.Equal(OutboundDeliveryStatus.Failed, message.OutboundDeliveryStatus);
        Assert.Equal(1, message.DeliveryAttemptCount);
        Assert.NotNull(message.LastDeliveryAttemptAt);
        Assert.Null(message.DeliveredAt);
    }

    [Fact]
    public async Task RetryMessageDeliveryAsync_UsesTheSameFailedMessageAndMarksItSent()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();
        harness.GmailSendService.ThrowOnSend = new InvalidOperationException("Gmail send failed.");

        var initial = await harness.Service.AddMessageAsync(
            harness.Ticket.Id,
            harness.TechnicalStaff.Id,
            UserRole.TechnicalStaff,
            new CreateTicketMessageRequest { Message = "Retry this reply", IsInternal = false });

        var failedMessageId = initial.Value!.Id;
        harness.GmailSendService.ThrowOnSend = null;

        var retry = await harness.Service.RetryMessageDeliveryAsync(
            harness.Ticket.Id,
            failedMessageId,
            harness.Admin.Id,
            UserRole.Admin);

        Assert.Equal(TicketOperationFailure.None, retry.Failure);
        Assert.Equal(failedMessageId, retry.Value!.Id);
        Assert.Equal(OutboundDeliveryStatus.Sent, retry.Value.DeliveryStatus);
        Assert.Equal(2, retry.Value.DeliveryAttemptCount);
        Assert.Single(await harness.DbContext.TicketMessages.ToListAsync());
        Assert.Equal(2, harness.GmailSendService.Messages.Count);

        var secondRetry = await harness.Service.RetryMessageDeliveryAsync(
            harness.Ticket.Id,
            failedMessageId,
            harness.Admin.Id,
            UserRole.Admin);

        Assert.Equal(TicketOperationFailure.InvalidRequest, secondRetry.Failure);
        Assert.Equal(2, harness.GmailSendService.Messages.Count);
    }

    [Fact]
    public async Task TrackedTicket_FailedReplyThenSuccessfulRetryCompletesFirstResponseOnce()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();
        var ticket = await harness.Service.CreateTicketRecordAsync(
            harness.Customer,
            "Tracked retry",
            "Description",
            TicketPriority.Normal,
            new DateTime(2026, 8, 23, 9, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);
        harness.GmailSendService.ThrowOnSend = new InvalidOperationException("Delivery failed.");

        var failed = await harness.Service.AddMessageAsync(
            ticket.Id,
            harness.TechnicalStaff.Id,
            UserRole.TechnicalStaff,
            new CreateTicketMessageRequest { Message = "Retry reply", IsInternal = false });
        Assert.Null((await harness.DbContext.Tickets.SingleAsync(item => item.Id == ticket.Id)).FirstRespondedAt);

        harness.GmailSendService.ThrowOnSend = null;
        var retried = await harness.Service.RetryMessageDeliveryAsync(
            ticket.Id,
            failed.Value!.Id,
            harness.Admin.Id,
            UserRole.Admin);

        var persistedTicket = await harness.DbContext.Tickets.SingleAsync(item => item.Id == ticket.Id);
        Assert.Equal(retried.Value!.DeliveredAt, persistedTicket.FirstRespondedAt);
        Assert.Single(await harness.DbContext.TicketMessages.Where(item => item.TicketId == ticket.Id).ToListAsync());
    }

    [Fact]
    public async Task RetryMessageDeliveryAsync_FailureKeepsTheMessageFailedAndIncrementsAttempts()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();
        harness.GmailSendService.ThrowOnSend = new InvalidOperationException("Gmail send failed.");

        var initial = await harness.Service.AddMessageAsync(
            harness.Ticket.Id,
            harness.TechnicalStaff.Id,
            UserRole.TechnicalStaff,
            new CreateTicketMessageRequest { Message = "Retry failure", IsInternal = false });

        var retry = await harness.Service.RetryMessageDeliveryAsync(
            harness.Ticket.Id,
            initial.Value!.Id,
            harness.TechnicalStaff.Id,
            UserRole.TechnicalStaff);

        Assert.Equal(TicketOperationFailure.DeliveryFailed, retry.Failure);
        Assert.Equal(initial.Value.Id, retry.Value!.Id);
        Assert.Equal(OutboundDeliveryStatus.Failed, retry.Value.DeliveryStatus);
        Assert.Equal(2, retry.Value.DeliveryAttemptCount);
        Assert.Single(await harness.DbContext.TicketMessages.ToListAsync());
    }

    [Fact]
    public async Task RetryMessageDeliveryAsync_UsesTheExternalRequesterRecipient()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();
        var requester = new ExternalRequester
        {
            Email = "external-retry@example.test",
            NormalizedEmail = "external-retry@example.test",
            DisplayName = "External Retry"
        };
        harness.DbContext.ExternalRequesters.Add(requester);
        await harness.DbContext.SaveChangesAsync();

        var ticket = new Ticket
        {
            TicketNumber = "SUP-2026-009003",
            ExternalRequesterId = requester.Id,
            Title = "External retry",
            Description = "desc",
            Status = TicketStatus.New,
            Priority = TicketPriority.Normal
        };
        harness.DbContext.Tickets.Add(ticket);
        await harness.DbContext.SaveChangesAsync();

        harness.GmailSendService.ThrowOnSend = new InvalidOperationException("Gmail send failed.");
        var initial = await harness.Service.AddMessageAsync(
            ticket.Id,
            harness.TechnicalStaff.Id,
            UserRole.TechnicalStaff,
            new CreateTicketMessageRequest { Message = "External retry message", IsInternal = false });

        harness.GmailSendService.ThrowOnSend = null;
        var retry = await harness.Service.RetryMessageDeliveryAsync(
            ticket.Id,
            initial.Value!.Id,
            harness.TechnicalStaff.Id,
            UserRole.TechnicalStaff);

        Assert.Equal(TicketOperationFailure.None, retry.Failure);
        Assert.Equal(initial.Value.Id, retry.Value!.Id);
        Assert.Equal(OutboundDeliveryStatus.Sent, retry.Value.DeliveryStatus);
        Assert.Equal(2, retry.Value.DeliveryAttemptCount);
        Assert.Equal(requester.Email, harness.GmailSendService.Messages.Last().ToEmail);
        Assert.Single(await harness.DbContext.TicketMessages.Where(item => item.TicketId == ticket.Id).ToListAsync());
    }

    [Fact]
    public async Task CustomerPublicReply_HasNoOutboundDeliveryState()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();

        var result = await harness.Service.AddMessageAsync(
            harness.Ticket.Id,
            harness.Customer.Id,
            UserRole.Customer,
            new CreateTicketMessageRequest { Message = "Customer response", IsInternal = false });

        Assert.Equal(TicketOperationFailure.None, result.Failure);
        Assert.Equal(OutboundDeliveryStatus.NotApplicable, result.Value!.DeliveryStatus);
        Assert.Equal(0, result.Value.DeliveryAttemptCount);
    }

    [Fact]
    public async Task GetMessagesAsync_ForCustomerHidesFailedStaffRepliesButKeepsSentAndLegacyReplies()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();
        harness.DbContext.TicketMessages.AddRange(
            new TicketMessage
            {
                TicketId = harness.Ticket.Id,
                SenderId = harness.TechnicalStaff.Id,
                Message = "Failed reply",
                OutboundDeliveryStatus = OutboundDeliveryStatus.Failed
            },
            new TicketMessage
            {
                TicketId = harness.Ticket.Id,
                SenderId = harness.TechnicalStaff.Id,
                Message = "Sent reply",
                OutboundDeliveryStatus = OutboundDeliveryStatus.Sent
            },
            new TicketMessage
            {
                TicketId = harness.Ticket.Id,
                SenderId = harness.TechnicalStaff.Id,
                Message = "Legacy reply",
                OutboundDeliveryStatus = OutboundDeliveryStatus.NotApplicable
            });
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetMessagesAsync(
            harness.Ticket.Id,
            harness.Customer.Id,
            UserRole.Customer);

        Assert.Equal(TicketOperationFailure.None, result.Failure);
        Assert.DoesNotContain(result.Value!, message => message.Message == "Failed reply");
        Assert.Contains(result.Value!, message => message.Message == "Sent reply");
        Assert.Contains(result.Value!, message => message.Message == "Legacy reply");
    }

    [Fact]
    public async Task UpdatePriorityAsync_AdminUpdatesPriorityWithoutChangingStatusOrRequester()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();
        harness.Ticket.Priority = TicketPriority.Low;
        var originalStatus = harness.Ticket.Status;
        var originalCustomerId = harness.Ticket.CustomerId;
        var originalExternalRequesterId = harness.Ticket.ExternalRequesterId;
        var originalAssignedUserId = harness.Ticket.AssignedUserId;
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.UpdatePriorityAsync(
            harness.Ticket.Id,
            harness.Admin.Id,
            UserRole.Admin,
            new UpdateTicketPriorityRequest { Priority = TicketPriority.High });

        Assert.Equal(TicketOperationFailure.None, result.Failure);
        Assert.Equal(TicketPriority.High, result.Value!.Priority);

        var ticket = await harness.DbContext.Tickets.SingleAsync();
        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(originalStatus, ticket.Status);
        Assert.Equal(originalCustomerId, ticket.CustomerId);
        Assert.Equal(originalExternalRequesterId, ticket.ExternalRequesterId);
        Assert.Equal(originalAssignedUserId, ticket.AssignedUserId);
        Assert.Empty(await harness.DbContext.TicketStatusHistories.ToListAsync());
    }

    [Fact]
    public async Task TrackedTicket_ResolvedThenClosed_PreservesFirstResolutionCompletion()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();
        var ticket = await harness.Service.CreateTicketRecordAsync(
            harness.Customer,
            "Tracked resolution",
            "Description",
            TicketPriority.Normal,
            new DateTime(2026, 8, 23, 9, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        var resolved = await harness.Service.UpdateStatusAsync(
            ticket.Id,
            harness.Admin.Id,
            UserRole.Admin,
            new UpdateTicketStatusRequest { Status = TicketStatus.Resolved });
        var resolvedAt = (await harness.DbContext.Tickets.SingleAsync(item => item.Id == ticket.Id)).ResolvedAt;

        var closed = await harness.Service.UpdateStatusAsync(
            ticket.Id,
            harness.Admin.Id,
            UserRole.Admin,
            new UpdateTicketStatusRequest { Status = TicketStatus.Closed });
        var persistedTicket = await harness.DbContext.Tickets.SingleAsync(item => item.Id == ticket.Id);

        Assert.Equal(TicketOperationFailure.None, resolved.Failure);
        Assert.Equal(TicketOperationFailure.None, closed.Failure);
        Assert.NotNull(resolvedAt);
        Assert.Equal(resolvedAt, persistedTicket.ResolvedAt);
    }

    [Fact]
    public async Task TrackedTicket_RequesterReplyReopensResolutionCycleWithoutRestartingFirstResponse()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();
        var ticket = await harness.Service.CreateTicketRecordAsync(
            harness.Customer,
            "Tracked reopen",
            "Description",
            TicketPriority.High,
            new DateTime(2026, 8, 23, 9, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);
        var originalFirstResponseDueAt = ticket.FirstResponseDueAt;
        ticket.FirstRespondedAt = new DateTime(2026, 8, 23, 9, 30, 0, DateTimeKind.Utc);
        ticket.Status = TicketStatus.Resolved;
        ticket.ResolvedAt = new DateTime(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc);
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.AddMessageAsync(
            ticket.Id,
            harness.Customer.Id,
            UserRole.Customer,
            new CreateTicketMessageRequest { Message = "Reopen", IsInternal = false });
        var persistedTicket = await harness.DbContext.Tickets.SingleAsync(item => item.Id == ticket.Id);

        Assert.Equal(TicketOperationFailure.None, result.Failure);
        Assert.Equal(TicketStatus.Reopened, persistedTicket.Status);
        Assert.Null(persistedTicket.ResolvedAt);
        Assert.Equal(originalFirstResponseDueAt, persistedTicket.FirstResponseDueAt);
        Assert.Equal(ticket.FirstRespondedAt, persistedTicket.FirstRespondedAt);
        Assert.NotEqual(new DateTime(2026, 8, 23, 9, 0, 0, DateTimeKind.Utc), persistedTicket.ResolutionCycleStartedAt);
    }

    [Fact]
    public async Task UpdatePriorityAsync_TechnicalStaffUpdatesPriority()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();

        var result = await harness.Service.UpdatePriorityAsync(
            harness.Ticket.Id,
            harness.TechnicalStaff.Id,
            UserRole.TechnicalStaff,
            new UpdateTicketPriorityRequest { Priority = TicketPriority.Critical });

        Assert.Equal(TicketOperationFailure.None, result.Failure);
        Assert.Equal(TicketPriority.Critical, result.Value!.Priority);
    }

    [Fact]
    public async Task UpdatePriorityAsync_CustomerIsForbidden()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();

        var result = await harness.Service.UpdatePriorityAsync(
            harness.Ticket.Id,
            harness.Customer.Id,
            UserRole.Customer,
            new UpdateTicketPriorityRequest { Priority = TicketPriority.High });

        Assert.Equal(TicketOperationFailure.Forbidden, result.Failure);
        Assert.Equal(TicketPriority.Normal, (await harness.DbContext.Tickets.SingleAsync()).Priority);
    }

    [Fact]
    public async Task UpdatePriorityAsync_RejectsInvalidPriority()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();

        var result = await harness.Service.UpdatePriorityAsync(
            harness.Ticket.Id,
            harness.Admin.Id,
            UserRole.Admin,
            new UpdateTicketPriorityRequest { Priority = (TicketPriority)999 });

        Assert.Equal(TicketOperationFailure.InvalidRequest, result.Failure);
        Assert.Equal(TicketPriority.Normal, (await harness.DbContext.Tickets.SingleAsync()).Priority);
    }

    [Fact]
    public async Task GetAllAsync_FiltersTicketsByEveryPriority()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();
        await AddTicketAsync(harness, TicketPriority.Low, TicketStatus.New, "Low priority");
        await AddTicketAsync(harness, TicketPriority.High, TicketStatus.InProgress, "High priority");
        await AddTicketAsync(harness, TicketPriority.Critical, TicketStatus.Resolved, "Critical priority");

        foreach (var priority in Enum.GetValues<TicketPriority>())
        {
            var tickets = (await harness.Service.GetAllAsync(
                harness.Admin.Id,
                UserRole.Admin,
                priority)).Items;

            Assert.NotEmpty(tickets);
            Assert.All(tickets, ticket => Assert.Equal(priority, ticket.Priority));
        }
    }

    [Fact]
    public async Task GetAllAsync_PriorityFilterRemainsCompatibleWithSearchAndStatusFiltering()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();
        await AddTicketAsync(harness, TicketPriority.High, TicketStatus.InProgress, "VPN erişim sorunu");
        await AddTicketAsync(harness, TicketPriority.High, TicketStatus.Resolved, "ERP erişim sorunu");
        await AddTicketAsync(harness, TicketPriority.Low, TicketStatus.InProgress, "VPN düşük öncelik");

        var highPriorityTickets = (await harness.Service.GetAllAsync(
            harness.TechnicalStaff.Id,
            UserRole.TechnicalStaff,
            TicketPriority.High)).Items;

        var searchAndPriority = highPriorityTickets
            .Where(ticket => ticket.Title.Contains("VPN", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var statusAndPriority = highPriorityTickets
            .Where(ticket => ticket.Status == TicketStatus.InProgress)
            .ToList();

        Assert.Single(searchAndPriority);
        Assert.Single(statusAndPriority);
        Assert.All(highPriorityTickets, ticket => Assert.Equal(TicketPriority.High, ticket.Priority));
    }

    [Fact]
    public async Task GetAllAsync_AgingFiltersUseInclusiveThresholdsAndExcludeInactiveTickets()
    {
        var now = new DateTime(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await TicketServiceHarness.CreateAsync(new FixedTimeProvider(now));
        harness.Ticket.CreatedAt = now.AddMinutes(-120);
        var belowTwoHours = CreateTicket(harness, "Below two hours", now.AddSeconds(-(119 * 60 + 59)));
        var atEightHours = CreateTicket(harness, "At eight hours", now.AddMinutes(-480));
        var belowEightHours = CreateTicket(harness, "Below eight hours", now.AddSeconds(-(479 * 60 + 59)));
        var atTwentyFourHours = CreateTicket(harness, "At twenty-four hours", now.AddMinutes(-1440));
        var belowTwentyFourHours = CreateTicket(harness, "Below twenty-four hours", now.AddSeconds(-(1439 * 60 + 59)));
        var resolved = CreateTicket(harness, "Resolved", now.AddHours(-30), TicketStatus.Resolved);
        var closed = CreateTicket(harness, "Closed", now.AddHours(-30), TicketStatus.Closed);
        harness.DbContext.Tickets.AddRange(
            belowTwoHours,
            atEightHours,
            belowEightHours,
            atTwentyFourHours,
            belowTwentyFourHours,
            resolved,
            closed);
        await harness.DbContext.SaveChangesAsync();

        var awaiting = (await harness.Service.GetAllAsync(
            harness.Admin.Id,
            UserRole.Admin,
            agingFilter: TicketAgingFilter.Awaiting)).Items;
        var twoHours = (await harness.Service.GetAllAsync(
            harness.Admin.Id,
            UserRole.Admin,
            agingFilter: TicketAgingFilter.WaitingOver2Hours)).Items;
        var eightHours = (await harness.Service.GetAllAsync(
            harness.Admin.Id,
            UserRole.Admin,
            agingFilter: TicketAgingFilter.WaitingOver8Hours)).Items;
        var twentyFourHours = (await harness.Service.GetAllAsync(
            harness.Admin.Id,
            UserRole.Admin,
            agingFilter: TicketAgingFilter.WaitingOver24Hours)).Items;

        Assert.DoesNotContain(awaiting, item => item.Id is var id && (id == resolved.Id || id == closed.Id));
        Assert.Contains(twoHours, item => item.Id == harness.Ticket.Id);
        Assert.DoesNotContain(twoHours, item => item.Id == belowTwoHours.Id);
        Assert.Contains(eightHours, item => item.Id == atEightHours.Id);
        Assert.DoesNotContain(eightHours, item => item.Id == belowEightHours.Id);
        Assert.Contains(twentyFourHours, item => item.Id == atTwentyFourHours.Id);
        Assert.DoesNotContain(twentyFourHours, item => item.Id == belowTwentyFourHours.Id);
    }

    [Fact]
    public async Task GetAllAsync_WaitingSortsPlaceAwaitingTicketsFirstWithDeterministicTies()
    {
        var now = new DateTime(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await TicketServiceHarness.CreateAsync(new FixedTimeProvider(now));
        var oldestWaiting = CreateTicket(harness, "Oldest waiting", now.AddHours(-5));
        var sameAgeWaiting = CreateTicket(harness, "Same age waiting", now.AddHours(-5));
        var shortestWaiting = CreateTicket(harness, "Shortest waiting", now.AddHours(-2));
        var answered = CreateTicket(harness, "Answered", now.AddHours(-4));
        harness.DbContext.Tickets.AddRange(oldestWaiting, sameAgeWaiting, shortestWaiting, answered);
        await harness.DbContext.SaveChangesAsync();
        harness.DbContext.TicketMessages.Add(new TicketMessage
        {
            TicketId = answered.Id,
            SenderId = harness.TechnicalStaff.Id,
            Message = "Delivered",
            OutboundDeliveryStatus = OutboundDeliveryStatus.Sent,
            CreatedAt = now.AddHours(-3),
            DeliveredAt = now.AddHours(-3)
        });
        await harness.DbContext.SaveChangesAsync();

        var longest = (await harness.Service.GetAllAsync(
            harness.Admin.Id,
            UserRole.Admin,
            sort: TicketListSort.LongestWaiting)).Items;
        var shortest = (await harness.Service.GetAllAsync(
            harness.Admin.Id,
            UserRole.Admin,
            sort: TicketListSort.ShortestWaiting)).Items;

        var longestIds = longest.Select(item => item.Id).ToList();
        var shortestIds = shortest.Select(item => item.Id).ToList();
        Assert.True(longestIds.IndexOf(oldestWaiting.Id) < longestIds.IndexOf(shortestWaiting.Id));
        Assert.True(longestIds.IndexOf(sameAgeWaiting.Id) < longestIds.IndexOf(shortestWaiting.Id));
        Assert.True(longestIds.IndexOf(shortestWaiting.Id) < longestIds.IndexOf(answered.Id));
        Assert.True(shortestIds.IndexOf(shortestWaiting.Id) < shortestIds.IndexOf(oldestWaiting.Id));
        Assert.True(shortestIds.IndexOf(shortestWaiting.Id) < shortestIds.IndexOf(answered.Id));
        Assert.True(longestIds.IndexOf(oldestWaiting.Id) < longestIds.IndexOf(sameAgeWaiting.Id));
    }

    [Fact]
    public async Task GetAllAsync_ComposesSearchPriorityStatusAndAssignmentWithAging()
    {
        var now = new DateTime(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await TicketServiceHarness.CreateAsync(new FixedTimeProvider(now));
        var matching = CreateTicket(
            harness,
            "VPN erişim sorunu",
            now.AddHours(-3),
            TicketStatus.New,
            TicketPriority.High,
            harness.TechnicalStaff.Id);
        var otherPriority = CreateTicket(
            harness,
            "VPN düşük öncelik",
            now.AddHours(-3),
            TicketStatus.New,
            TicketPriority.Low,
            harness.TechnicalStaff.Id);
        var otherAssignee = CreateTicket(
            harness,
            "VPN farklı atama",
            now.AddHours(-3),
            TicketStatus.New,
            TicketPriority.High,
            harness.Admin.Id);
        var legacy = CreateTicket(harness, "Legacy waiting", now.AddHours(-10));
        harness.DbContext.Tickets.AddRange(matching, otherPriority, otherAssignee, legacy);
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetAllAsync(
            harness.Admin.Id,
            UserRole.Admin,
            priority: TicketPriority.High,
            status: TicketStatus.New,
            assignedUserId: harness.TechnicalStaff.Id,
            search: "VPN",
            agingFilter: TicketAgingFilter.WaitingOver2Hours);
        var legacyResult = await harness.Service.GetAllAsync(
            harness.Admin.Id,
            UserRole.Admin,
            agingFilter: TicketAgingFilter.WaitingOver8Hours);

        var item = Assert.Single(result.Items);
        Assert.Equal(matching.Id, item.Id);
        Assert.Contains(legacyResult.Items, item => item.Id == legacy.Id && item.Sla.MostUrgentState == SlaState.NotTracked);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByAssignedUserId()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();
        var assigned = CreateTicket(harness, "Assigned to technical staff", DateTime.UtcNow, assignedUserId: harness.TechnicalStaff.Id);
        var other = CreateTicket(harness, "Assigned to admin", DateTime.UtcNow, assignedUserId: harness.Admin.Id);
        harness.DbContext.Tickets.AddRange(assigned, other);
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetAllAsync(
            harness.Admin.Id,
            UserRole.Admin,
            assignedUserId: harness.TechnicalStaff.Id);

        Assert.Contains(result.Items, ticket => ticket.Id == assigned.Id);
        Assert.DoesNotContain(result.Items, ticket => ticket.Id == other.Id);
    }

    [Theory]
    [InlineData(TicketStatus.New)]
    [InlineData(TicketStatus.Assigned)]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.WaitingForCustomer)]
    [InlineData(TicketStatus.Reopened)]
    public async Task GetAllAsync_ActiveOnlyIncludesEveryActiveStatus(TicketStatus status)
    {
        await using var harness = await TicketServiceHarness.CreateAsync();
        var ticket = CreateTicket(harness, $"Active {status}", DateTime.UtcNow, status);
        harness.DbContext.Tickets.Add(ticket);
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetAllAsync(
            harness.Admin.Id,
            UserRole.Admin,
            activeOnly: true);

        Assert.Contains(result.Items, item => item.Id == ticket.Id);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public async Task GetAllAsync_ActiveOnlyExcludesTerminalStatuses(TicketStatus status)
    {
        await using var harness = await TicketServiceHarness.CreateAsync();
        var ticket = CreateTicket(harness, $"Terminal {status}", DateTime.UtcNow, status);
        harness.DbContext.Tickets.Add(ticket);
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetAllAsync(
            harness.Admin.Id,
            UserRole.Admin,
            activeOnly: true);

        Assert.DoesNotContain(result.Items, item => item.Id == ticket.Id);
    }

    [Fact]
    public async Task GetAllAsync_FiltersUnassignedTickets()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();
        var unassigned = CreateTicket(harness, "Unassigned", DateTime.UtcNow);
        var assigned = CreateTicket(harness, "Assigned", DateTime.UtcNow, assignedUserId: harness.TechnicalStaff.Id);
        harness.DbContext.Tickets.AddRange(unassigned, assigned);
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetAllAsync(
            harness.Admin.Id,
            UserRole.Admin,
            assignmentState: TicketAssignmentFilter.Unassigned);

        Assert.Contains(result.Items, item => item.Id == unassigned.Id);
        Assert.DoesNotContain(result.Items, item => item.Id == assigned.Id);
    }

    [Fact]
    public async Task GetAllAsync_FiltersUnassignedActiveTickets()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();
        var active = CreateTicket(harness, "Active unassigned", DateTime.UtcNow, TicketStatus.New);
        var resolved = CreateTicket(harness, "Resolved unassigned", DateTime.UtcNow, TicketStatus.Resolved);
        harness.DbContext.Tickets.AddRange(active, resolved);
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetAllAsync(
            harness.Admin.Id,
            UserRole.Admin,
            assignmentState: TicketAssignmentFilter.Unassigned,
            activeOnly: true);

        Assert.Contains(result.Items, item => item.Id == active.Id);
        Assert.DoesNotContain(result.Items, item => item.Id == resolved.Id);
    }

    [Fact]
    public async Task GetAllAsync_FiltersInactiveTechnicalStaffAssignees()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();
        var inactiveStaff = await AddTechnicalStaffAsync(harness, isActive: false);
        var inactiveTicket = CreateTicket(harness, "Inactive assignment", DateTime.UtcNow, assignedUserId: inactiveStaff.Id);
        var activeTicket = CreateTicket(harness, "Active assignment", DateTime.UtcNow, assignedUserId: harness.TechnicalStaff.Id);
        harness.DbContext.Tickets.AddRange(inactiveTicket, activeTicket);
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetAllAsync(
            harness.Admin.Id,
            UserRole.Admin,
            assignmentState: TicketAssignmentFilter.InactiveAssignee);

        Assert.Contains(result.Items, item => item.Id == inactiveTicket.Id);
        Assert.DoesNotContain(result.Items, item => item.Id == activeTicket.Id);
    }

    [Fact]
    public async Task GetAllAsync_FiltersInactiveTechnicalStaffAssigneesWithinActiveScope()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();
        var inactiveStaff = await AddTechnicalStaffAsync(harness, isActive: false);
        var active = CreateTicket(harness, "Active inactive assignment", DateTime.UtcNow, assignedUserId: inactiveStaff.Id);
        var closed = CreateTicket(harness, "Closed inactive assignment", DateTime.UtcNow, TicketStatus.Closed, assignedUserId: inactiveStaff.Id);
        harness.DbContext.Tickets.AddRange(active, closed);
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetAllAsync(
            harness.Admin.Id,
            UserRole.Admin,
            assignmentState: TicketAssignmentFilter.InactiveAssignee,
            activeOnly: true);

        Assert.Contains(result.Items, item => item.Id == active.Id);
        Assert.DoesNotContain(result.Items, item => item.Id == closed.Id);
    }

    [Fact]
    public async Task GetAllAsync_DoesNotClassifyInactiveAssigneeAsUnassigned()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();
        var inactiveStaff = await AddTechnicalStaffAsync(harness, isActive: false);
        var ticket = CreateTicket(harness, "Inactive assignment", DateTime.UtcNow, assignedUserId: inactiveStaff.Id);
        harness.DbContext.Tickets.Add(ticket);
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetAllAsync(
            harness.Admin.Id,
            UserRole.Admin,
            assignmentState: TicketAssignmentFilter.Unassigned);

        Assert.DoesNotContain(result.Items, item => item.Id == ticket.Id);
    }

    [Fact]
    public async Task GetAllAsync_DoesNotClassifyActiveTechnicalStaffAsInactiveAssignee()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();
        var ticket = CreateTicket(harness, "Active technical assignment", DateTime.UtcNow, assignedUserId: harness.TechnicalStaff.Id);
        harness.DbContext.Tickets.Add(ticket);
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetAllAsync(
            harness.Admin.Id,
            UserRole.Admin,
            assignmentState: TicketAssignmentFilter.InactiveAssignee);

        Assert.DoesNotContain(result.Items, item => item.Id == ticket.Id);
    }

    [Fact]
    public async Task GetAllAsync_ComposesSearchWithAssignedUserFilter()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();
        var matching = CreateTicket(harness, "VPN matching", DateTime.UtcNow, assignedUserId: harness.TechnicalStaff.Id);
        var otherAssignee = CreateTicket(harness, "VPN other", DateTime.UtcNow, assignedUserId: harness.Admin.Id);
        harness.DbContext.Tickets.AddRange(matching, otherAssignee);
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetAllAsync(harness.Admin.Id, UserRole.Admin,
            assignedUserId: harness.TechnicalStaff.Id, search: "VPN");

        var item = Assert.Single(result.Items, item => item.Title.Contains("VPN"));
        Assert.Equal(matching.Id, item.Id);
    }

    [Fact]
    public async Task GetAllAsync_ComposesPriorityWithAssignedUserFilter()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();
        var matching = CreateTicket(harness, "High priority", DateTime.UtcNow, priority: TicketPriority.High, assignedUserId: harness.TechnicalStaff.Id);
        var differentPriority = CreateTicket(harness, "Normal priority", DateTime.UtcNow, assignedUserId: harness.TechnicalStaff.Id);
        harness.DbContext.Tickets.AddRange(matching, differentPriority);
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetAllAsync(harness.Admin.Id, UserRole.Admin,
            priority: TicketPriority.High, assignedUserId: harness.TechnicalStaff.Id);

        Assert.Contains(result.Items, item => item.Id == matching.Id);
        Assert.DoesNotContain(result.Items, item => item.Id == differentPriority.Id);
    }

    [Fact]
    public async Task GetAllAsync_ComposesAgingWithAssignedUserFilter()
    {
        var now = new DateTime(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await TicketServiceHarness.CreateAsync(new FixedTimeProvider(now));
        var matching = CreateTicket(harness, "Long wait", now.AddHours(-3), assignedUserId: harness.TechnicalStaff.Id);
        var shortWait = CreateTicket(harness, "Short wait", now.AddMinutes(-30), assignedUserId: harness.TechnicalStaff.Id);
        harness.DbContext.Tickets.AddRange(matching, shortWait);
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetAllAsync(harness.Admin.Id, UserRole.Admin,
            assignedUserId: harness.TechnicalStaff.Id,
            agingFilter: TicketAgingFilter.WaitingOver2Hours);

        Assert.Contains(result.Items, item => item.Id == matching.Id);
        Assert.DoesNotContain(result.Items, item => item.Id == shortWait.Id);
    }

    [Fact]
    public async Task GetAllAsync_ComposesStatusWithUnassignedFilter()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();
        var matching = CreateTicket(harness, "New unassigned", DateTime.UtcNow, TicketStatus.New);
        var otherStatus = CreateTicket(harness, "Assigned status", DateTime.UtcNow, TicketStatus.Assigned);
        harness.DbContext.Tickets.AddRange(matching, otherStatus);
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetAllAsync(harness.Admin.Id, UserRole.Admin,
            status: TicketStatus.New,
            assignmentState: TicketAssignmentFilter.Unassigned);

        Assert.Contains(result.Items, item => item.Id == matching.Id);
        Assert.DoesNotContain(result.Items, item => item.Id == otherStatus.Id);
    }

    [Fact]
    public async Task GetAllAsync_SortsOldestAndNewestTicketsDeterministically()
    {
        var now = new DateTime(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await TicketServiceHarness.CreateAsync(new FixedTimeProvider(now));
        var oldest = CreateTicket(harness, "Oldest", now.AddHours(-4));
        var newest = CreateTicket(harness, "Newest", now.AddHours(-1));
        harness.DbContext.Tickets.AddRange(oldest, newest);
        await harness.DbContext.SaveChangesAsync();

        var ascending = await harness.Service.GetAllAsync(
            harness.Admin.Id,
            UserRole.Admin,
            sort: TicketListSort.OldestTicket);
        var descending = await harness.Service.GetAllAsync(
            harness.Admin.Id,
            UserRole.Admin,
            sort: TicketListSort.NewestTicket);

        Assert.True(ascending.Items.Select(item => item.Id).ToList().IndexOf(oldest.Id)
            < ascending.Items.Select(item => item.Id).ToList().IndexOf(newest.Id));
        Assert.True(descending.Items.Select(item => item.Id).ToList().IndexOf(newest.Id)
            < descending.Items.Select(item => item.Id).ToList().IndexOf(oldest.Id));
    }

    [Fact]
    public async Task GetAllAsync_ReturnsStableFastPathPagesAndMetadata()
    {
        var now = new DateTime(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await TicketServiceHarness.CreateAsync(new FixedTimeProvider(now));
        var tickets = Enumerable.Range(1, 30)
            .Select(index => CreateTicket(harness, $"Page {index}", now.AddMinutes(-index)))
            .ToArray();
        harness.DbContext.Tickets.AddRange(tickets);
        await harness.DbContext.SaveChangesAsync();

        var first = await harness.Service.GetAllAsync(harness.Admin.Id, UserRole.Admin, page: 1, pageSize: 25);
        var second = await harness.Service.GetAllAsync(harness.Admin.Id, UserRole.Admin, page: 2, pageSize: 25);

        Assert.Equal(31, first.Pagination.TotalCount);
        Assert.Equal(2, first.Pagination.TotalPages);
        Assert.False(first.Pagination.HasPreviousPage);
        Assert.True(first.Pagination.HasNextPage);
        Assert.Equal(25, first.Items.Count);
        Assert.True(second.Pagination.HasPreviousPage);
        Assert.False(second.Pagination.HasNextPage);
        Assert.Equal(6, second.Items.Count);
        Assert.Empty(first.Items.Select(item => item.Id).Intersect(second.Items.Select(item => item.Id)));
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyOutOfRangePageWithMetadata()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();

        var result = await harness.Service.GetAllAsync(
            harness.Admin.Id,
            UserRole.Admin,
            page: 3,
            pageSize: 25);

        Assert.Empty(result.Items);
        Assert.Equal(3, result.Pagination.Page);
        Assert.Equal(1, result.Pagination.TotalCount);
        Assert.Equal(1, result.Pagination.TotalPages);
        Assert.True(result.Pagination.HasPreviousPage);
        Assert.False(result.Pagination.HasNextPage);
    }

    [Fact]
    public async Task GetAllAsync_AgingFilterCountsBeforePagination()
    {
        var now = new DateTime(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await TicketServiceHarness.CreateAsync(new FixedTimeProvider(now));
        var twoHours = CreateTicket(harness, "Two", now.AddMinutes(-120));
        var eightHours = CreateTicket(harness, "Eight", now.AddMinutes(-480));
        var day = CreateTicket(harness, "Day", now.AddMinutes(-1440));
        harness.DbContext.Tickets.AddRange(twoHours, eightHours, day);
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetAllAsync(
            harness.Admin.Id,
            UserRole.Admin,
            agingFilter: TicketAgingFilter.WaitingOver2Hours,
            page: 2,
            pageSize: 2);

        Assert.Equal(3, result.Pagination.TotalCount);
        Assert.Equal(2, result.Pagination.TotalPages);
        Assert.Single(result.Items);
        Assert.All(result.Items, item => Assert.True(item.AwaitingStaffMinutes >= 120));
    }

    [Fact]
    public async Task GetAllAsync_LongestWaitingOrdersGloballyBeforePagination()
    {
        var now = new DateTime(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await TicketServiceHarness.CreateAsync(new FixedTimeProvider(now));
        var oldest = CreateTicket(harness, "Oldest", now.AddHours(-8));
        var middle = CreateTicket(harness, "Middle", now.AddHours(-4));
        var newest = CreateTicket(harness, "Newest", now.AddHours(-2));
        harness.DbContext.Tickets.AddRange(oldest, middle, newest);
        await harness.DbContext.SaveChangesAsync();

        var first = await harness.Service.GetAllAsync(
            harness.Admin.Id,
            UserRole.Admin,
            sort: TicketListSort.LongestWaiting,
            page: 1,
            pageSize: 2);
        var second = await harness.Service.GetAllAsync(
            harness.Admin.Id,
            UserRole.Admin,
            sort: TicketListSort.LongestWaiting,
            page: 2,
            pageSize: 2);

        Assert.Equal([oldest.Id, middle.Id], first.Items.Where(item => item.Id != harness.Ticket.Id).Select(item => item.Id).ToArray());
        Assert.Contains(second.Items, item => item.Id == newest.Id);
        Assert.Empty(first.Items.Select(item => item.Id).Intersect(second.Items.Select(item => item.Id)));
    }

    [Fact]
    public async Task GetAllAsync_SqlAgingPath_PreservesDeliveryRequesterAndReopenSemantics()
    {
        var now = new DateTime(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await TicketServiceHarness.CreateAsync(new FixedTimeProvider(now));
        var failed = CreateTicket(harness, "Failed delivery", now.AddHours(-10));
        var sent = CreateTicket(harness, "Sent delivery", now.AddHours(-10));
        var legacy = CreateTicket(harness, "Legacy delivery", now.AddHours(-10));
        var reopened = CreateTicket(harness, "Reopened", now.AddHours(-10), TicketStatus.Reopened);
        var externalRequester = new ExternalRequester
        {
            Email = "external@example.test",
            NormalizedEmail = "external@example.test",
            DisplayName = "External requester",
            CreatedAt = now.AddHours(-10),
            UpdatedAt = now.AddHours(-10)
        };
        harness.DbContext.ExternalRequesters.Add(externalRequester);
        await harness.DbContext.SaveChangesAsync();
        var external = new Ticket
        {
            TicketNumber = $"SUP-{Guid.NewGuid():N}"[..30],
            ExternalRequesterId = externalRequester.Id,
            Title = "External requester",
            Description = "Test description",
            Status = TicketStatus.New,
            Priority = TicketPriority.Normal,
            CreatedAt = now.AddHours(-10),
            UpdatedAt = now.AddHours(-10)
        };
        harness.DbContext.Tickets.AddRange(failed, sent, legacy, reopened, external);
        await harness.DbContext.SaveChangesAsync();
        harness.DbContext.TicketMessages.AddRange(
            new TicketMessage
            {
                TicketId = failed.Id,
                SenderId = harness.TechnicalStaff.Id,
                Message = "Failed",
                OutboundDeliveryStatus = OutboundDeliveryStatus.Failed,
                CreatedAt = now.AddHours(-9)
            },
            new TicketMessage
            {
                TicketId = sent.Id,
                SenderId = harness.TechnicalStaff.Id,
                Message = "Sent",
                OutboundDeliveryStatus = OutboundDeliveryStatus.Sent,
                DeliveredAt = now.AddHours(-9),
                CreatedAt = now.AddHours(-9)
            },
            new TicketMessage
            {
                TicketId = legacy.Id,
                SenderId = harness.TechnicalStaff.Id,
                Message = "Legacy",
                OutboundDeliveryStatus = OutboundDeliveryStatus.NotApplicable,
                CreatedAt = now.AddHours(-9)
            },
            new TicketMessage
            {
                TicketId = reopened.Id,
                SenderId = harness.TechnicalStaff.Id,
                Message = "Prior staff response",
                OutboundDeliveryStatus = OutboundDeliveryStatus.Sent,
                DeliveredAt = now.AddHours(-9),
                CreatedAt = now.AddHours(-9)
            },
            new TicketMessage
            {
                TicketId = reopened.Id,
                SenderId = harness.Customer.Id,
                Message = "Reopen request",
                CreatedAt = now.AddHours(-8)
            },
            new TicketMessage
            {
                TicketId = external.Id,
                ExternalRequesterId = externalRequester.Id,
                Message = "External request",
                CreatedAt = now.AddHours(-8)
            });
        await harness.DbContext.SaveChangesAsync();

        var awaiting = await harness.Service.GetAllAsync(
            harness.Admin.Id,
            UserRole.Admin,
            agingFilter: TicketAgingFilter.Awaiting,
            sort: TicketListSort.LongestWaiting);

        Assert.Contains(awaiting.Items, item => item.Id == failed.Id && item.IsAwaitingStaffReply);
        Assert.Contains(awaiting.Items, item => item.Id == reopened.Id
            && item.AwaitingStaffSince == now.AddHours(-8));
        Assert.Contains(awaiting.Items, item => item.Id == external.Id
            && item.RequesterType == "ExternalRequester");
        Assert.DoesNotContain(awaiting.Items, item => item.Id == sent.Id);
        Assert.DoesNotContain(awaiting.Items, item => item.Id == legacy.Id);
    }

    [Fact]
    public async Task GetAllAsync_SqlAgingPath_DoesNotIssueSeparateCandidateMessageLoad()
    {
        var now = new DateTime(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await TicketServiceHarness.CreateAsync(new FixedTimeProvider(now));
        harness.Ticket.CreatedAt = now.AddHours(-9);
        harness.DbContext.Tickets.AddRange(
            CreateTicket(harness, "Eight hour wait", now.AddHours(-8)),
            CreateTicket(harness, "Short wait", now.AddHours(-1)));
        await harness.DbContext.SaveChangesAsync();
        harness.CommandInterceptor.Clear();

        var result = await harness.Service.GetAllAsync(
            harness.Admin.Id,
            UserRole.Admin,
            agingFilter: TicketAgingFilter.WaitingOver8Hours,
            page: 1,
            pageSize: 2);

        Assert.Equal(2, result.Pagination.TotalCount);
        Assert.Contains(harness.CommandInterceptor.Commands,
            command => command.TrimStart().StartsWith("SELECT COUNT(*)", StringComparison.Ordinal)
                && command.Contains("TicketMessages", StringComparison.Ordinal));
        Assert.Contains(harness.CommandInterceptor.Commands,
            command => command.Contains("TicketMessages", StringComparison.Ordinal));
        Assert.DoesNotContain(harness.CommandInterceptor.Commands,
            command => Regex.IsMatch(
                command,
                "FROM \\\"TicketMessages\\\" AS \\\"[^\\\"]+\\\"\\s+WHERE \\\"[^\\\"]+\\\"\\.\\\"TicketId\\\" IN",
                RegexOptions.Singleline));
    }

    [Fact]
    public async Task GetAllAsync_UsesInMemoryAgingOnlyWhenExplicitlyDisabledByConfiguration()
    {
        var now = new DateTime(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await TicketServiceHarness.CreateAsync(
            new FixedTimeProvider(now),
            sqlProjectionEnabled: false);
        harness.Ticket.CreatedAt = now.AddHours(-3);
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetAllAsync(
            harness.Admin.Id,
            UserRole.Admin,
            agingFilter: TicketAgingFilter.WaitingOver2Hours);

        var ticket = Assert.Single(result.Items);
        Assert.Equal(harness.Ticket.Id, ticket.Id);
        Assert.True(ticket.IsAwaitingStaffReply);
    }

    [Fact]
    public async Task GetAllAsync_PreservesCustomerOwnershipWithPagination()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();
        var otherCustomer = new User
        {
            FirstName = "Other",
            LastName = "Customer",
            Email = "other@example.test",
            PasswordHash = "hash",
            Role = UserRole.Customer,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        harness.DbContext.Users.Add(otherCustomer);
        await harness.DbContext.SaveChangesAsync();
        harness.DbContext.Tickets.Add(new Ticket
        {
            TicketNumber = "SUP-2026-OTHER",
            CustomerId = otherCustomer.Id,
            Title = "Other customer ticket",
            Description = "desc",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetAllAsync(
            harness.Customer.Id,
            UserRole.Customer,
            page: 1,
            pageSize: 25);

        Assert.All(result.Items, item => Assert.Equal(harness.Customer.Id, item.CustomerId));
        Assert.Equal(1, result.Pagination.TotalCount);
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_UsesCompleteAuthorizedTicketScope()
    {
        await using var harness = await TicketServiceHarness.CreateAsync();
        var tickets = Enumerable.Range(1, 30)
            .Select(index => CreateTicket(harness, $"Summary {index}", DateTime.UtcNow.AddMinutes(-index)))
            .ToArray();
        harness.DbContext.Tickets.AddRange(tickets);
        await harness.DbContext.SaveChangesAsync();

        var page = await harness.Service.GetAllAsync(
            harness.Admin.Id,
            UserRole.Admin,
            page: 1,
            pageSize: 25);
        var summary = await harness.Service.GetDashboardSummaryAsync(
            harness.Admin.Id,
            UserRole.Admin);

        Assert.Equal(25, page.Items.Count);
        Assert.Equal(31, summary.TotalCount);
        Assert.Equal(6, summary.RecentTickets.Count);
    }

    private static async Task AddTicketAsync(
        TicketServiceHarness harness,
        TicketPriority priority,
        TicketStatus status,
        string title)
    {
        harness.DbContext.Tickets.Add(new Ticket
        {
            TicketNumber = $"SUP-2026-{Guid.NewGuid():N}"[..30],
            CustomerId = harness.Customer.Id,
            Title = title,
            Description = "Test description",
            Status = status,
            Priority = priority,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await harness.DbContext.SaveChangesAsync();
    }

    private static Ticket CreateTicket(
        TicketServiceHarness harness,
        string title,
        DateTime createdAt,
        TicketStatus status = TicketStatus.New,
        TicketPriority priority = TicketPriority.Normal,
        int? assignedUserId = null) =>
        new()
        {
            TicketNumber = $"SUP-2026-{Guid.NewGuid():N}"[..30],
            CustomerId = harness.Customer.Id,
            AssignedUserId = assignedUserId,
            Title = title,
            Description = "Test description",
            Status = status,
            Priority = priority,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };

    private static async Task<User> AddTechnicalStaffAsync(
        TicketServiceHarness harness,
        bool isActive)
    {
        var staff = new User
        {
            FirstName = "Inactive",
            LastName = "Technician",
            Email = $"technical-{Guid.NewGuid():N}@example.test",
            PasswordHash = "hash",
            Role = UserRole.TechnicalStaff,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };
        harness.DbContext.Users.Add(staff);
        await harness.DbContext.SaveChangesAsync();
        return staff;
    }

    internal sealed class RecordingEmailService : IEmailService
    {
        public List<SentEmail> Messages { get; } = [];

        public Task SendAsync(
            string toEmail,
            string subject,
            string body,
            CancellationToken cancellationToken = default)
        {
            Messages.Add(new SentEmail(toEmail, subject, body));
            return Task.CompletedTask;
        }
    }

    internal sealed record SentEmail(string ToEmail, string Subject, string Body);

    internal sealed class RecordingGmailSendService : IGmailSendService
    {
        public List<SentEmail> Messages { get; } = [];
        public Exception? ThrowOnSend { get; set; }

        public Task SendPlainTextAsync(
            string toEmail,
            string subject,
            string body,
            CancellationToken cancellationToken = default)
        {
            Messages.Add(new SentEmail(toEmail, subject, body));

            if (ThrowOnSend is not null)
            {
                throw ThrowOnSend;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingTicketActivityWriter : ITicketActivityWriter
    {
        public TicketActivity Append(TicketActivityAppendRequest request) =>
            throw new InvalidOperationException("Simulated activity persistence failure.");
    }

    internal sealed class TicketServiceHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private TicketServiceHarness(
            SqliteConnection connection,
            AppDbContext dbContext,
            User customer,
            User technicalStaff,
            User admin,
            Ticket ticket,
            RecordingEmailService emailService,
            RecordingGmailSendService gmailSendService,
            TicketService service,
            RecordingCommandInterceptor commandInterceptor)
        {
            _connection = connection;
            DbContext = dbContext;
            Customer = customer;
            TechnicalStaff = technicalStaff;
            Admin = admin;
            Ticket = ticket;
            EmailService = emailService;
            GmailSendService = gmailSendService;
            Service = service;
            CommandInterceptor = commandInterceptor;
        }

        public AppDbContext DbContext { get; }
        public User Customer { get; }
        public User TechnicalStaff { get; }
        public User Admin { get; }
        public Ticket Ticket { get; }
        public RecordingEmailService EmailService { get; }
        public RecordingGmailSendService GmailSendService { get; }
        public TicketService Service { get; }
        public RecordingCommandInterceptor CommandInterceptor { get; }

        public static async Task<TicketServiceHarness> CreateAsync(
            TimeProvider? timeProvider = null,
            bool sqlProjectionEnabled = true,
            ITicketActivityWriter? ticketActivityWriter = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var commandInterceptor = new RecordingCommandInterceptor();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(commandInterceptor)
                .Options;
            var dbContext = new AppDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();

            var customer = new User
            {
                FirstName = "Test",
                LastName = "Customer",
                Email = "customer@example.test",
                PasswordHash = "hash",
                Role = UserRole.Customer,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
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
            dbContext.Users.AddRange(customer, technicalStaff, admin);
            await dbContext.SaveChangesAsync();

            var ticket = new Ticket
            {
                TicketNumber = "SUP-2026-001009",
                CustomerId = customer.Id,
                Title = "VPN bağlantı problemi",
                Description = "desc",
                Status = TicketStatus.InProgress,
                Priority = TicketPriority.Normal,
                CreatedAt = DateTime.UtcNow.AddHours(-1),
                UpdatedAt = DateTime.UtcNow.AddHours(-1)
            };
            dbContext.Tickets.Add(ticket);
            await dbContext.SaveChangesAsync();

            var emailService = new RecordingEmailService();
            var gmailSendService = new RecordingGmailSendService();
            var slaTargetResolver = new SlaTargetResolver(Options.Create(new SlaSettings()));
            var ticketSlaService = new TicketSlaService(slaTargetResolver);
            var service = new TicketService(
                dbContext,
                new NotificationService(dbContext),
                emailService,
                new TicketEmailComposer(Options.Create(new ApplicationSettings())),
                gmailSendService,
                ticketSlaService,
                new TicketSlaCalculator(ticketSlaService, slaTargetResolver, timeProvider ?? TimeProvider.System),
                new QueueAgingCalculator(timeProvider ?? TimeProvider.System),
                ticketActivityWriter ?? new TicketActivityWriter(
                    dbContext,
                    new TicketActivityMetadataCodec(),
                    timeProvider ?? TimeProvider.System),
                NullLogger<TicketService>.Instance,
                timeProvider ?? TimeProvider.System,
                Options.Create(new QueueAgingSettings
                {
                    SqlProjectionEnabled = sqlProjectionEnabled
                }));

            return new TicketServiceHarness(
                connection,
                dbContext,
                customer,
                technicalStaff,
                admin,
                ticket,
                emailService,
                gmailSendService,
                service,
                commandInterceptor);
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    internal sealed class RecordingCommandInterceptor : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];

        public void Clear() => Commands.Clear();

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
