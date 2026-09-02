using SupportFlow.Api.Enums;

namespace SupportFlow.Api.DTOs.Sla;

public sealed class TicketSlaResponse
{
    public bool IsTracked { get; init; }
    public DateTime? ResolutionCycleStartedAt { get; init; }
    public TicketSlaMetricResponse FirstResponse { get; init; } = new();
    public TicketSlaMetricResponse Resolution { get; init; } = new();
    public SlaState MostUrgentState { get; init; }
}

public sealed class TicketSlaMetricResponse
{
    public SlaState State { get; init; }
    public DateTime? DueAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int? RemainingMinutes { get; init; }
    public int? OverdueMinutes { get; init; }
}

public sealed class TicketSlaSummaryResponse
{
    public SlaState FirstResponseState { get; init; }
    public SlaState ResolutionState { get; init; }
    public SlaState MostUrgentState { get; init; }
}
