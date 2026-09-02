using SupportFlow.Api.Enums;
using SupportFlow.Api.Services.QueueAging;

namespace SupportFlow.Api.Tests.QueueAging;

public sealed class QueueAgingCalculatorTests
{
    private static readonly DateTime Now = new(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void InitialTicket_StartsWaitingAtCreatedAt()
    {
        var createdAt = Now.AddHours(-1);
        var result = Calculate(NewTicket(createdAt));

        Assert.True(result.IsAwaitingStaffReply);
        Assert.Equal(createdAt, result.AwaitingStaffSince);
        Assert.Equal(60, result.AwaitingStaffMinutes);
    }

    [Fact]
    public void TicketAge_UsesCreatedAt()
    {
        var result = Calculate(NewTicket(Now.AddMinutes(-91)));

        Assert.Equal(91, result.TicketAgeMinutes);
    }

    [Fact]
    public void SentStaffReply_StopsWaiting()
    {
        var result = Calculate(NewTicket(Now.AddHours(-2)), Staff(OutboundDeliveryStatus.Sent, Now.AddHours(-1)));

        Assert.False(result.IsAwaitingStaffReply);
        Assert.Null(result.AwaitingStaffSince);
    }

    [Fact]
    public void SentStaffReply_UsesDeliveredAt()
    {
        var result = Calculate(
            NewTicket(Now.AddHours(-3)),
            Staff(OutboundDeliveryStatus.Sent, Now.AddHours(-2), Now.AddHours(-1)),
            Requester(Now.AddMinutes(-90)));

        Assert.False(result.IsAwaitingStaffReply);
    }

    [Fact]
    public void SentStaffReply_FallsBackToCreatedAt()
    {
        var result = Calculate(
            NewTicket(Now.AddHours(-3)),
            Staff(OutboundDeliveryStatus.Sent, Now.AddHours(-2)),
            Requester(Now.AddHours(-1)));

        Assert.True(result.IsAwaitingStaffReply);
        Assert.Equal(Now.AddHours(-1), result.AwaitingStaffSince);
    }

    [Fact]
    public void FailedStaffReply_DoesNotStopWaiting()
    {
        var createdAt = Now.AddHours(-2);
        var result = Calculate(NewTicket(createdAt), Staff(OutboundDeliveryStatus.Failed, Now.AddHours(-1)));

        Assert.True(result.IsAwaitingStaffReply);
        Assert.Equal(createdAt, result.AwaitingStaffSince);
    }

    [Fact]
    public void PendingStaffReply_DoesNotStopWaiting()
    {
        var createdAt = Now.AddHours(-2);
        var result = Calculate(NewTicket(createdAt), Staff(OutboundDeliveryStatus.Pending, Now.AddHours(-1)));

        Assert.True(result.IsAwaitingStaffReply);
        Assert.Equal(createdAt, result.AwaitingStaffSince);
    }

    [Fact]
    public void InternalNote_IsIgnored()
    {
        var createdAt = Now.AddHours(-2);
        var result = Calculate(
            NewTicket(createdAt),
            Staff(OutboundDeliveryStatus.Sent, Now.AddHours(-1), isInternal: true));

        Assert.True(result.IsAwaitingStaffReply);
        Assert.Equal(createdAt, result.AwaitingStaffSince);
    }

    [Fact]
    public void LegacyNotApplicableStaffReply_StopsWaiting()
    {
        var result = Calculate(NewTicket(Now.AddHours(-2)), Staff(OutboundDeliveryStatus.NotApplicable, Now.AddHours(-1)));

        Assert.False(result.IsAwaitingStaffReply);
    }

    [Fact]
    public void MultipleRequesterActivities_PreserveOldestUnansweredAnchor()
    {
        var createdAt = Now.AddHours(-3);
        var result = Calculate(
            NewTicket(createdAt),
            Requester(Now.AddHours(-2)),
            Requester(Now.AddHours(-1)));

        Assert.Equal(createdAt, result.AwaitingStaffSince);
    }

    [Fact]
    public void SuccessfulStaffReplyThenRequesterActivity_StartsNewInterval()
    {
        var requesterAt = Now.AddHours(-1);
        var result = Calculate(
            NewTicket(Now.AddHours(-3)),
            Staff(OutboundDeliveryStatus.Sent, Now.AddHours(-2)),
            Requester(requesterAt));

        Assert.True(result.IsAwaitingStaffReply);
        Assert.Equal(requesterAt, result.AwaitingStaffSince);
    }

    [Fact]
    public void NewerRequesterActivityAndFailedStaffReply_PreservesRequesterAnchor()
    {
        var requesterAt = Now.AddHours(-1);
        var result = Calculate(
            NewTicket(Now.AddHours(-4)),
            Staff(OutboundDeliveryStatus.Sent, Now.AddHours(-3)),
            Requester(requesterAt),
            Staff(OutboundDeliveryStatus.Failed, Now.AddMinutes(-30)));

        Assert.Equal(requesterAt, result.AwaitingStaffSince);
    }

    [Fact]
    public void ResolvedTicket_IsNotAwaiting()
    {
        var result = Calculate(NewTicket(Now.AddHours(-1), TicketStatus.Resolved));

        Assert.False(result.IsAwaitingStaffReply);
        Assert.Null(result.AwaitingStaffSince);
    }

    [Fact]
    public void ClosedTicket_IsNotAwaiting()
    {
        var result = Calculate(NewTicket(Now.AddHours(-1), TicketStatus.Closed));

        Assert.False(result.IsAwaitingStaffReply);
        Assert.Null(result.AwaitingStaffSince);
    }

    [Fact]
    public void ReopenedTicket_UsesRequesterReplyAfterSuccessfulStaffResponse()
    {
        var reopenedReplyAt = Now.AddHours(-1);
        var result = Calculate(
            NewTicket(Now.AddHours(-4), TicketStatus.Reopened),
            Staff(OutboundDeliveryStatus.Sent, Now.AddHours(-3)),
            Requester(reopenedReplyAt));

        Assert.True(result.IsAwaitingStaffReply);
        Assert.Equal(reopenedReplyAt, result.AwaitingStaffSince);
    }

    [Fact]
    public void RegisteredCustomerActivity_IsIncluded()
    {
        var requesterAt = Now.AddHours(-1);
        var result = Calculate(
            NewTicket(Now.AddHours(-3)),
            Staff(OutboundDeliveryStatus.Sent, Now.AddHours(-2)),
            Requester(requesterAt));

        Assert.Equal(requesterAt, result.AwaitingStaffSince);
    }

    [Fact]
    public void ExternalRequesterActivity_IsIncluded()
    {
        var requesterAt = Now.AddHours(-1);
        var result = Calculate(
            NewTicket(Now.AddHours(-3)),
            Staff(OutboundDeliveryStatus.Sent, Now.AddHours(-2)),
            ExternalRequester(requesterAt));

        Assert.Equal(requesterAt, result.AwaitingStaffSince);
    }

    [Fact]
    public void LegacySlaUntrackedTicket_StillAges()
    {
        var result = Calculate(NewTicket(Now.AddHours(-10)));

        Assert.True(result.IsAwaitingStaffReply);
        Assert.Equal(QueueAgingBucket.LongWait, result.AgingBucket);
    }

    [Fact]
    public void FutureCreatedAt_ClampsDurationsToZero()
    {
        var result = Calculate(NewTicket(Now.AddHours(1)));

        Assert.Equal(0, result.TicketAgeMinutes);
        Assert.Equal(0, result.AwaitingStaffMinutes);
        Assert.Equal(QueueAgingBucket.New, result.AgingBucket);
    }

    [Theory]
    [InlineData(0, QueueAgingBucket.New)]
    [InlineData(29 * 60 + 59, QueueAgingBucket.New)]
    [InlineData(30 * 60, QueueAgingBucket.ShortWait)]
    [InlineData(119 * 60 + 59, QueueAgingBucket.ShortWait)]
    [InlineData(120 * 60, QueueAgingBucket.Waiting)]
    [InlineData(479 * 60 + 59, QueueAgingBucket.Waiting)]
    [InlineData(480 * 60, QueueAgingBucket.LongWait)]
    [InlineData(1439 * 60 + 59, QueueAgingBucket.LongWait)]
    [InlineData(1440 * 60, QueueAgingBucket.Overdue)]
    [InlineData(4319 * 60 + 59, QueueAgingBucket.Overdue)]
    [InlineData(4320 * 60, QueueAgingBucket.CriticalWait)]
    public void AgingBucket_UsesExactBoundaries(int ageSeconds, QueueAgingBucket expected)
    {
        var result = Calculate(NewTicket(Now.AddSeconds(-ageSeconds)));

        Assert.Equal(expected, result.AgingBucket);
    }

    private static QueueAgingResult Calculate(
        QueueAgingTicketInput ticket,
        params QueueAgingMessageInput[] messages) =>
        new QueueAgingCalculator(new FixedTimeProvider(Now)).Calculate(ticket, messages);

    private static QueueAgingTicketInput NewTicket(DateTime createdAt, TicketStatus status = TicketStatus.New) =>
        new(status, createdAt);

    private static QueueAgingMessageInput Requester(DateTime createdAt) =>
        new(true, false, false, OutboundDeliveryStatus.NotApplicable, createdAt, null);

    private static QueueAgingMessageInput ExternalRequester(DateTime createdAt) =>
        new(true, false, false, OutboundDeliveryStatus.NotApplicable, createdAt, null);

    private static QueueAgingMessageInput Staff(
        OutboundDeliveryStatus status,
        DateTime createdAt,
        DateTime? deliveredAt = null,
        bool isInternal = false) =>
        new(false, true, isInternal, status, createdAt, deliveredAt);

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
