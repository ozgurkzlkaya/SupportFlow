using Microsoft.EntityFrameworkCore;
using SupportFlow.Api.Data;
using SupportFlow.Api.DTOs.Dashboard;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;
using SupportFlow.Api.Services.QueueAging;
using SupportFlow.Api.Services.Sla;

namespace SupportFlow.Api.Services;

public sealed class DashboardService(
    AppDbContext dbContext,
    IQueueAgingCalculator queueAgingCalculator,
    ITicketSlaCalculator ticketSlaCalculator)
{
    public async Task<QueueAgingDashboardResponse> GetQueueAgingAsync(
        CancellationToken cancellationToken = default)
    {
        var tickets = await dbContext.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.Status != TicketStatus.Resolved && ticket.Status != TicketStatus.Closed)
            .Select(ticket => new DashboardTicketProjection(
                ticket.Id,
                ticket.TicketNumber,
                ticket.Title,
                ticket.Status,
                ticket.Priority,
                ticket.CreatedAt,
                ticket.AssignedUser == null
                    ? null
                    : ticket.AssignedUser.FirstName + " " + ticket.AssignedUser.LastName,
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
                .Select(message => new DashboardMessageProjection(
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

        var awaiting = tickets
            .Select(ticket => new DashboardTicketWithAging(
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
                        .ToArray())))
            .Where(item => item.Aging.IsAwaitingStaffReply)
            .ToList();

        var oldest = awaiting
            .OrderBy(item => item.Aging.AwaitingStaffSince)
            .ThenBy(item => item.Ticket.CreatedAt)
            .ThenBy(item => item.Ticket.Id)
            .Take(5)
            .Select(item => new OldestWaitingTicketResponse
            {
                TicketId = item.Ticket.Id,
                TicketNumber = item.Ticket.TicketNumber,
                Title = item.Ticket.Title,
                Priority = item.Ticket.Priority,
                AssignedUserName = item.Ticket.AssignedUserName,
                AwaitingStaffSince = item.Aging.AwaitingStaffSince!.Value,
                AwaitingStaffMinutes = item.Aging.AwaitingStaffMinutes!.Value,
                AgingBucket = item.Aging.AgingBucket!.Value,
                SlaMostUrgentState = ticketSlaCalculator.CalculateSummary(ToSlaTicket(item.Ticket)).MostUrgentState
            })
            .ToList();

        return new QueueAgingDashboardResponse
        {
            AwaitingReplyCount = awaiting.Count,
            WaitingOver2HoursCount = awaiting.Count(item => item.Aging.AwaitingStaffMinutes >= 120),
            WaitingOver8HoursCount = awaiting.Count(item => item.Aging.AwaitingStaffMinutes >= 480),
            WaitingOver24HoursCount = awaiting.Count(item => item.Aging.AwaitingStaffMinutes >= 1440),
            OldestWaitingTickets = oldest
        };
    }

    private static Ticket ToSlaTicket(DashboardTicketProjection ticket) =>
        new()
        {
            Id = ticket.Id,
            TicketNumber = ticket.TicketNumber,
            Title = ticket.Title,
            Status = ticket.Status,
            Priority = ticket.Priority,
            CreatedAt = ticket.CreatedAt,
            FirstResponseDueAt = ticket.FirstResponseDueAt,
            FirstRespondedAt = ticket.FirstRespondedAt,
            ResolutionCycleStartedAt = ticket.ResolutionCycleStartedAt,
            ResolutionDueAt = ticket.ResolutionDueAt,
            ResolvedAt = ticket.ResolvedAt
        };

    private sealed record DashboardTicketProjection(
        int Id,
        string TicketNumber,
        string Title,
        TicketStatus Status,
        TicketPriority Priority,
        DateTime CreatedAt,
        string? AssignedUserName,
        DateTime? FirstResponseDueAt,
        DateTime? FirstRespondedAt,
        DateTime? ResolutionCycleStartedAt,
        DateTime? ResolutionDueAt,
        DateTime? ResolvedAt);
    private sealed record DashboardMessageProjection(
        int TicketId,
        UserRole? SenderRole,
        bool IsExternalRequester,
        OutboundDeliveryStatus DeliveryStatus,
        DateTime? DeliveredAt,
        DateTime CreatedAt);
    private sealed record DashboardTicketWithAging(
        DashboardTicketProjection Ticket,
        QueueAgingResult Aging);
}
