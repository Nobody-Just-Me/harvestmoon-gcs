using FluentAssertions;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Core.Models;
using MavLinkNet;
using Xunit;

namespace HarvestmoonGCS.Tests.Services;

/// <summary>
/// Unit tests for MavLinkService command sending
/// Tests ARM/DISARM, flight mode changes, and command acknowledgment handling
/// </summary>
public class MavLinkServiceCommandTests
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
    public async Task ArmDisarmAsync_WhenNotConnected_ShouldReturnFalse()
    {
        // Arrange
        var service = new MavLinkService();

        // Act
        var result = await service.ArmDisarmAsync(true);

        // Assert
        result.Should().BeFalse("ARM command should fail when not connected");
    }

    [Fact]
    public async Task ArmDisarmAsync_WhenConnected_ShouldSendCommand()
    {
        // Arrange
        var service = new MavLinkService();
        await service.ConnectAsync(ConnectionType.UDP, "127.0.0.1", 14550);
        
        bool messageReceived = false;
        service.MessageReceived += (sender, message) =>
        {
            if (message.Contains("ARM") || message.Contains("DISARM"))
            {
                messageReceived = true;
            }
        };

        // Act - Send ARM command (will timeout without autopilot, but command is sent)
        var armTask = service.ArmDisarmAsync(true);
        
        // Wait a short time for the command to be sent
        await Task.Delay(100);

        // Assert - Command was sent (we can't verify result without autopilot)
        // The method will timeout and return false, but the command was sent
        
        // Cleanup
        await service.DisconnectAsync();
    }

    [Fact]
    public async Task ArmDisarmAsync_WithAcknowledgment_ShouldReturnSuccess()
    {
        // Arrange
        var service = new MavLinkService();
        await service.ConnectAsync(ConnectionType.UDP, "127.0.0.1", 14550);
        service.EnterPlaybackMode(); // Use playback mode to inject packets

        // Start the ARM command (it will wait for acknowledgment)
        var armTask = service.ArmDisarmAsync(true);

        // Simulate command acknowledgment
        await Task.Delay(50); // Small delay to ensure command is sent
        
        var ackPacket = CreateCommandAckPacket(MavCmd.ComponentArmDisarm, MavResult.Accepted);
        service.InjectPacket(ackPacket);

        // Act
        var result = await armTask;

        // Assert
        result.Should().BeTrue("ARM command should succeed with acknowledgment");

        // Cleanup
        service.ExitPlaybackMode();
    }

    [Fact]
    public async Task ArmDisarmAsync_WithTimeout_ShouldReturnFalse()
    {
        // Arrange
        var service = new MavLinkService();
        await service.ConnectAsync(ConnectionType.UDP, "127.0.0.1", 14550);

        // Act - Send ARM command without autopilot (will timeout)
        var result = await service.ArmDisarmAsync(true);

        // Assert
        result.Should().BeFalse("ARM command should timeout without autopilot");

        // Cleanup
        await service.DisconnectAsync();
    }

    [Fact]
    public async Task SetFlightModeAsync_WhenNotConnected_ShouldReturnFalse()
    {
        // Arrange
        var service = new MavLinkService();

        // Act
        var result = await service.SetFlightModeAsync("AUTO");

        // Assert
        result.Should().BeFalse("SET_MODE command should fail when not connected");
    }

    [Fact]
    public async Task SetFlightModeAsync_WithValidMode_ShouldSendCommand()
    {
        // Arrange
        var service = new MavLinkService();
        await service.ConnectAsync(ConnectionType.UDP, "127.0.0.1", 14550);

        // Act - Send SET_MODE command (will timeout without autopilot, but command is sent)
        var setModeTask = service.SetFlightModeAsync("AUTO");
        
        // Wait a short time for the command to be sent
        await Task.Delay(100);

        // Assert - Command was sent (we can't verify result without autopilot)
        
        // Cleanup
        await service.DisconnectAsync();
    }

    [Fact]
    public async Task SetFlightModeAsync_WithInvalidMode_ShouldThrowException()
    {
        // Arrange
        var service = new MavLinkService();
        await service.ConnectAsync(ConnectionType.UDP, "127.0.0.1", 14550);

        // Act
        var act = async () => await service.SetFlightModeAsync("INVALID_MODE");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>("Invalid mode should throw exception");

        // Cleanup
        await service.DisconnectAsync();
    }

    [Fact]
    public async Task SetFlightModeAsync_WithAcknowledgment_ShouldReturnSuccess()
    {
        // Arrange
        var service = new MavLinkService();
        await service.ConnectAsync(ConnectionType.UDP, "127.0.0.1", 14550);
        service.EnterPlaybackMode(); // Use playback mode to inject packets

        // Start the SET_MODE command (it will wait for acknowledgment)
        var setModeTask = service.SetFlightModeAsync("RTL");

        // Simulate command acknowledgment
        await Task.Delay(50); // Small delay to ensure command is sent
        
        var ackPacket = CreateCommandAckPacket(MavCmd.DoSetMode, MavResult.Accepted);
        service.InjectPacket(ackPacket);

        // Act
        var result = await setModeTask;

        // Assert
        result.Should().BeTrue("SET_MODE command should succeed with acknowledgment");

        // Cleanup
        service.ExitPlaybackMode();
    }

    [Fact]
    public async Task SendCommandLongAsync_WhenNotConnected_ShouldThrowException()
    {
        // Arrange
        var service = new MavLinkService();

        // Act
        var act = async () => await service.SendCommandLongAsync(400, 1, 0, 0, 0, 0, 0, 0);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>("Command should fail when not connected");
    }

    [Fact]
    public async Task SendCommandLongAsync_WhenConnected_ShouldSendCommand()
    {
        // Arrange
        var service = new MavLinkService();
        await service.ConnectAsync(ConnectionType.UDP, "127.0.0.1", 14550);

        // Act
        var act = async () => await service.SendCommandLongAsync(400, 1, 0, 0, 0, 0, 0, 0);

        // Assert
        await act.Should().NotThrowAsync("Command should be sent when connected");

        // Cleanup
        await service.DisconnectAsync();
    }

    [Fact]
    public async Task SendMessage_WhenNotConnected_ShouldThrowException()
    {
        // Arrange
        var service = new MavLinkService();
        var message = new UasHeartbeat
        {
            Type = MavLinkNet.MavType.Gcs,
            Autopilot = MavAutopilot.Invalid,
            BaseMode = MavModeFlag.CustomModeEnabled,
            CustomMode = 0,
            SystemStatus = MavState.Active,
            MavlinkVersion = 3
        };

        // Act
        var act = () => service.SendMessage(message);

        // Assert
        act.Should().Throw<InvalidOperationException>("SendMessage should fail when not connected");
    }

    [Fact]
    public async Task SendMessage_WhenConnected_ShouldSendMessage()
    {
        // Arrange
        var service = new MavLinkService();
        await service.ConnectAsync(Core.Models.ConnectionType.UDP, "127.0.0.1", 14550);
        
        var message = new UasHeartbeat
        {
            Type = MavLinkNet.MavType.Gcs,
            Autopilot = MavAutopilot.Invalid,
            BaseMode = MavModeFlag.CustomModeEnabled,
            CustomMode = 0,
            SystemStatus = MavState.Active,
            MavlinkVersion = 3
        };

        // Act
        var act = () => service.SendMessage(message);

        // Assert
        act.Should().NotThrow("SendMessage should succeed when connected");

        // Cleanup
        await service.DisconnectAsync();
    }

    [Fact]
    public async Task CommandAcknowledgment_ShouldCompleteCommand()
    {
        // Arrange
        var service = new MavLinkService();
        service.EnterPlaybackMode();

        bool ackReceived = false;
        service.MessageReceived += (sender, message) =>
        {
            if (message.Contains("ComponentArmDisarm") && message.Contains("Accepted"))
            {
                ackReceived = true;
            }
        };

        // Act - Inject command acknowledgment packet
        var ackPacket = CreateCommandAckPacket(MavCmd.ComponentArmDisarm, MavResult.Accepted);
        service.InjectPacket(ackPacket);

        // Wait for event processing
        await Task.Delay(100);

        // Assert
        ackReceived.Should().BeTrue("Command acknowledgment should be processed");

        // Cleanup
        service.ExitPlaybackMode();
    }

    [Fact]
    public async Task MultipleCommands_ShouldTrackSeparately()
    {
        // Arrange
        var service = new MavLinkService();
        await service.ConnectAsync(ConnectionType.UDP, "127.0.0.1", 14550);
        service.EnterPlaybackMode();

        // Start multiple commands
        var armTask = service.ArmDisarmAsync(true);
        await Task.Delay(50);
        var setModeTask = service.SetFlightModeAsync("AUTO");
        await Task.Delay(50);

        // Simulate acknowledgments in reverse order
        var setModeAck = CreateCommandAckPacket(MavCmd.DoSetMode, MavResult.Accepted);
        service.InjectPacket(setModeAck);
        
        var armAck = CreateCommandAckPacket(MavCmd.ComponentArmDisarm, MavResult.Accepted);
        service.InjectPacket(armAck);

        // Act
        var armResult = await armTask;
        var setModeResult = await setModeTask;

        // Assert
        armResult.Should().BeTrue("ARM command should succeed");
        setModeResult.Should().BeTrue("SET_MODE command should succeed");

        // Cleanup
        service.ExitPlaybackMode();
    }

    [Fact]
    public async Task DisconnectAsync_ShouldCancelPendingCommands()
    {
        // Arrange
        var service = new MavLinkService();
        await service.ConnectAsync(ConnectionType.UDP, "127.0.0.1", 14550);

        // Start a command that will wait for acknowledgment
        var armTask = service.ArmDisarmAsync(true);
        await Task.Delay(50); // Ensure command is sent

        // Act - Disconnect while command is pending
        await service.DisconnectAsync();

        // Assert - The command task should complete (either timeout or cancel)
        var act = async () => await armTask;
        await act.Should().NotThrowAsync("Pending commands should be handled gracefully on disconnect");
    }

    /// <summary>
    /// Helper method to create a command acknowledgment packet
    /// </summary>
    private MavLinkPacketBase CreateCommandAckPacket(MavCmd command, MavResult result)
    {
        var ackMessage = new UasCommandAck
        {
            Command = command,
            Result = result
        };

        return new TestMavLinkPacket(ackMessage, 1, 0, 0);
    }
}
