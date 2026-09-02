using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SupportFlow.Api.Data;
using SupportFlow.Api.Models;
using SupportFlow.Api.Services.Gmail.Models;

namespace SupportFlow.Api.Services.Gmail;

public sealed partial class InboundEmailCorrelationService : IInboundEmailCorrelationService
{
    private readonly AppDbContext _dbContext;

    public InboundEmailCorrelationService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<InboundEmailCorrelationResult> ResolveAsync(
        InboundEmailMessage message,
        CancellationToken cancellationToken = default)
    {
        var exactThread = await _dbContext.TicketEmailThreads
            .Include(thread => thread.Ticket)
            .SingleOrDefaultAsync(
                thread => thread.GmailThreadId == message.GmailThreadId,
                cancellationToken);

        if (exactThread is not null)
        {
            return InboundEmailCorrelationResult.Matched(
                exactThread.Ticket,
                InboundEmailCorrelationStrategy.GmailThreadId,
                exactThread);
        }

        if (!string.IsNullOrWhiteSpace(message.InReplyTo))
        {
            var ticket = await FindUnambiguousTicketByInternetMessageIdAsync(
                message.InReplyTo,
                cancellationToken);
            if (ticket is not null)
            {
                return InboundEmailCorrelationResult.Matched(
                    ticket,
                    InboundEmailCorrelationStrategy.InReplyTo);
            }
        }

        var referenceIds = message.References
            .Reverse()
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (referenceIds.Length > 0)
        {
            var matches = await _dbContext.ProcessedEmailMessages
                .Where(processed => processed.InternetMessageId != null
                    && referenceIds.Contains(processed.InternetMessageId))
                .Select(processed => new
                {
                    processed.InternetMessageId,
                    processed.TicketEmailThread.Ticket
                })
                .ToListAsync(cancellationToken);

            foreach (var referenceId in referenceIds)
            {
                var tickets = matches
                    .Where(match => match.InternetMessageId == referenceId)
                    .Select(match => match.Ticket)
                    .DistinctBy(ticket => ticket.Id)
                    .Take(2)
                    .ToArray();

                if (tickets.Length == 1)
                {
                    return InboundEmailCorrelationResult.Matched(
                        tickets[0],
                        InboundEmailCorrelationStrategy.References);
                }
            }
        }

        var ticketNumber = ExtractTicketNumber(message.Subject);
        if (ticketNumber is not null)
        {
            var ticket = await _dbContext.Tickets
                .SingleOrDefaultAsync(
                    candidate => candidate.TicketNumber == ticketNumber,
                    cancellationToken);
            if (ticket is not null)
            {
                return InboundEmailCorrelationResult.Matched(
                    ticket,
                    InboundEmailCorrelationStrategy.TicketNumber);
            }
        }

        return InboundEmailCorrelationResult.NoMatch();
    }

    internal static string? ExtractTicketNumber(string? subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        var match = TicketNumberRegex().Match(subject);
        return match.Success ? match.Value.ToUpperInvariant() : null;
    }

    private async Task<Ticket?> FindUnambiguousTicketByInternetMessageIdAsync(
        string internetMessageId,
        CancellationToken cancellationToken)
    {
        var tickets = await _dbContext.ProcessedEmailMessages
            .Where(processed => processed.InternetMessageId == internetMessageId)
            .Select(processed => processed.TicketEmailThread.Ticket)
            .Distinct()
            .Take(2)
            .ToListAsync(cancellationToken);

        return tickets.Count == 1 ? tickets[0] : null;
    }

    [GeneratedRegex(
        @"(?<![A-Z0-9])SUP-\d{4}-\d{6}(?![A-Z0-9])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TicketNumberRegex();
}
