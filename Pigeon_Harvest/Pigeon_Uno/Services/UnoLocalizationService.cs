using Windows.Globalization;
using Pigeon_Uno.Core.Services;

namespace Pigeon_Uno.Services;

public class UnoLocalizationService : ILocalizationService
{
    public string CurrentLanguage { get; private set; } = "en";

    public string GetString(string key)
    {
        return key; // Simplified implementation
    }

    public async Task SetLanguageAsync(string languageCode)
    {
        CurrentLanguage = languageCode;
        ApplicationLanguages.PrimaryLanguageOverride = languageCode;
        await Task.CompletedTask;
    }

    public string[] GetAvailableLanguages()
    {
        return new[] { "en", "id" };
    }
}
