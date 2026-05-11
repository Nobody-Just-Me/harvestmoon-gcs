using Xunit;
using HarvestmoonGCS.Core.ViewModels;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Core.Models;
using Moq;
using System.Collections.Generic;

namespace HarvestmoonGCS.Tests.Integration;

/// <summary>
/// Integration tests for CalibrationViewModel state preservation during tab navigation
/// Validates: Requirement 8.3 - Tab switching preserves input field values
/// Task 1.4: Test tab switching preserves input field values
/// 
/// Note: These tests validate the ViewModel state preservation behavior that underlies
/// the UI tab switching functionality. The actual UI tests are in UIConversion folder
/// but are currently disabled pending additional framework work.
/// </summary>
public class CalibrationTabStatePreservationTests
{
    private readonly Mock<IMavLinkService> _mockMavLinkService;
    private readonly CalibrationViewModel _viewModel;

    public CalibrationTabStatePreservationTests()
    {
        _mockMavLinkService = new Mock<IMavLinkService>();
        _viewModel = new CalibrationViewModel(_mockMavLinkService.Object);
    }

    /// <summary>
    /// Test that servo configuration values are preserved in ViewModel
    /// Validates: Requirement 8.3 - State preservation for servo output configuration
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Task1.4")]
    public void ViewModel_PreservesServoConfigurationValues()
    {
        // Arrange - Modify existing servo configuration values (ServoConfigs is pre-populated with 16 entries)
        _viewModel.ServoConfigs[1].Min = 1100;
        _viewModel.ServoConfigs[1].Trim = 1500;
        _viewModel.ServoConfigs[1].Max = 1900;
        _viewModel.ServoConfigs[1].Reverse = true;
        _viewModel.ServoConfigs[1].Function = 2;

        _viewModel.ServoConfigs[5].Min = 1050;
        _viewModel.ServoConfigs[5].Trim = 1450;
        _viewModel.ServoConfigs[5].Max = 1850;
        _viewModel.ServoConfigs[5].Reverse = false;
        _viewModel.ServoConfigs[5].Function = 1;

        _viewModel.ServoConfigs[10].Min = 1200;
        _viewModel.ServoConfigs[10].Trim = 1600;
        _viewModel.ServoConfigs[10].Max = 2000;
        _viewModel.ServoConfigs[10].Reverse = true;
        _viewModel.ServoConfigs[10].Function = 3;

        // Act - Simulate tab switching by accessing other ViewModel properties
        _ = _viewModel.WaypointSpeed;
        _ = _viewModel.IsLoading;
        _ = _viewModel.CurrentPWM;

        // Assert - Servo configurations should still be preserved
        Assert.Equal(16, _viewModel.ServoConfigs.Count); // Pre-populated with 16 entries
        Assert.Equal(1100, _viewModel.ServoConfigs[1].Min);
        Assert.Equal(1500, _viewModel.ServoConfigs[1].Trim);
        Assert.Equal(1900, _viewModel.ServoConfigs[1].Max);
        Assert.True(_viewModel.ServoConfigs[1].Reverse);
        Assert.Equal(2, _viewModel.ServoConfigs[1].Function);

        Assert.Equal(1050, _viewModel.ServoConfigs[5].Min);
        Assert.Equal(1450, _viewModel.ServoConfigs[5].Trim);
        Assert.Equal(1850, _viewModel.ServoConfigs[5].Max);
        Assert.False(_viewModel.ServoConfigs[5].Reverse);
        Assert.Equal(1, _viewModel.ServoConfigs[5].Function);
    }

    /// <summary>
    /// Test that waypoint parameter values are preserved in ViewModel
    /// Validates: Requirement 8.3 - State preservation for settings configuration
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Task1.4")]
    public void ViewModel_PreservesWaypointParameterValues()
    {
        // Arrange - Set waypoint parameter values
        _viewModel.WaypointSpeed = 5.5f;
        _viewModel.WaypointRadius = 10.0f;
        _viewModel.WaypointSpeedUp = 2.5f;
        _viewModel.WaypointSpeedDown = 1.5f;
        _viewModel.LoiterSpeed = 3.0f;

        // Act - Simulate tab switching by accessing other ViewModel properties
        _ = _viewModel.ServoConfigs;
        _ = _viewModel.FlightModes;
        _ = _viewModel.CompassDevices;

        // Assert - Waypoint parameters should still be preserved
        Assert.Equal(5.5f, _viewModel.WaypointSpeed);
        Assert.Equal(10.0f, _viewModel.WaypointRadius);
        Assert.Equal(2.5f, _viewModel.WaypointSpeedUp);
        Assert.Equal(1.5f, _viewModel.WaypointSpeedDown);
        Assert.Equal(3.0f, _viewModel.LoiterSpeed);
    }

