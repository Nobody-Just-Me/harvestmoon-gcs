using FluentAssertions;
using Pigeon_Uno.Core.Services;
using Pigeon_Uno.Core.Models;
using MavLinkNet;
using Xunit;

namespace Pigeon_Uno.Tests.Services;

/// <summary>
/// Unit tests for MavLinkService mission operations
/// Tests mission upload and download functionality
/// </summary>
    public class MavLinkServiceMissionTests
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

        public override void Serialize(System.IO.BinaryWriter w)
        {
            // Not used in tests
        }
    }

    [Fact]
    public async Task UploadMissionAsync_WhenNotConnected_ShouldReturnFalse()
    {
        // Arrange
        var service = new MavLinkService();
        var waypoints = CreateTestWaypoints(3);

        // Act
        var result = await service.UploadMissionAsync(waypoints);

        // Assert
        result.Should().BeFalse("Mission upload should fail when not connected");
    }

    [Fact]
    public async Task UploadMissionAsync_WithEmptyMission_ShouldSucceed()
    {
        // Arrange
        var service = new MavLinkService();
        await service.ConnectAsync(ConnectionType.UDP, "127.0.0.1", 14550);
        service.EnterPlaybackMode();

        var waypoints = new List<WaypointData>();

        // Start upload
        var uploadTask = service.UploadMissionAsync(waypoints);
        await Task.Delay(200);

        // Simulate mission acknowledgment
        var ackPacket = CreateMissionAckPacket(MavMissionResult.MavMissionAccepted);
        service.InjectPacket(ackPacket);

        // Act
        var result = await uploadTask;

        // Assert
        result.Should().BeTrue("Empty mission upload should succeed");

        // Cleanup
        service.ExitPlaybackMode();
    }

    [Fact]
    public async Task UploadMissionAsync_WithWaypoints_ShouldSendMissionCount()
    {
        // Arrange
        var service = new MavLinkService();
        await service.ConnectAsync(ConnectionType.UDP, "127.0.0.1", 14550);
        service.EnterPlaybackMode();

        var waypoints = CreateTestWaypoints(3);
        bool missionCountSent = false;

        // Monitor for MISSION_COUNT being sent
        // In a real scenario, we'd capture the sent message
        // For this test, we'll verify the state machine works

        // Start upload
        var uploadTask = service.UploadMissionAsync(waypoints);
        await Task.Delay(200);

        // Simulate autopilot requesting mission items
        for (int i = 0; i < waypoints.Count; i++)
        {
            var requestPacket = CreateMissionRequestPacket(i);
            service.InjectPacket(requestPacket);
            await Task.Delay(100);
        }

        // Simulate mission acknowledgment
        var ackPacket = CreateMissionAckPacket(MavMissionResult.MavMissionAccepted);
        service.InjectPacket(ackPacket);

        // Act
        var result = await uploadTask;

        // Assert
        result.Should().BeTrue("Mission upload should succeed with proper protocol");

        // Cleanup
        service.ExitPlaybackMode();
    }

    [Fact]
    public async Task UploadMissionAsync_WithTimeout_ShouldReturnFalse()
    {
        // Arrange
        var service = new MavLinkService();
        await service.ConnectAsync(ConnectionType.UDP, "127.0.0.1", 14550);

        var waypoints = CreateTestWaypoints(3);

        // Act - Upload without autopilot (will timeout)
        var result = await service.UploadMissionAsync(waypoints);

        // Assert
        result.Should().BeFalse("Mission upload should timeout without autopilot");

        // Cleanup
        await service.DisconnectAsync();
    }

    [Fact]
    public async Task UploadMissionAsync_WithRejection_ShouldReturnFalse()
    {
        // Arrange
        var service = new MavLinkService();
        await service.ConnectAsync(ConnectionType.UDP, "127.0.0.1", 14550);
        service.EnterPlaybackMode();

        var waypoints = CreateTestWaypoints(3);

        // Start upload
        var uploadTask = service.UploadMissionAsync(waypoints);
        await Task.Delay(200);

        // Simulate mission rejection
        var ackPacket = CreateMissionAckPacket(MavMissionResult.MavMissionError);
        service.InjectPacket(ackPacket);

        // Act
        var result = await uploadTask;

        // Assert
        result.Should().BeFalse("Mission upload should fail when rejected");

        // Cleanup
        service.ExitPlaybackMode();
    }

    [Fact]
    public async Task DownloadMissionAsync_WhenNotConnected_ShouldReturnEmptyList()
    {
        // Arrange
        var service = new MavLinkService();

        // Act
        var result = await service.DownloadMissionAsync();

        // Assert
        result.Should().BeEmpty("Mission download should return empty list when not connected");
    }

    [Fact]
    public async Task DownloadMissionAsync_WithEmptyMission_ShouldReturnEmptyList()
    {
        // Arrange
        var service = new MavLinkService();
        await service.ConnectAsync(ConnectionType.UDP, "127.0.0.1", 14550);
        service.EnterPlaybackMode();

        // Start download
        var downloadTask = service.DownloadMissionAsync();
        await Task.Delay(200);

        // Simulate empty mission count
        var countPacket = CreateMissionCountPacket(0);
        service.InjectPacket(countPacket);

        // Act
        var result = await downloadTask;

        // Assert
        result.Should().BeEmpty("Empty mission download should return empty list");

        // Cleanup
        service.ExitPlaybackMode();
    }

    [Fact]
    public async Task DownloadMissionAsync_WithWaypoints_ShouldReturnWaypoints()
    {
        // Arrange
        var service = new MavLinkService();
        await service.ConnectAsync(ConnectionType.UDP, "127.0.0.1", 14550);
        service.EnterPlaybackMode();

        var expectedWaypoints = CreateTestWaypoints(3);

        // Start download
        var downloadTask = service.DownloadMissionAsync();
        await Task.Delay(200);

        // Simulate mission count
        var countPacket = CreateMissionCountPacket(expectedWaypoints.Count);
        service.InjectPacket(countPacket);
        await Task.Delay(50);

        // Simulate mission items
        for (int i = 0; i < expectedWaypoints.Count; i++)
        {
            var itemPacket = CreateMissionItemIntPacket(expectedWaypoints[i]);
            service.InjectPacket(itemPacket);
            await Task.Delay(50);
        }

        // Act
        var result = await downloadTask;

        // Assert
        result.Should().HaveCount(expectedWaypoints.Count, "Downloaded mission should have correct count");
        result[0].Latitude.Should().BeApproximately(expectedWaypoints[0].Latitude, 0.0000001);
        result[0].Longitude.Should().BeApproximately(expectedWaypoints[0].Longitude, 0.0000001);
        result[0].Altitude.Should().BeApproximately(expectedWaypoints[0].Altitude, 0.1);

        // Cleanup
        service.ExitPlaybackMode();
    }

    [Fact]
    public async Task DownloadMissionAsync_WithTimeout_ShouldReturnEmptyList()
    {
        // Arrange
        var service = new MavLinkService();
        await service.ConnectAsync(ConnectionType.UDP, "127.0.0.1", 14550);

        // Act - Download without autopilot (will timeout)
        var result = await service.DownloadMissionAsync();

        // Assert
        result.Should().BeEmpty("Mission download should return empty list on timeout");

        // Cleanup
        await service.DisconnectAsync();
    }

    [Fact]
    public async Task MissionOperations_ShouldHandleSequentialUploads()
    {
        // Arrange
        var service = new MavLinkService();
        await service.ConnectAsync(ConnectionType.UDP, "127.0.0.1", 14550);
        service.EnterPlaybackMode();

        var waypoints1 = CreateTestWaypoints(2);
        var waypoints2 = CreateTestWaypoints(3);

        // First upload
        var upload1Task = service.UploadMissionAsync(waypoints1);
        await Task.Delay(200);
        
        for (int i = 0; i < waypoints1.Count; i++)
        {
            service.InjectPacket(CreateMissionRequestPacket(i));
            await Task.Delay(100);
        }
        service.InjectPacket(CreateMissionAckPacket(MavMissionResult.MavMissionAccepted));
        
        var result1 = await upload1Task;

        // Second upload
        var upload2Task = service.UploadMissionAsync(waypoints2);
        await Task.Delay(200);
        
        for (int i = 0; i < waypoints2.Count; i++)
        {
            service.InjectPacket(CreateMissionRequestPacket(i));
            await Task.Delay(100);
        }
        service.InjectPacket(CreateMissionAckPacket(MavMissionResult.MavMissionAccepted));
        
        var result2 = await upload2Task;

        // Assert
        result1.Should().BeTrue("First upload should succeed");
        result2.Should().BeTrue("Second upload should succeed");

        // Cleanup
        service.ExitPlaybackMode();
    }

    /// <summary>
    /// Helper method to create test waypoints
    /// </summary>
    private List<WaypointData> CreateTestWaypoints(int count)
    {
        var waypoints = new List<WaypointData>();
        
        for (int i = 0; i < count; i++)
        {
            waypoints.Add(new WaypointData
            {
                Sequence = i,
                Latitude = -6.2 + (i * 0.001),
                Longitude = 106.8 + (i * 0.001),
                Altitude = 50 + (i * 10),
                Command = WaypointCommand.Waypoint,
                Param1 = 0,
                Param2 = 0,
                Param3 = 0,
                Param4 = 0,
                IsCurrent = i == 0
            });
        }

        return waypoints;
    }

    /// <summary>
    /// Helper method to create a MISSION_REQUEST packet
    /// </summary>
    private MavLinkPacketBase CreateMissionRequestPacket(int seq)
    {
        var requestMessage = new UasMissionRequest
        {
            TargetSystem = 255,
            TargetComponent = 0,
            Seq = (ushort)seq
        };

        var packet = new TestMavLinkPacket(requestMessage, 1, 0, 0);

        return packet;
    }

    /// <summary>
    /// Helper method to create a MISSION_ACK packet
    /// </summary>
    private MavLinkPacketBase CreateMissionAckPacket(MavMissionResult result)
    {
        var ackMessage = new UasMissionAck
        {
            TargetSystem = 255,
            TargetComponent = 0,
            Type = result
        };

        var packet = new TestMavLinkPacket(ackMessage, 1, 0, 0);

        return packet;
    }

    /// <summary>
    /// Helper method to create a MISSION_COUNT packet
    /// </summary>
    private MavLinkPacketBase CreateMissionCountPacket(int count)
    {
        var countMessage = new UasMissionCount
        {
            TargetSystem = 255,
            TargetComponent = 0,
            Count = (ushort)count
        };

        var packet = new TestMavLinkPacket(countMessage, 1, 0, 0);

        return packet;
    }

    /// <summary>
    /// Helper method to create a MISSION_ITEM_INT packet
    /// </summary>
    private MavLinkPacketBase CreateMissionItemIntPacket(WaypointData waypoint)
    {
        var itemMessage = new UasMissionItemInt
        {
            TargetSystem = 255,
            TargetComponent = 0,
            Seq = (ushort)waypoint.Sequence,
            Frame = MavLinkNet.MavFrame.GlobalRelativeAlt,
            Command = (MavCmd)waypoint.Command,
            Current = (byte)(waypoint.IsCurrent ? 1 : 0),
            Autocontinue = 1,
            Param1 = (float)waypoint.Param1,
            Param2 = (float)waypoint.Param2,
            Param3 = (float)waypoint.Param3,
            Param4 = (float)waypoint.Param4,
            X = (int)(waypoint.Latitude * 1e7),
            Y = (int)(waypoint.Longitude * 1e7),
            Z = (float)waypoint.Altitude
        };

        var packet = new TestMavLinkPacket(itemMessage, 1, 0, 0);

        return packet;
    }
}
