namespace SupportFlow.Api.Models;

public sealed class TicketTagAssignment
{
    public int TicketId { get; set; }
    public int TagId { get; set; }

    public Ticket Ticket { get; set; } = null!;
    public TicketTag Tag { get; set; } = null!;
}
