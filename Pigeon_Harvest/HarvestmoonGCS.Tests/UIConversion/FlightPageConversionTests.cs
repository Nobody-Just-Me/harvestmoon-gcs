using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace HarvestmoonGCS.Tests.UIConversion;

/// <summary>
/// Unit tests for FlightPage XAML conversion from WPF to Uno Platform
/// </summary>
[TestClass]
public class FlightPageConversionTests : PageConversionTestBase
{
    private const string WpfPath = "Custom UserControls/FlightControl.xaml";
    private const string UnoPath = "Views/FlightPage.xaml";
    private const string PageName = "FlightPage";

    [TestMethod]
    public void FlightPage_GridStructure_MatchesWpf()
    {
        // Arrange
        var wpfXaml = LoadWpfXaml(WpfPath);
        var unoXaml = LoadUnoXaml(UnoPath);

        // Act
        var wpfGrid = XamlParsingUtilities.ParseGridStructure(wpfXaml);
        var unoGrid = XamlParsingUtilities.ParseGridStructure(unoXaml);

        // Assert
        AssertGridStructureEquals(wpfGrid, unoGrid, PageName);
    }

    [TestMethod]
    public void FlightPage_AllBindings_AreValid()
    {
        // Arrange
        var unoXaml = LoadUnoXaml(UnoPath);

        // Act
        var bindings = XamlParsingUtilities.ExtractBindings(unoXaml);

        // Assert
        Assert.IsTrue(bindings.Count > 0, "FlightPage should have bindings");

        foreach (var binding in bindings)
        {
            Assert.IsTrue(binding.BindingExpression.StartsWith("{Binding"), 
                $"Invalid binding syntax in {binding.ControlName ?? binding.ControlType}.{binding.Property}");
            Assert.IsTrue(binding.BindingExpression.EndsWith("}"), 
                $"Invalid binding syntax in {binding.ControlName ?? binding.ControlType}.{binding.Property}");
        }
    }

    [TestMethod]
    public void FlightPage_AllEventHandlers_AreConnected()
    {
        // Arrange
        var unoXaml = LoadUnoXaml(UnoPath);

        // Act
        var handlers = XamlParsingUtilities.ExtractEventHandlers(unoXaml);

        // Assert
        foreach (var handler in handlers)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(handler.HandlerMethod), 
                $"Event handler for {handler.EventName} on {handler.ControlName ?? handler.ControlType} is empty");
        }
    }

    [TestMethod]
    public void FlightPage_AllStaticResources_AreValid()
    {
        // Arrange
        var unoXaml = LoadUnoXaml(UnoPath);

        // Act
        var resources = XamlParsingUtilities.ExtractResourceReferences(unoXaml);

        // Assert
        foreach (var resource in resources)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(resource.ResourceKey), 
                $"Resource key is empty for {resource.ControlName ?? resource.ControlType}.{resource.Property}");
            
            // Uno Platform doesn't support DynamicResource
            Assert.IsFalse(resource.IsDynamic, 
                $"DynamicResource not supported in Uno Platform: {resource.ResourceKey} in {resource.ControlName ?? resource.ControlType}.{resource.Property}");
        }
    }

    [TestMethod]
    public void FlightPage_AvionicsInstruments_ArePresent()
    {
        // Arrange
        var unoXaml = LoadUnoXaml(UnoPath);
        var controls = XamlParsingUtilities.ExtractControls(unoXaml);

        // Act
        var avionicsControls = controls.Where(c => 
            c.Type.Contains("AttitudeIndicator") ||
            c.Type.Contains("Altimeter") ||
            c.Type.Contains("HeadingIndicator") ||
            c.Type.Contains("AirspeedIndicator") ||
            c.Type.Contains("VerticalSpeedIndicator") ||
            c.Type.Contains("TurnCoordinator")).ToList();

        // Assert
        Assert.IsTrue(avionicsControls.Count >= 6, 
            $"FlightPage should have at least 6 avionics instruments, found {avionicsControls.Count}");
    }

    [TestMethod]
    public void FlightPage_GPSInfoPanel_HasRequiredControls()
    {
        // Arrange
        var unoXaml = LoadUnoXaml(UnoPath);
        var controls = XamlParsingUtilities.ExtractControls(unoXaml);

        // Act
        var gpsControls = controls.Where(c => 
            c.Name != null && (
                c.Name.Contains("Latitude") ||
                c.Name.Contains("Longitude") ||
                c.Name.Contains("HDOP") ||
                c.Name.Contains("Sats") ||
                c.Name.Contains("GPS"))).ToList();

        // Assert
        Assert.IsTrue(gpsControls.Count > 0, 
            "FlightPage should have GPS info controls");
    }

    [TestMethod]
    public void FlightPage_ArmButton_HasCorrectProperties()
    {
        // Arrange
        var unoXaml = LoadUnoXaml(UnoPath);
        var controls = XamlParsingUtilities.ExtractControls(unoXaml);

        // Act
        var armButton = controls.FirstOrDefault(c => 
            c.Name != null && c.Name.Contains("arm", System.StringComparison.OrdinalIgnoreCase));

        // Assert
        if (armButton != null)
        {
            Assert.AreEqual("Button", armButton.Type, "ARM control should be a Button");
            
            // Check for CornerRadius if it was in WPF
            var wpfXaml = LoadWpfXaml(WpfPath);
            var wpfControls = XamlParsingUtilities.ExtractControls(wpfXaml);
            var wpfArmButton = wpfControls.FirstOrDefault(c => 
                c.Name != null && c.Name.Contains("arm", System.StringComparison.OrdinalIgnoreCase));

            if (wpfArmButton != null && wpfArmButton.Properties.ContainsKey("CornerRadius"))
            {
                Assert.IsTrue(armButton.Properties.ContainsKey("CornerRadius"), 
                    "ARM button should have CornerRadius property");
            }
        }
    }
}
