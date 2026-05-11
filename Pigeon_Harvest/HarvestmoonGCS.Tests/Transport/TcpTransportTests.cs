using FluentAssertions;
using HarvestmoonGCS.Core.Transport;
using Xunit;

namespace HarvestmoonGCS.Tests.Transport;

public class TcpTransportTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange & Act
        var transport = new TcpTransport("127.0.0.1", 5760);

        // Assert
        transport.Should().NotBeNull();
        transport.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithNullHost_ThrowsArgumentNullException()
    {
        // Arrange & Act
        Action act = () => new TcpTransport(null!, 5760);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IsConnected_WhenNotConnected_ReturnsFalse()
    {
        // Arrange
        var transport = new TcpTransport("127.0.0.1", 5760);

        // Act & Assert
        transport.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void Dispose_WhenCalled_DoesNotThrow()
    {
        // Arrange
        var transport = new TcpTransport("127.0.0.1", 5760);

        // Act
        Action act = () => transport.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var transport = new TcpTransport("127.0.0.1", 5760);

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
        var transport = new TcpTransport("127.0.0.1", 5760);

        // Act
        Func<Task> act = async () => await transport.DisconnectAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReadAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Arrange
        var transport = new TcpTransport("127.0.0.1", 5760);
        var buffer = new byte[100];

        // Act
        Func<Task> act = async () => await transport.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("TCP client is not connected.");
    }

    [Fact]
    public async Task WriteAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Arrange
        var transport = new TcpTransport("127.0.0.1", 5760);
        var buffer = new byte[] { 1, 2, 3 };

        // Act
        Func<Task> act = async () => await transport.WriteAsync(buffer, 0, buffer.Length);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("TCP client is not connected.");
    }
}
