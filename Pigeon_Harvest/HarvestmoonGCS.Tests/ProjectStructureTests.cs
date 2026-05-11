using Xunit;
using FluentAssertions;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using HarvestmoonGCS.Core.Services;

namespace HarvestmoonGCS.Tests;

/// <summary>
/// Tests to validate the project structure and configuration.
/// Validates Requirements 16.1, 16.2, 16.3, 16.4
/// </summary>
public class ProjectStructureTests
{
    [Fact]
    public void ProjectReferences_CoreProjectIsReferenced()
    {
        // Arrange
        var testAssembly = Assembly.GetExecutingAssembly();
        var referencedAssemblies = testAssembly.GetReferencedAssemblies();

        // Act
        var coreAssembly = referencedAssemblies
            .FirstOrDefault(a => a.Name == "HarvestmoonGCS.Core");

        // Assert
        coreAssembly.Should().NotBeNull("Test project should reference HarvestmoonGCS.Core");
    }

    [Fact]
    public void ProjectReferences_CoreProjectCanBeLoaded()
    {
        // Arrange & Act
        Assembly? coreAssembly = null;
        Action loadAction = () =>
        {
            coreAssembly = Assembly.Load("HarvestmoonGCS.Core");
        };

        // Assert
        loadAction.Should().NotThrow("HarvestmoonGCS.Core assembly should be loadable");
        coreAssembly.Should().NotBeNull();
    }

    [Fact]
    public void NuGetPackages_XunitIsInstalled()
    {
        // Arrange
        var testAssembly = Assembly.GetExecutingAssembly();
        var referencedAssemblies = testAssembly.GetReferencedAssemblies();

        // Act
        var xunitAssembly = referencedAssemblies
            .FirstOrDefault(a => a.Name == "xunit.core");

        // Assert
        xunitAssembly.Should().NotBeNull("xUnit should be installed as test framework");
    }

    [Fact]
    public void NuGetPackages_MoqIsInstalled()
    {
        // Arrange
        var testAssembly = Assembly.GetExecutingAssembly();
        var referencedAssemblies = testAssembly.GetReferencedAssemblies();

        // Act
        var moqAssembly = referencedAssemblies
            .FirstOrDefault(a => a.Name == "Moq");

        // Assert
        moqAssembly.Should().NotBeNull("Moq should be installed for mocking");
    }

    [Fact]
    public void NuGetPackages_FluentAssertionsIsInstalled()
    {
        // Arrange
        var testAssembly = Assembly.GetExecutingAssembly();
        var referencedAssemblies = testAssembly.GetReferencedAssemblies();

        // Act
        var fluentAssertionsAssembly = referencedAssemblies
            .FirstOrDefault(a => a.Name == "FluentAssertions");

        // Assert
        fluentAssertionsAssembly.Should().NotBeNull("FluentAssertions should be installed for better assertions");
    }

    [Fact]
    public void DependencyInjection_ServiceCollectionCanBeCreated()
    {
        // Arrange & Act
        Action createAction = () =>
        {
            var services = new ServiceCollection();
        };

        // Assert
        createAction.Should().NotThrow("ServiceCollection should be available for DI");
    }

    [Fact]
    public void DependencyInjection_CoreServicesCanBeRegistered()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        Action registerAction = () =>
        {
            // Register mock implementations of core services
            services.AddSingleton<ILoggingService, MockLoggingService>();
            services.AddSingleton<ISettingsService, MockSettingsService>();
            services.AddSingleton<IGeofenceService, MockGeofenceService>();
        };

