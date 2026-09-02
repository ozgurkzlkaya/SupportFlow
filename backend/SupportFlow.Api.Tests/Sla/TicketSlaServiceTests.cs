using Microsoft.Extensions.Options;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;
using SupportFlow.Api.Services.Sla;
using SupportFlow.Api.Settings;

namespace SupportFlow.Api.Tests.Sla;

public sealed class TicketSlaServiceTests
{
    private static readonly DateTime CreatedAt = new(2026, 8, 23, 8, 55, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(TicketPriority.Low, 1440, 7200)]
    [InlineData(TicketPriority.Normal, 480, 2880)]
    [InlineData(TicketPriority.High, 120, 960)]
    [InlineData(TicketPriority.Critical, 30, 240)]
    public void InitializeNewTicket_SetsConfiguredDeadlineTargets(
        TicketPriority priority,
        int firstResponseMinutes,
        int resolutionMinutes)
    {
        var ticket = CreateTicket(priority);

        CreateService().InitializeNewTicket(ticket);

        Assert.Equal(CreatedAt, ticket.ResolutionCycleStartedAt);
        Assert.Equal(CreatedAt.AddMinutes(firstResponseMinutes), ticket.FirstResponseDueAt);
        Assert.Equal(CreatedAt.AddMinutes(resolutionMinutes), ticket.ResolutionDueAt);
        Assert.Null(ticket.FirstRespondedAt);
        Assert.Null(ticket.ResolvedAt);
    }

    [Fact]
    public void InitializeNewTicket_RegisteredGmailTicketUsesGmailCreatedAt()
    {
        var receivedAt = new DateTime(2026, 8, 23, 9, 45, 0, DateTimeKind.Utc);
        var ticket = CreateTicket(TicketPriority.Normal, receivedAt);
        ticket.CustomerId = 10;

        CreateService().InitializeNewTicket(ticket);

        Assert.Equal(receivedAt, ticket.ResolutionCycleStartedAt);
        Assert.Equal(receivedAt.AddHours(8), ticket.FirstResponseDueAt);
        Assert.Equal(receivedAt.AddHours(48), ticket.ResolutionDueAt);
    }

    [Fact]
    public void InitializeNewTicket_ExternalRequesterGmailTicketUsesGmailCreatedAt()
    {
        var receivedAt = new DateTime(2026, 8, 23, 10, 30, 0, DateTimeKind.Utc);
        var ticket = CreateTicket(TicketPriority.Normal, receivedAt);
        ticket.ExternalRequesterId = 20;

        CreateService().InitializeNewTicket(ticket);

        Assert.Equal(receivedAt, ticket.ResolutionCycleStartedAt);
        Assert.Equal(receivedAt.AddHours(8), ticket.FirstResponseDueAt);
        Assert.Equal(receivedAt.AddHours(48), ticket.ResolutionDueAt);
    }

    [Fact]
    public void InitializeNewTicket_IsIdempotent()
    {
        var service = CreateService();
        var ticket = CreateTicket(TicketPriority.Normal);
        service.InitializeNewTicket(ticket);
        var firstResponseDueAt = ticket.FirstResponseDueAt;
        var resolutionDueAt = ticket.ResolutionDueAt;

        ticket.Priority = TicketPriority.Critical;
        service.InitializeNewTicket(ticket);

        Assert.Equal(firstResponseDueAt, ticket.FirstResponseDueAt);
        Assert.Equal(resolutionDueAt, ticket.ResolutionDueAt);
    }

    [Fact]
    public void SentFirstStaffReply_RecordsDeliveredAt()
    {
        var service = CreateTrackedServiceAndTicket(out var ticket);
        var deliveredAt = CreatedAt.AddMinutes(15);

        service.RecordFirstSuccessfulStaffResponse(
            ticket,
            StaffMessage(OutboundDeliveryStatus.Sent, CreatedAt.AddMinutes(10), deliveredAt),
            UserRole.TechnicalStaff);

        Assert.Equal(deliveredAt, ticket.FirstRespondedAt);
    }

