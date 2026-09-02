using SupportFlow.Api.Models;
using SupportFlow.Api.Services.Email;

namespace SupportFlow.Api.Services.Gmail;

public interface ITicketAcknowledgmentService
{
    Task TrySendNewTicketAcknowledgmentAsync(
        Ticket ticket,
        TicketEmailRecipient recipient,
        CancellationToken cancellationToken = default);
}
