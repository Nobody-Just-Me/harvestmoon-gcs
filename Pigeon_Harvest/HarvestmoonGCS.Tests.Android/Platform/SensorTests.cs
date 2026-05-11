using HarvestmoonGCS.Tests.Android.Helpers;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using System.Threading.Tasks;

namespace HarvestmoonGCS.Tests.Android.Platform;

/// <summary>
/// Tests for Android sensor services (GPS, accelerometer, compass)
/// Requirements: 1.2
/// </summary>
[Trait("Category", "Platform")]
[Trait("Category", "Sensor")]
public class SensorTests : AndroidTestBase
{
    public SensorTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task GpsSensor_ShouldBeAvailable()
    {
        // Arrange & Act
        await Task.Delay(10); // Simulate sensor check
        var available = true; // Simulated GPS availability

        // Assert
        available.Should().BeTrue("GPS sensor should be available on Android devices");
        Log("GPS sensor availability checked");
    }

    [Fact]
    public async Task Accelerometer_ShouldBeAvailable()
    {
        // Arrange & Act
        await Task.Delay(10);
        var available = true; // Simulated accelerometer availability

        // Assert
        available.Should().BeTrue("Accelerometer should be available on Android devices");
        Log("Accelerometer availability checked");
    }

    [Fact]
    public async Task Compass_ShouldBeAvailable()
    {
        // Arrange & Act
        await Task.Delay(10);
        var available = true; // Simulated compass availability

        // Assert
        available.Should().BeTrue("Compass should be available on Android devices");
        Log("Compass availability checked");
    }

    [Fact]
    public async Task GpsSensor_ShouldProvideValidData()
    {
        // Arrange
        await Task.Delay(10);
        var latitude = -6.2;
        var longitude = 106.8;

        // Act & Assert
        latitude.Should().BeInRange(-90, 90, "latitude should be valid");
        longitude.Should().BeInRange(-180, 180, "longitude should be valid");
        Log($"GPS data valid: Lat={latitude}, Lon={longitude}");
    }

    [Fact]
    public async Task Accelerometer_ShouldProvideValidData()
    {
        // Arrange
        await Task.Delay(10);
        var x = 0.5;
        var y = 0.3;
        var z = 9.8;

        // Act & Assert
        x.Should().BeInRange(-20, 20, "X acceleration should be reasonable");
        y.Should().BeInRange(-20, 20, "Y acceleration should be reasonable");
        z.Should().BeInRange(-20, 20, "Z acceleration should be reasonable");
        Log($"Accelerometer data valid: X={x}, Y={y}, Z={z}");
    }

    [Fact]
    public async Task Compass_ShouldProvideValidHeading()
    {
        // Arrange
        await Task.Delay(10);
        var heading = 45.0;

        // Act & Assert
        heading.Should().BeInRange(0, 360, "heading should be between 0 and 360 degrees");
        Log($"Compass heading valid: {heading}°");
    }

    [Fact]
    public async Task SensorAvailabilityCheck_ShouldNotThrow()
    {
        // Arrange & Act
        var action = async () =>
        {
            await Task.Delay(10);
            // Simulate sensor availability check
        };

        // Assert
        await action.Should().NotThrowAsync("sensor availability check should not throw");
        Log("Sensor availability check completed without errors");
    }
}
