namespace SupportFlow.Api.Services.Gmail.Models;

public sealed record GmailMailboxProcessingItem(
    InboundEmailProcessingStatus Status,
    int? TicketId,
    string? TicketNumber,
    string? Reason,
    int? ExternalRequesterId = null,
    bool? ExternalRequesterWasCreated = null);
