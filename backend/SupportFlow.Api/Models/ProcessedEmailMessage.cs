namespace SupportFlow.Api.Models;

public class ProcessedEmailMessage
{
    public int Id { get; set; }
    public int TicketEmailThreadId { get; set; }
    public string GmailMessageId { get; set; } = string.Empty;
    public string? InternetMessageId { get; set; }
    public int? TicketMessageId { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

    public TicketEmailThread TicketEmailThread { get; set; } = null!;
    public TicketMessage? TicketMessage { get; set; }
}
