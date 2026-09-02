using SupportFlow.Api.Enums;

namespace SupportFlow.Api.DTOs.Tickets;

public sealed class TicketDashboardSummaryResponse
{
    public int TotalCount { get; init; }
    public int OpenCount { get; init; }
    public int NewCount { get; init; }
    public int InProgressCount { get; init; }
    public int WaitingForCustomerCount { get; init; }
    public int ReopenedCount { get; init; }
    public int ResolvedOrClosedCount { get; init; }
    public int LowPriorityCount { get; init; }
    public int NormalPriorityCount { get; init; }
    public int HighPriorityCount { get; init; }
    public int CriticalPriorityCount { get; init; }
    public int SlaBreachedCount { get; init; }
    public int SlaDueSoonCount { get; init; }
    public int FirstResponseBreachedCount { get; init; }
    public int ResolutionBreachedCount { get; init; }
    public IReadOnlyList<TicketDashboardRecentItemResponse> RecentTickets { get; init; } = [];
}

public sealed class TicketDashboardRecentItemResponse
{
    public int Id { get; init; }
    public string TicketNumber { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public TicketStatus Status { get; init; }
    public TicketPriority Priority { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public string CustomerName { get; init; } = string.Empty;
}
