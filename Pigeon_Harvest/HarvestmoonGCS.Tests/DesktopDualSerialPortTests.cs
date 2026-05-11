using Xunit;
using HarvestmoonGCS.Services;
using System.Threading.Tasks;
using System.Text;
using System.Linq;

namespace HarvestmoonGCS.Tests;

/// <summary>
/// Unit tests for Desktop Dual Serial Port functionality
/// Tests the core logic without requiring actual serial hardware
/// </summary>
public class DesktopDualSerialPortTests
{
    [Fact]
    public void Constructor_ShouldInitialize()
    {
        // Arrange & Act
        using var service = new DesktopDualSerialPortService();

        // Assert
        Assert.NotNull(service);
        Assert.False(service.IsMavLinkOpen);
        Assert.False(service.IsLoRaOpen);
        Assert.False(service.AreBothOpen);
    }

    [Fact]
    public async Task GetAvailablePortsAsync_ShouldReturnList()
    {
        // Arrange
        using var service = new DesktopDualSerialPortService();

        // Act
        var ports = await service.GetAvailablePortsAsync();

        // Assert
        Assert.NotNull(ports);
        // Note: May be empty if no serial ports available
    }

    [Fact]
    public async Task OpenMavLinkPortAsync_WithInvalidPort_ShouldReturnFalse()
    {
        // Arrange
        using var service = new DesktopDualSerialPortService();
        var invalidPort = "INVALID_PORT_NAME";

        // Act
        var result = await service.OpenMavLinkPortAsync(invalidPort, 57600);

        // Assert
        Assert.False(result);
        Assert.False(service.IsMavLinkOpen);
    }

    [Fact]
    public async Task OpenLoRaPortAsync_WithInvalidPort_ShouldReturnFalse()
    {
        // Arrange
        using var service = new DesktopDualSerialPortService();
        var invalidPort = "INVALID_PORT_NAME";

        // Act
        var result = await service.OpenLoRaPortAsync(invalidPort, 57600);

        // Assert
        Assert.False(result);
        Assert.False(service.IsLoRaOpen);
    }