    /// <summary>
    /// Test that PID parameter values are preserved in ViewModel
    /// Validates: Requirement 8.3 - State preservation for PID tuning parameters
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Task1.4")]
    public void ViewModel_PreservesPIDParameterValues()
    {
        // Arrange - Set PID parameter values
        _viewModel.RollPID = new PIDParameters { P = 0.15f, I = 0.05f, D = 0.01f, IMAX = 0.5f };
        _viewModel.PitchPID = new PIDParameters { P = 0.16f, I = 0.06f, D = 0.02f, IMAX = 0.6f };
        _viewModel.YawPID = new PIDParameters { P = 0.20f, I = 0.10f, D = 0.03f, IMAX = 0.7f };

        // Act - Simulate tab switching by accessing other ViewModel properties
        _ = _viewModel.WaypointSpeed;
        _ = _viewModel.ServoConfigs;
        _ = _viewModel.SelectedMotorChannels;

        // Assert - PID parameters should still be preserved
        Assert.NotNull(_viewModel.RollPID);
        Assert.Equal(0.15f, _viewModel.RollPID.P);
        Assert.Equal(0.05f, _viewModel.RollPID.I);
        Assert.Equal(0.01f, _viewModel.RollPID.D);
        Assert.Equal(0.5f, _viewModel.RollPID.IMAX);

        Assert.NotNull(_viewModel.PitchPID);
        Assert.Equal(0.16f, _viewModel.PitchPID.P);
        Assert.Equal(0.06f, _viewModel.PitchPID.I);

        Assert.NotNull(_viewModel.YawPID);
        Assert.Equal(0.20f, _viewModel.YawPID.P);
        Assert.Equal(0.10f, _viewModel.YawPID.I);
    }

    /// <summary>
    /// Test that flight mode selections are preserved in ViewModel
    /// Validates: Requirement 8.3 - State preservation for flight mode configuration
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Task1.4")]
    public void ViewModel_PreservesFlightModeSelections()
    {
        // Arrange - Set flight mode selections
        _viewModel.FlightModes[1] = 0; // Stabilize
        _viewModel.FlightModes[2] = 2; // AltHold
        _viewModel.FlightModes[3] = 5; // Loiter
        _viewModel.FlightModes[4] = 6; // RTL
        _viewModel.FlightModes[5] = 3; // Auto
        _viewModel.FlightModes[6] = 4; // Guided

        // Act - Simulate tab switching by accessing other ViewModel properties
        _ = _viewModel.ServoConfigs;
        _ = _viewModel.WaypointSpeed;
        _ = _viewModel.CurrentPWM;

        // Assert - Flight mode selections should still be preserved
        Assert.Equal(6, _viewModel.FlightModes.Count);
        Assert.Equal(0, _viewModel.FlightModes[1]);
        Assert.Equal(2, _viewModel.FlightModes[2]);
        Assert.Equal(5, _viewModel.FlightModes[3]);
        Assert.Equal(6, _viewModel.FlightModes[4]);
        Assert.Equal(3, _viewModel.FlightModes[5]);
        Assert.Equal(4, _viewModel.FlightModes[6]);
    }

    /// <summary>
    /// Test that motor selection states are preserved in ViewModel
    /// Validates: Requirement 8.3 - State preservation for ESC calibration motor selections
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Task1.4")]
    public void ViewModel_PreservesMotorSelectionStates()
    {
        // Arrange - Select specific motors
        _viewModel.SelectedMotorChannels.Clear();
        _viewModel.SelectedMotorChannels.Add(1);
        _viewModel.SelectedMotorChannels.Add(5);
        _viewModel.SelectedMotorChannels.Add(10);
        _viewModel.SelectedMotorChannels.Add(16);

        // Act - Simulate tab switching by accessing other ViewModel properties
        _ = _viewModel.FlightModes;
        _ = _viewModel.ServoConfigs;
        _ = _viewModel.WaypointSpeed;

        // Assert - Motor selections should still be preserved
        Assert.Equal(4, _viewModel.SelectedMotorChannels.Count);
        Assert.Contains(1, _viewModel.SelectedMotorChannels);
        Assert.Contains(5, _viewModel.SelectedMotorChannels);
        Assert.Contains(10, _viewModel.SelectedMotorChannels);
        Assert.Contains(16, _viewModel.SelectedMotorChannels);
    }

    /// <summary>
    /// Test that motor test parameters are preserved in ViewModel
    /// Validates: Requirement 8.3 - State preservation for motor test configuration
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Task1.4")]
    public void ViewModel_PreservesMotorTestParameters()
    {
        // Arrange - Set motor test parameters
        _viewModel.ThrottlePercentage = 50;
        _viewModel.TestDuration = 5;
        _viewModel.SelectedMotor = 3;

        // Act - Simulate tab switching by accessing other ViewModel properties
        _ = _viewModel.ServoConfigs;
        _ = _viewModel.FlightModes;
        _ = _viewModel.WaypointSpeed;

        // Assert - Motor test parameters should still be preserved
        Assert.Equal(50, _viewModel.ThrottlePercentage);
        Assert.Equal(5, _viewModel.TestDuration);
        Assert.Equal(3, _viewModel.SelectedMotor);
    }

