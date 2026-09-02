using SupportFlow.Api.Enums;

namespace SupportFlow.Api.Models;

public class TicketMessage
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int? SenderId { get; set; }
    public int? ExternalRequesterId { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
    public OutboundDeliveryStatus OutboundDeliveryStatus { get; set; } = OutboundDeliveryStatus.NotApplicable;
    public string? OutboundDeliveryError { get; set; }
    public DateTime? LastDeliveryAttemptAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public int DeliveryAttemptCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Ticket Ticket { get; set; } = null!;
    public User? Sender { get; set; }
    public ExternalRequester? ExternalRequester { get; set; }
}
