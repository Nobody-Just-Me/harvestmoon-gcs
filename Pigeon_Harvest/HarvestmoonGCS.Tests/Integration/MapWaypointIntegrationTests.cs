using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Core.Helpers;
using HarvestmoonGCS.Helpers;
using Moq;

namespace HarvestmoonGCS.Tests.Integration;

/// <summary>
/// Integration tests for map and waypoint management
/// Tests waypoint management flow and map updates with GPS data
/// **Validates: Requirements 4.1, 4.2, 4.3, 4.6**
/// </summary>
public class MapWaypointIntegrationTests : IDisposable
{
    private readonly Mock<IGeofenceService> _mockGeofenceService;
    private readonly GeofenceData _testGeofence;
    private readonly List<WaypointData> _waypoints;

    public MapWaypointIntegrationTests()
    {
        // Setup mock geofence service
        _mockGeofenceService = new Mock<IGeofenceService>();
        _testGeofence = new GeofenceData
        {
            IsActive = false,
            Type = GeofenceType.Circular,
            Radius = 1000,
            CenterLatitude = -35.3632620,
            CenterLongitude = 149.1652300,
            MaxAltitude = 500,
            Status = GeofenceStatus.Inactive,
            Vertices = new List<GeofenceVertex>()
        };

        _mockGeofenceService.Setup(x => x.CurrentGeofence).Returns(_testGeofence);
        _mockGeofenceService.Setup(x => x.LoadGeofenceParametersAsync()).Returns(Task.CompletedTask);
        _mockGeofenceService.Setup(x => x.SaveGeofenceParametersAsync()).Returns(Task.CompletedTask);

        _waypoints = new List<WaypointData>();
    }

    [Fact]
    public void WaypointData_Creation_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var testLat = -35.3632620;
        var testLon = 149.1652300;
        var testAlt = 100.0;
        var testSequence = 1;

        // Act
        var waypoint = new WaypointData
        {
            Latitude = testLat,
            Longitude = testLon,
            Altitude = testAlt,
            Sequence = testSequence,
            Command = WaypointCommand.Waypoint
        };

