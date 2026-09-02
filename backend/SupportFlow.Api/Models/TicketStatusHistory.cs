using SupportFlow.Api.Enums;

namespace SupportFlow.Api.Models;

public class TicketStatusHistory
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int ChangedByUserId { get; set; }
    public TicketStatus OldStatus { get; set; }
    public TicketStatus NewStatus { get; set; }
    public string? Note { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    public Ticket Ticket { get; set; } = null!;
    public User ChangedByUser { get; set; } = null!;
}
