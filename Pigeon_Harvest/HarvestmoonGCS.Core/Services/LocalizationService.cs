using System.Collections.Generic;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Services;

namespace HarvestmoonGCS.Services;

public class LocalizationService : ILocalizationService
{
    private readonly Dictionary<string, Dictionary<string, string>> _resources = new();
    private string _currentLanguage = "en-US";

    public string CurrentLanguage => _currentLanguage;

    public LocalizationService()
    {
        LoadResources();
    }

    public string GetString(string key)
    {
        if (_resources.TryGetValue(_currentLanguage, out var languageResources) &&
            languageResources.TryGetValue(key, out var value))
        {
            return value;
        }
        
        // Fallback to English
        if (_resources.TryGetValue("en-US", out var englishResources) &&
            englishResources.TryGetValue(key, out var englishValue))
        {
            return englishValue;
        }
        
        return key; // Return key if not found
    }

    public async Task SetLanguageAsync(string languageCode)
    {
        if (_resources.ContainsKey(languageCode))
        {
            _currentLanguage = languageCode;
        }
        await Task.CompletedTask;
    }

    public string[] GetAvailableLanguages()
    {
        return new[] { "en-US", "id-ID" };
    }

    private void LoadResources()
    {
        // English resources
        _resources["en-US"] = new Dictionary<string, string>
        {
            ["AppName"] = "Pigeon GCS",
            ["Flight"] = "Flight",
            ["Map"] = "Map",
            ["Stats"] = "Statistics",
            ["Calibration"] = "Calibration",
            ["Tracker"] = "Tracker",
            ["TLOG"] = "TLOG",
            ["LoRa"] = "LoRa",
            ["Connect"] = "Connect",
            ["Disconnect"] = "Disconnect",
            ["Connected"] = "Connected",
            ["Disconnected"] = "Disconnected",
            ["Armed"] = "Armed",
            ["Disarmed"] = "Disarmed"
        };

        // Indonesian resources
        _resources["id-ID"] = new Dictionary<string, string>
        {
            ["AppName"] = "Pigeon GCS",
            ["Flight"] = "Penerbangan",
            ["Map"] = "Peta",
            ["Stats"] = "Statistik",
            ["Calibration"] = "Kalibrasi",
            ["Tracker"] = "Pelacak",
            ["TLOG"] = "TLOG",
            ["LoRa"] = "LoRa",
            ["Connect"] = "Sambung",
            ["Disconnect"] = "Putus",
            ["Connected"] = "Tersambung",
            ["Disconnected"] = "Terputus",
            ["Armed"] = "Bersenjata",
            ["Disarmed"] = "Tidak Bersenjata"
        };
    }
}
