using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportFlow.Api.Data;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;
using SupportFlow.Api.Services.QueueAging;

namespace SupportFlow.Api.Tests.QueueAging;

public sealed class QueueAgingQueryProjectionTests
{
    private static readonly DateTime Now = new(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [MemberData(nameof(ParityScenarios))]
    public async Task Projection_MatchesQueueAgingCalculator(ParityScenario scenario)
    {
        await using var harness = await ProjectionHarness.CreateAsync();
        var ticket = await harness.AddScenarioAsync(scenario);

        var projected = await harness.DbContext.Tickets
            .AsNoTracking()
            .Where(item => item.Id == ticket.Id)
            .ProjectQueueAging()
            .SingleAsync();

        var expected = new QueueAgingCalculator(new FixedTimeProvider(Now)).Calculate(
            new QueueAgingTicketInput(scenario.Status, scenario.CreatedAt),
            scenario.Messages.Select(message => message.ToCalculatorInput()).ToArray());

        Assert.Equal(expected.IsAwaitingStaffReply, projected.IsAwaitingStaffReply);
        Assert.Equal(expected.AwaitingStaffSince, projected.AwaitingStaffSince);

        // The projection intentionally exposes the anchor only; the calculator derives durations/buckets.
        if (projected.AwaitingStaffSince.HasValue)
        {
            var projectionDuration = Now - projected.AwaitingStaffSince.Value;
            var projectionMinutes = projectionDuration <= TimeSpan.Zero
                ? 0
                : (int)Math.Floor(projectionDuration.TotalMinutes);
            Assert.Equal(expected.AwaitingStaffMinutes, projectionMinutes);
        }
        else
        {
            Assert.Null(expected.AwaitingStaffMinutes);
            Assert.Null(expected.AgingBucket);
        }
    }

    [Fact]
    public async Task Projection_ExecutesRelationallyAndGeneratedSqlContainsCorrelatedAggregates()
    {
        await using var harness = await ProjectionHarness.CreateAsync();
        await harness.AddScenarioAsync(ParityScenarios().Select(item => (ParityScenario)item[0]).First());

        var query = harness.DbContext.Tickets.AsNoTracking().ProjectQueueAging();
        var sql = query.ToQueryString();
        var result = await query.ToListAsync();

        Assert.Single(result);
        Assert.Contains("TicketMessages", sql, StringComparison.Ordinal);
        Assert.Contains("MAX", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AsEnumerable", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Projection_GeneratesSqlServerQueryableSqlForBaseFiltersAndSorting()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=SupportFlowProjectionTranslation;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var context = new AppDbContext(options);

        var baseSql = context.Tickets.AsNoTracking()
            .ProjectQueueAging()
            .ToQueryString();
        var awaitingSql = context.Tickets.AsNoTracking()
            .ProjectQueueAging(awaitingOnly: true)
            .ToQueryString();
        var overEightHoursSql = context.Tickets.AsNoTracking()
            .ProjectQueueAging(awaitingOnly: true, awaitingBeforeOrAt: Now.AddHours(-8))
            .ToQueryString();
        var longestWaitingSql = context.Tickets.AsNoTracking()
            .ProjectQueueAging(sort: QueueAgingProjectionSort.LongestWaiting)
            .Skip(25)
            .Take(25)
            .ToQueryString();
        var shortestWaitingSql = context.Tickets.AsNoTracking()
            .ProjectQueueAging(sort: QueueAgingProjectionSort.ShortestWaiting)
            .ToQueryString();

        Assert.Contains("TicketMessages", baseSql, StringComparison.Ordinal);
        Assert.Contains("MAX", baseSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TicketMessages", awaitingSql, StringComparison.Ordinal);
        Assert.Contains("TicketMessages", overEightHoursSql, StringComparison.Ordinal);
        Assert.Contains("ORDER BY", longestWaitingSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OFFSET", longestWaitingSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FETCH", longestWaitingSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY", shortestWaitingSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AsEnumerable", baseSql, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Projection_UsesTheSameMessageAfterFailedDeliveryIsRetriedSuccessfully()
    {
        await using var harness = await ProjectionHarness.CreateAsync();
        var createdAt = Now.AddHours(-3);
        var ticket = await harness.AddScenarioAsync(new ParityScenario(
            "same message retry",
            TicketStatus.New,
            createdAt,
            false,
            [Staff(Now.AddHours(-2), OutboundDeliveryStatus.Failed)]));
        var message = await harness.DbContext.TicketMessages.SingleAsync();
        var messageId = message.Id;

        var beforeRetry = await harness.DbContext.Tickets
            .Where(item => item.Id == ticket.Id)
            .ProjectQueueAging()
            .SingleAsync();
        Assert.True(beforeRetry.IsAwaitingStaffReply);

        message.OutboundDeliveryStatus = OutboundDeliveryStatus.Sent;
        message.DeliveredAt = Now.AddHours(-1);
        message.LastDeliveryAttemptAt = Now.AddHours(-1);
        message.DeliveryAttemptCount = 2;
        await harness.DbContext.SaveChangesAsync();
        harness.DbContext.ChangeTracker.Clear();

        var afterRetry = await harness.DbContext.Tickets
            .Where(item => item.Id == ticket.Id)
            .ProjectQueueAging()
            .SingleAsync();
        var persistedMessage = await harness.DbContext.TicketMessages.SingleAsync();

        Assert.False(afterRetry.IsAwaitingStaffReply);
        Assert.Null(afterRetry.AwaitingStaffSince);
        Assert.Equal(messageId, persistedMessage.Id);
        Assert.Equal(2, persistedMessage.DeliveryAttemptCount);
        Assert.Equal(Now.AddHours(-1), persistedMessage.DeliveredAt);
    }

    [Fact]
    public async Task Projection_ExecutesAwaitingFiltersAndWaitingSortsRelationally()
    {
        await using var harness = await ProjectionHarness.CreateAsync();
        var longest = await harness.AddScenarioAsync(new ParityScenario(
            "longest waiting",
            TicketStatus.New,
            Now.AddHours(-9),
            false,
            []));
        var shortest = await harness.AddScenarioAsync(new ParityScenario(
            "shortest waiting",
            TicketStatus.New,
            Now.AddHours(-3),
            false,
            []));

        var overEightHours = await harness.DbContext.Tickets
            .ProjectQueueAging(awaitingOnly: true, awaitingBeforeOrAt: Now.AddHours(-8))
            .ToListAsync();
        var longestFirst = await harness.DbContext.Tickets
            .ProjectQueueAging(sort: QueueAgingProjectionSort.LongestWaiting)
            .ToListAsync();
        var shortestFirst = await harness.DbContext.Tickets
            .ProjectQueueAging(sort: QueueAgingProjectionSort.ShortestWaiting)
            .ToListAsync();

        Assert.Equal([longest.Id], overEightHours.Select(item => item.TicketId));
        Assert.Equal(longest.Id, longestFirst.First().TicketId);
        Assert.Equal(shortest.Id, shortestFirst.First().TicketId);
    }

    public static IEnumerable<object[]> ParityScenarios()
    {
        var t0 = Now.AddHours(-10);
        var t1 = Now.AddHours(-8);
        var t2 = Now.AddHours(-6);
        var t3 = Now.AddHours(-4);

        yield return Case("new active ticket", TicketStatus.New, t0);
        yield return Case("assigned active ticket", TicketStatus.Assigned, t0);
        yield return Case("in progress active ticket", TicketStatus.InProgress, t0);
        yield return Case("waiting for customer active ticket", TicketStatus.WaitingForCustomer, t0);
        yield return Case("reopened active ticket", TicketStatus.Reopened, t0);
        yield return Case("resolved ticket", TicketStatus.Resolved, t0);
        yield return Case("closed ticket", TicketStatus.Closed, t0);
        yield return Case("sent delivered at", TicketStatus.New, t0, Staff(t1, OutboundDeliveryStatus.Sent, t2));
        yield return Case("sent created at fallback", TicketStatus.New, t0, Staff(t1, OutboundDeliveryStatus.Sent), Registered(t2));
        yield return Case("legacy not applicable", TicketStatus.New, t0, Staff(t1, OutboundDeliveryStatus.NotApplicable));
        yield return Case("failed ignored", TicketStatus.New, t0, Staff(t1, OutboundDeliveryStatus.Failed));
        yield return Case("pending ignored", TicketStatus.New, t0, Staff(t1, OutboundDeliveryStatus.Pending));
        yield return Case("internal staff ignored", TicketStatus.New, t0, Staff(t1, OutboundDeliveryStatus.Sent, isInternal: true));
        yield return Case("registered requester public message", TicketStatus.New, t0, Staff(t1, OutboundDeliveryStatus.Sent), Registered(t2));
        yield return Case("external requester public message", TicketStatus.New, t0, Staff(t1, OutboundDeliveryStatus.Sent), External(t2), externalTicket: true);
        yield return Case("staff public message is not requester", TicketStatus.New, t0, Staff(t1, OutboundDeliveryStatus.Sent));
        yield return Case("internal requester-like message ignored", TicketStatus.New, t0, Staff(t1, OutboundDeliveryStatus.Sent), Registered(t2, isInternal: true));
        yield return Case("requester staff sent", TicketStatus.New, t0, Staff(t1, OutboundDeliveryStatus.Sent));
        yield return Case("requester failed", TicketStatus.New, t0, Staff(t1, OutboundDeliveryStatus.Failed));
        yield return Case("requester pending", TicketStatus.New, t0, Staff(t1, OutboundDeliveryStatus.Pending));
        yield return Case("staff sent then requester", TicketStatus.New, t0, Staff(t1, OutboundDeliveryStatus.Sent), Registered(t2));
        yield return Case("requester then failed staff", TicketStatus.New, t0, Staff(t1, OutboundDeliveryStatus.Sent), Registered(t2), Staff(t3, OutboundDeliveryStatus.Failed));
        yield return Case("three requester messages", TicketStatus.New, t0, Registered(t1), Registered(t2), Registered(t3));
        yield return Case("multiple requester after staff", TicketStatus.New, t0, Staff(t1, OutboundDeliveryStatus.Sent), Registered(t2), Registered(t3));
        yield return Case("multiple successful staff uses latest", TicketStatus.New, t0, Staff(t1, OutboundDeliveryStatus.Sent), Staff(t2, OutboundDeliveryStatus.NotApplicable), Registered(t3));
        yield return Case("failed sent same message retry result", TicketStatus.New, t0, Staff(t1, OutboundDeliveryStatus.Sent, t2));
        yield return Case("legacy reply then requester", TicketStatus.New, t0, Staff(t1, OutboundDeliveryStatus.NotApplicable), Registered(t2));
        yield return Case("requester equal staff timestamp", TicketStatus.New, t0, Staff(t1, OutboundDeliveryStatus.Sent), Registered(t1));
        yield return Case("requester one tick after staff", TicketStatus.New, t0, Staff(t1, OutboundDeliveryStatus.Sent), Registered(t1.AddTicks(1)));
        yield return Case("requester one tick before staff", TicketStatus.New, t0, Registered(t1.AddTicks(-1)), Staff(t1, OutboundDeliveryStatus.Sent));
        yield return Case("resolved then requester reopens", TicketStatus.Reopened, t0, Staff(t1, OutboundDeliveryStatus.Sent), Registered(t2));
        yield return Case("closed then requester reopens", TicketStatus.Reopened, t0, Staff(t1, OutboundDeliveryStatus.Sent), Registered(t2));
        yield return Case("reopened preserves assignment semantics", TicketStatus.Reopened, t0, Staff(t1, OutboundDeliveryStatus.Sent), Registered(t2));
        yield return Case("registered ticket replies", TicketStatus.New, t0, Staff(t1, OutboundDeliveryStatus.Sent), Registered(t2));
        yield return Case("external ticket replies", TicketStatus.New, t0, Staff(t1, OutboundDeliveryStatus.Sent), External(t2), externalTicket: true);
        yield return Case("same numeric registered external ids", TicketStatus.New, t0, Staff(t1, OutboundDeliveryStatus.Sent), External(t2), externalTicket: true);
        yield return Case("future ticket created at", TicketStatus.New, Now.AddHours(1));
        yield return Case("future requester message", TicketStatus.New, t0, Staff(t1, OutboundDeliveryStatus.Sent), Registered(Now.AddHours(1)));
        yield return Case("exact thirty minute boundary", TicketStatus.New, Now.AddMinutes(-30));
        yield return Case("exact two hour boundary", TicketStatus.New, Now.AddHours(-2));
        yield return Case("exact eight hour boundary", TicketStatus.New, Now.AddHours(-8));
        yield return Case("exact twenty four hour boundary", TicketStatus.New, Now.AddHours(-24));
        yield return Case("exact seventy two hour boundary", TicketStatus.New, Now.AddHours(-72));
        yield return Case("legacy sla untracked still ages", TicketStatus.New, t0);
        yield return Case("only internal notes", TicketStatus.New, t0, Registered(t1, isInternal: true));
        yield return Case("external sender without registered user", TicketStatus.New, t0, Staff(t1, OutboundDeliveryStatus.Sent), External(t2), externalTicket: true);
    }

    private static object[] Case(
        string name,
        TicketStatus status,
        DateTime createdAt,
        params MessageScenario[] messages) =>
        [new ParityScenario(name, status, createdAt, false, messages)];

    private static object[] Case(
        string name,
        TicketStatus status,
        DateTime createdAt,
        MessageScenario message,
        bool externalTicket) =>
        [new ParityScenario(name, status, createdAt, externalTicket, [message])];

    private static object[] Case(
        string name,
        TicketStatus status,
        DateTime createdAt,
        MessageScenario first,
        MessageScenario second,
        bool externalTicket) =>
        [new ParityScenario(name, status, createdAt, externalTicket, [first, second])];

    private static object[] Case(
        string name,
        TicketStatus status,
        DateTime createdAt,
        MessageScenario first,
        MessageScenario second,
        MessageScenario third,
        bool externalTicket) =>
        [new ParityScenario(name, status, createdAt, externalTicket, [first, second, third])];

    private static object[] Case(
        string name,
        TicketStatus status,
        DateTime createdAt,
        MessageScenario first,
        MessageScenario second,
        MessageScenario third,
        MessageScenario fourth) =>
        [new ParityScenario(name, status, createdAt, false, [first, second, third, fourth])];

    private static MessageScenario Registered(DateTime createdAt, bool isInternal = false) =>
        new(MessageKind.RegisteredRequester, createdAt, OutboundDeliveryStatus.NotApplicable, null, isInternal);

    private static MessageScenario External(DateTime createdAt, bool isInternal = false) =>
        new(MessageKind.ExternalRequester, createdAt, OutboundDeliveryStatus.NotApplicable, null, isInternal);

    private static MessageScenario Staff(
        DateTime createdAt,
        OutboundDeliveryStatus status,
        DateTime? deliveredAt = null,
        bool isInternal = false) =>
        new(MessageKind.Staff, createdAt, status, deliveredAt, isInternal);

    public sealed record ParityScenario(
        string Name,
        TicketStatus Status,
        DateTime CreatedAt,
        bool ExternalTicket,
        IReadOnlyList<MessageScenario> Messages)
    {
        public override string ToString() => Name;
    }

    public sealed record MessageScenario(
        MessageKind Kind,
        DateTime CreatedAt,
        OutboundDeliveryStatus DeliveryStatus,
        DateTime? DeliveredAt,
        bool IsInternal)
    {
        public QueueAgingMessageInput ToCalculatorInput() => new(
            Kind is MessageKind.RegisteredRequester or MessageKind.ExternalRequester,
            Kind == MessageKind.Staff,
            IsInternal,
            DeliveryStatus,
            CreatedAt,
            DeliveredAt);
    }

    public enum MessageKind
    {
        RegisteredRequester,
        ExternalRequester,
        Staff
    }

    private sealed class ProjectionHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private ProjectionHarness(SqliteConnection connection, AppDbContext dbContext, User customer, User staff, ExternalRequester externalRequester)
        {
            _connection = connection;
            DbContext = dbContext;
            Customer = customer;
            Staff = staff;
            ExternalRequester = externalRequester;
        }

        public AppDbContext DbContext { get; }
        private User Customer { get; }
        private User Staff { get; }
        private ExternalRequester ExternalRequester { get; }

        public static async Task<ProjectionHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var dbContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options);
            await dbContext.Database.EnsureCreatedAsync();

            var customer = new User { FirstName = "Customer", LastName = "User", Email = "customer@example.test", PasswordHash = "hash", Role = UserRole.Customer };
            var staff = new User { FirstName = "Staff", LastName = "User", Email = "staff@example.test", PasswordHash = "hash", Role = UserRole.TechnicalStaff };
            var externalRequester = new ExternalRequester { Email = "external@example.test", NormalizedEmail = "external@example.test", DisplayName = "External" };
            dbContext.AddRange(customer, staff, externalRequester);
            await dbContext.SaveChangesAsync();
            return new ProjectionHarness(connection, dbContext, customer, staff, externalRequester);
        }

        public async Task<Ticket> AddScenarioAsync(ParityScenario scenario)
        {
            var ticket = new Ticket
            {
                TicketNumber = $"SUP-{Guid.NewGuid():N}"[..30],
                CustomerId = scenario.ExternalTicket ? null : Customer.Id,
                ExternalRequesterId = scenario.ExternalTicket ? ExternalRequester.Id : null,
                Title = scenario.Name,
                Description = "Projection parity test",
                Status = scenario.Status,
                Priority = TicketPriority.Normal,
                CreatedAt = scenario.CreatedAt,
                UpdatedAt = scenario.CreatedAt
            };
            DbContext.Tickets.Add(ticket);
            await DbContext.SaveChangesAsync();

            var messages = scenario.Messages.Select(message => new TicketMessage
            {
                TicketId = ticket.Id,
                SenderId = message.Kind == MessageKind.RegisteredRequester ? Customer.Id
                    : message.Kind == MessageKind.Staff ? Staff.Id : null,
                ExternalRequesterId = message.Kind == MessageKind.ExternalRequester ? ExternalRequester.Id : null,
                Message = scenario.Name,
                IsInternal = message.IsInternal,
                OutboundDeliveryStatus = message.DeliveryStatus,
                DeliveredAt = message.DeliveredAt,
                CreatedAt = message.CreatedAt
            });
            DbContext.TicketMessages.AddRange(messages);
            await DbContext.SaveChangesAsync();
            return ticket;
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
