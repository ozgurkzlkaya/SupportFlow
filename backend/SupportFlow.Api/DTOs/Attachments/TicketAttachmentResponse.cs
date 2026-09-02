namespace SupportFlow.Api.DTOs.Attachments;

public class TicketAttachmentResponse
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int? MessageId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public bool IsImage { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? UploadedByUserId { get; set; }
    public string UploadedByUserName { get; set; } = string.Empty;
    public int? UploadedByExternalRequesterId { get; set; }
    public string UploadedByExternalRequesterName { get; set; } = string.Empty;
}
