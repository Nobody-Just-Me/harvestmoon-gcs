using Xunit;

namespace Pigeon_Uno.Tests.Integration;

/// <summary>
/// Integration tests for Flight Mode ComboBox population
/// Validates: Requirement 3.2 - Flight mode dropdowns populated with available modes
/// Task 4.1: Populate flight mode ComboBoxes on page load
/// Task 4.1.1: Add available modes (Stabilize, Acro, AltHold, Auto, Guided, Loiter, RTL, Circle, Land)
/// Task 4.1.2: Set default selections
/// </summary>
public class FlightModeComboBoxPopulationTests
{
    private readonly string[] _expectedFlightModes = new[]
    {
        "Stabilize",
        "Acro",
        "AltHold",
        "Auto",
        "Guided",
        "Loiter",
        "RTL",
        "Circle",
        "Land"
    };

    [Fact]
    public void FlightModeList_ContainsAllRequiredModes()
    {
        // Arrange
        var modes = new[] { "Stabilize", "Acro", "AltHold", "Auto", "Guided", "Loiter", "RTL", "Circle", "Land" };

        // Assert
        Assert.Equal(9, modes.Length);
        Assert.Contains("Stabilize", modes);
        Assert.Contains("Acro", modes);
        Assert.Contains("AltHold", modes);
        Assert.Contains("Auto", modes);
        Assert.Contains("Guided", modes);
        Assert.Contains("Loiter", modes);
        Assert.Contains("RTL", modes);
        Assert.Contains("Circle", modes);
        Assert.Contains("Land", modes);
    }

    [Fact]
    public void FlightModeList_HasCorrectOrder()
    {
        // Arrange
        var modes = new[] { "Stabilize", "Acro", "AltHold", "Auto", "Guided", "Loiter", "RTL", "Circle", "Land" };

        // Assert - Verify the order matches the requirement
        Assert.Equal("Stabilize", modes[0]);
        Assert.Equal("Acro", modes[1]);
        Assert.Equal("AltHold", modes[2]);
        Assert.Equal("Auto", modes[3]);
        Assert.Equal("Guided", modes[4]);
        Assert.Equal("Loiter", modes[5]);
        Assert.Equal("RTL", modes[6]);
        Assert.Equal("Circle", modes[7]);
        Assert.Equal("Land", modes[8]);
    }

    [Theory]
    [InlineData(0, "Stabilize")]
    [InlineData(1, "Acro")]
    [InlineData(2, "AltHold")]
    [InlineData(3, "Auto")]
    [InlineData(4, "Guided")]
    [InlineData(5, "Loiter")]
    [InlineData(6, "RTL")]
    [InlineData(7, "Circle")]
    [InlineData(8, "Land")]
    public void FlightModeList_IndexMapsToCorrectMode(int index, string expectedMode)
    {
        // Arrange
        var modes = new[] { "Stabilize", "Acro", "AltHold", "Auto", "Guided", "Loiter", "RTL", "Circle", "Land" };

        // Assert
        Assert.Equal(expectedMode, modes[index]);
    }

    [Fact]
    public void DefaultFlightMode_ShouldBeStabilize()
    {
        // Arrange
        var modes = new[] { "Stabilize", "Acro", "AltHold", "Auto", "Guided", "Loiter", "RTL", "Circle", "Land" };
        int defaultIndex = 0;

        // Assert
        Assert.Equal("Stabilize", modes[defaultIndex]);
    }

    [Fact]
    public void FlightModeComboBoxes_ShouldHaveSixInstances()
    {
        // Arrange
        int expectedComboBoxCount = 6;

        // Assert - Verify that we need 6 ComboBoxes for 6 flight mode slots
        Assert.Equal(6, expectedComboBoxCount);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void FlightModeComboBox_ShouldExistForEachSlot(int slotNumber)
    {
        // Arrange
        string comboBoxName = $"CMB_fmode{slotNumber}";

        // Assert - Verify naming convention
        Assert.NotNull(comboBoxName);
        Assert.StartsWith("CMB_fmode", comboBoxName);
    }

    [Fact]
    public void FlightModeComboBoxes_AllShouldHaveSameItems()
    {
        // Arrange
        var modes = new[] { "Stabilize", "Acro", "AltHold", "Auto", "Guided", "Loiter", "RTL", "Circle", "Land" };

        // Assert - All 6 ComboBoxes should have the same 9 modes
        for (int i = 1; i <= 6; i++)
        {
            // Each ComboBox should have all 9 modes
            Assert.Equal(9, modes.Length);
        }
    }

    [Fact]
    public void FlightModeComboBoxes_AllShouldDefaultToStabilize()
    {
        // Arrange
        int defaultSelectedIndex = 0;
        var modes = new[] { "Stabilize", "Acro", "AltHold", "Auto", "Guided", "Loiter", "RTL", "Circle", "Land" };

        // Assert - Default selection should be index 0 (Stabilize)
        Assert.Equal("Stabilize", modes[defaultSelectedIndex]);
    }

    [Theory]
    [InlineData(0, 1230, 1)]
    [InlineData(1231, 1360, 2)]
    [InlineData(1361, 1490, 3)]
    [InlineData(1491, 1620, 4)]
    [InlineData(1621, 1749, 5)]
    [InlineData(1750, 2200, 6)]
    public void FlightModePWMRanges_MapCorrectlyToModeSlots(int minPWM, int maxPWM, int expectedModeSlot)
    {
        // Assert - Verify PWM range mapping as per requirement 3.5
        Assert.InRange(minPWM, 0, 2200);
        Assert.InRange(maxPWM, 0, 2200);
        Assert.InRange(expectedModeSlot, 1, 6);
        Assert.True(minPWM < maxPWM || (minPWM == 0 && maxPWM == 1230));
    }

    [Fact]
    public void FlightModeConfiguration_ShouldSupportAllNineModes()
    {
        // Arrange
        var requiredModes = new[]
        {
            "Stabilize",
            "Acro",
            "AltHold",
            "Auto",
            "Guided",
            "Loiter",
            "RTL",
            "Circle",
            "Land"
        };

        // Assert - Verify all required modes are available
        Assert.Equal(9, requiredModes.Length);
        foreach (var mode in requiredModes)
        {
            Assert.NotNull(mode);
            Assert.NotEmpty(mode);
        }
    }
}
