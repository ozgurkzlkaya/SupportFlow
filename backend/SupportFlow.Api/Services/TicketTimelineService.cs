using Microsoft.EntityFrameworkCore;
using SupportFlow.Api.Data;
using SupportFlow.Api.DTOs.Common;
using SupportFlow.Api.DTOs.Tickets;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Services.TicketActivities;

namespace SupportFlow.Api.Services;

public sealed class TicketTimelineService
{
    private const int ActivityOriginOrder = 0;
    private const int LegacyStatusOriginOrder = 1;
    private const int LegacyMessageOriginOrder = 2;
    private const int LegacyAttachmentOriginOrder = 3;
    private const int SyntheticCreationOriginOrder = 4;

    private readonly AppDbContext _dbContext;
    private readonly ITicketActivityMetadataCodec _metadataCodec;
    private readonly ILogger<TicketTimelineService> _logger;

    public TicketTimelineService(
        AppDbContext dbContext,
        ITicketActivityMetadataCodec metadataCodec,
        ILogger<TicketTimelineService> logger)
    {
        _dbContext = dbContext;
        _metadataCodec = metadataCodec;
        _logger = logger;
    }

    public async Task<TicketOperationResult<PagedResponse<TicketTimelineItemResponse>>> GetTimelineAsync(
        int ticketId,
        int currentUserId,
        UserRole currentUserRole,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (!await CanAccessTicketAsync(ticketId, currentUserId, currentUserRole, cancellationToken))
        {
            return TicketOperationResult<PagedResponse<TicketTimelineItemResponse>>.Failed(
                TicketOperationFailure.NotFound);
        }

        var rows = BuildTimelineQuery(ticketId, currentUserRole);
        var totalCount = await rows.CountAsync(cancellationToken);
        var pageRows = await rows
            .OrderByDescending(row => row.OccurredAt)
            .ThenBy(row => row.OriginOrder)
            .ThenByDescending(row => row.UnderlyingId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = pageRows
            .Select(row => CreateResponse(row, currentUserRole))
            .ToArray();

        return TicketOperationResult<PagedResponse<TicketTimelineItemResponse>>.Succeeded(
            CreatePagedResponse(items, page, pageSize, totalCount));
    }

    private IQueryable<TimelineRow> BuildTimelineQuery(int ticketId, UserRole currentUserRole)
    {
        var activities =
            from activity in _dbContext.TicketActivities.AsNoTracking()
            join attachment in _dbContext.Attachments.AsNoTracking()
                on activity.RelatedAttachmentId equals (int?)attachment.Id into attachmentGroup
            from attachment in attachmentGroup.DefaultIfEmpty()
            join message in _dbContext.TicketMessages.AsNoTracking()
                on activity.RelatedMessageId equals (int?)message.Id into messageGroup
            from message in messageGroup.DefaultIfEmpty()
            where activity.TicketId == ticketId
            select new TimelineRow
            {
                Id = "activity-" + activity.Id,
                KindName = activity.ActivityType.ToString(),
                OccurredAt = activity.OccurredAt,
                SourceName = activity.Source.ToString(),
                VisibilityName = activity.Visibility.ToString(),
                ActorKindName = activity.ActorKind.ToString(),
                ActorDisplayName = activity.ActorDisplayName,
                ActorRoleName = activity.ActorRole == null ? null : activity.ActorRole.ToString(),
                RelatedMessageId = activity.RelatedMessageId,
                RelatedAttachmentId = activity.RelatedAttachmentId,
                MetadataVersion = activity.MetadataVersion,
                MetadataJson = activity.MetadataJson,
                LegacyOldStatusName = null,
                LegacyNewStatusName = null,
                AttachmentFileName = attachment == null ? null : attachment.OriginalFileName,
                AttachmentContentType = attachment == null ? null : attachment.ContentType,
                AttachmentFileSize = attachment == null ? null : attachment.FileSize,
                IsCustomerVisible = message == null
                    ? attachment == null || attachment.MessageId == null || !attachment.Message!.IsInternal
                    : !message.IsInternal
                        && (message.SenderId == null
                            || (message.Sender!.Role != UserRole.Admin
                                && message.Sender.Role != UserRole.TechnicalStaff)
                            || message.OutboundDeliveryStatus == OutboundDeliveryStatus.Sent
                            || message.OutboundDeliveryStatus == OutboundDeliveryStatus.NotApplicable),
                OriginOrder = ActivityOriginOrder,
                UnderlyingId = activity.Id
            };

        if (currentUserRole == UserRole.Customer)
        {
            activities = activities.Where(activity =>
                activity.VisibilityName == nameof(TicketActivityVisibility.RequesterVisible)
                && activity.IsCustomerVisible);
        }

        var legacyStatus =
            from history in _dbContext.TicketStatusHistories.AsNoTracking()
            where history.TicketId == ticketId
                && !_dbContext.TicketActivities.Any(activity =>
                    activity.TicketId == ticketId
                    && activity.RelatedStatusHistoryId == history.Id)
            select new TimelineRow
            {
                Id = "status-" + history.Id,
                KindName = nameof(TicketActivityType.StatusChanged),
                OccurredAt = history.ChangedAt,
                SourceName = null,
                VisibilityName = nameof(TicketActivityVisibility.RequesterVisible),
                ActorKindName = nameof(TicketActivityActorKind.User),
                ActorDisplayName = history.ChangedByUser.FirstName + " " + history.ChangedByUser.LastName,
                ActorRoleName = history.ChangedByUser.Role.ToString(),
                RelatedMessageId = null,
                RelatedAttachmentId = null,
                MetadataVersion = 0,
                MetadataJson = null,
                LegacyOldStatusName = history.OldStatus.ToString(),
                LegacyNewStatusName = history.NewStatus.ToString(),
                AttachmentFileName = null,
                AttachmentContentType = null,
                AttachmentFileSize = null,
                IsCustomerVisible = true,
                OriginOrder = LegacyStatusOriginOrder,
                UnderlyingId = history.Id
            };

        var legacyMessages =
            from message in _dbContext.TicketMessages.AsNoTracking()
            where message.TicketId == ticketId
                && !_dbContext.TicketActivities.Any(activity =>
                    activity.TicketId == ticketId
                    && activity.RelatedMessageId == message.Id)
            select new TimelineRow
            {
                Id = "message-" + message.Id,
                KindName = message.IsInternal
                    ? nameof(TicketActivityType.InternalNoteCreated)
                    : nameof(TicketActivityType.MessageCreated),
                OccurredAt = message.CreatedAt,
                SourceName = null,
                VisibilityName = message.IsInternal
                    ? nameof(TicketActivityVisibility.Internal)
                    : nameof(TicketActivityVisibility.RequesterVisible),
                ActorKindName = message.SenderId != null
                    ? nameof(TicketActivityActorKind.User)
                    : message.ExternalRequesterId != null
                        ? nameof(TicketActivityActorKind.ExternalRequester)
                        : nameof(TicketActivityActorKind.System),
                ActorDisplayName = message.SenderId != null
                    ? message.Sender!.FirstName + " " + message.Sender.LastName
                    : message.ExternalRequesterId != null
                        ? message.ExternalRequester!.DisplayName ?? "Harici Talep Sahibi"
                        : "Sistem",
                ActorRoleName = message.SenderId != null ? message.Sender!.Role.ToString() : null,
                RelatedMessageId = message.Id,
                RelatedAttachmentId = null,
                MetadataVersion = 0,
                MetadataJson = null,
                LegacyOldStatusName = null,
                LegacyNewStatusName = null,
                AttachmentFileName = null,
                AttachmentContentType = null,
                AttachmentFileSize = null,
                IsCustomerVisible = !message.IsInternal
                    && (message.SenderId == null
                        || (message.Sender!.Role != UserRole.Admin
                            && message.Sender.Role != UserRole.TechnicalStaff)
                        || message.OutboundDeliveryStatus == OutboundDeliveryStatus.Sent
                        || message.OutboundDeliveryStatus == OutboundDeliveryStatus.NotApplicable),
                OriginOrder = LegacyMessageOriginOrder,
                UnderlyingId = message.Id
            };

        if (currentUserRole == UserRole.Customer)
        {
            legacyMessages = legacyMessages.Where(message => message.IsCustomerVisible);
        }

        var legacyAttachments =
            from attachment in _dbContext.Attachments.AsNoTracking()
            where attachment.TicketId == ticketId
                && !_dbContext.TicketActivities.Any(activity =>
                    activity.TicketId == ticketId
                    && activity.RelatedAttachmentId == attachment.Id)
            select new TimelineRow
            {
                Id = "attachment-" + attachment.Id,
                KindName = nameof(TicketActivityType.AttachmentAdded),
                OccurredAt = attachment.CreatedAt,
                SourceName = null,
                VisibilityName = attachment.MessageId != null && attachment.Message!.IsInternal
                    ? nameof(TicketActivityVisibility.Internal)
                    : nameof(TicketActivityVisibility.RequesterVisible),
                ActorKindName = attachment.UploadedByUserId != null
                    ? nameof(TicketActivityActorKind.User)
                    : attachment.UploadedByExternalRequesterId != null
                        ? nameof(TicketActivityActorKind.ExternalRequester)
                        : nameof(TicketActivityActorKind.System),
                ActorDisplayName = attachment.UploadedByUserId != null
                    ? attachment.UploadedByUser!.FirstName + " " + attachment.UploadedByUser.LastName
                    : attachment.UploadedByExternalRequesterId != null
                        ? attachment.UploadedByExternalRequester!.DisplayName ?? "Harici Talep Sahibi"
                        : "Sistem",
                ActorRoleName = attachment.UploadedByUserId != null ? attachment.UploadedByUser!.Role.ToString() : null,
                RelatedMessageId = null,
                RelatedAttachmentId = attachment.Id,
                MetadataVersion = 0,
                MetadataJson = null,
                LegacyOldStatusName = null,
                LegacyNewStatusName = null,
                AttachmentFileName = attachment.OriginalFileName,
                AttachmentContentType = attachment.ContentType,
                AttachmentFileSize = attachment.FileSize,
                IsCustomerVisible = attachment.MessageId == null || !attachment.Message!.IsInternal,
                OriginOrder = LegacyAttachmentOriginOrder,
                UnderlyingId = attachment.Id
            };

        if (currentUserRole == UserRole.Customer)
        {
            legacyAttachments = legacyAttachments.Where(attachment => attachment.IsCustomerVisible);
        }

        var syntheticCreation =
            from ticket in _dbContext.Tickets.AsNoTracking()
            where ticket.Id == ticketId
                && !_dbContext.TicketActivities.Any(activity =>
                    activity.TicketId == ticketId
                    && activity.ActivityType == TicketActivityType.TicketCreated)
            select new TimelineRow
            {
                Id = "ticket-created-" + ticket.Id,
                KindName = nameof(TicketActivityType.TicketCreated),
                OccurredAt = ticket.CreatedAt,
                SourceName = null,
                VisibilityName = nameof(TicketActivityVisibility.RequesterVisible),
                ActorKindName = ticket.CustomerId != null
                    ? nameof(TicketActivityActorKind.User)
                    : ticket.ExternalRequesterId != null
                        ? nameof(TicketActivityActorKind.ExternalRequester)
                        : nameof(TicketActivityActorKind.System),
                ActorDisplayName = ticket.CustomerId != null
                    ? ticket.Customer!.FirstName + " " + ticket.Customer.LastName
                    : ticket.ExternalRequesterId != null
                        ? ticket.ExternalRequester!.DisplayName ?? "Harici Talep Sahibi"
                        : "Sistem",
                ActorRoleName = ticket.CustomerId != null ? ticket.Customer!.Role.ToString() : null,
                RelatedMessageId = null,
                RelatedAttachmentId = null,
                MetadataVersion = 0,
                MetadataJson = null,
                LegacyOldStatusName = null,
                LegacyNewStatusName = null,
                AttachmentFileName = null,
                AttachmentContentType = null,
                AttachmentFileSize = null,
                IsCustomerVisible = true,
                OriginOrder = SyntheticCreationOriginOrder,
                UnderlyingId = ticket.Id
            };

        return activities
            .Concat(legacyStatus)
            .Concat(legacyMessages)
            .Concat(legacyAttachments)
            .Concat(syntheticCreation);
    }

    private TicketTimelineItemResponse CreateResponse(TimelineRow row, UserRole currentUserRole)
    {
        var kind = ParseRequiredEnum<TicketActivityType>(row.KindName);
        var actorKind = ParseRequiredEnum<TicketActivityActorKind>(row.ActorKindName);
        var metadata = row.MetadataVersion == 0
            ? CreateLegacyMetadata(row, kind)
            : CreateActivityMetadata(row, kind, currentUserRole);

        return new TicketTimelineItemResponse
        {
            Id = row.Id,
            Kind = kind,
            OccurredAt = row.OccurredAt,
            Source = ParseOptionalEnum<TicketActivitySource>(row.SourceName),
            Visibility = ParseRequiredEnum<TicketActivityVisibility>(row.VisibilityName),
            Actor = new TicketTimelineActorResponse
            {
                Kind = actorKind,
                DisplayName = actorKind == TicketActivityActorKind.System
                    ? "Sistem"
                    : row.ActorDisplayName,
                Role = ParseOptionalEnum<UserRole>(row.ActorRoleName)
            },
            Title = GetTitle(kind),
            Summary = GetSummary(row),
            RelatedMessageId = row.RelatedMessageId,
            RelatedAttachmentId = row.RelatedAttachmentId,
            Metadata = metadata,
            Attachment = row.AttachmentFileName is null
                ? null
                : new TicketTimelineAttachmentResponse
                {
                    FileName = row.AttachmentFileName,
                    ContentType = row.AttachmentContentType ?? string.Empty,
                    FileSize = row.AttachmentFileSize ?? 0
                }
        };
    }

    private TicketTimelineMetadataResponse? CreateActivityMetadata(
        TimelineRow row,
        TicketActivityType kind,
        UserRole currentUserRole)
    {
        try
        {
            var metadata = _metadataCodec.Deserialize(kind, row.MetadataVersion, row.MetadataJson ?? "{}");
            return metadata switch
            {
                StatusChangedTicketActivityMetadata status => new TicketTimelineMetadataResponse
                {
                    OldStatus = status.OldStatus,
                    NewStatus = status.NewStatus
                },
                PriorityChangedTicketActivityMetadata priority when currentUserRole is not UserRole.Customer => new TicketTimelineMetadataResponse
                {
                    OldPriority = priority.OldPriority,
                    NewPriority = priority.NewPriority
                },
                AssignmentChangedTicketActivityMetadata assignment when currentUserRole is not UserRole.Customer => new TicketTimelineMetadataResponse
                {
                    OldAssigneeName = assignment.OldDisplayName,
                    NewAssigneeName = assignment.NewDisplayName
                },
                ClassificationChangedTicketActivityMetadata classification when currentUserRole is not UserRole.Customer => new TicketTimelineMetadataResponse
                {
                    OldCategoryName = classification.OldCategoryName,
                    NewCategoryName = classification.NewCategoryName,
                    AddedTags = classification.AddedTags.Select(tag => tag.Name).ToArray(),
                    RemovedTags = classification.RemovedTags.Select(tag => tag.Name).ToArray()
                },
                _ => null
            };
        }
        catch (ArgumentException exception)
        {
            _logger.LogWarning(
                exception,
                "Ticket activity metadata could not be projected safely. Timeline item: {TimelineItemId}",
                row.Id);
            return null;
        }
    }

    private static TicketTimelineMetadataResponse? CreateLegacyMetadata(TimelineRow row, TicketActivityType kind)
    {
        if (kind != TicketActivityType.StatusChanged
            || !Enum.TryParse<TicketStatus>(row.LegacyOldStatusName, out var oldStatus)
            || !Enum.TryParse<TicketStatus>(row.LegacyNewStatusName, out var newStatus))
        {
            return null;
        }

        return new TicketTimelineMetadataResponse
        {
            OldStatus = oldStatus,
            NewStatus = newStatus
        };
    }

    private static string GetTitle(TicketActivityType kind) => kind switch
    {
        TicketActivityType.TicketCreated => "Talep oluşturuldu",
        TicketActivityType.StatusChanged => "Durum güncellendi",
        TicketActivityType.PriorityChanged => "Öncelik güncellendi",
        TicketActivityType.AssignmentChanged => "Atama güncellendi",
        TicketActivityType.ClassificationChanged => "Sınıflandırma güncellendi",
        TicketActivityType.MessageCreated => "Mesaj eklendi",
        TicketActivityType.InternalNoteCreated => "İç not eklendi",
        TicketActivityType.AttachmentAdded => "Dosya eklendi",
        _ => "Talep etkinliği"
    };

    private static string? GetSummary(TimelineRow row) =>
        string.Equals(row.KindName, nameof(TicketActivityType.AttachmentAdded), StringComparison.Ordinal)
            ? row.AttachmentFileName
            : null;

    private static TEnum ParseRequiredEnum<TEnum>(string value) where TEnum : struct, Enum =>
        Enum.Parse<TEnum>(value, ignoreCase: false);

    private static TEnum? ParseOptionalEnum<TEnum>(string? value) where TEnum : struct, Enum =>
        value is null ? null : ParseRequiredEnum<TEnum>(value);

    private async Task<bool> CanAccessTicketAsync(
        int ticketId,
        int currentUserId,
        UserRole currentUserRole,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.Id == ticketId);

        if (currentUserRole == UserRole.Customer)
        {
            query = query.Where(ticket => ticket.CustomerId == currentUserId);
        }

        return await query.AnyAsync(cancellationToken);
    }

