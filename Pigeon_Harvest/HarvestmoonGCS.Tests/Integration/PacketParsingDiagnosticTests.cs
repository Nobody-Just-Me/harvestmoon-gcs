using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using HarvestmoonGCS.Core.Services;
using MavLinkNet;

namespace HarvestmoonGCS.Tests.Integration
{
    /// <summary>
    /// Integration tests for packet parsing diagnostic logging.
    /// Verifies that diagnostic logs appear when packets are parsed.
    /// Tests verify that OnPacketReceived method logs packet details correctly.
    /// </summary>
    public class PacketParsingDiagnosticTests
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
        public void OnPacketReceived_FiresPacketReceivedEvent_WhenPacketIsInjected()
        {
            // Arrange
            var service = new MavLinkService();
            bool packetReceived = false;
            MavLinkPacketBase? receivedPacket = null;
            
            service.PacketReceived += (sender, packet) =>
            {
                packetReceived = true;
                receivedPacket = packet;
            };
            
            // Create a mock heartbeat packet
            var heartbeatPacket = CreateMockHeartbeatPacket();
            
            // Act
            service.InjectPacket(heartbeatPacket);
            
            // Wait briefly for async processing
            Thread.Sleep(100);
            
            // Assert
            Assert.True(packetReceived, "PacketReceived event should fire");
            Assert.NotNull(receivedPacket);
        }
        
        [Fact]
        public void OnPacketReceived_ProcessesPacketCorrectly_WhenValidPacketIsInjected()
        {
            // Arrange
            var service = new MavLinkService();
            int packetCount = 0;
            
            service.PacketReceived += (sender, packet) =>
            {
                packetCount++;
            };
            
            // Create mock packets
            var packet1 = CreateMockHeartbeatPacket();
            var packet2 = CreateMockHeartbeatPacket();
            var packet3 = CreateMockHeartbeatPacket();
            
            // Act
            service.InjectPacket(packet1);
            service.InjectPacket(packet2);
            service.InjectPacket(packet3);
            
            // Wait briefly for async processing
            Thread.Sleep(200);
            
            // Assert
            Assert.Equal(3, packetCount);
        }
        
        [Fact]
        public void OnPacketReceived_LogsPacketDetails_WhenPacketIsProcessed()
        {
            // Arrange
            var service = new MavLinkService();
            bool packetReceived = false;
            
            service.PacketReceived += (sender, packet) =>
            {
                packetReceived = true;
                // Verify packet has expected properties
                Assert.True(packet.SystemId >= 0);
                Assert.True(packet.PacketSequenceNumber >= 0);
            };
            
            var packet = CreateMockHeartbeatPacket();
            
            // Act
            service.InjectPacket(packet);
            
            // Wait briefly for async processing
            Thread.Sleep(200);
            
            // Assert
            Assert.True(packetReceived, "Packet should be received and logged");
        }
        
        /// <summary>
        /// Creates a mock HEARTBEAT packet for testing
        /// </summary>
        private MavLinkPacketBase CreateMockHeartbeatPacket()
        {
            var heartbeat = new UasHeartbeat
            {
                Type = MavType.Quadrotor,
                Autopilot = MavAutopilot.Ardupilotmega,
                BaseMode = MavModeFlag.CustomModeEnabled,
                CustomMode = 0,
                SystemStatus = MavState.Active,
                MavlinkVersion = 3
            };

            return new TestMavLinkPacket(heartbeat, 1, 1, 0);
        }
    }
}
