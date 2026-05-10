using FluentAssertions;
using Pigeon_Uno.Core.Transport;
using Xunit;

namespace Pigeon_Uno.Tests.Transport;

public class UdpTransportTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange & Act
        var transport = new UdpTransport("127.0.0.1", 14550);

        // Assert
        transport.Should().NotBeNull();
        transport.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithNullHost_ThrowsArgumentNullException()
    {
        // Arrange & Act
        Action act = () => new UdpTransport(null!, 14550);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IsConnected_WhenNotConnected_ReturnsFalse()
    {
        // Arrange
        var transport = new UdpTransport("127.0.0.1", 14550);

        // Act & Assert
        transport.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void Dispose_WhenCalled_DoesNotThrow()
    {
        // Arrange
        var transport = new UdpTransport("127.0.0.1", 14550);

        // Act
        Action act = () => transport.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var transport = new UdpTransport("127.0.0.1", 14550);

        // Act
        Action act = () =>
        {
            transport.Dispose();
            transport.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task DisconnectAsync_WhenNotConnected_DoesNotThrow()
    {
        // Arrange
        var transport = new UdpTransport("127.0.0.1", 14550);

        // Act
        Func<Task> act = async () => await transport.DisconnectAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReadAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Arrange
        var transport = new UdpTransport("127.0.0.1", 14550);
        var buffer = new byte[100];

        // Act
        Func<Task> act = async () => await transport.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("UDP client is not connected.");
    }

    [Fact]
    public async Task WriteAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Arrange
        var transport = new UdpTransport("127.0.0.1", 14550);
        var buffer = new byte[] { 1, 2, 3 };

        // Act
        Func<Task> act = async () => await transport.WriteAsync(buffer, 0, buffer.Length);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("UDP client is not connected.");
    }

    [Fact]
    public async Task ConnectAsync_WithValidIPAddress_ReturnsTrue()
    {
        // Arrange
        var transport = new UdpTransport("127.0.0.1", 14550);

        // Act
        var result = await transport.ConnectAsync();

        // Assert
        result.Should().BeTrue();
        transport.IsConnected.Should().BeTrue();

        // Cleanup
        await transport.DisconnectAsync();
        transport.Dispose();
    }

    [Fact]
    public async Task ConnectAsync_WhenAlreadyConnected_ReturnsTrue()
    {
        // Arrange
        var transport = new UdpTransport("127.0.0.1", 14550);
        await transport.ConnectAsync();

        // Act
        var result = await transport.ConnectAsync();

        // Assert
        result.Should().BeTrue();
        transport.IsConnected.Should().BeTrue();

        // Cleanup
        await transport.DisconnectAsync();
        transport.Dispose();
    }

    [Fact]
    public async Task DisconnectAsync_WhenConnected_SetsIsConnectedToFalse()
    {
        // Arrange
        var transport = new UdpTransport("127.0.0.1", 14550);
        await transport.ConnectAsync();

        // Act
        await transport.DisconnectAsync();

        // Assert
        transport.IsConnected.Should().BeFalse();

        // Cleanup
        transport.Dispose();
    }

    [Fact]
    public async Task ConnectAsync_WithInvalidHost_ReturnsFalse()
    {
        // Arrange
        var transport = new UdpTransport("invalid.host.that.does.not.exist.12345", 14550);

        // Act
        var result = await transport.ConnectAsync();

        // Assert
        result.Should().BeFalse();
        transport.IsConnected.Should().BeFalse();

        // Cleanup
        transport.Dispose();
    }
}
