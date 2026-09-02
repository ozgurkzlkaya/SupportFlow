using SupportFlow.Api.Enums;

namespace SupportFlow.Api.DTOs.Users;

public class UserProfileResponse
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public int? CompanyId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
