#if !__ANDROID__
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Core.Models;

namespace HarvestmoonGCS.Platforms.Desktop.Services;

/// <summary>
/// Desktop settings service implementation using JSON file
/// </summary>
public class DesktopSettingsService : ISettingsService
{
    private readonly string _settingsPath;
    private readonly Dictionary<string, object> _settings;
    private AppSettings _appSettings;
    
    /// <summary>
    /// Gets the current application settings
    /// </summary>
    public AppSettings Settings => _appSettings;
    
    public DesktopSettingsService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Pigeon"
        );
        
        Directory.CreateDirectory(appDataPath);
        _settingsPath = Path.Combine(appDataPath, "settings.json");
        
        _settings = LoadSettings();
        _appSettings = new AppSettings();
    }
    
    private Dictionary<string, object> LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<Dictionary<string, object>>(json) 
                    ?? new Dictionary<string, object>();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DesktopSettingsService] Error loading settings: {ex.Message}");
        }
        
        return new Dictionary<string, object>();
    }
    
    private void SaveSettings()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DesktopSettingsService] Error saving settings: {ex.Message}");
        }
    }
    
    public void SaveString(string key, string value)
    {
        _settings[key] = value;
        SaveSettings();
    }
    
    public string? GetString(string key, string? defaultValue = null)
    {
        if (_settings.TryGetValue(key, out var value) && value is JsonElement element)
        {
            return element.GetString() ?? defaultValue;
        }
        return _settings.TryGetValue(key, out var stringValue) ? stringValue?.ToString() : defaultValue;
    }
    
    public void SaveInt(string key, int value)
    {
        _settings[key] = value;
        SaveSettings();
    }
    
    public int GetInt(string key, int defaultValue = 0)
    {
        if (_settings.TryGetValue(key, out var value))
        {
            if (value is JsonElement element && element.TryGetInt32(out var intValue))
                return intValue;
            if (int.TryParse(value?.ToString(), out var parsedValue))
                return parsedValue;
        }
        return defaultValue;
    }
    
    public void SaveBool(string key, bool value)
    {
        _settings[key] = value;
        SaveSettings();
    }
    
    public bool GetBool(string key, bool defaultValue = false)
    {
        if (_settings.TryGetValue(key, out var value))
        {
            if (value is JsonElement element && element.ValueKind == JsonValueKind.True)
                return true;
            if (value is JsonElement element2 && element2.ValueKind == JsonValueKind.False)
                return false;
            if (bool.TryParse(value?.ToString(), out var boolValue))
                return boolValue;
        }
        return defaultValue;
    }
    
    public void SaveFloat(string key, float value)
    {
        _settings[key] = value;
        SaveSettings();
    }
    
    public float GetFloat(string key, float defaultValue = 0f)
    {
        if (_settings.TryGetValue(key, out var value))
        {
            if (value is JsonElement element && element.TryGetSingle(out var floatValue))
                return floatValue;
            if (float.TryParse(value?.ToString(), out var parsedValue))
                return parsedValue;
        }
        return defaultValue;
    }
    
    public bool Contains(string key)
    {
        return _settings.ContainsKey(key);
    }
    
    public void Remove(string key)
    {
        _settings.Remove(key);
        SaveSettings();
    }
    
    public void Clear()
    {
        _settings.Clear();
        SaveSettings();
    }

    // Additional methods for compatibility
    public async Task<bool> SetSettingAsync<T>(string key, T value)
    {
        try
        {
            _settings[key] = value ?? throw new ArgumentNullException(nameof(value));
            SaveSettings();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public T GetSetting<T>(string key, T defaultValue)
    {
        if (_settings.TryGetValue(key, out var value))
        {
            try
            {
                if (value is T directValue)
                {
                    return directValue;
                }
                
                if (value is JsonElement element)
                {
                    return JsonSerializer.Deserialize<T>(element.GetRawText()) ?? defaultValue;
                }
                
                // Try to convert string representation
                if (typeof(T) == typeof(string))
                {
                    return (T)(object)(value?.ToString() ?? defaultValue?.ToString() ?? "");
                }
                
                if (typeof(T) == typeof(int) && int.TryParse(value?.ToString(), out var intVal))
                {
                    return (T)(object)intVal;
                }
                
                if (typeof(T) == typeof(bool) && bool.TryParse(value?.ToString(), out var boolVal))
                {
                    return (T)(object)boolVal;
                }
                
                if (typeof(T) == typeof(float) && float.TryParse(value?.ToString(), out var floatVal))
                {
                    return (T)(object)floatVal;
                }
            }
            catch
            {
                // Fall through to default
            }
        }
        return defaultValue;
    }

    public async Task<bool> SaveSettingsAsync()
    {
        try
        {
            SaveSettings();
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
            var newSettings = LoadSettings();
            _settings.Clear();
            foreach (var kvp in newSettings)
            {
                _settings[kvp.Key] = kvp.Value;
            }
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
            _settings.Clear();
            
            // Reset AppSettings to defaults
            _appSettings = new AppSettings();
            
            // Save the reset settings
            SaveSettings();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
#endif