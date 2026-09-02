namespace SupportFlow.Api.Services.Gmail;

public interface IGmailSendService
{
    Task SendPlainTextAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken = default);
}
