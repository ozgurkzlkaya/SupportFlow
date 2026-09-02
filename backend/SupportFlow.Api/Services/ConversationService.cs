using Microsoft.EntityFrameworkCore;
using SupportFlow.Api.Data;
using SupportFlow.Api.DTOs.Conversations;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;
using SupportFlow.Api.Services.QueueAging;
using SupportFlow.Api.Services.Sla;

namespace SupportFlow.Api.Services;

public sealed class ConversationService
{
    private readonly AppDbContext _dbContext;
    private readonly ITicketSlaCalculator _ticketSlaCalculator;
    private readonly IQueueAgingCalculator _queueAgingCalculator;

    public ConversationService(
        AppDbContext dbContext,
        ITicketSlaCalculator ticketSlaCalculator,
        IQueueAgingCalculator queueAgingCalculator)
    {
        _dbContext = dbContext;
        _ticketSlaCalculator = ticketSlaCalculator;
        _queueAgingCalculator = queueAgingCalculator;
    }

    public async Task<IReadOnlyList<ConversationSummaryResponse>> GetSummariesAsync(
        int currentUserId,
        string? search,
        ConversationFilter filter = ConversationFilter.All,
        CancellationToken cancellationToken = default) =>
        (await GetSummaryListAsync(currentUserId, search, filter, cancellationToken)).Items;

    public async Task<ConversationSummaryListResponse> GetSummaryListAsync(
        int currentUserId,
        string? search,
        ConversationFilter filter = ConversationFilter.All,
        CancellationToken cancellationToken = default)
    {
        var pinKeys = await _dbContext.ConversationPins
            .AsNoTracking()
            .Where(pin => pin.UserId == currentUserId)
            .Select(pin => new ConversationPinKey(pin.RequesterType, pin.RequesterId))
            .ToHashSetAsync(cancellationToken);
        var readStates = await _dbContext.ConversationReadStates
            .AsNoTracking()
            .Where(state => state.UserId == currentUserId)
            .Select(state => new ConversationReadStateKey(
                state.RequesterType,
                state.RequesterId,
                state.LastReadAt))
            .ToDictionaryAsync(
                state => new ConversationPinKey(state.RequesterType, state.RequesterId),
                state => state.LastReadAt,
                cancellationToken);
        var activityByRequester = await GetActivityByRequesterAsync(cancellationToken);
        var registered = await GetRegisteredSummariesAsync(cancellationToken);
        var external = await GetExternalSummariesAsync(cancellationToken);

        var summaries = registered.Concat(external).ToList();
        foreach (var summary in summaries)
        {
            summary.IsPinned = pinKeys.Contains(
                new ConversationPinKey(summary.RequesterType, summary.RequesterId));
            var key = new ConversationPinKey(summary.RequesterType, summary.RequesterId);
            if (activityByRequester.TryGetValue(key, out var activity))
            {
                summary.SearchTerms = activity.SearchTerms;
                summary.AwaitingReplyCount = activity.AwaitingReplyCount;
                summary.IsAwaitingStaffReply = activity.AwaitingReplyCount > 0;
                summary.LastRequesterActivityAt = activity.LastRequesterActivityAt;
                summary.OldestAwaitingSince = activity.OldestAwaitingSince;
                summary.OldestAwaitingMinutes = activity.OldestAwaitingMinutes;
                summary.SlaBreachedTicketCount = activity.SlaBreachedTicketCount;
                summary.SlaDueSoonTicketCount = activity.SlaDueSoonTicketCount;
                summary.IsUnread = activity.LastRequesterActivityAt.HasValue
                    && (!readStates.TryGetValue(key, out var lastReadAt)
                        || activity.LastRequesterActivityAt.Value > lastReadAt);
            }
        }

        var counts = new ConversationFilterCountsResponse
        {
            All = summaries.Count,
            AwaitingReply = summaries.Count(item => item.IsAwaitingStaffReply),
            Unread = summaries.Count(item => item.IsUnread),
            Pinned = summaries.Count(item => item.IsPinned)
        };

        var normalizedSearch = search?.Trim();
        var items = summaries
            .Where(item => MatchesSearch(item, normalizedSearch))
            .Where(item => MatchesFilter(item, filter))
            .OrderByDescending(item => item.IsPinned)
            .ThenByDescending(item => item.LastActivityAt)
            .ThenBy(item => item.DisplayName)
            .ToList();

        return new ConversationSummaryListResponse
        {
            Items = items,
            Counts = counts
        };
    }

