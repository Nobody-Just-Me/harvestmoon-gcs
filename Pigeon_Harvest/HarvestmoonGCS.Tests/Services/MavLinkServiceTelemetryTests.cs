using FluentAssertions;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Models;
using MavLinkNet;
using Xunit;

namespace HarvestmoonGCS.Tests.Services;

/// <summary>
/// Unit tests for MavLinkService telemetry parsing
/// Tests parsing of MAVLink messages and updating FlightData
/// </summary>
public class MavLinkServiceTelemetryTests
{
    /// <summary>
    /// Helper class to create test packets
    /// </summary>
    private class TestMavLinkPacket : MavLinkPacketBase
    {
        public TestMavLinkPacket(UasMessage message, byte systemId, byte componentId, byte sequenceNumber)
        {
            Message = message;
            SystemId = systemId;
            ComponentId = componentId;
            PacketSequenceNumber = sequenceNumber;
            IsValid = true;
        }

        public override int GetPacketSize()
        {
            return 0; // Not used in tests
        }

        public override void Serialize(BinaryWriter w)
        {
            // Not used in tests
        }
    }

    [Fact]
    public void TelemetryReceived_WhenHeartbeatReceived_ShouldUpdateVehicleType()
    {
        // Arrange
        var service = new MavLinkService();
        service.EnterPlaybackMode();
        
        FlightData? receivedData = null;
        service.TelemetryReceived += (sender, data) =>
        {
            receivedData = data;
        };

        // Create a HEARTBEAT message for a quadrotor
        var heartbeat = new UasHeartbeat
        {
            Type = MavLinkNet.MavType.Quadrotor,
            Autopilot = MavAutopilot.Ardupilotmega,
            BaseMode = MavModeFlag.CustomModeEnabled,
            CustomMode = 0,
            SystemStatus = MavState.Active,
            MavlinkVersion = 3
        };

        // Create a MAVLink packet
        var packet = new TestMavLinkPacket(heartbeat, 1, 1, 0);

        // Act
        service.InjectPacket(packet);

        // Assert
        receivedData.Should().NotBeNull();
        receivedData!.Tipe.Should().Be(TipeDevice.WAHANA);
        receivedData.Type.Should().Be(1); // Copter category
    }

    [Fact]
    public void TelemetryReceived_WhenFixedWingHeartbeat_ShouldReturnType1()
    {
        // Arrange
        var service = new MavLinkService();
        service.EnterPlaybackMode();
        
        FlightData? receivedData = null;
        service.TelemetryReceived += (sender, data) => receivedData = data;

        // Create a HEARTBEAT message for a fixed wing aircraft
        var heartbeat = new UasHeartbeat
        {
            Type = MavLinkNet.MavType.FixedWing,  // FixedWing = 1 (Plane)
            Autopilot = MavAutopilot.Ardupilotmega,
            BaseMode = MavModeFlag.CustomModeEnabled,
            CustomMode = 0,
            SystemStatus = MavState.Active,
            MavlinkVersion = 3
        };

        var packet = new TestMavLinkPacket(heartbeat, 1, 1, 0);

        // Act
        service.InjectPacket(packet);

        // Assert
        receivedData.Should().NotBeNull();
        receivedData!.Type.Should().Be(2); // Fixed wing category
    }

    [Fact]
    public void TelemetryReceived_WhenHexarotorHeartbeat_ShouldReturnType13()
    {
        // Arrange
        var service = new MavLinkService();
        service.EnterPlaybackMode();
        
        FlightData? receivedData = null;
        service.TelemetryReceived += (sender, data) => receivedData = data;

        // Create a HEARTBEAT message for a hexarotor (6-motor copter)
        var heartbeat = new UasHeartbeat
        {
            Type = MavLinkNet.MavType.Hexarotor,  // Hexarotor = 13 (Copter type)
            Autopilot = MavAutopilot.Ardupilotmega,
            BaseMode = MavModeFlag.CustomModeEnabled,
            CustomMode = 0,
            SystemStatus = MavState.Active,
            MavlinkVersion = 3
        };

        var packet = new TestMavLinkPacket(heartbeat, 1, 1, 0);

        // Act
        service.InjectPacket(packet);

        // Assert
        receivedData.Should().NotBeNull();
        receivedData!.Type.Should().Be(1); // Copter category
    }

