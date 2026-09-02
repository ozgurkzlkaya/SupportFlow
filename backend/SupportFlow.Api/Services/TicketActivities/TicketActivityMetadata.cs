using SupportFlow.Api.Enums;

namespace SupportFlow.Api.Services.TicketActivities;

public abstract record TicketActivityMetadata;

public sealed record StatusChangedTicketActivityMetadata(
    TicketStatus OldStatus,
    TicketStatus NewStatus) : TicketActivityMetadata;

public sealed record PriorityChangedTicketActivityMetadata(
    TicketPriority OldPriority,
    TicketPriority NewPriority) : TicketActivityMetadata;

public sealed record AssignmentChangedTicketActivityMetadata(
    int? OldUserId,
    int? NewUserId,
    string? OldDisplayName,
    string? NewDisplayName) : TicketActivityMetadata;

public sealed record TicketActivityTagSnapshot(int Id, string Name);

public sealed record ClassificationChangedTicketActivityMetadata(
    int? OldCategoryId,
    int? NewCategoryId,
    string? OldCategoryName,
    string? NewCategoryName,
    IReadOnlyList<TicketActivityTagSnapshot> AddedTags,
    IReadOnlyList<TicketActivityTagSnapshot> RemovedTags) : TicketActivityMetadata;
