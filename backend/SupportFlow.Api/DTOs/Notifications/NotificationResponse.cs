using SupportFlow.Api.Enums;

namespace SupportFlow.Api.DTOs.Notifications;

public class NotificationResponse
{
    public int Id { get; set; }
    public int? TicketId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
