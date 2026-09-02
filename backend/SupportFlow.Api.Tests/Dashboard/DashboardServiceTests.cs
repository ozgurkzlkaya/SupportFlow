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

public sealed class DashboardServiceTests
{
    [Fact]
    public async Task QueueAging_UsesActiveWaitingTicketsForCountsAndDeliverySemantics()
    {
        var now = new DateTime(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await DashboardHarness.CreateAsync(now);
        var atTwoHours = harness.AddTicket("At two hours", now.AddMinutes(-120));
        var belowTwoHours = harness.AddTicket("Below two hours", now.AddSeconds(-(119 * 60 + 59)));
        var atEightHours = harness.AddTicket("At eight hours", now.AddMinutes(-480));
        var atTwentyFourHours = harness.AddTicket("At twenty-four hours", now.AddMinutes(-1440));
        var failed = harness.AddTicket("Failed reply", now.AddHours(-10));
        var sent = harness.AddTicket("Sent reply", now.AddHours(-10));
        var resolved = harness.AddTicket("Resolved", now.AddHours(-30), TicketStatus.Resolved);
        var closed = harness.AddTicket("Closed", now.AddHours(-30), TicketStatus.Closed);
        harness.DbContext.Tickets.AddRange(atTwoHours, belowTwoHours, atEightHours, atTwentyFourHours, failed, sent, resolved, closed);
        await harness.DbContext.SaveChangesAsync();
        harness.DbContext.TicketMessages.AddRange(
            new TicketMessage
            {
                TicketId = failed.Id,
                SenderId = harness.Staff.Id,
                Message = "Failed",
                OutboundDeliveryStatus = OutboundDeliveryStatus.Failed,
                CreatedAt = now.AddHours(-9)
            },
            new TicketMessage
            {
                TicketId = sent.Id,
                SenderId = harness.Staff.Id,
                Message = "Sent",
                OutboundDeliveryStatus = OutboundDeliveryStatus.Sent,
                CreatedAt = now.AddHours(-9),
                DeliveredAt = now.AddHours(-9)
            });
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetQueueAgingAsync();

        Assert.Equal(5, result.AwaitingReplyCount);
        Assert.Equal(4, result.WaitingOver2HoursCount);
        Assert.Equal(3, result.WaitingOver8HoursCount);
        Assert.Equal(1, result.WaitingOver24HoursCount);
        Assert.DoesNotContain(result.OldestWaitingTickets, item => item.TicketId == sent.Id);
        Assert.DoesNotContain(result.OldestWaitingTickets, item => item.TicketId is var id && (id == resolved.Id || id == closed.Id));
    }

    [Fact]
    public async Task QueueAging_ReturnsLongestFiveWithAssignmentAndLegacySlaState()
    {
        var now = new DateTime(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await DashboardHarness.CreateAsync(now);
        var tickets = Enumerable.Range(1, 6)
            .Select(index => harness.AddTicket(
                $"Waiting {index}",
                now.AddHours(-index),
                assignedUserId: index == 6 ? harness.Staff.Id : null))
            .ToArray();
        harness.DbContext.Tickets.AddRange(tickets);
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetQueueAgingAsync();

        Assert.Equal(6, result.AwaitingReplyCount);
        Assert.Equal(5, result.OldestWaitingTickets.Count);
        Assert.Equal(tickets[5].Id, result.OldestWaitingTickets[0].TicketId);
        Assert.Equal(harness.Staff.FirstName + " " + harness.Staff.LastName, result.OldestWaitingTickets[0].AssignedUserName);
        Assert.Contains(result.OldestWaitingTickets, item => item.AssignedUserName is null);
        Assert.All(result.OldestWaitingTickets, item => Assert.Equal(SlaState.NotTracked, item.SlaMostUrgentState));
        Assert.DoesNotContain(result.OldestWaitingTickets, item => item.TicketId == tickets[0].Id);
    }

    [Fact]
    public void QueueAgingEndpoint_AllowsOnlyAdminAndTechnicalStaff()
    {
        var attribute = Assert.Single(typeof(DashboardController).GetCustomAttributes<AuthorizeAttribute>());

        Assert.Equal("Admin,TechnicalStaff", attribute.Roles);
        Assert.DoesNotContain(nameof(UserRole.Customer), attribute.Roles);
    }

    private sealed class DashboardHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private DashboardHarness(
            SqliteConnection connection,
            AppDbContext dbContext,
            User customer,
            User staff,
            DashboardService service)
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
        public DashboardService Service { get; }

        public static async Task<DashboardHarness> CreateAsync(DateTime now)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var dbContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options);
            await dbContext.Database.EnsureCreatedAsync();

            var customer = new User
            {
                FirstName = "Customer",
                LastName = "User",
                Email = "customer@example.test",
                PasswordHash = "hash",
                Role = UserRole.Customer,
                IsActive = true
            };
            var staff = new User
            {
                FirstName = "Staff",
                LastName = "User",
                Email = "staff@example.test",
                PasswordHash = "hash",
                Role = UserRole.TechnicalStaff,
                IsActive = true
            };
            dbContext.Users.AddRange(customer, staff);
            await dbContext.SaveChangesAsync();

            var targets = new SlaTargetResolver(Options.Create(new SlaSettings()));
            var lifecycle = new TicketSlaService(targets);
            var service = new DashboardService(
                dbContext,
                new QueueAgingCalculator(new FixedTimeProvider(now)),
                new TicketSlaCalculator(lifecycle, targets, new FixedTimeProvider(now)));
            return new DashboardHarness(connection, dbContext, customer, staff, service);
        }

        public Ticket AddTicket(
            string title,
            DateTime createdAt,
            TicketStatus status = TicketStatus.New,
            int? assignedUserId = null) =>
            new()
            {
                TicketNumber = $"SUP-2026-{Guid.NewGuid():N}"[..30],
                CustomerId = Customer.Id,
                AssignedUserId = assignedUserId,
                Title = title,
                Description = "Dashboard test",
                Status = status,
                Priority = TicketPriority.Normal,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            };

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
