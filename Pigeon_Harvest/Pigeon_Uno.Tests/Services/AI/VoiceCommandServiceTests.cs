using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Pigeon_Uno.Core.Models;
using Pigeon_Uno.Core.Models.AI;
using Pigeon_Uno.Core.Services;
using Pigeon_Uno.Core.Services.AI;
using Xunit;

namespace Pigeon_Uno.Tests.Services.AI;

public class VoiceCommandServiceTests
{
    [Fact]
    public async Task ProcessTextAsync_UnknownCommand_ReturnsInvalidResult()
    {
        var mav = new Mock<IMavLinkService>();
        mav.SetupGet(m => m.IsConnected).Returns(true);
        var settings = CreateSettings(enabled: true);
        var service = new VoiceCommandService(mav.Object, settings);

        var result = await service.ProcessTextAsync("ini perintah random tidak dikenal");

        result.Should().NotBeNull();
        result.Command.Should().Be(VoiceCommand.Unknown);
        result.IsValid.Should().BeFalse();
        result.IsExecuted.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessTextAsync_TakeoffWhenNotArmed_IsRejectedByValidator()
    {
        var mav = new Mock<IMavLinkService>();
        mav.SetupGet(m => m.IsConnected).Returns(true);

        var settings = CreateSettings(enabled: true);
        var service = new VoiceCommandService(mav.Object, settings);
        service.UpdateTelemetrySnapshot(new TelemetrySnapshot
        {
            Timestamp = DateTime.UtcNow,
            Armed = false,
            BatteryPercent = 85,
            GpsSatellites = 10,
            Altitude = 0
        });

        var result = await service.ProcessTextAsync("takeoff");

        result.Command.Should().Be(VoiceCommand.Takeoff);
        result.IsValid.Should().BeFalse();
        result.IsExecuted.Should().BeFalse();
        result.Message.Should().Contain("belum ARM");
    }

    [Fact]
    public async Task ProcessTextAsync_ArmWithValidState_ExecutesArmCommand()
    {
        var mav = new Mock<IMavLinkService>();
        mav.SetupGet(m => m.IsConnected).Returns(true);
        mav.Setup(m => m.ArmDisarmAsync(true)).ReturnsAsync(true);

        var settings = CreateSettings(enabled: true);
        var service = new VoiceCommandService(mav.Object, settings);
        service.UpdateTelemetrySnapshot(new TelemetrySnapshot
        {
            Timestamp = DateTime.UtcNow,
            Armed = false,
            BatteryPercent = 90,
            GpsSatellites = 11,
            Altitude = 0
        });

        var firstResult = await service.ProcessTextAsync("arm drone");
        var result = await service.ProcessTextAsync("konfirmasi arm");
        await Task.Delay(50);

        firstResult.Command.Should().Be(VoiceCommand.Arm);
        firstResult.IsValid.Should().BeTrue();
        firstResult.IsExecuted.Should().BeFalse();
        firstResult.Message.Should().Contain("konfirmasi");

        result.Command.Should().Be(VoiceCommand.Arm);
        result.IsValid.Should().BeTrue();
        result.IsExecuted.Should().BeTrue();
        mav.Verify(m => m.ArmDisarmAsync(true), Times.Once);
    }

    [Fact]
    public async Task StartListeningAsync_WhenVoiceDisabled_RaisesRecognitionError()
    {
        var mav = new Mock<IMavLinkService>();
        var settings = CreateSettings(enabled: false);
        var service = new VoiceCommandService(mav.Object, settings);

        var raised = false;
        service.RecognitionError += (_, _) => raised = true;

        await service.StartListeningAsync();

        raised.Should().BeTrue();
        service.IsListening.Should().BeFalse();
    }

    [Fact]
    public async Task StartListeningAsync_WithoutVoiceEngine_RaisesRecognitionError()
    {
        var mav = new Mock<IMavLinkService>();
        var settings = CreateSettings(enabled: true);
        var service = new VoiceCommandService(mav.Object, settings);

        var raised = false;
        service.RecognitionError += (_, msg) =>
        {
            raised = msg.Contains("belum terdaftar", StringComparison.OrdinalIgnoreCase);
        };

        await service.StartListeningAsync();

        raised.Should().BeTrue();
        service.IsListening.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessTextAsync_TakePhoto_UsesCameraServiceWithoutMavConnection()
    {
        var mav = new Mock<IMavLinkService>();
        mav.SetupGet(m => m.IsConnected).Returns(false);
        var camera = new Mock<ICameraService>();
        camera.Setup(c => c.TakePictureAsync(It.IsAny<string>())).ReturnsAsync(true);

        var settings = CreateSettings(enabled: true);
        var service = new VoiceCommandService(mav.Object, settings, null, camera.Object);

        var result = await service.ProcessTextAsync("ambil foto");

        result.Command.Should().Be(VoiceCommand.TakePhoto);
        result.IsValid.Should().BeTrue();
        result.IsExecuted.Should().BeTrue();
        camera.Verify(c => c.TakePictureAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ProcessTextAsync_EnableGeofence_SetsFenceParameter()
    {
        var mav = new Mock<IMavLinkService>();
        mav.SetupGet(m => m.IsConnected).Returns(true);
        mav.Setup(m => m.SetParameterAsync("FENCE_ENABLE", 1)).ReturnsAsync(true);

        var settings = CreateSettings(enabled: true);
        var service = new VoiceCommandService(mav.Object, settings);

        var result = await service.ProcessTextAsync("aktifkan geofence");

        result.Command.Should().Be(VoiceCommand.EnableGeofence);
        result.IsValid.Should().BeTrue();
        result.IsExecuted.Should().BeTrue();
        mav.Verify(m => m.SetParameterAsync("FENCE_ENABLE", 1), Times.Once);
    }

    [Fact]
    public async Task ProcessTextAsync_Status_WithTelemetry_ReturnsStatusMessage()
    {
        var settings = CreateSettings(enabled: true);
        var service = new VoiceCommandService(null, settings);
        service.UpdateTelemetrySnapshot(new TelemetrySnapshot
        {
            Timestamp = DateTime.UtcNow,
            Armed = true,
            BatteryPercent = 88,
            BatteryVoltage = 12.3,
            GpsSatellites = 11,
            GpsHdop = 0.8,
            Altitude = 42,
            Speed = 13.2
        });

        var result = await service.ProcessTextAsync("status drone");

        result.Command.Should().Be(VoiceCommand.Status);
        result.IsValid.Should().BeTrue();
        result.IsExecuted.Should().BeTrue();
        result.Message.Should().Contain("battery");
        result.Message.Should().Contain("GPS");
    }

    [Fact]
    public async Task ProcessTextAsync_CancelCriticalCommand_ClearsPendingConfirmation()
    {
        var mav = new Mock<IMavLinkService>();
        mav.SetupGet(m => m.IsConnected).Returns(true);
        mav.Setup(m => m.ArmDisarmAsync(true)).ReturnsAsync(true);

        var settings = CreateSettings(enabled: true);
        var service = new VoiceCommandService(mav.Object, settings);
        service.UpdateTelemetrySnapshot(new TelemetrySnapshot
        {
            Timestamp = DateTime.UtcNow,
            Armed = false,
            BatteryPercent = 90,
            GpsSatellites = 10,
            Altitude = 0
        });

        var armResult = await service.ProcessTextAsync("arm drone");
        var cancelResult = await service.ProcessTextAsync("batal");
        var confirmResult = await service.ProcessTextAsync("konfirmasi arm");

        armResult.IsExecuted.Should().BeFalse();
        cancelResult.IsValid.Should().BeTrue();
        confirmResult.IsExecuted.Should().BeFalse();
        confirmResult.Message.Should().Contain("Tidak ada perintah kritis");
        mav.Verify(m => m.ArmDisarmAsync(true), Times.Never);
    }

    [Theory]
    [InlineData("arm drone", VoiceCommand.Arm)]
    [InlineData("disarm drone", VoiceCommand.Disarm)]
    [InlineData("takeoff", VoiceCommand.Takeoff)]
    [InlineData("land", VoiceCommand.Land)]
    [InlineData("return to launch", VoiceCommand.ReturnToLaunch)]
    [InlineData("pause mission", VoiceCommand.PauseMission)]
    [InlineData("resume mission", VoiceCommand.ResumeMission)]
    [InlineData("start mission", VoiceCommand.StartMission)]
    [InlineData("emergency stop", VoiceCommand.EmergencyStop)]
    [InlineData("mode stabilize", VoiceCommand.ModeStabilize)]
    [InlineData("mode loiter", VoiceCommand.ModeLoiter)]
    [InlineData("mode auto", VoiceCommand.ModeAuto)]
    [InlineData("mode guided", VoiceCommand.ModeGuided)]
    [InlineData("mode circle", VoiceCommand.ModeCircle)]
    [InlineData("mode follow", VoiceCommand.ModeFollow)]
    [InlineData("mode poshold", VoiceCommand.ModePoshold)]
    [InlineData("mode acro", VoiceCommand.ModeAcro)]
    [InlineData("go to waypoint 3", VoiceCommand.GoToWaypoint)]
    [InlineData("next waypoint 4", VoiceCommand.NextWaypoint)]
    [InlineData("clear mission", VoiceCommand.ClearMission)]
    [InlineData("hold position", VoiceCommand.HoldPosition)]
    [InlineData("set home", VoiceCommand.SetHome)]
    [InlineData("request logs", VoiceCommand.RequestLogs)]
    [InlineData("set speed 10", VoiceCommand.SetSpeed)]
    [InlineData("set altitude 50", VoiceCommand.SetAltitude)]
    [InlineData("gimbal down", VoiceCommand.GimbalDown)]
    [InlineData("gimbal up", VoiceCommand.GimbalUp)]
    [InlineData("gimbal forward", VoiceCommand.GimbalForward)]
    [InlineData("center camera", VoiceCommand.CenterCamera)]
    [InlineData("enable geofence", VoiceCommand.EnableGeofence)]
    [InlineData("disable geofence", VoiceCommand.DisableGeofence)]
    [InlineData("take photo", VoiceCommand.TakePhoto)]
    [InlineData("start recording", VoiceCommand.StartRecording)]
    [InlineData("stop recording", VoiceCommand.StopRecording)]
    [InlineData("zoom in", VoiceCommand.ZoomIn)]
    [InlineData("zoom out", VoiceCommand.ZoomOut)]
    [InlineData("status drone", VoiceCommand.Status)]
    [InlineData("status baterai", VoiceCommand.BatteryCheck)]
    [InlineData("status gps", VoiceCommand.GpsCheck)]
    [InlineData("cek altitude", VoiceCommand.AltitudeCheck)]
    [InlineData("cek speed", VoiceCommand.SpeedCheck)]
    public async Task ProcessTextAsync_AllSupportedCommands_DoNotUseFallbackMessage(string input, VoiceCommand expected)
    {
        var mav = new Mock<IMavLinkService>();
        mav.SetupGet(m => m.IsConnected).Returns(true);
        mav.SetReturnsDefault(Task.FromResult(true));
        mav.SetReturnsDefault(Task.CompletedTask);

        var camera = new Mock<ICameraService>();
        camera.SetReturnsDefault(Task.FromResult(true));
        camera.SetReturnsDefault(Task.CompletedTask);

        var settings = CreateSettings(enabled: true);
        var service = new VoiceCommandService(mav.Object, settings, null, camera.Object);
        service.UpdateTelemetrySnapshot(new TelemetrySnapshot
        {
            Timestamp = DateTime.UtcNow,
            Armed = true,
            BatteryPercent = 90,
            BatteryVoltage = 12.3,
            GpsSatellites = 11,
            GpsHdop = 0.8,
            Altitude = 0,
            Speed = 12
        });

        var result = await service.ProcessTextAsync(input);

        result.Command.Should().Be(expected);
        result.Message.Should().NotContain("belum tersedia di versi ini");
    }

    [Fact]
    public async Task ProcessTextAsync_PersistsAuditEntry_WhenHistoryStoreRegistered()
    {
        var mav = new Mock<IMavLinkService>();
        mav.SetupGet(m => m.IsConnected).Returns(true);
        mav.Setup(m => m.ArmDisarmAsync(true)).ReturnsAsync(true);

        var history = new Mock<IPIAHistoryStore>();
        history.Setup(h => h.SaveCommandAuditEntryAsync(It.IsAny<CommandAuditEntry>(), default))
            .Returns(Task.CompletedTask);

        var settings = CreateSettings(enabled: true);
        var service = new VoiceCommandService(mav.Object, settings, null, null, history.Object);
        service.UpdateTelemetrySnapshot(new TelemetrySnapshot
        {
            Timestamp = DateTime.UtcNow,
            Armed = false,
            BatteryPercent = 92,
            GpsSatellites = 10,
            Altitude = 0
        });

        _ = await service.ProcessTextAsync("arm drone");
        var result = await service.ProcessTextAsync("konfirmasi arm");
        await Task.Delay(30);

        result.Command.Should().Be(VoiceCommand.Arm);
        history.Verify(h => h.SaveCommandAuditEntryAsync(
            It.Is<CommandAuditEntry>(e =>
                e.Command == VoiceCommand.Arm &&
                e.IsValid &&
                e.IsExecuted &&
                e.Confidence > 0),
            default), Times.Once);
    }

    private static AISettings CreateSettings(bool enabled)
    {
        return new AISettings
        {
            VoiceCommand = new VoiceCommandConfig
            {
                Enabled = enabled,
                ConfidenceThreshold = 0.6,
                Language = "id-ID"
            }
        };
    }
}
