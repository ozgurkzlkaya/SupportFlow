using Microsoft.EntityFrameworkCore;
using SupportFlow.Api.Data;
using SupportFlow.Api.DTOs.Notifications;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;

namespace SupportFlow.Api.Services;

public class NotificationService
{
    private readonly AppDbContext _dbContext;

    public NotificationService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<NotificationResponse>> GetNotificationsAsync(
        int userId,
        bool unreadOnly,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId);

        if (unreadOnly)
        {
            query = query.Where(notification => !notification.IsRead);
        }

        return await query
            .OrderByDescending(notification => notification.CreatedAt)
            .Select(notification => new NotificationResponse
            {
                Id = notification.Id,
                TicketId = notification.TicketId,
                Title = notification.Title,
                Message = notification.Message,
                Type = notification.Type,
                IsRead = notification.IsRead,
                CreatedAt = notification.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public Task<int> GetUnreadCountAsync(
        int userId,
        CancellationToken cancellationToken = default) =>
        _dbContext.Notifications
            .AsNoTracking()
            .CountAsync(
                notification => notification.UserId == userId && !notification.IsRead,
                cancellationToken);

    public async Task<bool> MarkAsReadAsync(
        int notificationId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var notification = await _dbContext.Notifications
            .SingleOrDefaultAsync(
                item => item.Id == notificationId && item.UserId == userId,
                cancellationToken);

        if (notification is null)
        {
            return false;
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<int> MarkAllAsReadAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var notifications = await _dbContext.Notifications
            .Where(notification => notification.UserId == userId && !notification.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
        }

        if (notifications.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return notifications.Count;
    }

    public Task NotifyNewTicketCreatedAsync(
        Ticket ticket,
        User customer,
        CancellationToken cancellationToken = default) =>
        NotifyTechnicalAndAdminUsersAsync(
            "Yeni Talep",
            $"{ticket.TicketNumber} için yeni bir destek talebi oluşturuldu.",
            NotificationType.TicketCreated,
            ticket.Id,
            cancellationToken);

    public Task NotifyCustomerReplyAsync(
        Ticket ticket,
        User customer,
        CancellationToken cancellationToken = default)
    {
        var message = ticket.AssignedUserId.HasValue
            ? $"{ticket.TicketNumber} için müşteriden yeni bir yanıt geldi."
            : $"{ticket.TicketNumber} için müşteri yanıtı kuyrukta işlem bekliyor.";

        return NotifyAssignedOrRelevantStaffAsync(
            ticket,
            "Yeni Müşteri Yanıtı",
            message,
            NotificationType.CustomerReply,
            cancellationToken);
    }

    public Task NotifyTechnicalReplyAsync(
        Ticket ticket,
        User sender,
        CancellationToken cancellationToken = default) =>
        NotifySpecificUsersAsync(
            ticket.CustomerId.HasValue ? [ticket.CustomerId.Value] : [],
            "Destek Yanıtı",
            $"{ticket.TicketNumber} için destek ekibi yanıt gönderdi.",
            NotificationType.TechnicalReply,
            ticket.Id,
            cancellationToken);

    public Task NotifyAssignmentAsync(
        Ticket ticket,
        User assignedUser,
        CancellationToken cancellationToken = default) =>
        NotifySpecificUsersAsync(
            [assignedUser.Id],
            "Talep Size Atandı",
            $"{ticket.TicketNumber} numaralı talep size atandı.",
            NotificationType.Assignment,
            ticket.Id,
            cancellationToken);

    public Task NotifyStatusChangeAsync(
        Ticket ticket,
        TicketStatus newStatus,
        CancellationToken cancellationToken = default) =>
        NotifySpecificUsersAsync(
            ticket.CustomerId.HasValue ? [ticket.CustomerId.Value] : [],
            "Durum Güncellendi",
            $"{ticket.TicketNumber} durumu {GetStatusLabel(newStatus)} olarak güncellendi.",
            NotificationType.StatusChange,
            ticket.Id,
            cancellationToken);

    public Task NotifyInboundGmailNewTicketAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default) =>
        NotifyTechnicalAndAdminUsersAsync(
            "E-posta Talebi Alındı",
            $"{ticket.TicketNumber} için yeni müşteri e-postası işlendi.",
            NotificationType.GmailInbound,
            ticket.Id,
            cancellationToken);

    public Task NotifyInboundGmailReplyAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default) =>
        NotifyAssignedOrRelevantStaffAsync(
            ticket,
            "E-posta Yanıtı Alındı",
            $"{ticket.TicketNumber} için e-posta yanıtı alındı.",
            NotificationType.GmailInbound,
            cancellationToken);

    public Task NotifyGmailAcknowledgmentSentAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default) =>
        NotifyAdminUsersAsync(
            "E-posta Gönderildi",
            $"{ticket.TicketNumber} için bilgilendirme e-postası gönderildi.",
            NotificationType.GmailOutboundSuccess,
            ticket.Id,
            cancellationToken);

    public Task NotifyGmailAcknowledgmentFailedAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default) =>
        NotifyAdminUsersAsync(
            "E-posta Gönderilemedi",
            $"{ticket.TicketNumber} için bilgilendirme e-postası gönderilemedi.",
            NotificationType.GmailOutboundFailure,
            ticket.Id,
            cancellationToken);

