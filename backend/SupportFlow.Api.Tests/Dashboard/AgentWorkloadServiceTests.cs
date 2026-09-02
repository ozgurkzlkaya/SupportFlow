using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SupportFlow.Api.Controllers;
using SupportFlow.Api.Data;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;
using SupportFlow.Api.Services;
using SupportFlow.Api.Services.QueueAging;
using SupportFlow.Api.Services.Sla;
using SupportFlow.Api.Settings;

namespace SupportFlow.Api.Tests.Dashboard;

public sealed class AgentWorkloadServiceTests
{
    private static readonly DateTime Now = new(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Snapshot_IncludesOnlyActiveTechnicalStaffAndZeroWorkAgents()
    {
        await using var harness = await WorkloadHarness.CreateAsync(Now);
        var idle = harness.NewUser("Idle", UserRole.TechnicalStaff, true);
        var inactive = harness.NewUser("Inactive", UserRole.TechnicalStaff, false);
        var admin = harness.NewUser("Admin", UserRole.Admin, true);
        var customer = harness.NewUser("Customer", UserRole.Customer, true);
        harness.DbContext.Users.AddRange(idle, inactive, admin, customer);
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetSnapshotAsync();

        Assert.Equal([harness.Staff.Id, idle.Id], result.Agents.Select(agent => agent.UserId).Order());
        var idleRow = Assert.Single(result.Agents, agent => agent.UserId == idle.Id);
        Assert.Equal(0, idleRow.ActiveTicketCount);
        Assert.Null(idleRow.LongestAwaitingMinutes);
    }

    [Theory]
    [InlineData(TicketStatus.New)]
    [InlineData(TicketStatus.Assigned)]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.WaitingForCustomer)]
    [InlineData(TicketStatus.Reopened)]
    public async Task Snapshot_IncludesEveryExplicitActiveStatus(TicketStatus status)
    {
        await using var harness = await WorkloadHarness.CreateAsync(Now);
        harness.DbContext.Tickets.Add(harness.NewTicket(status, harness.Staff.Id));
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetSnapshotAsync();

        Assert.Equal(1, result.Totals.ActiveTicketCount);
        Assert.Equal(1, Assert.Single(result.Agents).ActiveTicketCount);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public async Task Snapshot_ExcludesTerminalStatuses(TicketStatus status)
    {
        await using var harness = await WorkloadHarness.CreateAsync(Now);
        harness.DbContext.Tickets.Add(harness.NewTicket(status, harness.Staff.Id));
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetSnapshotAsync();

        Assert.Equal(0, result.Totals.ActiveTicketCount);
        Assert.Equal(0, Assert.Single(result.Agents).ActiveTicketCount);
    }

    [Fact]
    public async Task Snapshot_SeparatesUnassignedInactiveAndInvalidAssigneeTickets()
    {
        await using var harness = await WorkloadHarness.CreateAsync(Now);
        var inactiveStaff = harness.NewUser("Inactive", UserRole.TechnicalStaff, false);
        var admin = harness.NewUser("Admin", UserRole.Admin, true);
        harness.DbContext.Users.AddRange(inactiveStaff, admin);
        await harness.DbContext.SaveChangesAsync();
        harness.DbContext.Tickets.AddRange(
            harness.NewTicket(TicketStatus.New, null),
            harness.NewTicket(TicketStatus.New, inactiveStaff.Id),
            harness.NewTicket(TicketStatus.New, admin.Id));
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetSnapshotAsync();

        Assert.Equal(3, result.Totals.ActiveTicketCount);
        Assert.Equal(1, result.Totals.UnassignedTicketCount);
        Assert.Equal(1, result.Totals.InactiveAssigneeTicketCount);
        Assert.Equal(1, result.Totals.InvalidAssigneeTicketCount);
        Assert.Equal(0, Assert.Single(result.Agents).ActiveTicketCount);
    }

    [Fact]
    public async Task Snapshot_AttributesReassignedTicketToCurrentOwnerOnly()
    {
        await using var harness = await WorkloadHarness.CreateAsync(Now);
        var currentOwner = harness.NewUser("Current", UserRole.TechnicalStaff, true);
        harness.DbContext.Users.Add(currentOwner);
        await harness.DbContext.SaveChangesAsync();
        harness.DbContext.Tickets.Add(harness.NewTicket(TicketStatus.Reopened, currentOwner.Id));
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetSnapshotAsync();

        Assert.Equal(0, Assert.Single(result.Agents, agent => agent.UserId == harness.Staff.Id).ActiveTicketCount);
        Assert.Equal(1, Assert.Single(result.Agents, agent => agent.UserId == currentOwner.Id).ActiveTicketCount);
        Assert.Equal(1, result.Totals.ActiveTicketCount);
    }

    [Theory]
    [InlineData(TicketPriority.Low, 0, 0)]
    [InlineData(TicketPriority.Normal, 0, 0)]
    [InlineData(TicketPriority.High, 0, 1)]
    [InlineData(TicketPriority.Critical, 1, 0)]
    public async Task Snapshot_CountsPriorityWithoutDuplicateAttribution(
        TicketPriority priority,
        int expectedCritical,
        int expectedHigh)
    {
        await using var harness = await WorkloadHarness.CreateAsync(Now);
        harness.DbContext.Tickets.Add(harness.NewTicket(TicketStatus.New, harness.Staff.Id, priority));
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetSnapshotAsync();

        Assert.Equal(expectedCritical, result.Totals.CriticalTicketCount);
        Assert.Equal(expectedHigh, result.Totals.HighPriorityTicketCount);
        Assert.Equal(expectedCritical, Assert.Single(result.Agents).CriticalTicketCount);
        Assert.Equal(expectedHigh, Assert.Single(result.Agents).HighPriorityTicketCount);
    }

    [Theory]
    [InlineData(120, 1, 0, 0)]
    [InlineData(480, 1, 1, 0)]
    [InlineData(1440, 1, 1, 1)]
    public async Task Snapshot_UsesInclusiveQueueAgingThresholds(
        int minutes,
        int expectedTwoHours,
        int expectedEightHours,
        int expectedTwentyFourHours)
    {
        await using var harness = await WorkloadHarness.CreateAsync(Now);
        harness.DbContext.Tickets.Add(harness.NewTicket(
            TicketStatus.New,
            harness.Staff.Id,
            createdAt: Now.AddMinutes(-minutes)));
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetSnapshotAsync();

        Assert.Equal(1, result.Totals.AwaitingReplyCount);
        Assert.Equal(expectedTwoHours, result.Totals.WaitingOver2HoursCount);
        Assert.Equal(expectedEightHours, result.Totals.WaitingOver8HoursCount);
        Assert.Equal(expectedTwentyFourHours, result.Totals.WaitingOver24HoursCount);
        Assert.Equal(minutes, Assert.Single(result.Agents).LongestAwaitingMinutes);
    }

    [Fact]
    public async Task Snapshot_DoesNotTreatSuccessfulStaffReplyAsAwaiting()
    {
        await using var harness = await WorkloadHarness.CreateAsync(Now);
        var ticket = harness.NewTicket(TicketStatus.InProgress, harness.Staff.Id, createdAt: Now.AddHours(-12));
        harness.DbContext.Tickets.Add(ticket);
        await harness.DbContext.SaveChangesAsync();
        harness.DbContext.TicketMessages.Add(new TicketMessage
        {
            TicketId = ticket.Id,
            SenderId = harness.Staff.Id,
            Message = "Answered",
            OutboundDeliveryStatus = OutboundDeliveryStatus.Sent,
            DeliveredAt = Now.AddHours(-1),
            CreatedAt = Now.AddHours(-1)
        });
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetSnapshotAsync();

        Assert.Equal(0, result.Totals.AwaitingReplyCount);
        Assert.Null(Assert.Single(result.Agents).LongestAwaitingMinutes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Snapshot_UsesSameQueueAgingForRegisteredAndExternalRequesters(bool external)
    {
        await using var harness = await WorkloadHarness.CreateAsync(Now);
        ExternalRequester? requester = null;
        if (external)
        {
            requester = new ExternalRequester
            {
                Email = "external@example.test",
                NormalizedEmail = "external@example.test",
                DisplayName = "External",
                CreatedAt = Now,
                UpdatedAt = Now
            };
            harness.DbContext.ExternalRequesters.Add(requester);
            await harness.DbContext.SaveChangesAsync();
        }

        var ticket = harness.NewTicket(TicketStatus.New, harness.Staff.Id, createdAt: Now.AddHours(-8));
        if (requester is not null)
        {
            ticket.CustomerId = null;
            ticket.ExternalRequesterId = requester.Id;
        }

        harness.DbContext.Tickets.Add(ticket);
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetSnapshotAsync();

        Assert.Equal(1, result.Totals.AwaitingReplyCount);
        Assert.Equal(1, Assert.Single(result.Agents).WaitingOver8HoursCount);
    }

    [Fact]
    public async Task Snapshot_CountsSlaRiskOncePerTicketAndExcludesLegacyTickets()
    {
        await using var harness = await WorkloadHarness.CreateAsync(Now);
        var breached = harness.NewTicket(TicketStatus.New, harness.Staff.Id, TicketPriority.Normal);
        breached.FirstResponseDueAt = Now.AddHours(-1);
        breached.ResolutionCycleStartedAt = Now.AddHours(-10);
        breached.ResolutionDueAt = Now.AddHours(-1);
        var dueSoon = harness.NewTicket(TicketStatus.New, harness.Staff.Id, TicketPriority.High);
        dueSoon.FirstResponseDueAt = Now.AddMinutes(10);
        dueSoon.ResolutionCycleStartedAt = Now;
        dueSoon.ResolutionDueAt = Now.AddHours(20);
        var legacy = harness.NewTicket(TicketStatus.New, harness.Staff.Id, TicketPriority.Critical);
        harness.DbContext.Tickets.AddRange(breached, dueSoon, legacy);
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetSnapshotAsync();

        Assert.Equal(1, result.Totals.SlaBreachedCount);
        Assert.Equal(1, result.Totals.SlaDueSoonCount);
        var agent = Assert.Single(result.Agents);
        Assert.Equal(1, agent.SlaBreachedCount);
        Assert.Equal(1, agent.SlaDueSoonCount);
    }

    [Fact]
    public async Task Snapshot_OrdersAgentsByRiskThenName()
    {
        await using var harness = await WorkloadHarness.CreateAsync(Now);
        var criticalAgent = harness.NewUser("Alpha", UserRole.TechnicalStaff, true);
        var breachedAgent = harness.NewUser("Zulu", UserRole.TechnicalStaff, true);
        harness.DbContext.Users.AddRange(criticalAgent, breachedAgent);
        await harness.DbContext.SaveChangesAsync();
        var breached = harness.NewTicket(TicketStatus.New, breachedAgent.Id);
        breached.FirstResponseDueAt = Now.AddHours(-1);
        breached.ResolutionCycleStartedAt = Now.AddHours(-10);
        breached.ResolutionDueAt = Now.AddHours(-1);
        harness.DbContext.Tickets.AddRange(
            breached,
            harness.NewTicket(TicketStatus.New, criticalAgent.Id, TicketPriority.Critical),
            harness.NewTicket(TicketStatus.New, harness.Staff.Id, createdAt: Now.AddHours(-8)));
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetSnapshotAsync();

        Assert.Equal(
            [breachedAgent.Id, criticalAgent.Id, harness.Staff.Id],
            result.Agents.Select(agent => agent.UserId));
    }

    [Fact]
    public async Task Snapshot_ReturnsEmptyTeamWhilePreservingExceptionTotals()
    {
        await using var harness = await WorkloadHarness.CreateAsync(Now);
        harness.Staff.IsActive = false;
        harness.DbContext.Tickets.AddRange(
            harness.NewTicket(TicketStatus.New, null),
            harness.NewTicket(TicketStatus.New, harness.Staff.Id));
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetSnapshotAsync();

        Assert.Empty(result.Agents);
        Assert.Equal(2, result.Totals.ActiveTicketCount);
        Assert.Equal(1, result.Totals.UnassignedTicketCount);
        Assert.Equal(1, result.Totals.InactiveAssigneeTicketCount);
    }

    [Fact]
    public async Task Snapshot_ReturnsZeroMetricsForEmptyQueue()
    {
        await using var harness = await WorkloadHarness.CreateAsync(Now);

        var result = await harness.Service.GetSnapshotAsync();

        Assert.Equal(Now, result.GeneratedAt);
        Assert.Equal(0, result.Totals.ActiveTicketCount);
        var agent = Assert.Single(result.Agents);
        Assert.Equal(0, agent.ActiveTicketCount);
        Assert.Null(agent.LongestAwaitingMinutes);
    }

    [Fact]
    public void AgentWorkloadEndpoint_AllowsOnlyAdmin()
    {
        var attribute = Assert.Single(typeof(AgentWorkloadController)
            .GetCustomAttributes<AuthorizeAttribute>());

        Assert.Equal(nameof(UserRole.Admin), attribute.Roles);
        Assert.DoesNotContain(nameof(UserRole.TechnicalStaff), attribute.Roles);
        Assert.DoesNotContain(nameof(UserRole.Customer), attribute.Roles);
    }

    private sealed class WorkloadHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private WorkloadHarness(
            SqliteConnection connection,
            AppDbContext dbContext,
            User customer,
            User staff,
            IAgentWorkloadService service)
        {
            _connection = connection;
            DbContext = dbContext;
            Customer = customer;
            Staff = staff;
            Service = service;
        }

        public AppDbContext DbContext { get; }
        public User Customer { get; }
        public User Staff { get; }
        public IAgentWorkloadService Service { get; }

        public static async Task<WorkloadHarness> CreateAsync(DateTime now)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var dbContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options);
            await dbContext.Database.EnsureCreatedAsync();
            var customer = CreateUser("Customer", UserRole.Customer, true);
            var staff = CreateUser("Staff", UserRole.TechnicalStaff, true);
            dbContext.Users.AddRange(customer, staff);
            await dbContext.SaveChangesAsync();
            var targets = new SlaTargetResolver(Options.Create(new SlaSettings()));
            var service = new AgentWorkloadService(
                dbContext,
                new QueueAgingCalculator(new FixedTimeProvider(now)),
                new TicketSlaCalculator(new TicketSlaService(targets), targets, new FixedTimeProvider(now)),
                new FixedTimeProvider(now));
            return new WorkloadHarness(connection, dbContext, customer, staff, service);
        }

        public User NewUser(string firstName, UserRole role, bool isActive) =>
            CreateUser(firstName, role, isActive, $"{firstName.ToLowerInvariant()}-{Guid.NewGuid():N}@example.test");

        public Ticket NewTicket(
            TicketStatus status,
            int? assignedUserId,
            TicketPriority priority = TicketPriority.Normal,
            DateTime? createdAt = null) =>
            new()
            {
                TicketNumber = $"SUP-{Guid.NewGuid():N}"[..30],
                CustomerId = Customer.Id,
                AssignedUserId = assignedUserId,
                Title = "Workload test",
                Description = "Workload test",
                Status = status,
                Priority = priority,
                CreatedAt = createdAt ?? Now,
                UpdatedAt = createdAt ?? Now
            };

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private static User CreateUser(
            string firstName,
            UserRole role,
            bool isActive,
            string? email = null) =>
            new()
            {
                FirstName = firstName,
                LastName = "User",
                Email = email ?? $"{firstName.ToLowerInvariant()}-{Guid.NewGuid():N}@example.test",
                PasswordHash = "hash",
                Role = role,
                IsActive = isActive
            };
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
