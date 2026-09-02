namespace SupportFlow.Api.DTOs.Tickets;

public sealed class UpdateTicketClassificationRequest
{
    public int? CategoryId { get; init; }
    public IReadOnlyList<int>? TagIds { get; init; }
}
