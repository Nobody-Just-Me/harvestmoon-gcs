using FluentAssertions;
using Pigeon_Uno.ViewModels;
using Pigeon_Uno.Core.Services;
using Pigeon_Uno.Core.Models;
using Pigeon_Uno.Models;
using Pigeon_Uno.Services;
using Xunit;
using Moq;
using System;

namespace Pigeon_Uno.Tests;

public class TrackerViewModelTests
{
    private readonly Mock<IMavLinkService> _mockMavLinkService;
    private readonly Mock<IDispatcherService> _mockDispatcherService;
    private readonly TrackerViewModel _viewModel;

    public TrackerViewModelTests()
    {
        _mockMavLinkService = new Mock<IMavLinkService>();
        _mockDispatcherService = new Mock<IDispatcherService>();
        
        // Setup dispatcher to execute actions immediately for testing
        _mockDispatcherService
            .Setup(d => d.Enqueue(It.IsAny<Action>()))
            .Callback<Action>(action => action());

        _viewModel = new TrackerViewModel(_mockMavLinkService.Object, _mockDispatcherService.Object);
    }

    [Fact]
    public void CalculateTracking_ShouldCalculateCorrectBearing_WhenVehicleIsNorth()
    {
        // Arrange: Tracker at origin, vehicle 1 degree north
        _viewModel.TrackerLat = 0;
        _viewModel.TrackerLon = 0;
        _viewModel.TrackerAlt = 0;

        // Act: Simulate telemetry with vehicle 1 degree north
        var telemetry = CreateTelemetry(latitude: 10000000, longitude: 0, altitude: 0);
        RaiseTelemetryReceived(telemetry);

        // Assert: Bearing should be approximately 0 degrees (North)
        _viewModel.Bearing.Should().BeApproximately(0, 0.1);
    }

    [Fact]
    public void CalculateTracking_ShouldCalculateCorrectBearing_WhenVehicleIsEast()
    {
        // Arrange: Tracker at origin, vehicle 1 degree east
        _viewModel.TrackerLat = 0;
        _viewModel.TrackerLon = 0;
        _viewModel.TrackerAlt = 0;

        // Act: Simulate telemetry with vehicle 1 degree east
        var telemetry = CreateTelemetry(latitude: 0, longitude: 10000000, altitude: 0);
        RaiseTelemetryReceived(telemetry);

        // Assert: Bearing should be approximately 90 degrees (East)
        _viewModel.Bearing.Should().BeApproximately(90, 0.1);
    }

    [Fact]
    public void CalculateTracking_ShouldCalculateCorrectDistance()
    {
        // Arrange: Tracker at origin
        _viewModel.TrackerLat = 0;
        _viewModel.TrackerLon = 0;
        _viewModel.TrackerAlt = 0;

        // Act: Simulate telemetry with vehicle 1 degree north (approximately 111km)
        var telemetry = CreateTelemetry(latitude: 10000000, longitude: 0, altitude: 0);
        RaiseTelemetryReceived(telemetry);

        // Assert: Distance should be approximately 111km (111194 meters)
        _viewModel.Distance.Should().BeApproximately(111194, 200);
    }

    [Fact]
    public void CalculateTracking_ShouldCalculateCorrectPitch_WhenVehicleIsAbove()
    {
        // Arrange: Tracker at sea level, vehicle nearby but 100m higher
        _viewModel.TrackerLat = 0;
        _viewModel.TrackerLon = 0;
        _viewModel.TrackerAlt = 0;

        // Act: Simulate telemetry with vehicle very close horizontally (~10m) but 100m altitude
        // Using approximately 0.00009 degrees latitude (≈10m horizontal) and 100m altitude
        var telemetry = CreateTelemetry(latitude: 900, longitude: 0, altitude: 100000); // ~10m horizontal, 100m vertical
        RaiseTelemetryReceived(telemetry);

        // Assert: Pitch should be steep (greater than 80 degrees)
        _viewModel.Pitch.Should().BeGreaterThan(80);
    }

