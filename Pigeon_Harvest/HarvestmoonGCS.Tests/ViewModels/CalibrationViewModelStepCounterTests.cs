using Xunit;
using HarvestmoonGCS.Core.ViewModels;
using HarvestmoonGCS.Core.Services;
using Moq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace HarvestmoonGCS.Tests.ViewModels;

/// <summary>
/// Comprehensive unit tests for CalibrationViewModel calibration step counter
/// Validates: Task 2.1.3 - Initialize calibration step counter
/// Ensures _currentCalibrationStep is properly initialized to 0 and tracks the 6 accelerometer positions
/// </summary>
public class CalibrationViewModelStepCounterTests
{
    private readonly Mock<IMavLinkService> _mockMavLinkService;
    private readonly CalibrationViewModel _viewModel;

    public CalibrationViewModelStepCounterTests()
    {
        _mockMavLinkService = new Mock<IMavLinkService>();
        _viewModel = new CalibrationViewModel(_mockMavLinkService.Object);
    }

    #region Step Counter Initialization Tests

    /// <summary>
    /// Test that calibration step counter is initialized to 0 when calibration starts
    /// Validates: Task 2.1.3 - Initialize calibration step counter to 0
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.1.3")]
    public async Task StartAccelerometerCalibration_InitializesStepCounterToZero()
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

