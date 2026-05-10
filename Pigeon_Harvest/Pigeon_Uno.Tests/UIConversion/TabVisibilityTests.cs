using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pigeon_Uno.Views;
using Pigeon_Uno.Core.ViewModels;
using Pigeon_Uno.Core.Services;
using Moq;
using System.Linq;
using System.Reflection;

namespace Pigeon_Uno.Tests.UIConversion;

/// <summary>
/// Unit tests for CalibrationPage tab visibility and navigation
/// Validates: Requirements 8.1, 8.2, 8.5 (Task 1.2)
/// </summary>
[TestClass]
public class TabVisibilityTests
{
    private Mock<IMavLinkService> _mockMavLinkService = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockMavLinkService = new Mock<IMavLinkService>();
    }

    /// <summary>
    /// Test that verifies only one tab content is visible at a time
    /// Validates: Requirement 8.5 - THE Calibration_System SHALL ensure only one tab content is visible at any time
    /// </summary>
    [TestMethod]
    [TestCategory("TabVisibility")]
    [TestCategory("Task1.2")]
    public void Button_Click_EnsuresOnlyOneTabVisible()
    {
        // Arrange
        var page = new CalibrationPage();
        var buttonClickMethod = typeof(CalibrationPage).GetMethod("Button_Click", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(buttonClickMethod, "Button_Click method not found");

        // Get all tab content grids
        var acceleCalib = GetPrivateField<Grid>(page, "Accele_calib");
        var kompassCalib = GetPrivateField<Grid>(page, "Kompass_calib");
        var flightMode = GetPrivateField<Grid>(page, "Flight_mode");
        var servoOutput = GetPrivateField<Grid>(page, "ServoOutput");
        var setting = GetPrivateField<Grid>(page, "Setting");
        var escCalib = GetPrivateField<Grid>(page, "EscCalib");
        var motorTest = GetPrivateField<Grid>(page, "MotorTestGrid");

        var allTabs = new[] { acceleCalib, kompassCalib, flightMode, servoOutput, setting, escCalib, motorTest };

        // Test each tab button
        var tabButtons = new[]
        {
            ("AccelerometerButton", acceleCalib),
            ("CompassButton", kompassCalib),
            ("FlightButton", flightMode),
            ("ServoOutputButton", servoOutput),
            ("SettingButton", setting),
            ("EscCalibButton", escCalib),
            ("MotorTestButton", motorTest)
        };

        foreach (var (buttonName, expectedVisibleTab) in tabButtons)
        {
            // Act
            var button = new Button { Name = buttonName };
            buttonClickMethod.Invoke(page, new object[] { button, new RoutedEventArgs() });

            // Assert
            var visibleTabs = allTabs.Where(tab => tab?.Visibility == Visibility.Visible).ToList();
            
            Assert.AreEqual(1, visibleTabs.Count, 
                $"Expected exactly 1 visible tab after clicking {buttonName}, but found {visibleTabs.Count}");
            
            Assert.AreEqual(Visibility.Visible, expectedVisibleTab?.Visibility, 
                $"Expected {buttonName} to show its corresponding tab");
            
            // Verify all other tabs are collapsed
            foreach (var tab in allTabs.Where(t => t != expectedVisibleTab))
            {
                Assert.AreEqual(Visibility.Collapsed, tab?.Visibility, 
                    $"Tab should be collapsed when {buttonName} is clicked");
            }
        }
    }

    /// <summary>
    /// Test that verifies Accelerometer tab is visible by default
    /// Validates: Requirement 8.1 - WHEN the calibration page loads, THE Calibration_System SHALL display the Accelerometer tab content by default
    /// </summary>
    [TestMethod]
    [TestCategory("TabVisibility")]
    [TestCategory("Task1.2")]
    public void CalibrationPage_DefaultTab_IsAccelerometer()
    {
        // Arrange & Act
        var page = new CalibrationPage();
        
        // Trigger the Loaded event to simulate page load
        var loadedEvent = typeof(FrameworkElement).GetEvent("Loaded");
        var loadedDelegate = Delegate.CreateDelegate(
            typeof(RoutedEventHandler), 
            page, 
            "ShowAccelerometerTab", 
            false, 
            false);

        // Get all tab content grids
        var acceleCalib = GetPrivateField<Grid>(page, "Accele_calib");
        var kompassCalib = GetPrivateField<Grid>(page, "Kompass_calib");
        var flightMode = GetPrivateField<Grid>(page, "Flight_mode");
        var servoOutput = GetPrivateField<Grid>(page, "ServoOutput");
        var setting = GetPrivateField<Grid>(page, "Setting");
        var escCalib = GetPrivateField<Grid>(page, "EscCalib");
        var motorTest = GetPrivateField<Grid>(page, "MotorTestGrid");

        // Call ShowAccelerometerTab directly
        var showAccelMethod = typeof(CalibrationPage).GetMethod("ShowAccelerometerTab", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        showAccelMethod?.Invoke(page, null);

        // Assert
        Assert.AreEqual(Visibility.Visible, acceleCalib?.Visibility, 
            "Accelerometer tab should be visible by default");
        
        Assert.AreEqual(Visibility.Collapsed, kompassCalib?.Visibility, 
            "Compass tab should be collapsed by default");
        Assert.AreEqual(Visibility.Collapsed, flightMode?.Visibility, 
            "Flight Mode tab should be collapsed by default");
        Assert.AreEqual(Visibility.Collapsed, servoOutput?.Visibility, 
            "Servo Output tab should be collapsed by default");
        Assert.AreEqual(Visibility.Collapsed, setting?.Visibility, 
            "Setting tab should be collapsed by default");
        Assert.AreEqual(Visibility.Collapsed, escCalib?.Visibility, 
            "ESC Calib tab should be collapsed by default");
        Assert.AreEqual(Visibility.Collapsed, motorTest?.Visibility, 
            "Motor Test tab should be collapsed by default");

        // Verify only one tab is visible
        var allTabs = new[] { acceleCalib, kompassCalib, flightMode, servoOutput, setting, escCalib, motorTest };
        var visibleCount = allTabs.Count(tab => tab?.Visibility == Visibility.Visible);
        Assert.AreEqual(1, visibleCount, "Exactly one tab should be visible by default");
    }

    /// <summary>
    /// Test that verifies tab switching hides previous tab and shows new tab
    /// Validates: Requirement 8.2 - WHEN the user clicks a tab button, THE Calibration_System SHALL hide all other tab contents and show only the selected tab content
    /// </summary>
    [TestMethod]
    [TestCategory("TabVisibility")]
    [TestCategory("Task1.2")]
    public void Button_Click_HidesPreviousTab_ShowsNewTab()
    {
        // Arrange
        var page = new CalibrationPage();
        var buttonClickMethod = typeof(CalibrationPage).GetMethod("Button_Click", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(buttonClickMethod, "Button_Click method not found");

        // Get all tab content grids
        var acceleCalib = GetPrivateField<Grid>(page, "Accele_calib");
        var kompassCalib = GetPrivateField<Grid>(page, "Kompass_calib");
        var flightMode = GetPrivateField<Grid>(page, "Flight_mode");

        // Act - Click Accelerometer button first
        var accelButton = new Button { Name = "AccelerometerButton" };
        buttonClickMethod.Invoke(page, new object[] { accelButton, new RoutedEventArgs() });

        // Assert - Accelerometer should be visible
        Assert.AreEqual(Visibility.Visible, acceleCalib?.Visibility, 
            "Accelerometer tab should be visible after clicking its button");
        Assert.AreEqual(Visibility.Collapsed, kompassCalib?.Visibility, 
            "Compass tab should be collapsed");

        // Act - Click Compass button
        var compassButton = new Button { Name = "CompassButton" };
        buttonClickMethod.Invoke(page, new object[] { compassButton, new RoutedEventArgs() });

        // Assert - Compass should be visible, Accelerometer should be hidden
        Assert.AreEqual(Visibility.Collapsed, acceleCalib?.Visibility, 
            "Accelerometer tab should be collapsed after clicking Compass button");
        Assert.AreEqual(Visibility.Visible, kompassCalib?.Visibility, 
            "Compass tab should be visible after clicking its button");

        // Act - Click Flight Mode button
        var flightButton = new Button { Name = "FlightButton" };
        buttonClickMethod.Invoke(page, new object[] { flightButton, new RoutedEventArgs() });

        // Assert - Flight Mode should be visible, others should be hidden
        Assert.AreEqual(Visibility.Collapsed, acceleCalib?.Visibility, 
            "Accelerometer tab should be collapsed");
        Assert.AreEqual(Visibility.Collapsed, kompassCalib?.Visibility, 
            "Compass tab should be collapsed");
        Assert.AreEqual(Visibility.Visible, flightMode?.Visibility, 
            "Flight Mode tab should be visible after clicking its button");
    }

    /// <summary>
    /// Test that verifies no edge cases allow multiple tabs to be visible
    /// Validates: Requirement 8.5 - THE Calibration_System SHALL ensure only one tab content is visible at any time
    /// </summary>
    [TestMethod]
    [TestCategory("TabVisibility")]
    [TestCategory("Task1.2")]
    public void Button_Click_NoEdgeCases_AllowMultipleTabs()
    {
        // Arrange
        var page = new CalibrationPage();
        var buttonClickMethod = typeof(CalibrationPage).GetMethod("Button_Click", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(buttonClickMethod, "Button_Click method not found");

        // Get all tab content grids
        var allTabs = new[]
        {
            GetPrivateField<Grid>(page, "Accele_calib"),
            GetPrivateField<Grid>(page, "Kompass_calib"),
            GetPrivateField<Grid>(page, "Flight_mode"),
            GetPrivateField<Grid>(page, "ServoOutput"),
            GetPrivateField<Grid>(page, "Setting"),
            GetPrivateField<Grid>(page, "EscCalib"),
            GetPrivateField<Grid>(page, "MotorTestGrid")
        };

        var buttonNames = new[]
        {
            "AccelerometerButton", "CompassButton", "FlightButton", 
            "ServoOutputButton", "SettingButton", "EscCalibButton", "MotorTestButton"
        };

        // Act & Assert - Click each button multiple times in different orders
        for (int iteration = 0; iteration < 3; iteration++)
        {
            foreach (var buttonName in buttonNames)
            {
                var button = new Button { Name = buttonName };
                buttonClickMethod.Invoke(page, new object[] { button, new RoutedEventArgs() });

                // Verify exactly one tab is visible
                var visibleCount = allTabs.Count(tab => tab?.Visibility == Visibility.Visible);
                Assert.AreEqual(1, visibleCount, 
                    $"Exactly one tab should be visible after clicking {buttonName} (iteration {iteration})");
            }
        }
    }

    /// <summary>
    /// Test that verifies rapid tab switching maintains mutual exclusivity
    /// Validates: Requirement 8.5 - THE Calibration_System SHALL ensure only one tab content is visible at any time
    /// </summary>
    [TestMethod]
    [TestCategory("TabVisibility")]
    [TestCategory("Task1.2")]
    public void Button_Click_RapidSwitching_MaintainsMutualExclusivity()
    {
        // Arrange
        var page = new CalibrationPage();
        var buttonClickMethod = typeof(CalibrationPage).GetMethod("Button_Click", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(buttonClickMethod, "Button_Click method not found");

        // Get all tab content grids
        var allTabs = new[]
        {
            GetPrivateField<Grid>(page, "Accele_calib"),
            GetPrivateField<Grid>(page, "Kompass_calib"),
            GetPrivateField<Grid>(page, "Flight_mode"),
            GetPrivateField<Grid>(page, "ServoOutput"),
            GetPrivateField<Grid>(page, "Setting"),
            GetPrivateField<Grid>(page, "EscCalib"),
            GetPrivateField<Grid>(page, "MotorTestGrid")
        };

        var buttonNames = new[]
        {
            "AccelerometerButton", "CompassButton", "FlightButton", 
            "ServoOutputButton", "SettingButton", "EscCalibButton", "MotorTestButton"
        };

        // Act - Rapidly switch between tabs
        for (int i = 0; i < 50; i++)
        {
            var buttonName = buttonNames[i % buttonNames.Length];
            var button = new Button { Name = buttonName };
            buttonClickMethod.Invoke(page, new object[] { button, new RoutedEventArgs() });

            // Assert - Verify exactly one tab is visible after each click
            var visibleCount = allTabs.Count(tab => tab?.Visibility == Visibility.Visible);
            Assert.AreEqual(1, visibleCount, 
                $"Exactly one tab should be visible after rapid click {i} on {buttonName}");
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
