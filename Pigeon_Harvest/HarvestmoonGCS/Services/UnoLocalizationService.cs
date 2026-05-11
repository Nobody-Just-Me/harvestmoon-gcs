using Windows.Globalization;
using HarvestmoonGCS.Core.Services;

namespace HarvestmoonGCS.Services;

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
