using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using MavLinkNet;
using Moq;
using Pigeon_Uno.Core.Services;
using Pigeon_Uno.Core.Models;
using Xunit;

namespace Pigeon_Uno.Tests;

public class MissionServiceTests
{
    private readonly Mock<IMavLinkService> _mockMavLink;
    private readonly MissionService _missionService;

    public MissionServiceTests()
    {
        _mockMavLink = new Mock<IMavLinkService>();
        _missionService = new MissionService(_mockMavLink.Object);
    }

    [Fact]
    public async Task UploadMission_ShouldSendMissionCount()
    {
        // Arrange
        _mockMavLink.Setup(m => m.IsConnected).Returns(true);
        var waypoints = new List<MissionWaypoint>
        {
            new MissionWaypoint { Sequence = 0, Latitude = 0, Longitude = 0, Altitude = 10, Command = MavCommand.NavWaypoint },
            new MissionWaypoint { Sequence = 1, Latitude = 1, Longitude = 1, Altitude = 20, Command = MavCommand.NavWaypoint }
        };

        // Act
        await _missionService.UploadMissionAsync(waypoints);

        // Assert
        _mockMavLink.Verify(m => m.UploadMissionAsync(It.Is<List<WaypointData>>(wps =>
            wps.Count == 2 &&
            wps[0].Sequence == 0 &&
            wps[1].Sequence == 1
        )), Times.Once);
    }

    [Fact]
    public async Task DownloadMission_ShouldSendRequestList()
    {
        // Arrange
        _mockMavLink.Setup(m => m.IsConnected).Returns(true);

        // Act
        await _missionService.DownloadMissionAsync();

        // Assert
        _mockMavLink.Verify(m => m.DownloadMissionAsync(), Times.Once);
    }
}
