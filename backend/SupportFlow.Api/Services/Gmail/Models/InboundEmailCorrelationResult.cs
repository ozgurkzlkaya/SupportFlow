using SupportFlow.Api.Models;

namespace SupportFlow.Api.Services.Gmail.Models;

public enum InboundEmailCorrelationStrategy
{
    None,
    GmailThreadId,
    InReplyTo,
    References,
    TicketNumber
}

public sealed record InboundEmailCorrelationResult(
    Ticket? Ticket,
    TicketEmailThread? EmailThread,
    InboundEmailCorrelationStrategy Strategy)
{
    public static InboundEmailCorrelationResult NoMatch() =>
        new(null, null, InboundEmailCorrelationStrategy.None);

    public static InboundEmailCorrelationResult Matched(
        Ticket ticket,
        InboundEmailCorrelationStrategy strategy,
        TicketEmailThread? emailThread = null) =>
        new(ticket, emailThread, strategy);
}
