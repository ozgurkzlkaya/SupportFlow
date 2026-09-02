namespace SupportFlow.Api.Models;

public class Attachment
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int? MessageId { get; set; }
    public int? UploadedByUserId { get; set; }
    public int? UploadedByExternalRequesterId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Ticket Ticket { get; set; } = null!;
    public TicketMessage? Message { get; set; }
    public User? UploadedByUser { get; set; }
    public ExternalRequester? UploadedByExternalRequester { get; set; }
}