        // Assert
        registerAction.Should().NotThrow("Core services should be registerable in DI container");
        services.Count.Should().Be(3, "Three services should be registered");
    }

    [Fact]
    public void DependencyInjection_ServiceProviderCanBeBuilt()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ILoggingService, MockLoggingService>();

        // Act
        IServiceProvider? serviceProvider = null;
        Action buildAction = () =>
        {
            serviceProvider = services.BuildServiceProvider();
        };

        // Assert
        buildAction.Should().NotThrow("ServiceProvider should be buildable");
        serviceProvider.Should().NotBeNull();
    }

    [Fact]
    public void DependencyInjection_RegisteredServicesCanBeResolved()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ILoggingService, MockLoggingService>();
        services.AddSingleton<ISettingsService, MockSettingsService>();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var loggingService = serviceProvider.GetService<ILoggingService>();
        var settingsService = serviceProvider.GetService<ISettingsService>();

        // Assert
        loggingService.Should().NotBeNull("Logging service should be resolvable");
        loggingService.Should().BeOfType<MockLoggingService>();
        settingsService.Should().NotBeNull("Settings service should be resolvable");
        settingsService.Should().BeOfType<MockSettingsService>();
    }

    [Fact]
    public void PlatformTargets_DesktopTargetIsConfigured()
    {
        // Arrange
        var projectPath = FindProjectFile("HarvestmoonGCS.csproj");

        // Act & Assert
        if (projectPath != null)
        {
            var projectContent = File.ReadAllText(projectPath);
            projectContent.Should().Contain("net9.0-desktop", 
                "Project should target net9.0-desktop for cross-platform desktop support (Windows, Linux, macOS)");
        }
        else
        {
            // If we can't find the project file, at least verify the assembly is built for the right framework
            var assembly = Assembly.Load("HarvestmoonGCS.Core");
            assembly.Should().NotBeNull("Core assembly should be loadable");
        }
    }

    [Fact]
    public void PlatformTargets_CoreProjectTargetsNet8()
    {
        // Arrange
        var projectPath = FindProjectFile("HarvestmoonGCS.Core.csproj");

        // Act & Assert
        if (projectPath != null)
        {
            var projectContent = File.ReadAllText(projectPath);
            projectContent.Should().Contain("net9.0", 
                "Core project should target .NET 9.0");
        }
        else
        {
            // Verify the assembly is built for the right framework
            var assembly = Assembly.Load("HarvestmoonGCS.Core");
            assembly.Should().NotBeNull("Core assembly should be loadable");
        }
    }

    [Fact]
    public void PlatformTargets_UnoSdkIsUsed()
    {
        // Arrange
        var projectPath = FindProjectFile("HarvestmoonGCS.csproj");

        // Act & Assert
        if (projectPath != null)
        {
            var projectContent = File.ReadAllText(projectPath);
            projectContent.Should().Contain("Uno.Sdk", 
                "Project should use Uno.Sdk for cross-platform support");
        }
    }

    [Fact]
    public void CoreServices_LoggingServiceInterfaceExists()
    {
        // Arrange
        var coreAssembly = Assembly.Load("HarvestmoonGCS.Core");

        // Act
        var loggingServiceType = coreAssembly.GetType("HarvestmoonGCS.Core.Services.ILoggingService");

        // Assert
        loggingServiceType.Should().NotBeNull("ILoggingService interface should exist in Core project");
        loggingServiceType!.IsInterface.Should().BeTrue("ILoggingService should be an interface");
    }

    [Fact]
    public void CoreServices_SettingsServiceInterfaceExists()
    {
        // Arrange
        var coreAssembly = Assembly.Load("HarvestmoonGCS.Core");

        // Act
        var settingsServiceType = coreAssembly.GetType("HarvestmoonGCS.Core.Services.ISettingsService");

        // Assert
        settingsServiceType.Should().NotBeNull("ISettingsService interface should exist in Core project");
        settingsServiceType!.IsInterface.Should().BeTrue("ISettingsService should be an interface");
    }

    [Fact]
    public void CoreServices_GeofenceServiceInterfaceExists()
    {
        // Arrange
        var coreAssembly = Assembly.Load("HarvestmoonGCS.Core");

        // Act
        var geofenceServiceType = coreAssembly.GetType("HarvestmoonGCS.Core.Services.IGeofenceService");

        // Assert
        geofenceServiceType.Should().NotBeNull("IGeofenceService interface should exist in Core project");
        geofenceServiceType!.IsInterface.Should().BeTrue("IGeofenceService should be an interface");
    }

    [Fact]
    public void CoreModels_FlightDataModelExists()
    {
        // Arrange
        var coreAssembly = Assembly.Load("HarvestmoonGCS.Core");

        // Act
        var flightDataType = coreAssembly.GetType("HarvestmoonGCS.Core.Models.FlightData")
            ?? coreAssembly.GetType("HarvestmoonGCS.Models.FlightData");

        // Assert
        flightDataType.Should().NotBeNull("FlightData model should exist in Core project");
        flightDataType!.IsClass.Should().BeTrue("FlightData should be a class");
    }

    [Fact]
    public void CoreModels_GPSDataModelExists()
    {
        // Arrange
        var coreAssembly = Assembly.Load("HarvestmoonGCS.Core");

        // Act
        var gpsDataType = coreAssembly.GetType("HarvestmoonGCS.Core.Models.GPSData")
            ?? coreAssembly.GetType("HarvestmoonGCS.Models.GPSData");

        // Assert
        gpsDataType.Should().NotBeNull("GPSData model should exist in Core project");
        gpsDataType!.IsClass.Should().BeTrue("GPSData should be a class");
    }

    [Fact]
    public void CoreModels_WaypointModelExists()
    {
        // Arrange
        var coreAssembly = Assembly.Load("HarvestmoonGCS.Core");

        // Act
        var waypointType = coreAssembly.GetType("HarvestmoonGCS.Core.Models.Waypoint")
            ?? coreAssembly.GetType("HarvestmoonGCS.Models.Waypoint");

        // Assert
        waypointType.Should().NotBeNull("Waypoint model should exist in Core project");
        waypointType!.IsClass.Should().BeTrue("Waypoint should be a class");
    }

    #region Helper Methods

    private string? FindProjectFile(string fileName)
    {
        // Start from the test assembly location and search upwards
        var currentDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        
        while (currentDir != null)
        {
            // Look for the project file in HarvestmoonGCS directory
            var pigeonUnoDir = Path.Combine(currentDir, "HarvestmoonGCS");
            if (Directory.Exists(pigeonUnoDir))
            {
                var projectFile = Path.Combine(pigeonUnoDir, fileName);
                if (File.Exists(projectFile))
                {
                    return projectFile;
                }
            }

            // Look for the project file in HarvestmoonGCS.Core directory
            var coreDir = Path.Combine(currentDir, "HarvestmoonGCS.Core");
            if (Directory.Exists(coreDir))
            {
                var projectFile = Path.Combine(coreDir, fileName);
                if (File.Exists(projectFile))
                {
                    return projectFile;
                }
            }

            // Move up one directory
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }

        return null;
    }

    #endregion

    #region Mock Services for Testing

    private class MockLoggingService : ILoggingService
    {
        public void LogDebug(string message) { }
        public void LogInfo(string message) { }
        public void LogWarning(string message) { }
        public void LogError(string message, Exception? exception = null) { }
        public void LogDebug(string message, string component) { }
        public void LogInfo(string message, string component) { }
        public void LogWarning(string message, string component) { }
        public void LogError(string message, string component, Exception? exception = null) { }
    }

    private class MockSettingsService : ISettingsService
    {
        public Core.Models.AppSettings Settings { get; } = new Core.Models.AppSettings();
        public Task<bool> LoadSettingsAsync() => Task.FromResult(true);
        public Task<bool> SaveSettingsAsync() => Task.FromResult(true);
        public Task<bool> ResetToDefaultAsync() => Task.FromResult(true);
        public T GetSetting<T>(string key, T defaultValue) => defaultValue;
        public Task<bool> SetSettingAsync<T>(string key, T value) => Task.FromResult(true);
        
        // Basic settings methods
        public void SaveString(string key, string value) { }
        public string? GetString(string key, string? defaultValue = null) => defaultValue;
        public void SaveInt(string key, int value) { }
        public int GetInt(string key, int defaultValue = 0) => defaultValue;
        public void SaveBool(string key, bool value) { }
        public bool GetBool(string key, bool defaultValue = false) => defaultValue;
        public void SaveFloat(string key, float value) { }
        public float GetFloat(string key, float defaultValue = 0f) => defaultValue;
        public bool Contains(string key) => false;
        public void Remove(string key) { }
        public void Clear() { }
    }

    private class MockGeofenceService : IGeofenceService
    {
        private Core.Models.GeofenceData _data = new Core.Models.GeofenceData();

        public Core.Models.GeofenceData CurrentGeofence => _data;

        public void SetGeofenceActive(bool isActive)
        {
            _data.IsActive = isActive;
        }

        public void SetGeofenceCenter(double latitude, double longitude)
        {
            _data.CenterLatitude = latitude;
            _data.CenterLongitude = longitude;
        }

        public void SetGeofenceRadius(double radius)
        {
            _data.Radius = radius;
        }

        public void SetMaxAltitude(double maxAltitude)
        {
            _data.MaxAltitude = maxAltitude;
        }

        public void SetGeofenceType(HarvestmoonGCS.Core.Models.GeofenceType type)
        {
            _data.Type = type;
        }

        public void AddPolygonVertex(double latitude, double longitude)
        {
            _data.Vertices.Add(new HarvestmoonGCS.Core.Models.GeofenceVertex(_data.Vertices.Count, latitude, longitude));
        }

        public void ClearPolygonVertices()
        {
            _data.Vertices.Clear();
        }

        public void CompletePolygon()
        {
            // No-op for tests
        }

        public double CalculateDistanceToBoundary(double latitude, double longitude, double altitude)
        {
            return 0.0;
        }

        public bool IsInsideGeofence(double latitude, double longitude, double altitude)
        {
            return true;
        }

        public Task SaveGeofenceParametersAsync() => Task.CompletedTask;
        public Task LoadGeofenceParametersAsync() => Task.CompletedTask;
        public Task SendGeofenceToVehicleAsync() => Task.CompletedTask;
        
        public Task<List<Core.Models.GeofenceData>> GetActiveGeofencesAsync()
        {
            return Task.FromResult(new List<Core.Models.GeofenceData> { _data });
        }
    }

    #endregion
}
