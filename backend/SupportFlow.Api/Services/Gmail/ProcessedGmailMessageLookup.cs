using Microsoft.EntityFrameworkCore;
using SupportFlow.Api.Data;

namespace SupportFlow.Api.Services.Gmail;

public sealed class ProcessedGmailMessageLookup(AppDbContext dbContext) : IProcessedGmailMessageLookup
{
    public async Task<IReadOnlyDictionary<string, ProcessedGmailMessageMatch>> FindProcessedAsync(
        IReadOnlyCollection<string> gmailMessageIds,
        CancellationToken cancellationToken = default)
    {
        var ids = gmailMessageIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<string, ProcessedGmailMessageMatch>(StringComparer.Ordinal);
        }

        var matches = await dbContext.ProcessedEmailMessages
            .AsNoTracking()
            .Where(message => ids.Contains(message.GmailMessageId))
            .Select(message => new ProcessedGmailMessageMatch(
                message.GmailMessageId,
                message.TicketEmailThread.TicketId,
                message.TicketEmailThread.Ticket.TicketNumber))
            .ToListAsync(cancellationToken);

        return matches.ToDictionary(match => match.GmailMessageId, StringComparer.Ordinal);
    }
}