    /// <summary>
    /// Test that PWM slider value is preserved in ViewModel
    /// Validates: Requirement 8.3 - State preservation for ESC calibration PWM value
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Task1.4")]
    public void ViewModel_PreservesPWMSliderValue()
    {
        // Arrange - Set PWM value
        _viewModel.CurrentPWM = 1750;

        // Act - Simulate tab switching by accessing other ViewModel properties
        _ = _viewModel.FlightModes;
        _ = _viewModel.ServoConfigs;
        _ = _viewModel.WaypointSpeed;
        _ = _viewModel.SelectedMotorChannels;

        // Assert - PWM value should still be preserved
        Assert.Equal(1750, _viewModel.CurrentPWM);
    }

    /// <summary>
    /// Test that all configuration values are preserved simultaneously
    /// Validates: Requirement 8.3 - Comprehensive state preservation across all tabs
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Task1.4")]
    public void ViewModel_PreservesAllConfigurationValues_Simultaneously()
    {
        // Arrange - Set values across all configuration types
        // Servo configuration
        _viewModel.ServoConfigs[1] = new ServoConfig { Channel = 1, Min = 1100, Trim = 1500, Max = 1900 };
        
        // Waypoint parameters
        _viewModel.WaypointSpeed = 5.5f;
        _viewModel.WaypointRadius = 10.0f;
        
        // PID parameters
        _viewModel.RollPID = new PIDParameters { P = 0.15f, I = 0.05f, D = 0.01f };
        
        // Flight modes
        _viewModel.FlightModes[1] = 0;
        _viewModel.FlightModes[2] = 2;
        
        // Motor selections
        _viewModel.SelectedMotorChannels.Add(1);
        _viewModel.SelectedMotorChannels.Add(5);
        
        // Motor test parameters
        _viewModel.ThrottlePercentage = 50;
        _viewModel.TestDuration = 5;
        
        // PWM value
        _viewModel.CurrentPWM = 1750;

        // Act - Simulate extensive tab switching by accessing all properties multiple times
        for (int i = 0; i < 10; i++)
        {
            _ = _viewModel.ServoConfigs;
            _ = _viewModel.WaypointSpeed;
            _ = _viewModel.RollPID;
            _ = _viewModel.FlightModes;
            _ = _viewModel.SelectedMotorChannels;
            _ = _viewModel.ThrottlePercentage;
            _ = _viewModel.CurrentPWM;
        }

        // Assert - All values should still be preserved
        Assert.Equal(1100, _viewModel.ServoConfigs[1].Min);
        Assert.Equal(5.5f, _viewModel.WaypointSpeed);
        Assert.Equal(10.0f, _viewModel.WaypointRadius);
        Assert.Equal(0.15f, _viewModel.RollPID.P);
        Assert.Equal(0, _viewModel.FlightModes[1]);
        Assert.Equal(2, _viewModel.FlightModes[2]);
        Assert.Contains(1, _viewModel.SelectedMotorChannels);
        Assert.Contains(5, _viewModel.SelectedMotorChannels);
        Assert.Equal(50, _viewModel.ThrottlePercentage);
        Assert.Equal(5, _viewModel.TestDuration);
        Assert.Equal(1750, _viewModel.CurrentPWM);
    }

    /// <summary>
    /// Test that empty/default values are preserved correctly
    /// Validates: Requirement 8.3 - State preservation for empty input fields
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Task1.4")]
    public void ViewModel_PreservesEmptyAndDefaultValues()
    {
        // Arrange - Set some values to defaults/empty
        _viewModel.WaypointSpeed = 0f;
        _viewModel.SelectedMotorChannels.Clear();
        _viewModel.ServoConfigs.Clear();

        // Act - Simulate tab switching
        _ = _viewModel.FlightModes;
        _ = _viewModel.RollPID;
        _ = _viewModel.CurrentPWM;

        // Assert - Empty/default values should be preserved
        Assert.Equal(0f, _viewModel.WaypointSpeed);
        Assert.Empty(_viewModel.SelectedMotorChannels);
        Assert.Empty(_viewModel.ServoConfigs);
    }

    /// <summary>
    /// Test that compass device list is preserved in ViewModel
    /// Validates: Requirement 8.3 - State preservation for compass calibration
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Task1.4")]
    public void ViewModel_PreservesCompassDeviceList()
    {
        // Arrange - Add compass devices
        _viewModel.CompassDevices.Add(new CompassDevice { Number = 1, DevID = 123, DevType = "HMC5883" });
        _viewModel.CompassDevices.Add(new CompassDevice { Number = 2, DevID = 456, DevType = "LSM303D" });

        // Act - Simulate tab switching
        _ = _viewModel.ServoConfigs;
        _ = _viewModel.FlightModes;
        _ = _viewModel.WaypointSpeed;

        // Assert - Compass devices should be preserved
        Assert.Equal(2, _viewModel.CompassDevices.Count);
        Assert.Equal(1, _viewModel.CompassDevices[0].Number);
        Assert.Equal(123, _viewModel.CompassDevices[0].DevID);
        Assert.Equal("HMC5883", _viewModel.CompassDevices[0].DevType);
        Assert.Equal(2, _viewModel.CompassDevices[1].Number);
        Assert.Equal(456, _viewModel.CompassDevices[1].DevID);
        Assert.Equal("LSM303D", _viewModel.CompassDevices[1].DevType);
    }
}
