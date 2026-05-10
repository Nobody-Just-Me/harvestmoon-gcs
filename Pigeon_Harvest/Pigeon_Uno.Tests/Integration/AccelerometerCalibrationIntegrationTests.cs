using Xunit;
using Pigeon_Uno.Core.ViewModels;
using Pigeon_Uno.Core.Services;
using Moq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace Pigeon_Uno.Tests.Integration;

/// <summary>
/// Integration tests for complete accelerometer calibration workflow
/// Validates: Requirement 1 - Accelerometer Calibration UI
/// Task 2.5: Test complete 6-position calibration workflow
/// </summary>
public class AccelerometerCalibrationIntegrationTests
{
    private readonly Mock<IMavLinkService> _mockMavLinkService;
    private readonly CalibrationViewModel _viewModel;

    public AccelerometerCalibrationIntegrationTests()
    {
        _mockMavLinkService = new Mock<IMavLinkService>();
        _viewModel = new CalibrationViewModel(_mockMavLinkService.Object);
    }

    /// <summary>
    /// Test complete 6-position calibration workflow
    /// Validates: Requirements 1.2, 1.3 - Complete calibration sequence
    /// Task 2.5: Test complete 6-position calibration workflow
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Task2.5")]
    public async Task Complete6PositionCalibration_CompletesAllPositionsInSequence()
    {
        // Arrange
        var completedSteps = new List<int>();
        var isLoadingStates = new List<bool>();

        _viewModel.CalibrationStepCompleted += (sender, step) =>
        {
            completedSteps.Add(step);
        };

        _viewModel.IsLoadingChanged += (sender, isLoading) =>
        {
            isLoadingStates.Add(isLoading);
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

        // Assert - All 6 positions completed in sequence
        Assert.Equal(6, completedSteps.Count);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, completedSteps);

        // Assert - Loading state changed (true at start, false at end)
        Assert.Contains(true, isLoadingStates);
        Assert.Contains(false, isLoadingStates);
        Assert.False(_viewModel.IsLoading); // Final state should be false

        // Assert - MAVLink command was sent
        _mockMavLinkService.Verify(
            m => m.SendCommandLongAsync(
                241,  // MAV_CMD_PREFLIGHT_CALIBRATION
                0, 0, 0, 0,
                1,    // param5 = 1 for accelerometer
                0, 0),
            Times.Once);
    }

    /// <summary>
    /// Test that calibration positions complete sequentially (not simultaneously)
    /// Validates: Requirement 1.3 - Sequential position completion
    /// Task 2.5: Test complete 6-position calibration workflow
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Task2.5")]
    public async Task Complete6PositionCalibration_PositionsCompleteSequentially()
    {
        // Arrange
        var completedSteps = new List<(int step, System.DateTime time)>();

        _viewModel.CalibrationStepCompleted += (sender, step) =>
        {
            completedSteps.Add((step, System.DateTime.UtcNow));
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

        // Assert - Each position completes after the previous one
        for (int i = 0; i < completedSteps.Count - 1; i++)
        {
            Assert.True(completedSteps[i].time <= completedSteps[i + 1].time,
                $"Position {completedSteps[i].step} should complete before position {completedSteps[i + 1].step}");
        }
    }

    /// <summary>
    /// Test that visual feedback is provided for each completed position
    /// Validates: Requirement 1.3 - Visual indicator updates
    /// Task 2.5: Test complete 6-position calibration workflow
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Task2.5")]
    public async Task Complete6PositionCalibration_ProvidesVisualFeedbackForEachPosition()
    {
        // Arrange
        var visualFeedbackReceived = new Dictionary<int, bool>();

        _viewModel.CalibrationStepCompleted += (sender, step) =>
        {
            // Simulate visual feedback (border color change, text update)
            visualFeedbackReceived[step] = true;
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

        // Assert - Visual feedback received for all 6 positions
        Assert.Equal(6, visualFeedbackReceived.Count);
        for (int i = 1; i <= 6; i++)
        {
            Assert.True(visualFeedbackReceived.ContainsKey(i),
                $"Visual feedback should be received for position {i}");
            Assert.True(visualFeedbackReceived[i],
                $"Visual feedback for position {i} should be true");
        }
    }

    /// <summary>
    /// Test that calibration workflow handles errors gracefully
    /// Validates: Requirement 10.3 - Error handling during calibration
    /// Task 2.5: Test complete 6-position calibration workflow
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Task2.5")]
    public async Task Complete6PositionCalibration_HandlesErrorsGracefully()
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

        // Assert - IsLoading should be reset to false even on error
        Assert.False(_viewModel.IsLoading);
    }

    /// <summary>
    /// Test that calibration can be restarted after completion
    /// Validates: Requirement 8.3 - State management allows retry
    /// Task 2.5: Test complete 6-position calibration workflow
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Task2.5")]
    public async Task Complete6PositionCalibration_CanBeRestartedAfterCompletion()
    {
        // Arrange
        var firstRunSteps = new List<int>();
        var secondRunSteps = new List<int>();
        bool isFirstRun = true;

        _viewModel.CalibrationStepCompleted += (sender, step) =>
        {
            if (isFirstRun)
                firstRunSteps.Add(step);
            else
                secondRunSteps.Add(step);
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

        // Act - First run
        await _viewModel.StartAccelerometerCalibrationAsync();
        isFirstRun = false;

        // Act - Second run
        await _viewModel.StartAccelerometerCalibrationAsync();

        // Assert - Both runs completed all 6 positions
        Assert.Equal(6, firstRunSteps.Count);
        Assert.Equal(6, secondRunSteps.Count);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, firstRunSteps);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, secondRunSteps);

        // Assert - MAVLink command was sent twice
        _mockMavLinkService.Verify(
            m => m.SendCommandLongAsync(
                241, 0, 0, 0, 0, 1, 0, 0),
            Times.Exactly(2));
    }

