using Xunit;
using HarvestmoonGCS.Core.ViewModels;
using HarvestmoonGCS.Core.Services;
using Moq;
using System.Threading.Tasks;

namespace HarvestmoonGCS.Tests.ViewModels;

/// <summary>
/// Comprehensive unit tests for CalibrationViewModel IsLoading state management
/// Validates: Task 2.1.2 - Update IsLoading state
/// Ensures IsLoading property is properly managed during all calibration operations
/// </summary>
public class CalibrationViewModelIsLoadingTests
{
    private readonly Mock<IMavLinkService> _mockMavLinkService;
    private readonly CalibrationViewModel _viewModel;

    public CalibrationViewModelIsLoadingTests()
    {
        _mockMavLinkService = new Mock<IMavLinkService>();
        _viewModel = new CalibrationViewModel(_mockMavLinkService.Object);
    }

    #region Accelerometer Calibration IsLoading Tests

    /// <summary>
    /// Test that IsLoading starts as false
    /// Validates: Task 2.1.2 - Initial state verification
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.1.2")]
    public void IsLoading_InitialState_IsFalse()
    {
        // Assert
        Assert.False(_viewModel.IsLoading);
    }

    /// <summary>
    /// Test that IsLoading is set to true when accelerometer calibration starts
    /// Validates: Task 2.1.2 - IsLoading set to true on start
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.1.2")]
    public async Task StartAccelerometerCalibration_SetsIsLoadingTrue()
    {
        // Arrange
        bool isLoadingSetToTrue = false;
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
                // Check IsLoading during execution
                isLoadingSetToTrue = _viewModel.IsLoading;
                await Task.Delay(10);
            });

        // Act
        await _viewModel.StartAccelerometerCalibrationAsync();

        // Assert
        Assert.True(isLoadingSetToTrue, "IsLoading should be true during calibration");
    }

    /// <summary>
    /// Test that IsLoading is set to false when accelerometer calibration completes
    /// Validates: Task 2.1.2 - IsLoading set to false on completion
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.1.2")]
    public async Task StartAccelerometerCalibration_ResetsIsLoadingFalse_OnCompletion()
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
        Assert.False(_viewModel.IsLoading, "IsLoading should be false after calibration completes");
    }

    /// <summary>
    /// Test that IsLoading is reset to false even when accelerometer calibration fails
    /// Validates: Task 2.1.2 - IsLoading reset on error
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.1.2")]
    public async Task StartAccelerometerCalibration_ResetsIsLoadingFalse_OnError()
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
            .ThrowsAsync(new System.Exception("MAVLink error"));

        // Act & Assert
        await Assert.ThrowsAsync<System.Exception>(
            async () => await _viewModel.StartAccelerometerCalibrationAsync());

        // IsLoading should be reset even on error
        Assert.False(_viewModel.IsLoading, "IsLoading should be false after error");
    }

    #endregion

    #region Compass Calibration IsLoading Tests

    /// <summary>
    /// Test that IsLoading is set to true when compass calibration starts
    /// Validates: Task 2.1.2 - IsLoading set to true on compass calibration start
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.1.2")]
    public async Task StartCompassCalibration_SetsIsLoadingTrue()
    {
        // Arrange
        bool isLoadingSetToTrue = false;

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
                // Check IsLoading during execution
                isLoadingSetToTrue = _viewModel.IsLoading;
                await Task.Delay(10);
            });

        // Act
        await _viewModel.StartCompassCalibrationAsync();
        
        // Give time for the background task to complete
        await Task.Delay(100);

        // Assert
        Assert.True(isLoadingSetToTrue, "IsLoading should be true during compass calibration");
    }

    /// <summary>
    /// Test that IsLoading is set to false when compass calibration completes
    /// Validates: Task 2.1.2 - IsLoading set to false on compass calibration completion
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.1.2")]
    public async Task StartCompassCalibration_ResetsIsLoadingFalse_OnCompletion()
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
        await _viewModel.StartCompassCalibrationAsync();
        
        // Wait for the simulated progress to complete
        await Task.Delay(11000); // Wait for the full simulation (100 steps * 500ms + buffer)

        // Assert
        Assert.False(_viewModel.IsLoading, "IsLoading should be false after compass calibration completes");
    }

    /// <summary>
    /// Test that IsLoading is reset to false when compass calibration fails
    /// Validates: Task 2.1.2 - IsLoading reset on compass calibration error
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.1.2")]
    public async Task StartCompassCalibration_ResetsIsLoadingFalse_OnError()
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
            .ThrowsAsync(new System.Exception("Compass calibration error"));

        // Act & Assert
        await Assert.ThrowsAsync<System.Exception>(
            async () => await _viewModel.StartCompassCalibrationAsync());

        // IsLoading should be reset even on error
        Assert.False(_viewModel.IsLoading, "IsLoading should be false after compass calibration error");
    }

    #endregion

    #region IsLoadingChanged Event Tests

    /// <summary>
    /// Test that IsLoadingChanged event is raised when IsLoading changes to true
    /// Validates: Task 2.1.2 - IsLoadingChanged event raised
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.1.2")]
    public async Task IsLoadingChanged_RaisedWhenSetToTrue()
    {
        // Arrange
        bool eventRaised = false;
        bool eventValue = false;

        _viewModel.IsLoadingChanged += (sender, isLoading) =>
        {
            if (isLoading)
            {
                eventRaised = true;
                eventValue = isLoading;
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
        Assert.True(eventRaised, "IsLoadingChanged event should be raised when IsLoading is set to true");
        Assert.True(eventValue, "Event value should be true");
    }

    /// <summary>
    /// Test that IsLoadingChanged event is raised when IsLoading changes to false
    /// Validates: Task 2.1.2 - IsLoadingChanged event raised on completion
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.1.2")]
    public async Task IsLoadingChanged_RaisedWhenSetToFalse()
    {
        // Arrange
        bool eventRaisedForFalse = false;
        bool lastEventValue = true;

        _viewModel.IsLoadingChanged += (sender, isLoading) =>
        {
            if (!isLoading)
            {
                eventRaisedForFalse = true;
                lastEventValue = isLoading;
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
        Assert.True(eventRaisedForFalse, "IsLoadingChanged event should be raised when IsLoading is set to false");
        Assert.False(lastEventValue, "Event value should be false");
    }

    /// <summary>
    /// Test that IsLoadingChanged event is raised exactly twice during calibration (true then false)
    /// Validates: Task 2.1.2 - IsLoadingChanged event raised for both state changes
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.1.2")]
    public async Task IsLoadingChanged_RaisedTwiceDuringCalibration()
    {
        // Arrange
        int eventCount = 0;
        var eventValues = new System.Collections.Generic.List<bool>();

        _viewModel.IsLoadingChanged += (sender, isLoading) =>
        {
            eventCount++;
            eventValues.Add(isLoading);
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
        Assert.True(eventCount >= 2, $"IsLoadingChanged event should be raised at least twice, but was raised {eventCount} times");
        Assert.True(eventValues[0], "First event should be true (calibration started)");
        Assert.False(eventValues[eventValues.Count - 1], "Last event should be false (calibration completed)");
    }

    /// <summary>
    /// Test that PropertyChanged event is raised for IsLoading property
    /// Validates: Task 2.1.2 - INotifyPropertyChanged implementation
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.1.2")]
    public async Task PropertyChanged_RaisedForIsLoading()
    {
        // Arrange
        int propertyChangedCount = 0;
        string? lastPropertyName = null;

        _viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(_viewModel.IsLoading))
            {
                propertyChangedCount++;
                lastPropertyName = args.PropertyName;
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
        Assert.True(propertyChangedCount >= 2, $"PropertyChanged should be raised at least twice for IsLoading, but was raised {propertyChangedCount} times");
        Assert.Equal(nameof(_viewModel.IsLoading), lastPropertyName);
    }

    #endregion

    #region Multiple Calibration Operations Tests

    /// <summary>
    /// Test that IsLoading is properly managed across multiple sequential calibrations
    /// Validates: Task 2.1.2 - IsLoading state management across multiple operations
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.1.2")]
    public async Task IsLoading_ProperlyManagedAcrossMultipleCalibrations()
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

        // Act & Assert - First calibration
        await _viewModel.StartAccelerometerCalibrationAsync();
        Assert.False(_viewModel.IsLoading, "IsLoading should be false after first calibration");

        // Act & Assert - Second calibration
        await _viewModel.StartAccelerometerCalibrationAsync();
        Assert.False(_viewModel.IsLoading, "IsLoading should be false after second calibration");

        // Act & Assert - Third calibration
        await _viewModel.StartAccelerometerCalibrationAsync();
        Assert.False(_viewModel.IsLoading, "IsLoading should be false after third calibration");
    }

    /// <summary>
    /// Test that IsLoading doesn't change for operations that don't use it
    /// Validates: Task 2.1.2 - IsLoading only changes for appropriate operations
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Task2.1.2")]
    public async Task IsLoading_DoesNotChangeForNonLoadingOperations()
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

        // Act - Operations that don't set IsLoading
        await _viewModel.StartSimpleAccelCalibrationAsync();
        bool isLoadingAfterSimple = _viewModel.IsLoading;

        await _viewModel.StartLevelCalibrationAsync();
        bool isLoadingAfterLevel = _viewModel.IsLoading;

        // Assert
        Assert.False(isLoadingAfterSimple, "IsLoading should remain false for simple accel calibration");
        Assert.False(isLoadingAfterLevel, "IsLoading should remain false for level calibration");
    }

    #endregion
}
