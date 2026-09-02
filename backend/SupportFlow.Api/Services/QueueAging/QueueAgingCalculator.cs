using SupportFlow.Api.Enums;

namespace SupportFlow.Api.Services.QueueAging;

public sealed class QueueAgingCalculator(TimeProvider timeProvider) : IQueueAgingCalculator
{
    public QueueAgingResult Calculate(
        QueueAgingTicketInput ticket,
        IReadOnlyCollection<QueueAgingMessageInput> messages)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var ticketAge = GetCompletedMinutes(now - EnsureUtc(ticket.CreatedAt));

        if (ticket.Status is TicketStatus.Resolved or TicketStatus.Closed)
        {
            return new QueueAgingResult(false, null, null, ticketAge, null);
        }

        var lastSuccessfulStaffResponseAt = messages
            .Where(message => !message.IsInternal && message.IsPublicStaffReply)
            .Select(GetSuccessfulStaffResponseAt)
            .Where(timestamp => timestamp.HasValue)
            .Select(timestamp => timestamp!.Value)
            .DefaultIfEmpty(DateTime.MinValue)
            .Max();

        var requesterActivities = messages
            .Where(message => !message.IsInternal && message.IsRequesterActivity)
            .Select(message => EnsureUtc(message.CreatedAt))
            .Append(EnsureUtc(ticket.CreatedAt));
        var awaitingSince = requesterActivities
            .Where(timestamp => timestamp > lastSuccessfulStaffResponseAt)
            .Cast<DateTime?>()
            .DefaultIfEmpty()
            .Min();

        return CalculateFromAwaitingAnchor(ticket, awaitingSince, ticketAge, now);
    }

    public QueueAgingResult CalculateFromAwaitingAnchor(
        QueueAgingTicketInput ticket,
        DateTime? awaitingStaffSince,
        DateTime now)
    {
        var normalizedNow = EnsureUtc(now);
        var ticketAge = GetCompletedMinutes(normalizedNow - EnsureUtc(ticket.CreatedAt));
        return CalculateFromAwaitingAnchor(ticket, awaitingStaffSince, ticketAge, normalizedNow);
    }

    private static QueueAgingResult CalculateFromAwaitingAnchor(
        QueueAgingTicketInput ticket,
        DateTime? awaitingStaffSince,
        int ticketAge,
        DateTime now)
    {
        if (ticket.Status is TicketStatus.Resolved or TicketStatus.Closed || !awaitingStaffSince.HasValue)
        {
            return new QueueAgingResult(false, null, null, ticketAge, null);
        }

        var normalizedAwaitingSince = EnsureUtc(awaitingStaffSince.Value);
        var duration = now - normalizedAwaitingSince;
        var awaitingMinutes = GetCompletedMinutes(duration);
        return new QueueAgingResult(
            true,
            normalizedAwaitingSince,
            awaitingMinutes,
            ticketAge,
            GetBucket(duration));
    }

    private static DateTime? GetSuccessfulStaffResponseAt(QueueAgingMessageInput message) =>
        message.DeliveryStatus switch
        {
            OutboundDeliveryStatus.Sent => EnsureUtc(message.DeliveredAt ?? message.CreatedAt),
            OutboundDeliveryStatus.NotApplicable => EnsureUtc(message.CreatedAt),
            _ => null
        };

    private static QueueAgingBucket GetBucket(TimeSpan duration)
    {
        if (duration < TimeSpan.FromMinutes(30)) return QueueAgingBucket.New;
        if (duration < TimeSpan.FromHours(2)) return QueueAgingBucket.ShortWait;
        if (duration < TimeSpan.FromHours(8)) return QueueAgingBucket.Waiting;
        if (duration < TimeSpan.FromHours(24)) return QueueAgingBucket.LongWait;
        if (duration < TimeSpan.FromHours(72)) return QueueAgingBucket.Overdue;
        return QueueAgingBucket.CriticalWait;
    }

    private static int GetCompletedMinutes(TimeSpan duration) =>
        duration <= TimeSpan.Zero ? 0 : (int)Math.Floor(duration.TotalMinutes);

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
