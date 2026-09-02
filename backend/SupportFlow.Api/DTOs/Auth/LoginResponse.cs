namespace SupportFlow.Api.DTOs.Auth;

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public AuthUserResponse User { get; set; } = new();
}
