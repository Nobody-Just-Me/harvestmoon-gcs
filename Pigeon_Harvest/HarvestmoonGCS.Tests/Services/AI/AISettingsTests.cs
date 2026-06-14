using Xunit;
using HarvestmoonGCS.Core.Models.AI;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Core.Models;
using System;
using System.IO;
using System.Threading.Tasks;

namespace HarvestmoonGCS.Tests.Services.AI;

public class AISettingsTests
{
    private static string CreateSettingsPath()
        => Path.Combine(Path.GetTempPath(), "HarvestmoonGCS.Tests", Guid.NewGuid().ToString("N"), "settings.json");

    [Fact]
    public void AISettings_HasCorrectDefaults()
    {
        var settings = new AISettings();

        Assert.True(settings.Enabled);
        Assert.Equal("OpenRouter", settings.Provider);
        Assert.Equal("Gemini", settings.FallbackProvider);
        Assert.Equal(string.Empty, settings.ApiKey);
        Assert.Equal("https://openrouter.ai/api/v1", settings.BaseUrl);
        Assert.Equal("https://pigeon-gcs.app", settings.SiteUrl);
        Assert.Equal("Pigeon GCS - PIA", settings.SiteName);
        Assert.Equal(30, settings.HistoryRetentionDays);
    }

    [Fact]
    public void AIModelsConfig_HasCorrectDefaults()
    {
        var models = new AIModelsConfig();

        Assert.Equal("google/gemini-2.5-flash-lite", models.TelemetryAnalysis);
        Assert.Equal("google/gemini-2.5-flash-lite", models.AnomalyDetection);
        Assert.Equal("google/gemini-2.5-flash-lite", models.NaturalLanguageChat);
        Assert.Equal("deepseek/deepseek-v4", models.MaintenancePrediction);
        Assert.Equal("deepseek/deepseek-v4", models.PerformanceScoring);
        Assert.Equal("google/gemini-2.5-flash-lite", models.VoiceIntent);
        Assert.Equal("openrouter/free", models.Fallback);
    }

    [Fact]
    public void AIAnalysisConfig_HasCorrectDefaults()
    {
        var config = new AIAnalysisConfig();

        Assert.Equal(30, config.IntervalSeconds);
        Assert.Equal(30, config.BufferSeconds);
        Assert.Equal(0.7, config.MinConfidence);
    }

    [Fact]
    public void TelemetrySamplingConfig_HasCorrectDefaults()
    {
        var config = new TelemetrySamplingConfig();

        Assert.Equal(500, config.MinIntervalMs);
        Assert.Equal(2000, config.ForceIntervalMs);
        Assert.Equal(0.8, config.BatteryDeltaPercent);
        Assert.Equal(1.5, config.AltitudeDeltaMeters);
        Assert.Equal(0.8, config.SpeedDeltaMps);
        Assert.Equal(7, config.HeadingDeltaDeg);
        Assert.Equal(0.6, config.VerticalSpeedDeltaMps);
        Assert.Equal(2.5, config.GpsDistanceDeltaMeters);
        Assert.Equal(0.3, config.GpsHdopDelta);
        Assert.Equal(0.08, config.BatteryVoltageDeltaVolts);
        Assert.Equal(0.4, config.BatteryCurrentDeltaAmp);
        Assert.Equal(2.0, config.RollDeltaDeg);
        Assert.Equal(2.0, config.PitchDeltaDeg);
        Assert.Equal(8.0, config.VibrationMagnitudeDelta);
        Assert.Equal(8.0, config.LinkQualityDeltaPercent);
        Assert.True(config.SampleOnFlightModeChange);
        Assert.True(config.SampleOnArmedStateChange);
    }

    [Fact]
    public void AICacheConfig_HasCorrectDefaults()
    {
        var config = new AICacheConfig();

        Assert.True(config.Enabled);
        Assert.Equal(300, config.TTLSeconds);
        Assert.Equal(50, config.MaxSizeMB);
    }

    [Fact]
    public void AnomalyDetectionConfig_HasCorrectDefaults()
    {
        var config = new AnomalyDetectionConfig();

        Assert.True(config.RuleBasedEnabled);
        Assert.True(config.StatisticalEnabled);
        Assert.True(config.AIEnabled);
        Assert.False(config.AutoResponseEnabled);
        Assert.NotNull(config.Thresholds);
    }

