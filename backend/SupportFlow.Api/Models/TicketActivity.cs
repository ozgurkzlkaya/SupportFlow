using SupportFlow.Api.Enums;

namespace SupportFlow.Api.Models;

public sealed class TicketActivity
{
    private TicketActivity()
    {
    }

    internal TicketActivity(
        int ticketId,
        TicketActivityType activityType,
        TicketActivitySource source,
        TicketActivityVisibility visibility,
        DateTime occurredAt,
        DateTime recordedAt,
        TicketActivityActorKind actorKind,
        int? actorUserId,
        int? externalRequesterId,
        string actorDisplayName,
        UserRole? actorRole,
        int? relatedStatusHistoryId,
        int? relatedMessageId,
        int? relatedAttachmentId,
        int metadataVersion,
        string metadataJson)
    {
        TicketId = ticketId;
        ActivityType = activityType;
        Source = source;
        Visibility = visibility;
        OccurredAt = occurredAt;
        RecordedAt = recordedAt;
        ActorKind = actorKind;
        ActorUserId = actorUserId;
        ExternalRequesterId = externalRequesterId;
        ActorDisplayName = actorDisplayName;
        ActorRole = actorRole;
        RelatedStatusHistoryId = relatedStatusHistoryId;
        RelatedMessageId = relatedMessageId;
        RelatedAttachmentId = relatedAttachmentId;
        MetadataVersion = metadataVersion;
        MetadataJson = metadataJson;
    }

    public int Id { get; private set; }
    public int TicketId { get; private set; }
    public TicketActivityType ActivityType { get; private set; }
    public TicketActivitySource Source { get; private set; }
    public TicketActivityVisibility Visibility { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public DateTime RecordedAt { get; private set; }
    public TicketActivityActorKind ActorKind { get; private set; }
    public int? ActorUserId { get; private set; }
    public int? ExternalRequesterId { get; private set; }
    public string ActorDisplayName { get; private set; } = string.Empty;
    public UserRole? ActorRole { get; private set; }
    public int? RelatedStatusHistoryId { get; private set; }
    public int? RelatedMessageId { get; private set; }
    public int? RelatedAttachmentId { get; private set; }
    public int MetadataVersion { get; private set; }
    public string MetadataJson { get; private set; } = string.Empty;

    public Ticket Ticket { get; private set; } = null!;
    public User? ActorUser { get; private set; }
    public ExternalRequester? ExternalRequester { get; private set; }
    public TicketStatusHistory? RelatedStatusHistory { get; private set; }
    public TicketMessage? RelatedMessage { get; private set; }
    public Attachment? RelatedAttachment { get; private set; }
}
