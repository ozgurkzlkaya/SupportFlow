using SupportFlow.Api.Enums;

namespace SupportFlow.Api.Services.QueueAging;

public interface IQueueAgingCalculator
{
    QueueAgingResult Calculate(
        QueueAgingTicketInput ticket,
        IReadOnlyCollection<QueueAgingMessageInput> messages);

    QueueAgingResult CalculateFromAwaitingAnchor(
        QueueAgingTicketInput ticket,
        DateTime? awaitingStaffSince,
        DateTime now);
}

public sealed record QueueAgingTicketInput(TicketStatus Status, DateTime CreatedAt);

public sealed record QueueAgingMessageInput(
    bool IsRequesterActivity,
    bool IsPublicStaffReply,
    bool IsInternal,
    OutboundDeliveryStatus DeliveryStatus,
    DateTime CreatedAt,
    DateTime? DeliveredAt);

public sealed record QueueAgingResult(
    bool IsAwaitingStaffReply,
    DateTime? AwaitingStaffSince,
    int? AwaitingStaffMinutes,
    int TicketAgeMinutes,
    QueueAgingBucket? AgingBucket);
