namespace SupportFlow.Api.Models;

public class ExternalRequester
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    public ICollection<TicketMessage> TicketMessages { get; set; } = new List<TicketMessage>();
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}
