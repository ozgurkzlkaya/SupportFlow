using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Google;

namespace SupportFlow.Api.Services.Gmail;

public interface IGmailApiRateLimitState
{
    bool TryGetRateLimitedUntil(out DateTime rateLimitedUntil);

    GmailApiRateLimitSnapshot GetSnapshot();

    GmailApiRateLimitSnapshot RecordRateLimit(DateTime? providerRetryAtUtc);

    void MarkSuccessfulCall();
}

public sealed record GmailApiRateLimitSnapshot(
    bool IsRateLimited,
    DateTime? RateLimitedUntil,
    int ConsecutiveFailures);

public sealed class GmailApiRateLimitState(TimeProvider timeProvider) : IGmailApiRateLimitState
{
    private static readonly TimeSpan[] FallbackBackoff =
    [
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(30)
    ];

    private readonly object _syncRoot = new();
    private DateTime? _rateLimitedUntil;
    private int _consecutiveFailures;

    public bool TryGetRateLimitedUntil(out DateTime rateLimitedUntil)
    {
        lock (_syncRoot)
        {
            if (_rateLimitedUntil is { } until && until > GetUtcNow())
            {
                rateLimitedUntil = until;
                return true;
            }

            _rateLimitedUntil = null;
            rateLimitedUntil = default;
            return false;
        }
    }

    public GmailApiRateLimitSnapshot GetSnapshot()
    {
        _ = TryGetRateLimitedUntil(out _);

        lock (_syncRoot)
        {
            return new GmailApiRateLimitSnapshot(
                _rateLimitedUntil is not null,
                _rateLimitedUntil,
                _consecutiveFailures);
        }
    }

    public GmailApiRateLimitSnapshot RecordRateLimit(DateTime? providerRetryAtUtc)
    {
        lock (_syncRoot)
        {
            _consecutiveFailures++;
            var now = GetUtcNow();
            var fallbackIndex = Math.Min(_consecutiveFailures - 1, FallbackBackoff.Length - 1);
            var fallbackUntil = now.Add(FallbackBackoff[fallbackIndex]);
            var providerUntil = providerRetryAtUtc?.ToUniversalTime();

            _rateLimitedUntil = providerUntil is { } retryAt && retryAt > now
                ? retryAt
                : fallbackUntil;

            return new GmailApiRateLimitSnapshot(
                true,
                _rateLimitedUntil,
                _consecutiveFailures);
        }
    }

    public void MarkSuccessfulCall()
    {
        lock (_syncRoot)
        {
            _rateLimitedUntil = null;
            _consecutiveFailures = 0;
        }
    }

    private DateTime GetUtcNow() => timeProvider.GetUtcNow().UtcDateTime;
}

public sealed class GmailRateLimitedException(DateTime retryAtUtc)
    : InvalidOperationException("Gmail API geçici olarak hız sınırında.")
{
    public DateTime RetryAtUtc { get; } = retryAtUtc;
}

internal static partial class GmailApiRateLimitDetector
{
    public static bool TryHandle(
        GoogleApiException exception,
        IGmailApiRateLimitState rateLimitState,
        out GmailRateLimitedException rateLimitedException)
    {
        if (exception.HttpStatusCode != HttpStatusCode.TooManyRequests)
        {
            rateLimitedException = null!;
            return false;
        }

        var snapshot = rateLimitState.RecordRateLimit(ParseProviderRetryAt(exception.Message));
        rateLimitedException = new GmailRateLimitedException(snapshot.RateLimitedUntil!.Value);
        return true;
    }

    internal static DateTime? ParseProviderRetryAt(string? value)
    {
        var match = RetryAfterRegex().Match(value ?? string.Empty);
        if (!match.Success
            || !DateTimeOffset.TryParse(
                match.Groups["timestamp"].Value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var retryAt))
        {
            return null;
        }

        return retryAt.UtcDateTime;
    }

    [GeneratedRegex(
        @"Retry after\s+(?<timestamp>\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z)",
        RegexOptions.IgnoreCase)]
    private static partial Regex RetryAfterRegex();
}
