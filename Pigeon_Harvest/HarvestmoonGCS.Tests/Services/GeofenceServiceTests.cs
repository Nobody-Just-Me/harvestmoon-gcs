using FluentAssertions;
using MavLinkNet;
using Moq;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Helpers;
using Xunit;

namespace HarvestmoonGCS.Tests.Services;

public class GeofenceServiceTests
{
    [Fact]
    public async Task SendGeofenceToVehicleAsync_CircularGeofence_ShouldUploadFenceParameters()
    {
        var mavLink = CreateConnectedMavLinkMock();
        var service = new GeofenceService(Mock.Of<ISettingsService>(), mavLink.Object);

        service.SetGeofenceCenter(-6.2, 106.8);
        service.SetGeofenceRadius(750);
        service.SetMaxAltitude(120);
        service.SetGeofenceActive(true);

        await service.SendGeofenceToVehicleAsync();

        mavLink.Verify(m => m.SetParameterAsync("FENCE_ENABLE", 0), Times.Once);
        mavLink.Verify(m => m.SetParameterAsync("FENCE_TYPE", 3), Times.Once);
        mavLink.Verify(m => m.SetParameterAsync("FENCE_RADIUS", 750), Times.Once);
        mavLink.Verify(m => m.SetParameterAsync("FENCE_ALT_MAX", 120), Times.Once);
        mavLink.Verify(m => m.SetParameterAsync("FENCE_ACTION", 1), Times.Once);
        mavLink.Verify(m => m.SetParameterAsync("FENCE_ENABLE", 1), Times.Once);
        mavLink.Verify(m => m.SendMessage(It.IsAny<UasMessage>()), Times.Never);
    }

    [Fact]
    public async Task SendGeofenceToVehicleAsync_PolygonGeofence_ShouldUploadFencePoints()
    {
        var mavLink = CreateConnectedMavLinkMock();
        var service = new GeofenceService(Mock.Of<ISettingsService>(), mavLink.Object);

        service.SetGeofenceType(GeofenceType.Polygon);
        service.AddPolygonVertex(-6.2000, 106.8000);
        service.AddPolygonVertex(-6.2000, 106.8100);
        service.AddPolygonVertex(-6.2100, 106.8050);
        service.SetMaxAltitude(150);
        service.CompletePolygon();

        await service.SendGeofenceToVehicleAsync();

        mavLink.Verify(m => m.SetParameterAsync("FENCE_TYPE", 5), Times.Once);
        mavLink.Verify(m => m.SetParameterAsync("FENCE_TOTAL", 3), Times.Once);
        mavLink.Verify(m => m.SetParameterAsync("FENCE_ALT_MAX", 150), Times.Once);
        mavLink.Verify(m => m.SetParameterAsync("FENCE_ENABLE", 1), Times.Once);
        mavLink.Verify(m => m.SendMessage(It.Is<UasFencePoint>(p => p.Idx >= 1 && p.Idx <= 3 && p.Count == 3)), Times.Exactly(3));
    }

    [Fact]
    public async Task SendGeofenceToVehicleAsync_InactiveGeofence_ShouldDisableVehicleFenceOnly()
    {
        var mavLink = CreateConnectedMavLinkMock();
        var service = new GeofenceService(Mock.Of<ISettingsService>(), mavLink.Object);

        await service.SendGeofenceToVehicleAsync();

        mavLink.Verify(m => m.SetParameterAsync("FENCE_ENABLE", 0), Times.Once);
        mavLink.Verify(m => m.SetParameterAsync(It.Is<string>(name => name != "FENCE_ENABLE"), It.IsAny<float>()), Times.Never);
        mavLink.Verify(m => m.SendMessage(It.IsAny<UasMessage>()), Times.Never);
    }

    [Fact]
    public async Task SendGeofenceToVehicleAsync_WhenDisconnected_ShouldThrowClearError()
    {
        var mavLink = new Mock<IMavLinkService>();
        var service = new GeofenceService(Mock.Of<ISettingsService>(), mavLink.Object);
        service.SetGeofenceActive(true);

        var act = () => service.SendGeofenceToVehicleAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*MAVLink is not connected*");
    }

