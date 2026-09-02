using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;

namespace SupportFlow.Api.Services.Sla;

public sealed class TicketSlaService(ISlaTargetResolver targetResolver) : ITicketSlaService
{
    public bool IsTracked(Ticket ticket) =>
        ticket.ResolutionCycleStartedAt.HasValue
        && ticket.FirstResponseDueAt.HasValue
        && ticket.ResolutionDueAt.HasValue;

    public void InitializeNewTicket(Ticket ticket)
    {
        // Only a clean, newly-created ticket can start SLA tracking. This prevents
        // legacy or partially populated records from being implicitly backfilled.
        if (HasAnySlaValue(ticket))
        {
            return;
        }

        var targets = targetResolver.GetTargets(ticket.Priority);
        ticket.ResolutionCycleStartedAt = ticket.CreatedAt;
        ticket.FirstResponseDueAt = ticket.CreatedAt + targets.FirstResponse;
        ticket.ResolutionDueAt = ticket.CreatedAt + targets.Resolution;
        ticket.FirstRespondedAt = null;
        ticket.ResolvedAt = null;
    }

    public void RecordFirstSuccessfulStaffResponse(
        Ticket ticket,
        TicketMessage message,
        UserRole senderRole)
    {
        if (!IsTracked(ticket)
            || ticket.FirstRespondedAt.HasValue
            || message.IsInternal
            || senderRole is not (UserRole.Admin or UserRole.TechnicalStaff))
        {
            return;
        }

        DateTime? responseAt = message.OutboundDeliveryStatus switch
        {
            OutboundDeliveryStatus.Sent => message.DeliveredAt ?? message.CreatedAt,
            // This only runs for a lifecycle event; historical rows are never
            // evaluated merely by reading a ticket.
            OutboundDeliveryStatus.NotApplicable => message.CreatedAt,
            _ => null
        };

        if (responseAt.HasValue)
        {
            ticket.FirstRespondedAt = responseAt.Value;
        }
    }

    public void ApplyStatusTransition(
        Ticket ticket,
        TicketStatus oldStatus,
        TicketStatus newStatus,
        DateTime changedAt)
    {
        if (!IsTracked(ticket))
        {
            return;
        }

        if (newStatus is TicketStatus.Resolved or TicketStatus.Closed)
        {
            // Closing a resolved ticket preserves the first completion of this cycle.
            ticket.ResolvedAt ??= changedAt;
            return;
        }

        if (oldStatus is TicketStatus.Resolved or TicketStatus.Closed
            && newStatus == TicketStatus.Reopened)
        {
            var targets = targetResolver.GetTargets(ticket.Priority);
            ticket.ResolvedAt = null;
            ticket.ResolutionCycleStartedAt = changedAt;
            ticket.ResolutionDueAt = changedAt + targets.Resolution;
        }
    }

    public void RecalculateUnfinishedDeadlines(Ticket ticket, TicketPriority newPriority)
    {
        if (!IsTracked(ticket))
        {
            return;
        }

        var targets = targetResolver.GetTargets(newPriority);

        if (!ticket.FirstRespondedAt.HasValue)
        {
            ticket.FirstResponseDueAt = ticket.CreatedAt + targets.FirstResponse;
        }

        if (!ticket.ResolvedAt.HasValue)
        {
            ticket.ResolutionDueAt = ticket.ResolutionCycleStartedAt!.Value + targets.Resolution;
        }
    }

    private static bool HasAnySlaValue(Ticket ticket) =>
        ticket.FirstResponseDueAt.HasValue
        || ticket.FirstRespondedAt.HasValue
        || ticket.ResolutionCycleStartedAt.HasValue
        || ticket.ResolutionDueAt.HasValue
        || ticket.ResolvedAt.HasValue;
}
