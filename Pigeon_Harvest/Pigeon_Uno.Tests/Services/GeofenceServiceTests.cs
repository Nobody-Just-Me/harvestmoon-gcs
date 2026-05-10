using FluentAssertions;
using MavLinkNet;
using Moq;
using Pigeon_Uno.Core.Models;
using Pigeon_Uno.Core.Services;
using Xunit;

namespace Pigeon_Uno.Tests.Services;

public class GeofenceServiceTests
{
    [Fact]
    public async Task SendGeofenceToVehicleAsync_CircularGeofence_ShouldUploadFenceParameters()
    {
        var mavLink = CreateConnectedMavLinkMock();
        var service = new GeofenceService(Mock.Of<ISettingsService>(), mavLink.Object);

        service.SetGeofenceCenter(-6.2, 106.8);
        service.SetGeofenceRadius(750);
        service.SetMaxAltitude(120);
        service.SetGeofenceActive(true);

        await service.SendGeofenceToVehicleAsync();

        mavLink.Verify(m => m.SetParameterAsync("FENCE_ENABLE", 0), Times.Once);
        mavLink.Verify(m => m.SetParameterAsync("FENCE_TYPE", 3), Times.Once);
        mavLink.Verify(m => m.SetParameterAsync("FENCE_RADIUS", 750), Times.Once);
        mavLink.Verify(m => m.SetParameterAsync("FENCE_ALT_MAX", 120), Times.Once);
        mavLink.Verify(m => m.SetParameterAsync("FENCE_ACTION", 1), Times.Once);
        mavLink.Verify(m => m.SetParameterAsync("FENCE_ENABLE", 1), Times.Once);
        mavLink.Verify(m => m.SendMessage(It.IsAny<UasMessage>()), Times.Never);
    }

    [Fact]
    public async Task SendGeofenceToVehicleAsync_PolygonGeofence_ShouldUploadFencePoints()
    {
        var mavLink = CreateConnectedMavLinkMock();
        var service = new GeofenceService(Mock.Of<ISettingsService>(), mavLink.Object);

        service.SetGeofenceType(GeofenceType.Polygon);
        service.AddPolygonVertex(-6.2000, 106.8000);
        service.AddPolygonVertex(-6.2000, 106.8100);
        service.AddPolygonVertex(-6.2100, 106.8050);
        service.SetMaxAltitude(150);
        service.CompletePolygon();

        await service.SendGeofenceToVehicleAsync();

        mavLink.Verify(m => m.SetParameterAsync("FENCE_TYPE", 5), Times.Once);
        mavLink.Verify(m => m.SetParameterAsync("FENCE_TOTAL", 3), Times.Once);
        mavLink.Verify(m => m.SetParameterAsync("FENCE_ALT_MAX", 150), Times.Once);
        mavLink.Verify(m => m.SetParameterAsync("FENCE_ENABLE", 1), Times.Once);
        mavLink.Verify(m => m.SendMessage(It.Is<UasFencePoint>(p => p.Idx >= 1 && p.Idx <= 3 && p.Count == 3)), Times.Exactly(3));
    }

    [Fact]
    public async Task SendGeofenceToVehicleAsync_InactiveGeofence_ShouldDisableVehicleFenceOnly()
    {
        var mavLink = CreateConnectedMavLinkMock();
        var service = new GeofenceService(Mock.Of<ISettingsService>(), mavLink.Object);

        await service.SendGeofenceToVehicleAsync();

        mavLink.Verify(m => m.SetParameterAsync("FENCE_ENABLE", 0), Times.Once);
        mavLink.Verify(m => m.SetParameterAsync(It.Is<string>(name => name != "FENCE_ENABLE"), It.IsAny<float>()), Times.Never);
        mavLink.Verify(m => m.SendMessage(It.IsAny<UasMessage>()), Times.Never);
    }

    [Fact]
    public async Task SendGeofenceToVehicleAsync_WhenDisconnected_ShouldThrowClearError()
    {
        var mavLink = new Mock<IMavLinkService>();
        var service = new GeofenceService(Mock.Of<ISettingsService>(), mavLink.Object);
        service.SetGeofenceActive(true);

        var act = () => service.SendGeofenceToVehicleAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*MAVLink is not connected*");
    }

    [Fact]
    public void SetGeofenceActive_ShouldKeepCompatibilityIsEnabledInSync()
    {
        var service = new GeofenceService(Mock.Of<ISettingsService>());

        service.SetGeofenceActive(true);
        service.CurrentGeofence.IsActive.Should().BeTrue();
        service.CurrentGeofence.IsEnabled.Should().BeTrue();

        service.SetGeofenceActive(false);
        service.CurrentGeofence.IsActive.Should().BeFalse();
        service.CurrentGeofence.IsEnabled.Should().BeFalse();
    }

    private static Mock<IMavLinkService> CreateConnectedMavLinkMock()
    {
        var mavLink = new Mock<IMavLinkService>();
        mavLink.SetupGet(m => m.IsConnected).Returns(true);
        mavLink.Setup(m => m.SetParameterAsync(It.IsAny<string>(), It.IsAny<float>()))
            .ReturnsAsync(true);
        return mavLink;
    }
}
