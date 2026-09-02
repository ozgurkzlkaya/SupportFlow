using SupportFlow.Api.Enums;

namespace SupportFlow.Api.DTOs.Dashboard;

public sealed class QueueAgingDashboardResponse
{
    public int AwaitingReplyCount { get; init; }
    public int WaitingOver2HoursCount { get; init; }
    public int WaitingOver8HoursCount { get; init; }
    public int WaitingOver24HoursCount { get; init; }
    public IReadOnlyList<OldestWaitingTicketResponse> OldestWaitingTickets { get; init; } = [];
}

public sealed class OldestWaitingTicketResponse
{
    public int TicketId { get; init; }
    public string TicketNumber { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public TicketPriority Priority { get; init; }
    public string? AssignedUserName { get; init; }
    public DateTime AwaitingStaffSince { get; init; }
    public int AwaitingStaffMinutes { get; init; }
    public QueueAgingBucket AgingBucket { get; init; }
    public SlaState SlaMostUrgentState { get; init; }
}
