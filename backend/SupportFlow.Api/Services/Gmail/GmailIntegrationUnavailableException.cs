namespace SupportFlow.Api.Services.Gmail;

public class GmailIntegrationUnavailableException : InvalidOperationException
{
    public GmailIntegrationUnavailableException(string message)
        : base(message)
    {
    }
}
