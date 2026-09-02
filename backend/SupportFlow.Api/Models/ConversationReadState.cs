using SupportFlow.Api.DTOs.Conversations;

namespace SupportFlow.Api.Models;

public sealed class ConversationReadState
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public ConversationRequesterType RequesterType { get; set; }
    public int RequesterId { get; set; }
    public DateTime LastReadAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
