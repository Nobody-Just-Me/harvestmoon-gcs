using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HarvestmoonGCS.Core.Config
{
    /// <summary>
    /// Manajer konfigurasi aplikasi dengan pattern Singleton
    /// Menggunakan System.Text.Json untuk cross-platform compatibility
    /// </summary>
    public class ConfigManager
    {
        private static ConfigManager? _instance;
        private static readonly object _lock = new object();
        
        /// <summary>
        /// Instance singleton ConfigManager
        /// </summary>
        public static ConfigManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new ConfigManager();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Pengaturan aplikasi
        /// </summary>
        public Models.AppSettings Settings { get; private set; }
        
        private readonly string _configPath;
        private readonly string _configFolder;

        private ConfigManager()
        {
            try
            {
                // Define cross-platform path
                string appDataPath = GetAppDataPath();
                
                _configFolder = Path.Combine(appDataPath, "PigeonGCS");
                _configPath = Path.Combine(_configFolder, "settings.json");
                
                System.Diagnostics.Debug.WriteLine($"[ConfigManager] Config path: {_configPath}");
                
                Settings = new Models.AppSettings();
                Load();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigManager] Constructor error: {ex.Message}");
                Settings = new Models.AppSettings();
            }
        }

        /// <summary>
        /// Mendapatkan path AppData yang cross-platform
        /// </summary>
        private string GetAppDataPath()
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (string.IsNullOrEmpty(appDataPath))
                {
                    appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                }
                if (string.IsNullOrEmpty(appDataPath))
                {
                    appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".config");
                }
                return appDataPath;
            }
            catch
            {
                return Path.GetTempPath();
            }
        }

        /// <summary>
        /// Memuat pengaturan dari file
        /// </summary>
        public void Load()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    string json = File.ReadAllText(_configPath);
                    var loadedSettings = JsonSerializer.Deserialize<Models.AppSettings>(json, GetJsonOptions());
                    if (loadedSettings != null)
                    {
                        Settings = loadedSettings;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigManager] Failed to load settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Menyimpan pengaturan ke file
        /// </summary>
        public void Save()
        {
            try
            {
                if (!Directory.Exists(_configFolder))
                {
                    Directory.CreateDirectory(_configFolder);
                }

                string json = JsonSerializer.Serialize(Settings, GetJsonOptions());
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigManager] Failed to save settings: {ex.Message}");
            }
        }

        private JsonSerializerOptions GetJsonOptions()
        {
            return new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }
    }
}
