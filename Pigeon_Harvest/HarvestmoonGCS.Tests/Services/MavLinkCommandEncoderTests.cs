using FluentAssertions;
using MavLinkNet;
using HarvestmoonGCS.Core.Services.MavLink;
using Xunit;

namespace HarvestmoonGCS.Tests.Services;

public class MavLinkCommandEncoderTests
{
    [Fact]
    public void CreateSetParameterCommand_ShouldCreateParamSetMessage()
    {
        var message = MavLinkCommandEncoder.CreateSetParameterCommand("FENCE_ENABLE", 1);

        var paramSet = message.Should().BeOfType<UasParamSet>().Subject;
        paramSet.TargetSystem.Should().Be(1);
        paramSet.TargetComponent.Should().Be(0);
        new string(paramSet.ParamId).TrimEnd('\0').Should().Be("FENCE_ENABLE");
        paramSet.ParamValue.Should().Be(1);
        paramSet.ParamType.Should().Be(MavParamType.Real32);
    }

    [Fact]
    public void CreateSetParameterCommand_ShouldPadAndTrimParamIdToSixteenCharacters()
    {
        var message = MavLinkCommandEncoder.CreateSetParameterCommand("ABCDEFGHIJKLMNOPQRST", 42);

        var paramSet = message.Should().BeOfType<UasParamSet>().Subject;
        paramSet.ParamId.Should().HaveCount(16);
        new string(paramSet.ParamId).Should().Be("ABCDEFGHIJKLMNOP");
    }

    [Fact]
    public void CreateRequestParametersCommand_ShouldCreateParamRequestList()
    {
        var message = MavLinkCommandEncoder.CreateRequestParametersCommand();

        var request = message.Should().BeOfType<UasParamRequestList>().Subject;
        request.TargetSystem.Should().Be(1);
        request.TargetComponent.Should().Be(0);
    }

    [Fact]
    public void CreateWaypointCommand_ShouldCreateMissionItemInt()
    {
        var message = MavLinkCommandEncoder.CreateWaypointCommand(2, -6.2, 106.8, 120, 15);

        var waypoint = message.Should().BeOfType<UasMissionItemInt>().Subject;
        waypoint.Seq.Should().Be(2);
        waypoint.Command.Should().Be(MavCmd.NavWaypoint);
        waypoint.Frame.Should().Be(MavFrame.GlobalRelativeAltInt);
        waypoint.X.Should().Be(-62_000_000);
        waypoint.Y.Should().Be(1_068_000_000);
        waypoint.Z.Should().Be(120);
        waypoint.Param2.Should().Be(15);
        waypoint.Autocontinue.Should().Be(1);
    }

    [Fact]
    public void CreateHeartbeatMessage_ShouldCreateGcsHeartbeat()
    {
        var message = MavLinkCommandEncoder.CreateHeartbeatMessage();

        var heartbeat = message.Should().BeOfType<UasHeartbeat>().Subject;
        heartbeat.Type.Should().Be(MavType.Gcs);
        heartbeat.Autopilot.Should().Be(MavAutopilot.Invalid);
        heartbeat.SystemStatus.Should().Be(MavState.Active);
        heartbeat.MavlinkVersion.Should().Be(3);
    }
}
