using Xunit;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Core.Models;
using System.IO;
using System.Threading.Tasks;

namespace HarvestmoonGCS.Tests;

public class SettingsServiceTests
{
    [Fact]
    public async Task LoadSettings_CreatesDefaultSettings_WhenFileDoesNotExist()
    {
        // Arrange
        var settingsService = new JsonSettingsService();

        // Act
        var result = await settingsService.LoadSettingsAsync();

        // Assert
        Assert.True(result);
        Assert.NotNull(settingsService.Settings);
        Assert.Equal("en", settingsService.Settings.Language);
        Assert.Equal("ArcGISTopographic", settingsService.Settings.MapType);
    }

    [Fact]
    public async Task SaveSettings_CreatesFile_WithCorrectData()
    {
        // Arrange
        var settingsService = new JsonSettingsService();
        await settingsService.LoadSettingsAsync();

        // Modify settings
        settingsService.Settings.Language = "id";
        settingsService.Settings.MapType = "GoogleSatellite";
        settingsService.Settings.Connection.IpAddress = "192.168.1.100";
        settingsService.Settings.Connection.Port = 14550;

        // Act
        var result = await settingsService.SaveSettingsAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task LoadSettings_RestoresSavedSettings()
    {
        // Arrange
        var settingsService1 = new JsonSettingsService();
        await settingsService1.LoadSettingsAsync();

        // Modify and save settings
        settingsService1.Settings.Language = "id";
        settingsService1.Settings.MapType = "GoogleSatellite";
        settingsService1.Settings.Connection.IpAddress = "192.168.1.100";
        settingsService1.Settings.Connection.Port = 14550;
        settingsService1.Settings.Map.DefaultWaypointRadius = 75.0f;
        await settingsService1.SaveSettingsAsync();

        // Create new instance and load
        var settingsService2 = new JsonSettingsService();
        var result = await settingsService2.LoadSettingsAsync();

        // Assert
        Assert.True(result);
        Assert.Equal("id", settingsService2.Settings.Language);
        Assert.Equal("GoogleSatellite", settingsService2.Settings.MapType);
        Assert.Equal("192.168.1.100", settingsService2.Settings.Connection.IpAddress);
        Assert.Equal(14550, settingsService2.Settings.Connection.Port);
        Assert.Equal(75.0f, settingsService2.Settings.Map.DefaultWaypointRadius);
    }

    [Fact]
    public async Task ResetToDefault_RestoresDefaultSettings()
    {
        // Arrange
        var settingsService = new JsonSettingsService();
        await settingsService.LoadSettingsAsync();

        // Modify settings
        settingsService.Settings.Language = "id";
        settingsService.Settings.MapType = "GoogleSatellite";
        await settingsService.SaveSettingsAsync();

        // Act
        var result = await settingsService.ResetToDefaultAsync();

        // Assert
        Assert.True(result);
        Assert.Equal("en", settingsService.Settings.Language);
        Assert.Equal("ArcGISTopographic", settingsService.Settings.MapType);
    }

    [Fact]
    public async Task GetSetting_ReturnsCorrectValue()
    {
        // Arrange
        var settingsService = new JsonSettingsService();
        await settingsService.LoadSettingsAsync();

        // Act
        var language = settingsService.GetSetting("Language", "default");

        // Assert
        Assert.Equal("en", language);
    }

    [Fact]
    public async Task SetSetting_UpdatesAndSavesValue()
    {
        // Arrange
        var settingsService = new JsonSettingsService();
        await settingsService.LoadSettingsAsync();

        // Act
        var result = await settingsService.SetSettingAsync("Language", "id");

        // Assert
        Assert.True(result);
        Assert.Equal("id", settingsService.Settings.Language);
    }

    [Fact]
    public void ConnectionSettings_HasCorrectDefaults()
    {
        // Arrange & Act
        var settings = new ConnectionSettings();

        // Assert
        Assert.Equal("TCP", settings.ConnectionType);
        Assert.Equal("127.0.0.1", settings.IpAddress);
        Assert.Equal(5760, settings.Port);
        Assert.Equal("COM1", settings.SerialPort);
        Assert.Equal(57600, settings.BaudRate);
        Assert.False(settings.AutoConnect);
    }

    [Fact]
    public void MapSettings_HasCorrectDefaults()
    {
        // Arrange & Act
        var settings = new MapSettings();

        // Assert
        Assert.True(settings.FollowVehicle);
        Assert.Equal(50.0f, settings.DefaultWaypointRadius);
        Assert.Equal(100.0f, settings.DefaultWaypointAltitude);
        Assert.False(settings.ShowGeofence);
        Assert.Equal(1000.0f, settings.GeofenceRadius);
    }

    [Fact]
    public void UiSettings_HasCorrectDefaults()
    {
        // Arrange & Act
        var settings = new UiSettings();

        // Assert
        Assert.Equal("Dark", settings.Theme);
        Assert.False(settings.ShowAdvancedOptions);
        Assert.True(settings.EnableSoundAlerts);
        Assert.False(settings.EnableVoiceAlerts);
    }
}
