using Xunit;
using FluentAssertions;
using Moq;
using HarvestmoonGCS.ViewModels;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Core.Models;

namespace HarvestmoonGCS.Tests.Integration;

public class MapViewModelIntegrationTests
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
    public async Task WaypointImportFlow_MissionPlannerFormat_ImportsAndDisplaysWaypoints()
    {
        var mockFile = new Mock<IFileService>();
        var mockDialog = new Mock<IDialogService>();
        var tempFile = Path.GetTempFileName();
        
        File.WriteAllText(tempFile, @"QGC WPL 110
0	1	0	16	0	0	0	0	-7.2754	112.7947	100	1
1	0	0	16	0	0	0	0	-7.2764	112.7957	100	1
2	0	0	16	0	0	0	0	-7.2774	112.7967	100	1");
        
        mockFile.Setup(x => x.PickFileAsync(It.IsAny<string[]>())).ReturnsAsync(tempFile);
        
        var viewModel = CreateViewModel(
            fileService: mockFile.Object,
            dialogService: mockDialog.Object);
        
        viewModel.LoadMissionCommand.Execute(null);
        await Task.Delay(100);
        await Task.Delay(100);
        
        viewModel.Waypoints.Should().HaveCount(3);
        viewModel.Waypoints[0].Latitude.Should().Be(-7.2754);
        viewModel.Waypoints[0].Longitude.Should().Be(112.7947);
        viewModel.Waypoints[0].Altitude.Should().Be(100);
        viewModel.TotalDistance.Should().BeGreaterThan(0);
        
        File.Delete(tempFile);
    }

    [Fact]
    public async Task WaypointImportFlow_CsvFormat_ImportsAndDisplaysWaypoints()
    {
        var mockFile = new Mock<IFileService>();
        var mockDialog = new Mock<IDialogService>();
        var tempFile = Path.GetTempFileName();
        
        File.WriteAllText(tempFile, @"-7.2754,112.7947,100
-7.2764,112.7957,100
-7.2774,112.7967,100");
        
        mockFile.Setup(x => x.PickFileAsync(It.IsAny<string[]>())).ReturnsAsync(tempFile);
        
        var viewModel = CreateViewModel(
            fileService: mockFile.Object,
            dialogService: mockDialog.Object);
        
        viewModel.LoadMissionCommand.Execute(null);
        await Task.Delay(100);
        await Task.Delay(100);
        
        viewModel.Waypoints.Should().HaveCount(3);
        viewModel.Waypoints[0].Latitude.Should().Be(-7.2754);
        viewModel.Waypoints[0].Longitude.Should().Be(112.7947);
        viewModel.Waypoints[0].Altitude.Should().Be(100);
        
        File.Delete(tempFile);
    }

    [Fact]
    public async Task WaypointImportFlow_InvalidFile_ShowsError()
    {
        var mockFile = new Mock<IFileService>();
        var mockDialog = new Mock<IDialogService>();
        var tempFile = Path.GetTempFileName();
        
        File.WriteAllText(tempFile, @"invalid content
not a valid waypoint");
        
        mockFile.Setup(x => x.PickFileAsync(It.IsAny<string[]>())).ReturnsAsync(tempFile);
        mockDialog.Setup(x => x.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        
        var viewModel = CreateViewModel(
            fileService: mockFile.Object,
            dialogService: mockDialog.Object);
        
        viewModel.LoadMissionCommand.Execute(null);
        await Task.Delay(100);
        await Task.Delay(100);
        
        viewModel.Waypoints.Should().BeEmpty();
        mockDialog.Verify(x => x.ShowAlertAsync(It.Is<string>(s => s.Contains("No valid waypoints")), It.IsAny<string>()), Times.Once);
        
        File.Delete(tempFile);
    }

    [Fact]
    public async Task WaypointImportFlow_CancelledFilePicker_DoesNothing()
    {
        var mockFile = new Mock<IFileService>();
        var mockDialog = new Mock<IDialogService>();
        
        mockFile.Setup(x => x.PickFileAsync(It.IsAny<string[]>())).ReturnsAsync((string?)null);
        
        var viewModel = CreateViewModel(
            fileService: mockFile.Object,
            dialogService: mockDialog.Object);
        
        viewModel.AddWaypointCommand.Execute(null);
        await Task.Delay(100);
        
        viewModel.LoadMissionCommand.Execute(null);
        await Task.Delay(100);
        await Task.Delay(100);
        
        viewModel.Waypoints.Should().HaveCount(1);
        mockDialog.Verify(x => x.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task MissionExecutionFlow_WithConnectedVehicle_UploadsAndStartsMission()
    {
        var mockMavLink = new Mock<IMavLinkService>();
        var mockDialog = new Mock<IDialogService>();
        
        mockMavLink.Setup(x => x.IsConnected).Returns(true);
        mockMavLink.Setup(x => x.UploadMissionAsync(It.IsAny<IEnumerable<WaypointData>>())).ReturnsAsync(true);
        mockDialog.Setup(x => x.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        mockDialog.Setup(x => x.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        
        var viewModel = CreateViewModel(
            mavLinkService: mockMavLink.Object,
            dialogService: mockDialog.Object);
        
        viewModel.AddWaypointCommand.Execute(null);
        await Task.Delay(100);
        viewModel.AddWaypointCommand.Execute(null);
        await Task.Delay(100);
        
        viewModel.UploadMissionCommand.Execute(null);
        await Task.Delay(100);
        
        mockMavLink.Verify(x => x.UploadMissionAsync(It.Is<IEnumerable<WaypointData>>(w => w.Count() == 2)), Times.Once);
        mockDialog.Verify(x => x.ShowAlertAsync(It.Is<string>(s => s.Contains("Successfully uploaded")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task MissionExecutionFlow_WithNoVehicle_ShowsError()
    {
        var mockMavLink = new Mock<IMavLinkService>();
        var mockDialog = new Mock<IDialogService>();
        
        mockMavLink.Setup(x => x.IsConnected).Returns(false);
        mockDialog.Setup(x => x.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        
        var viewModel = CreateViewModel(
            mavLinkService: mockMavLink.Object,
            dialogService: mockDialog.Object);
        
        viewModel.AddWaypointCommand.Execute(null);
        await Task.Delay(100);
        
        viewModel.UploadMissionCommand.Execute(null);
        await Task.Delay(100);
        
        mockMavLink.Verify(x => x.UploadMissionAsync(It.IsAny<IEnumerable<WaypointData>>()), Times.Never);
        mockDialog.Verify(x => x.ShowAlertAsync(It.Is<string>(s => s.Contains("not connected")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task MissionExecutionFlow_WithNoWaypoints_ShowsError()
    {
        var mockMavLink = new Mock<IMavLinkService>();
        var mockDialog = new Mock<IDialogService>();
        
        mockMavLink.Setup(x => x.IsConnected).Returns(true);
        mockDialog.Setup(x => x.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        
        var viewModel = CreateViewModel(
            mavLinkService: mockMavLink.Object,
            dialogService: mockDialog.Object);
        
        viewModel.UploadMissionCommand.Execute(null);
        await Task.Delay(100);
        
        mockMavLink.Verify(x => x.UploadMissionAsync(It.IsAny<IEnumerable<WaypointData>>()), Times.Never);
        mockDialog.Verify(x => x.ShowAlertAsync(It.Is<string>(s => s.Contains("No waypoints")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task MissionExecutionFlow_UserCancelsConfirmation_DoesNotUpload()
    {
        var mockMavLink = new Mock<IMavLinkService>();
        var mockDialog = new Mock<IDialogService>();
        
        mockMavLink.Setup(x => x.IsConnected).Returns(true);
        mockDialog.Setup(x => x.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        
        var viewModel = CreateViewModel(
            mavLinkService: mockMavLink.Object,
            dialogService: mockDialog.Object);
        
        viewModel.AddWaypointCommand.Execute(null);
        await Task.Delay(100);
        
        viewModel.UploadMissionCommand.Execute(null);
        await Task.Delay(100);
        
        mockMavLink.Verify(x => x.UploadMissionAsync(It.IsAny<IEnumerable<WaypointData>>()), Times.Never);
    }

    [Fact]
    public async Task MissionExecutionFlow_UploadFails_ShowsError()
    {
        var mockMavLink = new Mock<IMavLinkService>();
        var mockDialog = new Mock<IDialogService>();
        
        mockMavLink.Setup(x => x.IsConnected).Returns(true);
        mockMavLink.Setup(x => x.UploadMissionAsync(It.IsAny<IEnumerable<WaypointData>>())).ReturnsAsync(false);
        mockDialog.Setup(x => x.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        mockDialog.Setup(x => x.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        
        var viewModel = CreateViewModel(
            mavLinkService: mockMavLink.Object,
            dialogService: mockDialog.Object);
        
        viewModel.AddWaypointCommand.Execute(null);
        await Task.Delay(100);
        
        viewModel.UploadMissionCommand.Execute(null);
        await Task.Delay(100);
        
        mockDialog.Verify(x => x.ShowAlertAsync(It.Is<string>(s => s.Contains("Failed to upload")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task SaveMissionFlow_WithWaypoints_SavesToFile()
    {
        var mockFile = new Mock<IFileService>();
        var mockDialog = new Mock<IDialogService>();
        
        mockFile.Setup(x => x.SaveMissionFileAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("/path/to/mission.waypoints");
        mockDialog.Setup(x => x.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        
        var viewModel = CreateViewModel(
            fileService: mockFile.Object,
            dialogService: mockDialog.Object);
        
        viewModel.AddWaypointCommand.Execute(null);
        await Task.Delay(100);
        viewModel.AddWaypointCommand.Execute(null);
        await Task.Delay(100);
        
        viewModel.SaveMissionCommand.Execute(null);
        await Task.Delay(100);
        await Task.Delay(100);
        
        mockFile.Verify(x => x.SaveMissionFileAsync(It.Is<string>(s => s.EndsWith(".waypoints")), It.IsAny<string>()), Times.Once);
        mockDialog.Verify(x => x.ShowAlertAsync(It.Is<string>(s => s.Contains("saved")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task SaveMissionFlow_WithNoWaypoints_ShowsError()
    {
        var mockFile = new Mock<IFileService>();
        var mockDialog = new Mock<IDialogService>();
        
        mockDialog.Setup(x => x.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        
        var viewModel = CreateViewModel(
            fileService: mockFile.Object,
            dialogService: mockDialog.Object);
        
        viewModel.SaveMissionCommand.Execute(null);
        await Task.Delay(100);
        await Task.Delay(100);
        
        mockFile.Verify(x => x.SaveMissionFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        mockDialog.Verify(x => x.ShowAlertAsync(It.Is<string>(s => s.Contains("No waypoints")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task DownloadMissionFlow_WithConnectedVehicle_DownloadsWaypoints()
    {
        var mockMavLink = new Mock<IMavLinkService>();
        var mockDialog = new Mock<IDialogService>();
        
        var downloadedWaypoints = new List<WaypointData>
        {
            new() { Sequence = 1, Latitude = -7.2754, Longitude = 112.7947, Altitude = 100 },
            new() { Sequence = 2, Latitude = -7.2764, Longitude = 112.7957, Altitude = 100 }
        };
        
        mockMavLink.Setup(x => x.IsConnected).Returns(true);
        mockMavLink.Setup(x => x.DownloadMissionAsync()).ReturnsAsync(downloadedWaypoints);
        mockDialog.Setup(x => x.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        mockDialog.Setup(x => x.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        
        var viewModel = CreateViewModel(
            mavLinkService: mockMavLink.Object,
            dialogService: mockDialog.Object);
        
        viewModel.DownloadMissionCommand.Execute(null);
        await Task.Delay(100);
        
        viewModel.Waypoints.Should().HaveCount(2);
        mockDialog.Verify(x => x.ShowAlertAsync(It.Is<string>(s => s.Contains("downloaded")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task DownloadMissionFlow_WithNoVehicle_ShowsError()
    {
        var mockMavLink = new Mock<IMavLinkService>();
        var mockDialog = new Mock<IDialogService>();
        
        mockMavLink.Setup(x => x.IsConnected).Returns(false);
        mockDialog.Setup(x => x.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        
        var viewModel = CreateViewModel(
            mavLinkService: mockMavLink.Object,
            dialogService: mockDialog.Object);
        
        viewModel.DownloadMissionCommand.Execute(null);
        await Task.Delay(100);
        
        mockMavLink.Verify(x => x.DownloadMissionAsync(), Times.Never);
        mockDialog.Verify(x => x.ShowAlertAsync(It.Is<string>(s => s.Contains("not connected")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CompleteFlow_AddImportUpload_ClearAndVerifyEmpty()
    {
        var viewModel = CreateViewModel();
        
        viewModel.AddWaypointCommand.Execute(null);
        await Task.Delay(100);
        viewModel.AddWaypointCommand.Execute(null);
        await Task.Delay(100);
        viewModel.AddWaypointCommand.Execute(null);
        await Task.Delay(100);
        
        viewModel.Waypoints.Should().HaveCount(3);
        viewModel.TotalDistance.Should().BeGreaterThan(0);
        
        viewModel.ClearMissionCommand.Execute(null);
        await Task.Delay(100);
        
        viewModel.Waypoints.Should().BeEmpty();
        viewModel.TotalDistance.Should().Be(0);
    }
}
