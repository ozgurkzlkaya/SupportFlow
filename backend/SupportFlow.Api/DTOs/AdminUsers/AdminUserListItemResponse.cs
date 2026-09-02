using SupportFlow.Api.Enums;

namespace SupportFlow.Api.DTOs.AdminUsers;

public sealed class AdminUserListItemResponse
{
    public int Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public UserRole Role { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public int ActiveTicketCount { get; init; }
}
