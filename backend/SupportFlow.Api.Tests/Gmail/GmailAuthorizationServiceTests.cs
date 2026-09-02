using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Gmail.v1;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using SupportFlow.Api.Services.Gmail;
using SupportFlow.Api.Settings;

namespace SupportFlow.Api.Tests.Gmail;

public sealed class GmailAuthorizationServiceTests
{
    [Fact]
    public async Task GetStatusAsync_WithReadonlyScope_ReturnsCanReadTrueAndCanSendFalse()
    {
        await using var harness = await AuthorizationHarness.CreateAsync(
            [GmailService.Scope.GmailReadonly]);

        var status = await harness.Service.GetStatusAsync();

        Assert.True(status.Configured);
        Assert.True(status.Authorized);
        Assert.True(status.CanRead);
        Assert.False(status.CanSend);
        Assert.Equal("support11flow@gmail.com", status.Mailbox);
    }

    [Fact]
    public async Task GetStatusAsync_WithReadonlyAndSendScopes_ReturnsCanReadTrueAndCanSendTrue()
    {
        await using var harness = await AuthorizationHarness.CreateAsync(
            [GmailService.Scope.GmailReadonly, GmailService.Scope.GmailSend]);

        var status = await harness.Service.GetStatusAsync();

        Assert.True(status.Configured);
        Assert.True(status.Authorized);
        Assert.True(status.CanRead);
        Assert.True(status.CanSend);
    }

    [Fact]
    public async Task CompleteAuthorizationAsync_WithValidState_Succeeds()
    {
        await using var harness = await AuthorizationHarness.CreateAsync();
        var start = await harness.Service.CreateAuthorizationStartAsync();
        var state = ExtractState(start.AuthorizationUrl);

        var result = await harness.Service.CompleteAuthorizationAsync(
            "test-code",
            state,
            error: null);

        Assert.Equal("Gmail authorization completed successfully.", result.Message);
        Assert.Equal(GmailOAuthReturnTarget.Web, result.ReturnTarget);

        var status = await harness.Service.GetStatusAsync();
        Assert.True(status.Authorized);
        Assert.True(status.CanRead);
        Assert.True(status.CanSend);
    }

    [Fact]
    public async Task CompleteAuthorizationAsync_WithMobileStart_ReturnsMobileTarget()
    {
        await using var harness = await AuthorizationHarness.CreateAsync();
        var start = await harness.Service.CreateAuthorizationStartAsync(
            GmailOAuthReturnTarget.Mobile);
        var state = ExtractState(start.AuthorizationUrl);

        Assert.Equal("supportflowmobile://gmail-auth", start.CompletionReturnUri);

        var result = await harness.Service.CompleteAuthorizationAsync(
            "test-code",
            state,
            error: null);

        Assert.Equal("Gmail authorization completed successfully.", result.Message);
        Assert.Equal(GmailOAuthReturnTarget.Mobile, result.ReturnTarget);
    }

    [Fact]
    public async Task CompleteAuthorizationAsync_WhenMobileAuthorizationIsDeclined_ReturnsMobileTarget()
    {
        await using var harness = await AuthorizationHarness.CreateAsync();
        var start = await harness.Service.CreateAuthorizationStartAsync(
            GmailOAuthReturnTarget.Mobile);
        var state = ExtractState(start.AuthorizationUrl);

        var exception = await Assert.ThrowsAsync<GmailOAuthCompletionException>(() =>
            harness.Service.CompleteAuthorizationAsync(
                code: null,
                state: state,
                error: "access_denied"));

        Assert.Equal(GmailOAuthReturnTarget.Mobile, exception.ReturnTarget);
    }

    [Fact]
    public async Task CompleteAuthorizationAsync_WithExpiredState_Fails()
    {
        await using var harness = await AuthorizationHarness.CreateAsync();
        var start = await harness.Service.CreateAuthorizationStartAsync();
        var state = ExtractState(start.AuthorizationUrl);
        harness.TimeProvider.Advance(TimeSpan.FromMinutes(16));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Service.CompleteAuthorizationAsync(
                "test-code",
                state,
                error: null));

