using SupportFlow.Api.DTOs.Conversations;

namespace SupportFlow.Api.Models;

public class ConversationPin
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public ConversationRequesterType RequesterType { get; set; }
    public int RequesterId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