    private Task NotifyAssignedOrRelevantStaffAsync(
        Ticket ticket,
        string title,
        string message,
        NotificationType type,
        CancellationToken cancellationToken)
    {
        if (ticket.AssignedUserId.HasValue)
        {
            return NotifySpecificUsersAsync(
                [ticket.AssignedUserId.Value],
                title,
                message,
                type,
                ticket.Id,
                cancellationToken);
        }

        return NotifyTechnicalUsersAsync(
            title,
            message,
            type,
            ticket.Id,
            cancellationToken);
    }

    private async Task NotifyTechnicalUsersAsync(
        string title,
        string message,
        NotificationType type,
        int? ticketId,
        CancellationToken cancellationToken)
    {
        var userIds = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.IsActive && user.Role == UserRole.TechnicalStaff)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        await CreateNotificationsAsync(userIds, title, message, type, ticketId, cancellationToken);
    }

    private async Task NotifyTechnicalAndAdminUsersAsync(
        string title,
        string message,
        NotificationType type,
        int? ticketId,
        CancellationToken cancellationToken)
    {
        var userIds = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.IsActive && (user.Role == UserRole.TechnicalStaff || user.Role == UserRole.Admin))
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        await CreateNotificationsAsync(userIds, title, message, type, ticketId, cancellationToken);
    }

    private async Task NotifyAdminUsersAsync(
        string title,
        string message,
        NotificationType type,
        int? ticketId,
        CancellationToken cancellationToken)
    {
        var userIds = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.IsActive && user.Role == UserRole.Admin)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        await CreateNotificationsAsync(userIds, title, message, type, ticketId, cancellationToken);
    }

    private async Task NotifySpecificUsersAsync(
        IReadOnlyCollection<int> userIds,
        string title,
        string message,
        NotificationType type,
        int? ticketId,
        CancellationToken cancellationToken)
    {
        var activeUserIds = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.IsActive && userIds.Contains(user.Id))
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        await CreateNotificationsAsync(activeUserIds, title, message, type, ticketId, cancellationToken);
    }

    private async Task CreateNotificationsAsync(
        IReadOnlyCollection<int> userIds,
        string title,
        string message,
        NotificationType type,
        int? ticketId,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var notifications = userIds
            .Distinct()
            .Select(userId => new Notification
            {
                UserId = userId,
                TicketId = ticketId,
                Title = title,
                Message = message,
                Type = type,
                IsRead = false,
                CreatedAt = now
            })
            .ToList();

        _dbContext.Notifications.AddRange(notifications);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string GetStatusLabel(TicketStatus status) =>
        status switch
        {
            TicketStatus.New => "Yeni",
            TicketStatus.Assigned => "Atandı",
            TicketStatus.InProgress => "İşlemde",
            TicketStatus.WaitingForCustomer => "Müşteri Bekleniyor",
            TicketStatus.Resolved => "Çözüldü",
            TicketStatus.Closed => "Kapatıldı",
            TicketStatus.Reopened => "Yeniden Açıldı",
            _ => status.ToString()
        };
}
