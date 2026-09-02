namespace SupportFlow.Api.DTOs.Dashboard;

public sealed class AgentWorkloadResponse
{
    public DateTime GeneratedAt { get; init; }
    public AgentWorkloadTotalsResponse Totals { get; init; } = new();
    public IReadOnlyList<AgentWorkloadAgentResponse> Agents { get; init; } = [];
}

public sealed class AgentWorkloadTotalsResponse
{
    public int ActiveTicketCount { get; init; }
    public int UnassignedTicketCount { get; init; }
    public int InactiveAssigneeTicketCount { get; init; }
    public int InvalidAssigneeTicketCount { get; init; }
    public int CriticalTicketCount { get; init; }
    public int HighPriorityTicketCount { get; init; }
    public int AwaitingReplyCount { get; init; }
    public int WaitingOver2HoursCount { get; init; }
    public int WaitingOver8HoursCount { get; init; }
    public int WaitingOver24HoursCount { get; init; }
    public int SlaBreachedCount { get; init; }
    public int SlaDueSoonCount { get; init; }
}

public sealed class AgentWorkloadAgentResponse
{
    public int UserId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public int ActiveTicketCount { get; init; }
    public int CriticalTicketCount { get; init; }
    public int HighPriorityTicketCount { get; init; }
    public int AwaitingReplyCount { get; init; }
    public int WaitingOver8HoursCount { get; init; }
    public int WaitingOver24HoursCount { get; init; }
    public int? LongestAwaitingMinutes { get; init; }
    public int SlaBreachedCount { get; init; }
    public int SlaDueSoonCount { get; init; }
}