    public Task<IReadOnlyList<ConversationSummaryResponse>> GetSummariesAsync(
        int currentUserId,
        string? search,
        bool pinnedOnly,
        CancellationToken cancellationToken = default) =>
        GetSummariesAsync(
            currentUserId,
            search,
            pinnedOnly ? ConversationFilter.Pinned : ConversationFilter.All,
            cancellationToken);

    public async Task<ConversationDetailResponse?> GetDetailAsync(
        ConversationRequesterType requesterType,
        int requesterId,
        int currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (requesterId <= 0)
        {
            return null;
        }

        var requester = requesterType == ConversationRequesterType.Registered
            ? await _dbContext.Users
                .AsNoTracking()
                .Where(user => user.Id == requesterId && user.Role == UserRole.Customer)
                .Select(user => new RequesterProjection(
                    user.Id,
                    user.FirstName + " " + user.LastName,
                    user.Email))
                .SingleOrDefaultAsync(cancellationToken)
            : await _dbContext.ExternalRequesters
                .AsNoTracking()
                .Where(item => item.Id == requesterId)
                .Select(item => new RequesterProjection(
                    item.Id,
                    string.IsNullOrWhiteSpace(item.DisplayName) ? item.Email : item.DisplayName!,
                    item.Email))
                .SingleOrDefaultAsync(cancellationToken);

        if (requester is null)
        {
            return null;
        }

        var isPinned = await _dbContext.ConversationPins
            .AsNoTracking()
            .AnyAsync(
                pin => pin.UserId == currentUserId
                    && pin.RequesterType == requesterType
                    && pin.RequesterId == requesterId,
                cancellationToken);
        var lastReadAt = await _dbContext.ConversationReadStates
            .AsNoTracking()
            .Where(state => state.UserId == currentUserId
                && state.RequesterType == requesterType
                && state.RequesterId == requesterId)
            .Select(state => (DateTime?)state.LastReadAt)
            .SingleOrDefaultAsync(cancellationToken);

        var tickets = await _dbContext.Tickets
            .AsNoTracking()
            .Where(ticket => requesterType == ConversationRequesterType.Registered
                ? ticket.CustomerId == requesterId
                : ticket.ExternalRequesterId == requesterId)
            .Select(ticket => new TicketProjection(
                ticket.Id,
                ticket.TicketNumber,
                ticket.Title,
                ticket.Status,
                ticket.Priority,
                ticket.CreatedAt,
                ticket.Description,
                ticket.FirstResponseDueAt,
                ticket.FirstRespondedAt,
                ticket.ResolutionCycleStartedAt,
                ticket.ResolutionDueAt,
                ticket.ResolvedAt))
            .ToListAsync(cancellationToken);

        var ticketIds = tickets.Select(ticket => ticket.Id).ToArray();
        var messages = ticketIds.Length == 0
            ? []
            : await _dbContext.TicketMessages
                .AsNoTracking()
                .Where(message => ticketIds.Contains(message.TicketId) && !message.IsInternal)
                .OrderBy(message => message.CreatedAt)
                .Select(message => new MessageProjection(
                    message.TicketId,
                    message.Id,
                    message.Sender != null
                        ? message.Sender.FirstName + " " + message.Sender.LastName
                        : message.ExternalRequester != null
                            ? (string.IsNullOrWhiteSpace(message.ExternalRequester.DisplayName)
                                ? message.ExternalRequester.Email
                                : message.ExternalRequester.DisplayName!)
                            : "SupportFlow",
                    message.Sender != null
                        && (message.Sender.Role == UserRole.Admin
                            || message.Sender.Role == UserRole.TechnicalStaff)
                        ? "Destek"
                        : message.ExternalRequester != null
                            ? "Harici Müşteri"
                            : "Kayıtlı Müşteri",
                    message.Sender != null
                        && (message.Sender.Role == UserRole.Admin
                            || message.Sender.Role == UserRole.TechnicalStaff),
                    message.Message,
                    message.OutboundDeliveryStatus,
                    message.DeliveryAttemptCount,
                    message.DeliveredAt,
                    message.LastDeliveryAttemptAt,
                    message.OutboundDeliveryStatus == OutboundDeliveryStatus.Failed
                        ? message.OutboundDeliveryError
                        : null,
                    message.CreatedAt))
                .ToListAsync(cancellationToken);
        var messagesByTicket = messages
            .GroupBy(message => message.TicketId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var attachments = ticketIds.Length == 0
            ? []
            : await _dbContext.Attachments
                .AsNoTracking()
                .Where(attachment => ticketIds.Contains(attachment.TicketId))
                .Select(attachment => new AttachmentProjection(
                    attachment.TicketId,
                    attachment.Id,
                    attachment.OriginalFileName,
                    attachment.ContentType,
                    attachment.FileSize))
                .ToListAsync(cancellationToken);
        var attachmentsByTicket = attachments
            .GroupBy(attachment => attachment.TicketId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var responses = tickets.Select(ticket =>
        {
            var ticketMessages = messagesByTicket.GetValueOrDefault(ticket.Id, []);
            var lastActivityAt = ticketMessages.Count == 0
                ? ticket.CreatedAt
                : ticketMessages.Max(message => message.CreatedAt);
            var lastRequesterActivityAt = GetLastRequesterActivityAt(ticket, ticketMessages);
            var aging = CalculateQueueAging(ticket, ticketMessages);

            return new ConversationTicketResponse
            {
                Id = ticket.Id,
                TicketNumber = ticket.TicketNumber,
                Title = ticket.Title,
                Status = ticket.Status,
                Priority = ticket.Priority,
                CreatedAt = ticket.CreatedAt,
                LastActivityAt = lastActivityAt,
                IsAwaitingStaffReply = aging.IsAwaitingStaffReply,
                AwaitingStaffSince = aging.AwaitingStaffSince,
                AwaitingStaffMinutes = aging.AwaitingStaffMinutes,
                AgingBucket = aging.AgingBucket,
                Sla = _ticketSlaCalculator.CalculateSummary(ToSlaTicket(ticket)),
                InitialMessage = HasMatchingPublicMessage(ticket.Description, ticketMessages)
                    ? null
                    : ticket.Description,
                Messages = ticketMessages.Select(message => new ConversationMessageResponse
                {
                    Id = message.Id,
                    SenderName = message.SenderName,
                    SenderLabel = message.SenderLabel,
                    IsSupportReply = message.IsSupportReply,
                    Message = message.Message,
                    DeliveryStatus = message.DeliveryStatus,
                    DeliveryAttemptCount = message.DeliveryAttemptCount,
                    DeliveredAt = message.DeliveredAt,
                    LastDeliveryAttemptAt = message.LastDeliveryAttemptAt,
                    SafeDeliveryError = message.SafeDeliveryError,
                    CreatedAt = message.CreatedAt
                }).ToList(),
                Attachments = attachmentsByTicket.GetValueOrDefault(ticket.Id, [])
                    .Select(attachment => new ConversationAttachmentResponse
                    {
                        Id = attachment.Id,
                        FileName = attachment.FileName,
                        ContentType = attachment.ContentType,
                        FileSize = attachment.FileSize
                    })
                    .ToList()
            };
        })
            .OrderByDescending(ticket => ticket.LastActivityAt)
            .ToList();

        return new ConversationDetailResponse
        {
            RequesterType = requesterType,
            RequesterId = requester.Id,
            DisplayName = requester.DisplayName,
            Email = requester.Email,
            TicketCount = responses.Count,
            OpenTicketCount = responses.Count(ticket => IsOpen(ticket.Status)),
            LastActivityAt = responses.Count == 0
                ? DateTime.MinValue
                : responses.Max(ticket => ticket.LastActivityAt),
            IsPinned = isPinned,
            IsUnread = responses.Any(ticket =>
                GetLastRequesterActivityAt(
                    tickets.Single(item => item.Id == ticket.Id),
                    messagesByTicket.GetValueOrDefault(ticket.Id, [])) is { } activity
                && (!lastReadAt.HasValue || activity > lastReadAt.Value)),
            OldestAwaitingSince = responses
                .Where(ticket => ticket.AwaitingStaffSince.HasValue)
                .Select(ticket => ticket.AwaitingStaffSince!.Value)
                .Cast<DateTime?>()
                .DefaultIfEmpty()
                .Min(),
            OldestAwaitingMinutes = responses
                .Where(ticket => ticket.AwaitingStaffSince.HasValue)
                .OrderBy(ticket => ticket.AwaitingStaffSince)
                .Select(ticket => ticket.AwaitingStaffMinutes)
                .FirstOrDefault(),
            Tickets = responses
        };
    }

    public async Task<ConversationPinOperationResult> MarkReadAsync(
        int currentUserId,
        UserRole currentUserRole,
        ConversationRequesterType requesterType,
        int requesterId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserRole is not (UserRole.Admin or UserRole.TechnicalStaff))
        {
            return ConversationPinOperationResult.Failed(ConversationPinOperationFailure.Forbidden);
        }

        if (requesterId <= 0 || !Enum.IsDefined(requesterType))
        {
            return ConversationPinOperationResult.Failed(ConversationPinOperationFailure.InvalidRequest);
        }

        if (!await ConversationExistsAsync(requesterType, requesterId, cancellationToken))
        {
            return ConversationPinOperationResult.Failed(ConversationPinOperationFailure.NotFound);
        }

        var now = DateTime.UtcNow;
        var state = await _dbContext.ConversationReadStates
            .SingleOrDefaultAsync(
                item => item.UserId == currentUserId
                    && item.RequesterType == requesterType
                    && item.RequesterId == requesterId,
                cancellationToken);
        if (state is null)
        {
            _dbContext.ConversationReadStates.Add(new ConversationReadState
            {
                UserId = currentUserId,
                RequesterType = requesterType,
                RequesterId = requesterId,
                LastReadAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            state.LastReadAt = now;
            state.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ConversationPinOperationResult.Succeeded();
    }

    public async Task<ConversationPinOperationResult> PinAsync(
        int currentUserId,
        UserRole currentUserRole,
        ConversationRequesterType requesterType,
        int requesterId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserRole is not (UserRole.Admin or UserRole.TechnicalStaff))
        {
            return ConversationPinOperationResult.Failed(ConversationPinOperationFailure.Forbidden);
        }

        if (requesterId <= 0 || !Enum.IsDefined(requesterType))
        {
            return ConversationPinOperationResult.Failed(ConversationPinOperationFailure.InvalidRequest);
        }

        var currentUserExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                user => user.Id == currentUserId
                    && user.IsActive
                    && user.Role == currentUserRole,
                cancellationToken);
        if (!currentUserExists)
        {
            return ConversationPinOperationResult.Failed(ConversationPinOperationFailure.Unauthorized);
        }

        if (!await ConversationExistsAsync(requesterType, requesterId, cancellationToken))
        {
            return ConversationPinOperationResult.Failed(ConversationPinOperationFailure.NotFound);
        }

        var exists = await _dbContext.ConversationPins
            .AsNoTracking()
            .AnyAsync(
                pin => pin.UserId == currentUserId
                    && pin.RequesterType == requesterType
                    && pin.RequesterId == requesterId,
                cancellationToken);
        if (exists)
        {
            return ConversationPinOperationResult.Succeeded();
        }

        _dbContext.ConversationPins.Add(new ConversationPin
        {
            UserId = currentUserId,
            RequesterType = requesterType,
            RequesterId = requesterId,
            CreatedAt = DateTime.UtcNow
        });

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The unique index makes concurrent PUT requests safely idempotent.
            _dbContext.ChangeTracker.Clear();

            var pinWasCreatedConcurrently = await _dbContext.ConversationPins
                .AsNoTracking()
                .AnyAsync(
                    pin => pin.UserId == currentUserId
                        && pin.RequesterType == requesterType
                        && pin.RequesterId == requesterId,
                    cancellationToken);
            if (!pinWasCreatedConcurrently)
            {
                throw;
            }
        }

        return ConversationPinOperationResult.Succeeded();
    }

    public async Task<ConversationPinOperationResult> UnpinAsync(
        int currentUserId,
        UserRole currentUserRole,
        ConversationRequesterType requesterType,
        int requesterId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserRole is not (UserRole.Admin or UserRole.TechnicalStaff))
        {
            return ConversationPinOperationResult.Failed(ConversationPinOperationFailure.Forbidden);
        }

        if (requesterId <= 0 || !Enum.IsDefined(requesterType))
        {
            return ConversationPinOperationResult.Failed(ConversationPinOperationFailure.InvalidRequest);
        }

        var currentUserExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                user => user.Id == currentUserId
                    && user.IsActive
                    && user.Role == currentUserRole,
                cancellationToken);
        if (!currentUserExists)
        {
            return ConversationPinOperationResult.Failed(ConversationPinOperationFailure.Unauthorized);
        }

