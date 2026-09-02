using Microsoft.EntityFrameworkCore;
using SupportFlow.Api.Data;
using SupportFlow.Api.DTOs.Dashboard;
using SupportFlow.Api.DTOs.Sla;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;
using SupportFlow.Api.Services.QueueAging;
using SupportFlow.Api.Services.Sla;

namespace SupportFlow.Api.Services;

public sealed class AgentWorkloadService(
    AppDbContext dbContext,
    IQueueAgingCalculator queueAgingCalculator,
    ITicketSlaCalculator ticketSlaCalculator,
    TimeProvider timeProvider) : IAgentWorkloadService
{
    public async Task<AgentWorkloadResponse> GetSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        var agents = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.IsActive && user.Role == UserRole.TechnicalStaff)
            .Select(user => new AgentProjection(
                user.Id,
                user.FirstName + " " + user.LastName,
                user.Email))
            .ToListAsync(cancellationToken);
        var agentIds = agents.Select(agent => agent.Id).ToHashSet();

        var tickets = await dbContext.Tickets
            .AsNoTracking()
            .Where(ticket => TicketLifecycle.ActiveStatuses.Contains(ticket.Status))
            .Select(ticket => new TicketProjection(
                ticket.Id,
                ticket.AssignedUserId,
                ticket.AssignedUser == null ? null : (UserRole?)ticket.AssignedUser.Role,
                ticket.AssignedUser == null ? null : ticket.AssignedUser.IsActive,
                ticket.Status,
                ticket.Priority,
                ticket.CreatedAt,
                ticket.FirstResponseDueAt,
                ticket.FirstRespondedAt,
                ticket.ResolutionCycleStartedAt,
                ticket.ResolutionDueAt,
                ticket.ResolvedAt))
            .ToListAsync(cancellationToken);
        var ticketIds = tickets.Select(ticket => ticket.Id).ToArray();
        var messages = ticketIds.Length == 0
            ? []
            : await dbContext.TicketMessages
                .AsNoTracking()
                .Where(message => ticketIds.Contains(message.TicketId) && !message.IsInternal)
                .Select(message => new MessageProjection(
                    message.TicketId,
                    message.Sender != null ? (UserRole?)message.Sender.Role : null,
                    message.ExternalRequesterId.HasValue,
                    message.OutboundDeliveryStatus,
                    message.DeliveredAt,
                    message.CreatedAt))
                .ToListAsync(cancellationToken);
        var messagesByTicket = messages
            .GroupBy(message => message.TicketId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var workloadTickets = tickets
            .Select(ticket => new WorkloadTicket(
                ticket,
                queueAgingCalculator.Calculate(
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
                ticketSlaCalculator.CalculateSummary(ToSlaTicket(ticket))))
            .ToArray();

        return new AgentWorkloadResponse
        {
            GeneratedAt = timeProvider.GetUtcNow().UtcDateTime,
            Totals = CreateTotals(workloadTickets),
            Agents = agents
                .Select(agent => CreateAgent(agent, workloadTickets.Where(ticket => ticket.Ticket.AssignedUserId == agent.Id)))
                .OrderByDescending(agent => agent.SlaBreachedCount)
                .ThenByDescending(agent => agent.CriticalTicketCount)
                .ThenByDescending(agent => agent.WaitingOver8HoursCount)
                .ThenByDescending(agent => agent.ActiveTicketCount)
                .ThenBy(agent => agent.FullName)
                .ThenBy(agent => agent.UserId)
                .ToArray()
        };
    }

    private static AgentWorkloadTotalsResponse CreateTotals(IReadOnlyCollection<WorkloadTicket> tickets) =>
        new()
        {
            ActiveTicketCount = tickets.Count,
            UnassignedTicketCount = tickets.Count(ticket => !ticket.Ticket.AssignedUserId.HasValue),
            InactiveAssigneeTicketCount = tickets.Count(ticket =>
                ticket.Ticket.AssignedUserRole == UserRole.TechnicalStaff
                && ticket.Ticket.AssignedUserIsActive == false),
            InvalidAssigneeTicketCount = tickets.Count(ticket =>
                ticket.Ticket.AssignedUserId.HasValue
                && ticket.Ticket.AssignedUserRole != UserRole.TechnicalStaff),
            CriticalTicketCount = tickets.Count(ticket => ticket.Ticket.Priority == TicketPriority.Critical),
            HighPriorityTicketCount = tickets.Count(ticket => ticket.Ticket.Priority == TicketPriority.High),
            AwaitingReplyCount = tickets.Count(ticket => ticket.Aging.IsAwaitingStaffReply),
            WaitingOver2HoursCount = tickets.Count(ticket => ticket.Aging.AwaitingStaffMinutes >= 120),
            WaitingOver8HoursCount = tickets.Count(ticket => ticket.Aging.AwaitingStaffMinutes >= 480),
            WaitingOver24HoursCount = tickets.Count(ticket => ticket.Aging.AwaitingStaffMinutes >= 1440),
            SlaBreachedCount = tickets.Count(ticket => ticket.Sla.MostUrgentState == SlaState.Breached),
            SlaDueSoonCount = tickets.Count(ticket => ticket.Sla.MostUrgentState == SlaState.DueSoon)
        };

    private static AgentWorkloadAgentResponse CreateAgent(
        AgentProjection agent,
        IEnumerable<WorkloadTicket> source)
    {
        var tickets = source.ToArray();
        var awaitingMinutes = tickets
            .Where(ticket => ticket.Aging.IsAwaitingStaffReply)
            .Select(ticket => ticket.Aging.AwaitingStaffMinutes)
            .Where(minutes => minutes.HasValue)
            .Select(minutes => minutes!.Value)
            .ToArray();

        return new AgentWorkloadAgentResponse
        {
            UserId = agent.Id,
            FullName = agent.FullName,
            Email = agent.Email,
            ActiveTicketCount = tickets.Length,
            CriticalTicketCount = tickets.Count(ticket => ticket.Ticket.Priority == TicketPriority.Critical),
            HighPriorityTicketCount = tickets.Count(ticket => ticket.Ticket.Priority == TicketPriority.High),
            AwaitingReplyCount = tickets.Count(ticket => ticket.Aging.IsAwaitingStaffReply),
            WaitingOver8HoursCount = tickets.Count(ticket => ticket.Aging.AwaitingStaffMinutes >= 480),
            WaitingOver24HoursCount = tickets.Count(ticket => ticket.Aging.AwaitingStaffMinutes >= 1440),
            LongestAwaitingMinutes = awaitingMinutes.Length == 0 ? null : awaitingMinutes.Max(),
            SlaBreachedCount = tickets.Count(ticket => ticket.Sla.MostUrgentState == SlaState.Breached),
            SlaDueSoonCount = tickets.Count(ticket => ticket.Sla.MostUrgentState == SlaState.DueSoon)
        };
    }

    private static Ticket ToSlaTicket(TicketProjection ticket) =>
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

    private sealed record AgentProjection(int Id, string FullName, string Email);
    private sealed record TicketProjection(
        int Id,
        int? AssignedUserId,
        UserRole? AssignedUserRole,
        bool? AssignedUserIsActive,
        TicketStatus Status,
        TicketPriority Priority,
        DateTime CreatedAt,
        DateTime? FirstResponseDueAt,
        DateTime? FirstRespondedAt,
        DateTime? ResolutionCycleStartedAt,
        DateTime? ResolutionDueAt,
        DateTime? ResolvedAt);
    private sealed record MessageProjection(
        int TicketId,
        UserRole? SenderRole,
        bool IsExternalRequester,
        OutboundDeliveryStatus DeliveryStatus,
        DateTime? DeliveredAt,
        DateTime CreatedAt);
    private sealed record WorkloadTicket(
        TicketProjection Ticket,
        QueueAgingResult Aging,
        TicketSlaSummaryResponse Sla);
}
