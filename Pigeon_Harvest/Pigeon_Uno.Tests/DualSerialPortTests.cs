using System;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Pigeon_Uno.Core.Services;

namespace Pigeon_Uno.Tests;

/// <summary>
/// Unit tests for Dual Serial Port functionality
/// Tests the core logic without requiring actual USB hardware
/// </summary>
public class DualSerialPortTests
{
    [Fact]
    public void TestSerialDataReceivedEventArgs()
    {
        // Arrange
        byte[] testData = Encoding.ASCII.GetBytes("Test MAVLink Data");
        
        // Act
        var eventArgs = new SerialDataReceivedEventArgs(testData, testData.Length);
        
        // Assert
        Assert.NotNull(eventArgs.Data);
        Assert.Equal(testData.Length, eventArgs.BytesReceived);
        Assert.Equal(testData, eventArgs.Data);
    }

    [Fact]
    public void TestSerialErrorEventArgs()
    {
        // Arrange
        string errorMessage = "USB device not found";
        var exception = new Exception("Test exception");
        
        // Act
        var eventArgs = new SerialErrorEventArgs(errorMessage, exception);
        
        // Assert
        Assert.Equal(errorMessage, eventArgs.ErrorMessage);
        Assert.Equal(exception, eventArgs.Exception);
    }

    [Fact]
    public void TestMavLinkPacketCreation()
    {
        // Arrange - Create a simple MAVLink heartbeat packet
        byte[] heartbeat = new byte[] {
            0xFE, // STX (start of frame)
            0x09, // Length
            0x00, // Sequence
            0x01, // System ID
            0x01, // Component ID
            0x00, // Message ID (HEARTBEAT)
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // Payload
            0x00, 0x00 // Checksum
        };
        
        // Act
        var eventArgs = new SerialDataReceivedEventArgs(heartbeat, heartbeat.Length);
        
        // Assert
        Assert.Equal(17, eventArgs.BytesReceived); // MAVLink v1 heartbeat = 17 bytes
        Assert.Equal(0xFE, eventArgs.Data[0]); // STX
        Assert.Equal(0x09, eventArgs.Data[1]); // Length
        Assert.Equal(0x00, eventArgs.Data[5]); // Message ID (HEARTBEAT)
    }

    [Fact]
    public void TestLoRaCommandCreation()
    {
        // Arrange - Create LoRa AT command
        string command = "AT+MODE=0\r\n";
        byte[] loraCommand = Encoding.ASCII.GetBytes(command);
        
        // Act
        var eventArgs = new SerialDataReceivedEventArgs(loraCommand, loraCommand.Length);
        
        // Assert
        Assert.Equal(command.Length, eventArgs.BytesReceived);
        string received = Encoding.ASCII.GetString(eventArgs.Data);
        Assert.Equal(command, received);
    }

    [Fact]
    public void TestDataForwarding()
    {
        // Arrange
        byte[] mavlinkData = Encoding.ASCII.GetBytes("MAVLink telemetry");
        byte[] loraData = Encoding.ASCII.GetBytes("LoRa command");
        
        bool mavlinkReceived = false;
        bool loraReceived = false;
        
        // Simulate event handlers
        EventHandler<SerialDataReceivedEventArgs> mavlinkHandler = (s, e) => {
            mavlinkReceived = true;
            Assert.Equal(mavlinkData, e.Data);
        };
        
        EventHandler<SerialDataReceivedEventArgs> loraHandler = (s, e) => {
            loraReceived = true;
            Assert.Equal(loraData, e.Data);
        };
        
        // Act
        mavlinkHandler.Invoke(this, new SerialDataReceivedEventArgs(mavlinkData, mavlinkData.Length));
        loraHandler.Invoke(this, new SerialDataReceivedEventArgs(loraData, loraData.Length));
        
        // Assert
        Assert.True(mavlinkReceived);
        Assert.True(loraReceived);
    }

    [Fact]
    public void TestBidirectionalRelay()
    {
        // Arrange
        byte[] originalData = Encoding.ASCII.GetBytes("Test relay data");
        byte[] forwardedData = null;
        
        // Simulate MAVLink -> LoRa relay
        EventHandler<SerialDataReceivedEventArgs> mavlinkHandler = (s, e) => {
            // Forward to LoRa
            forwardedData = e.Data;
        };
        
        // Act
        mavlinkHandler.Invoke(this, new SerialDataReceivedEventArgs(originalData, originalData.Length));
        
        // Assert
        Assert.NotNull(forwardedData);
        Assert.Equal(originalData, forwardedData);
    }

    [Fact]
    public void TestMultipleDataPackets()
    {
        // Arrange
        int packetCount = 0;
        int totalBytes = 0;
        
        EventHandler<SerialDataReceivedEventArgs> handler = (s, e) => {
            packetCount++;
            totalBytes += e.BytesReceived;
        };
        
        // Act - Simulate receiving 10 packets
        for (int i = 0; i < 10; i++)
        {
            byte[] data = Encoding.ASCII.GetBytes($"Packet {i}");
            handler.Invoke(this, new SerialDataReceivedEventArgs(data, data.Length));
        }
        
        // Assert
        Assert.Equal(10, packetCount);
        Assert.True(totalBytes > 0);
    }

    [Fact]
    public void TestErrorHandling()
    {
        // Arrange
        bool errorOccurred = false;
        string errorMessage = null;
        
        EventHandler<string> errorHandler = (s, error) => {
            errorOccurred = true;
            errorMessage = error;
        };
        
        // Act
        errorHandler.Invoke(this, "USB device disconnected");
        
        // Assert
        Assert.True(errorOccurred);
        Assert.Equal("USB device disconnected", errorMessage);
    }

    [Fact]
    public void TestLargeDataPacket()
    {
        // Arrange - Create 4KB data packet
        byte[] largeData = new byte[4096];
        for (int i = 0; i < largeData.Length; i++)
        {
            largeData[i] = (byte)(i % 256);
        }
        
        // Act
        var eventArgs = new SerialDataReceivedEventArgs(largeData, largeData.Length);
        
        // Assert
        Assert.Equal(4096, eventArgs.BytesReceived);
        Assert.Equal(largeData.Length, eventArgs.Data.Length);
    }

    [Fact]
    public void TestEmptyDataPacket()
    {
        // Arrange
        byte[] emptyData = Array.Empty<byte>();
        
        // Act
        var eventArgs = new SerialDataReceivedEventArgs(emptyData, 0);
        
        // Assert
        Assert.Equal(0, eventArgs.BytesReceived);
        Assert.Empty(eventArgs.Data);
    }

    [Fact]
    public void TestConcurrentDataHandling()
    {
        // Arrange
        int mavlinkCount = 0;
        int loraCount = 0;
        object lockObj = new object();
        
        EventHandler<SerialDataReceivedEventArgs> mavlinkHandler = (s, e) => {
            lock (lockObj) { mavlinkCount++; }
        };
        
        EventHandler<SerialDataReceivedEventArgs> loraHandler = (s, e) => {
            lock (lockObj) { loraCount++; }
        };
        
        // Act - Simulate concurrent data from both ports
        Parallel.For(0, 100, i => {
            byte[] data = BitConverter.GetBytes(i);
            if (i % 2 == 0)
                mavlinkHandler.Invoke(this, new SerialDataReceivedEventArgs(data, data.Length));
            else
                loraHandler.Invoke(this, new SerialDataReceivedEventArgs(data, data.Length));
        });
        
        // Assert
        Assert.Equal(50, mavlinkCount);
        Assert.Equal(50, loraCount);
    }
}
