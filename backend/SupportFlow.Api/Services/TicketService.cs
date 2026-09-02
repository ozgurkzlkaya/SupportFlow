using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SupportFlow.Api.Data;
using SupportFlow.Api.DTOs.Common;
using SupportFlow.Api.DTOs.Tickets;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;
using SupportFlow.Api.Services.Email;
using SupportFlow.Api.Services.Gmail;
using SupportFlow.Api.Services.QueueAging;
using SupportFlow.Api.Services.Sla;
using SupportFlow.Api.Services.TicketActivities;
using SupportFlow.Api.Settings;

namespace SupportFlow.Api.Services;

public class TicketService
{
    private readonly AppDbContext _dbContext;
    private readonly NotificationService _notificationService;
    private readonly IEmailService _emailService;
    private readonly TicketEmailComposer _emailComposer;
    private readonly IGmailSendService _gmailSendService;
    private readonly ITicketSlaService _ticketSlaService;
    private readonly ITicketSlaCalculator _ticketSlaCalculator;
    private readonly IQueueAgingCalculator _queueAgingCalculator;
    private readonly ITicketActivityWriter _ticketActivityWriter;
    private readonly ILogger<TicketService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly bool _queueAgingSqlProjectionEnabled;

    public TicketService(
        AppDbContext dbContext,
        NotificationService notificationService,
        IEmailService emailService,
        TicketEmailComposer emailComposer,
        IGmailSendService gmailSendService,
        ITicketSlaService ticketSlaService,
        ITicketSlaCalculator ticketSlaCalculator,
        IQueueAgingCalculator queueAgingCalculator,
        ITicketActivityWriter ticketActivityWriter,
        ILogger<TicketService> logger,
        TimeProvider timeProvider,
        IOptions<QueueAgingSettings> queueAgingSettings)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _emailService = emailService;
        _emailComposer = emailComposer;
        _gmailSendService = gmailSendService;
        _ticketSlaService = ticketSlaService;
        _ticketSlaCalculator = ticketSlaCalculator;
        _queueAgingCalculator = queueAgingCalculator;
        _ticketActivityWriter = ticketActivityWriter;
        _logger = logger;
        _timeProvider = timeProvider;
        _queueAgingSqlProjectionEnabled = queueAgingSettings.Value.SqlProjectionEnabled;
    }

    public async Task<TicketDetailResponse?> CreateAsync(
        int customerId,
        CreateTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        var customer = await _dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                user => user.Id == customerId
                    && user.IsActive
                    && user.Role == UserRole.Customer,
                cancellationToken);

        if (customer is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;

        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        var ticket = await CreateTicketRecordAsync(
            customer,
            request.Title,
            request.Description,
            request.Priority,
            now,
            cancellationToken);
        AppendUserActivity(
            ticket,
            TicketActivityType.TicketCreated,
            TicketActivityVisibility.RequesterVisible,
            now,
            customer);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var aging = CalculateQueueAging(ticket);
        var response = new TicketDetailResponse
        {
            Id = ticket.Id,
            TicketNumber = ticket.TicketNumber,
            Title = ticket.Title,
            Description = ticket.Description,
            Status = ticket.Status,
            Priority = ticket.Priority,
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt,
            ClosedAt = ticket.ClosedAt,
            CustomerId = customer.Id,
            CustomerName = customer.FirstName + " " + customer.LastName,
            CustomerEmail = customer.Email,
            AssignedUserId = null,
            AssignedUserName = null,
            IsAwaitingStaffReply = aging.IsAwaitingStaffReply,
            AwaitingStaffSince = aging.AwaitingStaffSince,
            AwaitingStaffMinutes = aging.AwaitingStaffMinutes,
            TicketAgeMinutes = aging.TicketAgeMinutes,
            AgingBucket = aging.AgingBucket,
            Sla = _ticketSlaCalculator.Calculate(ticket)
        };

        await TryNotifyNewTicketAsync(ticket, customer, cancellationToken);
        await _notificationService.NotifyNewTicketCreatedAsync(ticket, customer, cancellationToken);
        return response;
    }

    public async Task<PagedResponse<TicketListItemResponse>> GetAllAsync(
        int currentUserId,
        UserRole currentUserRole,
        TicketPriority? priority = null,
        TicketStatus? status = null,
        int? assignedUserId = null,
        TicketAssignmentFilter? assignmentState = null,
        bool activeOnly = false,
        string? search = null,
        TicketAgingFilter? agingFilter = null,
        TicketListSort? sort = null,
        int? categoryId = null,
        TicketClassificationFilter? classificationState = null,
        int? tagId = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyListFilters(
            _dbContext.Tickets.AsNoTracking(),
            currentUserId,
            currentUserRole,
            priority,
            status,
            assignedUserId,
            assignmentState,
            activeOnly,
            search,
            categoryId,
            classificationState,
            tagId);
        var usesAgingPath = agingFilter.HasValue
            || sort is TicketListSort.LongestWaiting or TicketListSort.ShortestWaiting;

        if (!usesAgingPath)
        {
            var totalCount = await query.CountAsync(cancellationToken);
            var pageTickets = await ApplySqlSort(query, sort)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(ticket => ticket.Customer)
                .Include(ticket => ticket.ExternalRequester)
                .Include(ticket => ticket.AssignedUser)
                .Include(ticket => ticket.Category)
                .Include(ticket => ticket.TagAssignments)
                    .ThenInclude(assignment => assignment.Tag)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);
            var pageAgingByTicketId = await GetQueueAgingAsync(pageTickets, cancellationToken);

            return CreatePagedResponse(
                pageTickets.Select(ticket => CreateTicketListItemResponse(
                    ticket,
                    pageAgingByTicketId.GetValueOrDefault(ticket.Id, CreateTicketAgeOnlyResult(ticket)),
                    currentUserRole is not UserRole.Customer)).ToArray(),
                page,
                pageSize,
                totalCount);
        }

        if (!_queueAgingSqlProjectionEnabled)
        {
            _logger.LogWarning("Ticket-list Queue Aging SQL projection is disabled by configuration.");
            return await GetAllWithInMemoryQueueAgingAsync(
                query,
                agingFilter,
                sort,
                page,
                pageSize,
                currentUserRole is not UserRole.Customer,
                cancellationToken);
        }

        return await GetAllWithSqlQueueAgingAsync(
            query,
            agingFilter,
            sort,
            page,
            pageSize,
            currentUserRole is not UserRole.Customer,
            cancellationToken);
    }

    private async Task<PagedResponse<TicketListItemResponse>> GetAllWithSqlQueueAgingAsync(
        IQueryable<Ticket> query,
        TicketAgingFilter? agingFilter,
        TicketListSort? sort,
        int page,
        int pageSize,
        bool includeTags,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var agingQuery = query.ProjectQueueAging(
            awaitingOnly: agingFilter.HasValue,
            awaitingBeforeOrAt: GetAgingCutoff(agingFilter, now),
            sort: ToQueueAgingProjectionSort(sort));
        var totalCount = await agingQuery.CountAsync(cancellationToken);
        var pageAging = await agingQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        if (pageAging.Count == 0)
        {
            return CreatePagedResponse([], page, pageSize, totalCount);
        }

        var pageTicketIds = pageAging.Select(item => item.TicketId).ToArray();
        var pageTickets = await query
            .Where(ticket => pageTicketIds.Contains(ticket.Id))
            .Include(ticket => ticket.Customer)
            .Include(ticket => ticket.ExternalRequester)
            .Include(ticket => ticket.AssignedUser)
            .Include(ticket => ticket.Category)
            .Include(ticket => ticket.TagAssignments)
                .ThenInclude(assignment => assignment.Tag)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        var ticketById = pageTickets.ToDictionary(ticket => ticket.Id);

        var pageItems = pageAging.Select(aging =>
        {
            var ticket = ticketById[aging.TicketId];
            var result = _queueAgingCalculator.CalculateFromAwaitingAnchor(
                new QueueAgingTicketInput(ticket.Status, ticket.CreatedAt),
                aging.AwaitingStaffSince,
                now);
            return CreateTicketListItemResponse(ticket, result, includeTags);
        }).ToArray();

        return CreatePagedResponse(pageItems, page, pageSize, totalCount);
    }

    private async Task<PagedResponse<TicketListItemResponse>> GetAllWithInMemoryQueueAgingAsync(
        IQueryable<Ticket> query,
        TicketAgingFilter? agingFilter,
        TicketListSort? sort,
        int page,
        int pageSize,
        bool includeTags,
        CancellationToken cancellationToken)
    {
        var candidates = await query
            .Include(ticket => ticket.Customer)
            .Include(ticket => ticket.ExternalRequester)
            .Include(ticket => ticket.AssignedUser)
            .Include(ticket => ticket.Category)
            .Include(ticket => ticket.TagAssignments)
                .ThenInclude(assignment => assignment.Tag)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        var agingByTicketId = await GetQueueAgingAsync(candidates, cancellationToken);
        var ordered = OrderTickets(candidates.Select(ticket => new TicketWithAging(
                ticket,
                agingByTicketId.GetValueOrDefault(ticket.Id, CreateTicketAgeOnlyResult(ticket))))
            .Where(candidate => MatchesAgingFilter(candidate.Aging, agingFilter)), sort)
            .ToArray();

        var totalCountAfterAging = ordered.Length;
        var pageItems = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(candidate => CreateTicketListItemResponse(candidate.Ticket, candidate.Aging, includeTags))
            .ToArray();

        return CreatePagedResponse(pageItems, page, pageSize, totalCountAfterAging);
    }

    public async Task<TicketDashboardSummaryResponse> GetDashboardSummaryAsync(
        int currentUserId,
        UserRole currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyListFilters(
            _dbContext.Tickets.AsNoTracking(),
            currentUserId,
            currentUserRole,
            null,
            null,
            null,
            null,
            false,
            null,
            null,
            null,
            null);
        var tickets = await query.ToListAsync(cancellationToken);
        var slaStates = tickets.Select(ticket => new
        {
            Ticket = ticket,
            Sla = _ticketSlaCalculator.CalculateSummary(ticket)
        }).ToArray();
        var recentTickets = await query
            .OrderByDescending(ticket => ticket.UpdatedAt)
            .ThenByDescending(ticket => ticket.Id)
            .Take(6)
            .Include(ticket => ticket.Customer)
            .Include(ticket => ticket.ExternalRequester)
            .Select(ticket => new TicketDashboardRecentItemResponse
            {
                Id = ticket.Id,
                TicketNumber = ticket.TicketNumber,
                Title = ticket.Title,
                Status = ticket.Status,
                Priority = ticket.Priority,
                CreatedAt = ticket.CreatedAt,
                UpdatedAt = ticket.UpdatedAt,
                CustomerName = ticket.Customer != null
                    ? ticket.Customer.FirstName + " " + ticket.Customer.LastName
                    : ticket.ExternalRequester != null
                        ? ticket.ExternalRequester.DisplayName ?? ticket.ExternalRequester.Email
                        : string.Empty
            })
            .ToListAsync(cancellationToken);

        return new TicketDashboardSummaryResponse
        {
            TotalCount = tickets.Count,
            OpenCount = tickets.Count(ticket => TicketLifecycle.ActiveStatuses.Contains(ticket.Status)),
            NewCount = tickets.Count(ticket => ticket.Status == TicketStatus.New),
            InProgressCount = tickets.Count(ticket => ticket.Status == TicketStatus.InProgress),
            WaitingForCustomerCount = tickets.Count(ticket => ticket.Status == TicketStatus.WaitingForCustomer),
            ReopenedCount = tickets.Count(ticket => ticket.Status == TicketStatus.Reopened),
            ResolvedOrClosedCount = tickets.Count(ticket => ticket.Status is TicketStatus.Resolved or TicketStatus.Closed),
            LowPriorityCount = tickets.Count(ticket => ticket.Priority == TicketPriority.Low),
            NormalPriorityCount = tickets.Count(ticket => ticket.Priority == TicketPriority.Normal),
            HighPriorityCount = tickets.Count(ticket => ticket.Priority == TicketPriority.High),
            CriticalPriorityCount = tickets.Count(ticket => ticket.Priority == TicketPriority.Critical),
            SlaBreachedCount = slaStates.Count(item => item.Sla.MostUrgentState == SlaState.Breached),
            SlaDueSoonCount = slaStates.Count(item => item.Sla.MostUrgentState == SlaState.DueSoon),
            FirstResponseBreachedCount = slaStates.Count(item => item.Sla.FirstResponseState == SlaState.Breached),
            ResolutionBreachedCount = slaStates.Count(item => item.Sla.ResolutionState == SlaState.Breached),
            RecentTickets = recentTickets
        };
    }

    public async Task<TicketDetailResponse?> GetByIdAsync(
        int ticketId,
        int currentUserId,
        UserRole currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.Id == ticketId);

        if (currentUserRole == UserRole.Customer)
        {
            query = query.Where(ticket => ticket.CustomerId == currentUserId);
        }

        var ticket = await query
            .Include(item => item.Customer)
            .Include(item => item.ExternalRequester)
            .Include(item => item.AssignedUser)
            .Include(item => item.Category)
            .Include(item => item.TagAssignments)
                .ThenInclude(assignment => assignment.Tag)
            .Include(item => item.Messages)
                .ThenInclude(message => message.Sender)
            .AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);

        return ticket is null
            ? null
            : CreateTicketDetailResponse(ticket, includeTags: currentUserRole is not UserRole.Customer);
    }

    public async Task<TicketOperationResult<IReadOnlyList<TicketMessageResponse>>> GetMessagesAsync(
        int ticketId,
        int currentUserId,
        UserRole currentUserRole,
        CancellationToken cancellationToken = default)
    {
        if (!await CanAccessTicketAsync(
                ticketId,
                currentUserId,
                currentUserRole,
                cancellationToken))
        {
            return TicketOperationResult<IReadOnlyList<TicketMessageResponse>>.Failed(
                TicketOperationFailure.NotFound);
        }

        var query = _dbContext.TicketMessages
            .AsNoTracking()
            .Where(message => message.TicketId == ticketId);

        if (currentUserRole == UserRole.Customer)
        {
            query = query.Where(message =>
                !message.IsInternal
                && (message.Sender == null
                    || (message.Sender.Role != UserRole.Admin
                        && message.Sender.Role != UserRole.TechnicalStaff)
                    || message.OutboundDeliveryStatus == OutboundDeliveryStatus.Sent
                    || message.OutboundDeliveryStatus == OutboundDeliveryStatus.NotApplicable));
        }

        var messages = await query
            .OrderBy(message => message.CreatedAt)
            .Select(message => new TicketMessageResponse
            {
                Id = message.Id,
                TicketId = message.TicketId,
                SenderId = message.SenderId,
                SenderName = message.Sender == null ? string.Empty : message.Sender.FirstName + " " + message.Sender.LastName,
                SenderRole = message.Sender == null ? UserRole.Customer : message.Sender.Role,
                Message = message.Message,
                IsInternal = message.IsInternal,
                DeliveryStatus = message.OutboundDeliveryStatus,
                DeliveryAttemptCount = message.DeliveryAttemptCount,
                DeliveredAt = message.DeliveredAt,
                LastDeliveryAttemptAt = message.LastDeliveryAttemptAt,
                SafeDeliveryError = message.OutboundDeliveryStatus == OutboundDeliveryStatus.Failed
                    ? message.OutboundDeliveryError
                    : null,
                CreatedAt = message.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return TicketOperationResult<IReadOnlyList<TicketMessageResponse>>.Succeeded(messages);
    }

    public async Task<TicketOperationResult<TicketMessageResponse>> AddMessageAsync(
        int ticketId,
        int senderId,
        UserRole senderRole,
        CreateTicketMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var sender = await GetActiveUserAsync(senderId, senderRole, cancellationToken);

        if (sender is null)
        {
            return TicketOperationResult<TicketMessageResponse>.Failed(
                TicketOperationFailure.Unauthorized);
        }

        var ticket = await _dbContext.Tickets
            .SingleOrDefaultAsync(ticket => ticket.Id == ticketId, cancellationToken);

        if (ticket is null
            || senderRole == UserRole.Customer && ticket.CustomerId != senderId)
        {
            return TicketOperationResult<TicketMessageResponse>.Failed(
                TicketOperationFailure.NotFound);
        }

        if (senderRole == UserRole.Customer && request.IsInternal)
        {
            return TicketOperationResult<TicketMessageResponse>.Failed(
                TicketOperationFailure.Forbidden,
                "Customers cannot create internal messages.");
        }

        var now = DateTime.UtcNow;
        var wasReopened = false;
        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        var writeResult = AddTicketMessageRecord(
            ticket,
            sender,
            request.Message,
            request.IsInternal,
            now);
        var message = writeResult.Message;
        wasReopened = writeResult.WasReopened;

        await _dbContext.SaveChangesAsync(cancellationToken);
        AppendUserActivity(
            ticket,
            request.IsInternal ? TicketActivityType.InternalNoteCreated : TicketActivityType.MessageCreated,
            request.IsInternal ? TicketActivityVisibility.Internal : TicketActivityVisibility.RequesterVisible,
            now,
            sender,
            relatedMessageId: message.Id);
        AppendStatusChangedActivity(
            ticket,
            writeResult.ReopenedFromStatus,
            TicketActivitySource.Portal,
            now,
            sender,
            writeResult.StatusHistory?.Id);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (IsStaffPublicReply(senderRole, request.IsInternal))
        {
            return await DeliverStaffPublicReplyAsync(ticket, sender, message, cancellationToken);
        }

        var response = CreateTicketMessageResponse(message, sender);

        if (!request.IsInternal)
        {
            if (senderRole == UserRole.Customer)
            {
                await TryNotifyTechnicalStaffOfCustomerReplyAsync(
                    ticket,
                    sender,
                    message.Message,
                    wasReopened,
                    cancellationToken);
                await _notificationService.NotifyCustomerReplyAsync(
                    ticket,
                    sender,
                    cancellationToken);
            }
        }

        return TicketOperationResult<TicketMessageResponse>.Succeeded(response);
    }

    public async Task<TicketOperationResult<TicketMessageResponse>> RetryMessageDeliveryAsync(
        int ticketId,
        int messageId,
        int currentUserId,
        UserRole currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var currentUser = await GetActiveUserAsync(currentUserId, currentUserRole, cancellationToken);
        if (currentUser is null)
        {
            return TicketOperationResult<TicketMessageResponse>.Failed(TicketOperationFailure.Unauthorized);
        }

        if (currentUserRole is not (UserRole.Admin or UserRole.TechnicalStaff))
        {
            return TicketOperationResult<TicketMessageResponse>.Failed(TicketOperationFailure.Forbidden);
        }

        var ticket = await _dbContext.Tickets
            .SingleOrDefaultAsync(item => item.Id == ticketId, cancellationToken);
        if (ticket is null)
        {
            return TicketOperationResult<TicketMessageResponse>.Failed(TicketOperationFailure.NotFound);
        }

        var messageInfo = await _dbContext.TicketMessages
            .AsNoTracking()
            .Where(message => message.Id == messageId && message.TicketId == ticketId)
            .Select(message => new
            {
                message.IsInternal,
                message.SenderId,
                SenderRole = message.Sender == null ? (UserRole?)null : message.Sender.Role,
                message.OutboundDeliveryStatus
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (messageInfo is null)
        {
            return TicketOperationResult<TicketMessageResponse>.Failed(TicketOperationFailure.NotFound);
        }

        if (messageInfo.IsInternal
            || messageInfo.SenderId is null
            || messageInfo.SenderRole is not (UserRole.Admin or UserRole.TechnicalStaff)
            || messageInfo.OutboundDeliveryStatus != OutboundDeliveryStatus.Failed)
        {
            return TicketOperationResult<TicketMessageResponse>.Failed(
                TicketOperationFailure.InvalidRequest,
                "Only failed public staff replies can be retried.");
        }

        var attemptAt = DateTime.UtcNow;
        var attemptsStarted = await _dbContext.TicketMessages
            .Where(message => message.Id == messageId
                && message.TicketId == ticketId
                && message.OutboundDeliveryStatus == OutboundDeliveryStatus.Failed)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(message => message.OutboundDeliveryStatus, OutboundDeliveryStatus.Pending)
                    .SetProperty(message => message.OutboundDeliveryError, (string?)null)
                    .SetProperty(message => message.LastDeliveryAttemptAt, attemptAt)
                    .SetProperty(message => message.DeliveryAttemptCount, message => message.DeliveryAttemptCount + 1),
                cancellationToken);

        if (attemptsStarted == 0)
        {
            return TicketOperationResult<TicketMessageResponse>.Failed(
                TicketOperationFailure.InvalidRequest,
                "The message delivery is no longer available for retry.");
        }

        var message = await _dbContext.TicketMessages
            .Include(item => item.Sender)
            .SingleAsync(item => item.Id == messageId && item.TicketId == ticketId, cancellationToken);

        await _dbContext.Entry(message).ReloadAsync(cancellationToken);

        return await DeliverStaffPublicReplyAsync(ticket, message.Sender!, message, cancellationToken);
    }

    public async Task<TicketOperationResult<TicketDetailResponse>> AssignAsync(
        int ticketId,
        int changedByUserId,
        UserRole changedByUserRole,
        AssignTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        var changedByUser = await GetActiveUserAsync(
            changedByUserId,
            changedByUserRole,
            cancellationToken);

        if (changedByUser is null)
        {
            return TicketOperationResult<TicketDetailResponse>.Failed(
                TicketOperationFailure.Unauthorized);
        }

        if (changedByUserRole is not (UserRole.TechnicalStaff or UserRole.Admin))
        {
            return TicketOperationResult<TicketDetailResponse>.Failed(
                TicketOperationFailure.Forbidden);
        }

        var assignedUser = await _dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                user => user.Id == request.AssignedUserId
                    && user.IsActive
                    && user.Role == UserRole.TechnicalStaff,
                cancellationToken);

        if (assignedUser is null)
        {
            return TicketOperationResult<TicketDetailResponse>.Failed(
                TicketOperationFailure.InvalidRequest,
                "The assigned user must be an active TechnicalStaff user.");
        }

        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        var ticket = await _dbContext.Tickets
            .SingleOrDefaultAsync(ticket => ticket.Id == ticketId, cancellationToken);

        if (ticket is null)
        {
            return TicketOperationResult<TicketDetailResponse>.Failed(
                TicketOperationFailure.NotFound);
        }

        var previousAssignee = ticket.AssignedUserId.HasValue
            ? await _dbContext.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(user => user.Id == ticket.AssignedUserId.Value, cancellationToken)
            : null;
        var assignmentChanged = ticket.AssignedUserId != assignedUser.Id;
        var now = DateTime.UtcNow;
        ticket.AssignedUserId = assignedUser.Id;
        ticket.UpdatedAt = now;

        TicketStatusHistory? statusHistory = null;
        if (ticket.Status == TicketStatus.New)
        {
            ticket.Status = TicketStatus.Assigned;
            statusHistory = new TicketStatusHistory
            {
                TicketId = ticket.Id,
                ChangedByUserId = changedByUser.Id,
                OldStatus = TicketStatus.New,
                NewStatus = TicketStatus.Assigned,
                Note = "Ticket assigned to technical staff.",
                ChangedAt = now
            };
            _dbContext.TicketStatusHistories.Add(statusHistory);
        }

        if (assignmentChanged)
        {
            AppendUserActivity(
                ticket,
                TicketActivityType.AssignmentChanged,
                TicketActivityVisibility.Internal,
                now,
                changedByUser,
                new AssignmentChangedTicketActivityMetadata(
                    previousAssignee?.Id,
                    assignedUser.Id,
                    previousAssignee is null ? null : GetDisplayName(previousAssignee),
                    GetDisplayName(assignedUser)));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (statusHistory is not null)
        {
            AppendStatusChangedActivity(
                ticket,
                statusHistory.OldStatus,
                TicketActivitySource.Portal,
                now,
                changedByUser,
                statusHistory.Id);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);

        var response = await GetByIdAsync(
            ticket.Id,
            changedByUser.Id,
            changedByUser.Role,
            cancellationToken);

        if (assignmentChanged)
        {
            await TryNotifyAssignmentAsync(
                ticket,
                assignedUser,
                cancellationToken);
            await _notificationService.NotifyAssignmentAsync(
                ticket,
                assignedUser,
                cancellationToken);
        }

        return TicketOperationResult<TicketDetailResponse>.Succeeded(response!);
    }

    public async Task<TicketOperationResult<TicketDetailResponse>> UpdateStatusAsync(
        int ticketId,
        int changedByUserId,
        UserRole changedByUserRole,
        UpdateTicketStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var changedByUser = await GetActiveUserAsync(
            changedByUserId,
            changedByUserRole,
            cancellationToken);

        if (changedByUser is null)
        {
            return TicketOperationResult<TicketDetailResponse>.Failed(
                TicketOperationFailure.Unauthorized);
        }

        if (changedByUserRole is not (UserRole.TechnicalStaff or UserRole.Admin))
        {
            return TicketOperationResult<TicketDetailResponse>.Failed(
                TicketOperationFailure.Forbidden);
        }

        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        var ticket = await _dbContext.Tickets
            .SingleOrDefaultAsync(ticket => ticket.Id == ticketId, cancellationToken);

        if (ticket is null)
        {
            return TicketOperationResult<TicketDetailResponse>.Failed(
                TicketOperationFailure.NotFound);
        }

        if (ticket.Status == request.Status)
        {
            return TicketOperationResult<TicketDetailResponse>.Failed(
                TicketOperationFailure.InvalidRequest,
                "Ticket is already in the requested status.");
        }

        if (!IsAllowedTransition(ticket.Status, request.Status))
        {
            return TicketOperationResult<TicketDetailResponse>.Failed(
                TicketOperationFailure.InvalidRequest,
                $"The transition from {ticket.Status} to {request.Status} is not allowed.");
        }

        var now = DateTime.UtcNow;
        var oldStatus = ticket.Status;
        ticket.Status = request.Status;
        ticket.UpdatedAt = now;
        ticket.ClosedAt = request.Status == TicketStatus.Closed ? now : null;
        _ticketSlaService.ApplyStatusTransition(ticket, oldStatus, request.Status, now);

        var statusHistory = new TicketStatusHistory
        {
            TicketId = ticket.Id,
            ChangedByUserId = changedByUser.Id,
            OldStatus = oldStatus,
            NewStatus = request.Status,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            ChangedAt = now
        };
        _dbContext.TicketStatusHistories.Add(statusHistory);

        await _dbContext.SaveChangesAsync(cancellationToken);
        AppendStatusChangedActivity(
            ticket,
            oldStatus,
            TicketActivitySource.Portal,
            now,
            changedByUser,
            statusHistory.Id);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var response = await GetByIdAsync(
            ticket.Id,
            changedByUser.Id,
            changedByUser.Role,
            cancellationToken);

        if (ShouldNotifyCustomerOfStatus(request.Status) && ticket.CustomerId.HasValue)
        {
            await TryNotifyCustomerOfStatusChangeAsync(
                ticket,
                oldStatus,
                request.Status,
                request.Note,
                cancellationToken);
            await _notificationService.NotifyStatusChangeAsync(
                ticket,
                request.Status,
                cancellationToken);
        }

        return TicketOperationResult<TicketDetailResponse>.Succeeded(response!);
    }

    public async Task<TicketOperationResult<TicketDetailResponse>> UpdatePriorityAsync(
        int ticketId,
        int changedByUserId,
        UserRole changedByUserRole,
        UpdateTicketPriorityRequest request,
        CancellationToken cancellationToken = default)
    {
        var changedByUser = await GetActiveUserAsync(
            changedByUserId,
            changedByUserRole,
            cancellationToken);

        if (changedByUser is null)
        {
            return TicketOperationResult<TicketDetailResponse>.Failed(
                TicketOperationFailure.Unauthorized);
        }

        if (changedByUserRole is not (UserRole.TechnicalStaff or UserRole.Admin))
        {
            return TicketOperationResult<TicketDetailResponse>.Failed(
                TicketOperationFailure.Forbidden);
        }

        if (!Enum.IsDefined(request.Priority))
        {
            return TicketOperationResult<TicketDetailResponse>.Failed(
                TicketOperationFailure.InvalidRequest,
                "The supplied ticket priority is invalid.");
        }

        var ticket = await _dbContext.Tickets
            .SingleOrDefaultAsync(ticket => ticket.Id == ticketId, cancellationToken);

        if (ticket is null)
        {
            return TicketOperationResult<TicketDetailResponse>.Failed(
                TicketOperationFailure.NotFound);
        }

        if (ticket.Priority == request.Priority)
        {
            return TicketOperationResult<TicketDetailResponse>.Failed(
                TicketOperationFailure.InvalidRequest,
                "Ticket already has the requested priority.");
        }

        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        var oldPriority = ticket.Priority;
        var now = DateTime.UtcNow;
        ticket.Priority = request.Priority;
        ticket.UpdatedAt = now;
        _ticketSlaService.RecalculateUnfinishedDeadlines(ticket, request.Priority);
        AppendUserActivity(
            ticket,
            TicketActivityType.PriorityChanged,
            TicketActivityVisibility.Internal,
            now,
            changedByUser,
            new PriorityChangedTicketActivityMetadata(oldPriority, request.Priority));
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var response = await GetByIdAsync(
            ticket.Id,
            changedByUser.Id,
            changedByUser.Role,
            cancellationToken);

        return TicketOperationResult<TicketDetailResponse>.Succeeded(response!);
    }

    public async Task<TicketOperationResult<TicketClassificationResponse>> UpdateClassificationAsync(
        int ticketId,
        int changedByUserId,
        UserRole changedByUserRole,
        UpdateTicketClassificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var changedByUser = await GetActiveUserAsync(changedByUserId, changedByUserRole, cancellationToken);
        if (changedByUser is null)
        {
            return TicketOperationResult<TicketClassificationResponse>.Failed(TicketOperationFailure.Unauthorized);
        }

        if (changedByUserRole is not (UserRole.TechnicalStaff or UserRole.Admin))
        {
            return TicketOperationResult<TicketClassificationResponse>.Failed(TicketOperationFailure.Forbidden);
        }

        var requestedTagIds = (request.TagIds ?? [])
            .Distinct()
            .ToArray();
        if (requestedTagIds.Any(tagId => tagId <= 0))
        {
            return TicketOperationResult<TicketClassificationResponse>.Failed(
                TicketOperationFailure.InvalidRequest,
                "The supplied tag id is invalid.");
        }

        if (request.CategoryId is <= 0)
        {
            return TicketOperationResult<TicketClassificationResponse>.Failed(
                TicketOperationFailure.InvalidRequest,
                "The supplied category id is invalid.");
        }

        var ticket = await _dbContext.Tickets
            .Include(item => item.Category)
            .Include(item => item.TagAssignments)
                .ThenInclude(assignment => assignment.Tag)
            .SingleOrDefaultAsync(item => item.Id == ticketId, cancellationToken);
        if (ticket is null)
        {
            return TicketOperationResult<TicketClassificationResponse>.Failed(TicketOperationFailure.NotFound);
        }

        TicketCategory? requestedCategory = null;
        if (request.CategoryId.HasValue)
        {
            requestedCategory = request.CategoryId == ticket.CategoryId
                ? ticket.Category
                : await _dbContext.TicketCategories
                    .SingleOrDefaultAsync(item => item.Id == request.CategoryId.Value, cancellationToken);

            if (requestedCategory is null || !requestedCategory.IsActive && request.CategoryId != ticket.CategoryId)
            {
                return TicketOperationResult<TicketClassificationResponse>.Failed(
                    TicketOperationFailure.InvalidRequest,
                    "The supplied category must be active and available for assignment.");
            }
        }

        var requestedTags = requestedTagIds.Length == 0
            ? []
            : await _dbContext.TicketTags
                .Where(tag => requestedTagIds.Contains(tag.Id))
                .ToListAsync(cancellationToken);
        if (requestedTags.Count != requestedTagIds.Length)
        {
            return TicketOperationResult<TicketClassificationResponse>.Failed(
                TicketOperationFailure.InvalidRequest,
                "One or more supplied tags do not exist.");
        }

        var existingTagIds = ticket.TagAssignments.Select(assignment => assignment.TagId).ToHashSet();
        if (requestedTags.Any(tag => !existingTagIds.Contains(tag.Id) && !tag.IsActive))
        {
            return TicketOperationResult<TicketClassificationResponse>.Failed(
                TicketOperationFailure.InvalidRequest,
                "Newly assigned tags must be active.");
        }

        var categoryChanged = ticket.CategoryId != request.CategoryId;
        var tagsChanged = !existingTagIds.SetEquals(requestedTagIds);
        if (!categoryChanged && !tagsChanged)
        {
            return TicketOperationResult<TicketClassificationResponse>.Succeeded(
                CreateClassificationResponse(ticket.Category, ticket.TagAssignments.Select(assignment => assignment.Tag)));
        }

        var oldCategoryId = ticket.CategoryId;
        var oldCategoryName = ticket.Category?.Name;
        var oldTags = ticket.TagAssignments
            .Select(assignment => new TicketActivityTagSnapshot(assignment.Tag.Id, assignment.Tag.Name))
            .ToArray();
        var newTags = requestedTags
            .Select(tag => new TicketActivityTagSnapshot(tag.Id, tag.Name))
            .ToArray();
        var addedTags = newTags.Where(tag => !existingTagIds.Contains(tag.Id)).ToArray();
        var removedTags = oldTags.Where(tag => !requestedTagIds.Contains(tag.Id)).ToArray();

        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        ticket.CategoryId = request.CategoryId;
        ticket.Category = requestedCategory;
        foreach (var assignment in ticket.TagAssignments.Where(assignment => !requestedTagIds.Contains(assignment.TagId)).ToArray())
        {
            _dbContext.TicketTagAssignments.Remove(assignment);
        }

        foreach (var tag in requestedTags.Where(tag => !existingTagIds.Contains(tag.Id)))
        {
            ticket.TagAssignments.Add(new TicketTagAssignment
            {
                TicketId = ticket.Id,
                TagId = tag.Id,
                Tag = tag
            });
        }

        ticket.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        AppendUserActivity(
            ticket,
            TicketActivityType.ClassificationChanged,
            TicketActivityVisibility.Internal,
            ticket.UpdatedAt,
            changedByUser,
            new ClassificationChangedTicketActivityMetadata(
                oldCategoryId,
                request.CategoryId,
                oldCategoryName,
                requestedCategory?.Name,
                addedTags,
                removedTags));
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return TicketOperationResult<TicketClassificationResponse>.Succeeded(
            CreateClassificationResponse(requestedCategory, requestedTags));
    }

    public async Task<TicketOperationResult<IReadOnlyList<TicketStatusHistoryResponse>>> GetHistoryAsync(
        int ticketId,
        int currentUserId,
        UserRole currentUserRole,
        CancellationToken cancellationToken = default)
    {
        if (!await CanAccessTicketAsync(
                ticketId,
                currentUserId,
                currentUserRole,
                cancellationToken))
        {
            return TicketOperationResult<IReadOnlyList<TicketStatusHistoryResponse>>.Failed(
                TicketOperationFailure.NotFound);
        }

        var history = await _dbContext.TicketStatusHistories
            .AsNoTracking()
            .Where(item => item.TicketId == ticketId)
            .OrderBy(item => item.ChangedAt)
            .Select(item => new TicketStatusHistoryResponse
            {
                Id = item.Id,
                OldStatus = item.OldStatus,
                NewStatus = item.NewStatus,
                ChangedByUserId = item.ChangedByUserId,
                ChangedByUserName = item.ChangedByUser.FirstName + " " + item.ChangedByUser.LastName,
                Note = item.Note,
                ChangedAt = item.ChangedAt
            })
            .ToListAsync(cancellationToken);

        return TicketOperationResult<IReadOnlyList<TicketStatusHistoryResponse>>.Succeeded(history);
    }

    internal async Task<Ticket> CreateTicketRecordAsync(
        User customer,
        string title,
        string description,
        TicketPriority priority,
        DateTime createdAt,
        CancellationToken cancellationToken)
    {
        return await CreateGmailTicketRecordAsync(
            customer.Id,
            null,
            title,
            description,
            priority,
            createdAt,
            cancellationToken);
    }

    internal async Task<Ticket> CreateExternalRequesterTicketRecordAsync(
        ExternalRequester requester,
        string title,
        string description,
        TicketPriority priority,
        DateTime createdAt,
        CancellationToken cancellationToken) =>
        await CreateGmailTicketRecordAsync(
            null,
            requester,
            title,
            description,
            priority,
            createdAt,
            cancellationToken);

    private async Task<Ticket> CreateGmailTicketRecordAsync(
        int? customerId,
        ExternalRequester? externalRequester,
        string title,
        string description,
        TicketPriority priority,
        DateTime createdAt,
        CancellationToken cancellationToken)
    {
        if (customerId.HasValue == (externalRequester is not null))
        {
            throw new ArgumentException(
                "A Gmail-created ticket must have exactly one requester.");
        }

        var normalizedTitle = title.Trim();
        if (normalizedTitle.Length > 250)
        {
            normalizedTitle = normalizedTitle[..250];
        }

        var ticket = new Ticket
        {
            TicketNumber = $"TMP-{Guid.NewGuid():N}"[..30],
            CustomerId = customerId,
            ExternalRequester = externalRequester,
            AssignedUserId = null,
            Title = normalizedTitle,
            Description = description.Trim(),
            Status = TicketStatus.New,
            Priority = priority,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            ClosedAt = null
        };
        _ticketSlaService.InitializeNewTicket(ticket);

        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync(cancellationToken);

        ticket.TicketNumber = $"SUP-{createdAt.Year}-{ticket.Id:D6}";
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ticket;
    }

    internal TicketMessageWriteResult AddTicketMessageRecord(
        Ticket ticket,
        User sender,
        string messageText,
        bool isInternal,
        DateTime createdAt)
    {
        var message = new TicketMessage
        {
            TicketId = ticket.Id,
            SenderId = sender.Id,
            Message = messageText.Trim(),
            IsInternal = isInternal,
            OutboundDeliveryStatus = IsStaffPublicReply(sender.Role, isInternal)
                ? OutboundDeliveryStatus.Pending
                : OutboundDeliveryStatus.NotApplicable,
            DeliveryAttemptCount = IsStaffPublicReply(sender.Role, isInternal) ? 1 : 0,
            LastDeliveryAttemptAt = IsStaffPublicReply(sender.Role, isInternal) ? createdAt : null,
            CreatedAt = createdAt
        };

        _dbContext.TicketMessages.Add(message);
        ticket.UpdatedAt = createdAt;

        var wasReopened = false;
        TicketStatus? reopenedFromStatus = null;
        TicketStatusHistory? statusHistory = null;
        if (sender.Role == UserRole.Customer
            && ticket.Status is TicketStatus.Resolved or TicketStatus.Closed)
        {
            var oldStatus = ticket.Status;
            reopenedFromStatus = oldStatus;
            ticket.Status = TicketStatus.Reopened;
            ticket.ClosedAt = null;
            _ticketSlaService.ApplyStatusTransition(
                ticket,
                oldStatus,
                TicketStatus.Reopened,
                createdAt);
            wasReopened = true;

            statusHistory = new TicketStatusHistory
            {
                TicketId = ticket.Id,
                ChangedByUserId = sender.Id,
                OldStatus = oldStatus,
                NewStatus = TicketStatus.Reopened,
                Note = "Automatically reopened after customer reply.",
                ChangedAt = createdAt
            };
            _dbContext.TicketStatusHistories.Add(statusHistory);
        }

        return new TicketMessageWriteResult(message, wasReopened, reopenedFromStatus, statusHistory);
    }

    internal TicketMessageWriteResult AddExternalRequesterTicketMessageRecord(
        Ticket ticket,
        ExternalRequester requester,
        string messageText,
        bool isInternal,
        DateTime createdAt)
    {
        var message = new TicketMessage
        {
            TicketId = ticket.Id,
            ExternalRequesterId = requester.Id,
            Message = messageText.Trim(),
            IsInternal = isInternal,
            CreatedAt = createdAt
        };

        _dbContext.TicketMessages.Add(message);
        ticket.UpdatedAt = createdAt;

        var wasReopened = false;
        TicketStatus? reopenedFromStatus = null;
        if (ticket.Status is TicketStatus.Resolved or TicketStatus.Closed)
        {
            var oldStatus = ticket.Status;
            reopenedFromStatus = oldStatus;
            ticket.Status = TicketStatus.Reopened;
            ticket.ClosedAt = null;
            _ticketSlaService.ApplyStatusTransition(
                ticket,
                oldStatus,
                TicketStatus.Reopened,
                createdAt);
            wasReopened = true;

            // TicketStatusHistory requires a non-null User actor, which external requesters do not have.
        }

        return new TicketMessageWriteResult(message, wasReopened, reopenedFromStatus, null);
    }

    internal async Task TryNotifyNewTicketAsync(
        Ticket ticket,
        User customer,
        CancellationToken cancellationToken)
    {
        await TryRunNotificationAsync(ticket.TicketNumber, async () =>
        {
            var recipients = await GetActiveTechnicalStaffEmailsAsync(cancellationToken);
            var content = _emailComposer.ComposeNewTicket(ticket, customer);
            await SendToRecipientsAsync(recipients, content, cancellationToken);
        });
    }

    internal async Task TryNotifyNewTicketAsync(
        Ticket ticket,
        ExternalRequester requester,
        CancellationToken cancellationToken)
    {
        await TryRunNotificationAsync(ticket.TicketNumber, async () =>
        {
            var recipients = await GetActiveTechnicalStaffEmailsAsync(cancellationToken);
            var content = _emailComposer.ComposeNewTicket(ticket, requester);
            await SendToRecipientsAsync(recipients, content, cancellationToken);
        });
    }

    private TicketListItemResponse CreateTicketListItemResponse(Ticket ticket) =>
        CreateTicketListItemResponse(ticket, CalculateQueueAging(ticket));

    private TicketListItemResponse CreateTicketListItemResponse(
        Ticket ticket,
        QueueAgingResult aging,
        bool includeTags = true) =>
        new()
        {
            Id = ticket.Id,
            TicketNumber = ticket.TicketNumber,
            Title = ticket.Title,
            Status = ticket.Status,
            Priority = ticket.Priority,
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt,
            CustomerId = ticket.CustomerId,
            ExternalRequesterId = ticket.ExternalRequesterId,
            CustomerName = ticket.Customer != null
                ? ticket.Customer.FirstName + " " + ticket.Customer.LastName
                : ticket.ExternalRequester?.DisplayName ?? ticket.ExternalRequester?.Email ?? string.Empty,
            RequesterEmail = ticket.Customer?.Email ?? ticket.ExternalRequester?.Email ?? string.Empty,
            RequesterType = ticket.ExternalRequesterId.HasValue
                ? "ExternalRequester"
                : "RegisteredCustomer",
            AssignedUserId = ticket.AssignedUserId,
            AssignedUserName = ticket.AssignedUser == null
                ? null
                : ticket.AssignedUser.FirstName + " " + ticket.AssignedUser.LastName,
            Category = CreateClassificationItemResponse(ticket.Category),
            Tags = includeTags
                ? CreateClassificationTagResponses(ticket.TagAssignments.Select(assignment => assignment.Tag))
                : [],
            IsAwaitingStaffReply = aging.IsAwaitingStaffReply,
            AwaitingStaffSince = aging.AwaitingStaffSince,
            AwaitingStaffMinutes = aging.AwaitingStaffMinutes,
            TicketAgeMinutes = aging.TicketAgeMinutes,
            AgingBucket = aging.AgingBucket,
            Sla = _ticketSlaCalculator.CalculateSummary(ticket)
        };

    private TicketDetailResponse CreateTicketDetailResponse(Ticket ticket, bool includeTags = true) =>
        CreateTicketDetailResponse(ticket, CalculateQueueAging(ticket), includeTags);

    private TicketDetailResponse CreateTicketDetailResponse(
        Ticket ticket,
        QueueAgingResult aging,
        bool includeTags = true) =>
        new()
        {
            Id = ticket.Id,
            TicketNumber = ticket.TicketNumber,
            Title = ticket.Title,
            Description = ticket.Description,
            Status = ticket.Status,
            Priority = ticket.Priority,
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt,
            ClosedAt = ticket.ClosedAt,
            CustomerId = ticket.CustomerId,
            ExternalRequesterId = ticket.ExternalRequesterId,
            CustomerName = ticket.Customer != null
                ? ticket.Customer.FirstName + " " + ticket.Customer.LastName
                : ticket.ExternalRequester?.DisplayName ?? ticket.ExternalRequester?.Email ?? string.Empty,
            CustomerEmail = ticket.Customer?.Email ?? ticket.ExternalRequester?.Email ?? string.Empty,
            RequesterType = ticket.ExternalRequesterId.HasValue
                ? "ExternalRequester"
                : "RegisteredCustomer",
            AssignedUserId = ticket.AssignedUserId,
            AssignedUserName = ticket.AssignedUser == null
                ? null
                : ticket.AssignedUser.FirstName + " " + ticket.AssignedUser.LastName,
            Category = CreateClassificationItemResponse(ticket.Category),
            Tags = includeTags
                ? CreateClassificationTagResponses(ticket.TagAssignments.Select(assignment => assignment.Tag))
                : [],
            IsAwaitingStaffReply = aging.IsAwaitingStaffReply,
            AwaitingStaffSince = aging.AwaitingStaffSince,
            AwaitingStaffMinutes = aging.AwaitingStaffMinutes,
            TicketAgeMinutes = aging.TicketAgeMinutes,
            AgingBucket = aging.AgingBucket,
            Sla = _ticketSlaCalculator.Calculate(ticket)
        };

    private QueueAgingResult CalculateQueueAging(Ticket ticket) =>
        _queueAgingCalculator.Calculate(
            new QueueAgingTicketInput(ticket.Status, ticket.CreatedAt),
            ticket.Messages.Select(message => new QueueAgingMessageInput(
                message.Sender?.Role == UserRole.Customer || message.ExternalRequesterId.HasValue,
                message.Sender?.Role is UserRole.Admin or UserRole.TechnicalStaff,
                message.IsInternal,
                message.OutboundDeliveryStatus,
                message.CreatedAt,
                message.DeliveredAt)).ToArray());

    private IQueryable<Ticket> ApplyListFilters(
        IQueryable<Ticket> query,
        int currentUserId,
        UserRole currentUserRole,
        TicketPriority? priority,
        TicketStatus? status,
        int? assignedUserId,
        TicketAssignmentFilter? assignmentState,
        bool activeOnly,
        string? search,
        int? categoryId,
        TicketClassificationFilter? classificationState,
        int? tagId)
    {
        if (currentUserRole == UserRole.Customer)
        {
            query = query.Where(ticket => ticket.CustomerId == currentUserId);
        }

        if (priority.HasValue) query = query.Where(ticket => ticket.Priority == priority.Value);
        if (status.HasValue) query = query.Where(ticket => ticket.Status == status.Value);
        if (assignedUserId.HasValue) query = query.Where(ticket => ticket.AssignedUserId == assignedUserId.Value);
        if (assignmentState == TicketAssignmentFilter.Unassigned)
        {
            query = query.Where(ticket => ticket.AssignedUserId == null);
        }
        else if (assignmentState == TicketAssignmentFilter.InactiveAssignee)
        {
            query = query.Where(ticket => ticket.AssignedUserId != null
                && ticket.AssignedUser != null
                && ticket.AssignedUser.Role == UserRole.TechnicalStaff
                && !ticket.AssignedUser.IsActive);
        }

        if (activeOnly) query = query.Where(ticket => TicketLifecycle.ActiveStatuses.Contains(ticket.Status));
        if (categoryId.HasValue) query = query.Where(ticket => ticket.CategoryId == categoryId.Value);
        if (classificationState == TicketClassificationFilter.Unclassified)
        {
            query = query.Where(ticket => ticket.CategoryId == null);
        }
        if (tagId.HasValue)
        {
            query = query.Where(ticket => ticket.TagAssignments.Any(assignment => assignment.TagId == tagId.Value));
        }

        var normalizedSearch = search?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            query = query.Where(ticket =>
                ticket.TicketNumber.Contains(normalizedSearch)
                || ticket.Title.Contains(normalizedSearch)
                || ticket.Customer != null && (
                    ticket.Customer.FirstName.Contains(normalizedSearch)
                    || ticket.Customer.LastName.Contains(normalizedSearch)
                    || ticket.Customer.Email.Contains(normalizedSearch))
                || ticket.ExternalRequester != null && (
                    ticket.ExternalRequester.Email.Contains(normalizedSearch)
                    || ticket.ExternalRequester.DisplayName != null
                        && ticket.ExternalRequester.DisplayName.Contains(normalizedSearch)));
        }

        return query;
    }

    private static TicketClassificationResponse CreateClassificationResponse(
        TicketCategory? category,
        IEnumerable<TicketTag?> tags) =>
        new()
        {
            Category = CreateClassificationItemResponse(category),
            Tags = CreateClassificationTagResponses(tags)
        };

    private static TicketClassificationItemResponse? CreateClassificationItemResponse(TicketCategory? category) =>
        category is null
            ? null
            : new TicketClassificationItemResponse
            {
                Id = category.Id,
                Name = category.Name,
                IsActive = category.IsActive
            };

    private static IReadOnlyList<TicketClassificationItemResponse> CreateClassificationTagResponses(
        IEnumerable<TicketTag?> tags) =>
        tags
            .Where(tag => tag is not null)
            .Select(tag => new TicketClassificationItemResponse
            {
                Id = tag!.Id,
                Name = tag.Name,
                IsActive = tag.IsActive
            })
            .OrderBy(tag => tag.Name)
            .ThenBy(tag => tag.Id)
            .ToArray();

    private static IOrderedQueryable<Ticket> ApplySqlSort(
        IQueryable<Ticket> query,
        TicketListSort? sort) =>
        sort switch
        {
            TicketListSort.OldestTicket => query.OrderBy(ticket => ticket.CreatedAt).ThenBy(ticket => ticket.Id),
            TicketListSort.UpdatedNewest => query.OrderByDescending(ticket => ticket.UpdatedAt).ThenByDescending(ticket => ticket.Id),
            TicketListSort.UpdatedOldest => query.OrderBy(ticket => ticket.UpdatedAt).ThenBy(ticket => ticket.Id),
            _ => query.OrderByDescending(ticket => ticket.CreatedAt).ThenByDescending(ticket => ticket.Id)
        };

    private static DateTime? GetAgingCutoff(TicketAgingFilter? agingFilter, DateTime now) =>
        agingFilter switch
        {
            TicketAgingFilter.WaitingOver2Hours => now.AddHours(-2),
            TicketAgingFilter.WaitingOver8Hours => now.AddHours(-8),
            TicketAgingFilter.WaitingOver24Hours => now.AddHours(-24),
            _ => null
        };

    private static QueueAgingProjectionSort ToQueueAgingProjectionSort(TicketListSort? sort) =>
        sort switch
        {
            TicketListSort.LongestWaiting => QueueAgingProjectionSort.LongestWaiting,
            TicketListSort.ShortestWaiting => QueueAgingProjectionSort.ShortestWaiting,
            TicketListSort.OldestTicket => QueueAgingProjectionSort.OldestTicket,
            _ => QueueAgingProjectionSort.NewestTicket
        };

    private async Task<Dictionary<int, QueueAgingResult>> GetQueueAgingAsync(
        IReadOnlyCollection<Ticket> tickets,
        CancellationToken cancellationToken)
    {
        if (tickets.Count == 0) return [];

        var ticketIds = tickets.Select(ticket => ticket.Id).ToArray();
        var messages = await _dbContext.TicketMessages
            .AsNoTracking()
            .Where(message => ticketIds.Contains(message.TicketId) && !message.IsInternal)
            .Select(message => new QueueAgingMessageProjection(
                message.TicketId,
                message.Sender != null && message.Sender.Role == UserRole.Customer
                    || message.ExternalRequesterId.HasValue,
                message.Sender != null && (message.Sender.Role == UserRole.Admin
                    || message.Sender.Role == UserRole.TechnicalStaff),
                message.OutboundDeliveryStatus,
                message.CreatedAt,
                message.DeliveredAt))
            .ToListAsync(cancellationToken);
        var byTicket = messages.GroupBy(message => message.TicketId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        return tickets.ToDictionary(ticket => ticket.Id, ticket =>
            _queueAgingCalculator.Calculate(
                new QueueAgingTicketInput(ticket.Status, ticket.CreatedAt),
                byTicket.GetValueOrDefault(ticket.Id, [])
                    .Select(message => new QueueAgingMessageInput(
                        message.IsRequesterActivity,
                        message.IsPublicStaffReply,
                        false,
                        message.DeliveryStatus,
                        message.CreatedAt,
                        message.DeliveredAt))
                    .ToArray()));
    }

    private static QueueAgingResult CreateTicketAgeOnlyResult(Ticket ticket) =>
        new(false, null, null, 0, null);

    private static PagedResponse<TicketListItemResponse> CreatePagedResponse(
        IReadOnlyList<TicketListItemResponse> items,
        int page,
        int pageSize,
        int totalCount)
    {
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        return new PagedResponse<TicketListItemResponse>
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

    private static bool MatchesAgingFilter(
        QueueAgingResult aging,
        TicketAgingFilter? agingFilter) =>
        agingFilter switch
        {
            TicketAgingFilter.Awaiting => aging.IsAwaitingStaffReply,
            TicketAgingFilter.WaitingOver2Hours => aging.AwaitingStaffMinutes >= 120,
            TicketAgingFilter.WaitingOver8Hours => aging.AwaitingStaffMinutes >= 480,
            TicketAgingFilter.WaitingOver24Hours => aging.AwaitingStaffMinutes >= 1440,
            _ => true
        };

    private static IOrderedEnumerable<TicketWithAging> OrderTickets(
        IEnumerable<TicketWithAging> candidates,
        TicketListSort? sort) =>
        sort switch
        {
            TicketListSort.OldestTicket => candidates
                .OrderBy(candidate => candidate.Ticket.CreatedAt)
                .ThenBy(candidate => candidate.Ticket.Id),
            TicketListSort.LongestWaiting => candidates
                .OrderBy(candidate => !candidate.Aging.IsAwaitingStaffReply)
                .ThenBy(candidate => candidate.Aging.AwaitingStaffSince)
                .ThenBy(candidate => candidate.Ticket.CreatedAt)
                .ThenBy(candidate => candidate.Ticket.Id),
            TicketListSort.ShortestWaiting => candidates
                .OrderBy(candidate => !candidate.Aging.IsAwaitingStaffReply)
                .ThenByDescending(candidate => candidate.Aging.AwaitingStaffSince)
                .ThenBy(candidate => candidate.Ticket.CreatedAt)
                .ThenBy(candidate => candidate.Ticket.Id),
            _ => candidates
                .OrderByDescending(candidate => candidate.Ticket.CreatedAt)
                .ThenBy(candidate => candidate.Ticket.Id)
        };

    private sealed record TicketWithAging(Ticket Ticket, QueueAgingResult Aging);
    private sealed record QueueAgingMessageProjection(
        int TicketId,
        bool IsRequesterActivity,
        bool IsPublicStaffReply,
        OutboundDeliveryStatus DeliveryStatus,
        DateTime CreatedAt,
        DateTime? DeliveredAt);

    private static bool IsStaffPublicReply(UserRole senderRole, bool isInternal) =>
        !isInternal && senderRole is UserRole.Admin or UserRole.TechnicalStaff;

    private static TicketMessageResponse CreateTicketMessageResponse(
        TicketMessage message,
        User sender) =>
        new()
        {
            Id = message.Id,
            TicketId = message.TicketId,
            SenderId = sender.Id,
            SenderName = sender.FirstName + " " + sender.LastName,
            SenderRole = sender.Role,
            Message = message.Message,
            IsInternal = message.IsInternal,
            DeliveryStatus = message.OutboundDeliveryStatus,
            DeliveryAttemptCount = message.DeliveryAttemptCount,
            DeliveredAt = message.DeliveredAt,
            LastDeliveryAttemptAt = message.LastDeliveryAttemptAt,
            SafeDeliveryError = message.OutboundDeliveryStatus == OutboundDeliveryStatus.Failed
                ? message.OutboundDeliveryError
                : null,
            CreatedAt = message.CreatedAt
        };

    private async Task<TicketOperationResult<TicketMessageResponse>> DeliverStaffPublicReplyAsync(
        Ticket ticket,
        User sender,
        TicketMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            if (ticket.CustomerId.HasValue)
            {
                await SendCustomerTechnicalReplyAsync(ticket, sender, message.Message, cancellationToken);
            }
            else if (ticket.ExternalRequesterId.HasValue)
            {
                var requester = await _dbContext.ExternalRequesters
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        item => item.Id == ticket.ExternalRequesterId.Value,
                        cancellationToken);

                if (requester is null)
                {
                    throw new InvalidOperationException("The external requester is unavailable.");
                }

                await SendExternalRequesterTechnicalReplyAsync(
                    ticket,
                    requester,
                    sender,
                    message.Message,
                    cancellationToken);
            }
            else
            {
                throw new InvalidOperationException("The ticket has no email recipient.");
            }
        }
        catch (Exception exception)
        {
            message.OutboundDeliveryStatus = OutboundDeliveryStatus.Failed;
            message.OutboundDeliveryError = "E-posta gönderilemedi. Lütfen yeniden deneyin.";
            message.DeliveredAt = null;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogError(
                exception,
                "Public ticket reply delivery failed. Ticket: {TicketNumber}; MessageId: {TicketMessageId}",
                ticket.TicketNumber,
                message.Id);

            return TicketOperationResult<TicketMessageResponse>.PartiallySucceeded(
                CreateTicketMessageResponse(message, sender),
                "Yanıt talebe kaydedildi ancak e-posta gönderilemedi. Mesaj üzerinden yeniden deneyebilirsiniz.");
        }

        message.OutboundDeliveryStatus = OutboundDeliveryStatus.Sent;
        message.OutboundDeliveryError = null;
        message.DeliveredAt = DateTime.UtcNow;
        _ticketSlaService.RecordFirstSuccessfulStaffResponse(
            ticket,
            message,
            sender.Role);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (ticket.CustomerId.HasValue)
        {
            await _notificationService.NotifyTechnicalReplyAsync(ticket, sender, cancellationToken);
        }

        return TicketOperationResult<TicketMessageResponse>.Succeeded(
            CreateTicketMessageResponse(message, sender));
    }

    private async Task SendCustomerTechnicalReplyAsync(
        Ticket ticket,
        User sender,
        string message,
        CancellationToken cancellationToken)
    {
        var customerEmail = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == ticket.CustomerId)
            .Select(user => user.Email)
            .SingleAsync(cancellationToken);
        var content = _emailComposer.ComposeTechnicalReply(ticket, sender, message);
        await _gmailSendService.SendPlainTextAsync(
            customerEmail,
            content.Subject,
            content.Body,
            cancellationToken);
    }

    internal async Task TryNotifyTechnicalStaffOfCustomerReplyAsync(
        Ticket ticket,
        User customer,
        string message,
        bool wasReopened,
        CancellationToken cancellationToken)
    {
        await TryRunNotificationAsync(ticket.TicketNumber, async () =>
        {
            IReadOnlyList<string> recipients;

            if (ticket.AssignedUserId.HasValue)
            {
                recipients = await _dbContext.Users
                    .AsNoTracking()
                    .Where(user => user.Id == ticket.AssignedUserId.Value
                        && user.IsActive
                        && user.Role == UserRole.TechnicalStaff)
                    .Select(user => user.Email)
                    .ToListAsync(cancellationToken);
            }
            else
            {
                recipients = await GetActiveTechnicalStaffEmailsAsync(cancellationToken);
            }

            var content = _emailComposer.ComposeCustomerReply(
                ticket,
                customer,
                message,
                wasReopened);
            await SendToRecipientsAsync(recipients, content, cancellationToken);
        });
    }

    internal async Task TryNotifyTechnicalStaffOfExternalRequesterReplyAsync(
        Ticket ticket,
        ExternalRequester requester,
        string message,
        bool wasReopened,
        CancellationToken cancellationToken)
    {
        await TryRunNotificationAsync(ticket.TicketNumber, async () =>
        {
            var recipients = ticket.AssignedUserId.HasValue
                ? await _dbContext.Users
                    .AsNoTracking()
                    .Where(user => user.Id == ticket.AssignedUserId.Value
                        && user.IsActive
                        && user.Role == UserRole.TechnicalStaff)
                    .Select(user => user.Email)
                    .ToListAsync(cancellationToken)
                : await GetActiveTechnicalStaffEmailsAsync(cancellationToken);

            var content = _emailComposer.ComposeNewTicket(ticket, requester);
            await SendToRecipientsAsync(recipients, content with
            {
                Subject = wasReopened
                    ? $"{ticket.TicketNumber} talebi yeniden aÃ§Ä±ldÄ±"
                    : $"{ticket.TicketNumber} talebine harici mÃ¼ÅŸteri yanÄ±tÄ± geldi"
            }, cancellationToken);
        });
    }

    private async Task SendExternalRequesterTechnicalReplyAsync(
        Ticket ticket,
        ExternalRequester requester,
        User sender,
        string message,
        CancellationToken cancellationToken)
    {
        var content = _emailComposer.ComposeExternalRequesterTechnicalReply(
            ticket,
            sender,
            message);
        await _gmailSendService.SendPlainTextAsync(
            requester.Email,
            content.Subject,
            content.Body,
            cancellationToken);
    }

    private async Task TryNotifyAssignmentAsync(
        Ticket ticket,
        User assignedUser,
        CancellationToken cancellationToken)
    {
        await TryRunNotificationAsync(ticket.TicketNumber, async () =>
        {
            var customer = await _dbContext.Users
                .AsNoTracking()
                .SingleAsync(user => user.Id == ticket.CustomerId, cancellationToken);
            var content = _emailComposer.ComposeAssignment(ticket, customer, assignedUser);
            await _emailService.SendAsync(
                assignedUser.Email,
                content.Subject,
                content.Body,
                cancellationToken);
        });
    }

    private async Task TryNotifyCustomerOfStatusChangeAsync(
        Ticket ticket,
        TicketStatus oldStatus,
        TicketStatus newStatus,
        string? note,
        CancellationToken cancellationToken)
    {
        await TryRunNotificationAsync(ticket.TicketNumber, async () =>
        {
            var customerEmail = await _dbContext.Users
                .AsNoTracking()
                .Where(user => user.Id == ticket.CustomerId)
                .Select(user => user.Email)
                .SingleAsync(cancellationToken);
            var content = _emailComposer.ComposeStatusChange(
                ticket,
                oldStatus,
                newStatus,
                note);
            await _emailService.SendAsync(
                customerEmail,
                content.Subject,
                content.Body,
                cancellationToken);
        });
    }

    private async Task<IReadOnlyList<string>> GetActiveTechnicalStaffEmailsAsync(
        CancellationToken cancellationToken)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.IsActive && user.Role == UserRole.TechnicalStaff)
            .Select(user => user.Email)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private async Task SendToRecipientsAsync(
        IReadOnlyList<string> recipients,
        EmailContent content,
        CancellationToken cancellationToken)
    {
        foreach (var recipient in recipients.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await _emailService.SendAsync(
                recipient,
                content.Subject,
                content.Body,
                cancellationToken);
        }
    }

    private async Task TryRunNotificationAsync(
        string ticketNumber,
        Func<Task> notificationAction)
    {
        try
        {
            await notificationAction();
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Ticket email notification failed after the ticket operation committed. Ticket: {TicketNumber}",
                ticketNumber);
        }
    }

    private static bool ShouldNotifyCustomerOfStatus(TicketStatus status) =>
        status is TicketStatus.InProgress
            or TicketStatus.WaitingForCustomer
            or TicketStatus.Resolved
            or TicketStatus.Closed
            or TicketStatus.Reopened;

    private async Task<User?> GetActiveUserAsync(
        int userId,
        UserRole expectedRole,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                user => user.Id == userId
                    && user.IsActive
                    && user.Role == expectedRole,
                cancellationToken);
    }

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

    private void AppendUserActivity(
        Ticket ticket,
        TicketActivityType activityType,
        TicketActivityVisibility visibility,
        DateTime occurredAt,
        User actor,
        TicketActivityMetadata? metadata = null,
        int? relatedStatusHistoryId = null,
        int? relatedMessageId = null,
        int? relatedAttachmentId = null,
        TicketActivitySource source = TicketActivitySource.Portal) =>
        _ticketActivityWriter.Append(new TicketActivityAppendRequest(
            ticket.Id,
            activityType,
            source,
            visibility,
            occurredAt,
            TicketActivityActorKind.User,
            actor.Id,
            null,
            GetDisplayName(actor),
            actor.Role,
            relatedStatusHistoryId,
            relatedMessageId,
            relatedAttachmentId,
            metadata));

    private void AppendStatusChangedActivity(
        Ticket ticket,
        TicketStatus? oldStatus,
        TicketActivitySource source,
        DateTime occurredAt,
        User actor,
        int? relatedStatusHistoryId)
    {
        if (!oldStatus.HasValue)
        {
            return;
        }

        AppendUserActivity(
            ticket,
            TicketActivityType.StatusChanged,
            TicketActivityVisibility.RequesterVisible,
            occurredAt,
            actor,
            new StatusChangedTicketActivityMetadata(oldStatus.Value, ticket.Status),
            relatedStatusHistoryId,
            source: source);
    }

    private static string GetDisplayName(User user) =>
        $"{user.FirstName} {user.LastName}".Trim();

    private static bool IsAllowedTransition(TicketStatus oldStatus, TicketStatus newStatus)
    {
        return oldStatus switch
        {
            TicketStatus.New => newStatus is TicketStatus.Assigned
                or TicketStatus.InProgress
                or TicketStatus.WaitingForCustomer
                or TicketStatus.Resolved
                or TicketStatus.Closed,
            TicketStatus.Assigned => newStatus is TicketStatus.InProgress
                or TicketStatus.WaitingForCustomer
                or TicketStatus.Resolved
                or TicketStatus.Closed,
            TicketStatus.InProgress => newStatus is TicketStatus.WaitingForCustomer
                or TicketStatus.Resolved
                or TicketStatus.Closed,
            TicketStatus.WaitingForCustomer => newStatus is TicketStatus.InProgress
                or TicketStatus.Resolved
                or TicketStatus.Closed,
            TicketStatus.Resolved => newStatus is TicketStatus.Closed
                or TicketStatus.InProgress
                or TicketStatus.Reopened,
            TicketStatus.Closed => newStatus == TicketStatus.Reopened,
            TicketStatus.Reopened => newStatus is TicketStatus.Assigned
                or TicketStatus.InProgress
                or TicketStatus.WaitingForCustomer
                or TicketStatus.Resolved
                or TicketStatus.Closed,
            _ => false
        };
    }
}

internal sealed record TicketMessageWriteResult(
    TicketMessage Message,
    bool WasReopened,
    TicketStatus? ReopenedFromStatus,
    TicketStatusHistory? StatusHistory);

public enum TicketOperationFailure
{
    None,
    NotFound,
    InvalidRequest,
    Forbidden,
    Unauthorized,
    DeliveryFailed
}

public sealed record TicketOperationResult<T>(
    T? Value,
    TicketOperationFailure Failure,
    string? Error)
    where T : class
{
    public static TicketOperationResult<T> Succeeded(T value) =>
        new(value, TicketOperationFailure.None, null);

    public static TicketOperationResult<T> Failed(
        TicketOperationFailure failure,
        string? error = null) =>
        new(null, failure, error);

    public static TicketOperationResult<T> PartiallySucceeded(T value, string error) =>
        new(value, TicketOperationFailure.DeliveryFailed, error);
}
