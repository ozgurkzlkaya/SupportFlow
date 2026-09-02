namespace SupportFlow.Api.Models;

public sealed class TicketTag
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<TicketTagAssignment> Assignments { get; set; } = new List<TicketTagAssignment>();
}
