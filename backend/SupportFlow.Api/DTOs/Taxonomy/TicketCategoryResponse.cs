namespace SupportFlow.Api.DTOs.Taxonomy;

public sealed class TicketCategoryResponse
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public int UsageCount { get; init; }
}
