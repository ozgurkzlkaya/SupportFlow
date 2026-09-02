using SupportFlow.Api.Enums;

namespace SupportFlow.Api.DTOs.Tickets;

public sealed class TicketTimelineItemResponse
{
    public string Id { get; init; } = string.Empty;
    public TicketActivityType Kind { get; init; }
    public DateTime OccurredAt { get; init; }
    public TicketActivitySource? Source { get; init; }
    public TicketActivityVisibility Visibility { get; init; }
    public TicketTimelineActorResponse Actor { get; init; } = new();
    public string Title { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public int? RelatedMessageId { get; init; }
    public int? RelatedAttachmentId { get; init; }
    public TicketTimelineMetadataResponse? Metadata { get; init; }
    public TicketTimelineAttachmentResponse? Attachment { get; init; }
}

public sealed class TicketTimelineActorResponse
{
    public TicketActivityActorKind Kind { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public UserRole? Role { get; init; }
}

public sealed class TicketTimelineMetadataResponse
{
    public TicketStatus? OldStatus { get; init; }
    public TicketStatus? NewStatus { get; init; }
    public TicketPriority? OldPriority { get; init; }
    public TicketPriority? NewPriority { get; init; }
    public string? OldAssigneeName { get; init; }
    public string? NewAssigneeName { get; init; }
    public string? OldCategoryName { get; init; }
    public string? NewCategoryName { get; init; }
    public IReadOnlyList<string>? AddedTags { get; init; }
    public IReadOnlyList<string>? RemovedTags { get; init; }
}

public sealed class TicketTimelineAttachmentResponse
{
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSize { get; init; }
}