    /// <summary>
    /// Test complete workflow with all three calibration types
    /// Validates: Requirements 1.2, 1.4, 1.5 - All calibration types work
    /// Task 2.5: Test complete 6-position calibration workflow
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Task2.5")]
    public async Task CompleteCalibrationWorkflow_AllThreeTypesWork()
    {
        // Arrange
        var statusMessages = new List<string>();

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

        // Act - Run all three calibration types
        await _viewModel.StartAccelerometerCalibrationAsync();
        await _viewModel.StartSimpleAccelCalibrationAsync();
        await _viewModel.StartLevelCalibrationAsync();

        // Assert - All three commands were sent with correct parameters
        _mockMavLinkService.Verify(
            m => m.SendCommandLongAsync(241, 0, 0, 0, 0, 1, 0, 0), // Standard accel
            Times.Once);
        _mockMavLinkService.Verify(
            m => m.SendCommandLongAsync(241, 0, 0, 0, 0, 4, 0, 0), // Simple accel
            Times.Once);
        _mockMavLinkService.Verify(
            m => m.SendCommandLongAsync(241, 0, 0, 0, 0, 2, 0, 0), // Level
            Times.Once);

        // Assert - Status messages were displayed
        Assert.Contains(statusMessages, m => m.Contains("simple accelerometer calibration"));
        Assert.Contains(statusMessages, m => m.Contains("level calibration"));
    }

    /// <summary>
    /// Test that IsLoading state is managed correctly throughout workflow
    /// Validates: Requirement 11.1 - Loading indicators during calibration
    /// Task 2.5: Test complete 6-position calibration workflow
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Task2.5")]
    public async Task Complete6PositionCalibration_ManagesIsLoadingStateCorrectly()
    {
        // Arrange
        var isLoadingHistory = new List<(bool state, System.DateTime time)>();

        _viewModel.IsLoadingChanged += (sender, isLoading) =>
        {
            isLoadingHistory.Add((isLoading, System.DateTime.UtcNow));
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

        // Assert - IsLoading changed at least twice (true, then false)
        Assert.True(isLoadingHistory.Count >= 2);

        // Assert - First change should be to true
        Assert.True(isLoadingHistory.First().state);

        // Assert - Last change should be to false
        Assert.False(isLoadingHistory.Last().state);

        // Assert - Final state is false
        Assert.False(_viewModel.IsLoading);
    }
}
