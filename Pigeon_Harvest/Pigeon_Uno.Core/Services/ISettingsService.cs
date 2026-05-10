using System.Threading.Tasks;
using Pigeon_Uno.Core.Models;

namespace Pigeon_Uno.Core.Services;

/// <summary>
/// Cross-platform settings service interface
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets the current application settings
    /// </summary>
    AppSettings Settings { get; }
    
    void SaveString(string key, string value);
    string? GetString(string key, string? defaultValue = null);
    void SaveInt(string key, int value);
    int GetInt(string key, int defaultValue = 0);
    void SaveBool(string key, bool value);
    bool GetBool(string key, bool defaultValue = false);
    void SaveFloat(string key, float value);
    float GetFloat(string key, float defaultValue = 0f);
    bool Contains(string key);
    void Remove(string key);
    void Clear();
    
    // Additional methods used by existing code
    Task<bool> SetSettingAsync<T>(string key, T value);
    T GetSetting<T>(string key, T defaultValue);
    Task<bool> SaveSettingsAsync();
    Task<bool> LoadSettingsAsync();
    Task<bool> ResetToDefaultAsync();
}