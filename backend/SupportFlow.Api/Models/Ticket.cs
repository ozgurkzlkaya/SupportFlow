using SupportFlow.Api.Enums;

namespace SupportFlow.Api.Models;

public class Ticket
{
    public int Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public int? ExternalRequesterId { get; set; }
    public int? AssignedUserId { get; set; }
    public int? CategoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TicketStatus Status { get; set; } = TicketStatus.New;
    public TicketPriority Priority { get; set; } = TicketPriority.Normal;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
    public DateTime? FirstResponseDueAt { get; set; }
    public DateTime? FirstRespondedAt { get; set; }
    public DateTime? ResolutionCycleStartedAt { get; set; }
    public DateTime? ResolutionDueAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public User? Customer { get; set; }
    public ExternalRequester? ExternalRequester { get; set; }
    public User? AssignedUser { get; set; }
    public TicketCategory? Category { get; set; }
    public ICollection<TicketMessage> Messages { get; set; } = new List<TicketMessage>();
    public ICollection<TicketTagAssignment> TagAssignments { get; set; } = new List<TicketTagAssignment>();
    public ICollection<TicketStatusHistory> StatusHistory { get; set; } = new List<TicketStatusHistory>();
    public ICollection<TicketActivity> Activities { get; set; } = new List<TicketActivity>();
    public ICollection<TicketEmailThread> EmailThreads { get; set; } =
        new List<TicketEmailThread>();
}
