using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SupportFlow.Api.Controllers;
using SupportFlow.Api.Data;
using SupportFlow.Api.DTOs.Conversations;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;
using SupportFlow.Api.Services;
using SupportFlow.Api.Services.QueueAging;
using SupportFlow.Api.Services.Sla;
using SupportFlow.Api.Settings;

namespace SupportFlow.Api.Tests.Conversations;

public sealed class ConversationServiceTests
{
    [Fact]
    public async Task Summaries_GroupRegisteredAndExternalRequestersSeparately()
    {
        await using var harness = await ConversationHarness.CreateAsync();

        var summaries = await harness.Service.GetSummariesAsync(harness.Staff.Id, null);

        var registered = Assert.Single(summaries, item => item.RequesterType == ConversationRequesterType.Registered);
        var external = Assert.Single(summaries, item => item.RequesterType == ConversationRequesterType.External);
        Assert.Equal(2, registered.TicketCount);
        Assert.Equal(1, external.TicketCount);
        Assert.Equal(registered.Email, external.Email);
        Assert.NotEqual(registered.RequesterType, external.RequesterType);
    }

    [Fact]
    public async Task Summaries_CountBreachedAndDueSoonTrackedTickets_WithoutIncludingLegacyTickets()
    {
        var now = new DateTime(2026, 8, 24, 9, 0, 0, DateTimeKind.Utc);
        await using var harness = await ConversationHarness.CreateAsync(new FixedTimeProvider(now));
        harness.RegisteredDuplicateTicket.FirstResponseDueAt = now.AddMinutes(-1);
        harness.RegisteredDuplicateTicket.ResolutionCycleStartedAt = now.AddHours(-1);
        harness.RegisteredDuplicateTicket.ResolutionDueAt = now.AddHours(10);
        harness.ExternalTicket.FirstResponseDueAt = now.AddMinutes(6);
        harness.ExternalTicket.ResolutionCycleStartedAt = now.AddHours(-1);
        harness.ExternalTicket.ResolutionDueAt = now.AddHours(10);
        await harness.DbContext.SaveChangesAsync();

        var summaries = await harness.Service.GetSummariesAsync(harness.Staff.Id, null);

        var registered = summaries.Single(item => item.RequesterType == ConversationRequesterType.Registered);
        var external = summaries.Single(item => item.RequesterType == ConversationRequesterType.External);
        Assert.Equal(1, registered.SlaBreachedTicketCount);
        Assert.Equal(0, registered.SlaDueSoonTicketCount);
        Assert.Equal(0, external.SlaBreachedTicketCount);
        Assert.Equal(1, external.SlaDueSoonTicketCount);
    }

    [Fact]
    public async Task Detail_ExposesCompactSlaSummaryForEachTicket()
    {
        var now = new DateTime(2026, 8, 24, 9, 0, 0, DateTimeKind.Utc);
        await using var harness = await ConversationHarness.CreateAsync(new FixedTimeProvider(now));
        harness.ExternalTicket.FirstResponseDueAt = now.AddMinutes(6);
        harness.ExternalTicket.ResolutionCycleStartedAt = now.AddHours(-1);
        harness.ExternalTicket.ResolutionDueAt = now.AddHours(10);
        await harness.DbContext.SaveChangesAsync();

        var detail = await harness.Service.GetDetailAsync(
            ConversationRequesterType.External,
            harness.ExternalRequester.Id,
            harness.Staff.Id);

        var ticket = Assert.Single(detail!.Tickets);
        Assert.Equal(SlaState.DueSoon, ticket.Sla.FirstResponseState);
        Assert.Equal(SlaState.OnTrack, ticket.Sla.ResolutionState);
        Assert.Equal(SlaState.DueSoon, ticket.Sla.MostUrgentState);
    }

    [Fact]
    public async Task Summaries_SearchesByRequesterNameEmailTicketNumberAndTitle()
    {
        await using var harness = await ConversationHarness.CreateAsync();

        Assert.Single(await harness.Service.GetSummariesAsync(harness.Staff.Id, "Kayıtlı İstek Sahibi"));
        Assert.Equal(2, (await harness.Service.GetSummariesAsync(harness.Staff.Id, "shared@example.test")).Count);
        Assert.Single(await harness.Service.GetSummariesAsync(harness.Staff.Id, "SUP-2026-1002"));
        Assert.Single(await harness.Service.GetSummariesAsync(harness.Staff.Id, "Dış sistem"));
    }

    [Fact]
    public async Task Detail_ReturnsAllPublicTicketCommunicationWithoutInternalNotes()
    {
        await using var harness = await ConversationHarness.CreateAsync();

        var detail = await harness.Service.GetDetailAsync(
            ConversationRequesterType.Registered,
            harness.Customer.Id,
            harness.Staff.Id);

        Assert.NotNull(detail);
        Assert.Equal(2, detail.TicketCount);
        Assert.Equal(2, detail.OpenTicketCount);
        Assert.DoesNotContain(detail.Tickets.SelectMany(ticket => ticket.Messages), message => message.Message == "Internal only");
        Assert.Contains(detail.Tickets.SelectMany(ticket => ticket.Messages), message => message.Message == "Support response");
        Assert.Contains(detail.Tickets.SelectMany(ticket => ticket.Messages), message => message.Message == "Customer reply");
        var failedReply = Assert.Single(
            detail.Tickets.SelectMany(ticket => ticket.Messages),
            message => message.Message == "Support response");
        Assert.Equal(OutboundDeliveryStatus.Failed, failedReply.DeliveryStatus);
        Assert.Equal("Delivery failed.", failedReply.SafeDeliveryError);
    }

    [Fact]
    public async Task Detail_DoesNotDuplicateInitialRequestWhenMatchingPublicMessageExists()
    {
        await using var harness = await ConversationHarness.CreateAsync();

        var detail = await harness.Service.GetDetailAsync(
            ConversationRequesterType.Registered,
            harness.Customer.Id,
            harness.Staff.Id);

        var ticket = Assert.Single(detail!.Tickets, item => item.Id == harness.RegisteredDuplicateTicket.Id);
        Assert.Null(ticket.InitialMessage);
        Assert.Single(ticket.Messages, message => message.Message == "Same initial request");
    }

    [Fact]
    public async Task Detail_ProjectsExternalRequesterMessagesAndAttachmentMetadata()
    {
        await using var harness = await ConversationHarness.CreateAsync();

        var detail = await harness.Service.GetDetailAsync(
            ConversationRequesterType.External,
            harness.ExternalRequester.Id,
            harness.Staff.Id);

        Assert.NotNull(detail);
        Assert.Equal("External requester", detail.DisplayName);
        var ticket = Assert.Single(detail.Tickets, item => item.Id == harness.ExternalTicket.Id);
        var message = Assert.Single(ticket.Messages);
        Assert.Equal("Harici Müşteri", message.SenderLabel);
        Assert.False(message.IsSupportReply);
        var attachment = Assert.Single(ticket.Attachments);
        Assert.Equal("ekran.png", attachment.FileName);
    }

    [Fact]
    public async Task Detail_UnknownRequester_ReturnsNull()
    {
        await using var harness = await ConversationHarness.CreateAsync();

        var detail = await harness.Service.GetDetailAsync(
            ConversationRequesterType.External,
            999_999,
            harness.Staff.Id);

        Assert.Null(detail);
    }

    [Fact]
    public void Controller_AllowsOnlyAdminAndTechnicalStaff()
    {
        var attribute = Assert.Single(typeof(ConversationsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>());

        Assert.Equal("Admin,TechnicalStaff", attribute.Roles);
    }

    [Fact]
    public async Task PinAsync_PinsRegisteredAndExternalRequesters()
    {
        await using var harness = await ConversationHarness.CreateAsync();

        var registered = await harness.Service.PinAsync(
            harness.AdminA.Id,
            UserRole.Admin,
            ConversationRequesterType.Registered,
            harness.Customer.Id);
        var external = await harness.Service.PinAsync(
            harness.AdminA.Id,
            UserRole.Admin,
            ConversationRequesterType.External,
            harness.ExternalRequester.Id);

        Assert.Equal(ConversationPinOperationFailure.None, registered.Failure);
        Assert.Equal(ConversationPinOperationFailure.None, external.Failure);
        Assert.Equal(2, await harness.DbContext.ConversationPins.CountAsync());
    }

    [Fact]
    public async Task PinAsync_IsIdempotentAndUnpinIsSafe()
    {
        await using var harness = await ConversationHarness.CreateAsync();

        await harness.Service.PinAsync(
            harness.AdminA.Id,
            UserRole.Admin,
            ConversationRequesterType.Registered,
            harness.Customer.Id);
        var repeated = await harness.Service.PinAsync(
            harness.AdminA.Id,
            UserRole.Admin,
            ConversationRequesterType.Registered,
            harness.Customer.Id);
        var firstUnpin = await harness.Service.UnpinAsync(
            harness.AdminA.Id,
            UserRole.Admin,
            ConversationRequesterType.Registered,
            harness.Customer.Id);
        var repeatedUnpin = await harness.Service.UnpinAsync(
            harness.AdminA.Id,
            UserRole.Admin,
            ConversationRequesterType.Registered,
            harness.Customer.Id);

        Assert.Equal(ConversationPinOperationFailure.None, repeated.Failure);
        Assert.Equal(ConversationPinOperationFailure.None, firstUnpin.Failure);
        Assert.Equal(ConversationPinOperationFailure.None, repeatedUnpin.Failure);
        Assert.Empty(await harness.DbContext.ConversationPins.ToListAsync());
    }

    [Fact]
    public async Task PinAsync_CustomerIsForbidden()
    {
        await using var harness = await ConversationHarness.CreateAsync();

        var result = await harness.Service.PinAsync(
            harness.Customer.Id,
            UserRole.Customer,
            ConversationRequesterType.Registered,
            harness.Customer.Id);

        Assert.Equal(ConversationPinOperationFailure.Forbidden, result.Failure);
        Assert.Empty(await harness.DbContext.ConversationPins.ToListAsync());
    }

    [Fact]
    public async Task Pins_AreIsolatedBetweenStaffUsers()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        await harness.Service.PinAsync(
            harness.AdminA.Id,
            UserRole.Admin,
            ConversationRequesterType.Registered,
            harness.Customer.Id);
        await harness.Service.PinAsync(
            harness.Staff.Id,
            UserRole.TechnicalStaff,
            ConversationRequesterType.External,
            harness.ExternalRequester.Id);

        var adminA = await harness.Service.GetSummariesAsync(harness.AdminA.Id, null);
        var adminB = await harness.Service.GetSummariesAsync(harness.AdminB.Id, null);
        var staff = await harness.Service.GetSummariesAsync(harness.Staff.Id, null);

        Assert.True(adminA.Single(item => item.RequesterType == ConversationRequesterType.Registered).IsPinned);
        Assert.False(adminA.Single(item => item.RequesterType == ConversationRequesterType.External).IsPinned);
        Assert.All(adminB, item => Assert.False(item.IsPinned));
        Assert.True(staff.Single(item => item.RequesterType == ConversationRequesterType.External).IsPinned);
        Assert.False(staff.Single(item => item.RequesterType == ConversationRequesterType.Registered).IsPinned);
    }