    [Fact]
    public void TelemetryReceived_WhenGenericHeartbeat_ShouldDefaultToFixedWing()
    {
        // Arrange
        var service = new MavLinkService();
        service.EnterPlaybackMode();
        
        FlightData? receivedData = null;
        service.TelemetryReceived += (sender, data) => receivedData = data;

        // Create a HEARTBEAT message with Generic type (should default to FixedWing)
        var heartbeat = new UasHeartbeat
        {
            Type = MavLinkNet.MavType.Generic,  // Generic = 0, should default to FixedWing (1)
            Autopilot = MavAutopilot.Ardupilotmega,
            BaseMode = MavModeFlag.CustomModeEnabled,
            CustomMode = 0,
            SystemStatus = MavState.Active,
            MavlinkVersion = 3
        };

        var packet = new TestMavLinkPacket(heartbeat, 1, 1, 0);

        // Act
        service.InjectPacket(packet);

        // Assert
        receivedData.Should().NotBeNull();
        receivedData!.Type.Should().Be(1); // Generic (0) should default to FixedWing (1)
    }

    [Fact]
    public void TelemetryReceived_WhenAttitudeReceived_ShouldUpdateIMU()
    {
        // Arrange
        var service = new MavLinkService();
        service.EnterPlaybackMode();
        
        FlightData? receivedData = null;
        service.TelemetryReceived += (sender, data) =>
        {
            receivedData = data;
        };

        // Create an ATTITUDE message
        var attitude = new UasAttitude
        {
            TimeBootMs = 1000,
            Roll = 0.1f,  // radians
            Pitch = 0.2f, // radians
            Yaw = 1.57f,  // radians (approximately 90 degrees)
            Rollspeed = 0.01f,
            Pitchspeed = 0.02f,
            Yawspeed = 0.03f
        };

        var packet = new TestMavLinkPacket(attitude, 1, 1, 0);

        // Act
        service.InjectPacket(packet);

        // Assert
        receivedData.Should().NotBeNull();
        receivedData!.IMU.Roll.Should().BeApproximately(5.73f, 0.1f); // 0.1 rad ≈ 5.73°
        receivedData.IMU.Pitch.Should().BeApproximately(11.46f, 0.1f); // 0.2 rad ≈ 11.46°
        receivedData.IMU.Yaw.Should().BeApproximately(90.0f, 1.0f); // 1.57 rad ≈ 90°
    }

    [Fact]
    public void TelemetryReceived_WhenGpsRawIntReceived_ShouldUpdateGPS()
    {
        // Arrange
        var service = new MavLinkService();
        service.EnterPlaybackMode();
        
        FlightData? receivedData = null;
        service.TelemetryReceived += (sender, data) =>
        {
            receivedData = data;
        };

        // Create a GPS_RAW_INT message
        var gpsRaw = new UasGpsRawInt
        {
            TimeUsec = 1000000,
            FixType = GpsFixType._3dFix,
            Lat = -62000000, // -6.2 degrees (in 1E7 format)
            Lon = 1068000000, // 106.8 degrees (in 1E7 format)
            Alt = 100000, // 100 meters in mm
            Eph = 150, // HDOP * 100
            Epv = 200,
            Vel = 500,
            Cog = 18000,
            SatellitesVisible = 12
        };

        var packet = new TestMavLinkPacket(gpsRaw, 1, 1, 0);

        // Act
        service.InjectPacket(packet);

        // Assert
        receivedData.Should().NotBeNull();
        receivedData!.GPS.Sats.Should().Be(12);
        receivedData.GPS.Hdop.Should().BeApproximately(1.5f, 0.01f); // 150 / 100
    }

    [Fact]
    public void TelemetryReceived_WhenVfrHudReceived_ShouldUpdateSpeedAndThrottle()
    {
        // Arrange
        var service = new MavLinkService();
        service.EnterPlaybackMode();
        
        FlightData? receivedData = null;
        service.TelemetryReceived += (sender, data) =>
        {
            receivedData = data;
        };

        // Create a VFR_HUD message
        var vfrHud = new UasVfrHud
        {
            Airspeed = 15.5f,
            Groundspeed = 14.2f,
            Heading = 90,
            Throttle = 75,
            Alt = 50.0f,
            Climb = 2.5f
        };

        var packet = new TestMavLinkPacket(vfrHud, 1, 1, 0);

        // Act
        service.InjectPacket(packet);

        // Assert
        receivedData.Should().NotBeNull();
        receivedData!.Speed.Should().Be(14.2f);
        receivedData.ThrottlePercent.Should().Be(75);
    }

