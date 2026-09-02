using Google.Apis.Auth.OAuth2;
using SupportFlow.Api.DTOs.Gmail;

namespace SupportFlow.Api.Services.Gmail;

public interface IGmailAuthorizationService
{
    Task<GmailOAuthStartResponse> CreateAuthorizationStartAsync(
        GmailOAuthReturnTarget returnTarget = GmailOAuthReturnTarget.Web,
        CancellationToken cancellationToken = default);

    Task<GmailAuthorizationCompletionResult> CompleteAuthorizationAsync(
        string? code,
        string? state,
        string? error,
        CancellationToken cancellationToken = default);

    Task<GmailOAuthStatusResponse> GetStatusAsync(
        CancellationToken cancellationToken = default);

    Task<UserCredential> GetAuthorizedCredentialAsync(
        CancellationToken cancellationToken = default);

    string GetMobileReturnUri();
}
