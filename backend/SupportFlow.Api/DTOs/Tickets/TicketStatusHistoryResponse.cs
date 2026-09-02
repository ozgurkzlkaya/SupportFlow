using SupportFlow.Api.Enums;

namespace SupportFlow.Api.DTOs.Tickets;

public class TicketStatusHistoryResponse
{
    public int Id { get; set; }
    public TicketStatus OldStatus { get; set; }
    public TicketStatus NewStatus { get; set; }
    public int ChangedByUserId { get; set; }
    public string ChangedByUserName { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime ChangedAt { get; set; }
}
