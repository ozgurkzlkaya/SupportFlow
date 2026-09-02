using Microsoft.Extensions.Options;
using SupportFlow.Api.Services.Gmail.Models;
using SupportFlow.Api.Settings;

namespace SupportFlow.Api.Services.Gmail;

// This intentionally remains in-memory. A restart uses the bounded catch-up query.
public sealed class GmailInboxDiscoveryState
{
    private readonly object _syncRoot = new();
    private DateTime? _lastSuccessfulBackgroundCycleStartedAtUtc;
    private readonly string _mailboxAddress;

    public GmailInboxDiscoveryState(IOptions<GmailSettings> settings)
    {
        _mailboxAddress = settings.Value.MailboxAddress;
    }

    public DateTime? GetLastSuccessfulBackgroundCycleStartedAtUtc()
    {
        lock (_syncRoot)
        {
            return _lastSuccessfulBackgroundCycleStartedAtUtc;
        }
    }

    public GmailInboxDiscoveryQuery GetBackgroundDiscoveryQuery() =>
        GmailInboxService.BuildBackgroundDiscoveryQuery(
            _mailboxAddress,
            GetLastSuccessfulBackgroundCycleStartedAtUtc());

    public void MarkBackgroundCycleSucceeded(DateTime cycleStartedAtUtc)
    {
        lock (_syncRoot)
        {
            _lastSuccessfulBackgroundCycleStartedAtUtc = EnsureUtc(cycleStartedAtUtc);
        }
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
