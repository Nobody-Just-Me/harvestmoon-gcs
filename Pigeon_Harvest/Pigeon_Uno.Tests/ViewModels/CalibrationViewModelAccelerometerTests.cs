using Xunit;
using Pigeon_Uno.Core.ViewModels;
using Pigeon_Uno.Core.Services;
using Moq;
using System.Threading.Tasks;

namespace Pigeon_Uno.Tests.ViewModels;

/// <summary>
/// Unit tests for CalibrationViewModel accelerometer calibration functionality
/// Validates: Requirement 1 - Accelerometer Calibration UI
/// Task 2.1.1: Send MAV_CMD_PREFLIGHT_CALIBRATION with param5=1
/// </summary>
public class CalibrationViewModelAccelerometerTests
{
    private readonly Mock<IMavLinkService> _mockMavLinkService;
    private readonly CalibrationViewModel _viewModel;

    public CalibrationViewModelAccelerometerTests()
    {
        _mockMavLinkService = new Mock<IMavLinkService>();
        _viewModel = new CalibrationViewModel(_mockMavLinkService.Object);
    }

    /// <summary>
    /// Test that StartAccelerometerCalibrationAsync sends correct MAVLink command
    /// Validates: Requirement 1.2 - Start accelerometer calibration via MAVLink
    /// Task 2.1.1: Send MAV_CMD_PREFLIGHT_CALIBRATION with param5=1
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.1.1")]
    public async Task StartAccelerometerCalibrationAsync_SendsCorrectMAVLinkCommand()
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
        await _viewModel.StartAccelerometerCalibrationAsync();

