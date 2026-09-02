using SupportFlow.Api.Services.Gmail.Models;

namespace SupportFlow.Api.Services.Gmail;

public sealed class GmailDiagnosticsCache(TimeProvider timeProvider)
{
    internal static readonly TimeSpan TimeToLive = TimeSpan.FromSeconds(90);

    private readonly object _syncRoot = new();
    private IReadOnlyList<GmailMessageDiagnostic>? _messages;
    private DateTime? _cachedAt;

    public bool TryGet(
        out IReadOnlyList<GmailMessageDiagnostic>? messages,
        out DateTime? cachedAt)
    {
        lock (_syncRoot)
        {
            if (_messages is not null
                && _cachedAt is { } createdAt
                && createdAt.Add(TimeToLive) > GetUtcNow())
            {
                messages = _messages;
                cachedAt = createdAt;
                return true;
            }

            _messages = null;
            _cachedAt = null;
            messages = null;
            cachedAt = null;
            return false;
        }
    }

    public void Set(IReadOnlyList<GmailMessageDiagnostic> messages)
    {
        lock (_syncRoot)
        {
            _messages = messages;
            _cachedAt = GetUtcNow();
        }
    }

    private DateTime GetUtcNow() => timeProvider.GetUtcNow().UtcDateTime;
}
