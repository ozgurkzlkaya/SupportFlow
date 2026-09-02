using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportFlow.Api.Data;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;
using SupportFlow.Api.Services;

namespace SupportFlow.Api.Tests.Notifications;

public sealed class NotificationServiceTests
{
    [Fact]
    public async Task GetNotificationsAsync_ReturnsOnlyCurrentUsersNotifications()
    {
        await using var harness = await NotificationHarness.CreateAsync();

        var notifications = await harness.Service.GetNotificationsAsync(harness.Customer.Id, false);

        var notification = Assert.Single(notifications);
        Assert.Equal("Müşteri bildirimi", notification.Title);
        Assert.DoesNotContain(notifications, item => item.Title == "Teknik bildirimi");
    }

    [Fact]
    public async Task MarkAsReadAsync_DoesNotAllowCrossUserAccess()
    {
        await using var harness = await NotificationHarness.CreateAsync();

        var updated = await harness.Service.MarkAsReadAsync(
            harness.TechnicalNotification.Id,
            harness.Customer.Id);

        Assert.False(updated);
        Assert.False((await harness.ReloadNotificationAsync(harness.TechnicalNotification.Id)).IsRead);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_UpdatesOnlyUnreadNotificationsForCurrentUser()
    {
        await using var harness = await NotificationHarness.CreateAsync();
        harness.DbContext.Notifications.Add(new Notification
        {
            UserId = harness.Customer.Id,
            Title = "İkinci bildirim",
            Message = "Açık",
            Type = NotificationType.CustomerReply,
            IsRead = false,
            CreatedAt = DateTime.UtcNow.AddMinutes(1)
        });
        await harness.DbContext.SaveChangesAsync();

        var updatedCount = await harness.Service.MarkAllAsReadAsync(harness.Customer.Id);

        Assert.Equal(2, updatedCount);
        Assert.All(
            await harness.DbContext.Notifications.Where(item => item.UserId == harness.Customer.Id).ToListAsync(),
            notification => Assert.True(notification.IsRead));
        Assert.False((await harness.ReloadNotificationAsync(harness.TechnicalNotification.Id)).IsRead);
    }

    [Fact]
    public async Task NotifyNewTicketCreatedAsync_NotifiesActiveTechnicalStaffAndAdmins()
    {
        await using var harness = await NotificationHarness.CreateAsync();

        await harness.Service.NotifyNewTicketCreatedAsync(
            harness.UnassignedTicket,
            harness.Customer,
            CancellationToken.None);

        var notifications = await harness.GetNotificationsForTicketAsync(
            harness.UnassignedTicket.Id,
            NotificationType.TicketCreated);
        Assert.Equal(
            new[] { harness.TechnicalStaff.Id, harness.Admin.Id },
            notifications.Select(item => item.UserId).OrderBy(id => id).ToArray());
    }

    [Fact]
    public async Task NotifyCustomerReplyAsync_AssignedTicket_NotifiesOnlyAssignedTechnicalStaff()
    {
        await using var harness = await NotificationHarness.CreateAsync();

        await harness.Service.NotifyCustomerReplyAsync(
            harness.AssignedTicket,
            harness.Customer,
            CancellationToken.None);

        var notifications = await harness.GetNotificationsByTypeAsync(NotificationType.CustomerReply);
        var notification = Assert.Single(notifications);
        Assert.Equal(harness.TechnicalStaff.Id, notification.UserId);
    }

    [Fact]
    public async Task NotifyCustomerReplyAsync_UnassignedTicket_NotifiesOnlyActiveTechnicalStaff()
    {
        await using var harness = await NotificationHarness.CreateAsync();

        await harness.Service.NotifyCustomerReplyAsync(
            harness.UnassignedTicket,
            harness.Customer,
            CancellationToken.None);

        var notifications = await harness.GetNotificationsByTypeAsync(NotificationType.CustomerReply);
        var notification = Assert.Single(notifications);
        Assert.Equal(harness.TechnicalStaff.Id, notification.UserId);
        Assert.DoesNotContain(notifications, item => item.UserId == harness.Admin.Id);
        Assert.DoesNotContain(notifications, item => item.UserId == harness.InactiveTechnicalStaff.Id);
    }

    [Fact]
    public async Task NotifyTechnicalReplyAsync_NotifiesOnlyCustomer()
    {
        await using var harness = await NotificationHarness.CreateAsync();

        await harness.Service.NotifyTechnicalReplyAsync(
            harness.AssignedTicket,
            harness.TechnicalStaff,
            CancellationToken.None);

        var notifications = await harness.GetNotificationsForTicketAsync(
            harness.AssignedTicket.Id,
            NotificationType.TechnicalReply);
        var notification = Assert.Single(notifications);
        Assert.Equal(harness.Customer.Id, notification.UserId);
    }

    [Fact]
    public async Task NotifyAssignmentAsync_NotifiesOnlyAssignee()
    {
        await using var harness = await NotificationHarness.CreateAsync();

        await harness.Service.NotifyAssignmentAsync(
            harness.AssignedTicket,
            harness.TechnicalStaff,
            CancellationToken.None);

        var notifications = await harness.GetNotificationsForTicketAsync(
            harness.AssignedTicket.Id,
            NotificationType.Assignment);
        var notification = Assert.Single(notifications);
        Assert.Equal(harness.TechnicalStaff.Id, notification.UserId);
    }

    [Fact]
    public async Task NotifyInboundGmailReplyAsync_UnassignedTicket_NotifiesOnlyActiveTechnicalStaff()
    {
        await using var harness = await NotificationHarness.CreateAsync();

        await harness.Service.NotifyInboundGmailReplyAsync(
            harness.UnassignedTicket,
            CancellationToken.None);

        var notifications = await harness.GetNotificationsByTypeAsync(NotificationType.GmailInbound);
        var notification = Assert.Single(notifications);
        Assert.Equal(harness.TechnicalStaff.Id, notification.UserId);
    }

    [Fact]
    public async Task NotifyGmailAcknowledgmentFailedAsync_NotifiesOnlyAdmins()
    {
        await using var harness = await NotificationHarness.CreateAsync();

        await harness.Service.NotifyGmailAcknowledgmentFailedAsync(
            harness.AssignedTicket,
            CancellationToken.None);

        var notifications = await harness.GetNotificationsByTypeAsync(NotificationType.GmailOutboundFailure);
        var notification = Assert.Single(notifications);
        Assert.Equal(harness.Admin.Id, notification.UserId);
    }

    private sealed class NotificationHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private NotificationHarness(
            SqliteConnection connection,
            AppDbContext dbContext,
            NotificationService service,
            User customer,
            User technicalStaff,
            User inactiveTechnicalStaff,
            User admin,
            Ticket unassignedTicket,
            Ticket assignedTicket,
            Notification customerNotification,
            Notification technicalNotification)
        {
            _connection = connection;
            DbContext = dbContext;
            Service = service;
            Customer = customer;
            TechnicalStaff = technicalStaff;
            InactiveTechnicalStaff = inactiveTechnicalStaff;
            Admin = admin;
            UnassignedTicket = unassignedTicket;
            AssignedTicket = assignedTicket;
            CustomerNotification = customerNotification;
            TechnicalNotification = technicalNotification;
        }

        public AppDbContext DbContext { get; }
        public NotificationService Service { get; }
        public User Customer { get; }
        public User TechnicalStaff { get; }
        public User InactiveTechnicalStaff { get; }
        public User Admin { get; }
        public Ticket UnassignedTicket { get; }
        public Ticket AssignedTicket { get; }
        public Notification CustomerNotification { get; }
        public Notification TechnicalNotification { get; }

        public static async Task<NotificationHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            var dbContext = new AppDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();

            var customer = new User
            {
                FirstName = "Müşteri",
                LastName = "Kullanıcı",
                Email = "customer@example.test",
                PasswordHash = "hash",
                Role = UserRole.Customer,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var technicalStaff = new User
            {
                FirstName = "Teknik",
                LastName = "Kullanıcı",
                Email = "technical@example.test",
                PasswordHash = "hash",
                Role = UserRole.TechnicalStaff,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var inactiveTechnicalStaff = new User
            {
                FirstName = "Pasif",
                LastName = "Teknik",
                Email = "inactive-tech@example.test",
                PasswordHash = "hash",
                Role = UserRole.TechnicalStaff,
                IsActive = false,
                CreatedAt = DateTime.UtcNow
            };
            var admin = new User
            {
                FirstName = "Yönetici",
                LastName = "Kullanıcı",
                Email = "admin@example.test",
                PasswordHash = "hash",
                Role = UserRole.Admin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.Users.AddRange(customer, technicalStaff, inactiveTechnicalStaff, admin);
            await dbContext.SaveChangesAsync();

            var unassignedTicket = new Ticket
            {
                TicketNumber = "SUP-2026-002001",
                CustomerId = customer.Id,
                Title = "Yeni ticket",
                Description = "Açıklama",
                Status = TicketStatus.New,
                Priority = TicketPriority.Normal,
                CreatedAt = DateTime.UtcNow.AddMinutes(-20),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-20)
            };
            var assignedTicket = new Ticket
            {
                TicketNumber = "SUP-2026-002002",
                CustomerId = customer.Id,
                AssignedUserId = technicalStaff.Id,
                Title = "Atanmış ticket",
                Description = "Açıklama",
                Status = TicketStatus.InProgress,
                Priority = TicketPriority.High,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
            };

            dbContext.Tickets.AddRange(unassignedTicket, assignedTicket);
            await dbContext.SaveChangesAsync();

            var customerNotification = new Notification
            {
                UserId = customer.Id,
                Title = "Müşteri bildirimi",
                Message = "Açık",
                Type = NotificationType.TechnicalReply,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };
            var technicalNotification = new Notification
            {
                UserId = technicalStaff.Id,
                Title = "Teknik bildirimi",
                Message = "Açık",
                Type = NotificationType.Assignment,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.Notifications.AddRange(customerNotification, technicalNotification);
            await dbContext.SaveChangesAsync();

            return new NotificationHarness(
                connection,
                dbContext,
                new NotificationService(dbContext),
                customer,
                technicalStaff,
                inactiveTechnicalStaff,
                admin,
                unassignedTicket,
                assignedTicket,
                customerNotification,
                technicalNotification);
        }

        public Task<Notification> ReloadNotificationAsync(int notificationId) =>
            DbContext.Notifications.SingleAsync(notification => notification.Id == notificationId);

        public Task<List<Notification>> GetNotificationsByTypeAsync(NotificationType type) =>
            DbContext.Notifications
                .Where(notification => notification.Type == type)
                .OrderBy(notification => notification.Id)
                .ToListAsync();

        public Task<List<Notification>> GetNotificationsForTicketAsync(int ticketId, NotificationType type) =>
            DbContext.Notifications
                .Where(notification => notification.TicketId == ticketId && notification.Type == type)
                .OrderBy(notification => notification.Id)
                .ToListAsync();

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