    [Fact]
    public void AnomalyThresholds_HasCorrectDefaults()
    {
        var thresholds = new AnomalyThresholds();

        Assert.Equal(15, thresholds.BatteryCritical);
        Assert.Equal(30, thresholds.BatteryWarning);
        Assert.Equal(60, thresholds.VibrationHigh);
        Assert.Equal(100, thresholds.VibrationCritical);
        Assert.Equal(6, thresholds.GpsMinSatellites);
        Assert.Equal(10, thresholds.WindMaxSpeed);
    }

    [Fact]
    public async Task SetSettingAsync_AISettings_StoresAndRetrievesCorrectly()
    {
        var settingsService = new JsonSettingsService(CreateSettingsPath());
        await settingsService.LoadSettingsAsync();

        var aiSettings = new AISettings
        {
            Enabled = false,
            Provider = "Gemini",
            FallbackProvider = "Grok",
            ApiKey = "test-key",
            Analysis = new AIAnalysisConfig { IntervalSeconds = 60 }
        };

        var result = await settingsService.SetSettingAsync("AISettings", aiSettings);

        Assert.True(result);

        var retrieved = settingsService.GetSetting<AISettings>("AISettings", new AISettings());
        Assert.False(retrieved.Enabled);
        Assert.Equal("Gemini", retrieved.Provider);
        Assert.Equal("Grok", retrieved.FallbackProvider);
        Assert.Equal(string.Empty, retrieved.ApiKey);
        Assert.Equal(60, retrieved.Analysis.IntervalSeconds);
        Assert.Equal(30, retrieved.HistoryRetentionDays);
    }

    [Fact]
    public async Task AISettings_RoundTrip_PreservesAllValues()
    {
        var settingsPath = CreateSettingsPath();
        var settingsService1 = new JsonSettingsService(settingsPath);
        await settingsService1.LoadSettingsAsync();

        var original = new AISettings
        {
            Enabled = true,
            Provider = "OpenRouter",
            FallbackProvider = "Gemini",
            ApiKey = "sk-or-v1-test",
            BaseUrl = "https://openrouter.ai/api/v1",
            Models = new AIModelsConfig
            {
                TelemetryAnalysis = "google/gemini-2.5-flash-lite",
                AnomalyDetection = "google/gemini-2.5-flash-lite",
                Fallback = "openrouter/free"
            },
            Analysis = new AIAnalysisConfig
            {
                IntervalSeconds = 30,
                BufferSeconds = 30,
                MinConfidence = 0.7
            },
            Cache = new AICacheConfig
            {
                Enabled = true,
                TTLSeconds = 300,
                MaxSizeMB = 50
            },
            HistoryRetentionDays = 45,
            AnomalyDetection = new AnomalyDetectionConfig
            {
                RuleBasedEnabled = true,
                StatisticalEnabled = true,
                AIEnabled = true,
                AutoResponseEnabled = false,
                Thresholds = new AnomalyThresholds
                {
                    BatteryCritical = 15,
                    BatteryWarning = 30,
                    VibrationHigh = 60,
                    VibrationCritical = 100,
                    GpsMinSatellites = 6,
                    WindMaxSpeed = 10
                }
            }
        };

        await settingsService1.SetSettingAsync("AISettings", original);

        var settingsService2 = new JsonSettingsService(settingsPath);
        await settingsService2.LoadSettingsAsync();

        var restored = settingsService2.GetSetting<AISettings>("AISettings", new AISettings());

        Assert.True(restored.Enabled);
        Assert.Equal("OpenRouter", restored.Provider);
        Assert.Equal("Gemini", restored.FallbackProvider);
        Assert.Equal(string.Empty, restored.ApiKey);
        Assert.Equal("google/gemini-2.5-flash-lite", restored.Models.TelemetryAnalysis);
        Assert.Equal(30, restored.Analysis.IntervalSeconds);
        Assert.True(restored.Cache.Enabled);
        Assert.Equal(300, restored.Cache.TTLSeconds);
        Assert.True(restored.AnomalyDetection.RuleBasedEnabled);
        Assert.Equal(15, restored.AnomalyDetection.Thresholds.BatteryCritical);
        Assert.Equal(45, restored.HistoryRetentionDays);
    }

    [Fact]
    public void AppSettings_HasAIProperty()
    {
        var settings = new AppSettings();
        Assert.NotNull(settings.AI);
        Assert.IsType<AISettings>(settings.AI);
    }
}
