using Xunit;
using FluentAssertions;
using Moq;
using Pigeon_Uno.ViewModels;
using Pigeon_Uno.Core.Services;
using Pigeon_Uno.Core.Models;

namespace Pigeon_Uno.Tests.ViewModels;

public class MapViewModelWaypointTests
{
    private static MapViewModel CreateViewModel(
        IMavLinkService? mavLinkService = null,
        IDialogService? dialogService = null,
        IGeofenceService? geofenceService = null,
        IFileService? fileService = null)
    {
        var mockMavLink = mavLinkService ?? new Mock<IMavLinkService>().Object;
        var mockDialog = dialogService ?? new Mock<IDialogService>().Object;
        var mockGeofence = geofenceService ?? new Mock<IGeofenceService>().Object;
        var mockFile = fileService ?? new Mock<IFileService>().Object;
        
        return new MapViewModel(mockGeofence, mockMavLink, mockDialog, mockFile);
    }

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        var mockMavLink = new Mock<IMavLinkService>();
        var mockDialog = new Mock<IDialogService>();
        var mockGeofence = new Mock<IGeofenceService>();
        var mockFile = new Mock<IFileService>();
        
        var viewModel = new MapViewModel(mockGeofence.Object, mockMavLink.Object, mockDialog.Object, mockFile.Object);
        
        viewModel.Should().NotBeNull();
        viewModel.Waypoints.Should().BeEmpty();
    }

    [Fact]
    public void AddWaypointCommand_WithValidPosition_AddsWaypoint()
    {
        var viewModel = CreateViewModel();
        
        viewModel.AddWaypointCommand.Execute((-7.2754, 112.7947));
        
        viewModel.Waypoints.Should().HaveCount(1);
        viewModel.Waypoints[0].Latitude.Should().Be(-7.2754);
        viewModel.Waypoints[0].Longitude.Should().Be(112.7947);
    }

    [Fact]
    public void ClearMissionCommand_WithWaypoints_ClearsAll()
    {
        var viewModel = CreateViewModel();
        viewModel.AddWaypointCommand.Execute((-7.2754, 112.7947));
        viewModel.AddWaypointCommand.Execute((-7.2764, 112.7957));
        
        viewModel.ClearMissionCommand.Execute(null);
        
        viewModel.Waypoints.Should().BeEmpty();
    }

    [Fact]
    public void DeleteWaypointCommand_WithExistingWaypoint_RemovesIt()
    {
        var viewModel = CreateViewModel();
        viewModel.AddWaypointCommand.Execute((-7.2754, 112.7947));
        var waypoint = viewModel.Waypoints[0];
        
        viewModel.DeleteWaypointCommand.Execute(waypoint);
        
        viewModel.Waypoints.Should().BeEmpty();
    }

    [Fact]
    public void Waypoints_Sequence_IsAutomaticallyAssigned()
    {
        var viewModel = CreateViewModel();
        
        viewModel.AddWaypointCommand.Execute((-7.2754, 112.7947));
        viewModel.AddWaypointCommand.Execute((-7.2764, 112.7957));
        viewModel.AddWaypointCommand.Execute((-7.2774, 112.7967));
        
        viewModel.Waypoints[0].Sequence.Should().Be(1);
        viewModel.Waypoints[1].Sequence.Should().Be(2);
        viewModel.Waypoints[2].Sequence.Should().Be(3);
    }

    [Fact]
    public void TotalDistance_WithMultipleWaypoints_IsCalculated()
    {
        var viewModel = CreateViewModel();
        
        viewModel.AddWaypointCommand.Execute((-7.2754, 112.7947));
        viewModel.AddWaypointCommand.Execute((-7.2764, 112.7957));
        
        viewModel.TotalDistance.Should().BeGreaterThan(0);
    }

    [Fact]
    public void TotalDistance_WithSingleWaypoint_IsZero()
    {
        var viewModel = CreateViewModel();
        
        viewModel.AddWaypointCommand.Execute((-7.2754, 112.7947));
        
        viewModel.TotalDistance.Should().Be(0);
    }

    [Fact]
    public void TotalDistance_WithNoWaypoints_IsZero()
    {
        var viewModel = CreateViewModel();
        
        viewModel.TotalDistance.Should().Be(0);
    }
}