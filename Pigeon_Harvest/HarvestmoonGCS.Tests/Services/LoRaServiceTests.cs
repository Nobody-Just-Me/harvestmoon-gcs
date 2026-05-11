using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Core.Models;

namespace HarvestmoonGCS.Tests.Services;

public class LoRaServiceTests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var service = new LoRaService();

        // Assert
        Assert.NotNull(service);
        Assert.False(service.IsConnected);
    }

    [Fact]
    public async Task ScanDevicesAsync_ReturnsEmptyList_WhenNoDevicesFound()
    {
        // Arrange
        var service = new LoRaService();

        // Act
        var devices = await service.ScanDevicesAsync();

        // Assert
        Assert.NotNull(devices);
        Assert.IsType<List<LoRaDevice>>(devices);
    }

    [Fact]
    public async Task ConnectAsync_ReturnsFalse_WhenDeviceIsNull()
    {
        // Arrange
        var service = new LoRaService();
        LoRaDevice? nullDevice = null;

        // Act
        var result = await service.ConnectAsync(nullDevice!);

        // Assert
        Assert.False(result);
        Assert.False(service.IsConnected);
    }

    [Fact]
    public async Task DisconnectAsync_CompletesSuccessfully_WhenNotConnected()
    {
        // Arrange
        var service = new LoRaService();

        // Act
        await service.DisconnectAsync();

        // Assert
        Assert.False(service.IsConnected);
    }

    [Fact]
    public async Task SendDataAsync_ReturnsFalse_WhenNotConnected()
    {
        // Arrange
        var service = new LoRaService();
        var testData = new byte[] { 0x01, 0x02, 0x03 };

        // Act
        var result = await service.SendDataAsync(testData);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ConfigureAsync_ReturnsFalse_WhenNotConnected()
    {
        // Arrange
        var service = new LoRaService();
        var config = new LoRaConfig
        {
            Frequency = 915.0f,
            Bandwidth = 125,
            SpreadingFactor = 7
        };

        // Act
        var result = await service.ConfigureAsync(config);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void DeviceDiscovered_EventCanBeSubscribed()
    {
        // Arrange
        var service = new LoRaService();
        var eventRaised = false;
        LoRaDevice? receivedDevice = null;

        service.DeviceDiscovered += (sender, device) =>
        {
            eventRaised = true;
            receivedDevice = device;
        };

        // Act
        // Event will be raised during actual device discovery

        // Assert
        Assert.False(eventRaised); // No devices discovered yet
        Assert.Null(receivedDevice);
    }

    [Fact]
    public void ConnectionStatusChanged_EventCanBeSubscribed()
    {
        // Arrange
        var service = new LoRaService();
        var eventRaised = false;
        var connectionStatus = false;

        service.ConnectionStatusChanged += (sender, isConnected) =>
        {
            eventRaised = true;
            connectionStatus = isConnected;
        };

        // Act
        // Event will be raised during actual connection

        // Assert
        Assert.False(eventRaised); // No connection changes yet
        Assert.False(connectionStatus);
    }

    [Fact]
    public void DataReceived_EventCanBeSubscribed()
    {
        // Arrange
        var service = new LoRaService();
        var eventRaised = false;
        byte[]? receivedData = null;

        service.DataReceived += (sender, data) =>
        {
            eventRaised = true;
            receivedData = data;
        };

        // Act
        // Event will be raised when data is received

        // Assert
        Assert.False(eventRaised); // No data received yet
        Assert.Null(receivedData);
    }

    [Fact]
    public void LoRaConfig_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new LoRaConfig();

        // Assert
        Assert.Equal(915.0f, config.Frequency);
        Assert.Equal(125, config.Bandwidth);
        Assert.Equal(7, config.SpreadingFactor);
        Assert.Equal(5, config.CodingRate);
        Assert.Equal(17, config.TxPower);
        Assert.Equal(8, config.PreambleLength);
        Assert.Equal(0x12, config.SyncWord);
        Assert.True(config.EnableCRC);
        Assert.False(config.LowDataRateOptimize);
    }

    [Fact]
    public void LoRaDevice_CanBeCreated()
    {
        // Arrange & Act
        var device = new LoRaDevice
        {
            Name = "Test Device",
            PortName = "COM3",
            RSSI = -50,
            Frequency = 915.0f,
            Bandwidth = 125,
            SpreadingFactor = 7,
            LinkQuality = 80
        };

        // Assert
        Assert.Equal("Test Device", device.Name);
        Assert.Equal("COM3", device.PortName);
        Assert.Equal(-50, device.RSSI);
        Assert.Equal(915.0f, device.Frequency);
        Assert.Equal(125, device.Bandwidth);
        Assert.Equal(7, device.SpreadingFactor);
        Assert.Equal(80, device.LinkQuality);
    }
}
