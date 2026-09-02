namespace SupportFlow.Api.DTOs.Gmail;

public sealed class GmailBackgroundSyncStatusResponse
{
    public bool Enabled { get; init; }
    public int IntervalSeconds { get; init; }
    public bool IsRunning { get; init; }
    public DateTime? LastStartedAt { get; init; }
    public DateTime? LastCompletedAt { get; init; }
    public DateTime? LastSuccessfulAt { get; init; }
    public string? LastError { get; init; }
    public GmailBackgroundSyncSummaryResponse? LastSummary { get; init; }
    public bool IsRateLimited { get; init; }
    public DateTime? RateLimitedUntil { get; init; }
    public int ConsecutiveFailures { get; init; }
}

public sealed record GmailBackgroundSyncSummaryResponse(
    int Examined,
    int NewTickets,
    int NewExternalTickets,
    int MessagesAdded,
    int AlreadyProcessed,
    int Failed);
