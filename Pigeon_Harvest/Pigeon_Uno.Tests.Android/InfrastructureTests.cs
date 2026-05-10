using Pigeon_Uno.Tests.Android.Helpers;
using Xunit;
using Xunit.Abstractions;
using System.Linq;
using FsCheck;

namespace Pigeon_Uno.Tests.Android;

/// <summary>
/// Tests to verify the Android test infrastructure is set up correctly
/// </summary>
[Trait("Category", "Infrastructure")]
public class InfrastructureTests : AndroidTestBase
{
    public InfrastructureTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void Configuration_ShouldBeValid()
    {
        // Arrange & Act
        var isValid = Config.IsValid();

        // Assert
        Assert.True(isValid, "Android test configuration should be valid");
        Assert.NotEmpty(Config.DeviceId);
        Assert.True(Config.ApiLevel >= 21, "API level should be at least 21 (Android 5.0)");
        Log($"Configuration valid: DeviceId={Config.DeviceId}, ApiLevel={Config.ApiLevel}");
    }

    [Fact]
    public void PerformanceMonitor_ShouldTrackMemory()
    {
        // Arrange
        PerformanceMonitor.Start();

        // Act
        var snapshot1 = PerformanceMonitor.GetSnapshot();
        var _ = new byte[1024 * 1024]; // Allocate 1MB
        var snapshot2 = PerformanceMonitor.GetSnapshot();

        PerformanceMonitor.Stop();

        // Assert
        Assert.True(snapshot2.MemoryUsage >= snapshot1.MemoryUsage, "Memory usage should increase after allocation");
        Log($"Memory tracking works: {snapshot1.MemoryUsage / 1024 / 1024}MB -> {snapshot2.MemoryUsage / 1024 / 1024}MB");
    }

    [Fact]
    public void MockDroneSimulator_ShouldConnect()
    {
        // Arrange
        using var simulator = new MockDroneSimulator();

        // Act
        var connected = simulator.ConnectAsync().Result;

        // Assert
        Assert.True(connected, "Mock drone should connect successfully");
        Assert.True(simulator.IsConnected, "Simulator should report connected state");
        Log("Mock drone simulator connected successfully");
    }

    [Fact]
    public void MockDroneSimulator_ShouldGenerateTelemetry()
    {
        // Arrange
        using var simulator = new MockDroneSimulator();
        simulator.ConnectAsync().Wait();

        // Act
        var telemetry = simulator.GenerateTelemetry();

        // Assert
        Assert.NotNull(telemetry);
        Assert.InRange(telemetry.Latitude, -90, 90);
        Assert.InRange(telemetry.Longitude, -180, 180);
        Assert.InRange(telemetry.Altitude, 0, double.MaxValue);
        Assert.InRange(telemetry.BatteryPercent, 0, 100);
        Log($"Generated telemetry: Lat={telemetry.Latitude:F6}, Lon={telemetry.Longitude:F6}, Alt={telemetry.Altitude:F1}m");
    }

    [Fact]
    public void TestDataGenerator_ShouldGenerateValidWaypoints()
    {
        // Arrange & Act
        var waypoint = Gen.Sample(0, 1, TestDataGenerator.WaypointGenerator()).First();

        // Assert
        Assert.NotNull(waypoint);
        Assert.InRange(waypoint.Latitude, -90, 90);
        Assert.InRange(waypoint.Longitude, -180, 180);
        Assert.InRange(waypoint.Altitude, 0, double.MaxValue);
        Log($"Generated waypoint: Lat={waypoint.Latitude:F6}, Lon={waypoint.Longitude:F6}, Alt={waypoint.Altitude:F1}m");
    }

    [Fact]
    public void TestDataGenerator_ShouldGenerateValidMissions()
    {
        // Arrange & Act
        var mission = Gen.Sample(0, 1, TestDataGenerator.MissionGenerator(3, 10)).First();

        // Assert
        Assert.NotNull(mission);
        Assert.InRange(mission.Count, 3, 10);
        Assert.All(mission, wp =>
        {
            Assert.InRange(wp.Latitude, -90, 90);
            Assert.InRange(wp.Longitude, -180, 180);
        });
        Log($"Generated mission with {mission.Count} waypoints");
    }

    [Fact]
    public void AndroidTestHelper_ShouldCaptureMemoryUsage()
    {
        // Arrange & Act
        var memoryUsage = AndroidTestHelper.GetCurrentMemoryUsage();

        // Assert
        Assert.True(memoryUsage > 0, "Memory usage should be greater than zero");
        Log($"Current memory usage: {memoryUsage / 1024 / 1024}MB");
    }

    [Fact]
    public void PerformanceThresholds_ShouldBeConfigured()
    {
        // Arrange & Act
        var maxHeapMB = Config.GetThreshold("MaxHeapMemoryMB", 0);
        var maxCpuPercent = Config.GetThreshold("MaxUIThreadCpuPercent", 0.0);
        var maxStartupSeconds = Config.GetThreshold("MaxStartupTimeSeconds", 0.0);

        // Assert
        Assert.True(maxHeapMB > 0, "Max heap memory threshold should be configured");
        Assert.True(maxCpuPercent > 0, "Max CPU threshold should be configured");
        Assert.True(maxStartupSeconds > 0, "Max startup time threshold should be configured");
        Log($"Thresholds: Memory={maxHeapMB}MB, CPU={maxCpuPercent}%, Startup={maxStartupSeconds}s");
    }
}