    [Fact]
    public void SentFirstStaffReply_WithoutDeliveredAtUsesMessageCreatedAt()
    {
        var service = CreateTrackedServiceAndTicket(out var ticket);
        var messageAt = CreatedAt.AddMinutes(10);

        service.RecordFirstSuccessfulStaffResponse(
            ticket,
            StaffMessage(OutboundDeliveryStatus.Sent, messageAt),
            UserRole.Admin);

        Assert.Equal(messageAt, ticket.FirstRespondedAt);
    }

    [Fact]
    public void LaterSentReply_DoesNotOverwriteFirstResponse()
    {
        var service = CreateTrackedServiceAndTicket(out var ticket);
        var firstAt = CreatedAt.AddMinutes(10);
        service.RecordFirstSuccessfulStaffResponse(
            ticket,
            StaffMessage(OutboundDeliveryStatus.Sent, firstAt, firstAt),
            UserRole.TechnicalStaff);

        service.RecordFirstSuccessfulStaffResponse(
            ticket,
            StaffMessage(OutboundDeliveryStatus.Sent, firstAt.AddMinutes(10), firstAt.AddMinutes(10)),
            UserRole.TechnicalStaff);

        Assert.Equal(firstAt, ticket.FirstRespondedAt);
    }

    [Theory]
    [InlineData(OutboundDeliveryStatus.Failed)]
    [InlineData(OutboundDeliveryStatus.Pending)]
    public void FailedOrPendingStaffReply_DoesNotCompleteFirstResponse(OutboundDeliveryStatus deliveryStatus)
    {
        var service = CreateTrackedServiceAndTicket(out var ticket);

        service.RecordFirstSuccessfulStaffResponse(
            ticket,
            StaffMessage(deliveryStatus, CreatedAt.AddMinutes(10)),
            UserRole.TechnicalStaff);

        Assert.Null(ticket.FirstRespondedAt);
    }

    [Fact]
    public void InternalStaffNote_DoesNotCompleteFirstResponse()
    {
        var service = CreateTrackedServiceAndTicket(out var ticket);

        service.RecordFirstSuccessfulStaffResponse(
            ticket,
            StaffMessage(OutboundDeliveryStatus.Sent, CreatedAt.AddMinutes(10), CreatedAt.AddMinutes(10), isInternal: true),
            UserRole.TechnicalStaff);

        Assert.Null(ticket.FirstRespondedAt);
    }

    [Fact]
    public void RequesterMessage_DoesNotCompleteFirstResponse()
    {
        var service = CreateTrackedServiceAndTicket(out var ticket);

        service.RecordFirstSuccessfulStaffResponse(
            ticket,
            StaffMessage(OutboundDeliveryStatus.Sent, CreatedAt.AddMinutes(10), CreatedAt.AddMinutes(10)),
            UserRole.Customer);

        Assert.Null(ticket.FirstRespondedAt);
    }

    [Fact]
    public void LegacyNotApplicableStaffReply_OnTrackedTicketCompletesFirstResponseAtMessageTime()
    {
        var service = CreateTrackedServiceAndTicket(out var ticket);
        var messageAt = CreatedAt.AddMinutes(10);

        service.RecordFirstSuccessfulStaffResponse(
            ticket,
            StaffMessage(OutboundDeliveryStatus.NotApplicable, messageAt),
            UserRole.Admin);

        Assert.Equal(messageAt, ticket.FirstRespondedAt);
    }

