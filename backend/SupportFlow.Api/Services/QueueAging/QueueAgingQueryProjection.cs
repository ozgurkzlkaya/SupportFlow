using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;

namespace SupportFlow.Api.Services.QueueAging;

public sealed record QueueAgingQueryProjection(
    int TicketId,
    bool IsAwaitingStaffReply,
    DateTime? AwaitingStaffSince);

public enum QueueAgingProjectionSort
{
    None,
    NewestTicket,
    OldestTicket,
    LongestWaiting,
    ShortestWaiting
}

public static class QueueAgingQueryProjectionExtensions
{
    private static readonly TicketStatus[] ActiveStatuses =
    [
        TicketStatus.New,
        TicketStatus.Assigned,
        TicketStatus.InProgress,
        TicketStatus.WaitingForCustomer,
        TicketStatus.Reopened
    ];

    /// <summary>
    /// Projects the QueueAgingCalculator rules into SQL for scalable list queries.
    /// The calculator remains the semantic authority; this query is an optimized representation
    /// whose parity is guarded by relational tests.
    /// </summary>
    public static IQueryable<QueueAgingQueryProjection> ProjectQueueAging(
        this IQueryable<Ticket> tickets,
        bool awaitingOnly = false,
        DateTime? awaitingBeforeOrAt = null,
        QueueAgingProjectionSort sort = QueueAgingProjectionSort.None)
    {
        var withLastSuccessfulStaffResponse = tickets.Select(ticket => new
        {
            Ticket = ticket,
            LastSuccessfulStaffResponseAt = ticket.Messages
                .Where(message => !message.IsInternal
                    && message.Sender != null
                    && (message.Sender.Role == UserRole.Admin
                        || message.Sender.Role == UserRole.TechnicalStaff)
                    && (message.OutboundDeliveryStatus == OutboundDeliveryStatus.Sent
                        || message.OutboundDeliveryStatus == OutboundDeliveryStatus.NotApplicable))
                .Select(message => message.OutboundDeliveryStatus == OutboundDeliveryStatus.Sent
                    ? (DateTime?)(message.DeliveredAt ?? message.CreatedAt)
                    : message.CreatedAt)
                .Max()
        });

        var withRequesterAnchors = withLastSuccessfulStaffResponse.Select(item => new
        {
            item.Ticket,
            item.LastSuccessfulStaffResponseAt,
            InitialRequestCandidate = !item.LastSuccessfulStaffResponseAt.HasValue
                || item.Ticket.CreatedAt > item.LastSuccessfulStaffResponseAt.Value
                    ? (DateTime?)item.Ticket.CreatedAt
                    : null,
            RequesterMessageCandidate = item.Ticket.Messages
                .Where(message => !message.IsInternal
                    && (message.ExternalRequesterId.HasValue
                        || message.Sender != null && message.Sender.Role == UserRole.Customer)
                    && (!item.LastSuccessfulStaffResponseAt.HasValue
                        || message.CreatedAt > item.LastSuccessfulStaffResponseAt.Value))
                .Select(message => (DateTime?)message.CreatedAt)
                .Min()
        });

        var withAwaitingAnchor = withRequesterAnchors.Select(item => new
        {
            item.Ticket,
            item.LastSuccessfulStaffResponseAt,
            AwaitingStaffSince = item.InitialRequestCandidate.HasValue
                ? item.RequesterMessageCandidate.HasValue
                    ? item.InitialRequestCandidate.Value < item.RequesterMessageCandidate.Value
                        ? item.InitialRequestCandidate
                        : item.RequesterMessageCandidate
                    : item.InitialRequestCandidate
                : item.RequesterMessageCandidate
        });

        var filtered = withAwaitingAnchor;
        if (awaitingOnly)
        {
            filtered = filtered.Where(item => ActiveStatuses.Contains(item.Ticket.Status)
                && item.AwaitingStaffSince.HasValue);
        }

        if (awaitingBeforeOrAt.HasValue)
        {
            filtered = filtered.Where(item => ActiveStatuses.Contains(item.Ticket.Status)
                && item.AwaitingStaffSince.HasValue
                && item.AwaitingStaffSince.Value <= awaitingBeforeOrAt.Value);
        }

        if (sort == QueueAgingProjectionSort.LongestWaiting)
        {
            return filtered
                .OrderBy(item => !ActiveStatuses.Contains(item.Ticket.Status)
                    || !item.AwaitingStaffSince.HasValue)
                .ThenBy(item => item.AwaitingStaffSince)
                .ThenBy(item => item.Ticket.CreatedAt)
                .ThenBy(item => item.Ticket.Id)
                .Select(item => new QueueAgingQueryProjection(
                    item.Ticket.Id,
                    ActiveStatuses.Contains(item.Ticket.Status)
                        && (!item.LastSuccessfulStaffResponseAt.HasValue
                            || item.Ticket.CreatedAt > item.LastSuccessfulStaffResponseAt.Value
                            || item.Ticket.Messages.Any(message => !message.IsInternal
                                && (message.ExternalRequesterId.HasValue
                                    || message.Sender != null && message.Sender.Role == UserRole.Customer)
                                && message.CreatedAt > item.LastSuccessfulStaffResponseAt.Value)),
                    ActiveStatuses.Contains(item.Ticket.Status) ? item.AwaitingStaffSince : null));
        }

        if (sort == QueueAgingProjectionSort.ShortestWaiting)
        {
            return filtered
                .OrderBy(item => !ActiveStatuses.Contains(item.Ticket.Status)
                    || !item.AwaitingStaffSince.HasValue)
                .ThenByDescending(item => item.AwaitingStaffSince)
                .ThenBy(item => item.Ticket.CreatedAt)
                .ThenBy(item => item.Ticket.Id)
                .Select(item => new QueueAgingQueryProjection(
                    item.Ticket.Id,
                    ActiveStatuses.Contains(item.Ticket.Status)
                        && (!item.LastSuccessfulStaffResponseAt.HasValue
                            || item.Ticket.CreatedAt > item.LastSuccessfulStaffResponseAt.Value
                            || item.Ticket.Messages.Any(message => !message.IsInternal
                                && (message.ExternalRequesterId.HasValue
                                    || message.Sender != null && message.Sender.Role == UserRole.Customer)
                                && message.CreatedAt > item.LastSuccessfulStaffResponseAt.Value)),
                    ActiveStatuses.Contains(item.Ticket.Status) ? item.AwaitingStaffSince : null));
        }

        if (sort == QueueAgingProjectionSort.OldestTicket)
        {
            return filtered
                .OrderBy(item => item.Ticket.CreatedAt)
                .ThenBy(item => item.Ticket.Id)
                .Select(item => new QueueAgingQueryProjection(
                    item.Ticket.Id,
                    ActiveStatuses.Contains(item.Ticket.Status)
                        && (!item.LastSuccessfulStaffResponseAt.HasValue
                            || item.Ticket.CreatedAt > item.LastSuccessfulStaffResponseAt.Value
                            || item.Ticket.Messages.Any(message => !message.IsInternal
                                && (message.ExternalRequesterId.HasValue
                                    || message.Sender != null && message.Sender.Role == UserRole.Customer)
                                && message.CreatedAt > item.LastSuccessfulStaffResponseAt.Value)),
                    ActiveStatuses.Contains(item.Ticket.Status) ? item.AwaitingStaffSince : null));
        }

        if (sort == QueueAgingProjectionSort.NewestTicket)
        {
            return filtered
                .OrderByDescending(item => item.Ticket.CreatedAt)
                .ThenBy(item => item.Ticket.Id)
                .Select(item => new QueueAgingQueryProjection(
                    item.Ticket.Id,
                    ActiveStatuses.Contains(item.Ticket.Status)
                        && (!item.LastSuccessfulStaffResponseAt.HasValue
                            || item.Ticket.CreatedAt > item.LastSuccessfulStaffResponseAt.Value
                            || item.Ticket.Messages.Any(message => !message.IsInternal
                                && (message.ExternalRequesterId.HasValue
                                    || message.Sender != null && message.Sender.Role == UserRole.Customer)
                                && message.CreatedAt > item.LastSuccessfulStaffResponseAt.Value)),
                    ActiveStatuses.Contains(item.Ticket.Status) ? item.AwaitingStaffSince : null));
        }

        return filtered.Select(item => new QueueAgingQueryProjection(
            item.Ticket.Id,
            ActiveStatuses.Contains(item.Ticket.Status)
                && (!item.LastSuccessfulStaffResponseAt.HasValue
                    || item.Ticket.CreatedAt > item.LastSuccessfulStaffResponseAt.Value
                    || item.Ticket.Messages.Any(message => !message.IsInternal
                        && (message.ExternalRequesterId.HasValue
                            || message.Sender != null && message.Sender.Role == UserRole.Customer)
                        && message.CreatedAt > item.LastSuccessfulStaffResponseAt.Value)),
            ActiveStatuses.Contains(item.Ticket.Status) ? item.AwaitingStaffSince : null));
    }
}
