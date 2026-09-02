using SupportFlow.Api.Models;

namespace SupportFlow.Api.Services.Email;

public sealed record TicketEmailRecipient(string Email, string DisplayName)
{
    public static TicketEmailRecipient From(User user) =>
        new(user.Email, string.Join(' ', new[] { user.FirstName, user.LastName }.Where(value => !string.IsNullOrWhiteSpace(value))));

    public static TicketEmailRecipient From(ExternalRequester requester) =>
        new(requester.Email, requester.DisplayName ?? requester.Email);
}
