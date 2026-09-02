using SupportFlow.Api.Enums;
using SupportFlow.Api.DTOs.Sla;

namespace SupportFlow.Api.DTOs.Tickets;

public class TicketDetailResponse
{
    public int Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TicketStatus Status { get; set; }
    public TicketPriority Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public int? CustomerId { get; set; }
    public int? ExternalRequesterId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string RequesterType { get; set; } = "RegisteredCustomer";
    public int? AssignedUserId { get; set; }
    public string? AssignedUserName { get; set; }
    public TicketClassificationItemResponse? Category { get; set; }
    public IReadOnlyList<TicketClassificationItemResponse> Tags { get; set; } = [];
    public bool IsAwaitingStaffReply { get; set; }
    public DateTime? AwaitingStaffSince { get; set; }
    public int? AwaitingStaffMinutes { get; set; }
    public int TicketAgeMinutes { get; set; }
    public QueueAgingBucket? AgingBucket { get; set; }
    public TicketSlaResponse Sla { get; set; } = new();
}
