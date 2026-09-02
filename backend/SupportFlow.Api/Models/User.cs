using SupportFlow.Api.Enums;

namespace SupportFlow.Api.Models;

public class User
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Company? Company { get; set; }
    public ICollection<Ticket> CreatedTickets { get; set; } = new List<Ticket>();
    public ICollection<Ticket> AssignedTickets { get; set; } = new List<Ticket>();
    public ICollection<TicketMessage> Messages { get; set; } = new List<TicketMessage>();
    public ICollection<ConversationPin> ConversationPins { get; set; } = new List<ConversationPin>();
    public ICollection<ConversationReadState> ConversationReadStates { get; set; } = new List<ConversationReadState>();
    public ICollection<UserAdminAudit> PerformedUserAdminAudits { get; set; } = new List<UserAdminAudit>();
    public ICollection<UserAdminAudit> TargetedUserAdminAudits { get; set; } = new List<UserAdminAudit>();
}
