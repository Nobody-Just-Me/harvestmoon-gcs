using FluentAssertions;
using Pigeon_Uno.Core.Transport;
using Xunit;

namespace Pigeon_Uno.Tests.Transport;

public class SerialTransportTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange & Act
        var transport = new SerialTransport("COM3", 57600);

        // Assert
        transport.Should().NotBeNull();
        transport.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithNullPortName_ThrowsArgumentNullException()
    {
        // Arrange & Act
        Action act = () => new SerialTransport(null!, 57600);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IsConnected_WhenNotConnected_ReturnsFalse()
    {
        // Arrange
        var transport = new SerialTransport("COM3", 57600);

        // Act & Assert
        transport.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void Dispose_WhenCalled_DoesNotThrow()
    {
        // Arrange
        var transport = new SerialTransport("COM3", 57600);

        // Act
        Action act = () => transport.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var transport = new SerialTransport("COM3", 57600);

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
        var transport = new SerialTransport("COM3", 57600);

        // Act
        Func<Task> act = async () => await transport.DisconnectAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReadAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Arrange
        var transport = new SerialTransport("COM3", 57600);
        var buffer = new byte[100];

        // Act
        Func<Task> act = async () => await transport.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Serial port is not connected.");
    }

    [Fact]
    public async Task WriteAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Arrange
        var transport = new SerialTransport("COM3", 57600);
        var buffer = new byte[] { 1, 2, 3 };

        // Act
        Func<Task> act = async () => await transport.WriteAsync(buffer, 0, buffer.Length);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Serial port is not connected.");
    }
}