    [Fact]
    public void TelemetryReceived_WhenSysStatusReceived_ShouldUpdateBatteryAndSignal()
    {
        // Arrange
        var service = new MavLinkService();
        service.EnterPlaybackMode();
        
        FlightData? receivedData = null;
        service.TelemetryReceived += (sender, data) =>
        {
            receivedData = data;
        };

        // Create a SYS_STATUS message
        var sysStatus = new UasSysStatus
        {
            OnboardControlSensorsPresent = 0,
            OnboardControlSensorsEnabled = 0,
            OnboardControlSensorsHealth = 0,
            Load = 500,
            VoltageBattery = 12600, // 12.6V in millivolts
            CurrentBattery = 1500, // 15A in centiamps
            BatteryRemaining = 75,
            DropRateComm = 100, // 1% packet loss
            ErrorsComm = 0,
            ErrorsCount1 = 0,
            ErrorsCount2 = 0,
            ErrorsCount3 = 0,
            ErrorsCount4 = 0
        };

        var packet = new TestMavLinkPacket(sysStatus, 1, 1, 0);

        // Act
        service.InjectPacket(packet);

        // Assert
        receivedData.Should().NotBeNull();
        receivedData!.MavlinkMiliVolt.Should().Be(12600);
        receivedData.MavlinkCentiAmp.Should().Be(1500);
        receivedData.Signal.Should().Be(99); // 100 - (100 / 100) = 99
    }

    [Fact]
    public void TelemetryReceived_WhenBatteryStatusReceived_ShouldUpdateBatteryVoltageAndCurrent()
    {
        // Arrange
        var service = new MavLinkService();
        service.EnterPlaybackMode();
        
        FlightData? receivedData = null;
        service.TelemetryReceived += (sender, data) =>
        {
            receivedData = data;
        };

        // Create a BATTERY_STATUS message
        var batteryStatus = new UasBatteryStatus
        {
            Id = 0,
            BatteryFunction = MavBatteryFunction.All,
            Type = MavBatteryType.Lipo,
            Temperature = 350, // 35°C
            Voltages = new ushort[] { 4200, 4180, 4190, 4210, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue }, // 4S battery
            CurrentBattery = 1500, // 15A in centiamps
            CurrentConsumed = 2500, // 2.5Ah in mAh
            EnergyConsumed = -1,
            BatteryRemaining = 75
        };

        var packet = new TestMavLinkPacket(batteryStatus, 1, 1, 0);

        // Act
        service.InjectPacket(packet);

        // Assert
        receivedData.Should().NotBeNull();
        receivedData!.BatteryVolt.Should().Be(16780); // 4200 + 4180 + 4190 + 4210
        receivedData.BatteryCurr.Should().Be(1500);
    }

    [Fact]
    public void TelemetryReceived_WhenBatteryStatusWithNegativeCurrent_ShouldUseAbsoluteValue()
    {
        // Arrange
        var service = new MavLinkService();
        service.EnterPlaybackMode();
        
        FlightData? receivedData = null;
        service.TelemetryReceived += (sender, data) =>
        {
            receivedData = data;
        };

        // Create a BATTERY_STATUS message with negative current (charging)
        var batteryStatus = new UasBatteryStatus
        {
            Id = 0,
            BatteryFunction = MavBatteryFunction.All,
            Type = MavBatteryType.Lipo,
            Temperature = 350,
            Voltages = new ushort[] { 4200, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue },
            CurrentBattery = -500, // Charging at 5A
            CurrentConsumed = 0,
            EnergyConsumed = -1,
            BatteryRemaining = 100
        };

        var packet = new TestMavLinkPacket(batteryStatus, 1, 1, 0);

        // Act
        service.InjectPacket(packet);

        // Assert
        receivedData.Should().NotBeNull();
        receivedData!.BatteryCurr.Should().Be(500); // Absolute value
    }

    [Fact]
    public void HeartbeatReceived_WhenHeartbeatReceived_ShouldEmitEvent()
    {
        // Arrange
        var service = new MavLinkService();
        service.EnterPlaybackMode();
        
        bool eventRaised = false;
        service.HeartbeatReceived += (sender, args) =>
        {
            eventRaised = true;
        };

        var heartbeat = new UasHeartbeat
        {
            Type = MavLinkNet.MavType.Quadrotor,
            Autopilot = MavAutopilot.Ardupilotmega,
            BaseMode = MavModeFlag.CustomModeEnabled,
            CustomMode = 0,
            SystemStatus = MavState.Active,
            MavlinkVersion = 3
        };

        var packet = new TestMavLinkPacket(heartbeat, 1, 1, 0);

        // Act
        service.InjectPacket(packet);

        // Assert
        eventRaised.Should().BeTrue();
    }
}