    [Fact]
    public void CalculateTracking_ShouldCalculate45DegreePitch_WhenDistanceEqualsAltitudeDifference()
    {
        // Arrange: Tracker at origin, sea level
        _viewModel.TrackerLat = 0;
        _viewModel.TrackerLon = 0;
        _viewModel.TrackerAlt = 0;

        // Act: Simulate telemetry where horizontal distance ≈ altitude difference
        // Using approximately 0.0009 degrees latitude (≈100m) and 100m altitude
        var telemetry = CreateTelemetry(latitude: 9000, longitude: 0, altitude: 100000); // ~100m horizontal, 100m vertical
        RaiseTelemetryReceived(telemetry);

        // Assert: Pitch should be approximately 45 degrees
        _viewModel.Pitch.Should().BeApproximately(45, 1);
    }

    [Fact]
    public void CalculateTracking_ShouldCalculateNegativePitch_WhenVehicleIsBelow()
    {
        // Arrange: Tracker at 1000m altitude
        _viewModel.TrackerLat = 0;
        _viewModel.TrackerLon = 0;
        _viewModel.TrackerAlt = 1000;

        // Act: Simulate telemetry with vehicle at sea level, some distance away
        var telemetry = CreateTelemetry(latitude: 9000, longitude: 0, altitude: 0); // ~100m horizontal, -1000m vertical
        RaiseTelemetryReceived(telemetry);

        // Assert: Pitch should be negative (vehicle below tracker)
        _viewModel.Pitch.Should().BeLessThan(0);
    }

    [Fact]
    public void CalculateTracking_ShouldUpdateInRealTime_WhenTelemetryReceived()
    {
        // Arrange
        _viewModel.TrackerLat = 0;
        _viewModel.TrackerLon = 0;
        _viewModel.TrackerAlt = 0;

        // Act: Send first telemetry
        var telemetry1 = CreateTelemetry(latitude: 10000000, longitude: 0, altitude: 0);
        RaiseTelemetryReceived(telemetry1);
        var bearing1 = _viewModel.Bearing;

        // Act: Send second telemetry with different position
        var telemetry2 = CreateTelemetry(latitude: 0, longitude: 10000000, altitude: 0);
        RaiseTelemetryReceived(telemetry2);
        var bearing2 = _viewModel.Bearing;

        // Assert: Bearing should have changed
        bearing1.Should().NotBe(bearing2);
        bearing1.Should().BeApproximately(0, 0.1); // North
        bearing2.Should().BeApproximately(90, 0.1); // East
    }

    [Fact]
    public void ToggleTracking_ShouldChangeTrackingState()
    {
        // Arrange
        var initialState = _viewModel.IsTracking;

        // Act
        _viewModel.ToggleTrackingCommand.Execute(null);

        // Assert
        _viewModel.IsTracking.Should().Be(!initialState);
    }

    [Fact]
    public void SetHome_ShouldSetTrackerPositionToVehiclePosition()
    {
        // Arrange: Set vehicle position via telemetry
        var telemetry = CreateTelemetry(latitude: 10000000, longitude: 20000000, altitude: 100000);
        RaiseTelemetryReceived(telemetry);

        // Act
        _viewModel.SetHomeCommand.Execute(null);

        // Assert
        _viewModel.TrackerLat.Should().Be(1.0);
        _viewModel.TrackerLon.Should().Be(2.0);
        _viewModel.TrackerAlt.Should().Be(100.0);
    }

    [Fact]
    public void TrackingIcon_ShouldChangeBasedOnTrackingState()
    {
        // Arrange
        _viewModel.IsTracking = false;

        // Assert: Not tracking
        _viewModel.TrackingIcon.Should().Be("▶");
        _viewModel.TrackingButtonText.Should().Be("Start Tracking");

        // Act: Start tracking
        _viewModel.IsTracking = true;

        // Assert: Tracking
        _viewModel.TrackingIcon.Should().Be("⏸");
        _viewModel.TrackingButtonText.Should().Be("Stop Tracking");
    }

    // Helper methods
    private FlightData CreateTelemetry(int latitude, int longitude, int altitude)
    {
        return new FlightData
        {
            GPS = new GPSData
            {
                Latitude = latitude,
                Longitude = longitude
            },
            Altitude = altitude
        };
    }

    private void RaiseTelemetryReceived(FlightData data)
    {
        _mockMavLinkService.Raise(m => m.TelemetryReceived += null, _mockMavLinkService.Object, data);
    }
}