    [Fact]
    public void FailedThenSentRetry_UsesSuccessfulDeliveryTimeAndCompletesExactlyOnce()
    {
        var service = CreateTrackedServiceAndTicket(out var ticket);
        var message = StaffMessage(OutboundDeliveryStatus.Failed, CreatedAt.AddMinutes(1));
        service.RecordFirstSuccessfulStaffResponse(ticket, message, UserRole.TechnicalStaff);
        var deliveredAt = CreatedAt.AddMinutes(2);
        message.OutboundDeliveryStatus = OutboundDeliveryStatus.Sent;
        message.DeliveredAt = deliveredAt;

        service.RecordFirstSuccessfulStaffResponse(ticket, message, UserRole.TechnicalStaff);
        service.RecordFirstSuccessfulStaffResponse(ticket, message, UserRole.TechnicalStaff);

        Assert.Equal(deliveredAt, ticket.FirstRespondedAt);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public void TerminalTransition_SetsResolutionCompletion(TicketStatus terminalStatus)
    {
        var service = CreateTrackedServiceAndTicket(out var ticket);
        var completedAt = CreatedAt.AddHours(1);

        service.ApplyStatusTransition(ticket, TicketStatus.InProgress, terminalStatus, completedAt);

        Assert.Equal(completedAt, ticket.ResolvedAt);
    }

    [Fact]
    public void ResolvedThenClosed_PreservesFirstResolutionCompletion()
    {
        var service = CreateTrackedServiceAndTicket(out var ticket);
        var resolvedAt = CreatedAt.AddHours(1);
        service.ApplyStatusTransition(ticket, TicketStatus.InProgress, TicketStatus.Resolved, resolvedAt);

        service.ApplyStatusTransition(ticket, TicketStatus.Resolved, TicketStatus.Closed, resolvedAt.AddMinutes(5));

        Assert.Equal(resolvedAt, ticket.ResolvedAt);
    }

    [Fact]
    public void Reopen_ClearsResolutionCompletion()
    {
        var service = CreateTrackedServiceAndTicket(out var ticket);
        ticket.ResolvedAt = CreatedAt.AddHours(1);

        service.ApplyStatusTransition(ticket, TicketStatus.Resolved, TicketStatus.Reopened, CreatedAt.AddHours(2));

        Assert.Null(ticket.ResolvedAt);
    }

    [Fact]
    public void Reopen_StartsNewResolutionCycleAndRecalculatesDeadline()
    {
        var service = CreateTrackedServiceAndTicket(out var ticket);
        var reopenedAt = CreatedAt.AddHours(2);
        ticket.ResolvedAt = CreatedAt.AddHours(1);

        service.ApplyStatusTransition(ticket, TicketStatus.Resolved, TicketStatus.Reopened, reopenedAt);

        Assert.Equal(reopenedAt, ticket.ResolutionCycleStartedAt);
        Assert.Equal(reopenedAt.AddHours(48), ticket.ResolutionDueAt);
    }

    [Fact]
    public void Reopen_PreservesFirstResponseFields()
    {
        var service = CreateTrackedServiceAndTicket(out var ticket);
        var firstDueAt = ticket.FirstResponseDueAt;
        var firstRespondedAt = CreatedAt.AddMinutes(30);
        ticket.FirstRespondedAt = firstRespondedAt;
        ticket.ResolvedAt = CreatedAt.AddHours(1);

        service.ApplyStatusTransition(ticket, TicketStatus.Closed, TicketStatus.Reopened, CreatedAt.AddHours(2));

        Assert.Equal(firstDueAt, ticket.FirstResponseDueAt);
        Assert.Equal(firstRespondedAt, ticket.FirstRespondedAt);
    }

    [Fact]
    public void PriorityEscalation_BeforeResponseRecalculatesFromOriginalAnchor()
    {
        var service = CreateTrackedServiceAndTicket(out var ticket);

        service.RecalculateUnfinishedDeadlines(ticket, TicketPriority.Critical);

        Assert.Equal(CreatedAt.AddMinutes(30), ticket.FirstResponseDueAt);
        Assert.Equal(CreatedAt.AddHours(4), ticket.ResolutionDueAt);
    }

    [Fact]
    public void PriorityDowngrade_ExtendsUnfinishedDeadlinesFromOriginalAnchor()
    {
        var service = CreateTrackedServiceAndTicket(out var ticket, TicketPriority.Critical);

        service.RecalculateUnfinishedDeadlines(ticket, TicketPriority.Low);

        Assert.Equal(CreatedAt.AddHours(24), ticket.FirstResponseDueAt);
        Assert.Equal(CreatedAt.AddHours(120), ticket.ResolutionDueAt);
    }

    [Fact]
    public void PriorityChange_AfterFirstResponseDoesNotRewriteFirstResponseFields()
    {
        var service = CreateTrackedServiceAndTicket(out var ticket);
        var firstDueAt = ticket.FirstResponseDueAt;
        var firstRespondedAt = CreatedAt.AddMinutes(10);
        ticket.FirstRespondedAt = firstRespondedAt;

        service.RecalculateUnfinishedDeadlines(ticket, TicketPriority.Critical);

        Assert.Equal(firstDueAt, ticket.FirstResponseDueAt);
        Assert.Equal(firstRespondedAt, ticket.FirstRespondedAt);
    }

    [Fact]
    public void PriorityChange_AfterResolutionDoesNotRewriteResolutionDeadline()
    {
        var service = CreateTrackedServiceAndTicket(out var ticket);
        var resolutionDueAt = ticket.ResolutionDueAt;
        ticket.ResolvedAt = CreatedAt.AddHours(1);

        service.RecalculateUnfinishedDeadlines(ticket, TicketPriority.Critical);

        Assert.Equal(resolutionDueAt, ticket.ResolutionDueAt);
    }

    [Fact]
    public void LegacyTicketPriorityChange_RemainsUntracked()
    {
        var ticket = CreateTicket(TicketPriority.Normal);

        CreateService().RecalculateUnfinishedDeadlines(ticket, TicketPriority.Critical);

        Assert.False(CreateService().IsTracked(ticket));
        Assert.Null(ticket.FirstResponseDueAt);
        Assert.Null(ticket.ResolutionCycleStartedAt);
        Assert.Null(ticket.ResolutionDueAt);
    }

    [Fact]
    public void LegacyTicket_NotApplicableMessageDoesNotMutateSlaFields()
    {
        var ticket = CreateTicket(TicketPriority.Normal);
        var service = CreateService();

        service.RecordFirstSuccessfulStaffResponse(
            ticket,
            StaffMessage(OutboundDeliveryStatus.NotApplicable, CreatedAt.AddMinutes(10)),
            UserRole.TechnicalStaff);

        Assert.Null(ticket.FirstRespondedAt);
    }

    [Fact]
    public void PartialLegacyTicket_IsNotTracked()
    {
        var ticket = CreateTicket(TicketPriority.Normal);
        ticket.FirstResponseDueAt = CreatedAt.AddHours(8);

        Assert.False(CreateService().IsTracked(ticket));
    }

    [Fact]
    public void GmailAutoReopen_UsesCurrentPriorityForNewResolutionCycle()
    {
        var service = CreateTrackedServiceAndTicket(out var ticket, TicketPriority.High);
        ticket.ResolvedAt = CreatedAt.AddHours(1);
        var reopenedAt = CreatedAt.AddHours(3);

        service.ApplyStatusTransition(ticket, TicketStatus.Resolved, TicketStatus.Reopened, reopenedAt);

        Assert.Equal(reopenedAt.AddHours(16), ticket.ResolutionDueAt);
        Assert.Null(ticket.ResolvedAt);
    }

    [Fact]
    public void PriorityChange_UsesCurrentResolutionCycleAnchor()
    {
        var service = CreateTrackedServiceAndTicket(out var ticket);
        var reopenedAt = CreatedAt.AddHours(2);
        ticket.ResolvedAt = CreatedAt.AddHours(1);
        service.ApplyStatusTransition(ticket, TicketStatus.Resolved, TicketStatus.Reopened, reopenedAt);

        service.RecalculateUnfinishedDeadlines(ticket, TicketPriority.High);

        Assert.Equal(reopenedAt.AddHours(16), ticket.ResolutionDueAt);
    }

    private static TicketSlaService CreateService() =>
        new(new SlaTargetResolver(Options.Create(new SlaSettings())));

    private static TicketSlaService CreateTrackedServiceAndTicket(
        out Ticket ticket,
        TicketPriority priority = TicketPriority.Normal)
    {
        var service = CreateService();
        ticket = CreateTicket(priority);
        service.InitializeNewTicket(ticket);
        return service;
    }

    private static Ticket CreateTicket(
        TicketPriority priority,
        DateTime? createdAt = null) =>
        new()
        {
            TicketNumber = "SUP-2026-000001",
            Title = "SLA test",
            Description = "Test",
            Priority = priority,
            CreatedAt = createdAt ?? CreatedAt,
            UpdatedAt = createdAt ?? CreatedAt
        };

    private static TicketMessage StaffMessage(
        OutboundDeliveryStatus deliveryStatus,
        DateTime createdAt,
        DateTime? deliveredAt = null,
        bool isInternal = false) =>
        new()
        {
            Message = "Staff reply",
            IsInternal = isInternal,
            OutboundDeliveryStatus = deliveryStatus,
            CreatedAt = createdAt,
            DeliveredAt = deliveredAt
        };
}
