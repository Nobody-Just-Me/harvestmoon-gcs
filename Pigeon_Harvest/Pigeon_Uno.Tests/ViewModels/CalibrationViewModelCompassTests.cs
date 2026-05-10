using Moq;
using Pigeon_Uno.Core.Services;
using Pigeon_Uno.Core.ViewModels;
using Pigeon_Uno.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Pigeon_Uno.Tests.ViewModels;

/// <summary>
/// Unit tests for CalibrationViewModel compass calibration functionality
/// Validates: Requirement 2 - Compass Calibration UI
/// Task 3.1: Implement Refresh Compass List button handler
/// </summary>
public class CalibrationViewModelCompassTests
{
    private readonly Mock<IMavLinkService> _mockMavLinkService;
    private readonly CalibrationViewModel _viewModel;

    public CalibrationViewModelCompassTests()
    {
        _mockMavLinkService = new Mock<IMavLinkService>();
        _viewModel = new CalibrationViewModel(_mockMavLinkService.Object);
    }

    [Fact]
    public async Task RefreshCompassListAsync_RequestsParametersFromMavLink()
    {
        // Arrange
        _mockMavLinkService
            .Setup(m => m.RequestParametersAsync())
            .Returns(Task.CompletedTask);
        
        _mockMavLinkService
            .Setup(m => m.GetParametersAsync())
            .ReturnsAsync(new Dictionary<string, float>());

        // Act
        await _viewModel.RefreshCompassListAsync();

        // Assert
        _mockMavLinkService.Verify(m => m.RequestParametersAsync(), Times.Once);
        _mockMavLinkService.Verify(m => m.GetParametersAsync(), Times.Once);
    }

    [Fact]
    public async Task RefreshCompassListAsync_ClearsExistingDevices()
    {
        // Arrange
        _viewModel.CompassDevices.Add(new CompassDevice { Number = 1, DevID = 12345, DevType = "Test" });
        
        _mockMavLinkService
            .Setup(m => m.RequestParametersAsync())
            .Returns(Task.CompletedTask);
        
        _mockMavLinkService
            .Setup(m => m.GetParametersAsync())
            .ReturnsAsync(new Dictionary<string, float>());

        // Act
        await _viewModel.RefreshCompassListAsync();

        // Assert - Should have default devices since no parameters were returned
        Assert.NotEmpty(_viewModel.CompassDevices);
    }

    [Fact]
    public async Task RefreshCompassListAsync_ParsesCompassDeviceParameters()
    {
        // Arrange
        var parameters = new Dictionary<string, float>
        {
            { "COMPASS_DEV_ID", 73225 },
            { "COMPASS_DEV_ID2", 73226 },
            { "COMPASS_DEV_ID3", 0 } // Device 3 not present
        };
        
        _mockMavLinkService
            .Setup(m => m.RequestParametersAsync())
            .Returns(Task.CompletedTask);
        
        _mockMavLinkService
            .Setup(m => m.GetParametersAsync())
            .ReturnsAsync(parameters);

        // Act
        await _viewModel.RefreshCompassListAsync();

        // Assert
        Assert.Equal(2, _viewModel.CompassDevices.Count);
        Assert.Equal(1, _viewModel.CompassDevices[0].Number);
        Assert.Equal(73225, _viewModel.CompassDevices[0].DevID);
        Assert.Equal(2, _viewModel.CompassDevices[1].Number);
        Assert.Equal(73226, _viewModel.CompassDevices[1].DevID);
    }

    [Fact]
    public async Task RefreshCompassListAsync_IgnoresZeroDeviceIds()
    {
        // Arrange
        var parameters = new Dictionary<string, float>
        {
            { "COMPASS_DEV_ID", 73225 },
            { "COMPASS_DEV_ID2", 0 }, // Should be ignored
            { "COMPASS_DEV_ID3", 0 }  // Should be ignored
        };
        
        _mockMavLinkService
            .Setup(m => m.RequestParametersAsync())
            .Returns(Task.CompletedTask);
        
        _mockMavLinkService
            .Setup(m => m.GetParametersAsync())
            .ReturnsAsync(parameters);

        // Act
        await _viewModel.RefreshCompassListAsync();

        // Assert
        Assert.Single(_viewModel.CompassDevices);
        Assert.Equal(1, _viewModel.CompassDevices[0].Number);
        Assert.Equal(73225, _viewModel.CompassDevices[0].DevID);
    }

