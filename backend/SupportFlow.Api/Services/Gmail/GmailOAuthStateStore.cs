using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SupportFlow.Api.Services.Gmail;

internal sealed class GmailOAuthStateStore
{
    private const string FileExtension = ".state";

    private readonly string _directoryPath;
    private readonly TimeProvider _timeProvider;

    public GmailOAuthStateStore(
        string directoryPath,
        TimeProvider timeProvider)
    {
        _directoryPath = directoryPath;
        _timeProvider = timeProvider;
    }

    public async Task StoreAsync(
        string state,
        TimeSpan lifetime,
        GmailOAuthReturnTarget returnTarget = GmailOAuthReturnTarget.Web,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime));
        }

        Directory.CreateDirectory(_directoryPath);

        var entry = new OAuthStateEntry(
            _timeProvider.GetUtcNow().Add(lifetime).ToUnixTimeMilliseconds(),
            returnTarget);
        var destinationPath = GetFilePath(state);
        var temporaryPath = $"{destinationPath}.{Guid.NewGuid():N}.tmp";

        try
        {
            await File.WriteAllTextAsync(
                temporaryPath,
                JsonSerializer.Serialize(entry),
                cancellationToken);
            File.Move(temporaryPath, destinationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public async Task<GmailOAuthState?> TryConsumeAsync(
        string state,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);

        var sourcePath = GetFilePath(state);
        if (!File.Exists(sourcePath))
        {
            return null;
        }

        var claimedPath = $"{sourcePath}.{Guid.NewGuid():N}.claimed";

        try
        {
            File.Move(sourcePath, claimedPath);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }

        try
        {
            var text = await File.ReadAllTextAsync(claimedPath, cancellationToken);
            if (!TryReadEntry(text, out var entry))
            {
                return null;
            }

            var now = _timeProvider.GetUtcNow();
            var expiresAt = DateTimeOffset.FromUnixTimeMilliseconds(entry.ExpiresAtUnixMilliseconds);
            return now <= expiresAt
                ? new GmailOAuthState(entry.ReturnTarget)
                : null;
        }
        finally
        {
            if (File.Exists(claimedPath))
            {
                File.Delete(claimedPath);
            }
        }
    }

    private string GetFilePath(string state)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(state));
        return Path.Combine(_directoryPath, $"{Convert.ToHexString(hash)}{FileExtension}");
    }

    private static bool TryReadEntry(string text, out OAuthStateEntry entry)
    {
        if (long.TryParse(text, out var legacyExpiresAtUnixMilliseconds))
        {
            entry = new OAuthStateEntry(
                legacyExpiresAtUnixMilliseconds,
                GmailOAuthReturnTarget.Web);
            return true;
        }

        try
        {
            var deserialized = JsonSerializer.Deserialize<OAuthStateEntry>(text);
            if (deserialized is not null
                && Enum.IsDefined(deserialized.ReturnTarget))
            {
                entry = deserialized;
                return true;
            }
        }
        catch (JsonException)
        {
            // Invalid state files are treated as invalid OAuth states.
        }

        entry = default!;
        return false;
    }

    private sealed record OAuthStateEntry(
        long ExpiresAtUnixMilliseconds,
        GmailOAuthReturnTarget ReturnTarget);
}

internal sealed record GmailOAuthState(GmailOAuthReturnTarget ReturnTarget);
