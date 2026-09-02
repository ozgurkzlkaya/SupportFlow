namespace SupportFlow.Api.DTOs.Taxonomy;

public sealed class CreateTicketCategoryRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
}
