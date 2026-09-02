namespace SupportFlow.Api.DTOs.Gmail;

public sealed record GmailRateLimitResponse(
    string Status,
    string Message,
    DateTime RetryAt);