    [Fact]
    public async Task Pins_KeepRegisteredAndExternalRequesterIdsSeparate()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        Assert.Equal(harness.Customer.Id, harness.ExternalRequester.Id);

        await harness.Service.PinAsync(
            harness.AdminA.Id,
            UserRole.Admin,
            ConversationRequesterType.Registered,
            harness.Customer.Id);

        var summaries = await harness.Service.GetSummariesAsync(harness.AdminA.Id, null);
        Assert.True(summaries.Single(item => item.RequesterType == ConversationRequesterType.Registered).IsPinned);
        Assert.False(summaries.Single(item => item.RequesterType == ConversationRequesterType.External).IsPinned);
    }

    [Fact]
    public async Task Summaries_SortPinnedFirstAndPreserveActivityOrdering()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        var beforePin = await harness.Service.GetSummariesAsync(harness.AdminA.Id, null);
        var registeredBefore = beforePin.Single(item => item.RequesterType == ConversationRequesterType.Registered);
        var externalBefore = beforePin.Single(item => item.RequesterType == ConversationRequesterType.External);
        Assert.True(externalBefore.LastActivityAt > registeredBefore.LastActivityAt);

        await harness.Service.PinAsync(
            harness.AdminA.Id,
            UserRole.Admin,
            ConversationRequesterType.Registered,
            harness.Customer.Id);
        await harness.Service.PinAsync(
            harness.AdminA.Id,
            UserRole.Admin,
            ConversationRequesterType.External,
            harness.ExternalRequester.Id);

        var afterPin = await harness.Service.GetSummariesAsync(harness.AdminA.Id, null);
        Assert.Equal(ConversationRequesterType.External, afterPin[0].RequesterType);
        Assert.Equal(externalBefore.LastActivityAt, afterPin[0].LastActivityAt);
        Assert.Equal(registeredBefore.LastActivityAt, afterPin[1].LastActivityAt);
    }

    [Fact]
    public async Task Summaries_SearchAndPinnedOnlyFiltersWorkTogether()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        await harness.Service.PinAsync(
            harness.AdminA.Id,
            UserRole.Admin,
            ConversationRequesterType.Registered,
            harness.Customer.Id);

        var searchResults = await harness.Service.GetSummariesAsync(
            harness.AdminA.Id,
            "shared@example.test");
        var pinnedSearchResults = await harness.Service.GetSummariesAsync(
            harness.AdminA.Id,
            "Kayıtlı İstek Sahibi",
            true);
        var emptyPinnedSearch = await harness.Service.GetSummariesAsync(
            harness.AdminA.Id,
            "Dış sistem",
            true);

        Assert.Equal(ConversationRequesterType.Registered, searchResults[0].RequesterType);
        Assert.Single(pinnedSearchResults);
        Assert.True(pinnedSearchResults[0].IsPinned);
        Assert.Empty(emptyPinnedSearch);
    }

    [Fact]
    public async Task Summaries_IgnoreOrphanPinsAndDetailShowsCurrentUsersPinState()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        harness.DbContext.ConversationPins.Add(new ConversationPin
        {
            UserId = harness.AdminA.Id,
            RequesterType = ConversationRequesterType.External,
            RequesterId = 999_999
        });
        await harness.DbContext.SaveChangesAsync();

        var summaries = await harness.Service.GetSummariesAsync(harness.AdminA.Id, null);
        var beforePin = await harness.Service.GetDetailAsync(
            ConversationRequesterType.Registered,
            harness.Customer.Id,
            harness.AdminA.Id);
        await harness.Service.PinAsync(
            harness.AdminA.Id,
            UserRole.Admin,
            ConversationRequesterType.Registered,
            harness.Customer.Id);
        var afterPin = await harness.Service.GetDetailAsync(
            ConversationRequesterType.Registered,
            harness.Customer.Id,
            harness.AdminA.Id);

        Assert.Equal(2, summaries.Count);
        Assert.NotNull(beforePin);
        Assert.False(beforePin.IsPinned);
        Assert.NotNull(afterPin);
        Assert.True(afterPin.IsPinned);
    }

    [Fact]
    public async Task AwaitingReply_IgnoresFailedPendingAndInternalStaffMessagesUntilSent()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        var ticket = await harness.DbContext.Tickets
            .SingleAsync(item => item.TicketNumber == "SUP-2026-1001");
        var staffMessage = await harness.DbContext.TicketMessages
            .SingleAsync(item => item.Message == "Support response");
        staffMessage.OutboundDeliveryStatus = OutboundDeliveryStatus.Pending;
        staffMessage.DeliveredAt = null;
        await harness.DbContext.SaveChangesAsync();

        var waiting = await harness.Service.GetSummariesAsync(harness.AdminA.Id, null);
        Assert.True(waiting.Single(item => item.RequesterType == ConversationRequesterType.Registered).IsAwaitingStaffReply);

        staffMessage.OutboundDeliveryStatus = OutboundDeliveryStatus.Sent;
        staffMessage.DeliveredAt = ticket.CreatedAt.AddMinutes(30);
        await harness.DbContext.SaveChangesAsync();

        var answered = await harness.Service.GetDetailAsync(
            ConversationRequesterType.Registered, harness.Customer.Id, harness.AdminA.Id);
        Assert.False(answered!.Tickets.Single(item => item.Id == ticket.Id).IsAwaitingStaffReply);
    }

    [Fact]
    public async Task AwaitingReply_LegacyReplyAndResolvedTicketAreNotWaiting()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        var ticket = await harness.DbContext.Tickets
            .SingleAsync(item => item.TicketNumber == "SUP-2026-1001");
        ticket.Status = TicketStatus.Resolved;
        harness.DbContext.TicketMessages.Add(new TicketMessage
        {
            TicketId = ticket.Id,
            SenderId = harness.Staff.Id,
            Message = "Legacy reply",
            OutboundDeliveryStatus = OutboundDeliveryStatus.NotApplicable,
            CreatedAt = ticket.CreatedAt.AddMinutes(40)
        });
        await harness.DbContext.SaveChangesAsync();

        var detail = await harness.Service.GetDetailAsync(
            ConversationRequesterType.Registered, harness.Customer.Id, harness.AdminA.Id);
        Assert.False(detail!.Tickets.Single(item => item.Id == ticket.Id).IsAwaitingStaffReply);
    }

    [Fact]
    public async Task FailedStaffDelivery_DoesNotClearExternalRequesterAwaitingReply()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        var requesterActivityAt = new DateTime(2026, 8, 23, 9, 0, 0, DateTimeKind.Utc);
        var failedReplyAt = requesterActivityAt.AddMinutes(1);
        var requesterMessage = await harness.DbContext.TicketMessages
            .SingleAsync(item => item.TicketId == harness.ExternalTicket.Id);
        requesterMessage.CreatedAt = requesterActivityAt;
        harness.DbContext.TicketMessages.Add(new TicketMessage
        {
            TicketId = harness.ExternalTicket.Id,
            SenderId = harness.Staff.Id,
            Message = "Failed staff reply",
            OutboundDeliveryStatus = OutboundDeliveryStatus.Failed,
            CreatedAt = failedReplyAt
        });
        await harness.DbContext.SaveChangesAsync();

        var detail = await harness.Service.GetDetailAsync(
            ConversationRequesterType.External,
            harness.ExternalRequester.Id,
            harness.AdminA.Id);

        Assert.True(detail!.Tickets.Single(item => item.Id == harness.ExternalTicket.Id).IsAwaitingStaffReply);
    }

    [Fact]
    public async Task FailedStaffReply_RetryingSameMessageAsSent_ClearsExternalRequesterAwaitingReply()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        var t0 = new DateTime(2026, 8, 23, 9, 0, 0, DateTimeKind.Utc);
        var t1 = t0.AddMinutes(1);
        var t2 = t0.AddMinutes(2);
        harness.ExternalTicket.CreatedAt = t0.AddMinutes(-5);
        var requesterMessage = await harness.DbContext.TicketMessages.SingleAsync(item => item.TicketId == harness.ExternalTicket.Id);
        requesterMessage.CreatedAt = t0;
        var staffMessage = new TicketMessage
        {
            TicketId = harness.ExternalTicket.Id,
            SenderId = harness.Staff.Id,
            Message = "Retryable staff reply",
            OutboundDeliveryStatus = OutboundDeliveryStatus.Failed,
            LastDeliveryAttemptAt = t1,
            DeliveryAttemptCount = 1,
            CreatedAt = t1
        };
        harness.DbContext.TicketMessages.Add(staffMessage);
        await harness.DbContext.SaveChangesAsync();

        var messageIdBefore = staffMessage.Id;
        var messageCountBefore = await harness.DbContext.TicketMessages.CountAsync(item => item.TicketId == harness.ExternalTicket.Id);
        var failedDetail = await harness.Service.GetDetailAsync(ConversationRequesterType.External, harness.ExternalRequester.Id, harness.AdminA.Id);
        var failedSummary = (await harness.Service.GetSummariesAsync(harness.AdminA.Id, null)).Single(item => item.RequesterType == ConversationRequesterType.External);
        Assert.True(failedDetail!.Tickets.Single(item => item.Id == harness.ExternalTicket.Id).IsAwaitingStaffReply);
        Assert.True(failedSummary.IsAwaitingStaffReply);
        Assert.Equal(1, failedSummary.AwaitingReplyCount);

        staffMessage.OutboundDeliveryStatus = OutboundDeliveryStatus.Sent;
        staffMessage.DeliveredAt = t2;
        staffMessage.LastDeliveryAttemptAt = t2;
        staffMessage.DeliveryAttemptCount++;
        await harness.DbContext.SaveChangesAsync();

        var messageIdAfter = staffMessage.Id;
        var messageCountAfter = await harness.DbContext.TicketMessages.CountAsync(item => item.TicketId == harness.ExternalTicket.Id);
        var sentDetail = await harness.Service.GetDetailAsync(ConversationRequesterType.External, harness.ExternalRequester.Id, harness.AdminA.Id);
        var sentSummary = (await harness.Service.GetSummariesAsync(harness.AdminA.Id, null)).Single(item => item.RequesterType == ConversationRequesterType.External);
        Assert.False(sentDetail!.Tickets.Single(item => item.Id == harness.ExternalTicket.Id).IsAwaitingStaffReply);
        Assert.False(sentSummary.IsAwaitingStaffReply);
        Assert.Equal(0, sentSummary.AwaitingReplyCount);
        Assert.Equal(messageIdBefore, messageIdAfter);
        Assert.Equal(messageCountBefore, messageCountAfter);
        Assert.Equal(2, staffMessage.DeliveryAttemptCount);
        Assert.Equal(t2, staffMessage.DeliveredAt);
        Assert.Single(await harness.DbContext.TicketMessages.Where(item => item.TicketId == harness.ExternalTicket.Id && item.SenderId == harness.Staff.Id).ToListAsync());
    }

    [Fact]
    public async Task SentStaffDelivery_ClearsExternalRequesterAwaitingReply()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        var t0 = new DateTime(2026, 8, 23, 9, 0, 0, DateTimeKind.Utc);
        harness.ExternalTicket.CreatedAt = t0.AddMinutes(-1);
        var requesterMessage = await harness.DbContext.TicketMessages.SingleAsync(item => item.TicketId == harness.ExternalTicket.Id);
        requesterMessage.CreatedAt = t0;
        harness.DbContext.TicketMessages.Add(new TicketMessage { TicketId = harness.ExternalTicket.Id, SenderId = harness.Staff.Id, Message = "Sent reply", OutboundDeliveryStatus = OutboundDeliveryStatus.Sent, CreatedAt = t0.AddMinutes(1), DeliveredAt = t0.AddMinutes(1) });
        await harness.DbContext.SaveChangesAsync();

        var detail = await harness.Service.GetDetailAsync(ConversationRequesterType.External, harness.ExternalRequester.Id, harness.AdminA.Id);
        var summary = (await harness.Service.GetSummariesAsync(harness.AdminA.Id, null)).Single(item => item.RequesterType == ConversationRequesterType.External);
        Assert.False(detail!.Tickets.Single(item => item.Id == harness.ExternalTicket.Id).IsAwaitingStaffReply);
        Assert.False(summary.IsAwaitingStaffReply);
        Assert.Equal(0, summary.AwaitingReplyCount);
    }

    [Fact]
    public async Task PendingStaffDelivery_DoesNotClearExternalRequesterAwaitingReply()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        var t0 = new DateTime(2026, 8, 23, 9, 0, 0, DateTimeKind.Utc);
        var requesterMessage = await harness.DbContext.TicketMessages.SingleAsync(item => item.TicketId == harness.ExternalTicket.Id);
        requesterMessage.CreatedAt = t0;
        harness.DbContext.TicketMessages.Add(new TicketMessage { TicketId = harness.ExternalTicket.Id, SenderId = harness.Staff.Id, Message = "Pending reply", OutboundDeliveryStatus = OutboundDeliveryStatus.Pending, CreatedAt = t0.AddMinutes(1) });
        await harness.DbContext.SaveChangesAsync();

        var detail = await harness.Service.GetDetailAsync(ConversationRequesterType.External, harness.ExternalRequester.Id, harness.AdminA.Id);
        var summary = (await harness.Service.GetSummariesAsync(harness.AdminA.Id, null)).Single(item => item.RequesterType == ConversationRequesterType.External);
        Assert.True(detail!.Tickets.Single(item => item.Id == harness.ExternalTicket.Id).IsAwaitingStaffReply);
        Assert.True(summary.IsAwaitingStaffReply);
    }

    [Fact]
    public async Task InternalNote_DoesNotClearExternalRequesterAwaitingReply()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        var t0 = new DateTime(2026, 8, 23, 9, 0, 0, DateTimeKind.Utc);
        var requesterMessage = await harness.DbContext.TicketMessages.SingleAsync(item => item.TicketId == harness.ExternalTicket.Id);
        requesterMessage.CreatedAt = t0;
        harness.DbContext.TicketMessages.Add(new TicketMessage { TicketId = harness.ExternalTicket.Id, SenderId = harness.Staff.Id, Message = "Internal note", IsInternal = true, CreatedAt = t0.AddMinutes(1) });
        await harness.DbContext.SaveChangesAsync();

        var detail = await harness.Service.GetDetailAsync(ConversationRequesterType.External, harness.ExternalRequester.Id, harness.AdminA.Id);
        Assert.True(detail!.Tickets.Single(item => item.Id == harness.ExternalTicket.Id).IsAwaitingStaffReply);
    }

    [Fact]
    public async Task LegacyNotApplicableStaffReply_ClearsExternalRequesterAwaitingReply()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        var t0 = new DateTime(2026, 8, 23, 9, 0, 0, DateTimeKind.Utc);
        harness.ExternalTicket.CreatedAt = t0.AddMinutes(-1);
        var requesterMessage = await harness.DbContext.TicketMessages.SingleAsync(item => item.TicketId == harness.ExternalTicket.Id);
        requesterMessage.CreatedAt = t0;
        harness.DbContext.TicketMessages.Add(new TicketMessage { TicketId = harness.ExternalTicket.Id, SenderId = harness.Staff.Id, Message = "Legacy reply", OutboundDeliveryStatus = OutboundDeliveryStatus.NotApplicable, CreatedAt = t0.AddMinutes(1) });
        await harness.DbContext.SaveChangesAsync();

        var detail = await harness.Service.GetDetailAsync(ConversationRequesterType.External, harness.ExternalRequester.Id, harness.AdminA.Id);
        var summary = (await harness.Service.GetSummariesAsync(harness.AdminA.Id, null)).Single(item => item.RequesterType == ConversationRequesterType.External);
        Assert.False(detail!.Tickets.Single(item => item.Id == harness.ExternalTicket.Id).IsAwaitingStaffReply);
        Assert.False(summary.IsAwaitingStaffReply);
        Assert.Equal(0, summary.AwaitingReplyCount);
    }

    [Fact]
    public async Task ResolvedExternalTicket_DoesNotAwaitStaffReply()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        harness.ExternalTicket.Status = TicketStatus.Resolved;
        harness.ExternalTicket.CreatedAt = new DateTime(2026, 8, 23, 8, 59, 0, DateTimeKind.Utc);
        await harness.DbContext.SaveChangesAsync();

        var detail = await harness.Service.GetDetailAsync(ConversationRequesterType.External, harness.ExternalRequester.Id, harness.AdminA.Id);
        var summary = (await harness.Service.GetSummariesAsync(harness.AdminA.Id, null)).Single(item => item.RequesterType == ConversationRequesterType.External);
        Assert.False(detail!.Tickets.Single(item => item.Id == harness.ExternalTicket.Id).IsAwaitingStaffReply);
        Assert.False(summary.IsAwaitingStaffReply);
    }

    [Fact]
    public async Task ClosedExternalTicket_DoesNotAwaitStaffReply()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        harness.ExternalTicket.Status = TicketStatus.Closed;
        harness.ExternalTicket.CreatedAt = new DateTime(2026, 8, 23, 8, 59, 0, DateTimeKind.Utc);
        await harness.DbContext.SaveChangesAsync();

        var detail = await harness.Service.GetDetailAsync(ConversationRequesterType.External, harness.ExternalRequester.Id, harness.AdminA.Id);
        var summary = (await harness.Service.GetSummariesAsync(harness.AdminA.Id, null)).Single(item => item.RequesterType == ConversationRequesterType.External);
        Assert.False(detail!.Tickets.Single(item => item.Id == harness.ExternalTicket.Id).IsAwaitingStaffReply);
        Assert.False(summary.IsAwaitingStaffReply);
    }

    [Fact]
    public async Task ReopenedExternalTicket_WithNewRequesterActivity_AwaitsStaffReply()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        var reopenedAt = new DateTime(2026, 8, 23, 9, 5, 0, DateTimeKind.Utc);
        harness.ExternalTicket.CreatedAt = reopenedAt.AddMinutes(-10);
        harness.ExternalTicket.Status = TicketStatus.Reopened;
        var requesterMessage = await harness.DbContext.TicketMessages.SingleAsync(item => item.TicketId == harness.ExternalTicket.Id);
        requesterMessage.CreatedAt = reopenedAt;
        await harness.DbContext.SaveChangesAsync();

        var detail = await harness.Service.GetDetailAsync(ConversationRequesterType.External, harness.ExternalRequester.Id, harness.AdminA.Id);
        var summary = (await harness.Service.GetSummariesAsync(harness.AdminA.Id, null)).Single(item => item.RequesterType == ConversationRequesterType.External);
        Assert.True(detail!.Tickets.Single(item => item.Id == harness.ExternalTicket.Id).IsAwaitingStaffReply);
        Assert.True(summary.IsAwaitingStaffReply);
        Assert.Equal(1, summary.AwaitingReplyCount);
    }

    [Fact]
    public async Task ExternalRequester_MultipleTickets_ReportsExactAwaitingReplyCount()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        var t0 = new DateTime(2026, 8, 23, 9, 0, 0, DateTimeKind.Utc);
        harness.ExternalTicket.CreatedAt = t0.AddMinutes(-10);
        var ticketARequesterMessage = await harness.DbContext.TicketMessages.SingleAsync(item => item.TicketId == harness.ExternalTicket.Id);
        ticketARequesterMessage.CreatedAt = t0;
        var ticketB = new Ticket { TicketNumber = "SUP-2026-1004", ExternalRequesterId = harness.ExternalRequester.Id, Title = "Answered", Description = "B", Status = TicketStatus.New, Priority = TicketPriority.Normal, CreatedAt = t0.AddMinutes(-10), UpdatedAt = t0.AddMinutes(-10) };
        var ticketC = new Ticket { TicketNumber = "SUP-2026-1005", ExternalRequesterId = harness.ExternalRequester.Id, Title = "Waiting", Description = "C", Status = TicketStatus.New, Priority = TicketPriority.Normal, CreatedAt = t0.AddMinutes(-10), UpdatedAt = t0.AddMinutes(-10) };
        harness.DbContext.Tickets.AddRange(ticketB, ticketC);
        await harness.DbContext.SaveChangesAsync();
        harness.DbContext.TicketMessages.AddRange(
            new TicketMessage { TicketId = ticketB.Id, ExternalRequesterId = harness.ExternalRequester.Id, Message = "B requester", CreatedAt = t0.AddMinutes(1) },
            new TicketMessage { TicketId = ticketB.Id, SenderId = harness.Staff.Id, Message = "B sent", OutboundDeliveryStatus = OutboundDeliveryStatus.Sent, CreatedAt = t0.AddMinutes(2), DeliveredAt = t0.AddMinutes(2) },
            new TicketMessage { TicketId = ticketC.Id, ExternalRequesterId = harness.ExternalRequester.Id, Message = "C requester", CreatedAt = t0.AddMinutes(3) });
        await harness.DbContext.SaveChangesAsync();

        var detail = await harness.Service.GetDetailAsync(ConversationRequesterType.External, harness.ExternalRequester.Id, harness.AdminA.Id);
        var summary = Assert.Single(
            await harness.Service.GetSummariesAsync(harness.AdminA.Id, null),
            item => item.RequesterType == ConversationRequesterType.External);
        Assert.True(detail!.Tickets.Single(item => item.Id == harness.ExternalTicket.Id).IsAwaitingStaffReply);
        Assert.False(detail.Tickets.Single(item => item.Id == ticketB.Id).IsAwaitingStaffReply);
        Assert.True(detail.Tickets.Single(item => item.Id == ticketC.Id).IsAwaitingStaffReply);
        Assert.True(summary.IsAwaitingStaffReply);
        Assert.Equal(2, summary.AwaitingReplyCount);
    }

    [Fact]
    public async Task AwaitingConversation_ExposesOldestAwaitingProjectionForTicketAndSummary()
    {
        var now = new DateTime(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await ConversationHarness.CreateAsync(new FixedTimeProvider(now));
        var createdAt = now.AddHours(-3);
        harness.ExternalTicket.CreatedAt = createdAt;
        var requesterMessage = await harness.DbContext.TicketMessages
            .SingleAsync(item => item.TicketId == harness.ExternalTicket.Id);
        requesterMessage.CreatedAt = now.AddHours(-2);
        await harness.DbContext.SaveChangesAsync();

        var detail = await harness.Service.GetDetailAsync(
            ConversationRequesterType.External,
            harness.ExternalRequester.Id,
            harness.AdminA.Id);
        var summary = (await harness.Service.GetSummariesAsync(harness.AdminA.Id, null))
            .Single(item => item.RequesterType == ConversationRequesterType.External);
        var ticket = Assert.Single(detail!.Tickets);

        Assert.Equal(createdAt, ticket.AwaitingStaffSince);
        Assert.Equal(180, ticket.AwaitingStaffMinutes);
        Assert.Equal(createdAt, summary.OldestAwaitingSince);
        Assert.Equal(180, summary.OldestAwaitingMinutes);
    }

    [Fact]
    public async Task AwaitingConversation_SelectsOldestActiveTicketAndExcludesAnsweredTicket()
    {
        var now = new DateTime(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await ConversationHarness.CreateAsync(new FixedTimeProvider(now));
        var oldestAt = now.AddHours(-3);
        harness.ExternalTicket.CreatedAt = oldestAt;
        var existingRequesterMessage = await harness.DbContext.TicketMessages
            .SingleAsync(item => item.TicketId == harness.ExternalTicket.Id);
        existingRequesterMessage.CreatedAt = now.AddHours(-2);
        var answeredTicket = new Ticket
        {
            TicketNumber = "SUP-2026-AGING-ANSWERED",
            ExternalRequesterId = harness.ExternalRequester.Id,
            Title = "Answered",
            Description = "Answered",
            Status = TicketStatus.New,
            Priority = TicketPriority.Normal,
            CreatedAt = now.AddHours(-2),
            UpdatedAt = now.AddHours(-2)
        };
        var newerWaitingTicket = new Ticket
        {
            TicketNumber = "SUP-2026-AGING-WAITING",
            ExternalRequesterId = harness.ExternalRequester.Id,
            Title = "Waiting",
            Description = "Waiting",
            Status = TicketStatus.New,
            Priority = TicketPriority.Normal,
            CreatedAt = now.AddHours(-1),
            UpdatedAt = now.AddHours(-1)
        };
        harness.DbContext.Tickets.AddRange(answeredTicket, newerWaitingTicket);
        await harness.DbContext.SaveChangesAsync();
        harness.DbContext.TicketMessages.Add(new TicketMessage
        {
            TicketId = answeredTicket.Id,
            SenderId = harness.Staff.Id,
            Message = "Delivered",
            OutboundDeliveryStatus = OutboundDeliveryStatus.Sent,
            CreatedAt = now.AddHours(-1.5),
            DeliveredAt = now.AddHours(-1.5)
        });
        await harness.DbContext.SaveChangesAsync();

        var summary = (await harness.Service.GetSummariesAsync(harness.AdminA.Id, null))
            .Single(item => item.RequesterType == ConversationRequesterType.External);

        Assert.Equal(2, summary.AwaitingReplyCount);
        Assert.Equal(oldestAt, summary.OldestAwaitingSince);
        Assert.Equal(180, summary.OldestAwaitingMinutes);
    }

    [Fact]
    public async Task ResolvedConversation_DoesNotExposeOldestAwaitingProjection()
    {
        var now = new DateTime(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await ConversationHarness.CreateAsync(new FixedTimeProvider(now));
        harness.ExternalTicket.Status = TicketStatus.Resolved;
        harness.ExternalTicket.CreatedAt = now.AddHours(-1);
        await harness.DbContext.SaveChangesAsync();

        var summary = (await harness.Service.GetSummariesAsync(harness.AdminA.Id, null))
            .Single(item => item.RequesterType == ConversationRequesterType.External);

        Assert.False(summary.IsAwaitingStaffReply);
        Assert.Equal(0, summary.AwaitingReplyCount);
        Assert.Null(summary.OldestAwaitingSince);
        Assert.Null(summary.OldestAwaitingMinutes);
    }

    [Fact]
    public async Task RegisteredCustomer_WithoutReadState_IsUnreadAndListingDoesNotCreateState()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        var t0 = new DateTime(2024, 8, 23, 9, 0, 0, DateTimeKind.Utc);
        var customerMessage = await harness.DbContext.TicketMessages.SingleAsync(item => item.Message == "Customer reply");
        var duplicateMessage = await harness.DbContext.TicketMessages.SingleAsync(item => item.Message == "Same initial request");
        var ticket = await harness.DbContext.Tickets.SingleAsync(item => item.TicketNumber == "SUP-2026-1001");
        ticket.CreatedAt = t0.AddMinutes(-2);
        customerMessage.CreatedAt = t0;
        harness.RegisteredDuplicateTicket.CreatedAt = t0.AddMinutes(-1);
        duplicateMessage.CreatedAt = t0.AddMinutes(-1);
        await harness.DbContext.SaveChangesAsync();

        var summary = (await harness.Service.GetSummariesAsync(harness.AdminA.Id, null))
            .Single(item => item.RequesterType == ConversationRequesterType.Registered);

        Assert.True(summary.IsUnread);
        Assert.Equal(0, await harness.DbContext.ConversationReadStates.CountAsync());
    }

    [Fact]
    public async Task RegisteredCustomer_MarkRead_ClearsUnreadWithoutChangingTicketData()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        var t0 = new DateTime(2024, 8, 23, 9, 0, 0, DateTimeKind.Utc);
        var customerMessage = await harness.DbContext.TicketMessages.SingleAsync(item => item.Message == "Customer reply");
        var duplicateMessage = await harness.DbContext.TicketMessages.SingleAsync(item => item.Message == "Same initial request");
        var ticket = await harness.DbContext.Tickets.SingleAsync(item => item.TicketNumber == "SUP-2026-1001");
        ticket.CreatedAt = t0.AddMinutes(-2);
        customerMessage.CreatedAt = t0;
        harness.RegisteredDuplicateTicket.CreatedAt = t0.AddMinutes(-1);
        duplicateMessage.CreatedAt = t0.AddMinutes(-1);
        var statusBefore = ticket.Status;
        var priorityBefore = ticket.Priority;
        var assignedUserIdBefore = ticket.AssignedUserId;
        var customerMessageBefore = customerMessage.Message;
        await harness.DbContext.SaveChangesAsync();

        var markStartedAt = DateTime.UtcNow;
        await harness.Service.MarkReadAsync(harness.AdminA.Id, UserRole.Admin, ConversationRequesterType.Registered, harness.Customer.Id);
        var markCompletedAt = DateTime.UtcNow;

        var state = await harness.DbContext.ConversationReadStates.SingleAsync();
        var summary = (await harness.Service.GetSummariesAsync(harness.AdminA.Id, null))
            .Single(item => item.RequesterType == ConversationRequesterType.Registered);
        Assert.InRange(state.LastReadAt, markStartedAt.AddSeconds(-1), markCompletedAt.AddSeconds(1));
        Assert.False(summary.IsUnread);
        Assert.Equal(statusBefore, ticket.Status);
        Assert.Equal(priorityBefore, ticket.Priority);
        Assert.Equal(assignedUserIdBefore, ticket.AssignedUserId);
        Assert.Equal(customerMessageBefore, customerMessage.Message);
    }

    [Fact]
    public async Task RegisteredCustomer_NewRequesterActivityAfterRead_IsUnreadAgainWithoutDuplicateState()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        var t0 = new DateTime(2024, 8, 23, 9, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2100, 8, 23, 9, 2, 0, DateTimeKind.Utc);
        var customerMessage = await harness.DbContext.TicketMessages.SingleAsync(item => item.Message == "Customer reply");
        var duplicateMessage = await harness.DbContext.TicketMessages.SingleAsync(item => item.Message == "Same initial request");
        var ticket = await harness.DbContext.Tickets.SingleAsync(item => item.TicketNumber == "SUP-2026-1001");
        ticket.CreatedAt = t0.AddMinutes(-2);
        customerMessage.CreatedAt = t0;
        harness.RegisteredDuplicateTicket.CreatedAt = t0.AddMinutes(-1);
        duplicateMessage.CreatedAt = t0.AddMinutes(-1);
        await harness.DbContext.SaveChangesAsync();
        await harness.Service.MarkReadAsync(harness.AdminA.Id, UserRole.Admin, ConversationRequesterType.Registered, harness.Customer.Id);

        harness.DbContext.TicketMessages.Add(new TicketMessage
        {
            TicketId = ticket.Id,
            SenderId = harness.Customer.Id,
            Message = "New customer activity",
            CreatedAt = t2
        });
        await harness.DbContext.SaveChangesAsync();

        var summary = (await harness.Service.GetSummariesAsync(harness.AdminA.Id, null))
            .Single(item => item.RequesterType == ConversationRequesterType.Registered);
        Assert.True(summary.IsUnread);
        Assert.Equal(1, await harness.DbContext.ConversationReadStates.CountAsync());
    }

    [Fact]
    public async Task ExternalRequester_SentStaffReplyAfterRead_DoesNotCreateUnread()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        var t0 = new DateTime(2026, 8, 23, 9, 0, 0, DateTimeKind.Utc);
        var t1 = t0.AddMinutes(1);
        var t2 = t0.AddMinutes(2);
        harness.ExternalTicket.CreatedAt = t0.AddMinutes(-1);
        var requesterMessage = await harness.DbContext.TicketMessages.SingleAsync(item => item.TicketId == harness.ExternalTicket.Id);
        requesterMessage.CreatedAt = t0;
        harness.DbContext.ConversationReadStates.Add(new ConversationReadState
        {
            UserId = harness.AdminA.Id,
            RequesterType = ConversationRequesterType.External,
            RequesterId = harness.ExternalRequester.Id,
            LastReadAt = t1,
            UpdatedAt = t1
        });
        harness.DbContext.TicketMessages.Add(new TicketMessage
        {
            TicketId = harness.ExternalTicket.Id,
            SenderId = harness.Staff.Id,
            Message = "Sent staff reply",
            OutboundDeliveryStatus = OutboundDeliveryStatus.Sent,
            DeliveredAt = t2,
            CreatedAt = t2
        });
        await harness.DbContext.SaveChangesAsync();

        var summary = (await harness.Service.GetSummariesAsync(harness.AdminA.Id, null))
            .Single(item => item.RequesterType == ConversationRequesterType.External);

        Assert.False(summary.IsUnread);
    }

    [Fact]
    public async Task ExternalRequester_InternalNoteAfterRead_DoesNotCreateUnread()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        var t0 = new DateTime(2026, 8, 23, 9, 0, 0, DateTimeKind.Utc);
        var t1 = t0.AddMinutes(1);
        var t2 = t0.AddMinutes(2);
        harness.ExternalTicket.CreatedAt = t0.AddMinutes(-1);
        var requesterMessage = await harness.DbContext.TicketMessages.SingleAsync(item => item.TicketId == harness.ExternalTicket.Id);
        requesterMessage.CreatedAt = t0;
        harness.DbContext.ConversationReadStates.Add(new ConversationReadState
        {
            UserId = harness.AdminA.Id,
            RequesterType = ConversationRequesterType.External,
            RequesterId = harness.ExternalRequester.Id,
            LastReadAt = t1,
            UpdatedAt = t1
        });
        harness.DbContext.TicketMessages.Add(new TicketMessage
        {
            TicketId = harness.ExternalTicket.Id,
            SenderId = harness.Staff.Id,
            Message = "Internal note",
            IsInternal = true,
            CreatedAt = t2
        });
        await harness.DbContext.SaveChangesAsync();

        var summary = (await harness.Service.GetSummariesAsync(harness.AdminA.Id, null))
            .Single(item => item.RequesterType == ConversationRequesterType.External);

        Assert.False(summary.IsUnread);
    }

    [Theory]
    [InlineData(OutboundDeliveryStatus.Failed)]
    [InlineData(OutboundDeliveryStatus.Pending)]
    public async Task ExternalRequester_FailedOrPendingStaffDeliveryAfterRead_DoesNotCreateUnread(OutboundDeliveryStatus deliveryStatus)
    {
        await using var harness = await ConversationHarness.CreateAsync();
        var t0 = new DateTime(2026, 8, 23, 9, 0, 0, DateTimeKind.Utc);
        var t1 = t0.AddMinutes(1);
        var t2 = t0.AddMinutes(2);
        harness.ExternalTicket.CreatedAt = t0.AddMinutes(-1);
        var requesterMessage = await harness.DbContext.TicketMessages.SingleAsync(item => item.TicketId == harness.ExternalTicket.Id);
        requesterMessage.CreatedAt = t0;
        harness.DbContext.ConversationReadStates.Add(new ConversationReadState
        {
            UserId = harness.AdminA.Id,
            RequesterType = ConversationRequesterType.External,
            RequesterId = harness.ExternalRequester.Id,
            LastReadAt = t1,
            UpdatedAt = t1
        });
        harness.DbContext.TicketMessages.Add(new TicketMessage
        {
            TicketId = harness.ExternalTicket.Id,
            SenderId = harness.Staff.Id,
            Message = "Undelivered staff reply",
            OutboundDeliveryStatus = deliveryStatus,
            CreatedAt = t2
        });
        await harness.DbContext.SaveChangesAsync();

        var summary = (await harness.Service.GetSummariesAsync(harness.AdminA.Id, null))
            .Single(item => item.RequesterType == ConversationRequesterType.External);

        Assert.False(summary.IsUnread);
    }

    [Fact]
    public async Task ExternalRequester_RequesterActivityAtLastReadBoundary_IsNotUnread()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        var t1 = new DateTime(2026, 8, 23, 9, 1, 0, DateTimeKind.Utc);
        harness.ExternalTicket.CreatedAt = t1.AddMinutes(-1);
        var requesterMessage = await harness.DbContext.TicketMessages.SingleAsync(item => item.TicketId == harness.ExternalTicket.Id);
        requesterMessage.CreatedAt = t1;
        harness.DbContext.ConversationReadStates.Add(new ConversationReadState
        {
            UserId = harness.AdminA.Id,
            RequesterType = ConversationRequesterType.External,
            RequesterId = harness.ExternalRequester.Id,
            LastReadAt = t1,
            UpdatedAt = t1
        });
        await harness.DbContext.SaveChangesAsync();

        var summary = (await harness.Service.GetSummariesAsync(harness.AdminA.Id, null))
            .Single(item => item.RequesterType == ConversationRequesterType.External);

        Assert.False(summary.IsUnread);
    }

    [Fact]
    public async Task RegisteredCustomer_AdminAMarkRead_DoesNotAffectAdminBUnreadState()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        var t0 = new DateTime(2024, 8, 23, 9, 0, 0, DateTimeKind.Utc);
        var ticket = await harness.DbContext.Tickets.SingleAsync(item => item.TicketNumber == "SUP-2026-1001");
        var customerMessage = await harness.DbContext.TicketMessages.SingleAsync(item => item.Message == "Customer reply");
        var duplicateMessage = await harness.DbContext.TicketMessages.SingleAsync(item => item.Message == "Same initial request");
        ticket.CreatedAt = t0.AddMinutes(-2);
        customerMessage.CreatedAt = t0;
        harness.RegisteredDuplicateTicket.CreatedAt = t0.AddMinutes(-1);
        duplicateMessage.CreatedAt = t0.AddMinutes(-1);
        await harness.DbContext.SaveChangesAsync();

        var beforeAdminA = (await harness.Service.GetSummariesAsync(harness.AdminA.Id, null)).Single(item => item.RequesterType == ConversationRequesterType.Registered);
        var beforeAdminB = (await harness.Service.GetSummariesAsync(harness.AdminB.Id, null)).Single(item => item.RequesterType == ConversationRequesterType.Registered);
        await harness.Service.MarkReadAsync(harness.AdminA.Id, UserRole.Admin, ConversationRequesterType.Registered, harness.Customer.Id);
        var afterAdminA = (await harness.Service.GetSummariesAsync(harness.AdminA.Id, null)).Single(item => item.RequesterType == ConversationRequesterType.Registered);
        var afterAdminB = (await harness.Service.GetSummariesAsync(harness.AdminB.Id, null)).Single(item => item.RequesterType == ConversationRequesterType.Registered);

        Assert.True(beforeAdminA.IsUnread);
        Assert.True(beforeAdminB.IsUnread);
        Assert.False(afterAdminA.IsUnread);
        Assert.True(afterAdminB.IsUnread);
        var state = await harness.DbContext.ConversationReadStates.SingleAsync();
        Assert.Equal(harness.AdminA.Id, state.UserId);
        Assert.Equal(ConversationRequesterType.Registered, state.RequesterType);
        Assert.Equal(harness.Customer.Id, state.RequesterId);
    }

    [Fact]
    public async Task RegisteredCustomer_TechnicalStaffMarkRead_DoesNotAffectAdminUnreadState()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        var t0 = new DateTime(2024, 8, 23, 9, 0, 0, DateTimeKind.Utc);
        var ticket = await harness.DbContext.Tickets.SingleAsync(item => item.TicketNumber == "SUP-2026-1001");
        var customerMessage = await harness.DbContext.TicketMessages.SingleAsync(item => item.Message == "Customer reply");
        var duplicateMessage = await harness.DbContext.TicketMessages.SingleAsync(item => item.Message == "Same initial request");
        ticket.CreatedAt = t0.AddMinutes(-2);
        customerMessage.CreatedAt = t0;
        harness.RegisteredDuplicateTicket.CreatedAt = t0.AddMinutes(-1);
        duplicateMessage.CreatedAt = t0.AddMinutes(-1);
        await harness.DbContext.SaveChangesAsync();

        await harness.Service.MarkReadAsync(harness.Staff.Id, UserRole.TechnicalStaff, ConversationRequesterType.Registered, harness.Customer.Id);
        var staffSummary = (await harness.Service.GetSummariesAsync(harness.Staff.Id, null)).Single(item => item.RequesterType == ConversationRequesterType.Registered);
        var adminSummary = (await harness.Service.GetSummariesAsync(harness.AdminA.Id, null)).Single(item => item.RequesterType == ConversationRequesterType.Registered);

        Assert.False(staffSummary.IsUnread);
        Assert.True(adminSummary.IsUnread);
        var state = await harness.DbContext.ConversationReadStates.SingleAsync();
        Assert.Equal(harness.Staff.Id, state.UserId);
    }

    [Fact]
    public async Task SameNumericRequesterIds_RegisteredReadState_DoesNotAffectExternalUnreadState()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        var t0 = new DateTime(2024, 8, 23, 9, 0, 0, DateTimeKind.Utc);
        var registeredTicket = await harness.DbContext.Tickets.SingleAsync(item => item.TicketNumber == "SUP-2026-1001");
        var customerMessage = await harness.DbContext.TicketMessages.SingleAsync(item => item.Message == "Customer reply");
        var duplicateMessage = await harness.DbContext.TicketMessages.SingleAsync(item => item.Message == "Same initial request");
        var externalMessage = await harness.DbContext.TicketMessages.SingleAsync(item => item.TicketId == harness.ExternalTicket.Id);
        registeredTicket.CreatedAt = t0.AddMinutes(-3);
        customerMessage.CreatedAt = t0;
        harness.RegisteredDuplicateTicket.CreatedAt = t0.AddMinutes(-2);
        duplicateMessage.CreatedAt = t0.AddMinutes(-2);
        harness.ExternalTicket.CreatedAt = t0.AddMinutes(-1);
        externalMessage.CreatedAt = t0;
        await harness.DbContext.SaveChangesAsync();

        Assert.Equal(harness.Customer.Id, harness.ExternalRequester.Id);
        await harness.Service.MarkReadAsync(harness.AdminA.Id, UserRole.Admin, ConversationRequesterType.Registered, harness.Customer.Id);
        var summaries = await harness.Service.GetSummariesAsync(harness.AdminA.Id, null);
        var registered = summaries.Single(item => item.RequesterType == ConversationRequesterType.Registered);
        var external = summaries.Single(item => item.RequesterType == ConversationRequesterType.External);

        Assert.False(registered.IsUnread);
        Assert.True(external.IsUnread);
        var state = await harness.DbContext.ConversationReadStates.SingleAsync();
        Assert.Equal(ConversationRequesterType.Registered, state.RequesterType);
    }

    [Fact]
    public async Task SameStaffUser_CanRetainRegisteredAndExternalReadStatesWithSameNumericRequesterId()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        var t0 = new DateTime(2024, 8, 23, 9, 0, 0, DateTimeKind.Utc);
        var registeredTicket = await harness.DbContext.Tickets.SingleAsync(item => item.TicketNumber == "SUP-2026-1001");
        var customerMessage = await harness.DbContext.TicketMessages.SingleAsync(item => item.Message == "Customer reply");
        var duplicateMessage = await harness.DbContext.TicketMessages.SingleAsync(item => item.Message == "Same initial request");
        var externalMessage = await harness.DbContext.TicketMessages.SingleAsync(item => item.TicketId == harness.ExternalTicket.Id);
        var statusBefore = registeredTicket.Status;
        var priorityBefore = registeredTicket.Priority;
        var assignedUserIdBefore = registeredTicket.AssignedUserId;
        registeredTicket.CreatedAt = t0.AddMinutes(-3);
        customerMessage.CreatedAt = t0;
        harness.RegisteredDuplicateTicket.CreatedAt = t0.AddMinutes(-2);
        duplicateMessage.CreatedAt = t0.AddMinutes(-2);
        harness.ExternalTicket.CreatedAt = t0.AddMinutes(-1);
        externalMessage.CreatedAt = t0;
        var pin = new ConversationPin
        {
            UserId = harness.AdminA.Id,
            RequesterType = ConversationRequesterType.External,
            RequesterId = harness.ExternalRequester.Id,
            CreatedAt = t0
        };
        harness.DbContext.ConversationPins.Add(pin);
        await harness.DbContext.SaveChangesAsync();
        var externalMessageCreatedAtBeforeMarkRead = externalMessage.CreatedAt;

        Assert.Equal(harness.Customer.Id, harness.ExternalRequester.Id);
        await harness.Service.MarkReadAsync(harness.AdminA.Id, UserRole.Admin, ConversationRequesterType.Registered, harness.Customer.Id);
        await harness.Service.MarkReadAsync(harness.AdminA.Id, UserRole.Admin, ConversationRequesterType.External, harness.ExternalRequester.Id);

        var states = await harness.DbContext.ConversationReadStates
            .Where(item => item.UserId == harness.AdminA.Id)
            .OrderBy(item => item.RequesterType)
            .ToListAsync();
        var preservedPin = await harness.DbContext.ConversationPins.SingleAsync();
        Assert.Equal(2, states.Count);
        Assert.Contains(states, item => item.RequesterType == ConversationRequesterType.Registered && item.RequesterId == harness.Customer.Id);
        Assert.Contains(states, item => item.RequesterType == ConversationRequesterType.External && item.RequesterId == harness.ExternalRequester.Id);
        Assert.Equal(pin.Id, preservedPin.Id);
        Assert.Equal(statusBefore, registeredTicket.Status);
        Assert.Equal(priorityBefore, registeredTicket.Priority);
        Assert.Equal(assignedUserIdBefore, registeredTicket.AssignedUserId);
        Assert.Equal(externalMessageCreatedAtBeforeMarkRead, externalMessage.CreatedAt);
    }

    [Fact]
    public async Task MarkRead_IsIdempotentAndIsolatedPerStaffUser()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        var before = await harness.Service.GetSummariesAsync(harness.AdminA.Id, null);
        Assert.True(before.Single(item => item.RequesterType == ConversationRequesterType.Registered).IsUnread);

        await harness.Service.MarkReadAsync(harness.AdminA.Id, UserRole.Admin, ConversationRequesterType.Registered, harness.Customer.Id);
        await harness.Service.MarkReadAsync(harness.AdminA.Id, UserRole.Admin, ConversationRequesterType.Registered, harness.Customer.Id);

        var adminA = await harness.Service.GetSummariesAsync(harness.AdminA.Id, null);
        var adminB = await harness.Service.GetSummariesAsync(harness.AdminB.Id, null);
        Assert.False(adminA.Single(item => item.RequesterType == ConversationRequesterType.Registered).IsUnread);
        Assert.True(adminB.Single(item => item.RequesterType == ConversationRequesterType.Registered).IsUnread);
        Assert.Equal(1, await harness.DbContext.ConversationReadStates.CountAsync());
    }

    [Fact]
    public async Task SummaryFilters_ComposeWithSearchAndPreservePinnedBehavior()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        await harness.Service.PinAsync(harness.AdminA.Id, UserRole.Admin, ConversationRequesterType.External, harness.ExternalRequester.Id);

        var unread = await harness.Service.GetSummariesAsync(harness.AdminA.Id, null, ConversationFilter.Unread);
        var waiting = await harness.Service.GetSummariesAsync(harness.AdminA.Id, "Dış sistem", ConversationFilter.AwaitingReply);
        var pinned = await harness.Service.GetSummariesAsync(harness.AdminA.Id, "Dış sistem", ConversationFilter.Pinned);

        Assert.All(unread, item => Assert.True(item.IsUnread));
        Assert.Single(waiting);
        Assert.True(waiting[0].IsAwaitingStaffReply);
        Assert.Single(pinned);
        Assert.True(pinned[0].IsPinned);
    }

    [Fact]
    public async Task SummaryList_ReturnsGlobalCountsThatIgnoreSearchWhileItemsRespectSearchAndFilter()
    {
        await using var harness = await ConversationHarness.CreateAsync();

        var response = await harness.Service.GetSummaryListAsync(
            harness.AdminA.Id,
            "SUP-2026-1001",
            ConversationFilter.AwaitingReply);

        Assert.Equal(2, response.Counts.All);
        Assert.Equal(2, response.Counts.AwaitingReply);
        Assert.Equal(2, response.Counts.Unread);
        Assert.Equal(0, response.Counts.Pinned);
        var item = Assert.Single(response.Items);
        Assert.Equal(ConversationRequesterType.Registered, item.RequesterType);
        Assert.True(item.IsAwaitingStaffReply);
    }

    [Fact]
    public async Task SummaryList_CountsArePinnedAndUnreadPerCurrentStaffUser()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        await harness.Service.PinAsync(
            harness.AdminA.Id,
            UserRole.Admin,
            ConversationRequesterType.External,
            harness.ExternalRequester.Id);
        await harness.Service.MarkReadAsync(
            harness.AdminA.Id,
            UserRole.Admin,
            ConversationRequesterType.Registered,
            harness.Customer.Id);

        var adminA = await harness.Service.GetSummaryListAsync(harness.AdminA.Id, null);
        var adminB = await harness.Service.GetSummaryListAsync(harness.AdminB.Id, null);

        Assert.Equal(1, adminA.Counts.Pinned);
        Assert.Equal(1, adminA.Counts.Unread);
        Assert.Equal(0, adminB.Counts.Pinned);
        Assert.Equal(2, adminB.Counts.Unread);
    }

    [Fact]
    public async Task SummaryList_PreservesRegisteredAndExternalGrouping()
    {
        await using var harness = await ConversationHarness.CreateAsync();

        var response = await harness.Service.GetSummaryListAsync(harness.Staff.Id, null);

        Assert.Equal(2, response.Counts.All);
        Assert.Single(response.Items, item => item.RequesterType == ConversationRequesterType.Registered);
        Assert.Single(response.Items, item => item.RequesterType == ConversationRequesterType.External);
    }

    [Fact]
    public async Task SummaryFilter_AwaitingReply_ReturnsOnlyAwaitingConversations()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        await ConfigureDeterministicFilterScenarioAsync(harness);

        var summaries = await harness.Service.GetSummariesAsync(harness.AdminA.Id, null, ConversationFilter.AwaitingReply);

        var summary = Assert.Single(summaries);
        Assert.Equal(ConversationRequesterType.Registered, summary.RequesterType);
        Assert.True(summary.IsAwaitingStaffReply);
    }

    [Fact]
    public async Task SummaryFilter_Unread_ReturnsOnlyUnreadConversations()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        await ConfigureDeterministicFilterScenarioAsync(harness);

        var summaries = await harness.Service.GetSummariesAsync(harness.AdminA.Id, null, ConversationFilter.Unread);

        var summary = Assert.Single(summaries);
        Assert.Equal(ConversationRequesterType.Registered, summary.RequesterType);
        Assert.True(summary.IsUnread);
    }

    [Fact]
    public async Task SummaryFilter_Pinned_ReturnsOnlyCurrentUsersPinnedConversations()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        await ConfigureDeterministicFilterScenarioAsync(harness);

        var summaries = await harness.Service.GetSummariesAsync(harness.AdminA.Id, null, ConversationFilter.Pinned);

        var summary = Assert.Single(summaries);
        Assert.Equal(ConversationRequesterType.External, summary.RequesterType);
        Assert.True(summary.IsPinned);
    }

    [Fact]
    public async Task SummaryFilter_All_ReturnsEveryConversation()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        await ConfigureDeterministicFilterScenarioAsync(harness);

        var summaries = await harness.Service.GetSummariesAsync(harness.AdminA.Id, null, ConversationFilter.All);

        Assert.Equal(2, summaries.Count);
        Assert.Contains(summaries, item => item.RequesterType == ConversationRequesterType.Registered);
        Assert.Contains(summaries, item => item.RequesterType == ConversationRequesterType.External);
    }

    [Fact]
    public async Task SummarySearch_AwaitingReply_AppliesBothConditions()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        await ConfigureDeterministicFilterScenarioAsync(harness);

        var summaries = await harness.Service.GetSummariesAsync(harness.AdminA.Id, "SUP-2026-1001", ConversationFilter.AwaitingReply);

        var summary = Assert.Single(summaries);
        Assert.Equal(ConversationRequesterType.Registered, summary.RequesterType);
        Assert.True(summary.IsAwaitingStaffReply);
    }

    [Fact]
    public async Task SummarySearch_Unread_AppliesBothConditions()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        await ConfigureDeterministicFilterScenarioAsync(harness);

        var summaries = await harness.Service.GetSummariesAsync(harness.AdminA.Id, "SUP-2026-1001", ConversationFilter.Unread);

        var summary = Assert.Single(summaries);
        Assert.Equal(ConversationRequesterType.Registered, summary.RequesterType);
        Assert.True(summary.IsUnread);
    }

    [Fact]
    public async Task SummarySearch_Pinned_AppliesBothConditions()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        await ConfigureDeterministicFilterScenarioAsync(harness);

        var summaries = await harness.Service.GetSummariesAsync(harness.AdminA.Id, "SUP-2026-1002", ConversationFilter.Pinned);

        var summary = Assert.Single(summaries);
        Assert.Equal(ConversationRequesterType.External, summary.RequesterType);
        Assert.True(summary.IsPinned);
    }

    [Fact]
    public async Task InternalNoteAfterRead_DoesNotAffectUnreadAwaitingOrPublicConversationState()
    {
        await using var harness = await ConversationHarness.CreateAsync();
        var t0 = new DateTime(2026, 8, 23, 9, 0, 0, DateTimeKind.Utc);
        var t1 = t0.AddMinutes(1);
        var t2 = t0.AddMinutes(2);
        var t3 = t0.AddMinutes(3);
        harness.ExternalTicket.CreatedAt = t0.AddMinutes(-1);
        var requesterMessage = await harness.DbContext.TicketMessages.SingleAsync(item => item.TicketId == harness.ExternalTicket.Id);
        requesterMessage.CreatedAt = t0;
        harness.DbContext.TicketMessages.AddRange(
            new TicketMessage
            {
                TicketId = harness.ExternalTicket.Id,
                SenderId = harness.Staff.Id,
                Message = "Public staff reply",
                OutboundDeliveryStatus = OutboundDeliveryStatus.Sent,
                DeliveredAt = t1,
                CreatedAt = t1
            },
            new TicketMessage
            {
                TicketId = harness.ExternalTicket.Id,
                SenderId = harness.Staff.Id,
                Message = "Private marker",
                IsInternal = true,
                CreatedAt = t3
            });
        harness.DbContext.ConversationReadStates.Add(new ConversationReadState
        {
            UserId = harness.AdminA.Id,
            RequesterType = ConversationRequesterType.External,
            RequesterId = harness.ExternalRequester.Id,
            LastReadAt = t2,
            UpdatedAt = t2
        });
        await harness.DbContext.SaveChangesAsync();

        var summary = (await harness.Service.GetSummariesAsync(harness.AdminA.Id, null))
            .Single(item => item.RequesterType == ConversationRequesterType.External);
        var detail = await harness.Service.GetDetailAsync(ConversationRequesterType.External, harness.ExternalRequester.Id, harness.AdminA.Id);
        var privateSearch = await harness.Service.GetSummariesAsync(harness.AdminA.Id, "private marker", ConversationFilter.All);

        Assert.False(summary.IsUnread);
        Assert.False(summary.IsAwaitingStaffReply);
        Assert.DoesNotContain("Private marker", summary.LastMessagePreview);
        Assert.DoesNotContain(detail!.Tickets.SelectMany(item => item.Messages), item => item.Message == "Private marker");
        Assert.Empty(privateSearch);
    }

    private static async Task ConfigureDeterministicFilterScenarioAsync(ConversationHarness harness)
    {
        var t0 = new DateTime(2026, 8, 23, 9, 0, 0, DateTimeKind.Utc);
        var registeredTicket = await harness.DbContext.Tickets.SingleAsync(item => item.TicketNumber == "SUP-2026-1001");
        var customerMessage = await harness.DbContext.TicketMessages.SingleAsync(item => item.Message == "Customer reply");
        var failedStaffMessage = await harness.DbContext.TicketMessages.SingleAsync(item => item.Message == "Support response");
        var internalStaffMessage = await harness.DbContext.TicketMessages.SingleAsync(item => item.Message == "Internal only");
        var duplicateMessage = await harness.DbContext.TicketMessages.SingleAsync(item => item.Message == "Same initial request");
        var externalMessage = await harness.DbContext.TicketMessages.SingleAsync(item => item.TicketId == harness.ExternalTicket.Id);
        registeredTicket.CreatedAt = t0.AddMinutes(-3);
        customerMessage.CreatedAt = t0;
        failedStaffMessage.CreatedAt = t0.AddMinutes(1);
        internalStaffMessage.CreatedAt = t0.AddMinutes(2);
        harness.RegisteredDuplicateTicket.CreatedAt = t0.AddMinutes(-2);
        duplicateMessage.CreatedAt = t0.AddMinutes(-1);
        harness.ExternalTicket.CreatedAt = t0.AddMinutes(-1);
        externalMessage.CreatedAt = t0;
        harness.DbContext.TicketMessages.Add(new TicketMessage
        {
            TicketId = harness.ExternalTicket.Id,
            SenderId = harness.Staff.Id,
            Message = "External sent reply",
            OutboundDeliveryStatus = OutboundDeliveryStatus.Sent,
            DeliveredAt = t0.AddMinutes(1),
            CreatedAt = t0.AddMinutes(1)
        });
        harness.DbContext.ConversationReadStates.Add(new ConversationReadState
        {
            UserId = harness.AdminA.Id,
            RequesterType = ConversationRequesterType.External,
            RequesterId = harness.ExternalRequester.Id,
            LastReadAt = t0.AddMinutes(2),
            UpdatedAt = t0.AddMinutes(2)
        });
        harness.DbContext.ConversationPins.Add(new ConversationPin
        {
            UserId = harness.AdminA.Id,
            RequesterType = ConversationRequesterType.External,
            RequesterId = harness.ExternalRequester.Id,
            CreatedAt = t0
        });
        await harness.DbContext.SaveChangesAsync();
    }

    private sealed class ConversationHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private ConversationHarness(
            SqliteConnection connection,
            AppDbContext dbContext,
            User customer,
            User staff,
            User adminA,
            User adminB,
            ExternalRequester externalRequester,
            Ticket registeredDuplicateTicket,
            Ticket externalTicket,
            ConversationService service)
        {
            _connection = connection;
            DbContext = dbContext;
            Customer = customer;
            Staff = staff;
            AdminA = adminA;
            AdminB = adminB;
            ExternalRequester = externalRequester;
            RegisteredDuplicateTicket = registeredDuplicateTicket;
            ExternalTicket = externalTicket;
            Service = service;
        }

        public AppDbContext DbContext { get; }
        public User Customer { get; }
        public User Staff { get; }
        public User AdminA { get; }
        public User AdminB { get; }
        public ExternalRequester ExternalRequester { get; }
        public Ticket RegisteredDuplicateTicket { get; }
        public Ticket ExternalTicket { get; }
        public ConversationService Service { get; }

        public static async Task<ConversationHarness> CreateAsync(TimeProvider? timeProvider = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var dbContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options);
            await dbContext.Database.EnsureCreatedAsync();

            var customer = new User
            {
                FirstName = "Kayıtlı",
                LastName = "İstek Sahibi",
                Email = "shared@example.test",
                PasswordHash = "hash",
                Role = UserRole.Customer,
                IsActive = true
            };
            var staff = new User
            {
                FirstName = "Support",
                LastName = "Agent",
                Email = "staff@example.test",
                PasswordHash = "hash",
                Role = UserRole.TechnicalStaff,
                IsActive = true
            };
            var adminA = new User
            {
                FirstName = "Admin",
                LastName = "One",
                Email = "admin-one@example.test",
                PasswordHash = "hash",
                Role = UserRole.Admin,
                IsActive = true
            };
            var adminB = new User
            {
                FirstName = "Admin",
                LastName = "Two",
                Email = "admin-two@example.test",
                PasswordHash = "hash",
                Role = UserRole.Admin,
                IsActive = true
            };
            var externalRequester = new ExternalRequester
            {
                DisplayName = "External requester",
                Email = "shared@example.test",
                NormalizedEmail = "shared@example.test"
            };
            dbContext.AddRange(customer, staff, adminA, adminB, externalRequester);
            await dbContext.SaveChangesAsync();

            var now = DateTime.UtcNow;
            var registeredTicket = new Ticket
            {
                TicketNumber = "SUP-2026-1001",
                CustomerId = customer.Id,
                Title = "Kayıtlı erişim",
                Description = "First registered request",
                Status = TicketStatus.New,
                Priority = TicketPriority.Normal,
                CreatedAt = now.AddMinutes(-20),
                UpdatedAt = now.AddMinutes(-20)
            };
            var registeredDuplicateTicket = new Ticket
            {
                TicketNumber = "SUP-2026-1003",
                CustomerId = customer.Id,
                Title = "Kayıtlı tekrar",
                Description = "Same initial request",
                Status = TicketStatus.InProgress,
                Priority = TicketPriority.High,
                CreatedAt = now.AddMinutes(-10),
                UpdatedAt = now.AddMinutes(-10)
            };
            var externalTicket = new Ticket
            {
                TicketNumber = "SUP-2026-1002",
                ExternalRequesterId = externalRequester.Id,
                Title = "Dış sistem bağlantısı",
                Description = "External initial request",
                Status = TicketStatus.New,
                Priority = TicketPriority.Normal,
                CreatedAt = now.AddMinutes(-5),
                UpdatedAt = now.AddMinutes(-5)
            };
            dbContext.AddRange(registeredTicket, registeredDuplicateTicket, externalTicket);
            await dbContext.SaveChangesAsync();

            dbContext.TicketMessages.AddRange(
                new TicketMessage
                {
                    TicketId = registeredTicket.Id,
                    SenderId = customer.Id,
                    Message = "Customer reply",
                    CreatedAt = now.AddMinutes(-15)
                },
                new TicketMessage
                {
                    TicketId = registeredTicket.Id,
                    SenderId = staff.Id,
                    Message = "Support response",
                    OutboundDeliveryStatus = OutboundDeliveryStatus.Failed,
                    OutboundDeliveryError = "Delivery failed.",
                    CreatedAt = now.AddMinutes(-12)
                },
                new TicketMessage
                {
                    TicketId = registeredTicket.Id,
                    SenderId = staff.Id,
                    Message = "Internal only",
                    IsInternal = true,
                    CreatedAt = now.AddMinutes(-11)
                },
                new TicketMessage
                {
                    TicketId = registeredDuplicateTicket.Id,
                    SenderId = customer.Id,
                    Message = "Same initial request",
                    CreatedAt = now.AddMinutes(-9)
                },
                new TicketMessage
                {
                    TicketId = externalTicket.Id,
                    ExternalRequesterId = externalRequester.Id,
                    Message = "External reply",
                    CreatedAt = now.AddMinutes(-4)
                });
            dbContext.Attachments.Add(new Attachment
            {
                TicketId = externalTicket.Id,
                OriginalFileName = "ekran.png",
                StoredFileName = "stored.png",
                FilePath = "ignored",
                ContentType = "image/png",
                FileSize = 128,
                CreatedAt = now.AddMinutes(-4)
            });
            await dbContext.SaveChangesAsync();

            return new ConversationHarness(
                connection,
                dbContext,
                customer,
                staff,
                adminA,
                adminB,
                externalRequester,
                registeredDuplicateTicket,
                externalTicket,
                new ConversationService(
                    dbContext,
                    CreateSlaCalculator(timeProvider ?? TimeProvider.System),
                    new QueueAgingCalculator(timeProvider ?? TimeProvider.System)));
        }

        private static ITicketSlaCalculator CreateSlaCalculator(TimeProvider timeProvider)
        {
            var targetResolver = new SlaTargetResolver(Options.Create(new SlaSettings()));
            var lifecycle = new TicketSlaService(targetResolver);
            return new TicketSlaCalculator(lifecycle, targetResolver, timeProvider);
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
