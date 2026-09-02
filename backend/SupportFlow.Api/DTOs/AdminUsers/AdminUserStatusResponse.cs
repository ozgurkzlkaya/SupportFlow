namespace SupportFlow.Api.DTOs.AdminUsers;

public sealed class AdminUserStatusResponse
{
    public int Id { get; init; }
    public bool IsActive { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
