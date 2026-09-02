namespace SupportFlow.Api.Settings;

public class GmailSettings
{
    public const string SectionName = "Gmail";

    public bool Enabled { get; set; }
    public string MailboxAddress { get; set; } = string.Empty;
    public string CredentialsPath { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = "SupportFlow";
    public string TokenStoragePath { get; set; } = "gmail-token";
    public string RedirectUri { get; set; } = string.Empty;
    public string MobileReturnUri { get; set; } = string.Empty;
}
