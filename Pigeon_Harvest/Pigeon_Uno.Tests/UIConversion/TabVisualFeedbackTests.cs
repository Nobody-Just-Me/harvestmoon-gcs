using Xunit;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Pigeon_Uno.Views;
using Pigeon_Uno.Core.Services;
using Moq;
using System.Reflection;
using System.Linq;

namespace Pigeon_Uno.Tests.UIConversion;

/// <summary>
/// Unit tests for CalibrationPage tab visual feedback
/// Validates: Requirement 8.4 (Task 1.5)
/// </summary>
public class TabVisualFeedbackTests
{
    private readonly Mock<IMavLinkService> _mockMavLinkService;

    public TabVisualFeedbackTests()
    {
        _mockMavLinkService = new Mock<IMavLinkService>();
    }

    /// <summary>
    /// Test that verifies the selected tab button has visual feedback
    /// Validates: Requirement 8.4 - THE Calibration_System SHALL provide visual feedback for the currently selected tab
    /// </summary>
    [Fact]
    [Trait("Category", "TabVisualFeedback")]
    [Trait("Category", "Task1.5")]
    public void Button_Click_AppliesVisualFeedbackToSelectedTab()
    {
        // Arrange
        var page = new CalibrationPage();
        var buttonClickMethod = typeof(CalibrationPage).GetMethod("Button_Click", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(buttonClickMethod);

        // Get all tab buttons
        var accelButton = GetPrivateField<Button>(page, "AccelerometerButton");
        var compassButton = GetPrivateField<Button>(page, "CompassButton");
        var flightButton = GetPrivateField<Button>(page, "FlightButton");

        Assert.NotNull(accelButton);
        Assert.NotNull(compassButton);
        Assert.NotNull(flightButton);

        // Act - Click Accelerometer button
        buttonClickMethod.Invoke(page, new object[] { accelButton, new RoutedEventArgs() });

        // Assert - Accelerometer button should have visual feedback
        Assert.NotNull(accelButton.Background);
        var accelBrush = accelButton.Background as SolidColorBrush;
        Assert.NotNull(accelBrush);
        Assert.NotEqual(Microsoft.UI.Colors.Transparent, accelBrush.Color);
        Assert.Equal(Microsoft.UI.Text.FontWeights.Bold, accelButton.FontWeight);

        // Assert - Other buttons should not have visual feedback
        var compassBrush = compassButton.Background as SolidColorBrush;
        Assert.NotNull(compassBrush);
        Assert.Equal(Microsoft.UI.Colors.Transparent, compassBrush.Color);
        Assert.Equal(Microsoft.UI.Text.FontWeights.Normal, compassButton.FontWeight);

        // Act - Click Compass button
        buttonClickMethod.Invoke(page, new object[] { compassButton, new RoutedEventArgs() });

        // Assert - Compass button should now have visual feedback
        compassBrush = compassButton.Background as SolidColorBrush;
        Assert.NotNull(compassBrush);
        Assert.NotEqual(Microsoft.UI.Colors.Transparent, compassBrush.Color);
        Assert.Equal(Microsoft.UI.Text.FontWeights.Bold, compassButton.FontWeight);

        // Assert - Accelerometer button should no longer have visual feedback
        accelBrush = accelButton.Background as SolidColorBrush;
        Assert.NotNull(accelBrush);
        Assert.Equal(Microsoft.UI.Colors.Transparent, accelBrush.Color);
        Assert.Equal(Microsoft.UI.Text.FontWeights.Normal, accelButton.FontWeight);
    }

    /// <summary>
    /// Test that verifies only one tab button has visual feedback at a time
    /// Validates: Requirement 8.4 - THE Calibration_System SHALL provide visual feedback for the currently selected tab
    /// </summary>
    [Fact]
    [Trait("Category", "TabVisualFeedback")]
    [Trait("Category", "Task1.5")]
    public void Button_Click_OnlyOneButtonHasVisualFeedback()
    {
        // Arrange
        var page = new CalibrationPage();
        var buttonClickMethod = typeof(CalibrationPage).GetMethod("Button_Click", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(buttonClickMethod);

        // Get all tab buttons
        var allButtons = new[]
        {
            GetPrivateField<Button>(page, "AccelerometerButton"),
            GetPrivateField<Button>(page, "CompassButton"),
            GetPrivateField<Button>(page, "FlightButton"),
            GetPrivateField<Button>(page, "ServoOutputButton"),
            GetPrivateField<Button>(page, "SettingButton"),
            GetPrivateField<Button>(page, "EscCalibButton"),
            GetPrivateField<Button>(page, "MotorTestButton")
        };

        // Verify all buttons exist
        foreach (var button in allButtons)
        {
            Assert.NotNull(button);
        }

        // Test each button
        foreach (var selectedButton in allButtons)
        {
            // Act - Click the button
            buttonClickMethod.Invoke(page, new object[] { selectedButton, new RoutedEventArgs() });

            // Assert - Count buttons with visual feedback
            int buttonsWithFeedback = 0;
            foreach (var button in allButtons)
            {
                var brush = button!.Background as SolidColorBrush;
                if (brush != null && brush.Color != Microsoft.UI.Colors.Transparent)
                {
                    buttonsWithFeedback++;
                }
            }

            Assert.Equal(1, buttonsWithFeedback);

            // Assert - The selected button has visual feedback
            var selectedBrush = selectedButton!.Background as SolidColorBrush;
            Assert.NotNull(selectedBrush);
            Assert.NotEqual(Microsoft.UI.Colors.Transparent, selectedBrush.Color);
            Assert.Equal(Microsoft.UI.Text.FontWeights.Bold, selectedButton.FontWeight);
        }
    }

    /// <summary>
    /// Test that verifies Accelerometer button has visual feedback by default
    /// Validates: Requirement 8.4 - THE Calibration_System SHALL provide visual feedback for the currently selected tab
    /// </summary>
    [Fact]
    [Trait("Category", "TabVisualFeedback")]
    [Trait("Category", "Task1.5")]
    public void CalibrationPage_DefaultTab_HasVisualFeedback()
    {
        // Arrange & Act
        var page = new CalibrationPage();
        
        // Call ShowAccelerometerTab to simulate page load
        var showAccelMethod = typeof(CalibrationPage).GetMethod("ShowAccelerometerTab", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        showAccelMethod?.Invoke(page, null);

        // Get Accelerometer button
        var accelButton = GetPrivateField<Button>(page, "AccelerometerButton");
        Assert.NotNull(accelButton);

        // Assert - Accelerometer button should have visual feedback
        var brush = accelButton.Background as SolidColorBrush;
        Assert.NotNull(brush);
        Assert.NotEqual(Microsoft.UI.Colors.Transparent, brush.Color);
        Assert.Equal(Microsoft.UI.Text.FontWeights.Bold, accelButton.FontWeight);
    }

    /// <summary>
    /// Test that verifies visual feedback persists through rapid tab switching
    /// Validates: Requirement 8.4 - THE Calibration_System SHALL provide visual feedback for the currently selected tab
    /// </summary>
    [Fact]
    [Trait("Category", "TabVisualFeedback")]
    [Trait("Category", "Task1.5")]
    public void Button_Click_RapidSwitching_MaintainsVisualFeedback()
    {
        // Arrange
        var page = new CalibrationPage();
        var buttonClickMethod = typeof(CalibrationPage).GetMethod("Button_Click", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(buttonClickMethod);

        // Get all tab buttons
        var allButtons = new[]
        {
            GetPrivateField<Button>(page, "AccelerometerButton"),
            GetPrivateField<Button>(page, "CompassButton"),
            GetPrivateField<Button>(page, "FlightButton"),
            GetPrivateField<Button>(page, "ServoOutputButton"),
            GetPrivateField<Button>(page, "SettingButton"),
            GetPrivateField<Button>(page, "EscCalibButton"),
            GetPrivateField<Button>(page, "MotorTestButton")
        };

        // Act - Rapidly switch between tabs
        for (int i = 0; i < 50; i++)
        {
            var selectedButton = allButtons[i % allButtons.Length];
            buttonClickMethod.Invoke(page, new object[] { selectedButton, new RoutedEventArgs() });

            // Assert - Exactly one button should have visual feedback
            int buttonsWithFeedback = 0;
            Button? buttonWithFeedback = null;
            
            foreach (var button in allButtons)
            {
                var brush = button!.Background as SolidColorBrush;
                if (brush != null && brush.Color != Microsoft.UI.Colors.Transparent)
                {
                    buttonsWithFeedback++;
                    buttonWithFeedback = button;
                }
            }

            Assert.Equal(1, buttonsWithFeedback);
            Assert.Equal(selectedButton, buttonWithFeedback);
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
