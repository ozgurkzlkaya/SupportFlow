using SupportFlow.Api.DTOs.Sla;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;

namespace SupportFlow.Api.Services.Sla;

public sealed class TicketSlaCalculator(
    ITicketSlaService ticketSlaService,
    ISlaTargetResolver targetResolver,
    TimeProvider timeProvider) : ITicketSlaCalculator
{
    public TicketSlaResponse Calculate(Ticket ticket)
    {
        if (!ticketSlaService.IsTracked(ticket))
        {
            return new TicketSlaResponse
            {
                IsTracked = false,
                FirstResponse = new TicketSlaMetricResponse { State = SlaState.NotTracked },
                Resolution = new TicketSlaMetricResponse { State = SlaState.NotTracked },
                MostUrgentState = SlaState.NotTracked
            };
        }

        var targets = targetResolver.GetTargets(ticket.Priority);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var firstResponse = CalculateMetric(
            ticket.FirstResponseDueAt!.Value,
            ticket.FirstRespondedAt,
            targets.FirstResponse,
            now);
        var resolution = CalculateMetric(
            ticket.ResolutionDueAt!.Value,
            ticket.ResolvedAt,
            targets.Resolution,
            now);

        return new TicketSlaResponse
        {
            IsTracked = true,
            ResolutionCycleStartedAt = ticket.ResolutionCycleStartedAt,
            FirstResponse = firstResponse,
            Resolution = resolution,
            MostUrgentState = GetMostUrgentState(firstResponse.State, resolution.State)
        };
    }

    public TicketSlaSummaryResponse CalculateSummary(Ticket ticket)
    {
        var sla = Calculate(ticket);
        return new TicketSlaSummaryResponse
        {
            FirstResponseState = sla.FirstResponse.State,
            ResolutionState = sla.Resolution.State,
            MostUrgentState = sla.MostUrgentState
        };
    }

    private static TicketSlaMetricResponse CalculateMetric(
        DateTime dueAt,
        DateTime? completedAt,
        TimeSpan target,
        DateTime now)
    {
        if (completedAt.HasValue)
        {
            return completedAt.Value <= dueAt
                ? new TicketSlaMetricResponse
                {
                    State = SlaState.Met,
                    DueAt = dueAt,
                    CompletedAt = completedAt
                }
                : new TicketSlaMetricResponse
                {
                    State = SlaState.Breached,
                    DueAt = dueAt,
                    CompletedAt = completedAt,
                    OverdueMinutes = ToPositiveMinutes(completedAt.Value - dueAt)
                };
        }

        var remaining = dueAt - now;
        if (remaining < TimeSpan.Zero)
        {
            return new TicketSlaMetricResponse
            {
                State = SlaState.Breached,
                DueAt = dueAt,
                OverdueMinutes = ToPositiveMinutes(-remaining)
            };
        }

        return new TicketSlaMetricResponse
        {
            State = remaining <= target * 0.20 ? SlaState.DueSoon : SlaState.OnTrack,
            DueAt = dueAt,
            RemainingMinutes = ToNonNegativeMinutes(remaining)
        };
    }

    private static SlaState GetMostUrgentState(SlaState firstResponse, SlaState resolution) =>
        GetUrgency(firstResponse) >= GetUrgency(resolution) ? firstResponse : resolution;

    private static int GetUrgency(SlaState state) => state switch
    {
        SlaState.Breached => 4,
        SlaState.DueSoon => 3,
        SlaState.OnTrack => 2,
        SlaState.Met => 1,
        _ => 0
    };

    private static int ToPositiveMinutes(TimeSpan value) =>
        Math.Max(1, (int)Math.Ceiling(value.TotalMinutes));

    private static int ToNonNegativeMinutes(TimeSpan value) =>
        Math.Max(0, (int)Math.Ceiling(value.TotalMinutes));
}
