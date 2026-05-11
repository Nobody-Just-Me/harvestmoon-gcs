using Xunit;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Core.Services.AI;

namespace HarvestmoonGCS.Tests.Services.AI;

public class TelemetrySnapshotMapperTests
{
    [Fact]
    public void ToSnapshot_WithValidData_MapsAllFields()
    {
        var data = new TelemetryData
        {
            Timestamp = new DateTime(2026, 4, 18, 10, 0, 0),
            BatteryVoltage = 12.5,
            BatteryPercentage = 85.0,
            Latitude = -6.12345,
            Longitude = 106.78901,
            Altitude = 100.5,
            SatelliteCount = 10,
            HDOP = 1.2,
            Barometers = 98.7,
            Speed = 15.3,
            VerticalSpeed = 2.5,
            Heading = 180.0,
            Roll = 5.0,
            Pitch = 3.0,
            Yaw = 90.0,
            FlightMode = FlightMode.AUTO,
            IsArmed = true
        };

        var snapshot = data.ToSnapshot();

        Assert.Equal(data.Timestamp, snapshot.Timestamp);
        Assert.Equal(data.BatteryVoltage, snapshot.BatteryVoltage);
        Assert.Equal(data.BatteryPercent, snapshot.BatteryPercent);
        Assert.Equal(data.Latitude, snapshot.GpsLatitude);
        Assert.Equal(data.Longitude, snapshot.GpsLongitude);
        Assert.Equal(data.Altitude, snapshot.GpsAltitude);
        Assert.Equal(data.SatelliteCount, snapshot.GpsSatellites);
        Assert.Equal(data.HDOP, snapshot.GpsHdop);
        Assert.Equal(data.Barometers, snapshot.Altitude);
        Assert.Equal(data.Speed, snapshot.Speed);
        Assert.Equal(data.VerticalSpeed, snapshot.VerticalSpeed);
        Assert.Equal(data.Heading, snapshot.Heading);
        Assert.Equal(data.Roll, snapshot.Roll);
        Assert.Equal(data.Pitch, snapshot.Pitch);
        Assert.Equal(data.Yaw, snapshot.Yaw);
        Assert.Equal(data.FlightMode, snapshot.FlightMode);
        Assert.Equal(data.IsArmed, snapshot.Armed);
    }

    [Fact]
    public void ToSnapshot_WithNullData_ReturnsEmptySnapshot()
    {
        TelemetryData? data = null;

        var snapshot = data.ToSnapshot();

        Assert.NotNull(snapshot);
        Assert.Equal(default, snapshot.Timestamp);
        Assert.Equal(0, snapshot.BatteryVoltage);
    }

    [Fact]
    public void ToSnapshot_WithPreviousSnapshot_ComputesBatteryDrainRate()
    {
        var previousData = new TelemetryData
        {
            Timestamp = new DateTime(2026, 4, 18, 10, 0, 0),
            BatteryPercentage = 90.0,
            IsArmed = true
        };
        var currentData = new TelemetryData
        {
            Timestamp = new DateTime(2026, 4, 18, 10, 0, 10),
            BatteryPercentage = 85.0,
            IsArmed = true
        };

        var previousSnapshot = previousData.ToSnapshot();
        var currentSnapshot = currentData.ToSnapshot(previousSnapshot);

        Assert.NotNull(currentSnapshot.BatteryDrainRate);
        Assert.Equal(0.5, currentSnapshot.BatteryDrainRate.Value);
    }

    [Fact]
    public void ToSnapshot_WithSameTimestamp_HasNoBatteryDrainRate()
    {
        var previousData = new TelemetryData
        {
            Timestamp = new DateTime(2026, 4, 18, 10, 0, 0),
            BatteryPercentage = 90.0,
            IsArmed = true
        };
        var currentData = new TelemetryData
        {
            Timestamp = new DateTime(2026, 4, 18, 10, 0, 0),
            BatteryPercentage = 85.0,
            IsArmed = true
        };

        var previousSnapshot = previousData.ToSnapshot();
        var currentSnapshot = currentData.ToSnapshot(previousSnapshot);

        Assert.Null(currentSnapshot.BatteryDrainRate);
    }

    [Fact]
    public void ToSnapshot_WithNoPreviousSnapshot_HasNoBatteryDrainRate()
    {
        var data = new TelemetryData
        {
            Timestamp = new DateTime(2026, 4, 18, 10, 0, 0),
            BatteryPercentage = 85.0,
            IsArmed = true
        };

        var snapshot = data.ToSnapshot();

        Assert.Null(snapshot.BatteryDrainRate);
    }

    [Fact]
    public void ToSnapshot_WithDisarmedState_MapsArmedFalse()
    {
        var data = new TelemetryData
        {
            Timestamp = new DateTime(2026, 4, 18, 10, 0, 0),
            IsArmed = false,
            FlightMode = FlightMode.DISARMED
        };

        var snapshot = data.ToSnapshot();

        Assert.False(snapshot.Armed);
        Assert.Equal(FlightMode.DISARMED, snapshot.FlightMode);
    }
}