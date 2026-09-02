using SupportFlow.Api.Enums;

namespace SupportFlow.Api.DTOs.Tickets;

public class TicketMessageResponse
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int? SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public UserRole SenderRole { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
    public OutboundDeliveryStatus DeliveryStatus { get; set; }
    public int DeliveryAttemptCount { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? LastDeliveryAttemptAt { get; set; }
    public string? SafeDeliveryError { get; set; }
    public DateTime CreatedAt { get; set; }
}
