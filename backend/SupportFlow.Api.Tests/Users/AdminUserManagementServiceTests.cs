using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportFlow.Api.Controllers;
using SupportFlow.Api.Data;
using SupportFlow.Api.DTOs.AdminUsers;
using SupportFlow.Api.DTOs.Sla;
using SupportFlow.Api.DTOs.Users;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;
using SupportFlow.Api.Services;
using SupportFlow.Api.Services.QueueAging;
using SupportFlow.Api.Services.Sla;

namespace SupportFlow.Api.Tests.Users;

public sealed class AdminUserManagementServiceTests
{
    private static readonly DateTime Now = new(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetUsersAsync_AppliesServerSideFiltersPaginationAndActiveAssignmentCounts()
    {
        await using var harness = await AdminUserHarness.CreateAsync();

        var result = await harness.Service.GetUsersAsync(
            "staff@example.test",
            UserRole.TechnicalStaff,
            UserAccountStatusFilter.Active,
            page: 1,
            pageSize: 1);

        var item = Assert.Single(result.Items);
        Assert.Equal(harness.Staff.Id, item.Id);
        Assert.Equal(2, item.ActiveTicketCount);
        Assert.Equal(1, result.Pagination.TotalCount);
        Assert.Equal(1, result.Pagination.TotalPages);
        Assert.False(result.Pagination.HasNextPage);

        var outOfRange = await harness.Service.GetUsersAsync(null, null, null, page: 99, pageSize: 25);
        Assert.Empty(outOfRange.Items);
        Assert.Equal(5, outOfRange.Pagination.TotalCount);
    }

    [Fact]
    public async Task GetUsersAsync_ExcludesResolvedAndClosedTicketsFromActiveAssignmentCount()
    {
        await using var harness = await AdminUserHarness.CreateAsync();

        var result = await harness.Service.GetUsersAsync(null, UserRole.TechnicalStaff, null, 1, 25);

        Assert.Equal(2, Assert.Single(result.Items).ActiveTicketCount);
    }

    [Fact]
    public async Task GetUserAsync_ReturnsSafeTechnicalStaffAndCustomerContexts()
    {
        await using var harness = await AdminUserHarness.CreateAsync();

        var staff = await harness.Service.GetUserAsync(harness.Staff.Id);
        var customer = await harness.Service.GetUserAsync(harness.Customer.Id);

        Assert.NotNull(staff);
        Assert.NotNull(staff!.TechnicalStaffContext);
        Assert.Null(staff.CustomerContext);
        Assert.Equal(2, staff.TechnicalStaffContext!.ActiveTicketCount);
        Assert.Equal(1, staff.TechnicalStaffContext.CriticalTicketCount);
        Assert.Equal(1, staff.TechnicalStaffContext.AwaitingReplyCount);
        Assert.Equal(1, staff.TechnicalStaffContext.SlaBreachedCount);

        Assert.NotNull(customer);
        Assert.NotNull(customer!.CustomerContext);
        Assert.Equal(3, customer.CustomerContext!.TotalTicketCount);
        Assert.Equal(1, customer.CustomerContext.ActiveTicketCount);
        Assert.Equal(1, customer.CustomerContext.ResolvedTicketCount);
        Assert.Equal(1, customer.CustomerContext.ClosedTicketCount);
        Assert.DoesNotContain("PasswordHash", typeof(AdminUserDetailResponse).GetProperties().Select(property => property.Name));
    }

    [Fact]
    public async Task UpdateStatusAsync_DeactivatesAndReactivatesStaffWithoutChangingAssignments()
    {
        await using var harness = await AdminUserHarness.CreateAsync();
        var assignedTicketIds = await harness.DbContext.Tickets
            .Where(ticket => ticket.AssignedUserId == harness.Staff.Id)
            .Select(ticket => ticket.Id)
            .ToArrayAsync();

        var deactivated = await harness.Service.UpdateStatusAsync(harness.AdminA.Id, harness.Staff.Id, false);

        Assert.Equal(AdminUserStatusUpdateFailure.None, deactivated.Failure);
        Assert.False(deactivated.User!.IsActive);
        Assert.Equal(assignedTicketIds, await harness.DbContext.Tickets
            .Where(ticket => ticket.AssignedUserId == harness.Staff.Id)
            .Select(ticket => ticket.Id)
            .ToArrayAsync());
        Assert.DoesNotContain(harness.Staff.Id, (await harness.UserService.GetActiveTechnicalStaffAsync()).Select(user => user.Id));
        var deactivationAudit = Assert.Single(await harness.DbContext.UserAdminAudits.ToListAsync());
        Assert.Equal(harness.AdminA.Id, deactivationAudit.ActorUserId);
        Assert.Equal(harness.Staff.Id, deactivationAudit.TargetUserId);
        Assert.Equal(UserAdminAuditAction.Deactivated, deactivationAudit.Action);
        Assert.True(deactivationAudit.OldIsActive);
        Assert.False(deactivationAudit.NewIsActive);
        Assert.Equal(Now, deactivationAudit.OccurredAt);
        Assert.Equal("admin-a User", deactivationAudit.ActorDisplayName);
        Assert.Equal("staff User", deactivationAudit.TargetDisplayName);
        Assert.Equal(harness.Staff.Email, deactivationAudit.TargetEmail);
        Assert.Equal(UserRole.TechnicalStaff, deactivationAudit.TargetRole);

        var reactivated = await harness.Service.UpdateStatusAsync(harness.AdminA.Id, harness.Staff.Id, true);
        Assert.True(reactivated.User!.IsActive);
        Assert.Contains(harness.Staff.Id, (await harness.UserService.GetActiveTechnicalStaffAsync()).Select(user => user.Id));
        Assert.Equal(UserAdminAuditAction.Activated, await harness.DbContext.UserAdminAudits
            .OrderByDescending(audit => audit.Id)
            .Select(audit => audit.Action)
            .FirstAsync());
    }

    [Fact]
    public async Task UpdateStatusAsync_DeactivatesCustomerWithoutChangingHistoricalOwnership()
    {
        await using var harness = await AdminUserHarness.CreateAsync();
        var ticketIds = await harness.DbContext.Tickets
            .Where(ticket => ticket.CustomerId == harness.Customer.Id)
            .Select(ticket => ticket.Id)
            .ToArrayAsync();

        var result = await harness.Service.UpdateStatusAsync(harness.AdminA.Id, harness.Customer.Id, false);

        Assert.Equal(AdminUserStatusUpdateFailure.None, result.Failure);
        Assert.False((await harness.DbContext.Users.SingleAsync(user => user.Id == harness.Customer.Id)).IsActive);
        Assert.Equal(ticketIds, await harness.DbContext.Tickets
            .Where(ticket => ticket.CustomerId == harness.Customer.Id)
            .Select(ticket => ticket.Id)
            .ToArrayAsync());
        var audit = Assert.Single(await harness.DbContext.UserAdminAudits.ToListAsync());
        Assert.Equal(UserAdminAuditAction.Deactivated, audit.Action);
        Assert.Equal(harness.Customer.Id, audit.TargetUserId);
    }

    [Fact]
    public async Task UpdateStatusAsync_IsIdempotentAndBlocksSelfDeactivation()
    {
        await using var harness = await AdminUserHarness.CreateAsync();

        var unchanged = await harness.Service.UpdateStatusAsync(harness.AdminA.Id, harness.Staff.Id, true);
        var self = await harness.Service.UpdateStatusAsync(harness.AdminA.Id, harness.AdminA.Id, false);

        Assert.Equal(AdminUserStatusUpdateFailure.None, unchanged.Failure);
        Assert.True(unchanged.User!.IsActive);
        Assert.Equal(AdminUserStatusUpdateFailure.SelfDeactivation, self.Failure);
        Assert.True((await harness.DbContext.Users.SingleAsync(user => user.Id == harness.AdminA.Id)).IsActive);
        Assert.Empty(await harness.DbContext.UserAdminAudits.ToListAsync());
    }

    [Fact]
    public async Task UpdateStatusAsync_ProtectsLastActiveAdminAndAllowsAnotherAdminToBeDeactivated()
    {
        await using var harness = await AdminUserHarness.CreateAsync();

        var otherAdmin = await harness.Service.UpdateStatusAsync(harness.AdminA.Id, harness.AdminB.Id, false);
        var lastAdmin = await harness.Service.UpdateStatusAsync(harness.AdminA.Id, harness.AdminA.Id, false);

        Assert.Equal(AdminUserStatusUpdateFailure.None, otherAdmin.Failure);
        Assert.Equal(AdminUserStatusUpdateFailure.SelfDeactivation, lastAdmin.Failure);
        Assert.Single(await harness.DbContext.UserAdminAudits.ToListAsync());

        await using var isolated = await AdminUserHarness.CreateAsync(includeSecondAdmin: false);
        var blockedLast = await isolated.Service.UpdateStatusAsync(isolated.Staff.Id, isolated.AdminA.Id, false);
        Assert.Equal(AdminUserStatusUpdateFailure.LastActiveAdmin, blockedLast.Failure);
        Assert.Empty(await isolated.DbContext.UserAdminAudits.ToListAsync());
    }

    [Fact]
    public async Task UpdateStatusAsync_MissingTargetDoesNotCreateAudit()
    {
        await using var harness = await AdminUserHarness.CreateAsync();

        var result = await harness.Service.UpdateStatusAsync(harness.AdminA.Id, 999_999, false);

        Assert.Equal(AdminUserStatusUpdateFailure.NotFound, result.Failure);
        Assert.Empty(await harness.DbContext.UserAdminAudits.ToListAsync());
    }

    [Fact]
    public async Task GetUserAuditsAsync_ReturnsNewestFirstPagedSafeSnapshots()
    {
        await using var harness = await AdminUserHarness.CreateAsync();
        await harness.Service.UpdateStatusAsync(harness.AdminA.Id, harness.Staff.Id, false);
        await harness.Service.UpdateStatusAsync(harness.AdminB.Id, harness.Staff.Id, true);

        var firstPage = await harness.Service.GetUserAuditsAsync(harness.Staff.Id, page: 1, pageSize: 1);
        var secondPage = await harness.Service.GetUserAuditsAsync(harness.Staff.Id, page: 2, pageSize: 1);
        var missing = await harness.Service.GetUserAuditsAsync(999_999, page: 1, pageSize: 25);

        Assert.NotNull(firstPage);
        Assert.NotNull(secondPage);
        Assert.Equal(2, firstPage!.Pagination.TotalCount);
        Assert.True(firstPage.Pagination.HasNextPage);
        Assert.Equal(UserAdminAuditAction.Activated, Assert.Single(firstPage.Items).Action);
        Assert.Equal(UserAdminAuditAction.Deactivated, Assert.Single(secondPage!.Items).Action);
        Assert.Null(missing);
        Assert.DoesNotContain("PasswordHash", typeof(AdminUserAuditResponse).GetProperties().Select(property => property.Name));
        Assert.DoesNotContain("Token", typeof(AdminUserAuditResponse).GetProperties().Select(property => property.Name));
    }

    [Fact]
    public async Task Controller_IsAdminOnlyAndReturnsUnauthorizedWithoutIdentity()
    {
        await using var harness = await AdminUserHarness.CreateAsync();
        var attribute = Assert.Single(typeof(AdminUsersController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal(nameof(UserRole.Admin), attribute.Roles);
        Assert.DoesNotContain(nameof(UserRole.TechnicalStaff), attribute.Roles);
        Assert.DoesNotContain(nameof(UserRole.Customer), attribute.Roles);

        var controller = new AdminUsersController(harness.Service)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        var result = await controller.UpdateStatus(harness.Customer.Id, new UpdateUserStatusRequest { IsActive = false }, CancellationToken.None);
        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    private sealed class AdminUserHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private AdminUserHarness(SqliteConnection connection, AppDbContext dbContext, AdminUserManagementService service, UserService userService, User adminA, User adminB, User staff, User customer)
        {
            _connection = connection;
            DbContext = dbContext;
            Service = service;
            UserService = userService;
            AdminA = adminA;
            AdminB = adminB;
            Staff = staff;
            Customer = customer;
        }

        public AppDbContext DbContext { get; }
        public AdminUserManagementService Service { get; }
        public UserService UserService { get; }
        public User AdminA { get; }
        public User AdminB { get; }
        public User Staff { get; }
        public User Customer { get; }

        public static async Task<AdminUserHarness> CreateAsync(bool includeSecondAdmin = true)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var dbContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options);
            await dbContext.Database.EnsureCreatedAsync();

            var adminA = NewUser("admin-a@example.test", UserRole.Admin, Now.AddMinutes(-1));
            var adminB = NewUser("admin-b@example.test", UserRole.Admin, Now.AddMinutes(-2));
            var staff = NewUser("staff@example.test", UserRole.TechnicalStaff, Now.AddMinutes(-3));
            var customer = NewUser("customer@example.test", UserRole.Customer, Now.AddMinutes(-4));
            var inactiveCustomer = NewUser("inactive@example.test", UserRole.Customer, Now.AddMinutes(-5));
            inactiveCustomer.IsActive = false;
            dbContext.Users.AddRange(includeSecondAdmin ? [adminA, adminB, staff, customer, inactiveCustomer] : [adminA, staff, customer, inactiveCustomer]);
            await dbContext.SaveChangesAsync();

            var staffOpen = NewTicket("SUP-STAFF-OPEN", TicketStatus.InProgress, TicketPriority.Critical, staff.Id, null, Now.AddHours(-4));
            staffOpen.FirstResponseDueAt = Now.AddHours(-1);
            staffOpen.ResolutionDueAt = Now.AddHours(-1);
            var staffWaiting = NewTicket("SUP-STAFF-WAIT", TicketStatus.WaitingForCustomer, TicketPriority.Normal, staff.Id, null, Now.AddHours(-2));
            var staffResolved = NewTicket("SUP-STAFF-RESOLVED", TicketStatus.Resolved, TicketPriority.Normal, staff.Id, null, Now.AddHours(-8));
            var staffClosed = NewTicket("SUP-STAFF-CLOSED", TicketStatus.Closed, TicketPriority.Normal, staff.Id, null, Now.AddHours(-9));
            var customerOpen = NewTicket("SUP-CUSTOMER-OPEN", TicketStatus.New, TicketPriority.Normal, null, customer.Id, Now.AddHours(-1));
            var customerResolved = NewTicket("SUP-CUSTOMER-RESOLVED", TicketStatus.Resolved, TicketPriority.Normal, null, customer.Id, Now.AddHours(-2));
            var customerClosed = NewTicket("SUP-CUSTOMER-CLOSED", TicketStatus.Closed, TicketPriority.Normal, null, customer.Id, Now.AddHours(-3));
            dbContext.Tickets.AddRange(staffOpen, staffWaiting, staffResolved, staffClosed, customerOpen, customerResolved, customerClosed);
            await dbContext.SaveChangesAsync();
            dbContext.TicketMessages.Add(new TicketMessage
            {
                TicketId = staffOpen.Id,
                SenderId = customer.Id,
                Message = "Customer is waiting.",
                IsInternal = false,
                CreatedAt = Now.AddHours(-3)
            });
            dbContext.TicketMessages.Add(new TicketMessage
            {
                TicketId = staffWaiting.Id,
                SenderId = adminA.Id,
                Message = "Staff response delivered.",
                IsInternal = false,
                OutboundDeliveryStatus = OutboundDeliveryStatus.Sent,
                DeliveredAt = Now.AddHours(-1),
                CreatedAt = Now.AddHours(-1)
            });
            await dbContext.SaveChangesAsync();

            var queueAgingCalculator = new QueueAgingCalculator(new FixedTimeProvider(Now));
            var service = new AdminUserManagementService(dbContext, queueAgingCalculator, new TestSlaCalculator(staffOpen.Id), new FixedTimeProvider(Now));
            return new AdminUserHarness(connection, dbContext, service, new UserService(dbContext, new PasswordHasher<User>()), adminA, adminB, staff, customer);
        }

        private static User NewUser(string email, UserRole role, DateTime createdAt) => new()
        {
            FirstName = email.Split('@')[0],
            LastName = "User",
            Email = email,
            PasswordHash = "hash",
            Role = role,
            IsActive = true,
            CreatedAt = createdAt
        };

        private static Ticket NewTicket(string number, TicketStatus status, TicketPriority priority, int? assignedUserId, int? customerId, DateTime createdAt) => new()
        {
            TicketNumber = number,
            Title = number,
            Description = "Test ticket",
            Status = status,
            Priority = priority,
            AssignedUserId = assignedUserId,
            CustomerId = customerId,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }

    private sealed class TestSlaCalculator(int breachedTicketId) : ITicketSlaCalculator
    {
        public TicketSlaResponse Calculate(Ticket ticket) => new()
        {
            IsTracked = true,
            MostUrgentState = ticket.Id == breachedTicketId ? SlaState.Breached : SlaState.OnTrack
        };

        public TicketSlaSummaryResponse CalculateSummary(Ticket ticket) => new()
        {
            MostUrgentState = ticket.Id == breachedTicketId ? SlaState.Breached : SlaState.OnTrack
        };
    }
}