    [Fact]
    public async Task RefreshCompassListAsync_AddsDefaultDevicesWhenNoParametersFound()
    {
        // Arrange
        _mockMavLinkService
            .Setup(m => m.RequestParametersAsync())
            .Returns(Task.CompletedTask);
        
        _mockMavLinkService
            .Setup(m => m.GetParametersAsync())
            .ReturnsAsync(new Dictionary<string, float>()); // Empty parameters

        // Act
        await _viewModel.RefreshCompassListAsync();

        // Assert
        Assert.Equal(2, _viewModel.CompassDevices.Count);
        Assert.Equal("HMC5883", _viewModel.CompassDevices[0].DevType);
        Assert.Equal("HMC5883", _viewModel.CompassDevices[1].DevType);
    }

    [Fact]
    public async Task RefreshCompassListAsync_RaisesStatusMessageChangedEvent()
    {
        // Arrange
        string? statusMessage = null;
        _viewModel.StatusMessageChanged += (sender, message) => statusMessage = message;
        
        _mockMavLinkService
            .Setup(m => m.RequestParametersAsync())
            .Returns(Task.CompletedTask);
        
        _mockMavLinkService
            .Setup(m => m.GetParametersAsync())
            .ReturnsAsync(new Dictionary<string, float>());

        // Act
        await _viewModel.RefreshCompassListAsync();

        // Assert
        Assert.NotNull(statusMessage);
        Assert.Contains("compass", statusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshCompassListAsync_HandlesExceptionGracefully()
    {
        // Arrange
        _mockMavLinkService
            .Setup(m => m.RequestParametersAsync())
            .ThrowsAsync(new System.Exception("Connection error"));

        // Act
        await _viewModel.RefreshCompassListAsync();

        // Assert - Should have default devices even on error
        Assert.Equal(2, _viewModel.CompassDevices.Count);
    }

    [Fact]
    public async Task RefreshCompassListAsync_IdentifiesHMC5883DeviceType()
    {
        // Arrange
        var parameters = new Dictionary<string, float>
        {
            { "COMPASS_DEV_ID", 0x02 } // HMC5883 device type
        };
        
        _mockMavLinkService
            .Setup(m => m.RequestParametersAsync())
            .Returns(Task.CompletedTask);
        
        _mockMavLinkService
            .Setup(m => m.GetParametersAsync())
            .ReturnsAsync(parameters);

        // Act
        await _viewModel.RefreshCompassListAsync();

        // Assert
        Assert.Single(_viewModel.CompassDevices);
        Assert.Equal("HMC5883", _viewModel.CompassDevices[0].DevType);
    }

    [Fact]
    public async Task RefreshCompassListAsync_IdentifiesAK8963DeviceType()
    {
        // Arrange
        var parameters = new Dictionary<string, float>
        {
            { "COMPASS_DEV_ID", 0x04 } // AK8963 device type
        };
        
        _mockMavLinkService
            .Setup(m => m.RequestParametersAsync())
            .Returns(Task.CompletedTask);
        
        _mockMavLinkService
            .Setup(m => m.GetParametersAsync())
            .ReturnsAsync(parameters);

        // Act
        await _viewModel.RefreshCompassListAsync();

        // Assert
        Assert.Single(_viewModel.CompassDevices);
        Assert.Equal("AK8963", _viewModel.CompassDevices[0].DevType);
    }

    [Fact]
    public async Task RefreshCompassListAsync_HandlesUnknownDeviceType()
    {
        // Arrange
        var parameters = new Dictionary<string, float>
        {
            { "COMPASS_DEV_ID", 0xFF } // Unknown device type
        };
        
        _mockMavLinkService
            .Setup(m => m.RequestParametersAsync())
            .Returns(Task.CompletedTask);
        
        _mockMavLinkService
            .Setup(m => m.GetParametersAsync())
            .ReturnsAsync(parameters);

        // Act
        await _viewModel.RefreshCompassListAsync();

        // Assert
        Assert.Single(_viewModel.CompassDevices);
        Assert.Contains("Unknown", _viewModel.CompassDevices[0].DevType);
    }

    #region Cancel Compass Calibration Tests (Task 3.5)

    /// <summary>
    /// Tests for Task 3.5: Implement Cancel button handler
    /// Validates: Requirement 2.5 - Cancel button functionality
    /// </summary>

    [Fact]
    public async Task CancelCompassCalibrationAsync_SendsCancelCommand()
    {
        // Arrange
        _mockMavLinkService
            .Setup(m => m.SendCommandLongAsync(
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>()))
            .Returns(Task.CompletedTask);

        // Act
        await _viewModel.CancelCompassCalibrationAsync();

        // Assert - Verify MAV_CMD_PREFLIGHT_CALIBRATION (241) with param2=0
        _mockMavLinkService.Verify(m => m.SendCommandLongAsync(
            241,  // MAV_CMD_PREFLIGHT_CALIBRATION
            0,    // param1
            0,    // param2 = 0 to cancel
            0,    // param3
            0,    // param4
            0,    // param5
            0,    // param6
            0),   // param7
            Times.Once);
    }

    [Fact]
    public async Task CancelCompassCalibrationAsync_ResetsProgressBars()
    {
        // Arrange
        int progress1 = -1;
        int progress2 = -1;
        _viewModel.CalibrationProgressChanged += (sender, progress) =>
        {
            progress1 = progress.progress1;
            progress2 = progress.progress2;
        };

        _mockMavLinkService
            .Setup(m => m.SendCommandLongAsync(
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>()))
            .Returns(Task.CompletedTask);

        // Act
        await _viewModel.CancelCompassCalibrationAsync();

        // Assert - Progress bars should be reset to 0
        Assert.Equal(0, progress1);
        Assert.Equal(0, progress2);
    }

    [Fact]
    public async Task CancelCompassCalibrationAsync_ResetsIsLoadingState()
    {
        // Arrange
        _viewModel.IsLoading = true; // Set loading state
        
        _mockMavLinkService
            .Setup(m => m.SendCommandLongAsync(
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>()))
            .Returns(Task.CompletedTask);

        // Act
        await _viewModel.CancelCompassCalibrationAsync();

        // Assert - IsLoading should be false after cancel
        Assert.False(_viewModel.IsLoading);
    }

    [Fact]
    public async Task CancelCompassCalibrationAsync_RaisesStatusMessageChangedEvent()
    {
        // Arrange
        var statusMessages = new List<string>();
        _viewModel.StatusMessageChanged += (sender, message) => statusMessages.Add(message);
        
        _mockMavLinkService
            .Setup(m => m.SendCommandLongAsync(
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>()))
            .Returns(Task.CompletedTask);

        // Act
        await _viewModel.CancelCompassCalibrationAsync();

        // Assert
        Assert.NotEmpty(statusMessages);
        Assert.Contains(statusMessages, m => m.Contains("cancel", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CancelCompassCalibrationAsync_HandlesExceptionGracefully()
    {
        // Arrange
        string? errorMessage = null;
        _viewModel.StatusMessageChanged += (sender, message) => errorMessage = message;
        
        _mockMavLinkService
            .Setup(m => m.SendCommandLongAsync(
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>()))
            .ThrowsAsync(new System.Exception("Connection error"));

        // Act & Assert - Should throw exception
        await Assert.ThrowsAsync<System.Exception>(
            async () => await _viewModel.CancelCompassCalibrationAsync());
        
        // Verify error message was raised
        Assert.NotNull(errorMessage);
        Assert.Contains("error", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancelCompassCalibrationAsync_SendsCorrectCommandParameters()
    {
        // Arrange
        float capturedParam1 = -1;
        float capturedParam2 = -1;
        
        _mockMavLinkService
            .Setup(m => m.SendCommandLongAsync(
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>()))
            .Callback<int, float, float, float, float, float, float, float>(
                (cmd, p1, p2, p3, p4, p5, p6, p7) =>
                {
                    capturedParam1 = p1;
                    capturedParam2 = p2;
                })
            .Returns(Task.CompletedTask);

        // Act
        await _viewModel.CancelCompassCalibrationAsync();

        // Assert - param1 should be 0, param2 should be 0 (cancel)
        Assert.Equal(0, capturedParam1);
        Assert.Equal(0, capturedParam2);
    }

    [Fact]
    public async Task CancelCompassCalibrationAsync_AfterStartCalibration_ResetsProgress()
    {
        // Arrange - Start calibration first
        _mockMavLinkService
            .Setup(m => m.SendCommandLongAsync(
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>()))
            .Returns(Task.CompletedTask);

        await _viewModel.StartCompassCalibrationAsync();
        
        // Wait a bit for simulated progress
        await Task.Delay(100);
        
        int finalProgress1 = -1;
        int finalProgress2 = -1;
        _viewModel.CalibrationProgressChanged += (sender, progress) =>
        {
            finalProgress1 = progress.progress1;
            finalProgress2 = progress.progress2;
        };

        // Act - Cancel calibration
        await _viewModel.CancelCompassCalibrationAsync();

        // Assert - Progress should be reset to 0
        Assert.Equal(0, finalProgress1);
        Assert.Equal(0, finalProgress2);
        Assert.False(_viewModel.IsLoading);
    }

    #endregion

    #region Reboot Vehicle Tests (Task 3.6)

    /// <summary>
    /// Tests for Task 3.6: Implement Reboot button handler
    /// Validates: Requirement 2.6 - Reboot button functionality
    /// </summary>

    [Fact]
    public async Task RebootVehicleAsync_SendsRebootCommand()
    {
        // Arrange
        _mockMavLinkService
            .Setup(m => m.SendCommandLongAsync(
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>()))
            .Returns(Task.CompletedTask);

        // Act
        await _viewModel.RebootVehicleAsync();

        // Assert - Verify MAV_CMD_PREFLIGHT_REBOOT_SHUTDOWN (246) with param1=1
        _mockMavLinkService.Verify(m => m.SendCommandLongAsync(
            246,  // MAV_CMD_PREFLIGHT_REBOOT_SHUTDOWN
            1,    // param1 = 1 to reboot autopilot
            0,    // param2
            0,    // param3
            0,    // param4
            0,    // param5
            0,    // param6
            0),   // param7
            Times.Once);
    }

    [Fact]
    public async Task RebootVehicleAsync_RaisesStatusMessageChangedEvent()
    {
        // Arrange
        var statusMessages = new List<string>();
        _viewModel.StatusMessageChanged += (sender, message) => statusMessages.Add(message);
        
        _mockMavLinkService
            .Setup(m => m.SendCommandLongAsync(
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>()))
            .Returns(Task.CompletedTask);

        // Act
        await _viewModel.RebootVehicleAsync();

        // Assert - Should raise status messages about reboot
        Assert.NotEmpty(statusMessages);
        Assert.Contains(statusMessages, m => m.Contains("reboot", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(statusMessages, m => m.Contains("restart", StringComparison.OrdinalIgnoreCase) || 
                                             m.Contains("reboot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RebootVehicleAsync_SendsCorrectCommandParameters()
    {
        // Arrange
        int capturedCommand = -1;
        float capturedParam1 = -1;
        
        _mockMavLinkService
            .Setup(m => m.SendCommandLongAsync(
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>()))
            .Callback<int, float, float, float, float, float, float, float>(
                (cmd, p1, p2, p3, p4, p5, p6, p7) =>
                {
                    capturedCommand = cmd;
                    capturedParam1 = p1;
                })
            .Returns(Task.CompletedTask);

        // Act
        await _viewModel.RebootVehicleAsync();

        // Assert - Command should be 246, param1 should be 1 (reboot autopilot)
        Assert.Equal(246, capturedCommand);
        Assert.Equal(1, capturedParam1);
    }

    [Fact]
    public async Task RebootVehicleAsync_HandlesExceptionGracefully()
    {
        // Arrange
        string? errorMessage = null;
        _viewModel.StatusMessageChanged += (sender, message) => errorMessage = message;
        
        _mockMavLinkService
            .Setup(m => m.SendCommandLongAsync(
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>()))
            .ThrowsAsync(new System.Exception("Connection error"));

        // Act & Assert - Should throw exception
        await Assert.ThrowsAsync<System.Exception>(
            async () => await _viewModel.RebootVehicleAsync());
        
        // Verify error message was raised
        Assert.NotNull(errorMessage);
        Assert.Contains("error", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RebootVehicleAsync_DisplaysConfirmationMessage()
    {
        // Arrange
        var statusMessages = new List<string>();
        _viewModel.StatusMessageChanged += (sender, message) => statusMessages.Add(message);
        
        _mockMavLinkService
            .Setup(m => m.SendCommandLongAsync(
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>()))
            .Returns(Task.CompletedTask);

        // Act
        await _viewModel.RebootVehicleAsync();

        // Assert - Should display confirmation that command was sent
        Assert.Contains(statusMessages, m => 
            m.Contains("sent", StringComparison.OrdinalIgnoreCase) ||
            m.Contains("restart", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RebootVehicleAsync_SendsCommandOnlyOnce()
    {
        // Arrange
        _mockMavLinkService
            .Setup(m => m.SendCommandLongAsync(
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>()))
            .Returns(Task.CompletedTask);

        // Act
        await _viewModel.RebootVehicleAsync();

        // Assert - Command should be sent exactly once
        _mockMavLinkService.Verify(m => m.SendCommandLongAsync(
            It.IsAny<int>(),
            It.IsAny<float>(),
            It.IsAny<float>(),
            It.IsAny<float>(),
            It.IsAny<float>(),
            It.IsAny<float>(),
            It.IsAny<float>(),
            It.IsAny<float>()),
            Times.Once);
    }

    [Fact]
    public async Task RebootVehicleAsync_UsesCorrectMavLinkCommandId()
    {
        // Arrange
        int capturedCommand = -1;
        
        _mockMavLinkService
            .Setup(m => m.SendCommandLongAsync(
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>()))
            .Callback<int, float, float, float, float, float, float, float>(
                (cmd, p1, p2, p3, p4, p5, p6, p7) => capturedCommand = cmd)
            .Returns(Task.CompletedTask);

        // Act
        await _viewModel.RebootVehicleAsync();

        // Assert - MAV_CMD_PREFLIGHT_REBOOT_SHUTDOWN = 246
        Assert.Equal(246, capturedCommand);
    }

    #endregion
}

