namespace SupportFlow.Api.Services.Gmail;

public class GmailAuthorizationRequiredException : InvalidOperationException
{
    public GmailAuthorizationRequiredException(string message)
        : base(message)
    {
    }
}
