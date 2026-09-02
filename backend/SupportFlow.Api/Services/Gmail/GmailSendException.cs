namespace SupportFlow.Api.Services.Gmail;

public sealed class GmailSendException : InvalidOperationException
{
    public GmailSendException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