        Assert.Equal("The OAuth state is invalid or expired.", exception.Message);
    }

    [Fact]
    public async Task CompleteAuthorizationAsync_WithUnknownState_Fails()
    {
        await using var harness = await AuthorizationHarness.CreateAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Service.CompleteAuthorizationAsync(
                "test-code",
                "unknown-state",
                error: null));

        Assert.Equal("The OAuth state is invalid or expired.", exception.Message);
    }

    [Fact]
    public async Task CompleteAuthorizationAsync_StateCanOnlyBeConsumedOnce()
    {
        await using var harness = await AuthorizationHarness.CreateAsync();
        var start = await harness.Service.CreateAuthorizationStartAsync();
        var state = ExtractState(start.AuthorizationUrl);

        await harness.Service.CompleteAuthorizationAsync(
            "test-code",
            state,
            error: null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Service.CompleteAuthorizationAsync(
                "test-code",
                state,
                error: null));

        Assert.Equal("The OAuth state is invalid or expired.", exception.Message);
    }

    [Fact]
    public async Task CreateAuthorizationStartAsync_MultipleStatesRemainValidUntilConsumed()
    {
        await using var harness = await AuthorizationHarness.CreateAsync();
        var firstStart = await harness.Service.CreateAuthorizationStartAsync();
        var secondStart = await harness.Service.CreateAuthorizationStartAsync();
        var firstState = ExtractState(firstStart.AuthorizationUrl);
        var secondState = ExtractState(secondStart.AuthorizationUrl);

        var firstResult = await harness.Service.CompleteAuthorizationAsync(
            "first-code",
            firstState,
            error: null);
        var secondResult = await harness.Service.CompleteAuthorizationAsync(
            "second-code",
            secondState,
            error: null);

        Assert.Equal("Gmail authorization completed successfully.", firstResult.Message);
        Assert.Equal("Gmail authorization completed successfully.", secondResult.Message);
    }

    [Fact]
    public async Task CreateAuthorizationStartAsync_UsesContentRootForStateDirectory()
    {
        await using var harness = await AuthorizationHarness.CreateAsync();
        var start = await harness.Service.CreateAuthorizationStartAsync();
        var state = ExtractState(start.AuthorizationUrl);
        var expectedFilePath = GetStateFilePath(
            Path.Combine(harness.RootPath, "gmail-oauth-state"),
            state);

        Assert.True(File.Exists(expectedFilePath));
    }

    [Fact]
    public async Task StateStore_PersistsAcrossStoreInstances()
    {
        var rootPath = CreateTemporaryRootPath();

        try
        {
            var timeProvider = new ManualTimeProvider();
            var firstStore = new GmailOAuthStateStore(
                Path.Combine(rootPath, "gmail-oauth-state"),
                timeProvider);
            var secondStore = new GmailOAuthStateStore(
                Path.Combine(rootPath, "gmail-oauth-state"),
                timeProvider);

            await firstStore.StoreAsync(
                "opaque-state-_-.1234567890",
                TimeSpan.FromMinutes(15));

            var consumed = await secondStore.TryConsumeAsync("opaque-state-_-.1234567890");

            Assert.NotNull(consumed);
            Assert.Equal(GmailOAuthReturnTarget.Web, consumed.ReturnTarget);
        }
        finally
        {
            DeleteDirectoryIfExists(rootPath);
        }
    }

    [Fact]
    public async Task StateStore_UnknownStateFails()
    {
        var rootPath = CreateTemporaryRootPath();

        try
        {
            var store = new GmailOAuthStateStore(
                Path.Combine(rootPath, "gmail-oauth-state"),
                new ManualTimeProvider());

            var consumed = await store.TryConsumeAsync("unknown-state");

            Assert.Null(consumed);
        }
        finally
        {
            DeleteDirectoryIfExists(rootPath);
        }
    }

    [Fact]
    public async Task StateStore_ExpiredStateFails()
    {
        var rootPath = CreateTemporaryRootPath();

        try
        {
            var timeProvider = new ManualTimeProvider();
            var store = new GmailOAuthStateStore(
                Path.Combine(rootPath, "gmail-oauth-state"),
                timeProvider);
            await store.StoreAsync("expired-state", TimeSpan.FromMinutes(15));
            timeProvider.Advance(TimeSpan.FromMinutes(16));

            var consumed = await store.TryConsumeAsync("expired-state");

            Assert.Null(consumed);
        }
        finally
        {
            DeleteDirectoryIfExists(rootPath);
        }
    }

    [Fact]
    public async Task StateStore_IsConsumedOnlyOnce()
    {
        var rootPath = CreateTemporaryRootPath();

        try
        {
            var store = new GmailOAuthStateStore(
                Path.Combine(rootPath, "gmail-oauth-state"),
                new ManualTimeProvider());
            await store.StoreAsync("single-use-state", TimeSpan.FromMinutes(15));

            var firstConsume = await store.TryConsumeAsync("single-use-state");
            var secondConsume = await store.TryConsumeAsync("single-use-state");

            Assert.NotNull(firstConsume);
            Assert.Null(secondConsume);
        }
        finally
        {
            DeleteDirectoryIfExists(rootPath);
        }
    }

    [Fact]
    public async Task StateStore_MultipleStatesCoexist()
    {
        var rootPath = CreateTemporaryRootPath();

        try
        {
            var store = new GmailOAuthStateStore(
                Path.Combine(rootPath, "gmail-oauth-state"),
                new ManualTimeProvider());
            await store.StoreAsync("first-state", TimeSpan.FromMinutes(15));
            await store.StoreAsync("second-state", TimeSpan.FromMinutes(15));

            var firstConsume = await store.TryConsumeAsync("first-state");
            var secondConsume = await store.TryConsumeAsync("second-state");

            Assert.NotNull(firstConsume);
            Assert.NotNull(secondConsume);
        }
        finally
        {
            DeleteDirectoryIfExists(rootPath);
        }
    }

    [Fact]
    public async Task StateStore_TreatsStateAsOpaque()
    {
        var rootPath = CreateTemporaryRootPath();

        try
        {
            const string state = "opaque-state-Aa0-_.~";
            var store = new GmailOAuthStateStore(
                Path.Combine(rootPath, "gmail-oauth-state"),
                new ManualTimeProvider());
            await store.StoreAsync(state, TimeSpan.FromMinutes(15));

            var exactConsume = await store.TryConsumeAsync(state);
            await store.StoreAsync(state, TimeSpan.FromMinutes(15));
            var alteredConsume = await store.TryConsumeAsync(state.ToUpperInvariant());

            Assert.NotNull(exactConsume);
            Assert.Null(alteredConsume);
        }
        finally
        {
            DeleteDirectoryIfExists(rootPath);
        }
    }

    private sealed class AuthorizationHarness : IAsyncDisposable
    {
        private readonly string _rootPath;

        private AuthorizationHarness(
            string rootPath,
            ManualTimeProvider timeProvider,
            TestableGmailAuthorizationService service)
        {
            _rootPath = rootPath;
            TimeProvider = timeProvider;
            Service = service;
        }

        public ManualTimeProvider TimeProvider { get; }
        public TestableGmailAuthorizationService Service { get; }
        public string RootPath => _rootPath;

        public static async Task<AuthorizationHarness> CreateAsync(
            IReadOnlyCollection<string>? scopes = null)
        {
            var rootPath = CreateTemporaryRootPath();

            var credentialsPath = Path.Combine(rootPath, "gmail-oauth.json");
            await File.WriteAllTextAsync(
                credentialsPath,
                """
                {
                  "web": {
                    "client_id": "test-client-id",
                    "client_secret": "test-client-secret",
                    "auth_uri": "https://accounts.google.com/o/oauth2/auth",
                    "token_uri": "https://oauth2.googleapis.com/token"
                  }
                }
                """);

            var settings = Options.Create(new GmailSettings
            {
                Enabled = true,
                MailboxAddress = "support11flow@gmail.com",
                CredentialsPath = "gmail-oauth.json",
                ApplicationName = "SupportFlow",
                TokenStoragePath = "gmail-token",
                RedirectUri = "http://localhost:5135/api/integrations/gmail/oauth/callback",
                MobileReturnUri = "supportflowmobile://gmail-auth"
            });

            var dataProtectionProvider = DataProtectionProvider.Create(
                new DirectoryInfo(rootPath));
            var timeProvider = new ManualTimeProvider();
            var service = new TestableGmailAuthorizationService(
                settings,
                new TestWebHostEnvironment(rootPath),
                dataProtectionProvider,
                timeProvider);

            if (scopes is not null)
            {
                var tokenStore = new ProtectedFileDataStore(
                    Path.Combine(rootPath, "gmail-token"),
                    dataProtectionProvider.CreateProtector("SupportFlow.GmailOAuthTokens.v1"));
                await tokenStore.StoreAsync(
                    "support11flow@gmail.com",
                    new TokenResponse
                    {
                        AccessToken = "access-token",
                        RefreshToken = "refresh-token",
                        Scope = string.Join(' ', scopes)
                    });
            }

            return new AuthorizationHarness(rootPath, timeProvider, service);
        }

        public ValueTask DisposeAsync()
        {
            DeleteDirectoryIfExists(_rootPath);

            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestableGmailAuthorizationService : GmailAuthorizationService
    {
        public TestableGmailAuthorizationService(
            IOptions<GmailSettings> settings,
            IWebHostEnvironment environment,
            IDataProtectionProvider dataProtectionProvider,
            TimeProvider timeProvider)
            : base(settings, environment, dataProtectionProvider, timeProvider)
        {
        }

        protected override Task<TokenResponse> ExchangeCodeForTokenAsync(
            Google.Apis.Auth.OAuth2.Flows.GoogleAuthorizationCodeFlow flow,
            string code,
            CancellationToken cancellationToken) =>
            Task.FromResult(new TokenResponse
            {
                AccessToken = $"access-token-{code}",
                RefreshToken = "refresh-token",
                Scope = string.Join(
                    " ",
                    GmailService.Scope.GmailReadonly,
                    GmailService.Scope.GmailSend)
            });
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration)
        {
            _utcNow = _utcNow.Add(duration);
        }
    }

    private static string ExtractState(string authorizationUrl)
    {
        var uri = new Uri(authorizationUrl);
        var query = QueryHelpers.ParseQuery(uri.Query);
        return query["state"].ToString() is { Length: > 0 } state
            ? state
            : throw new InvalidOperationException("Missing state.");
    }

    private static string CreateTemporaryRootPath()
    {
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "SupportFlow.Api.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
        return rootPath;
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static string GetStateFilePath(string directoryPath, string state)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(state));
        var fileName = $"{Convert.ToHexString(hash)}.state";
        return Path.Combine(directoryPath, fileName);
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public TestWebHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            WebRootPath = contentRootPath;
            ApplicationName = "SupportFlow.Api.Tests";
            EnvironmentName = "Development";
            WebRootFileProvider = new NullFileProvider();
            ContentRootFileProvider = new NullFileProvider();
        }

        public string ApplicationName { get; set; }
        public IFileProvider WebRootFileProvider { get; set; }
        public string WebRootPath { get; set; }
        public string EnvironmentName { get; set; }
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
    }
}
