namespace SupportFlow.Api.Settings;

public sealed class GmailBackgroundSyncSettings
{
    public const string SectionName = "GmailBackgroundSync";

    public bool Enabled { get; set; }
    public int IntervalSeconds { get; set; } = 600;
    public int InitialDelaySeconds { get; set; } = 30;
}
