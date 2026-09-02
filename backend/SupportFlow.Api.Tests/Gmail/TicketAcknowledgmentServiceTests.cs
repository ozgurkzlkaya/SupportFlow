using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SupportFlow.Api.Data;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;
using SupportFlow.Api.Services;
using SupportFlow.Api.Services.Email;
using SupportFlow.Api.Services.Gmail;

namespace SupportFlow.Api.Tests.Gmail;

public sealed class TicketAcknowledgmentServiceTests
{
    [Fact]
    public async Task TrySendNewTicketAcknowledgmentAsync_UsesExactSubjectFormat()
    {
        var gmailSendService = new RecordingGmailSendService();
        await using var dbContext = await CreateDbContextAsync();
        var customer = new User
        {
            FirstName = "Ayşe",
            LastName = "Yılmaz",
            Email = "customer@example.test",
            PasswordHash = "hash",
            Role = UserRole.Customer,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(customer);
        await dbContext.SaveChangesAsync();

        var ticket = new Ticket
        {
            TicketNumber = "SUP-2026-001009",
            CustomerId = customer.Id,
            Title = "VPN bağlantı problemi",
            Description = "desc",
            Status = TicketStatus.New,
            Priority = TicketPriority.Normal,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync();

        var service = new TicketAcknowledgmentService(
            gmailSendService,
            new NotificationService(dbContext),
            NullLogger<TicketAcknowledgmentService>.Instance);

        await service.TrySendNewTicketAcknowledgmentAsync(ticket, TicketEmailRecipient.From(customer));

        var sent = Assert.Single(gmailSendService.SentMessages);
        Assert.Equal("[SUP-2026-001009] VPN bağlantı problemi", sent.Subject);
        Assert.Contains("SUP-2026-001009", sent.Body, StringComparison.Ordinal);
        Assert.Contains("VPN bağlantı problemi", sent.Body, StringComparison.Ordinal);
        var notification = Assert.Single(await dbContext.Notifications.ToListAsync());
        Assert.Equal(NotificationType.GmailOutboundSuccess, notification.Type);
    }

    [Fact]
    public async Task TrySendNewTicketAcknowledgmentAsync_OnFailure_CreatesWarningNotification()
    {
        var gmailSendService = new RecordingGmailSendService
        {
            ThrowOnSend = new InvalidOperationException("boom")
        };
        await using var dbContext = await CreateDbContextAsync();
        var customer = new User
        {
            FirstName = "Ayşe",
            LastName = "Yılmaz",
            Email = "customer@example.test",
            PasswordHash = "hash",
            Role = UserRole.Customer,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(customer);
        await dbContext.SaveChangesAsync();

        var ticket = new Ticket
        {
            TicketNumber = "SUP-2026-001009",
            CustomerId = customer.Id,
            Title = "VPN bağlantı problemi",
            Description = "desc",
            Status = TicketStatus.New,
            Priority = TicketPriority.Normal,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync();

        var service = new TicketAcknowledgmentService(
            gmailSendService,
            new NotificationService(dbContext),
            NullLogger<TicketAcknowledgmentService>.Instance);

        await service.TrySendNewTicketAcknowledgmentAsync(ticket, TicketEmailRecipient.From(customer));

        var notification = Assert.Single(await dbContext.Notifications.ToListAsync());
        Assert.Equal(NotificationType.GmailOutboundFailure, notification.Type);
    }

    private sealed class RecordingGmailSendService : IGmailSendService
    {
        public List<SentMessage> SentMessages { get; } = [];
        public Exception? ThrowOnSend { get; set; }

        public Task SendPlainTextAsync(
            string toEmail,
            string subject,
            string body,
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnSend is not null)
            {
                throw ThrowOnSend;
            }

            SentMessages.Add(new SentMessage(toEmail, subject, body));
            return Task.CompletedTask;
        }
    }

    private static async Task<AppDbContext> CreateDbContextAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        dbContext.Users.Add(new User
        {
            FirstName = "Admin",
            LastName = "User",
            Email = "admin@example.test",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        return dbContext;
    }

    private sealed record SentMessage(string ToEmail, string Subject, string Body);
}
