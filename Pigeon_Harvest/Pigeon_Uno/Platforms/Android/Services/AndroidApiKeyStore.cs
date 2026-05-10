#if __ANDROID__
using System;
using System.Security.Cryptography;
using System.Text;
using Android.Content;
using Android.OS;
using Android.Security.Keystore;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;
using Pigeon_Uno.Core.Services.AI;
using JCipherMode = Javax.Crypto.CipherMode;

namespace Pigeon_Uno.Platforms.Android.Services;

/// <summary>
/// Android secure API key store using Android Keystore (AES-GCM).
/// For API levels below 23, uses encrypted fallback in app-private preferences.
/// </summary>
public sealed class AndroidApiKeyStore : IApiKeyStore
{
    private const string PreferencesName = "pigeon_secure_keys";
    private const string FallbackSaltKey = "fallback_salt";
    private const string KeyStoreName = "AndroidKeyStore";

    private readonly Context _context;
    private readonly object _lock = new();

    public AndroidApiKeyStore(Context context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public string? GetApiKey(string provider)
    {
        var normalized = NormalizeProvider(provider);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var payloadKey = BuildPayloadKey(normalized);
        lock (_lock)
        {
            try
            {
                var prefs = GetPrefs();
                var payload = prefs.GetString(payloadKey, null);
                if (string.IsNullOrWhiteSpace(payload))
                {
                    return null;
                }

                if (payload.StartsWith("ks:", StringComparison.Ordinal))
                {
                    return DecryptKeystorePayload(payload, normalized);
                }

                if (payload.StartsWith("fb:", StringComparison.Ordinal))
                {
                    return DecryptFallbackPayload(payload);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }

    public bool SaveApiKey(string provider, string apiKey)
    {
        var normalized = NormalizeProvider(provider);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return RemoveApiKey(normalized);
        }

        var payloadKey = BuildPayloadKey(normalized);
        lock (_lock)
        {
            try
            {
                var payload = SupportsKeystore()
                    ? EncryptKeystorePayload(apiKey.Trim(), normalized)
                    : EncryptFallbackPayload(apiKey.Trim());

                var prefs = GetPrefs();
                using var editor = prefs.Edit();
                editor.PutString(payloadKey, payload);
                editor.Apply();
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
        var normalized = NormalizeProvider(provider);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var payloadKey = BuildPayloadKey(normalized);
        lock (_lock)
        {
            try
            {
                var prefs = GetPrefs();
                using var editor = prefs.Edit();
                editor.Remove(payloadKey);
                editor.Apply();
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

    private bool SupportsKeystore() => (int)Build.VERSION.SdkInt >= (int)BuildVersionCodes.M;

    private ISharedPreferences GetPrefs()
    {
        return _context.GetSharedPreferences(PreferencesName, FileCreationMode.Private)
               ?? throw new InvalidOperationException("Secure preferences are unavailable.");
    }

    private string EncryptKeystorePayload(string plainText, string normalizedProvider)
    {
        var key = GetOrCreateKey(normalizedProvider);
        var cipher = Cipher.GetInstance("AES/GCM/NoPadding");
        cipher.Init(JCipherMode.EncryptMode, key);

        var iv = cipher.GetIV();
        var encrypted = cipher.DoFinal(Encoding.UTF8.GetBytes(plainText));

        return "ks:" +
               Convert.ToBase64String(iv) + ":" +
               Convert.ToBase64String(encrypted);
    }

    private string? DecryptKeystorePayload(string payload, string normalizedProvider)
    {
        var parts = payload.Split(':');
        if (parts.Length != 3)
        {
            return null;
        }

        var iv = Convert.FromBase64String(parts[1]);
        var encrypted = Convert.FromBase64String(parts[2]);
        var key = GetOrCreateKey(normalizedProvider);

        var cipher = Cipher.GetInstance("AES/GCM/NoPadding");
        var spec = new GCMParameterSpec(128, iv);
        cipher.Init(JCipherMode.DecryptMode, key, spec);
        var plain = cipher.DoFinal(encrypted);
        return Encoding.UTF8.GetString(plain);
    }

    private ISecretKey GetOrCreateKey(string normalizedProvider)
    {
        var keyAlias = BuildKeyAlias(normalizedProvider);
        var keyStore = KeyStore.GetInstance(KeyStoreName);
        keyStore.Load(null);

        var existing = keyStore.GetKey(keyAlias, null) as ISecretKey;
        if (existing != null)
        {
            return existing;
        }

        var keyGenerator = KeyGenerator.GetInstance(KeyProperties.KeyAlgorithmAes, KeyStoreName);
        var builder = new KeyGenParameterSpec.Builder(
            keyAlias,
            KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
            .SetBlockModes(KeyProperties.BlockModeGcm)
            .SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone)
            .SetKeySize(256)
            .SetRandomizedEncryptionRequired(true);

        keyGenerator.Init(builder.Build());
        return (ISecretKey)keyGenerator.GenerateKey();
    }

    private string EncryptFallbackPayload(string plainText)
    {
        var key = GetFallbackKey();
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plain = Encoding.UTF8.GetBytes(plainText);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(key);
        aes.Encrypt(nonce, plain, cipher, tag);

        return "fb:" +
               Convert.ToBase64String(nonce) + ":" +
               Convert.ToBase64String(tag) + ":" +
               Convert.ToBase64String(cipher);
    }

    private string? DecryptFallbackPayload(string payload)
    {
        var parts = payload.Split(':');
        if (parts.Length != 4)
        {
            return null;
        }

        var nonce = Convert.FromBase64String(parts[1]);
        var tag = Convert.FromBase64String(parts[2]);
        var cipher = Convert.FromBase64String(parts[3]);
        var plain = new byte[cipher.Length];
        var key = GetFallbackKey();

        using var aes = new AesGcm(key);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }

    private byte[] GetFallbackKey()
    {
        var prefs = GetPrefs();
        var saltB64 = prefs.GetString(FallbackSaltKey, null);

        byte[] salt;
        if (string.IsNullOrWhiteSpace(saltB64))
        {
            salt = RandomNumberGenerator.GetBytes(32);
            using var editor = prefs.Edit();
            editor.PutString(FallbackSaltKey, Convert.ToBase64String(salt));
            editor.Apply();
        }
        else
        {
            salt = Convert.FromBase64String(saltB64);
        }

        var androidId = global::Android.Provider.Settings.Secure.GetString(
            _context.ContentResolver,
            global::Android.Provider.Settings.Secure.AndroidId) ?? "unknown";
        var passphrase = $"{_context.PackageName}|{androidId}|Pigeon_Uno";

        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(passphrase),
            salt,
            120_000,
            HashAlgorithmName.SHA256,
            32);
    }

    private static string? NormalizeProvider(string provider)
    {
        return (provider ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "openrouter" => "openrouter",
            "gemini" => "gemini",
            "openai" => "openai",
            "grok" or "xai" => "grok",
            _ => null
        };
    }

    private static string BuildPayloadKey(string normalizedProvider)
    {
        return $"{normalizedProvider}_api_key_payload";
    }

    private static string BuildKeyAlias(string normalizedProvider)
    {
        return $"pigeon_{normalizedProvider}_api_key";
    }
}
#endif
