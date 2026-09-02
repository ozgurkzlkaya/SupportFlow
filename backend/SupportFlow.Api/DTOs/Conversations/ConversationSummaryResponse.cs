using System.Text.Json.Serialization;

namespace SupportFlow.Api.DTOs.Conversations;

public sealed class ConversationSummaryResponse
{
    public ConversationRequesterType RequesterType { get; init; }
    public int RequesterId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public int TicketCount { get; init; }
    public int OpenTicketCount { get; init; }
    public DateTime LastActivityAt { get; init; }
    public string LastMessagePreview { get; init; } = string.Empty;
    public int LatestTicketId { get; init; }
    public string LatestTicketNumber { get; init; } = string.Empty;
    public string LatestTicketTitle { get; init; } = string.Empty;
    public bool IsPinned { get; set; }
    public bool IsAwaitingStaffReply { get; set; }
    public int AwaitingReplyCount { get; set; }
    public bool IsUnread { get; set; }
    public DateTime? LastRequesterActivityAt { get; set; }
    public DateTime? OldestAwaitingSince { get; set; }
    public int? OldestAwaitingMinutes { get; set; }
    public int SlaBreachedTicketCount { get; set; }
    public int SlaDueSoonTicketCount { get; set; }

    [JsonIgnore]
    public IReadOnlyList<string> SearchTerms { get; set; } = [];
}
