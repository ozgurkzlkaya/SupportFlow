using SupportFlow.Api.Enums;

namespace SupportFlow.Api.DTOs.AdminUsers;

public sealed class AdminUserDetailResponse
{
    public int Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public UserRole Role { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public TechnicalStaffUserContextResponse? TechnicalStaffContext { get; init; }
    public CustomerUserContextResponse? CustomerContext { get; init; }
}

public sealed class TechnicalStaffUserContextResponse
{
    public int ActiveTicketCount { get; init; }
    public int CriticalTicketCount { get; init; }
    public int AwaitingReplyCount { get; init; }
    public int SlaBreachedCount { get; init; }
}

public sealed class CustomerUserContextResponse
{
    public int TotalTicketCount { get; init; }
    public int ActiveTicketCount { get; init; }
    public int ResolvedTicketCount { get; init; }
    public int ClosedTicketCount { get; init; }
}
