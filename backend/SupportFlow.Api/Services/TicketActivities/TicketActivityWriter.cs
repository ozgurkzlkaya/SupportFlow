using SupportFlow.Api.Data;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;

namespace SupportFlow.Api.Services.TicketActivities;

public sealed record TicketActivityAppendRequest(
    int TicketId,
    TicketActivityType ActivityType,
    TicketActivitySource Source,
    TicketActivityVisibility Visibility,
    DateTime OccurredAt,
    TicketActivityActorKind ActorKind,
    int? ActorUserId,
    int? ExternalRequesterId,
    string ActorDisplayName,
    UserRole? ActorRole = null,
    int? RelatedStatusHistoryId = null,
    int? RelatedMessageId = null,
    int? RelatedAttachmentId = null,
    TicketActivityMetadata? Metadata = null);

public interface ITicketActivityWriter
{
    TicketActivity Append(TicketActivityAppendRequest request);
}

public sealed class TicketActivityWriter(
    AppDbContext dbContext,
    ITicketActivityMetadataCodec metadataCodec,
    TimeProvider timeProvider) : ITicketActivityWriter
{
    public TicketActivity Append(TicketActivityAppendRequest request)
    {
        ValidateRequest(request);

        var metadata = metadataCodec.Serialize(request.ActivityType, request.Metadata);
        var activity = new TicketActivity(
            request.TicketId,
            request.ActivityType,
            request.Source,
            request.Visibility,
            request.OccurredAt,
            timeProvider.GetUtcNow().UtcDateTime,
            request.ActorKind,
            request.ActorUserId,
            request.ExternalRequesterId,
            request.ActorDisplayName.Trim(),
            request.ActorRole,
            request.RelatedStatusHistoryId,
            request.RelatedMessageId,
            request.RelatedAttachmentId,
            metadata.Version,
            metadata.Json);

        // The caller owns SaveChanges and any surrounding transaction.
        dbContext.TicketActivities.Add(activity);
        return activity;
    }

    private static void ValidateRequest(TicketActivityAppendRequest request)
    {
        if (request.TicketId <= 0
            || !Enum.IsDefined(request.ActivityType)
            || !Enum.IsDefined(request.Source)
            || !Enum.IsDefined(request.Visibility)
            || !Enum.IsDefined(request.ActorKind))
        {
            throw new ArgumentException("The activity request contains an invalid identifier or enum value.", nameof(request));
        }

        if (request.OccurredAt == default || request.OccurredAt.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("OccurredAt must be a non-default UTC timestamp.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ActorDisplayName) || request.ActorDisplayName.Trim().Length > 201)
        {
            throw new ArgumentException("ActorDisplayName is required and must be 201 characters or fewer.", nameof(request));
        }

        ValidateRelatedId(request.RelatedStatusHistoryId, nameof(request.RelatedStatusHistoryId));
        ValidateRelatedId(request.RelatedMessageId, nameof(request.RelatedMessageId));
        ValidateRelatedId(request.RelatedAttachmentId, nameof(request.RelatedAttachmentId));
        ValidateActor(request);
    }

    private static void ValidateActor(TicketActivityAppendRequest request)
    {
        var hasValidRole = request.ActorRole.HasValue && Enum.IsDefined(request.ActorRole.Value);
        switch (request.ActorKind)
        {
            case TicketActivityActorKind.User when request.ActorUserId is > 0
                                                    && request.ExternalRequesterId is null
                                                    && hasValidRole:
                return;
            case TicketActivityActorKind.ExternalRequester when request.ActorUserId is null
                                                                 && request.ExternalRequesterId is > 0
                                                                 && request.ActorRole is null:
                return;
            case TicketActivityActorKind.System when request.ActorUserId is null
                                                       && request.ExternalRequesterId is null
                                                       && request.ActorRole is null:
                return;
            default:
                throw new ArgumentException("Actor kind, identifiers, and role are inconsistent.", nameof(request));
        }
    }

    private static void ValidateRelatedId(int? id, string parameterName)
    {
        if (id is <= 0)
        {
            throw new ArgumentException("Related identifiers must be positive when supplied.", parameterName);
        }
    }
}