        // Assert - First step should be 1, indicating counter started at 0
        Assert.NotEmpty(completedSteps);
        Assert.Equal(1, completedSteps[0]);
    }

    /// <summary>
    /// Test that step counter resets to 0 on each new calibration
    /// Validates: Task 2.1.3 - Step counter resets for each calibration session
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.1.3")]
    public async Task StartAccelerometerCalibration_ResetsStepCounterToZero_OnEachStart()
    {
        // Arrange
        var firstCalibrationSteps = new List<int>();
        var secondCalibrationSteps = new List<int>();
        bool isFirstCalibration = true;

        _viewModel.CalibrationStepCompleted += (sender, step) =>
        {
            if (isFirstCalibration)
            {
                firstCalibrationSteps.Add(step);
            }
            else
            {
                secondCalibrationSteps.Add(step);
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

        // Act - First calibration
        await _viewModel.StartAccelerometerCalibrationAsync();
        isFirstCalibration = false;

        // Act - Second calibration
        await _viewModel.StartAccelerometerCalibrationAsync();

        // Assert - Both calibrations should start from step 1
        Assert.Equal(1, firstCalibrationSteps[0]);
        Assert.Equal(1, secondCalibrationSteps[0]);
        Assert.Equal(6, firstCalibrationSteps.Count);
        Assert.Equal(6, secondCalibrationSteps.Count);
    }

    #endregion

    #region Step Counter Tracking Tests

    /// <summary>
    /// Test that step counter tracks all 6 accelerometer calibration positions
    /// Validates: Task 2.1.3 - Counter tracks 6 calibration positions
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.1.3")]
    public async Task StartAccelerometerCalibration_TracksAllSixPositions()
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

        // Assert - Should track exactly 6 positions
        Assert.Equal(6, completedSteps.Count);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, completedSteps);
    }

    /// <summary>
    /// Test that step counter increments sequentially from 1 to 6
    /// Validates: Task 2.1.3 - Counter increments sequentially
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.1.3")]
    public async Task StartAccelerometerCalibration_IncrementsStepCounterSequentially()
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

        // Assert - Each step should be exactly 1 more than the previous
        for (int i = 0; i < completedSteps.Count; i++)
        {
            Assert.Equal(i + 1, completedSteps[i]);
        }
    }

    /// <summary>
    /// Test that step counter does not skip any positions
    /// Validates: Task 2.1.3 - Counter tracks all positions without skipping
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.1.3")]
    public async Task StartAccelerometerCalibration_DoesNotSkipPositions()
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

        // Assert - No gaps in the sequence
        for (int i = 1; i <= 6; i++)
        {
            Assert.Contains(i, completedSteps);
        }
    }

    /// <summary>
    /// Test that step counter does not exceed 6 positions
    /// Validates: Task 2.1.3 - Counter stops at 6 positions
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.1.3")]
    public async Task StartAccelerometerCalibration_DoesNotExceedSixPositions()
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

        // Assert - Should not have more than 6 steps
        Assert.Equal(6, completedSteps.Count);
        Assert.All(completedSteps, step => Assert.InRange(step, 1, 6));
    }

    #endregion

    #region Step Counter Event Tests

    /// <summary>
    /// Test that CalibrationStepCompleted event is raised for each position
    /// Validates: Task 2.1.3 - Event raised for each completed position
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.1.3")]
    public async Task StartAccelerometerCalibration_RaisesEventForEachPosition()
    {
        // Arrange
        int eventCount = 0;
        
        _viewModel.CalibrationStepCompleted += (sender, step) =>
        {
            eventCount++;
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

        // Assert - Event should be raised exactly 6 times
        Assert.Equal(6, eventCount);
    }

    /// <summary>
    /// Test that CalibrationStepCompleted event provides correct step numbers
    /// Validates: Task 2.1.3 - Event provides accurate step information
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.1.3")]
    public async Task StartAccelerometerCalibration_EventProvidesCorrectStepNumbers()
    {
        // Arrange
        var eventSteps = new List<int>();
        
        _viewModel.CalibrationStepCompleted += (sender, step) =>
        {
            eventSteps.Add(step);
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

        // Assert - Event should provide steps 1 through 6 in order
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, eventSteps);
    }

    /// <summary>
    /// Test that CalibrationStepCompleted event is raised in correct order
    /// Validates: Task 2.1.3 - Events raised in sequential order
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.1.3")]
    public async Task StartAccelerometerCalibration_EventsRaisedInCorrectOrder()
    {
        // Arrange
        var eventSteps = new List<int>();
        int previousStep = 0;
        bool orderCorrect = true;
        
        _viewModel.CalibrationStepCompleted += (sender, step) =>
        {
            eventSteps.Add(step);
            if (step != previousStep + 1)
            {
                orderCorrect = false;
            }
            previousStep = step;
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
        Assert.True(orderCorrect, "Events should be raised in sequential order");
        Assert.Equal(6, eventSteps.Count);
    }

    #endregion

    #region Step Counter Integration Tests

    /// <summary>
    /// Test that step counter works correctly with IsLoading state
    /// Validates: Task 2.1.3 - Step counter integrates with IsLoading state
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.1.3")]
    public async Task StartAccelerometerCalibration_StepCounterWorksWithIsLoading()
    {
        // Arrange
        var completedSteps = new List<int>();
        bool isLoadingDuringSteps = false;
        
        _viewModel.CalibrationStepCompleted += (sender, step) =>
        {
            completedSteps.Add(step);
            if (_viewModel.IsLoading)
            {
                isLoadingDuringSteps = true;
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
        Assert.Equal(6, completedSteps.Count);
        Assert.True(isLoadingDuringSteps, "IsLoading should be true during step completion");
        Assert.False(_viewModel.IsLoading, "IsLoading should be false after all steps complete");
    }

    /// <summary>
    /// Test that step counter maintains state across multiple calibration attempts
    /// Validates: Task 2.1.3 - Step counter properly resets between calibrations
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.1.3")]
    public async Task StartAccelerometerCalibration_StepCounterMaintainsStateAcrossMultipleAttempts()
    {
        // Arrange
        var firstAttemptSteps = new List<int>();
        var secondAttemptSteps = new List<int>();
        var thirdAttemptSteps = new List<int>();
        int attemptNumber = 1;

        _viewModel.CalibrationStepCompleted += (sender, step) =>
        {
            if (attemptNumber == 1)
                firstAttemptSteps.Add(step);
            else if (attemptNumber == 2)
                secondAttemptSteps.Add(step);
            else if (attemptNumber == 3)
                thirdAttemptSteps.Add(step);
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

        // Act - First attempt
        await _viewModel.StartAccelerometerCalibrationAsync();
        attemptNumber = 2;

        // Act - Second attempt
        await _viewModel.StartAccelerometerCalibrationAsync();
        attemptNumber = 3;

        // Act - Third attempt
        await _viewModel.StartAccelerometerCalibrationAsync();

        // Assert - All attempts should have the same step sequence
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, firstAttemptSteps);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, secondAttemptSteps);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, thirdAttemptSteps);
    }

    /// <summary>
    /// Test that step counter is properly initialized even when MAVLink command fails
    /// Validates: Task 2.1.3 - Step counter initialization is robust to errors
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.1.3")]
    public async Task StartAccelerometerCalibration_InitializesStepCounter_EvenOnError()
    {
        // Arrange
        var completedSteps = new List<int>();
        bool firstAttempt = true;
        
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
            .Returns(() =>
            {
                if (firstAttempt)
                {
                    firstAttempt = false;
                    throw new System.Exception("MAVLink error");
                }
                return Task.CompletedTask;
            });

        // Act - First attempt (will fail)
        await Assert.ThrowsAsync<System.Exception>(
            async () => await _viewModel.StartAccelerometerCalibrationAsync());

        // Act - Second attempt (will succeed)
        await _viewModel.StartAccelerometerCalibrationAsync();

        // Assert - Second attempt should start from step 1
        Assert.NotEmpty(completedSteps);
        Assert.Equal(1, completedSteps[0]);
        Assert.Equal(6, completedSteps.Count);
    }

    #endregion

    #region Step Counter Boundary Tests

    /// <summary>
    /// Test that step counter handles the first position correctly
    /// Validates: Task 2.1.3 - First position (step 1) is tracked correctly
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.1.3")]
    public async Task StartAccelerometerCalibration_HandlesFirstPositionCorrectly()
    {
        // Arrange
        int? firstStep = null;
        
        _viewModel.CalibrationStepCompleted += (sender, step) =>
        {
            if (firstStep == null)
            {
                firstStep = step;
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
        Assert.NotNull(firstStep);
        Assert.Equal(1, firstStep.Value);
    }

    /// <summary>
    /// Test that step counter handles the last position correctly
    /// Validates: Task 2.1.3 - Last position (step 6) is tracked correctly
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.1.3")]
    public async Task StartAccelerometerCalibration_HandlesLastPositionCorrectly()
    {
        // Arrange
        int? lastStep = null;
        
        _viewModel.CalibrationStepCompleted += (sender, step) =>
        {
            lastStep = step;
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
        Assert.NotNull(lastStep);
        Assert.Equal(6, lastStep.Value);
    }

    #endregion
}
