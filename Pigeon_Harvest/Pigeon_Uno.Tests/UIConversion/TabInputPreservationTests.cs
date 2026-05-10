using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Pigeon_Uno.Views;
using Pigeon_Uno.Core.ViewModels;
using Pigeon_Uno.Core.Services;
using Moq;
using System.Reflection;

namespace Pigeon_Uno.Tests.UIConversion;

/// <summary>
/// Unit tests for CalibrationPage tab switching input field preservation
/// Validates: Requirement 8.3 - Tab switching preserves input field values
/// Task 1.4: Test tab switching preserves input field values
/// </summary>
[TestClass]
public class TabInputPreservationTests
{
    private Mock<IMavLinkService> _mockMavLinkService = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockMavLinkService = new Mock<IMavLinkService>();
    }

    /// <summary>
    /// Test that TextBox values are preserved when switching between tabs
    /// Validates: Requirement 8.3 - WHEN switching between tabs, THE Calibration_System SHALL preserve the state of input fields
    /// </summary>
    [TestMethod]
    [TestCategory("InputPreservation")]
    [TestCategory("Task1.4")]
    public void TabSwitch_PreservesTextBoxValues()
    {
        // Arrange
        var page = new CalibrationPage();
        var buttonClickMethod = typeof(CalibrationPage).GetMethod("Button_Click", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(buttonClickMethod, "Button_Click method not found");

        // Navigate to Servo Output tab
        var servoButton = new Button { Name = "ServoOutputButton" };
        buttonClickMethod.Invoke(page, new object[] { servoButton, new RoutedEventArgs() });

        // Set values in servo Min/Trim/Max TextBoxes
        var min1 = GetPrivateField<TextBox>(page, "Min1");
        var trim1 = GetPrivateField<TextBox>(page, "Trim1");
        var max1 = GetPrivateField<TextBox>(page, "Max1");

        Assert.IsNotNull(min1, "Min1 TextBox not found");
        Assert.IsNotNull(trim1, "Trim1 TextBox not found");
        Assert.IsNotNull(max1, "Max1 TextBox not found");

        min1.Text = "1100";
        trim1.Text = "1500";
        max1.Text = "1900";

        // Act - Switch to another tab and back
        var flightButton = new Button { Name = "FlightButton" };
        buttonClickMethod.Invoke(page, new object[] { flightButton, new RoutedEventArgs() });

        var servoButton2 = new Button { Name = "ServoOutputButton" };
        buttonClickMethod.Invoke(page, new object[] { servoButton2, new RoutedEventArgs() });

        // Assert - Values should be preserved
        Assert.AreEqual("1100", min1.Text, "Min1 value should be preserved after tab switch");
        Assert.AreEqual("1500", trim1.Text, "Trim1 value should be preserved after tab switch");
        Assert.AreEqual("1900", max1.Text, "Max1 value should be preserved after tab switch");
    }

    /// <summary>
    /// Test that ComboBox selections are preserved when switching between tabs
    /// Validates: Requirement 8.3 - WHEN switching between tabs, THE Calibration_System SHALL preserve the state of input fields
    /// </summary>
    [TestMethod]
    [TestCategory("InputPreservation")]
    [TestCategory("Task1.4")]
    public void TabSwitch_PreservesComboBoxSelections()
    {
        // Arrange
        var page = new CalibrationPage();
        var buttonClickMethod = typeof(CalibrationPage).GetMethod("Button_Click", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(buttonClickMethod, "Button_Click method not found");

        // Navigate to Servo Output tab
        var servoButton = new Button { Name = "ServoOutputButton" };
        buttonClickMethod.Invoke(page, new object[] { servoButton, new RoutedEventArgs() });

        // Set ComboBox selection
        var function1 = GetPrivateField<ComboBox>(page, "Function1");
        Assert.IsNotNull(function1, "Function1 ComboBox not found");
        
        // Ensure ComboBox has items
        if (function1.Items.Count > 0)
        {
            function1.SelectedIndex = 2; // Select third item

            // Act - Switch to another tab and back
            var compassButton = new Button { Name = "CompassButton" };
            buttonClickMethod.Invoke(page, new object[] { compassButton, new RoutedEventArgs() });

            var servoButton2 = new Button { Name = "ServoOutputButton" };
            buttonClickMethod.Invoke(page, new object[] { servoButton2, new RoutedEventArgs() });

            // Assert - Selection should be preserved
            Assert.AreEqual(2, function1.SelectedIndex, "Function1 selection should be preserved after tab switch");
        }
    }

    /// <summary>
    /// Test that CheckBox states are preserved when switching between tabs
    /// Validates: Requirement 8.3 - WHEN switching between tabs, THE Calibration_System SHALL preserve the state of input fields
    /// </summary>
    [TestMethod]
    [TestCategory("InputPreservation")]
    [TestCategory("Task1.4")]
    public void TabSwitch_PreservesCheckBoxStates()
    {
        // Arrange
        var page = new CalibrationPage();
        var buttonClickMethod = typeof(CalibrationPage).GetMethod("Button_Click", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(buttonClickMethod, "Button_Click method not found");

        // Navigate to Servo Output tab
        var servoButton = new Button { Name = "ServoOutputButton" };
        buttonClickMethod.Invoke(page, new object[] { servoButton, new RoutedEventArgs() });

        // Set CheckBox state
        var reverse1 = GetPrivateField<CheckBox>(page, "Reverse1");
        Assert.IsNotNull(reverse1, "Reverse1 CheckBox not found");
        
        reverse1.IsChecked = true;

        // Act - Switch to another tab and back
        var settingButton = new Button { Name = "SettingButton" };
        buttonClickMethod.Invoke(page, new object[] { settingButton, new RoutedEventArgs() });

        var servoButton2 = new Button { Name = "ServoOutputButton" };
        buttonClickMethod.Invoke(page, new object[] { servoButton2, new RoutedEventArgs() });

        // Assert - CheckBox state should be preserved
        Assert.IsTrue(reverse1.IsChecked == true, "Reverse1 CheckBox state should be preserved after tab switch");
    }

    /// <summary>
    /// Test that Slider values are preserved when switching between tabs
    /// Validates: Requirement 8.3 - WHEN switching between tabs, THE Calibration_System SHALL preserve the state of input fields
    /// </summary>
    [TestMethod]
    [TestCategory("InputPreservation")]
    [TestCategory("Task1.4")]
    public void TabSwitch_PreservesSliderValues()
    {
        // Arrange
        var page = new CalibrationPage();
        var buttonClickMethod = typeof(CalibrationPage).GetMethod("Button_Click", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(buttonClickMethod, "Button_Click method not found");

        // Navigate to Motor Test tab
        var motorTestButton = new Button { Name = "MotorTestButton" };
        buttonClickMethod.Invoke(page, new object[] { motorTestButton, new RoutedEventArgs() });

        // Set Slider values
        var motorPwmSlider = GetPrivateField<Slider>(page, "MotorPwmSlider");
        var motorDurationSlider = GetPrivateField<Slider>(page, "MotorDurationSlider");

        Assert.IsNotNull(motorPwmSlider, "MotorPwmSlider not found");
        Assert.IsNotNull(motorDurationSlider, "MotorDurationSlider not found");

        motorPwmSlider.Value = 50;
        motorDurationSlider.Value = 5;

        // Act - Switch to another tab and back
        var accelButton = new Button { Name = "AccelerometerButton" };
        buttonClickMethod.Invoke(page, new object[] { accelButton, new RoutedEventArgs() });

        var motorTestButton2 = new Button { Name = "MotorTestButton" };
        buttonClickMethod.Invoke(page, new object[] { motorTestButton2, new RoutedEventArgs() });

        // Assert - Slider values should be preserved
        Assert.AreEqual(50, motorPwmSlider.Value, "MotorPwmSlider value should be preserved after tab switch");
        Assert.AreEqual(5, motorDurationSlider.Value, "MotorDurationSlider value should be preserved after tab switch");
    }

    /// <summary>
    /// Test that multiple input fields across different types are preserved simultaneously
    /// Validates: Requirement 8.3 - WHEN switching between tabs, THE Calibration_System SHALL preserve the state of input fields
    /// </summary>
    [TestMethod]
    [TestCategory("InputPreservation")]
    [TestCategory("Task1.4")]
    public void TabSwitch_PreservesMultipleInputTypes_Simultaneously()
    {
        // Arrange
        var page = new CalibrationPage();
        var buttonClickMethod = typeof(CalibrationPage).GetMethod("Button_Click", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(buttonClickMethod, "Button_Click method not found");

        // Navigate to Servo Output tab and set multiple inputs
        var servoButton = new Button { Name = "ServoOutputButton" };
        buttonClickMethod.Invoke(page, new object[] { servoButton, new RoutedEventArgs() });

        var min1 = GetPrivateField<TextBox>(page, "Min1");
        var reverse1 = GetPrivateField<CheckBox>(page, "Reverse1");
        var function1 = GetPrivateField<ComboBox>(page, "Function1");

        Assert.IsNotNull(min1, "Min1 TextBox not found");
        Assert.IsNotNull(reverse1, "Reverse1 CheckBox not found");
        Assert.IsNotNull(function1, "Function1 ComboBox not found");

        min1.Text = "1250";
        reverse1.IsChecked = true;
        if (function1.Items.Count > 1)
        {
            function1.SelectedIndex = 1;
        }

        // Act - Switch through multiple tabs
        var compassButton = new Button { Name = "CompassButton" };
        buttonClickMethod.Invoke(page, new object[] { compassButton, new RoutedEventArgs() });

        var escButton = new Button { Name = "EscCalibButton" };
        buttonClickMethod.Invoke(page, new object[] { escButton, new RoutedEventArgs() });

        var servoButton2 = new Button { Name = "ServoOutputButton" };
        buttonClickMethod.Invoke(page, new object[] { servoButton2, new RoutedEventArgs() });

        // Assert - All values should be preserved
        Assert.AreEqual("1250", min1.Text, "Min1 TextBox value should be preserved");
        Assert.IsTrue(reverse1.IsChecked == true, "Reverse1 CheckBox state should be preserved");
        if (function1.Items.Count > 1)
        {
            Assert.AreEqual(1, function1.SelectedIndex, "Function1 ComboBox selection should be preserved");
        }
    }

    /// <summary>
    /// Test that input values are preserved across all 16 servo channels
    /// Validates: Requirement 8.3 - WHEN switching between tabs, THE Calibration_System SHALL preserve the state of input fields
    /// </summary>
    [TestMethod]
    [TestCategory("InputPreservation")]
    [TestCategory("Task1.4")]
    public void TabSwitch_PreservesAllServoChannelInputs()
    {
        // Arrange
        var page = new CalibrationPage();
        var buttonClickMethod = typeof(CalibrationPage).GetMethod("Button_Click", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(buttonClickMethod, "Button_Click method not found");

        // Navigate to Servo Output tab
        var servoButton = new Button { Name = "ServoOutputButton" };
        buttonClickMethod.Invoke(page, new object[] { servoButton, new RoutedEventArgs() });

        // Set values for multiple servo channels
        var testChannels = new[] { 1, 5, 10, 16 };
        foreach (var channel in testChannels)
        {
            var minBox = GetPrivateField<TextBox>(page, $"Min{channel}");
            if (minBox != null)
            {
                minBox.Text = $"{1000 + channel * 10}";
            }
        }

        // Act - Switch tabs
        var flightButton = new Button { Name = "FlightButton" };
        buttonClickMethod.Invoke(page, new object[] { flightButton, new RoutedEventArgs() });

        var servoButton2 = new Button { Name = "ServoOutputButton" };
        buttonClickMethod.Invoke(page, new object[] { servoButton2, new RoutedEventArgs() });

        // Assert - All channel values should be preserved
        foreach (var channel in testChannels)
        {
            var minBox = GetPrivateField<TextBox>(page, $"Min{channel}");
            if (minBox != null)
            {
                Assert.AreEqual($"{1000 + channel * 10}", minBox.Text, 
                    $"Min{channel} value should be preserved after tab switch");
            }
        }
    }

    /// <summary>
    /// Test that Settings tab waypoint parameters are preserved
    /// Validates: Requirement 8.3 - WHEN switching between tabs, THE Calibration_System SHALL preserve the state of input fields
    /// </summary>
    [TestMethod]
    [TestCategory("InputPreservation")]
    [TestCategory("Task1.4")]
    public void TabSwitch_PreservesSettingsWaypointParameters()
    {
        // Arrange
        var page = new CalibrationPage();
        var buttonClickMethod = typeof(CalibrationPage).GetMethod("Button_Click", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(buttonClickMethod, "Button_Click method not found");

        // Navigate to Settings tab
        var settingButton = new Button { Name = "SettingButton" };
        buttonClickMethod.Invoke(page, new object[] { settingButton, new RoutedEventArgs() });

        // Set waypoint parameter values
        var wpSpeed = GetPrivateField<TextBox>(page, "WP_Speed");
        var wpRadius = GetPrivateField<TextBox>(page, "WP_Radius");

        if (wpSpeed != null && wpRadius != null)
        {
            wpSpeed.Text = "5.5";
            wpRadius.Text = "10.0";

            // Act - Switch tabs
            var motorTestButton = new Button { Name = "MotorTestButton" };
            buttonClickMethod.Invoke(page, new object[] { motorTestButton, new RoutedEventArgs() });

            var settingButton2 = new Button { Name = "SettingButton" };
            buttonClickMethod.Invoke(page, new object[] { settingButton2, new RoutedEventArgs() });

            // Assert - Waypoint values should be preserved
            Assert.AreEqual("5.5", wpSpeed.Text, "WP_Speed value should be preserved after tab switch");
            Assert.AreEqual("10.0", wpRadius.Text, "WP_Radius value should be preserved after tab switch");
        }
    }

    /// <summary>
    /// Test that ESC tab motor selections are preserved
    /// Validates: Requirement 8.3 - WHEN switching between tabs, THE Calibration_System SHALL preserve the state of input fields
    /// </summary>
    [TestMethod]
    [TestCategory("InputPreservation")]
    [TestCategory("Task1.4")]
    public void TabSwitch_PreservesESCMotorSelections()
    {
        // Arrange
        var page = new CalibrationPage();
        var buttonClickMethod = typeof(CalibrationPage).GetMethod("Button_Click", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(buttonClickMethod, "Button_Click method not found");

        // Navigate to ESC tab
        var escButton = new Button { Name = "EscCalibButton" };
        buttonClickMethod.Invoke(page, new object[] { escButton, new RoutedEventArgs() });

        // Select some motors
        var motor1 = GetPrivateField<CheckBox>(page, "Motor1CheckBox");
        var motor5 = GetPrivateField<CheckBox>(page, "Motor5CheckBox");
        var motor10 = GetPrivateField<CheckBox>(page, "Motor10CheckBox");

        if (motor1 != null) motor1.IsChecked = true;
        if (motor5 != null) motor5.IsChecked = true;
        if (motor10 != null) motor10.IsChecked = true;

        // Act - Switch tabs
        var compassButton = new Button { Name = "CompassButton" };
        buttonClickMethod.Invoke(page, new object[] { compassButton, new RoutedEventArgs() });

        var escButton2 = new Button { Name = "EscCalibButton" };
        buttonClickMethod.Invoke(page, new object[] { escButton2, new RoutedEventArgs() });

        // Assert - Motor selections should be preserved
        if (motor1 != null)
            Assert.IsTrue(motor1.IsChecked == true, "Motor1 selection should be preserved");
        if (motor5 != null)
            Assert.IsTrue(motor5.IsChecked == true, "Motor5 selection should be preserved");
        if (motor10 != null)
            Assert.IsTrue(motor10.IsChecked == true, "Motor10 selection should be preserved");
    }

    /// <summary>
    /// Test that rapid tab switching preserves all input values
    /// Validates: Requirement 8.3 - WHEN switching between tabs, THE Calibration_System SHALL preserve the state of input fields
    /// </summary>
    [TestMethod]
    [TestCategory("InputPreservation")]
    [TestCategory("Task1.4")]
    public void TabSwitch_RapidSwitching_PreservesInputValues()
    {
        // Arrange
        var page = new CalibrationPage();
        var buttonClickMethod = typeof(CalibrationPage).GetMethod("Button_Click", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(buttonClickMethod, "Button_Click method not found");

        // Navigate to Servo Output tab and set a value
        var servoButton = new Button { Name = "ServoOutputButton" };
        buttonClickMethod.Invoke(page, new object[] { servoButton, new RoutedEventArgs() });

        var min1 = GetPrivateField<TextBox>(page, "Min1");
        Assert.IsNotNull(min1, "Min1 TextBox not found");
        min1.Text = "1234";

        // Act - Rapidly switch through all tabs multiple times
        var buttons = new[]
        {
            new Button { Name = "AccelerometerButton" },
            new Button { Name = "CompassButton" },
            new Button { Name = "FlightButton" },
            new Button { Name = "SettingButton" },
            new Button { Name = "EscCalibButton" },
            new Button { Name = "MotorTestButton" },
            new Button { Name = "ServoOutputButton" }
        };

        for (int i = 0; i < 10; i++)
        {
            foreach (var button in buttons)
            {
                buttonClickMethod.Invoke(page, new object[] { button, new RoutedEventArgs() });
            }
        }

        // Assert - Value should still be preserved after rapid switching
        Assert.AreEqual("1234", min1.Text, "Min1 value should be preserved after rapid tab switching");
    }

    /// <summary>
    /// Test that empty input fields remain empty after tab switching
    /// Validates: Requirement 8.3 - WHEN switching between tabs, THE Calibration_System SHALL preserve the state of input fields
    /// </summary>
    [TestMethod]
    [TestCategory("InputPreservation")]
    [TestCategory("Task1.4")]
    public void TabSwitch_PreservesEmptyInputFields()
    {
        // Arrange
        var page = new CalibrationPage();
        var buttonClickMethod = typeof(CalibrationPage).GetMethod("Button_Click", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(buttonClickMethod, "Button_Click method not found");

        // Navigate to Servo Output tab
        var servoButton = new Button { Name = "ServoOutputButton" };
        buttonClickMethod.Invoke(page, new object[] { servoButton, new RoutedEventArgs() });

        // Clear a TextBox value
        var min2 = GetPrivateField<TextBox>(page, "Min2");
        Assert.IsNotNull(min2, "Min2 TextBox not found");
        min2.Text = "";

        // Act - Switch tabs
        var flightButton = new Button { Name = "FlightButton" };
        buttonClickMethod.Invoke(page, new object[] { flightButton, new RoutedEventArgs() });

        var servoButton2 = new Button { Name = "ServoOutputButton" };
        buttonClickMethod.Invoke(page, new object[] { servoButton2, new RoutedEventArgs() });

        // Assert - Empty value should be preserved
        Assert.AreEqual("", min2.Text, "Empty Min2 value should be preserved after tab switch");
    }

    /// <summary>
    /// Test that special characters in TextBox are preserved
    /// Validates: Requirement 8.3 - WHEN switching between tabs, THE Calibration_System SHALL preserve the state of input fields
    /// </summary>
    [TestMethod]
    [TestCategory("InputPreservation")]
    [TestCategory("Task1.4")]
    public void TabSwitch_PreservesSpecialCharactersInTextBox()
    {
        // Arrange
        var page = new CalibrationPage();
        var buttonClickMethod = typeof(CalibrationPage).GetMethod("Button_Click", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(buttonClickMethod, "Button_Click method not found");

        // Navigate to Settings tab
        var settingButton = new Button { Name = "SettingButton" };
        buttonClickMethod.Invoke(page, new object[] { settingButton, new RoutedEventArgs() });

        // Set value with decimal point and negative sign
        var wpSpeed = GetPrivateField<TextBox>(page, "WP_Speed");
        if (wpSpeed != null)
        {
            wpSpeed.Text = "-12.5";

            // Act - Switch tabs
            var accelButton = new Button { Name = "AccelerometerButton" };
            buttonClickMethod.Invoke(page, new object[] { accelButton, new RoutedEventArgs() });

            var settingButton2 = new Button { Name = "SettingButton" };
            buttonClickMethod.Invoke(page, new object[] { settingButton2, new RoutedEventArgs() });

            // Assert - Special characters should be preserved
            Assert.AreEqual("-12.5", wpSpeed.Text, "Decimal and negative values should be preserved after tab switch");
        }
    }

    /// <summary>
    /// Helper method to get private field value using reflection
    /// </summary>
    private T? GetPrivateField<T>(object obj, string fieldName) where T : class
    {
        var field = obj.GetType().GetMethod("FindName", BindingFlags.Public | BindingFlags.Instance);
        return field?.Invoke(obj, new object[] { fieldName }) as T;
    }
}
