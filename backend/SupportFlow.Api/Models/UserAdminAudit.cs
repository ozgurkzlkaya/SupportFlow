using SupportFlow.Api.Enums;

namespace SupportFlow.Api.Models;

public class UserAdminAudit
{
    public int Id { get; set; }
    public int ActorUserId { get; set; }
    public string ActorDisplayName { get; set; } = string.Empty;
    public int TargetUserId { get; set; }
    public string TargetDisplayName { get; set; } = string.Empty;
    public string TargetEmail { get; set; } = string.Empty;
    public UserRole TargetRole { get; set; }
    public UserAdminAuditAction Action { get; set; }
    public bool OldIsActive { get; set; }
    public bool NewIsActive { get; set; }
    public DateTime OccurredAt { get; set; }

    public User? ActorUser { get; set; }
    public User? TargetUser { get; set; }
}
