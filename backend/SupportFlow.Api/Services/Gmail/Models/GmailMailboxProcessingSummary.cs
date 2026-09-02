namespace SupportFlow.Api.Services.Gmail.Models;

public sealed record GmailMailboxProcessingSummary(
    int Processed,
    int NewTickets,
    int MessagesAdded,
    int AlreadyProcessed,
    int UnknownCustomers,
    int SenderMismatches,
    int SelfMessagesIgnored,
    int Failed,
    IReadOnlyList<GmailMailboxProcessingItem>? Results = null,
    int NewExternalTickets = 0,
    int ExternalRequestersCreated = 0,
    int ExternalRequestersMatched = 0,
    int ListedCandidates = 0,
    int AlreadyProcessedSkipped = 0,
    int UnseenFetched = 0,
    int AttachmentsFetched = 0,
    int SystemMessagesSkipped = 0);