    [Fact]
    public void SetGeofenceActive_ShouldKeepCompatibilityIsEnabledInSync()
    {
        var service = new GeofenceService(Mock.Of<ISettingsService>());

        service.SetGeofenceActive(true);
        service.CurrentGeofence.IsActive.Should().BeTrue();
        service.CurrentGeofence.IsEnabled.Should().BeTrue();

        service.SetGeofenceActive(false);
        service.CurrentGeofence.IsActive.Should().BeFalse();
        service.CurrentGeofence.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void CalculateDistanceToBoundary_CircularSimulation_ShouldDetectInsideOutsideAndAltitudeBreach()
    {
        var service = new GeofenceService(Mock.Of<ISettingsService>());
        service.SetGeofenceCenter(-7.2754, 112.7947);
        service.SetGeofenceRadius(120);
        service.SetMaxAltitude(150);
        service.SetGeofenceActive(true);

        var inside = service.CalculateDistanceToBoundary(-7.27545, 112.79475, 80);
        var outside = service.CalculateDistanceToBoundary(-7.2754, 112.7974, 80);
        var altitudeBreach = service.CalculateDistanceToBoundary(-7.27545, 112.79475, 175);

        inside.Should().BeGreaterThan(100);
        outside.Should().BeLessThan(0);
        altitudeBreach.Should().BeApproximately(-25, 0.1);
    }

    [Fact]
    public void CalculateDistanceToBoundary_PolygonSimulation_ShouldUseRequestedGeofence()
    {
        var service = new GeofenceService(Mock.Of<ISettingsService>());
        var polygon = new GeofenceData
        {
            Type = GeofenceType.Polygon,
            IsActive = true,
            IsEnabled = true,
            MaxAltitude = 120,
            Vertices =
            {
                new GeofenceVertex(1, -7.2760, 112.7940),
                new GeofenceVertex(2, -7.2760, 112.7960),
                new GeofenceVertex(3, -7.2740, 112.7960),
                new GeofenceVertex(4, -7.2740, 112.7940)
            }
        };

        var inside = service.CalculateDistanceToBoundary(polygon, -7.2750, 112.7950, 80);
        var outside = service.CalculateDistanceToBoundary(polygon, -7.2750, 112.7980, 80);

        inside.Should().BeGreaterThan(0);
        outside.Should().BeLessThan(0);
    }

    [Fact]
    public void MissionProgressCalculator_SimulatedMission_ShouldReportRealWaypointProgress()
    {
        var waypoints = new[]
        {
            new WaypointData { Sequence = 1, Latitude = -7.2754, Longitude = 112.7947, Altitude = 80 },
            new WaypointData { Sequence = 2, Latitude = -7.2754, Longitude = 112.7957, Altitude = 80 },
            new WaypointData { Sequence = 3, Latitude = -7.2754, Longitude = 112.7967, Altitude = 80 }
        };
        var telemetry = new TelemetryData
        {
            Latitude = -7.2754,
            Longitude = 112.7957,
            Altitude = 80,
            SatelliteCount = 12,
            HDOP = 0.8
        };

        var progress = MissionProgressCalculator.Calculate(telemetry, waypoints, waypointRadiusMeters: 5);

        progress.TotalWaypoints.Should().Be(3);
        progress.CurrentWaypoint.Should().Be(2);
        progress.ProgressPercent.Should().BeApproximately(66.7, 0.2);
        progress.RemainingDistanceMeters.Should().BeGreaterThan(100);
    }

    private static Mock<IMavLinkService> CreateConnectedMavLinkMock()
    {
        var mavLink = new Mock<IMavLinkService>();
        mavLink.SetupGet(m => m.IsConnected).Returns(true);
        mavLink.Setup(m => m.SetParameterAsync(It.IsAny<string>(), It.IsAny<float>()))
            .ReturnsAsync(true);
        return mavLink;
    }
}