    private static PagedResponse<TicketTimelineItemResponse> CreatePagedResponse(
        IReadOnlyList<TicketTimelineItemResponse> items,
        int page,
        int pageSize,
        int totalCount)
    {
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        return new PagedResponse<TicketTimelineItemResponse>
        {
            Items = items,
            Pagination = new PaginationMetadata
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                HasPreviousPage = page > 1,
                HasNextPage = page < totalPages
            }
        };
    }

    private sealed class TimelineRow
    {
        public string Id { get; init; } = string.Empty;
        public string KindName { get; init; } = string.Empty;
        public DateTime OccurredAt { get; init; }
        public string? SourceName { get; init; }
        public string VisibilityName { get; init; } = string.Empty;
        public string ActorKindName { get; init; } = string.Empty;
        public string ActorDisplayName { get; init; } = string.Empty;
        public string? ActorRoleName { get; init; }
        public int? RelatedMessageId { get; init; }
        public int? RelatedAttachmentId { get; init; }
        public int MetadataVersion { get; init; }
        public string? MetadataJson { get; init; }
        public string? LegacyOldStatusName { get; init; }
        public string? LegacyNewStatusName { get; init; }
        public string? AttachmentFileName { get; init; }
        public string? AttachmentContentType { get; init; }
        public long? AttachmentFileSize { get; init; }
        public bool IsCustomerVisible { get; init; }
        public int OriginOrder { get; init; }
        public int UnderlyingId { get; init; }
    }
}
