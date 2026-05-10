#if __ANDROID__
using Android.Content;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pigeon_Uno.Core.Services;
using Pigeon_Uno.Core.Models;

namespace Pigeon_Uno.Platforms.Android.Services;

/// <summary>
/// Android-specific settings service using SharedPreferences
/// </summary>
public class AndroidSettingsService : ISettingsService
{
    private const string PreferencesName = "pigeon_settings";
    private readonly Context _context;
    private ISharedPreferences? _preferences;
    private AppSettings _appSettings;
    
    /// <summary>
    /// Gets the current application settings
    /// </summary>
    public AppSettings Settings => _appSettings;
    
    public AndroidSettingsService(Context context)
    {
        _context = context;
        _preferences = _context.GetSharedPreferences(PreferencesName, FileCreationMode.Private);
        _appSettings = new AppSettings();
    }
    
    /// <summary>
    /// Save string setting
    /// </summary>
    public void SaveString(string key, string value)
    {
        var editor = _preferences?.Edit();
        editor?.PutString(key, value);
        editor?.Apply();
    }
    
    /// <summary>
    /// Get string setting
    /// </summary>
    public string? GetString(string key, string? defaultValue = null)
    {
        return _preferences?.GetString(key, defaultValue);
    }
    
    /// <summary>
    /// Save int setting
    /// </summary>
    public void SaveInt(string key, int value)
    {
        var editor = _preferences?.Edit();
        editor?.PutInt(key, value);
        editor?.Apply();
    }
    
    /// <summary>
    /// Get int setting
    /// </summary>
    public int GetInt(string key, int defaultValue = 0)
    {
        return _preferences?.GetInt(key, defaultValue) ?? defaultValue;
    }
    
    /// <summary>
    /// Save bool setting
    /// </summary>
    public void SaveBool(string key, bool value)
    {
        var editor = _preferences?.Edit();
        editor?.PutBoolean(key, value);
        editor?.Apply();
    }
    
    /// <summary>
    /// Get bool setting
    /// </summary>
    public bool GetBool(string key, bool defaultValue = false)
    {
        return _preferences?.GetBoolean(key, defaultValue) ?? defaultValue;
    }
    
    /// <summary>
    /// Save float setting
    /// </summary>
    public void SaveFloat(string key, float value)
    {
        var editor = _preferences?.Edit();
        editor?.PutFloat(key, value);
        editor?.Apply();
    }
    
    /// <summary>
    /// Get float setting
    /// </summary>
    public float GetFloat(string key, float defaultValue = 0f)
    {
        return _preferences?.GetFloat(key, defaultValue) ?? defaultValue;
    }
    
    /// <summary>
    /// Save long setting
    /// </summary>
    public void SaveLong(string key, long value)
    {
        var editor = _preferences?.Edit();
        editor?.PutLong(key, value);
        editor?.Apply();
    }
    
    /// <summary>
    /// Get long setting
    /// </summary>
    public long GetLong(string key, long defaultValue = 0L)
    {
        return _preferences?.GetLong(key, defaultValue) ?? defaultValue;
    }
    
    /// <summary>
    /// Check if key exists
    /// </summary>
    public bool Contains(string key)
    {
        return _preferences?.Contains(key) ?? false;
    }
    
    /// <summary>
    /// Remove setting
    /// </summary>
    public void Remove(string key)
    {
        var editor = _preferences?.Edit();
        editor?.Remove(key);
        editor?.Apply();
    }
    
    /// <summary>
    /// Clear all settings
    /// </summary>
    public void Clear()
    {
        var editor = _preferences?.Edit();
        editor?.Clear();
        editor?.Apply();
    }
    
    /// <summary>
    /// Get all keys
    /// </summary>
    public ICollection<string>? GetAllKeys()
    {
        return _preferences?.All?.Keys;
    }
    
    // Additional methods for ISettingsService compatibility
    public async Task<bool> SetSettingAsync<T>(string key, T value)
    {
        try
        {
            if (value is string stringValue)
            {
                SaveString(key, stringValue);
            }
            else if (value is int intValue)
            {
                SaveInt(key, intValue);
            }
            else if (value is bool boolValue)
            {
                SaveBool(key, boolValue);
            }
            else if (value is float floatValue)
            {
                SaveFloat(key, floatValue);
            }
            else if (value is long longValue)
            {
                SaveLong(key, longValue);
            }
            else
            {
                // Convert to string as fallback
                SaveString(key, value?.ToString() ?? "");
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    public T GetSetting<T>(string key, T defaultValue)
    {
        try
        {
            if (typeof(T) == typeof(string))
            {
                return (T)(object)(GetString(key, defaultValue?.ToString()) ?? defaultValue?.ToString() ?? "");
            }
            else if (typeof(T) == typeof(int))
            {
                return (T)(object)GetInt(key, (int)(object)(defaultValue ?? (object)0));
            }
            else if (typeof(T) == typeof(bool))
            {
                return (T)(object)GetBool(key, (bool)(object)(defaultValue ?? (object)false));
            }
            else if (typeof(T) == typeof(float))
            {
                return (T)(object)GetFloat(key, (float)(object)(defaultValue ?? (object)0f));
            }
            else if (typeof(T) == typeof(long))
            {
                return (T)(object)GetLong(key, (long)(object)(defaultValue ?? (object)0L));
            }
            else
            {
                // Try to parse from string
                var stringValue = GetString(key, defaultValue?.ToString());
                if (stringValue != null && typeof(T) == typeof(string))
                {
                    return (T)(object)stringValue;
                }
            }
        }
        catch
        {
            // Fall through to default
        }
        return defaultValue;
    }

    public async Task<bool> SaveSettingsAsync()
    {
        try
        {
            // Android SharedPreferences are automatically saved
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> LoadSettingsAsync()
    {
        try
        {
            // Android SharedPreferences are automatically loaded
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ResetToDefaultAsync()
    {
        try
        {
            // Clear all settings
            Clear();
            
            // Reset AppSettings to defaults
            _appSettings = new AppSettings();
            
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    // Common settings keys
    public const string KeyTheme = "theme";
    public const string KeyConnectionType = "connection_type";
    public const string KeyConnectionAddress = "connection_address";
    public const string KeyConnectionPort = "connection_port";
    public const string KeyBaudRate = "baud_rate";
    public const string KeyAutoConnect = "auto_connect";
    public const string KeyMapProvider = "map_provider";
    public const string KeyMapZoom = "map_zoom";
    public const string KeyMapCenter = "map_center";
    public const string KeyUnits = "units"; // metric/imperial
    public const string KeyLanguage = "language";
    public const string KeyNotifications = "notifications";
    public const string KeyVibration = "vibration";
    public const string KeyKeepScreenOn = "keep_screen_on";
}
#endif
