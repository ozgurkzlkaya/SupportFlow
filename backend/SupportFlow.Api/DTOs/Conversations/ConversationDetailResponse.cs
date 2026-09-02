using SupportFlow.Api.Enums;
using SupportFlow.Api.DTOs.Sla;

namespace SupportFlow.Api.DTOs.Conversations;

public sealed class ConversationDetailResponse
{
    public ConversationRequesterType RequesterType { get; init; }
    public int RequesterId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public int TicketCount { get; init; }
    public int OpenTicketCount { get; init; }
    public DateTime LastActivityAt { get; init; }
    public bool IsPinned { get; init; }
    public bool IsUnread { get; init; }
    public DateTime? OldestAwaitingSince { get; init; }
    public int? OldestAwaitingMinutes { get; init; }
    public IReadOnlyList<ConversationTicketResponse> Tickets { get; init; } = [];
}

public sealed class ConversationTicketResponse
{
    public int Id { get; init; }
    public string TicketNumber { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public TicketStatus Status { get; init; }
    public TicketPriority Priority { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime LastActivityAt { get; init; }
    public string? InitialMessage { get; init; }
    public IReadOnlyList<ConversationMessageResponse> Messages { get; init; } = [];
    public IReadOnlyList<ConversationAttachmentResponse> Attachments { get; init; } = [];
    public bool IsAwaitingStaffReply { get; init; }
    public DateTime? AwaitingStaffSince { get; init; }
    public int? AwaitingStaffMinutes { get; init; }
    public QueueAgingBucket? AgingBucket { get; init; }
    public TicketSlaSummaryResponse Sla { get; init; } = new();
}

public sealed class ConversationMessageResponse
{
    public int Id { get; init; }
    public string SenderName { get; init; } = string.Empty;
    public string SenderLabel { get; init; } = string.Empty;
    public bool IsSupportReply { get; init; }
    public string Message { get; init; } = string.Empty;
    public OutboundDeliveryStatus DeliveryStatus { get; init; }
    public int DeliveryAttemptCount { get; init; }
    public DateTime? DeliveredAt { get; init; }
    public DateTime? LastDeliveryAttemptAt { get; init; }
    public string? SafeDeliveryError { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class ConversationAttachmentResponse
{
    public int Id { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSize { get; init; }
}
