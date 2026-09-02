namespace SupportFlow.Api.DTOs.Tickets;

public sealed class TicketClassificationItemResponse
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}
