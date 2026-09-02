using SupportFlow.Api.Enums;

namespace SupportFlow.Api.DTOs.AdminUsers;

public sealed class AdminUserAuditResponse
{
    public int Id { get; init; }
    public int ActorUserId { get; init; }
    public string ActorDisplayName { get; init; } = string.Empty;
    public int TargetUserId { get; init; }
    public string TargetDisplayName { get; init; } = string.Empty;
    public string TargetEmail { get; init; } = string.Empty;
    public UserRole TargetRole { get; init; }
    public UserAdminAuditAction Action { get; init; }
    public bool OldIsActive { get; init; }
    public bool NewIsActive { get; init; }
    public DateTime OccurredAt { get; init; }
}
