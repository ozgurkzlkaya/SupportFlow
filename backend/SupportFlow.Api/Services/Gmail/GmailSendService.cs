using System.Globalization;
using System.Net.Mail;
using System.Text;
using Google;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Options;
using SupportFlow.Api.Settings;

namespace SupportFlow.Api.Services.Gmail;

public class GmailSendService : IGmailSendService
{
    private readonly IGmailAuthorizationService _gmailAuthorizationService;
    private readonly string _mailboxAddress;
    private readonly string _applicationName;
    private readonly IGmailApiRateLimitState _rateLimitState;

    public GmailSendService(
        IGmailAuthorizationService gmailAuthorizationService,
        IOptions<GmailSettings> settings,
        IGmailApiRateLimitState rateLimitState)
    {
        _gmailAuthorizationService = gmailAuthorizationService;
        _mailboxAddress = settings.Value.MailboxAddress;
        _applicationName = settings.Value.ApplicationName;
        _rateLimitState = rateLimitState;
    }

    public async Task SendPlainTextAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        var status = await _gmailAuthorizationService.GetStatusAsync(cancellationToken);
        if (!status.Configured)
        {
            throw new GmailIntegrationUnavailableException("Gmail integration is unavailable.");
        }

        if (!status.Authorized || !status.CanSend)
        {
            throw new GmailAuthorizationRequiredException(
                "Gmail reauthorization with gmail.send scope is required before acknowledgment emails can be sent.");
        }

        if (_rateLimitState.TryGetRateLimitedUntil(out var retryAt))
        {
            throw new GmailRateLimitedException(retryAt);
        }

        try
        {
            using var service = await CreateServiceAsync(cancellationToken);
            var request = service.Users.Messages.Send(
                new Message
                {
                    Raw = BuildRawMessage(
                        _mailboxAddress,
                        toEmail,
                        subject,
                        body)
                },
                "me");

            await request.ExecuteAsync(cancellationToken);
        }
        catch (GmailIntegrationUnavailableException)
        {
            throw;
        }
        catch (GmailAuthorizationRequiredException)
        {
            throw;
        }
        catch (GoogleApiException exception)
            when (GmailApiRateLimitDetector.TryHandle(
                exception,
                _rateLimitState,
                out _))
        {
            throw new GmailSendException(
                "The Gmail API temporarily rate limited the acknowledgment email send request.",
                exception);
        }
        catch (GoogleApiException exception)
        {
            throw new GmailSendException(
                "The Gmail API rejected the acknowledgment email send request.",
                exception);
        }
        catch (FormatException exception)
        {
            throw new GmailSendException(
                "The acknowledgment email address or content could not be formatted for Gmail delivery.",
                exception);
        }
    }

    private async Task<GmailService> CreateServiceAsync(
        CancellationToken cancellationToken)
    {
        var credential = await _gmailAuthorizationService.GetAuthorizedCredentialAsync(
            cancellationToken);

        return new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = _applicationName
        });
    }

    internal static string BuildRawMessage(
        string fromEmail,
        string toEmail,
        string subject,
        string body)
    {
        var from = new MailAddress(fromEmail).Address;
        var to = new MailAddress(toEmail).Address;
        var mimeBuilder = new StringBuilder();
        mimeBuilder.Append("From: ").Append(from).Append("\r\n");
        mimeBuilder.Append("To: ").Append(to).Append("\r\n");
        mimeBuilder.Append("Subject: ").Append(EncodeHeader(subject)).Append("\r\n");
        mimeBuilder.Append("Date: ")
            .Append(DateTimeOffset.UtcNow.ToString(
                "ddd, dd MMM yyyy HH:mm:ss zzz",
                CultureInfo.InvariantCulture))
            .Append("\r\n");
        mimeBuilder.Append("MIME-Version: 1.0\r\n");
        mimeBuilder.Append("Content-Type: text/plain; charset=utf-8\r\n");
        mimeBuilder.Append("Content-Transfer-Encoding: base64\r\n");
        mimeBuilder.Append("\r\n");
        mimeBuilder.Append(WrapBase64(body));

        return Base64UrlEncode(Encoding.UTF8.GetBytes(mimeBuilder.ToString()));
    }

    private static string EncodeHeader(string value) =>
        $"=?utf-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(value))}?=";

    private static string WrapBase64(string value)
    {
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        var builder = new StringBuilder(base64.Length + (base64.Length / 76 + 1) * 2);

        for (var index = 0; index < base64.Length; index += 76)
        {
            var length = Math.Min(76, base64.Length - index);
            builder.Append(base64, index, length).Append("\r\n");
        }

        return builder.ToString();
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
