using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Google.Apis.Util.Store;
using Microsoft.AspNetCore.DataProtection;

namespace SupportFlow.Api.Services.Gmail;

internal sealed class ProtectedFileDataStore : IDataStore
{
    private const string FileExtension = ".protected";

    private readonly string _directoryPath;
    private readonly IDataProtector _protector;

    public ProtectedFileDataStore(
        string directoryPath,
        IDataProtector protector)
    {
        _directoryPath = directoryPath;
        _protector = protector;
    }

    public async Task StoreAsync<T>(string key, T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        Directory.CreateDirectory(_directoryPath);

        var protectedJson = _protector.Protect(JsonSerializer.Serialize(value));
        var destinationPath = GetFilePath<T>(key);
        var temporaryPath = $"{destinationPath}.{Guid.NewGuid():N}.tmp";

        try
        {
            await File.WriteAllTextAsync(temporaryPath, protectedJson);
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

    public async Task<T?> GetAsync<T>(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var path = GetFilePath<T>(key);
        if (!File.Exists(path))
        {
            return default;
        }

        var protectedJson = await File.ReadAllTextAsync(path);
        var json = _protector.Unprotect(protectedJson);
        return JsonSerializer.Deserialize<T>(json);
    }

    public Task DeleteAsync<T>(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var path = GetFilePath<T>(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        if (!Directory.Exists(_directoryPath))
        {
            return Task.CompletedTask;
        }

        foreach (var path in Directory.EnumerateFiles(
                     _directoryPath,
                     $"*{FileExtension}",
                     SearchOption.TopDirectoryOnly))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string GetFilePath<T>(string key)
    {
        var storageKey = $"{typeof(T).FullName}\n{key}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(storageKey));
        return Path.Combine(
            _directoryPath,
            $"{Convert.ToHexString(hash)}{FileExtension}");
    }
}
