using Xunit;
using HarvestmoonGCS.Core.ViewModels;
using HarvestmoonGCS.Core.Services;
using Moq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace HarvestmoonGCS.Tests.ViewModels;

/// <summary>
/// Unit tests for CalibrationViewModel calibration step completion handler
/// Validates: Requirement 1.3 - Visual indicator updates for completed positions
/// Task 2.4: Implement calibration step completion handler
/// </summary>
public class CalibrationViewModelStepCompletionTests
{
    private readonly Mock<IMavLinkService> _mockMavLinkService;
    private readonly CalibrationViewModel _viewModel;

    public CalibrationViewModelStepCompletionTests()
    {
        _mockMavLinkService = new Mock<IMavLinkService>();
        _viewModel = new CalibrationViewModel(_mockMavLinkService.Object);
    }

    /// <summary>
    /// Test that CalibrationStepCompleted event is raised for each of the 6 positions
    /// Validates: Requirement 1.3 - Update visual indicator for each completed position
    /// Task 2.4.3: Raise CalibrationStepCompleted event
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.4.3")]
    public async Task CalibrationStepCompleted_RaisedForAllSixPositions()
    {
        // Arrange
        var completedSteps = new List<int>();

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

        // Assert - All 6 steps should be completed in order
        Assert.Equal(6, completedSteps.Count);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, completedSteps);
    }

    /// <summary>
    /// Test that CalibrationStepCompleted event provides correct step number
    /// Validates: Requirement 1.3 - Correct position identification
    /// Task 2.4.3: Raise CalibrationStepCompleted event with correct step number
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.4.3")]
    public async Task CalibrationStepCompleted_ProvidesCorrectStepNumber(int expectedStep)
    {
        // Arrange
        int? receivedStep = null;
        int eventCount = 0;

        _viewModel.CalibrationStepCompleted += (sender, step) =>
        {
            eventCount++;
            if (eventCount == expectedStep)
            {
                receivedStep = step;
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
            .Returns(Task.CompletedTask);

        // Act
        await _viewModel.StartAccelerometerCalibrationAsync();

        // Assert
        Assert.NotNull(receivedStep);
        Assert.Equal(expectedStep, receivedStep.Value);
    }

    /// <summary>
    /// Test that CalibrationStepCompleted event is raised in sequential order
    /// Validates: Requirement 1.3 - Sequential completion of positions
    /// Task 2.4: Sequential step completion
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.4")]
    public async Task CalibrationStepCompleted_RaisedInSequentialOrder()
    {
        // Arrange
        var completedSteps = new List<int>();

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

        // Assert - Steps should be in order 1, 2, 3, 4, 5, 6
        for (int i = 0; i < completedSteps.Count - 1; i++)
        {
            Assert.True(completedSteps[i] < completedSteps[i + 1], 
                $"Step {completedSteps[i]} should come before step {completedSteps[i + 1]}");
        }
    }

    /// <summary>
    /// Test that CalibrationStepCompleted event is not raised before calibration starts
    /// Validates: Requirement 1.2 - Event only raised during active calibration
    /// Task 2.4.3: Event timing
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.4.3")]
    public void CalibrationStepCompleted_NotRaisedBeforeCalibrationStarts()
    {
        // Arrange
        bool eventRaised = false;

        _viewModel.CalibrationStepCompleted += (sender, step) =>
        {
            eventRaised = true;
        };

        // Act - Don't start calibration

        // Assert
        Assert.False(eventRaised);
    }

    /// <summary>
    /// Test that multiple subscribers can receive CalibrationStepCompleted events
    /// Validates: Requirement 11.2 - Multiple UI components can subscribe to events
    /// Task 2.4.3: Event subscription support
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.4.3")]
    public async Task CalibrationStepCompleted_SupportsMultipleSubscribers()
    {
        // Arrange
        var subscriber1Steps = new List<int>();
        var subscriber2Steps = new List<int>();

        _viewModel.CalibrationStepCompleted += (sender, step) =>
        {
            subscriber1Steps.Add(step);
        };

        _viewModel.CalibrationStepCompleted += (sender, step) =>
        {
            subscriber2Steps.Add(step);
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

        // Assert - Both subscribers should receive all events
        Assert.Equal(6, subscriber1Steps.Count);
        Assert.Equal(6, subscriber2Steps.Count);
        Assert.Equal(subscriber1Steps, subscriber2Steps);
    }

    /// <summary>
    /// Test that CalibrationStepCompleted event includes sender reference
    /// Validates: Standard event pattern implementation
    /// Task 2.4.3: Event pattern compliance
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.4.3")]
    public async Task CalibrationStepCompleted_IncludesSenderReference()
    {
        // Arrange
        object? eventSender = null;

        _viewModel.CalibrationStepCompleted += (sender, step) =>
        {
            eventSender = sender;
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

        // Assert
        Assert.NotNull(eventSender);
        Assert.Same(_viewModel, eventSender);
    }

    /// <summary>
    /// Test that calibration step counter is initialized correctly
    /// Validates: Requirement 1.2 - Calibration step counter initialization
    /// Task 2.1.3: Initialize calibration step counter
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.1.3")]
    public async Task StartAccelerometerCalibration_InitializesStepCounter()
    {
        // Arrange
        var completedSteps = new List<int>();

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

        // Assert - First step should be 1, not 0
        Assert.Equal(1, completedSteps[0]);
    }

    /// <summary>
    /// Test that calibration completes all 6 steps without skipping
    /// Validates: Requirement 1.3 - All 6 positions must be calibrated
    /// Task 2.4: Complete calibration workflow
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.4")]
    public async Task CalibrationStepCompleted_CompletesAllSixStepsWithoutSkipping()
    {
        // Arrange
        var completedSteps = new List<int>();

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

        // Assert - Should have exactly 6 unique steps
        Assert.Equal(6, completedSteps.Count);
        Assert.Equal(6, completedSteps.Distinct().Count());
        
        // Verify all steps from 1 to 6 are present
        for (int i = 1; i <= 6; i++)
        {
            Assert.Contains(i, completedSteps);
        }
    }

    /// <summary>
    /// Test that calibration step completion timing is reasonable
    /// Validates: Requirement 11.2 - Real-time feedback timing
    /// Task 2.4: Step completion timing
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.4")]
    public async Task CalibrationStepCompleted_CompletesWithinReasonableTime()
    {
        // Arrange
        var startTime = System.DateTime.UtcNow;
        var completedSteps = new List<int>();

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
        var endTime = System.DateTime.UtcNow;

        // Assert - Should complete within 60 seconds (6 steps * 5 seconds + overhead)
        var duration = endTime - startTime;
        Assert.True(duration.TotalSeconds < 60, 
            $"Calibration took {duration.TotalSeconds} seconds, expected less than 60");
        Assert.Equal(6, completedSteps.Count);
    }
}
