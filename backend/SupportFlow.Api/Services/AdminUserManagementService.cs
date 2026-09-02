using System.Data;
using Microsoft.EntityFrameworkCore;
using SupportFlow.Api.Data;
using SupportFlow.Api.DTOs.AdminUsers;
using SupportFlow.Api.DTOs.Common;
using SupportFlow.Api.DTOs.Sla;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;
using SupportFlow.Api.Services.QueueAging;
using SupportFlow.Api.Services.Sla;

namespace SupportFlow.Api.Services;

public sealed class AdminUserManagementService(
    AppDbContext dbContext,
    IQueueAgingCalculator queueAgingCalculator,
    ITicketSlaCalculator ticketSlaCalculator,
    TimeProvider timeProvider)
{
    public async Task<PagedResponse<AdminUserListItemResponse>> GetUsersAsync(
        string? search,
        UserRole? role,
        UserAccountStatusFilter? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Users.AsNoTracking();

        if (role.HasValue)
        {
            query = query.Where(user => user.Role == role.Value);
        }

        if (status.HasValue)
        {
            var isActive = status.Value == UserAccountStatusFilter.Active;
            query = query.Where(user => user.IsActive == isActive);
        }

        var normalizedSearch = search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            query = query.Where(user =>
                user.FirstName.ToLower().Contains(normalizedSearch)
                || user.LastName.ToLower().Contains(normalizedSearch)
                || (user.FirstName + " " + user.LastName).ToLower().Contains(normalizedSearch)
                || user.Email.ToLower().Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(user => user.CreatedAt)
            .ThenByDescending(user => user.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new AdminUserListItemResponse
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = user.FirstName + " " + user.LastName,
                Email = user.Email,
                Role = user.Role,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                ActiveTicketCount = user.Role == UserRole.TechnicalStaff
                    ? dbContext.Tickets.Count(ticket =>
                        ticket.AssignedUserId == user.Id
                        && TicketLifecycle.ActiveStatuses.Contains(ticket.Status))
                    : 0
            })
            .ToListAsync(cancellationToken);

        return CreatePagedResponse(items, page, pageSize, totalCount);
    }

    public async Task<AdminUserDetailResponse?> GetUserAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var technicalStaffContext = user.Role == UserRole.TechnicalStaff
            ? await GetTechnicalStaffContextAsync(user.Id, cancellationToken)
            : null;
        var customerContext = user.Role == UserRole.Customer
            ? await GetCustomerContextAsync(user.Id, cancellationToken)
            : null;

        return new AdminUserDetailResponse
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = user.FirstName + " " + user.LastName,
            Email = user.Email,
            Role = user.Role,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            TechnicalStaffContext = technicalStaffContext,
            CustomerContext = customerContext
        };
    }

    public async Task<PagedResponse<AdminUserAuditResponse>?> GetUserAuditsAsync(
        int userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var userExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == userId, cancellationToken);
        if (!userExists)
        {
            return null;
        }

        var query = dbContext.UserAdminAudits
            .AsNoTracking()
            .Where(audit => audit.TargetUserId == userId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(audit => audit.OccurredAt)
            .ThenByDescending(audit => audit.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(audit => new AdminUserAuditResponse
            {
                Id = audit.Id,
                ActorUserId = audit.ActorUserId,
                ActorDisplayName = audit.ActorDisplayName,
                TargetUserId = audit.TargetUserId,
                TargetDisplayName = audit.TargetDisplayName,
                TargetEmail = audit.TargetEmail,
                TargetRole = audit.TargetRole,
                Action = audit.Action,
                OldIsActive = audit.OldIsActive,
                NewIsActive = audit.NewIsActive,
                OccurredAt = audit.OccurredAt
            })
            .ToListAsync(cancellationToken);

        return CreatePagedResponse(items, page, pageSize, totalCount);
    }

    public async Task<AdminUserStatusUpdateResult> UpdateStatusAsync(
        int actorUserId,
        int targetUserId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var actor = await dbContext.Users
            .SingleOrDefaultAsync(item => item.Id == actorUserId, cancellationToken);
        if (actor is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AdminUserStatusUpdateResult.ActorNotFound();
        }

        var user = await dbContext.Users
            .SingleOrDefaultAsync(item => item.Id == targetUserId, cancellationToken);
        if (user is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AdminUserStatusUpdateResult.NotFound();
        }

        if (!isActive && actorUserId == targetUserId)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AdminUserStatusUpdateResult.SelfDeactivation();
        }

        if (user.IsActive == isActive)
        {
            await transaction.CommitAsync(cancellationToken);
            return AdminUserStatusUpdateResult.Succeeded(ToStatusResponse(user));
        }

        if (!isActive && user.Role == UserRole.Admin)
        {
            var activeAdminCount = await dbContext.Users
                .CountAsync(item => item.IsActive && item.Role == UserRole.Admin, cancellationToken);
            if (activeAdminCount <= 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return AdminUserStatusUpdateResult.LastActiveAdmin();
            }
        }

        var oldIsActive = user.IsActive;
        var occurredAt = timeProvider.GetUtcNow().UtcDateTime;
        user.IsActive = isActive;
        user.UpdatedAt = occurredAt;
        dbContext.UserAdminAudits.Add(new UserAdminAudit
        {
            ActorUserId = actor.Id,
            ActorDisplayName = GetDisplayName(actor),
            TargetUserId = user.Id,
            TargetDisplayName = GetDisplayName(user),
            TargetEmail = user.Email,
            TargetRole = user.Role,
            Action = isActive ? UserAdminAuditAction.Activated : UserAdminAuditAction.Deactivated,
            OldIsActive = oldIsActive,
            NewIsActive = isActive,
            OccurredAt = occurredAt
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return AdminUserStatusUpdateResult.Succeeded(ToStatusResponse(user));
    }

    private async Task<TechnicalStaffUserContextResponse> GetTechnicalStaffContextAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        var tickets = await dbContext.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.AssignedUserId == userId
                && TicketLifecycle.ActiveStatuses.Contains(ticket.Status))
            .Select(ticket => new StaffTicketProjection(
                ticket.Id,
                ticket.Status,
                ticket.Priority,
                ticket.CreatedAt,
                ticket.FirstResponseDueAt,
                ticket.FirstRespondedAt,
                ticket.ResolutionCycleStartedAt,
                ticket.ResolutionDueAt,
                ticket.ResolvedAt))
            .ToListAsync(cancellationToken);

        if (tickets.Count == 0)
        {
            return new TechnicalStaffUserContextResponse();
        }

        var ticketIds = tickets.Select(ticket => ticket.Id).ToArray();
        var messages = await dbContext.TicketMessages
            .AsNoTracking()
            .Where(message => ticketIds.Contains(message.TicketId) && !message.IsInternal)
            .Select(message => new StaffMessageProjection(
                message.TicketId,
                message.Sender == null ? null : (UserRole?)message.Sender.Role,
                message.ExternalRequesterId.HasValue,
                message.OutboundDeliveryStatus,
                message.CreatedAt,
                message.DeliveredAt))
            .ToListAsync(cancellationToken);
        var messagesByTicket = messages
            .GroupBy(message => message.TicketId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var calculated = tickets.Select(ticket => new
        {
            Ticket = ticket,
            Aging = queueAgingCalculator.Calculate(
                new QueueAgingTicketInput(ticket.Status, ticket.CreatedAt),
                messagesByTicket.GetValueOrDefault(ticket.Id, [])
                    .Select(message => new QueueAgingMessageInput(
                        message.SenderRole == UserRole.Customer || message.IsExternalRequester,
                        message.SenderRole is UserRole.Admin or UserRole.TechnicalStaff,
                        false,
                        message.DeliveryStatus,
                        message.CreatedAt,
                        message.DeliveredAt))
                    .ToArray()),
            Sla = ticketSlaCalculator.CalculateSummary(ToSlaTicket(ticket))
        }).ToArray();

        return new TechnicalStaffUserContextResponse
        {
            ActiveTicketCount = calculated.Length,
            CriticalTicketCount = calculated.Count(item => item.Ticket.Priority == TicketPriority.Critical),
            AwaitingReplyCount = calculated.Count(item => item.Aging.IsAwaitingStaffReply),
            SlaBreachedCount = calculated.Count(item => item.Sla.MostUrgentState == SlaState.Breached)
        };
    }

    private async Task<CustomerUserContextResponse> GetCustomerContextAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        var counts = await dbContext.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.CustomerId == userId)
            .GroupBy(_ => 1)
            .Select(group => new CustomerUserContextResponse
            {
                TotalTicketCount = group.Count(),
                ActiveTicketCount = group.Count(ticket => TicketLifecycle.ActiveStatuses.Contains(ticket.Status)),
                ResolvedTicketCount = group.Count(ticket => ticket.Status == TicketStatus.Resolved),
                ClosedTicketCount = group.Count(ticket => ticket.Status == TicketStatus.Closed)
            })
            .SingleOrDefaultAsync(cancellationToken);

        return counts ?? new CustomerUserContextResponse();
    }

    private static PagedResponse<T> CreatePagedResponse<T>(
        IReadOnlyList<T> items,
        int page,
        int pageSize,
        int totalCount) => new()
        {
            Items = items,
            Pagination = new PaginationMetadata
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize),
                HasPreviousPage = page > 1,
                HasNextPage = (long)page * pageSize < totalCount
            }
        };

    private static string GetDisplayName(User user) => $"{user.FirstName} {user.LastName}";

    private static AdminUserStatusResponse ToStatusResponse(User user) => new()
    {
        Id = user.Id,
        IsActive = user.IsActive,
        UpdatedAt = user.UpdatedAt
    };

    private static Ticket ToSlaTicket(StaffTicketProjection ticket) => new()
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

    private sealed record StaffTicketProjection(
        int Id,
        TicketStatus Status,
        TicketPriority Priority,
        DateTime CreatedAt,
        DateTime? FirstResponseDueAt,
        DateTime? FirstRespondedAt,
        DateTime? ResolutionCycleStartedAt,
        DateTime? ResolutionDueAt,
        DateTime? ResolvedAt);

    private sealed record StaffMessageProjection(
        int TicketId,
        UserRole? SenderRole,
        bool IsExternalRequester,
        OutboundDeliveryStatus DeliveryStatus,
        DateTime CreatedAt,
        DateTime? DeliveredAt);
}

public enum AdminUserStatusUpdateFailure
{
    None,
    ActorNotFound,
    NotFound,
    SelfDeactivation,
    LastActiveAdmin
}

public sealed record AdminUserStatusUpdateResult(
    AdminUserStatusResponse? User,
    AdminUserStatusUpdateFailure Failure)
{
    public static AdminUserStatusUpdateResult Succeeded(AdminUserStatusResponse user) => new(user, AdminUserStatusUpdateFailure.None);
    public static AdminUserStatusUpdateResult ActorNotFound() => new(null, AdminUserStatusUpdateFailure.ActorNotFound);
    public static AdminUserStatusUpdateResult NotFound() => new(null, AdminUserStatusUpdateFailure.NotFound);
    public static AdminUserStatusUpdateResult SelfDeactivation() => new(null, AdminUserStatusUpdateFailure.SelfDeactivation);
    public static AdminUserStatusUpdateResult LastActiveAdmin() => new(null, AdminUserStatusUpdateFailure.LastActiveAdmin);
}
