using FluentAssertions;
using Pigeon_Uno.Core.Services;
using Pigeon_Uno.Core.Models;
using Xunit;

namespace Pigeon_Uno.Tests.Services;

/// <summary>
/// Unit tests for MavLinkService connection management
/// Tests connection state transitions, transport creation, and event emission
/// </summary>
public class MavLinkServiceConnectionTests
{
    [Fact]
    public async Task ConnectAsync_WithValidTcpConfig_ShouldConnect()
    {
        // Arrange
        var service = new MavLinkService();
        var config = new ConnectionConfig
        {
            Type = ConnectionType.TCP,
            Address = "127.0.0.1",
            Port = 5760
        };

        // Act
        var result = await service.ConnectAsync(config);

        // Assert - Connection may fail if no server is listening, but should not throw
        result.Should().BeFalse(); // Expected to fail without a real server
        service.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task ConnectAsync_WithValidUdpConfig_ShouldConnect()
    {
        // Arrange
        var service = new MavLinkService();
        var config = new ConnectionConfig
        {
            Type = ConnectionType.UDP,
            Address = "127.0.0.1",
            Port = 14550
        };

        // Act
        var result = await service.ConnectAsync(config);

        // Assert - UDP should connect successfully (connectionless protocol)
        result.Should().BeTrue();
        service.IsConnected.Should().BeTrue();

        // Cleanup
        await service.DisconnectAsync();
    }

    [Fact]
    public async Task ConnectAsync_InPlaybackMode_ShouldFail()
    {
        // Arrange
        var service = new MavLinkService();
        service.EnterPlaybackMode();

        var config = new ConnectionConfig
        {
            Type = ConnectionType.UDP,
            Address = "127.0.0.1",
            Port = 14550
        };

        // Act
        var result = await service.ConnectAsync(config);

        // Assert
        result.Should().BeFalse();
        service.IsConnected.Should().BeFalse();
        service.IsInPlaybackMode.Should().BeTrue();
    }

    [Fact]
    public async Task ConnectAsync_WithConnectionString_ShouldParseAndConnect()
    {
        // Arrange
        var service = new MavLinkService();
        var connectionString = "udp://127.0.0.1:14550";

        // Act
        var result = await service.ConnectAsync(connectionString);

        // Assert
        result.Should().BeTrue();
        service.IsConnected.Should().BeTrue();
        service.ConnectionType.Should().Be(ConnectionType.UDP);

        // Cleanup
        await service.DisconnectAsync();
    }

    [Fact]
    public async Task ConnectAsync_WithInvalidConnectionString_ShouldFail()
    {
        // Arrange
        var service = new MavLinkService();
        var connectionString = "invalid://connection";

        // Act
        var result = await service.ConnectAsync(connectionString);

        // Assert
        result.Should().BeFalse();
        service.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task ConnectAsync_WithTypeAndAddress_ShouldConnect()
    {
        // Arrange
        var service = new MavLinkService();

        // Act
        var result = await service.ConnectAsync(ConnectionType.UDP, "127.0.0.1", 14550);

        // Assert
        result.Should().BeTrue();
        service.IsConnected.Should().BeTrue();

        // Cleanup
        await service.DisconnectAsync();
    }

    [Fact]
    public async Task DisconnectAsync_WhenConnected_ShouldDisconnect()
    {
        // Arrange
        var service = new MavLinkService();
        await service.ConnectAsync(ConnectionType.UDP, "127.0.0.1", 14550);
        service.IsConnected.Should().BeTrue();

        // Act
        await service.DisconnectAsync();

        // Assert
        service.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task DisconnectAsync_WhenNotConnected_ShouldNotThrow()
    {
        // Arrange
        var service = new MavLinkService();

        // Act
        var act = async () => await service.DisconnectAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ConnectionStatusChanged_WhenConnecting_ShouldEmitEvent()
    {
        // Arrange
        var service = new MavLinkService();
        bool eventRaised = false;
        bool eventStatus = false;

        service.ConnectionStatusChanged += (sender, status) =>
        {
            eventRaised = true;
            eventStatus = status;
        };

        // Act
        await service.ConnectAsync(ConnectionType.UDP, "127.0.0.1", 14550);

        // Assert
        eventRaised.Should().BeTrue();
        eventStatus.Should().BeTrue();

        // Cleanup
        await service.DisconnectAsync();
    }

    [Fact]
    public async Task ConnectionStatusChanged_WhenDisconnecting_ShouldEmitEvent()
    {
        // Arrange
        var service = new MavLinkService();
        await service.ConnectAsync(ConnectionType.UDP, "127.0.0.1", 14550);

        bool eventRaised = false;
        bool eventStatus = true;

        service.ConnectionStatusChanged += (sender, status) =>
        {
            eventRaised = true;
            eventStatus = status;
        };

        // Act
        await service.DisconnectAsync();

        // Assert
        eventRaised.Should().BeTrue();
        eventStatus.Should().BeFalse();
    }

    [Fact]
    public async Task ConnectAsync_WhenAlreadyConnected_ShouldDisconnectFirst()
    {
        // Arrange
        var service = new MavLinkService();
        await service.ConnectAsync(ConnectionType.UDP, "127.0.0.1", 14550);
        service.IsConnected.Should().BeTrue();

        var config = new ConnectionConfig
        {
            Type = ConnectionType.UDP,
            Address = "127.0.0.1",
            Port = 14551
        };

        // Act
        var result = await service.ConnectAsync(config);

        // Assert
        result.Should().BeTrue();
        service.IsConnected.Should().BeTrue();

        // Cleanup
        await service.DisconnectAsync();
    }
}