    [Fact]
    public async Task WriteMavLinkAsync_WhenPortNotOpen_ShouldReturnFalse()
    {
        // Arrange
        using var service = new DesktopDualSerialPortService();
        var testData = Encoding.ASCII.GetBytes("Test");

        // Act
        var result = await service.WriteMavLinkAsync(testData);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task WriteLoRaAsync_WhenPortNotOpen_ShouldReturnFalse()
    {
        // Arrange
        using var service = new DesktopDualSerialPortService();
        var testData = Encoding.ASCII.GetBytes("Test");

        // Act
        var result = await service.WriteLoRaAsync(testData);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CloseMavLinkPortAsync_WhenNotOpen_ShouldNotThrow()
    {
        // Arrange
        using var service = new DesktopDualSerialPortService();

        // Act & Assert
        await service.CloseMavLinkPortAsync();
        Assert.False(service.IsMavLinkOpen);
    }

    [Fact]
    public async Task CloseLoRaPortAsync_WhenNotOpen_ShouldNotThrow()
    {
        // Arrange
        using var service = new DesktopDualSerialPortService();

        // Act & Assert
        await service.CloseLoRaPortAsync();
        Assert.False(service.IsLoRaOpen);
    }

    [Fact]
    public async Task CloseAllPortsAsync_ShouldCloseAllPorts()
    {
        // Arrange
        using var service = new DesktopDualSerialPortService();

        // Act
        await service.CloseAllPortsAsync();

        // Assert
        Assert.False(service.IsMavLinkOpen);
        Assert.False(service.IsLoRaOpen);
        Assert.False(service.AreBothOpen);
    }

    [Fact]
    public void MavLinkDataReceived_EventShouldBeSubscribable()
    {
        // Arrange
        using var service = new DesktopDualSerialPortService();
        var eventRaised = false;

        // Act
        service.MavLinkDataReceived += (s, e) => eventRaised = true;

        // Assert
        Assert.False(eventRaised); // Event not raised yet
    }

    [Fact]
    public void LoRaDataReceived_EventShouldBeSubscribable()
    {
        // Arrange
        using var service = new DesktopDualSerialPortService();
        var eventRaised = false;

        // Act
        service.LoRaDataReceived += (s, e) => eventRaised = true;

        // Assert
        Assert.False(eventRaised); // Event not raised yet
    }

    [Fact]
    public void ErrorOccurred_EventShouldBeSubscribable()
    {
        // Arrange
        using var service = new DesktopDualSerialPortService();
        var eventRaised = false;

        // Act
        service.ErrorOccurred += (s, e) => eventRaised = true;

        // Assert
        Assert.False(eventRaised); // Event not raised yet
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Arrange
        var service = new DesktopDualSerialPortService();

        // Act & Assert
        service.Dispose();
    }

    [Fact]
    public void AreBothOpen_WhenNeitherOpen_ShouldReturnFalse()
    {
        // Arrange
        using var service = new DesktopDualSerialPortService();

        // Act & Assert
        Assert.False(service.AreBothOpen);
    }

    [Fact]
    public async Task GetAvailablePortsAsync_ShouldReturnSerialPortInfo()
    {
        // Arrange
        using var service = new DesktopDualSerialPortService();

        // Act
        var ports = await service.GetAvailablePortsAsync();

        // Assert
        Assert.NotNull(ports);
        Assert.All(ports, port =>
        {
            Assert.NotNull(port.PortName);
            Assert.NotNull(port.DisplayName);
        });
    }

    [Fact]
    public async Task MultipleClose_ShouldNotThrow()
    {
        // Arrange
        using var service = new DesktopDualSerialPortService();

        // Act & Assert
        await service.CloseMavLinkPortAsync();
        await service.CloseMavLinkPortAsync(); // Second close
        await service.CloseLoRaPortAsync();
        await service.CloseLoRaPortAsync(); // Second close
    }

    [Fact]
    public void SerialPortInfo_ShouldHaveProperties()
    {
        // Arrange & Act
        var portInfo = new SerialPortInfo
        {
            PortName = "COM3",
            DisplayName = "COM3 - USB Serial Port"
        };

        // Assert
        Assert.Equal("COM3", portInfo.PortName);
        Assert.Equal("COM3 - USB Serial Port", portInfo.DisplayName);
    }
}

/// <summary>
/// Integration tests for Desktop Dual Serial Port
/// These tests require actual serial hardware or virtual serial ports
/// </summary>
public class DesktopDualSerialPortIntegrationTests
{
    // Note: These tests are skipped by default as they require hardware
    // To run these tests, you need:
    // 1. Two available serial ports
    // 2. Loopback connections (TX→RX on each port)
    // 3. Update port names below

    private const string TestMavLinkPort = "COM3"; // Update for your system
    private const string TestLoRaPort = "COM4";    // Update for your system
    private const bool HardwareAvailable = false;  // Set to true to run tests

    [Fact(Skip = "Requires hardware")]
    public async Task OpenMavLinkPort_WithValidPort_ShouldSucceed()
    {
        if (!HardwareAvailable) return;

        // Arrange
        using var service = new DesktopDualSerialPortService();

        // Act
        var result = await service.OpenMavLinkPortAsync(TestMavLinkPort, 57600);

        // Assert
        Assert.True(result);
        Assert.True(service.IsMavLinkOpen);

        // Cleanup
        await service.CloseMavLinkPortAsync();
    }

    [Fact(Skip = "Requires hardware")]
    public async Task OpenLoRaPort_WithValidPort_ShouldSucceed()
    {
        if (!HardwareAvailable) return;

        // Arrange
        using var service = new DesktopDualSerialPortService();

        // Act
        var result = await service.OpenLoRaPortAsync(TestLoRaPort, 57600);

        // Assert
        Assert.True(result);
        Assert.True(service.IsLoRaOpen);

        // Cleanup
        await service.CloseLoRaPortAsync();
    }

    [Fact(Skip = "Requires hardware")]
    public async Task OpenBothPorts_ShouldSucceed()
    {
        if (!HardwareAvailable) return;

        // Arrange
        using var service = new DesktopDualSerialPortService();

        // Act
        var mavlinkResult = await service.OpenMavLinkPortAsync(TestMavLinkPort, 57600);
        var loraResult = await service.OpenLoRaPortAsync(TestLoRaPort, 57600);

        // Assert
        Assert.True(mavlinkResult);
        Assert.True(loraResult);
        Assert.True(service.AreBothOpen);

        // Cleanup
        await service.CloseAllPortsAsync();
    }

    [Fact(Skip = "Requires hardware with loopback")]
    public async Task WriteMavLink_ShouldTriggerDataReceived()
    {
        if (!HardwareAvailable) return;

        // Arrange
        using var service = new DesktopDualSerialPortService();
        var dataReceived = false;
        var receivedData = Array.Empty<byte>();

        service.MavLinkDataReceived += (s, e) =>
        {
            dataReceived = true;
            receivedData = e.Data;
        };

        await service.OpenMavLinkPortAsync(TestMavLinkPort, 57600);

        // Act
        var testData = Encoding.ASCII.GetBytes("TEST");
        await service.WriteMavLinkAsync(testData);
        await Task.Delay(500); // Wait for loopback

        // Assert
        Assert.True(dataReceived);
        Assert.Equal(testData, receivedData);

        // Cleanup
        await service.CloseMavLinkPortAsync();
    }

    [Fact(Skip = "Requires hardware with loopback")]
    public async Task WriteLoRa_ShouldTriggerDataReceived()
    {
        if (!HardwareAvailable) return;

        // Arrange
        using var service = new DesktopDualSerialPortService();
        var dataReceived = false;
        var receivedData = Array.Empty<byte>();

        service.LoRaDataReceived += (s, e) =>
        {
            dataReceived = true;
            receivedData = e.Data;
        };

        await service.OpenLoRaPortAsync(TestLoRaPort, 57600);

        // Act
        var testData = Encoding.ASCII.GetBytes("TEST");
        await service.WriteLoRaAsync(testData);
        await Task.Delay(500); // Wait for loopback

        // Assert
        Assert.True(dataReceived);
        Assert.Equal(testData, receivedData);

        // Cleanup
        await service.CloseLoRaPortAsync();
    }
}
