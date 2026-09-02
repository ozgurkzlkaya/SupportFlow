using System.Security.Cryptography;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Gmail.v1;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.WebUtilities;
using SupportFlow.Api.DTOs.Gmail;
using SupportFlow.Api.Settings;

namespace SupportFlow.Api.Services.Gmail;

public class GmailAuthorizationService : IGmailAuthorizationService
{
    private const string OAuthStateDirectoryName = "gmail-oauth-state";
    private static readonly TimeSpan OAuthStateLifetime = TimeSpan.FromMinutes(15);
    private static readonly string[] GmailScopes =
    [
        GmailService.Scope.GmailReadonly,
        GmailService.Scope.GmailSend
    ];
    private static readonly string[] ReadCapableScopes =
    [
        GmailService.Scope.GmailReadonly,
        GmailService.Scope.GmailModify,
        GmailService.Scope.MailGoogleCom
    ];
    private static readonly string[] SendCapableScopes =
    [
        GmailService.Scope.GmailSend,
        GmailService.Scope.GmailCompose,
        GmailService.Scope.GmailModify,
        GmailService.Scope.MailGoogleCom
    ];

    private readonly GmailSettings _settings;
    private readonly IWebHostEnvironment _environment;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly GmailOAuthStateStore _stateStore;

    public GmailAuthorizationService(
        IOptions<GmailSettings> settings,
        IWebHostEnvironment environment,
        IDataProtectionProvider dataProtectionProvider,
        TimeProvider timeProvider)
    {
        _settings = settings.Value;
        _environment = environment;
        _dataProtectionProvider = dataProtectionProvider;
        _stateStore = new GmailOAuthStateStore(
            ResolvePath(OAuthStateDirectoryName),
            timeProvider);
    }

    public Task<GmailOAuthStatusResponse> GetStatusAsync(
        CancellationToken cancellationToken = default) =>
        GetStatusInternalAsync(cancellationToken);

    public Task<UserCredential> GetAuthorizedCredentialAsync(
        CancellationToken cancellationToken = default) =>
        GetAuthorizedCredentialInternalAsync(cancellationToken);

    public string GetMobileReturnUri()
    {
        EnsureMobileReturnConfiguration();
        return _settings.MobileReturnUri;
    }

    public async Task<GmailOAuthStartResponse> CreateAuthorizationStartAsync(
        GmailOAuthReturnTarget returnTarget = GmailOAuthReturnTarget.Web,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureBaseConfiguration();

        if (GetClientType() != GoogleOAuthClientType.Web)
        {
            throw new GmailIntegrationUnavailableException(
                "The configured Gmail OAuth client must be a Google Cloud Web application client.");
        }

        var completionReturnUri = returnTarget == GmailOAuthReturnTarget.Mobile
            ? GetMobileReturnUri()
            : null;

        var clientSecrets = LoadClientSecrets();
        var state = GenerateState();
        await _stateStore.StoreAsync(
            state,
            OAuthStateLifetime,
            returnTarget,
            cancellationToken);

        var request = new GoogleAuthorizationCodeRequestUrl(new Uri(GoogleAuthConsts.AuthorizationUrl))
        {
            ClientId = clientSecrets.ClientId,
            RedirectUri = _settings.RedirectUri,
            Scope = string.Join(' ', GmailScopes),
            State = state
        };
        request.AccessType = "offline";
        request.LoginHint = _settings.MailboxAddress;
        request.Prompt = "consent";

        return new GmailOAuthStartResponse
        {
            AuthorizationUrl = request.Build().AbsoluteUri,
            CompletionReturnUri = completionReturnUri
        };
    }

    public async Task<GmailAuthorizationCompletionResult> CompleteAuthorizationAsync(
        string? code,
        string? state,
        string? error,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            throw new InvalidOperationException("The OAuth state is missing.");
        }

