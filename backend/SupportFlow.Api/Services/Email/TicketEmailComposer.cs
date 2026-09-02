using Microsoft.Extensions.Options;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;
using SupportFlow.Api.Settings;

namespace SupportFlow.Api.Services.Email;

public class TicketEmailComposer
{
    private readonly string _frontendBaseUrl;

    public TicketEmailComposer(IOptions<ApplicationSettings> settings)
    {
        _frontendBaseUrl = settings.Value.FrontendBaseUrl.TrimEnd('/');
    }

    public EmailContent ComposeNewTicket(Ticket ticket, User customer)
    {
        return new EmailContent(
            $"Yeni destek talebi: {ticket.TicketNumber}",
            $"""
            Yeni bir destek talebi oluşturuldu.

            Talep No: {ticket.TicketNumber}
            Müşteri: {customer.FirstName} {customer.LastName}
            Konu: {ticket.Title}
            Öncelik: {GetPriorityLabel(ticket.Priority)}
            Oluşturulma: {FormatDate(ticket.CreatedAt)}

            Talebi görüntüleyin: {GetTechnicalTicketUrl(ticket.Id)}
            """);
    }

    public EmailContent ComposeNewTicket(Ticket ticket, ExternalRequester requester) =>
        new(
            $"Yeni destek talebi: {ticket.TicketNumber}",
            $"Yeni bir destek talebi oluşturuldu.\n\nTalep No: {ticket.TicketNumber}\nMüşteri: {requester.DisplayName ?? requester.Email}\nKonu: {ticket.Title}\nÖncelik: {GetPriorityLabel(ticket.Priority)}\nOluşturulma: {FormatDate(ticket.CreatedAt)}\n\nTalebi görüntüleyin: {GetTechnicalTicketUrl(ticket.Id)}");

    public EmailContent ComposeExternalRequesterTechnicalReply(
        Ticket ticket,
        User sender,
        string message) =>
        new(
            BuildCustomerTicketSubject(ticket),
            $"Destek talebinize yeni bir yanit gonderildi.\n\nTalep No: {ticket.TicketNumber}\nKonu: {ticket.Title}\nYanitlayan: {sender.FirstName} {sender.LastName}\nDurum: {GetStatusLabel(ticket.Status)}\nYanit: {GetMessagePreview(message)}\n\nEk bilgi vermek icin bu e-postayi yanitlayabilirsiniz.");

    public EmailContent ComposeTechnicalReply(
        Ticket ticket,
        User sender,
        string message)
    {
        return new EmailContent(
            BuildCustomerTicketSubject(ticket),
            $"""
            Destek talebinize yeni bir yanıt gönderildi.

            Talep No: {ticket.TicketNumber}
            Konu: {ticket.Title}
            Yanıtlayan: {sender.FirstName} {sender.LastName}
            Durum: {GetStatusLabel(ticket.Status)}
            Yanıt: {GetMessagePreview(message)}

            Talebi görüntüleyin: {GetCustomerTicketUrl(ticket.Id)}
            """);
    }

    public EmailContent ComposeCustomerReply(
        Ticket ticket,
        User customer,
        string message,
        bool wasReopened)
    {
        var reopenExplanation = wasReopened
            ? "\nMüşteri kapatılmış/çözülmüş talebe yeni bir yanıt gönderdi ve talep yeniden açıldı.\n"
            : string.Empty;

        return new EmailContent(
            wasReopened
                ? $"{ticket.TicketNumber} talebi yeniden açıldı"
                : $"{ticket.TicketNumber} talebine müşteri yanıtı geldi",
            $"""
            Müşteri destek talebine yeni bir yanıt gönderdi.
            {reopenExplanation}
            Talep No: {ticket.TicketNumber}
            Müşteri: {customer.FirstName} {customer.LastName}
            Konu: {ticket.Title}
            Yanıt: {GetMessagePreview(message)}

            Talebi görüntüleyin: {GetTechnicalTicketUrl(ticket.Id)}
            """);
    }

    public EmailContent ComposeAssignment(
        Ticket ticket,
        User customer,
        User assignedUser)
    {
        return new EmailContent(
            $"{ticket.TicketNumber} talebi size atandı",
            $"""
            Bir destek talebi size atandı.

            Talep No: {ticket.TicketNumber}
            Atanan Personel: {assignedUser.FirstName} {assignedUser.LastName}
            Müşteri: {customer.FirstName} {customer.LastName}
            Konu: {ticket.Title}
            Öncelik: {GetPriorityLabel(ticket.Priority)}
            Durum: {GetStatusLabel(ticket.Status)}

            Talebi görüntüleyin: {GetTechnicalTicketUrl(ticket.Id)}
            """);
    }

    public EmailContent ComposeStatusChange(
        Ticket ticket,
        TicketStatus oldStatus,
        TicketStatus newStatus,
        string? note)
    {
        var noteLine = string.IsNullOrWhiteSpace(note)
            ? string.Empty
            : $"\nDurum Notu: {note.Trim()}";

        return new EmailContent(
            BuildCustomerTicketSubject(ticket),
            $"""
            Destek talebinizin durumu güncellendi.

            Talep No: {ticket.TicketNumber}
            Konu: {ticket.Title}
            Önceki Durum: {GetStatusLabel(oldStatus)}
            Yeni Durum: {GetStatusLabel(newStatus)}{noteLine}

            Talebi görüntüleyin: {GetCustomerTicketUrl(ticket.Id)}
            """);
    }

    private string GetCustomerTicketUrl(int ticketId) =>
        GetTicketUrl($"customer/tickets/{ticketId}");

    private static string BuildCustomerTicketSubject(Ticket ticket) =>
        $"[{ticket.TicketNumber}] {ticket.Title}";

    private string GetTechnicalTicketUrl(int ticketId) =>
        GetTicketUrl($"technical/tickets/{ticketId}");

    private string GetTicketUrl(string relativePath) =>
        string.IsNullOrWhiteSpace(_frontendBaseUrl)
            ? "SupportFlow uygulaması"
            : $"{_frontendBaseUrl}/{relativePath}";

    private static string GetMessagePreview(string message)
    {
        const int maximumLength = 400;
        var normalized = message.Trim();
        return normalized.Length <= maximumLength
            ? normalized
            : normalized[..maximumLength] + "…";
    }

    private static string FormatDate(DateTime value) =>
        value.ToLocalTime().ToString("dd.MM.yyyy HH:mm");

    private static string GetPriorityLabel(TicketPriority priority) => priority switch
    {
        TicketPriority.Low => "Düşük",
        TicketPriority.Normal => "Normal",
        TicketPriority.High => "Yüksek",
        TicketPriority.Critical => "Kritik",
        _ => priority.ToString()
    };

    private static string GetStatusLabel(TicketStatus status) => status switch
    {
        TicketStatus.New => "Yeni",
        TicketStatus.Assigned => "Atandı",
        TicketStatus.InProgress => "Devam Ediyor",
        TicketStatus.WaitingForCustomer => "Müşteriden Bilgi Bekleniyor",
        TicketStatus.Resolved => "Çözüldü",
        TicketStatus.Closed => "Kapatıldı",
        TicketStatus.Reopened => "Yeniden Açıldı",
        _ => status.ToString()
    };
}

public sealed record EmailContent(string Subject, string Body);
