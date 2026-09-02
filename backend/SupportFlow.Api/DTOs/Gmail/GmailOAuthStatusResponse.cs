namespace SupportFlow.Api.DTOs.Gmail;

public class GmailOAuthStatusResponse
{
    public bool Configured { get; set; }
    public bool Authorized { get; set; }
    public bool CanRead { get; set; }
    public bool CanSend { get; set; }
    public string Mailbox { get; set; } = string.Empty;
    public GmailBackgroundSyncStatusResponse? BackgroundSync { get; set; }
}
