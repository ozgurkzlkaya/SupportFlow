namespace SupportFlow.Api.DTOs.Gmail;

public class GmailOAuthStartResponse
{
    public string AuthorizationUrl { get; set; } = string.Empty;
    public string? CompletionReturnUri { get; set; }
}
