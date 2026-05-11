using Moq;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Core.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace HarvestmoonGCS.Tests.ViewModels;

/// <summary>
/// Unit tests for CalibrationViewModel flight mode configuration functionality
/// Validates: Requirement 3 - Flight Mode Configuration UI
/// Task 4.1: Populate flight mode ComboBoxes on page load
/// </summary>
public class CalibrationViewModelFlightModeTests
{
    private readonly Mock<IMavLinkService> _mockMavLinkService;
    private readonly CalibrationViewModel _viewModel;

    public CalibrationViewModelFlightModeTests()
    {
        _mockMavLinkService = new Mock<IMavLinkService>();
        _viewModel = new CalibrationViewModel(_mockMavLinkService.Object);
    }

    [Fact]
    public async Task LoadFlightModesAsync_InitializesFlightModesDictionary()
    {
        // Act
        await _viewModel.LoadFlightModesAsync();

        // Assert
        Assert.NotNull(_viewModel.FlightModes);
        Assert.Equal(6, _viewModel.FlightModes.Count);
    }

    [Fact]
    public async Task LoadFlightModesAsync_InitializesAllSixModesToZero()
    {
        // Act
        await _viewModel.LoadFlightModesAsync();

        // Assert
        for (int i = 1; i <= 6; i++)
        {
            Assert.True(_viewModel.FlightModes.ContainsKey(i));
            Assert.Equal(0, _viewModel.FlightModes[i]);
        }
    }

    [Fact]
    public async Task SaveFlightModesAsync_SendsAllSixFlightModeParameters()
    {
        // Arrange
        var modes = new Dictionary<int, int>
        {
            { 1, 0 }, // Stabilize
            { 2, 1 }, // Acro
            { 3, 2 }, // AltHold
            { 4, 3 }, // Auto
            { 5, 4 }, // Guided
            { 6, 5 }  // Loiter
        };

        // Act
        await _viewModel.SaveFlightModesAsync(modes);

        // Assert
        _mockMavLinkService.Verify(m => m.SetParameterAsync("FLTMODE1", 0), Times.Once);
        _mockMavLinkService.Verify(m => m.SetParameterAsync("FLTMODE2", 1), Times.Once);
        _mockMavLinkService.Verify(m => m.SetParameterAsync("FLTMODE3", 2), Times.Once);
        _mockMavLinkService.Verify(m => m.SetParameterAsync("FLTMODE4", 3), Times.Once);
        _mockMavLinkService.Verify(m => m.SetParameterAsync("FLTMODE5", 4), Times.Once);
        _mockMavLinkService.Verify(m => m.SetParameterAsync("FLTMODE6", 5), Times.Once);
    }

    [Fact]
    public async Task SaveFlightModesAsync_RaisesStatusMessageChanged()
    {
        // Arrange
        var modes = new Dictionary<int, int> { { 1, 0 } };
        string? statusMessage = null;
        _viewModel.StatusMessageChanged += (sender, message) => statusMessage = message;

        // Act
        await _viewModel.SaveFlightModesAsync(modes);

        // Assert
        Assert.NotNull(statusMessage);
        Assert.Contains("Flight modes", statusMessage);
    }

    [Fact]
    public void UpdateCurrentModePWM_UpdatesCurrentModePWMProperty()
    {
        // Arrange
        float expectedPWM = 1500.5f;

        // Act
        _viewModel.UpdateCurrentModePWM(expectedPWM);

        // Assert
        Assert.Equal(expectedPWM, _viewModel.CurrentModePWM);
    }

    [Fact]
    public void UpdateCurrentModePWM_RaisesModePWMChangedEvent()
    {
        // Arrange
        float expectedPWM = 1500.5f;
        float? actualPWM = null;
        _viewModel.ModePWMChanged += (sender, pwm) => actualPWM = pwm;

        // Act
        _viewModel.UpdateCurrentModePWM(expectedPWM);

        // Assert
        Assert.NotNull(actualPWM);
        Assert.Equal(expectedPWM, actualPWM.Value);
    }

    [Theory]
    [InlineData(1000.0f)]
    [InlineData(1230.0f)]
    [InlineData(1360.0f)]
    [InlineData(1490.0f)]
    [InlineData(1620.0f)]
    [InlineData(1749.0f)]
    [InlineData(2000.0f)]
    public void UpdateCurrentModePWM_AcceptsValidPWMValues(float pwm)
    {
        // Act
        _viewModel.UpdateCurrentModePWM(pwm);

        // Assert
        Assert.Equal(pwm, _viewModel.CurrentModePWM);
    }

    [Fact]
    public void CurrentModePWM_PropertyChangedRaisedOnlyWhenValueChanges()
    {
        // Arrange
        int propertyChangedCount = 0;
        _viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(_viewModel.CurrentModePWM))
                propertyChangedCount++;
        };

        // Act
        _viewModel.UpdateCurrentModePWM(1500.0f);
        _viewModel.UpdateCurrentModePWM(1500.0f); // Same value, should not raise event
        _viewModel.UpdateCurrentModePWM(1600.0f); // Different value, should raise event

        // Assert
        Assert.Equal(2, propertyChangedCount);
    }

    [Fact]
    public async Task SaveFlightModesAsync_HandlesEmptyDictionary()
    {
        // Arrange
        var modes = new Dictionary<int, int>();

        // Act
        await _viewModel.SaveFlightModesAsync(modes);

        // Assert - Should not throw exception
        _mockMavLinkService.Verify(m => m.SetParameterAsync(It.IsAny<string>(), It.IsAny<float>()), Times.Never);
    }

    [Fact]
    public async Task SaveFlightModesAsync_HandlesPartialModeConfiguration()
    {
        // Arrange
        var modes = new Dictionary<int, int>
        {
            { 1, 0 },
            { 3, 2 },
            { 5, 4 }
        };

        // Act
        await _viewModel.SaveFlightModesAsync(modes);

        // Assert
        _mockMavLinkService.Verify(m => m.SetParameterAsync("FLTMODE1", 0), Times.Once);
        _mockMavLinkService.Verify(m => m.SetParameterAsync("FLTMODE3", 2), Times.Once);
        _mockMavLinkService.Verify(m => m.SetParameterAsync("FLTMODE5", 4), Times.Once);
        _mockMavLinkService.Verify(m => m.SetParameterAsync("FLTMODE2", It.IsAny<float>()), Times.Never);
        _mockMavLinkService.Verify(m => m.SetParameterAsync("FLTMODE4", It.IsAny<float>()), Times.Never);
        _mockMavLinkService.Verify(m => m.SetParameterAsync("FLTMODE6", It.IsAny<float>()), Times.Never);
    }
}
