using FluentAssertions;
using Pigeon_Uno.Services;
using Xunit;

namespace Pigeon_Uno.Tests;

public class MavLinkConnectionTests
{
    [Theory]
    [InlineData("tcp:127.0.0.1:5760", ConnectionType.Tcp, "127.0.0.1", 5760)]
    [InlineData("udp:14550", ConnectionType.Udp, null, 14550)]
    [InlineData("serial:COM3:57600", ConnectionType.Serial, "COM3", 57600)]
    public void ParseConnection_ValidString_ReturnsCorrectTransportInfo(string connString, ConnectionType expectedType, string expectedHostOrPort, int expectedPortOrBaud)
    {
        // Act
        var info = ConnectionParser.Parse(connString);

        // Assert
        info.Type.Should().Be(expectedType);
        if (expectedType == ConnectionType.Tcp)
        {
            info.Host.Should().Be(expectedHostOrPort);
            info.Port.Should().Be(expectedPortOrBaud);
        }
        else if (expectedType == ConnectionType.Udp)
        {
            info.Port.Should().Be(expectedPortOrBaud);
        }
        else if (expectedType == ConnectionType.Serial)
        {
            info.PortName.Should().Be(expectedHostOrPort);
            info.BaudRate.Should().Be(expectedPortOrBaud);
        }
    }
}
