namespace SupportFlow.Api.Services.Gmail;

public enum GmailOAuthReturnTarget
{
    Web,
    Mobile
}

public sealed record GmailAuthorizationCompletionResult(
    string Message,
    GmailOAuthReturnTarget ReturnTarget);

public sealed class GmailOAuthCompletionException : InvalidOperationException
{
    public GmailOAuthCompletionException(
        string message,
        GmailOAuthReturnTarget returnTarget)
        : base(message)
    {
        ReturnTarget = returnTarget;
    }

    public GmailOAuthReturnTarget ReturnTarget { get; }
}
