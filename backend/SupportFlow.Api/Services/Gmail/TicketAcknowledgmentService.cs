using SupportFlow.Api.Models;
using SupportFlow.Api.Services.Email;

namespace SupportFlow.Api.Services.Gmail;

public class TicketAcknowledgmentService : ITicketAcknowledgmentService
{
    private readonly IGmailSendService _gmailSendService;
    private readonly NotificationService _notificationService;
    private readonly ILogger<TicketAcknowledgmentService> _logger;

    public TicketAcknowledgmentService(
        IGmailSendService gmailSendService,
        NotificationService notificationService,
        ILogger<TicketAcknowledgmentService> logger)
    {
        _gmailSendService = gmailSendService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task TrySendNewTicketAcknowledgmentAsync(
        Ticket ticket,
        TicketEmailRecipient recipient,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _gmailSendService.SendPlainTextAsync(
                recipient.Email,
                $"[{ticket.TicketNumber}] {ticket.Title}",
                BuildBody(ticket, recipient),
                cancellationToken);
            await _notificationService.NotifyGmailAcknowledgmentSentAsync(
                ticket,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Ticket acknowledgment email failed after ticket persistence. Ticket: {TicketNumber}",
                ticket.TicketNumber);
            await _notificationService.NotifyGmailAcknowledgmentFailedAsync(
                ticket,
                cancellationToken);
        }
    }

    internal static string BuildBody(Ticket ticket, TicketEmailRecipient recipient)
    {
        var customerName = string.IsNullOrWhiteSpace(recipient.DisplayName)
            ? recipient.Email
            : recipient.DisplayName;

        return string.Join(
            '\n',
            [
                $"Merhaba {customerName},",
                string.Empty,
                "Destek talebiniz ba\u015far\u0131yla al\u0131nm\u0131\u015ft\u0131r.",
                string.Empty,
                "Talep No:",
                ticket.TicketNumber,
                string.Empty,
                "Konu:",
                ticket.Title,
                string.Empty,
                "Talebiniz teknik ekibimiz taraf\u0131ndan incelenecektir.",
                string.Empty,
                "Bu talebe ek bilgi g\u00f6ndermek veya yan\u0131t vermek i\u00e7in l\u00fctfen bu e-postay\u0131 yan\u0131tlay\u0131n.",
                string.Empty,
                "SupportFlow"
            ]);
    }
}