        // Assert - Verify MAV_CMD_PREFLIGHT_CALIBRATION (241) with param5=1
        _mockMavLinkService.Verify(
            m => m.SendCommandLongAsync(
                241,  // MAV_CMD_PREFLIGHT_CALIBRATION
                0,    // param1
                0,    // param2
                0,    // param3
                0,    // param4
                1,    // param5 = 1 for accelerometer calibration
                0,    // param6
                0),   // param7
            Times.Once);
    }

    /// <summary>
    /// Test that StartAccelerometerCalibrationAsync sets IsLoading to true during calibration
    /// Validates: Requirement 1.2 - Loading state during calibration
    /// Task 2.1.2: Update IsLoading state
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.1.2")]
    public async Task StartAccelerometerCalibrationAsync_SetsIsLoadingTrue_DuringCalibration()
    {
        // Arrange
        bool isLoadingDuringCalibration = false;
        var taskCompletionSource = new TaskCompletionSource<bool>();

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
            .Returns(async () =>
            {
                // Capture IsLoading state during command execution
                isLoadingDuringCalibration = _viewModel.IsLoading;
                await Task.Delay(10);
                return;
            });

        // Act
        await _viewModel.StartAccelerometerCalibrationAsync();

        // Assert
        Assert.True(isLoadingDuringCalibration);
    }

    /// <summary>
    /// Test that StartAccelerometerCalibrationAsync resets IsLoading to false after completion
    /// Validates: Requirement 1.2 - Loading state reset after calibration
    /// Task 2.1.2: Update IsLoading state
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.1.2")]
    public async Task StartAccelerometerCalibrationAsync_ResetsIsLoadingFalse_AfterCompletion()
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
        await _viewModel.StartAccelerometerCalibrationAsync();

        // Assert
        Assert.False(_viewModel.IsLoading);
    }

    /// <summary>
    /// Test that StartAccelerometerCalibrationAsync resets IsLoading even on error
    /// Validates: Requirement 10.3 - Error handling resets UI state
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorHandling")]
    public async Task StartAccelerometerCalibrationAsync_ResetsIsLoadingFalse_OnError()
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
            .ThrowsAsync(new System.Exception("MAVLink connection error"));

        // Act & Assert
        await Assert.ThrowsAsync<System.Exception>(
            async () => await _viewModel.StartAccelerometerCalibrationAsync());

        // IsLoading should be reset to false even on error
        Assert.False(_viewModel.IsLoading);
    }

    /// <summary>
    /// Test that IsLoadingChanged event is raised when IsLoading changes
    /// Validates: Requirement 11.2 - Real-time feedback via events
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Events")]
    public async Task StartAccelerometerCalibrationAsync_RaisesIsLoadingChangedEvent()
    {
        // Arrange
        int eventRaisedCount = 0;
        bool? lastEventValue = null;

        _viewModel.IsLoadingChanged += (sender, isLoading) =>
        {
            eventRaisedCount++;
            lastEventValue = isLoading;
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
        await _viewModel.StartAccelerometerCalibrationAsync();

        // Assert - Event should be raised at least twice (true when starting, false when done)
        Assert.True(eventRaisedCount >= 2);
        Assert.False(lastEventValue); // Last event should be false
    }

    /// <summary>
    /// Test that StartSimpleAccelCalibrationAsync sends correct MAVLink command
    /// Validates: Requirement 1.4 - Simple accelerometer calibration
    /// Task 2.2.1: Send MAV_CMD_PREFLIGHT_CALIBRATION with param5=4
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.2.1")]
    public async Task StartSimpleAccelCalibrationAsync_SendsCorrectMAVLinkCommand()
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
        await _viewModel.StartSimpleAccelCalibrationAsync();

        // Assert - Verify MAV_CMD_PREFLIGHT_CALIBRATION (241) with param5=4
        _mockMavLinkService.Verify(
            m => m.SendCommandLongAsync(
                241,  // MAV_CMD_PREFLIGHT_CALIBRATION
                0,    // param1
                0,    // param2
                0,    // param3
                0,    // param4
                4,    // param5 = 4 for simple accel calibration
                0,    // param6
                0),   // param7
            Times.Once);
    }

    /// <summary>
    /// Test that StartSimpleAccelCalibrationAsync displays status messages
    /// Validates: Requirement 1.4, 1.6 - Status message display during simple calibration
    /// Task 2.2.2: Display status message
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.2.2")]
    public async Task StartSimpleAccelCalibrationAsync_DisplaysStatusMessages()
    {
        // Arrange
        var statusMessages = new System.Collections.Generic.List<string>();

        _viewModel.StatusMessageChanged += (sender, message) =>
        {
            statusMessages.Add(message);
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
        await _viewModel.StartSimpleAccelCalibrationAsync();

        // Assert - Should have at least 3 status messages
        Assert.True(statusMessages.Count >= 3);
        Assert.Contains("Starting simple accelerometer calibration", statusMessages[0]);
        Assert.Contains("command sent", statusMessages[1]);
        Assert.Contains("complete", statusMessages[2]);
    }

    /// <summary>
    /// Test that StartSimpleAccelCalibrationAsync displays error message on failure
    /// Validates: Requirement 10.3 - Error handling displays error messages
    /// Task 2.2.2: Display status message (error case)
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.2.2")]
    public async Task StartSimpleAccelCalibrationAsync_DisplaysErrorMessage_OnFailure()
    {
        // Arrange
        string? errorMessage = null;

        _viewModel.StatusMessageChanged += (sender, message) =>
        {
            if (message.Contains("Error"))
            {
                errorMessage = message;
            }
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
            .ThrowsAsync(new System.Exception("MAVLink connection error"));

        // Act
        await _viewModel.StartSimpleAccelCalibrationAsync();

        // Assert - Should display error message
        Assert.NotNull(errorMessage);
        Assert.Contains("Error", errorMessage);
        Assert.Contains("simple accel calibration", errorMessage);
    }

    /// <summary>
    /// Test that StartLevelCalibrationAsync sends correct MAVLink command
    /// Validates: Requirement 1.5 - Level calibration
    /// Task 2.3.1: Send MAV_CMD_PREFLIGHT_CALIBRATION with param5=2
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.3.1")]
    public async Task StartLevelCalibrationAsync_SendsCorrectMAVLinkCommand()
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
        await _viewModel.StartLevelCalibrationAsync();

        // Assert - Verify MAV_CMD_PREFLIGHT_CALIBRATION (241) with param5=2
        _mockMavLinkService.Verify(
            m => m.SendCommandLongAsync(
                241,  // MAV_CMD_PREFLIGHT_CALIBRATION
                0,    // param1
                0,    // param2
                0,    // param3
                0,    // param4
                2,    // param5 = 2 for level calibration
                0,    // param6
                0),   // param7
            Times.Once);
    }

    /// <summary>
    /// Test that StartLevelCalibrationAsync displays status messages
    /// Validates: Requirement 1.5, 1.6 - Status message display during level calibration
    /// Task 2.3.2: Display status message
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.3.2")]
    public async Task StartLevelCalibrationAsync_DisplaysStatusMessages()
    {
        // Arrange
        var statusMessages = new System.Collections.Generic.List<string>();

        _viewModel.StatusMessageChanged += (sender, message) =>
        {
            statusMessages.Add(message);
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
        await _viewModel.StartLevelCalibrationAsync();

        // Assert - Should have at least 3 status messages
        Assert.True(statusMessages.Count >= 3);
        Assert.Contains("Starting level calibration", statusMessages[0]);
        Assert.Contains("command sent", statusMessages[1]);
        Assert.Contains("complete", statusMessages[2]);
    }

    /// <summary>
    /// Test that StartLevelCalibrationAsync displays error message on failure
    /// Validates: Requirement 10.3 - Error handling displays error messages
    /// Task 2.3.2: Display status message (error case)
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.3.2")]
    public async Task StartLevelCalibrationAsync_DisplaysErrorMessage_OnFailure()
    {
        // Arrange
        string? errorMessage = null;

        _viewModel.StatusMessageChanged += (sender, message) =>
        {
            if (message.Contains("Error"))
            {
                errorMessage = message;
            }
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
            .ThrowsAsync(new System.Exception("MAVLink connection error"));

        // Act
        await _viewModel.StartLevelCalibrationAsync();

        // Assert - Should display error message
        Assert.NotNull(errorMessage);
        Assert.Contains("Error", errorMessage);
        Assert.Contains("level calibration", errorMessage);
    }

    /// <summary>
    /// Test that CalibrationStepCompleted event is raised for each step
    /// Validates: Requirement 1.3 - Visual indicator updates for each position
    /// Task 2.4.3: Raise CalibrationStepCompleted event
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.4.3")]
    public async Task StartAccelerometerCalibrationAsync_RaisesCalibrationStepCompletedEvent()
    {
        // Arrange
        var completedSteps = new System.Collections.Generic.List<int>();

        _viewModel.CalibrationStepCompleted += (sender, step) =>
        {
            completedSteps.Add(step);
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
        await _viewModel.StartAccelerometerCalibrationAsync();

        // Assert - All 6 steps should be completed
        Assert.Equal(6, completedSteps.Count);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, completedSteps);
    }

    /// <summary>
    /// Test that SetAccelerometerPosition sends correct MAVLink command
    /// Validates: Requirement 1.3 - Set accelerometer position during calibration
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task SetAccelerometerPosition_SendsCorrectMAVLinkCommand()
    {
        // Arrange
        int position = 3;

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
        await _viewModel.SetAccelerometerPosition(position);

        // Assert - Verify MAV_CMD_ACCELCAL_VEHICLE_POS (42429)
        _mockMavLinkService.Verify(
            m => m.SendCommandLongAsync(
                42429,    // MAV_CMD_ACCELCAL_VEHICLE_POS
                position, // param1 = position
                0,        // param2
                0,        // param3
                0,        // param4
                0,        // param5
                0,        // param6
                0),       // param7
            Times.Once);
    }

    /// <summary>
    /// Test that multiple calibration commands can be sent sequentially
    /// Validates: Requirement 9.2 - Multiple calibration command support
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task AccelerometerCalibration_SupportsMultipleSequentialCommands()
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

        // Act - Send multiple calibration commands
        await _viewModel.StartAccelerometerCalibrationAsync();
        await _viewModel.StartSimpleAccelCalibrationAsync();
        await _viewModel.StartLevelCalibrationAsync();

        // Assert - All commands should be sent
        _mockMavLinkService.Verify(
            m => m.SendCommandLongAsync(
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<float>()),
            Times.Exactly(3));
    }
}