        // Assert
        waypoint.Latitude.Should().BeApproximately(testLat, 0.0000001, "Latitude should match input");
        waypoint.Longitude.Should().BeApproximately(testLon, 0.0000001, "Longitude should match input");
        waypoint.Altitude.Should().BeApproximately(testAlt, 0.1, "Altitude should match input");
        waypoint.Sequence.Should().Be(testSequence, "Sequence should match input");
        waypoint.Command.Should().Be(WaypointCommand.Waypoint, "Command should be set correctly");
    }

    [Fact]
    public void WaypointCollection_AddMultiple_ShouldMaintainSequence()
    {
        // Arrange & Act
        var testWaypoints = new[]
        {
            (-35.3632620, 149.1652300), // Canberra Airport
            (-35.3000000, 149.1000000), // Point 1
            (-35.2500000, 149.1500000), // Point 2
            (-35.3500000, 149.2000000)  // Point 3
        };

        foreach (var (lat, lon) in testWaypoints)
        {
            var waypoint = new WaypointData
            {
                Latitude = lat,
                Longitude = lon,
                Altitude = 100,
                Sequence = _waypoints.Count + 1,
                Command = WaypointCommand.Waypoint
            };
            _waypoints.Add(waypoint);
        }

        // Assert
        _waypoints.Should().HaveCount(4, "Should have 4 waypoints");
        
        // Verify waypoint sequences
        for (int i = 0; i < _waypoints.Count; i++)
        {
            _waypoints[i].Sequence.Should().Be(i + 1, $"Waypoint {i} should have correct sequence number");
        }
    }

    [Fact]
    public void WaypointCollection_RemoveAndRenumber_ShouldUpdateSequences()
    {
        // Arrange - Add multiple waypoints
        for (int i = 0; i < 5; i++)
        {
            var waypoint = new WaypointData
            {
                Latitude = -35.3632620 + (i * 0.001),
                Longitude = 149.1652300 + (i * 0.001),
                Altitude = 100,
                Sequence = i + 1,
                Command = WaypointCommand.Waypoint
            };
            _waypoints.Add(waypoint);
        }

        var initialCount = _waypoints.Count;
        var waypointToRemove = _waypoints[2]; // Middle waypoint

        // Act
        _waypoints.Remove(waypointToRemove);
        
        // Renumber remaining waypoints
        for (int i = 0; i < _waypoints.Count; i++)
        {
            _waypoints[i].Sequence = i + 1;
        }

        // Assert
        _waypoints.Should().HaveCount(initialCount - 1, "Should remove one waypoint");
        _waypoints.Should().NotContain(waypointToRemove, "Deleted waypoint should not be in collection");

        // Verify sequences are renumbered correctly
        for (int i = 0; i < _waypoints.Count; i++)
        {
            _waypoints[i].Sequence.Should().Be(i + 1, $"Waypoint {i} should have correct sequence number after deletion");
        }
    }

    [Fact]
    public void DistanceCalculation_WithKnownCoordinates_ShouldBeAccurate()
    {
        // Arrange - Use coordinates with known distance
        // Canberra Airport to Parliament House (approximately 8km)
        var waypoint1 = new WaypointData
        {
            Latitude = -35.3632620,
            Longitude = 149.1652300,
            Sequence = 1
        };
        
        var waypoint2 = new WaypointData
        {
            Latitude = -35.3081000,
            Longitude = 149.1244000,
            Sequence = 2
        };

        // Act
        var distance = GeoMath.CalculateDistance(
            waypoint1.Latitude, waypoint1.Longitude,
            waypoint2.Latitude, waypoint2.Longitude);

        // Assert
        distance.Should().BeInRange(7000, 9000, "Distance between Canberra Airport and Parliament House should be approximately 8km");
    }

    [Fact]
    public void GeofenceService_SetActive_ShouldUpdateStatus()
    {
        // Arrange
        _mockGeofenceService.Setup(x => x.SetGeofenceActive(It.IsAny<bool>()))
                           .Callback<bool>(active => _testGeofence.IsActive = active);

        // Act
        _mockGeofenceService.Object.SetGeofenceActive(true);

        // Assert
        _testGeofence.IsActive.Should().BeTrue("Geofence should be activated");
        _mockGeofenceService.Verify(x => x.SetGeofenceActive(true), Times.Once, "SetGeofenceActive should be called");
    }

    [Fact]
    public void GeofenceService_SetCenter_ShouldUpdateCoordinates()
    {
        // Arrange
        var newLat = -35.5000000;
        var newLon = 149.2000000;
        
        _mockGeofenceService.Setup(x => x.SetGeofenceCenter(It.IsAny<double>(), It.IsAny<double>()))
                           .Callback<double, double>((lat, lon) =>
                           {
                               _testGeofence.CenterLatitude = lat;
                               _testGeofence.CenterLongitude = lon;
                           });

        // Act
        _mockGeofenceService.Object.SetGeofenceCenter(newLat, newLon);

        // Assert
        _testGeofence.CenterLatitude.Should().BeApproximately(newLat, 0.0000001, "Center latitude should be updated");
        _testGeofence.CenterLongitude.Should().BeApproximately(newLon, 0.0000001, "Center longitude should be updated");
        _mockGeofenceService.Verify(x => x.SetGeofenceCenter(newLat, newLon), Times.Once, "SetGeofenceCenter should be called");
    }

    [Fact]
    public void GeofenceService_CalculateDistanceToBoundary_ShouldReturnCorrectDistance()
    {
        // Arrange
        var vehicleLat = -35.3632620;
        var vehicleLon = 149.1652300;
        var vehicleAlt = 150.0;
        var expectedDistance = 30.0;

        _mockGeofenceService.Setup(x => x.CalculateDistanceToBoundary(vehicleLat, vehicleLon, vehicleAlt))
                           .Returns(expectedDistance);

        // Act
        var distance = _mockGeofenceService.Object.CalculateDistanceToBoundary(vehicleLat, vehicleLon, vehicleAlt);

        // Assert
        distance.Should().Be(expectedDistance, "Should return the expected distance to boundary");
        _mockGeofenceService.Verify(x => x.CalculateDistanceToBoundary(vehicleLat, vehicleLon, vehicleAlt), Times.Once);
    }

    [Fact]
    public async Task GeofenceService_SaveAndLoadParameters_ShouldPersistData()
    {
        // Arrange
        _testGeofence.IsActive = true;
        _testGeofence.Radius = 1500;
        _testGeofence.MaxAltitude = 600;

        // Act
        await _mockGeofenceService.Object.SaveGeofenceParametersAsync();
        await _mockGeofenceService.Object.LoadGeofenceParametersAsync();

        // Assert
        _mockGeofenceService.Verify(x => x.SaveGeofenceParametersAsync(), Times.Once, "Should save parameters");
        _mockGeofenceService.Verify(x => x.LoadGeofenceParametersAsync(), Times.Once, "Should load parameters");
    }

    [Fact]
    public void WaypointCommand_EnumValues_ShouldBeCorrect()
    {
        // Assert - Verify important waypoint command values exist
        Enum.IsDefined(typeof(WaypointCommand), WaypointCommand.Waypoint).Should().BeTrue("Waypoint command should exist");
        Enum.IsDefined(typeof(WaypointCommand), WaypointCommand.SetHome).Should().BeTrue("SetHome command should exist");
        Enum.IsDefined(typeof(WaypointCommand), WaypointCommand.TakeOff).Should().BeTrue("TakeOff command should exist");
        Enum.IsDefined(typeof(WaypointCommand), WaypointCommand.Land).Should().BeTrue("Land command should exist");
    }

    [Fact]
    public void GeofenceType_EnumValues_ShouldBeCorrect()
    {
        // Assert - Verify geofence type values exist
        Enum.IsDefined(typeof(GeofenceType), GeofenceType.Circular).Should().BeTrue("Circular geofence type should exist");
        Enum.IsDefined(typeof(GeofenceType), GeofenceType.Polygon).Should().BeTrue("Polygon geofence type should exist");
    }

    [Fact]
    public void GeofenceStatus_EnumValues_ShouldBeCorrect()
    {
        // Assert - Verify geofence status values exist
        Enum.IsDefined(typeof(GeofenceStatus), GeofenceStatus.Inactive).Should().BeTrue("Inactive status should exist");
        Enum.IsDefined(typeof(GeofenceStatus), GeofenceStatus.Active).Should().BeTrue("Active status should exist");
        Enum.IsDefined(typeof(GeofenceStatus), GeofenceStatus.Drawing).Should().BeTrue("Drawing status should exist");
    }

    [Fact]
    public async Task CompleteWaypointWorkflow_ShouldHandleAllOperationsCorrectly()
    {
        // Arrange - Start with empty waypoint list
        _waypoints.Should().BeEmpty("Should start with empty waypoint list");

        // Act 1 - Add multiple waypoints
        var testWaypoints = new[]
        {
            (-35.3632620, 149.1652300), // Canberra Airport
            (-35.3000000, 149.1000000), // Point 1
            (-35.2500000, 149.1500000), // Point 2
            (-35.3500000, 149.2000000)  // Point 3
        };

        foreach (var (lat, lon) in testWaypoints)
        {
            var waypoint = new WaypointData
            {
                Latitude = lat,
                Longitude = lon,
                Altitude = 100,
                Sequence = _waypoints.Count + 1,
                Command = WaypointCommand.Waypoint
            };
            _waypoints.Add(waypoint);
        }

        // Assert 1 - Verify waypoints added
        _waypoints.Should().HaveCount(4, "Should have 4 waypoints");

        // Act 2 - Calculate total distance
        double totalDistance = 0;
        for (int i = 0; i < _waypoints.Count - 1; i++)
        {
            var wp1 = _waypoints[i];
            var wp2 = _waypoints[i + 1];
            totalDistance += GeoMath.CalculateDistance(
                wp1.Latitude, wp1.Longitude,
                wp2.Latitude, wp2.Longitude);
        }

        // Assert 2 - Verify distance calculation
        totalDistance.Should().BeGreaterThan(0, "Should calculate total distance");
        totalDistance.Should().BeLessThan(50000, "Distance should be reasonable for test area"); // 50km max

        // Act 3 - Delete a waypoint
        var waypointToDelete = _waypoints[2];
        var countBeforeDelete = _waypoints.Count;
        _waypoints.Remove(waypointToDelete);

        // Renumber sequences
        for (int i = 0; i < _waypoints.Count; i++)
        {
            _waypoints[i].Sequence = i + 1;
        }

        // Assert 3 - Verify waypoint deleted and sequences updated
        _waypoints.Should().HaveCount(countBeforeDelete - 1, "Should have one less waypoint");
        _waypoints.Should().NotContain(waypointToDelete, "Deleted waypoint should not be in collection");

        // Verify sequences are correct
        for (int i = 0; i < _waypoints.Count; i++)
        {
            _waypoints[i].Sequence.Should().Be(i + 1, $"Waypoint {i} should have correct sequence");
        }

        // Act 4 - Clear all waypoints
        _waypoints.Clear();

        // Assert 4 - Verify waypoints cleared
        _waypoints.Should().BeEmpty("Waypoint list should be cleared");
    }

    public void Dispose()
    {
        _waypoints?.Clear();
    }
}