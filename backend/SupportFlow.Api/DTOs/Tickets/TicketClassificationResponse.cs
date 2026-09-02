namespace SupportFlow.Api.DTOs.Tickets;

public sealed class TicketClassificationResponse
{
    public TicketClassificationItemResponse? Category { get; init; }
    public IReadOnlyList<TicketClassificationItemResponse> Tags { get; init; } = [];
}
