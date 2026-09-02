using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using SupportFlow.Api.Settings;

namespace SupportFlow.Api.Services.Email;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IOptions<EmailSettings> settings,
        ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        if (!HasUsableConfiguration())
        {
            _logger.LogInformation(
                "Email skipped because SMTP is not configured. Recipient: {Recipient}; Subject: {Subject}",
                toEmail,
                subject);
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_settings.FromEmail, _settings.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(toEmail);

        using var smtpClient = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
        {
            EnableSsl = _settings.EnableSsl,
            UseDefaultCredentials = false
        };

        if (!string.IsNullOrWhiteSpace(_settings.Username))
        {
            smtpClient.Credentials = new NetworkCredential(
                _settings.Username,
                _settings.Password);
        }

        try
        {
            await smtpClient.SendMailAsync(message, cancellationToken);
            _logger.LogInformation(
                "Email sent. Recipient: {Recipient}; Subject: {Subject}",
                toEmail,
                subject);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Email delivery failed. Recipient: {Recipient}; Subject: {Subject}",
                toEmail,
                subject);
        }
    }

    private bool HasUsableConfiguration()
    {
        var credentialsAreConsistent =
            string.IsNullOrWhiteSpace(_settings.Username)
                == string.IsNullOrWhiteSpace(_settings.Password);

        return !string.IsNullOrWhiteSpace(_settings.SmtpHost)
            && _settings.SmtpPort > 0
            && !string.IsNullOrWhiteSpace(_settings.FromEmail)
            && credentialsAreConsistent;
    }
}
