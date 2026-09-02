namespace SupportFlow.Api.Models;

public class TicketEmailThread
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public string GmailThreadId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Ticket Ticket { get; set; } = null!;
    public ICollection<ProcessedEmailMessage> ProcessedMessages { get; set; } =
        new List<ProcessedEmailMessage>();
}
