namespace SupportFlow.Api.Services.Gmail;

public interface IProcessedGmailMessageLookup
{
    Task<IReadOnlyDictionary<string, ProcessedGmailMessageMatch>> FindProcessedAsync(
        IReadOnlyCollection<string> gmailMessageIds,
        CancellationToken cancellationToken = default);
}

public sealed record ProcessedGmailMessageMatch(
    string GmailMessageId,
    int TicketId,
    string TicketNumber);
