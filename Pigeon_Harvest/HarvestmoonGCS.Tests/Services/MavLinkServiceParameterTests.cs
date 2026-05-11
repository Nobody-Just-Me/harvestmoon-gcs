using FluentAssertions;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Core.Models;
using MavLinkNet;
using Xunit;

namespace HarvestmoonGCS.Tests.Services;

/// <summary>
/// Unit tests for MavLinkService parameter operations
/// Tests parameter request, retrieval, and setting
/// </summary>
public class MavLinkServiceParameterTests
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
    public async Task GetParametersAsync_WhenNotConnected_ShouldReturnEmptyDictionary()
    {
        // Arrange
        var service = new MavLinkService();

        // Act
        var result = await service.GetParametersAsync();

        // Assert
        result.Should().NotBeNull("Result should not be null");
        result.Should().BeEmpty("Should return empty dictionary when not connected");
    }

    [Fact]
    public async Task SetParameterAsync_WhenNotConnected_ShouldReturnFalse()
    {
        // Arrange
        var service = new MavLinkService();

        // Act
        var result = await service.SetParameterAsync("TEST_PARAM", 123.45f);

        // Assert
        result.Should().BeFalse("SetParameter should fail when not connected");
    }

    [Fact]
    public async Task RequestParametersAsync_WhenNotConnected_ShouldNotThrow()
    {
        // Arrange
        var service = new MavLinkService();

        // Act
        var act = async () => await service.RequestParametersAsync();

        // Assert
        await act.Should().NotThrowAsync("RequestParameters should not throw when not connected");
    }

    [Fact]
    public async Task RequestParametersAsync_WhenConnected_ShouldSendMessage()
    {
        // Arrange
        var service = new MavLinkService();
        await service.ConnectAsync(ConnectionType.UDP, "127.0.0.1", 14550);

        // Act
        var act = async () => await service.RequestParametersAsync();

        // Assert
        await act.Should().NotThrowAsync("RequestParameters should send message when connected");

        // Cleanup
        await service.DisconnectAsync();
    }

    [Fact]
    public async Task GetParametersAsync_WithParameterValues_ShouldReturnParameters()
    {
        // Arrange
        var service = new MavLinkService();
        service.EnterPlaybackMode();

        // Start parameter download
        var getParamsTask = service.GetParametersAsync();

        // Simulate receiving parameter values
        await Task.Delay(200);
        
        // Inject 3 parameter values
        InjectParameterValue(service, "PARAM1", 10.5f, 0, 3);
        InjectParameterValue(service, "PARAM2", 20.0f, 1, 3);
        InjectParameterValue(service, "PARAM3", 30.5f, 2, 3);

        // Act
        var result = await getParamsTask;

        // Assert
        result.Should().NotBeNull("Result should not be null");
        result.Should().HaveCount(3, "Should have received 3 parameters");
        result.Should().ContainKey("PARAM1").WhoseValue.Should().Be(10.5f);
        result.Should().ContainKey("PARAM2").WhoseValue.Should().Be(20.0f);
        result.Should().ContainKey("PARAM3").WhoseValue.Should().Be(30.5f);

        // Cleanup
        service.ExitPlaybackMode();
    }

    [Fact]
    public async Task GetParametersAsync_WithTimeout_ShouldReturnPartialParameters()
    {
        // Arrange
        var service = new MavLinkService();
        await service.ConnectAsync(ConnectionType.UDP, "127.0.0.1", 14550);

        // Act - Request parameters without autopilot (will timeout)
        var result = await service.GetParametersAsync();

        // Assert
        result.Should().NotBeNull("Result should not be null even on timeout");
        result.Should().BeEmpty("Should return empty dictionary on timeout without autopilot");

        // Cleanup
        await service.DisconnectAsync();
    }

    [Fact]
    public async Task SetParameterAsync_WhenConnected_ShouldSendMessage()
    {
        // Arrange
        var service = new MavLinkService();
        await service.ConnectAsync(ConnectionType.UDP, "127.0.0.1", 14550);

        // Act
        var act = async () => await service.SetParameterAsync("TEST_PARAM", 123.45f);

        // Assert
        await act.Should().NotThrowAsync("SetParameter should send message when connected");

        // Cleanup
        await service.DisconnectAsync();
    }

    [Fact]
    public async Task SetParameterAsync_WithConfirmation_ShouldReturnTrue()
    {
        // Arrange
        var service = new MavLinkService();
        service.EnterPlaybackMode();

        // Act - Set parameter and immediately inject confirmation
        var setTask = service.SetParameterAsync("TEST_PARAM", 123.45f);
        await Task.Delay(50);
        
        // Simulate parameter confirmation with new value
        InjectParameterValue(service, "TEST_PARAM", 123.45f, 0, 1);
        
        var result = await setTask;

        // Assert
        result.Should().BeTrue("SetParameter should succeed when confirmed");

        // Cleanup
        service.ExitPlaybackMode();
    }

    [Fact]
    public async Task ParameterValue_ShouldUpdateDictionary()
    {
        // Arrange
        var service = new MavLinkService();
        service.EnterPlaybackMode();

        // Act - Inject parameter value
        InjectParameterValue(service, "TEST_PARAM", 42.0f, 0, 1);
        await Task.Delay(50);

        // Get parameters to verify it was stored
        var result = await service.GetParametersAsync();

        // Assert
        result.Should().ContainKey("TEST_PARAM").WhoseValue.Should().Be(42.0f);

        // Cleanup
        service.ExitPlaybackMode();
    }

    [Fact]
    public async Task ParameterValue_WithMultipleParameters_ShouldStoreAll()
    {
        // Arrange
        var service = new MavLinkService();
        service.EnterPlaybackMode();

        // Act - Inject multiple parameter values
        InjectParameterValue(service, "PARAM_A", 1.0f, 0, 5);
        InjectParameterValue(service, "PARAM_B", 2.0f, 1, 5);
        InjectParameterValue(service, "PARAM_C", 3.0f, 2, 5);
        InjectParameterValue(service, "PARAM_D", 4.0f, 3, 5);
        InjectParameterValue(service, "PARAM_E", 5.0f, 4, 5);
        await Task.Delay(250);

        // Get parameters to verify they were stored
        var result = await service.GetParametersAsync();

        // Assert
        result.Should().HaveCount(5, "Should have stored all 5 parameters");
        result["PARAM_A"].Should().Be(1.0f);
        result["PARAM_B"].Should().Be(2.0f);
        result["PARAM_C"].Should().Be(3.0f);
        result["PARAM_D"].Should().Be(4.0f);
        result["PARAM_E"].Should().Be(5.0f);

        // Cleanup
        service.ExitPlaybackMode();
    }

    [Fact]
    public async Task ParameterValue_ShouldUpdateExistingParameter()
    {
        // Arrange
        var service = new MavLinkService();
        service.EnterPlaybackMode();

        // Inject initial parameter value
        InjectParameterValue(service, "TEST_PARAM", 10.0f, 0, 1);
        await Task.Delay(200);

        // Act - Update the same parameter with new value
        InjectParameterValue(service, "TEST_PARAM", 20.0f, 0, 1);
        await Task.Delay(200);

        // Get parameters to verify it was updated
        var result = await service.GetParametersAsync();

        // Assert
        result.Should().ContainKey("TEST_PARAM").WhoseValue.Should().Be(20.0f, "Parameter should be updated with new value");

        // Cleanup
        service.ExitPlaybackMode();
    }

    [Fact]
    public async Task GetParametersAsync_WithIncompleteDownload_ShouldTimeout()
    {
        // Arrange
        var service = new MavLinkService();
        service.EnterPlaybackMode();

        // Start parameter download
        var getParamsTask = service.GetParametersAsync();

        // Simulate receiving only partial parameters (2 out of 5)
        await Task.Delay(200);
        InjectParameterValue(service, "PARAM1", 10.0f, 0, 5);
        InjectParameterValue(service, "PARAM2", 20.0f, 1, 5);
        // Don't send the remaining 3 parameters

        // Act - Wait for timeout (should return partial results)
        var result = await getParamsTask;

        // Assert
        result.Should().NotBeNull("Result should not be null");
        result.Should().HaveCount(2, "Should return partial parameters received before timeout");

        // Cleanup
        service.ExitPlaybackMode();
    }

    [Fact]
    public async Task RequestParameters_ShouldBeSameAsRequestParametersAsync()
    {
        // Arrange
        var service = new MavLinkService();
        await service.ConnectAsync(ConnectionType.UDP, "127.0.0.1", 14550);

        // Act
        var act1 = async () => await service.RequestParameters();
        var act2 = async () => await service.RequestParametersAsync();

        // Assert
        await act1.Should().NotThrowAsync("RequestParameters should work");
        await act2.Should().NotThrowAsync("RequestParametersAsync should work");

        // Cleanup
        await service.DisconnectAsync();
    }

    /// <summary>
    /// Helper method to inject a parameter value packet
    /// </summary>
    private void InjectParameterValue(MavLinkService service, string paramName, float value, ushort index, ushort count)
    {
        // Create parameter name as char array (16 characters max)
        char[] paramId = new char[16];
        for (int i = 0; i < paramName.Length && i < 16; i++)
        {
            paramId[i] = paramName[i];
        }

        var paramMessage = new UasParamValue
        {
            ParamId = paramId,
            ParamValue = value,
            ParamType = MavParamType.Real32,
            ParamCount = count,
            ParamIndex = index
        };

        var packet = new TestMavLinkPacket(paramMessage, 1, 0, 0);

        service.InjectPacket(packet);
    }
}
