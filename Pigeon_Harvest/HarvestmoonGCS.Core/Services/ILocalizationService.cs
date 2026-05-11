using System.Threading.Tasks;

namespace HarvestmoonGCS.Core.Services;

public interface ILocalizationService
{
    string CurrentLanguage { get; }
    string GetString(string key);
    Task SetLanguageAsync(string languageCode);
    string[] GetAvailableLanguages();
}
