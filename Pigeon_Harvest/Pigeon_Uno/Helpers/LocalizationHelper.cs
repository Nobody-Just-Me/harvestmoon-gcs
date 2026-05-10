using Windows.ApplicationModel.Resources;

namespace Pigeon_Uno.Helpers;

/// <summary>
/// Helper class for accessing localized strings from resource files
/// </summary>
public static class LocalizationHelper
{
    private static ResourceLoader? _resourceLoader;

    private static ResourceLoader ResourceLoader
    {
        get
        {
            _resourceLoader ??= ResourceLoader.GetForViewIndependentUse();
            return _resourceLoader;
        }
    }

    /// <summary>
    /// Gets a localized string by key
    /// </summary>
    /// <param name="key">The resource key</param>
    /// <returns>The localized string, or the key if not found</returns>
    public static string GetString(string key)
    {
        try
        {
            var value = ResourceLoader.GetString(key);
            return string.IsNullOrEmpty(value) ? key : value;
        }
        catch
        {
            return key;
        }
    }

    /// <summary>
    /// Reloads the resource loader (useful after language change)
    /// </summary>
    public static void Reload()
    {
        _resourceLoader = null;
    }
}