        await _dbContext.ConversationPins
            .Where(pin => pin.UserId == currentUserId
                && pin.RequesterType == requesterType
                && pin.RequesterId == requesterId)
            .ExecuteDeleteAsync(cancellationToken);

        return ConversationPinOperationResult.Succeeded();
    }

    private async Task<Dictionary<ConversationPinKey, RequesterActivity>> GetActivityByRequesterAsync(
        CancellationToken cancellationToken)
    {
        var tickets = await _dbContext.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.CustomerId.HasValue || ticket.ExternalRequesterId.HasValue)
            .Select(ticket => new ActivityTicketProjection(
                ticket.Id,
                ticket.CustomerId.HasValue ? ConversationRequesterType.Registered : ConversationRequesterType.External,
                ticket.CustomerId ?? ticket.ExternalRequesterId!.Value,
                ticket.Status,
                ticket.Priority,
                ticket.CreatedAt,
                ticket.FirstResponseDueAt,
                ticket.FirstRespondedAt,
                ticket.ResolutionCycleStartedAt,
                ticket.ResolutionDueAt,
                ticket.ResolvedAt,
                ticket.TicketNumber,
                ticket.Title))
            .ToListAsync(cancellationToken);
        var ticketIds = tickets.Select(ticket => ticket.Id).ToArray();
        var messages = ticketIds.Length == 0
            ? []
            : await _dbContext.TicketMessages
                .AsNoTracking()
                .Where(message => ticketIds.Contains(message.TicketId) && !message.IsInternal)
                .Select(message => new ActivityMessageProjection(
                    message.TicketId,
                    message.Sender != null ? (UserRole?)message.Sender.Role : null,
                    message.ExternalRequesterId.HasValue,
                    message.OutboundDeliveryStatus,
                    message.DeliveredAt,
                    message.CreatedAt))
                .ToListAsync(cancellationToken);
        var messagesByTicket = messages
            .GroupBy(message => message.TicketId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return tickets
            .GroupBy(ticket => new ConversationPinKey(ticket.RequesterType, ticket.RequesterId))
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var ticketActivities = group.Select(ticket =>
                    {
                        var ticketMessages = messagesByTicket.GetValueOrDefault(ticket.Id, []);
                        var requesterActivity = GetLastRequesterActivityAt(ticket.CreatedAt, ticketMessages);
                        var aging = CalculateQueueAging(ticket, ticketMessages);
                        var sla = _ticketSlaCalculator.CalculateSummary(ToSlaTicket(ticket));
                        return new TicketActivity(requesterActivity, aging, sla.MostUrgentState);
                    }).ToList();

                    return new RequesterActivity(
                        ticketActivities.Count(activity => activity.Aging.IsAwaitingStaffReply),
                        ticketActivities.Count(activity => activity.SlaState == SlaState.Breached),
                        ticketActivities.Count(activity => activity.SlaState == SlaState.DueSoon),
                        ticketActivities
                            .Where(activity => activity.LastRequesterActivityAt.HasValue)
                            .Select(activity => activity.LastRequesterActivityAt!.Value)
                            .DefaultIfEmpty()
                            .Max(),
                        ticketActivities
                            .Where(activity => activity.Aging.AwaitingStaffSince.HasValue)
                            .Select(activity => activity.Aging.AwaitingStaffSince!.Value)
                            .Cast<DateTime?>()
                            .DefaultIfEmpty()
                            .Min(),
                        ticketActivities
                            .Where(activity => activity.Aging.AwaitingStaffSince.HasValue)
                            .OrderBy(activity => activity.Aging.AwaitingStaffSince)
                            .Select(activity => activity.Aging.AwaitingStaffMinutes)
                            .FirstOrDefault(),
                        group
                            .SelectMany(ticket => new[] { ticket.TicketNumber, ticket.Title })
                            .ToArray());
                });
    }

    private static DateTime? GetLastRequesterActivityAt(
        TicketProjection ticket,
        IReadOnlyList<MessageProjection> messages) =>
        GetLastRequesterActivityAt(ticket.CreatedAt, messages.Select(message => new ActivityMessageProjection(
            ticket.Id,
            message.IsSupportReply ? UserRole.TechnicalStaff : UserRole.Customer,
            false,
            message.DeliveryStatus,
            message.DeliveredAt,
            message.CreatedAt)).ToList());

    private static DateTime? GetLastRequesterActivityAt(
        DateTime ticketCreatedAt,
        IReadOnlyList<ActivityMessageProjection> messages)
    {
        var requesterMessages = messages
            .Where(message => message.SenderRole == UserRole.Customer || message.IsExternalRequester)
            .Select(message => (DateTime?)message.CreatedAt);
        return requesterMessages.Append(ticketCreatedAt).Max();
    }

    private QueueAgingResult CalculateQueueAging(
        TicketProjection ticket,
        IReadOnlyList<MessageProjection> messages) =>
        _queueAgingCalculator.Calculate(
            new QueueAgingTicketInput(ticket.Status, ticket.CreatedAt),
            messages.Select(message => new QueueAgingMessageInput(
                !message.IsSupportReply,
                message.IsSupportReply,
                false,
                message.DeliveryStatus,
                message.CreatedAt,
                message.DeliveredAt)).ToArray());

    private QueueAgingResult CalculateQueueAging(
        ActivityTicketProjection ticket,
        IReadOnlyList<ActivityMessageProjection> messages) =>
        _queueAgingCalculator.Calculate(
            new QueueAgingTicketInput(ticket.Status, ticket.CreatedAt),
            messages.Select(message => new QueueAgingMessageInput(
                message.SenderRole == UserRole.Customer || message.IsExternalRequester,
                message.SenderRole is UserRole.Admin or UserRole.TechnicalStaff,
                false,
                message.DeliveryStatus,
                message.CreatedAt,
                message.DeliveredAt)).ToArray());

    private async Task<List<ConversationSummaryResponse>> GetRegisteredSummariesAsync(
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.CustomerId.HasValue);

        return await query
            .GroupBy(ticket => new
            {
                RequesterId = ticket.CustomerId!.Value,
                DisplayName = ticket.Customer!.FirstName + " " + ticket.Customer.LastName,
                ticket.Customer.Email
            })
            .Select(group => new ConversationSummaryResponse
            {
                RequesterType = ConversationRequesterType.Registered,
                RequesterId = group.Key.RequesterId,
                DisplayName = group.Key.DisplayName,
                Email = group.Key.Email,
                TicketCount = group.Count(),
                OpenTicketCount = group.Count(ticket => ticket.Status != TicketStatus.Resolved && ticket.Status != TicketStatus.Closed),
                LastActivityAt = group.Max(ticket => ticket.Messages
                    .Where(message => !message.IsInternal)
                    .Select(message => (DateTime?)message.CreatedAt)
                    .Max() ?? ticket.CreatedAt),
                LastMessagePreview = group
                    .OrderByDescending(ticket => ticket.Messages
                        .Where(message => !message.IsInternal)
                        .Select(message => (DateTime?)message.CreatedAt)
                        .Max() ?? ticket.CreatedAt)
                    .Select(ticket => ticket.Messages
                        .Where(message => !message.IsInternal)
                        .OrderByDescending(message => message.CreatedAt)
                        .Select(message => message.Message)
                        .FirstOrDefault() ?? ticket.Description)
                    .FirstOrDefault() ?? string.Empty,
                LatestTicketId = group
                    .OrderByDescending(ticket => ticket.Messages
                        .Where(message => !message.IsInternal)
                        .Select(message => (DateTime?)message.CreatedAt)
                        .Max() ?? ticket.CreatedAt)
                    .Select(ticket => ticket.Id)
                    .First(),
                LatestTicketNumber = group
                    .OrderByDescending(ticket => ticket.Messages
                        .Where(message => !message.IsInternal)
                        .Select(message => (DateTime?)message.CreatedAt)
                        .Max() ?? ticket.CreatedAt)
                    .Select(ticket => ticket.TicketNumber)
                    .First(),
                LatestTicketTitle = group
                    .OrderByDescending(ticket => ticket.Messages
                        .Where(message => !message.IsInternal)
                        .Select(message => (DateTime?)message.CreatedAt)
                        .Max() ?? ticket.CreatedAt)
                    .Select(ticket => ticket.Title)
                    .First()
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<List<ConversationSummaryResponse>> GetExternalSummariesAsync(
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.ExternalRequesterId.HasValue);

        return await query
            .GroupBy(ticket => new
            {
                RequesterId = ticket.ExternalRequesterId!.Value,
                DisplayName = ticket.ExternalRequester!.DisplayName ?? ticket.ExternalRequester.Email,
                ticket.ExternalRequester.Email
            })
            .Select(group => new ConversationSummaryResponse
            {
                RequesterType = ConversationRequesterType.External,
                RequesterId = group.Key.RequesterId,
                DisplayName = group.Key.DisplayName,
                Email = group.Key.Email,
                TicketCount = group.Count(),
                OpenTicketCount = group.Count(ticket => ticket.Status != TicketStatus.Resolved && ticket.Status != TicketStatus.Closed),
                LastActivityAt = group.Max(ticket => ticket.Messages
                    .Where(message => !message.IsInternal)
                    .Select(message => (DateTime?)message.CreatedAt)
                    .Max() ?? ticket.CreatedAt),
                LastMessagePreview = group
                    .OrderByDescending(ticket => ticket.Messages
                        .Where(message => !message.IsInternal)
                        .Select(message => (DateTime?)message.CreatedAt)
                        .Max() ?? ticket.CreatedAt)
                    .Select(ticket => ticket.Messages
                        .Where(message => !message.IsInternal)
                        .OrderByDescending(message => message.CreatedAt)
                        .Select(message => message.Message)
                        .FirstOrDefault() ?? ticket.Description)
                    .FirstOrDefault() ?? string.Empty,
                LatestTicketId = group
                    .OrderByDescending(ticket => ticket.Messages
                        .Where(message => !message.IsInternal)
                        .Select(message => (DateTime?)message.CreatedAt)
                        .Max() ?? ticket.CreatedAt)
                    .Select(ticket => ticket.Id)
                    .First(),
                LatestTicketNumber = group
                    .OrderByDescending(ticket => ticket.Messages
                        .Where(message => !message.IsInternal)
                        .Select(message => (DateTime?)message.CreatedAt)
                        .Max() ?? ticket.CreatedAt)
                    .Select(ticket => ticket.TicketNumber)
                    .First(),
                LatestTicketTitle = group
                    .OrderByDescending(ticket => ticket.Messages
                        .Where(message => !message.IsInternal)
                        .Select(message => (DateTime?)message.CreatedAt)
                        .Max() ?? ticket.CreatedAt)
                    .Select(ticket => ticket.Title)
                    .First()
            })
            .ToListAsync(cancellationToken);
    }

    private static bool HasMatchingPublicMessage(
        string description,
        IReadOnlyList<MessageProjection> messages) =>
        messages.Any(message =>
            !message.IsSupportReply
            && string.Equals(
                message.Message.Trim(),
                description.Trim(),
            StringComparison.Ordinal));

    private static Ticket ToSlaTicket(TicketProjection ticket) =>
        new()
        {
            Id = ticket.Id,
            TicketNumber = ticket.TicketNumber,
            Title = ticket.Title,
            Description = ticket.Description,
            Status = ticket.Status,
            Priority = ticket.Priority,
            CreatedAt = ticket.CreatedAt,
            FirstResponseDueAt = ticket.FirstResponseDueAt,
            FirstRespondedAt = ticket.FirstRespondedAt,
            ResolutionCycleStartedAt = ticket.ResolutionCycleStartedAt,
            ResolutionDueAt = ticket.ResolutionDueAt,
            ResolvedAt = ticket.ResolvedAt
        };

    private static Ticket ToSlaTicket(ActivityTicketProjection ticket) =>
        new()
        {
            Id = ticket.Id,
            Status = ticket.Status,
            Priority = ticket.Priority,
            CreatedAt = ticket.CreatedAt,
            FirstResponseDueAt = ticket.FirstResponseDueAt,
            FirstRespondedAt = ticket.FirstRespondedAt,
            ResolutionCycleStartedAt = ticket.ResolutionCycleStartedAt,
            ResolutionDueAt = ticket.ResolutionDueAt,
            ResolvedAt = ticket.ResolvedAt
        };

    private static bool MatchesSearch(ConversationSummaryResponse summary, string? search) =>
        string.IsNullOrWhiteSpace(search)
        || summary.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase)
        || summary.Email.Contains(search, StringComparison.OrdinalIgnoreCase)
        || summary.LatestTicketNumber.Contains(search, StringComparison.OrdinalIgnoreCase)
        || summary.LatestTicketTitle.Contains(search, StringComparison.OrdinalIgnoreCase)
        || summary.SearchTerms.Any(term => term.Contains(search, StringComparison.OrdinalIgnoreCase));

    private static bool MatchesFilter(
        ConversationSummaryResponse summary,
        ConversationFilter filter) =>
        filter switch
        {
            ConversationFilter.AwaitingReply => summary.IsAwaitingStaffReply,
            ConversationFilter.Unread => summary.IsUnread,
            ConversationFilter.Pinned => summary.IsPinned,
            _ => true
        };

    private static bool IsOpen(TicketStatus status) =>
        status is not (TicketStatus.Resolved or TicketStatus.Closed);

    private async Task<bool> ConversationExistsAsync(
        ConversationRequesterType requesterType,
        int requesterId,
        CancellationToken cancellationToken) =>
        requesterType == ConversationRequesterType.Registered
            ? await _dbContext.Tickets
                .AsNoTracking()
                .AnyAsync(ticket => ticket.CustomerId == requesterId, cancellationToken)
            : await _dbContext.Tickets
                .AsNoTracking()
                .AnyAsync(ticket => ticket.ExternalRequesterId == requesterId, cancellationToken);

    private sealed record ConversationPinKey(ConversationRequesterType RequesterType, int RequesterId);
    private sealed record ConversationReadStateKey(
        ConversationRequesterType RequesterType,
        int RequesterId,
        DateTime LastReadAt);
    private sealed record RequesterActivity(
        int AwaitingReplyCount,
        int SlaBreachedTicketCount,
        int SlaDueSoonTicketCount,
        DateTime? LastRequesterActivityAt,
        DateTime? OldestAwaitingSince,
        int? OldestAwaitingMinutes,
        IReadOnlyList<string> SearchTerms);
    private sealed record TicketActivity(
        DateTime? LastRequesterActivityAt,
        QueueAgingResult Aging,
        SlaState SlaState);
    private sealed record ActivityTicketProjection(
        int Id,
        ConversationRequesterType RequesterType,
        int RequesterId,
        TicketStatus Status,
        TicketPriority Priority,
        DateTime CreatedAt,
        DateTime? FirstResponseDueAt,
        DateTime? FirstRespondedAt,
        DateTime? ResolutionCycleStartedAt,
        DateTime? ResolutionDueAt,
        DateTime? ResolvedAt,
        string TicketNumber,
        string Title);
    private sealed record ActivityMessageProjection(
        int TicketId,
        UserRole? SenderRole,
        bool IsExternalRequester,
        OutboundDeliveryStatus DeliveryStatus,
        DateTime? DeliveredAt,
        DateTime CreatedAt);
    private sealed record RequesterProjection(int Id, string DisplayName, string Email);
    private sealed record TicketProjection(
        int Id,
        string TicketNumber,
        string Title,
        TicketStatus Status,
        TicketPriority Priority,
        DateTime CreatedAt,
        string Description,
        DateTime? FirstResponseDueAt,
        DateTime? FirstRespondedAt,
        DateTime? ResolutionCycleStartedAt,
        DateTime? ResolutionDueAt,
        DateTime? ResolvedAt);
    private sealed record MessageProjection(
        int TicketId,
        int Id,
        string SenderName,
        string SenderLabel,
        bool IsSupportReply,
        string Message,
        OutboundDeliveryStatus DeliveryStatus,
        int DeliveryAttemptCount,
        DateTime? DeliveredAt,
        DateTime? LastDeliveryAttemptAt,
        string? SafeDeliveryError,
        DateTime CreatedAt);
    private sealed record AttachmentProjection(
        int TicketId,
        int Id,
        string FileName,
        string ContentType,
        long FileSize);
}

public enum ConversationPinOperationFailure
{
    None,
    InvalidRequest,
    NotFound,
    Forbidden,
    Unauthorized
}

public sealed record ConversationPinOperationResult(ConversationPinOperationFailure Failure)
{
    public static ConversationPinOperationResult Succeeded() =>
        new(ConversationPinOperationFailure.None);

    public static ConversationPinOperationResult Failed(ConversationPinOperationFailure failure) =>
        new(failure);
}
