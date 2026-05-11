#if !__ANDROID__ && !__WASM__
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HarvestmoonGCS.Core.Services.AI;

namespace HarvestmoonGCS.Services.Security;

/// <summary>
/// Secure API key store for desktop platforms.
/// Uses AES-GCM with PBKDF2-derived key from machine/user fingerprint.
/// </summary>
public sealed class DesktopApiKeyStore : IApiKeyStore
{
    private const string OpenRouterApiKeyName = "openrouter_api_key";
    private const string GeminiApiKeyName = "gemini_api_key";
    private const string OpenAIApiKeyName = "openai_api_key";
    private const string GrokApiKeyName = "grok_api_key";
    private const string AesPrefix = "aesgcm:";

    private readonly string _storageFilePath;
    private readonly string _saltFilePath;
    private readonly object _lock = new();

    public DesktopApiKeyStore()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HarvestmoonGCS");
        Directory.CreateDirectory(appData);

        _storageFilePath = Path.Combine(appData, "secure_keys.json");
        _saltFilePath = Path.Combine(appData, "secure_keys.salt");
    }

    public string? GetApiKey(string provider)
    {
        var keyName = ResolveProviderKeyName(provider);
        if (string.IsNullOrWhiteSpace(keyName))
        {
            return null;
        }

        lock (_lock)
        {
            var entries = LoadEntries();
            if (!entries.TryGetValue(keyName, out var payload) || string.IsNullOrWhiteSpace(payload))
            {
                return null;
            }

            try
            {
                return Decrypt(payload);
            }
            catch
            {
                return null;
            }
        }
    }

    public bool SaveApiKey(string provider, string apiKey)
    {
        var keyName = ResolveProviderKeyName(provider);
        if (string.IsNullOrWhiteSpace(keyName))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return RemoveApiKey(provider);
        }

        lock (_lock)
        {
            try
            {
                var entries = LoadEntries();
                entries[keyName] = Encrypt(apiKey.Trim());
                SaveEntries(entries);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public bool RemoveApiKey(string provider)
    {
        var keyName = ResolveProviderKeyName(provider);
        if (string.IsNullOrWhiteSpace(keyName))
        {
            return false;
        }

        lock (_lock)
        {
            try
            {
                var entries = LoadEntries();
                if (!entries.Remove(keyName))
                {
                    return true;
                }

                SaveEntries(entries);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public string? GetOpenRouterApiKey()
    {
        return GetApiKey("OpenRouter");
    }

    public bool SaveOpenRouterApiKey(string apiKey)
    {
        return SaveApiKey("OpenRouter", apiKey);
    }

    public bool RemoveOpenRouterApiKey()
    {
        return RemoveApiKey("OpenRouter");
    }

    private static string? ResolveProviderKeyName(string provider)
    {
        return (provider ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "openrouter" => OpenRouterApiKeyName,
            "gemini" => GeminiApiKeyName,
            "openai" => OpenAIApiKeyName,
            "grok" or "xai" => GrokApiKeyName,
            _ => null
        };
    }

    private Dictionary<string, string> LoadEntries()
    {
        try
        {
            if (!File.Exists(_storageFilePath))
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }

            var json = File.ReadAllText(_storageFilePath);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private void SaveEntries(Dictionary<string, string> entries)
    {
        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = false });
        File.WriteAllText(_storageFilePath, json);
        SetUnixModeIfNeeded(_storageFilePath);
    }

    private string Encrypt(string plainText)
    {
        var plain = Encoding.UTF8.GetBytes(plainText);
        var key = GetDerivedKey();
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(key);
        aes.Encrypt(nonce, plain, cipher, tag);

        return AesPrefix +
               Convert.ToBase64String(nonce) + ":" +
               Convert.ToBase64String(tag) + ":" +
               Convert.ToBase64String(cipher);
    }

    private string? Decrypt(string payload)
    {
        if (!payload.StartsWith(AesPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var raw = payload.Substring(AesPrefix.Length);
        var parts = raw.Split(':');
        if (parts.Length != 3)
        {
            return null;
        }

        var nonce = Convert.FromBase64String(parts[0]);
        var tag = Convert.FromBase64String(parts[1]);
        var cipher = Convert.FromBase64String(parts[2]);

        var plain = new byte[cipher.Length];
        var key = GetDerivedKey();
        using var aes = new AesGcm(key);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }

    private byte[] GetDerivedKey()
    {
        var salt = GetOrCreateSalt();
        var passphrase = string.Join("|",
            "HarvestmoonGCS",
            Environment.UserName,
            Environment.MachineName,
            Environment.OSVersion.VersionString);

        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(passphrase),
            salt,
            150_000,
            HashAlgorithmName.SHA256,
            32);
    }

    private byte[] GetOrCreateSalt()
    {
        if (File.Exists(_saltFilePath))
        {
            return File.ReadAllBytes(_saltFilePath);
        }

        var salt = RandomNumberGenerator.GetBytes(32);
        File.WriteAllBytes(_saltFilePath, salt);
        SetUnixModeIfNeeded(_saltFilePath);
        return salt;
    }

    private static void SetUnixModeIfNeeded(string filePath)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(
                    filePath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
        catch
        {
        }
    }
}
#endif
