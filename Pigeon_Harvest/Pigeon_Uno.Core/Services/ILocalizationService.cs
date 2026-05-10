using System.Threading.Tasks;

namespace Pigeon_Uno.Core.Services;

public interface ILocalizationService
{
    string CurrentLanguage { get; }
    string GetString(string key);
    Task SetLanguageAsync(string languageCode);
    string[] GetAvailableLanguages();
}
