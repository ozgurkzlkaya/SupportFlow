using Microsoft.Extensions.Options;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;
using SupportFlow.Api.Services.Sla;
using SupportFlow.Api.Settings;

namespace SupportFlow.Api.Tests.Sla;

public sealed class TicketSlaCalculatorTests
{
    private static readonly DateTime Now = new(2026, 8, 24, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void LegacyTicket_IsNotTracked()
    {
        var sla = CreateCalculator().Calculate(new Ticket { Priority = TicketPriority.Normal });

        Assert.False(sla.IsTracked);
        Assert.Equal(SlaState.NotTracked, sla.FirstResponse.State);
        Assert.Equal(SlaState.NotTracked, sla.Resolution.State);
        Assert.Equal(SlaState.NotTracked, sla.MostUrgentState);
    }

    [Fact]
    public void FirstResponse_ActiveBeforeThreshold_IsOnTrack()
    {
        var ticket = CreateTrackedTicket(firstResponseDueAt: Now.AddHours(2));

        var metric = CreateCalculator().Calculate(ticket).FirstResponse;

        Assert.Equal(SlaState.OnTrack, metric.State);
        Assert.Equal(120, metric.RemainingMinutes);
        Assert.Null(metric.OverdueMinutes);
    }

    [Theory]
    [InlineData(TicketPriority.Normal, 96)]
    [InlineData(TicketPriority.Critical, 6)]
    [InlineData(TicketPriority.Low, 288)]
    public void FirstResponse_AtCurrentPriorityTwentyPercentThreshold_IsDueSoon(
        TicketPriority priority,
        int remainingMinutes)
    {
        var ticket = CreateTrackedTicket(priority, Now.AddMinutes(remainingMinutes));

        var metric = CreateCalculator().Calculate(ticket).FirstResponse;

        Assert.Equal(SlaState.DueSoon, metric.State);
        Assert.Equal(remainingMinutes, metric.RemainingMinutes);
    }

    [Fact]
    public void FirstResponse_ActivePastDue_IsBreached()
    {
        var ticket = CreateTrackedTicket(firstResponseDueAt: Now.AddMinutes(-5));

        var metric = CreateCalculator().Calculate(ticket).FirstResponse;

        Assert.Equal(SlaState.Breached, metric.State);
        Assert.Equal(5, metric.OverdueMinutes);
        Assert.Null(metric.RemainingMinutes);
    }

    [Fact]
    public void FirstResponse_CompletedAtDeadline_IsMet()
    {
        var dueAt = Now.AddHours(-1);
        var ticket = CreateTrackedTicket(firstResponseDueAt: dueAt, firstRespondedAt: dueAt);

        var metric = CreateCalculator().Calculate(ticket).FirstResponse;

        Assert.Equal(SlaState.Met, metric.State);
        Assert.Null(metric.RemainingMinutes);
        Assert.Null(metric.OverdueMinutes);
    }

    [Fact]
    public void FirstResponse_CompletedBeforeDeadline_IsMet()
    {
        var dueAt = Now.AddHours(-1);
        var ticket = CreateTrackedTicket(firstResponseDueAt: dueAt, firstRespondedAt: dueAt.AddMinutes(-1));

        Assert.Equal(SlaState.Met, CreateCalculator().Calculate(ticket).FirstResponse.State);
    }

    [Fact]
    public void FirstResponse_CompletedLate_RemainsBreached()
    {
        var dueAt = Now.AddHours(-1);
        var ticket = CreateTrackedTicket(firstResponseDueAt: dueAt, firstRespondedAt: dueAt.AddMinutes(4));

        var metric = CreateCalculator().Calculate(ticket).FirstResponse;

        Assert.Equal(SlaState.Breached, metric.State);
        Assert.Equal(4, metric.OverdueMinutes);
    }

    [Fact]
    public void FirstResponse_ExactCurrentDeadline_IsDueSoonNotBreached()
    {
        var ticket = CreateTrackedTicket(firstResponseDueAt: Now);

        var metric = CreateCalculator().Calculate(ticket).FirstResponse;

        Assert.Equal(SlaState.DueSoon, metric.State);
        Assert.Equal(0, metric.RemainingMinutes);
    }

    [Fact]
    public void Resolution_ActiveBeforeThreshold_IsOnTrack()
    {
        var ticket = CreateTrackedTicket(resolutionDueAt: Now.AddHours(10));

        Assert.Equal(SlaState.OnTrack, CreateCalculator().Calculate(ticket).Resolution.State);
    }

    [Fact]
    public void Resolution_AtThreshold_IsDueSoon()
    {
        var ticket = CreateTrackedTicket(resolutionDueAt: Now.AddMinutes(576));

        var metric = CreateCalculator().Calculate(ticket).Resolution;

        Assert.Equal(SlaState.DueSoon, metric.State);
        Assert.Equal(576, metric.RemainingMinutes);
    }

    [Fact]
    public void Resolution_ActivePastDue_IsBreached()
    {
        var ticket = CreateTrackedTicket(resolutionDueAt: Now.AddMinutes(-3));

        var metric = CreateCalculator().Calculate(ticket).Resolution;

        Assert.Equal(SlaState.Breached, metric.State);
        Assert.Equal(3, metric.OverdueMinutes);
    }

    [Fact]
    public void Resolution_CompletedAtDeadline_IsMet()
    {
        var dueAt = Now.AddHours(-1);
        var ticket = CreateTrackedTicket(resolutionDueAt: dueAt, resolvedAt: dueAt);

        Assert.Equal(SlaState.Met, CreateCalculator().Calculate(ticket).Resolution.State);
    }

    [Fact]
    public void Resolution_CompletedEarly_IsMet()
    {
        var dueAt = Now.AddHours(-1);
        var ticket = CreateTrackedTicket(resolutionDueAt: dueAt, resolvedAt: dueAt.AddMinutes(-1));

        Assert.Equal(SlaState.Met, CreateCalculator().Calculate(ticket).Resolution.State);
    }

    [Fact]
    public void Resolution_CompletedLate_RemainsBreached()
    {
        var dueAt = Now.AddHours(-1);
        var ticket = CreateTrackedTicket(resolutionDueAt: dueAt, resolvedAt: dueAt.AddMinutes(2));

        var metric = CreateCalculator().Calculate(ticket).Resolution;

        Assert.Equal(SlaState.Breached, metric.State);
        Assert.Equal(2, metric.OverdueMinutes);
    }

    [Fact]
    public void ReopenedTicket_UsesCurrentResolutionCycleOnly()
    {
        var ticket = CreateTrackedTicket(
            resolutionCycleStartedAt: Now.AddHours(-1),
            resolutionDueAt: Now.AddHours(15),
            resolvedAt: null,
            firstRespondedAt: Now.AddHours(-2));

        var sla = CreateCalculator().Calculate(ticket);

        Assert.Equal(SlaState.Met, sla.FirstResponse.State);
        Assert.Equal(SlaState.OnTrack, sla.Resolution.State);
    }

    [Fact]
    public void PriorityEscalation_UsesPersistedDeadlineWithoutRecalculation()
    {
        var ticket = CreateTrackedTicket(TicketPriority.Critical, Now.AddMinutes(6));

        var metric = CreateCalculator().Calculate(ticket).FirstResponse;

        Assert.Equal(SlaState.DueSoon, metric.State);
        Assert.Equal(Now.AddMinutes(6), metric.DueAt);
    }

    [Fact]
    public void PriorityDowngrade_UsesPersistedDeadlineWithoutRecalculation()
    {
        var ticket = CreateTrackedTicket(TicketPriority.Low, Now.AddMinutes(288));

        Assert.Equal(SlaState.DueSoon, CreateCalculator().Calculate(ticket).FirstResponse.State);
    }

    [Fact]
    public void DueSoonThreshold_UsesCurrentPriorityTarget()
    {
        var ticket = CreateTrackedTicket(TicketPriority.Critical, Now.AddMinutes(7));

        Assert.Equal(SlaState.OnTrack, CreateCalculator().Calculate(ticket).FirstResponse.State);
    }

    [Fact]
    public void Summary_UsesMostUrgentMetricState()
    {
        var ticket = CreateTrackedTicket(
            firstResponseDueAt: Now.AddHours(2),
            resolutionDueAt: Now.AddMinutes(-1));

        var summary = CreateCalculator().CalculateSummary(ticket);

        Assert.Equal(SlaState.OnTrack, summary.FirstResponseState);
        Assert.Equal(SlaState.Breached, summary.ResolutionState);
        Assert.Equal(SlaState.Breached, summary.MostUrgentState);
    }

    [Fact]
    public void ActiveMetric_UsesCeilingMinutesForRemainingTime()
    {
        var ticket = CreateTrackedTicket(firstResponseDueAt: Now.AddSeconds(1));

        Assert.Equal(1, CreateCalculator().Calculate(ticket).FirstResponse.RemainingMinutes);
    }

    [Fact]
    public void BreachedMetric_UsesCeilingMinutesForOverdueTime()
    {
        var ticket = CreateTrackedTicket(firstResponseDueAt: Now.AddSeconds(-1));

        Assert.Equal(1, CreateCalculator().Calculate(ticket).FirstResponse.OverdueMinutes);
    }

    [Fact]
    public void CompletedMetric_DoesNotExposeActiveDuration()
    {
        var dueAt = Now.AddHours(1);
        var ticket = CreateTrackedTicket(firstResponseDueAt: dueAt, firstRespondedAt: Now);

        var metric = CreateCalculator().Calculate(ticket).FirstResponse;

        Assert.Null(metric.RemainingMinutes);
        Assert.Null(metric.OverdueMinutes);
    }

    [Fact]
    public void Calculator_IsPureAndDoesNotMutateTicketState()
    {
        var ticket = CreateTrackedTicket(firstResponseDueAt: Now.AddMinutes(6));
        var originalDueAt = ticket.FirstResponseDueAt;
        var originalPriority = ticket.Priority;

        _ = CreateCalculator().Calculate(ticket);

        Assert.Equal(originalDueAt, ticket.FirstResponseDueAt);
        Assert.Equal(originalPriority, ticket.Priority);
        Assert.Null(ticket.FirstRespondedAt);
        Assert.Null(ticket.ResolvedAt);
    }

    private static TicketSlaCalculator CreateCalculator() =>
        CreateCalculator(Now);

    private static TicketSlaCalculator CreateCalculator(DateTime now)
    {
        var targetResolver = new SlaTargetResolver(Options.Create(new SlaSettings()));
        var lifecycle = new TicketSlaService(targetResolver);
        return new TicketSlaCalculator(lifecycle, targetResolver, new FixedTimeProvider(now));
    }

    private static Ticket CreateTrackedTicket(
        TicketPriority priority = TicketPriority.Normal,
        DateTime? firstResponseDueAt = null,
        DateTime? firstRespondedAt = null,
        DateTime? resolutionCycleStartedAt = null,
        DateTime? resolutionDueAt = null,
        DateTime? resolvedAt = null) =>
        new()
        {
            TicketNumber = "SUP-2026-000001",
            Priority = priority,
            CreatedAt = Now.AddHours(-8),
            FirstResponseDueAt = firstResponseDueAt ?? Now.AddHours(2),
            FirstRespondedAt = firstRespondedAt,
            ResolutionCycleStartedAt = resolutionCycleStartedAt ?? Now.AddHours(-8),
            ResolutionDueAt = resolutionDueAt ?? Now.AddHours(10),
            ResolvedAt = resolvedAt
        };

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