        var oauthState = await _stateStore.TryConsumeAsync(state, cancellationToken);
        if (oauthState is null)
        {
            throw new InvalidOperationException("The OAuth state is invalid or expired.");
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            throw new GmailOAuthCompletionException(
                "Google authorization was declined or could not be completed.",
                oauthState.ReturnTarget);
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new GmailOAuthCompletionException(
                "The authorization code is missing.",
                oauthState.ReturnTarget);
        }

        try
        {
            var flow = CreateAuthorizationCodeFlow(requireWebClient: true);
            var token = await ExchangeCodeForTokenAsync(flow, code, cancellationToken);

            await flow.DataStore.StoreAsync(
                _settings.MailboxAddress,
                token);
        }
        catch (GmailIntegrationUnavailableException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new GmailOAuthCompletionException(
                "Google authorization could not be completed.",
                oauthState.ReturnTarget);
        }

        return new GmailAuthorizationCompletionResult(
            "Gmail authorization completed successfully.",
            oauthState.ReturnTarget);
    }

    private async Task<GmailOAuthStatusResponse> GetStatusInternalAsync(
        CancellationToken cancellationToken)
    {
        if (!IsBaseConfigurationValid())
        {
            return CreateStatusResponse(
                configured: false,
                authorized: false,
                canRead: false,
                canSend: false);
        }

        if (GetClientType() != GoogleOAuthClientType.Web)
        {
            return CreateStatusResponse(
                configured: false,
                authorized: false,
                canRead: false,
                canSend: false);
        }

        var flow = CreateAuthorizationCodeFlow(requireWebClient: true);
        var token = await flow.LoadTokenAsync(
            _settings.MailboxAddress,
            cancellationToken);

        var authorized = HasUsableAuthorization(token);
        var grantedScopes = ParseGrantedScopes(token);
        var canRead = authorized
            && (grantedScopes.Count == 0 || HasAnyScope(grantedScopes, ReadCapableScopes));
        var canSend = authorized && HasAnyScope(grantedScopes, SendCapableScopes);

        return CreateStatusResponse(
            configured: true,
            authorized: authorized,
            canRead: canRead,
            canSend: canSend);
    }

    private async Task<UserCredential> GetAuthorizedCredentialInternalAsync(
        CancellationToken cancellationToken)
    {
        var flow = CreateAuthorizationCodeFlow(requireWebClient: true);
        var token = await flow.LoadTokenAsync(
            _settings.MailboxAddress,
            cancellationToken);

        if (!HasUsableAuthorization(token))
        {
            throw new GmailAuthorizationRequiredException(
                "Gmail authorization is required.");
        }

        return new UserCredential(flow, _settings.MailboxAddress, token);
    }

    private GoogleAuthorizationCodeFlow CreateAuthorizationCodeFlow(bool requireWebClient)
    {
        EnsureBaseConfiguration();

        var clientType = GetClientType();
        if (requireWebClient && clientType != GoogleOAuthClientType.Web)
        {
            throw new GmailIntegrationUnavailableException(
                "The configured Gmail OAuth client must be a Google Cloud Web application client.");
        }

        return new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = LoadClientSecrets(),
            Scopes = GmailScopes,
            DataStore = new ProtectedFileDataStore(
                ResolvePath(_settings.TokenStoragePath),
                _dataProtectionProvider.CreateProtector(
                    "SupportFlow.GmailOAuthTokens.v1"))
        });
    }

    private ClientSecrets LoadClientSecrets()
    {
        var credentialsPath = ResolvePath(_settings.CredentialsPath);
        using var stream = File.OpenRead(credentialsPath);
        return GoogleClientSecrets.FromStream(stream).Secrets;
    }

    private bool IsBaseConfigurationValid()
    {
        if (!_settings.Enabled)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(_settings.MailboxAddress)
            || string.IsNullOrWhiteSpace(_settings.CredentialsPath)
            || string.IsNullOrWhiteSpace(_settings.ApplicationName)
            || string.IsNullOrWhiteSpace(_settings.TokenStoragePath)
            || !HasValidRedirectUri())
        {
            return false;
        }

        return File.Exists(ResolvePath(_settings.CredentialsPath));
    }

    private void EnsureBaseConfiguration()
    {
        if (!_settings.Enabled)
        {
            throw new GmailIntegrationUnavailableException(
                "Gmail integration is disabled.");
        }

        if (string.IsNullOrWhiteSpace(_settings.MailboxAddress)
            || string.IsNullOrWhiteSpace(_settings.CredentialsPath)
            || string.IsNullOrWhiteSpace(_settings.ApplicationName)
            || string.IsNullOrWhiteSpace(_settings.TokenStoragePath)
            || !HasValidRedirectUri())
        {
            throw new GmailIntegrationUnavailableException(
                "Gmail integration configuration is incomplete.");
        }

        var credentialsPath = ResolvePath(_settings.CredentialsPath);
        if (!File.Exists(credentialsPath))
        {
            throw new GmailIntegrationUnavailableException(
                "The configured Gmail OAuth credentials file was not found.");
        }
    }

    private GoogleOAuthClientType GetClientType()
    {
        var credentialsPath = ResolvePath(_settings.CredentialsPath);
        using var stream = File.OpenRead(credentialsPath);
        using var document = JsonDocument.Parse(stream);

        if (document.RootElement.TryGetProperty("web", out _))
        {
            return GoogleOAuthClientType.Web;
        }

        if (document.RootElement.TryGetProperty("installed", out _))
        {
            return GoogleOAuthClientType.Installed;
        }

        return GoogleOAuthClientType.Unknown;
    }

    private GmailOAuthStatusResponse CreateStatusResponse(
        bool configured,
        bool authorized,
        bool canRead,
        bool canSend) =>
        new()
        {
            Configured = configured,
            Authorized = authorized,
            CanRead = canRead,
            CanSend = canSend,
            Mailbox = _settings.MailboxAddress
        };

    private static HashSet<string> ParseGrantedScopes(TokenResponse? token)
    {
        if (string.IsNullOrWhiteSpace(token?.Scope))
        {
            return [];
        }

        return token.Scope
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool HasAnyScope(
        HashSet<string> grantedScopes,
        IEnumerable<string> expectedScopes) =>
        expectedScopes.Any(grantedScopes.Contains);

    private bool HasValidRedirectUri() =>
        Uri.TryCreate(_settings.RedirectUri, UriKind.Absolute, out var redirectUri)
        && (redirectUri.Scheme == Uri.UriSchemeHttps
            || (redirectUri.Scheme == Uri.UriSchemeHttp && redirectUri.IsLoopback));

    private void EnsureMobileReturnConfiguration()
    {
        if (!Uri.TryCreate(_settings.MobileReturnUri, UriKind.Absolute, out var returnUri)
            || string.Equals(returnUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(returnUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new GmailIntegrationUnavailableException(
                "The mobile Gmail OAuth return URI is not configured.");
        }
    }

    private static bool HasUsableAuthorization(TokenResponse? token) =>
        token is not null
        && (!string.IsNullOrWhiteSpace(token.RefreshToken)
            || (!string.IsNullOrWhiteSpace(token.AccessToken)
                && !token.IsStale));

    private static string GenerateState() =>
        WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private string ResolvePath(string path) =>
        Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(path, _environment.ContentRootPath);

    protected virtual Task<TokenResponse> ExchangeCodeForTokenAsync(
        GoogleAuthorizationCodeFlow flow,
        string code,
        CancellationToken cancellationToken) =>
        flow.ExchangeCodeForTokenAsync(
            _settings.MailboxAddress,
            code,
            _settings.RedirectUri,
            cancellationToken);

    private enum GoogleOAuthClientType
    {
        Unknown,
        Installed,
        Web
    }
}
