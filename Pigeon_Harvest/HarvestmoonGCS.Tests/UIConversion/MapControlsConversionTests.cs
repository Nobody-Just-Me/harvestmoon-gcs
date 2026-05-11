using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Xml.Linq;
using System.Linq;

namespace HarvestmoonGCS.Tests.UIConversion;

/// <summary>
/// Tests for Task 5.7: Convert zoom and map controls
/// Verifies that map controls (follow_wahana button) are correctly converted from WPF to Uno
/// </summary>
[TestClass]
public class MapControlsConversionTests
{
    private XDocument _wpfXaml;
    private XDocument _unoXaml;
    private XNamespace _wpfNs;
    private XNamespace _unoNs;

    [TestInitialize]
    public void Setup()
    {
        // Load WPF XAML
        var wpfPath = "../../../../Pigeon_WPF_cs/Pigeon_WPF_cs/Custom UserControls/Waypoint.xaml";
        if (System.IO.File.Exists(wpfPath))
        {
            _wpfXaml = XDocument.Load(wpfPath);
            _wpfNs = _wpfXaml.Root.GetDefaultNamespace();
        }

        // Load Uno XAML
        var unoPath = "../../../Views/MapPage.xaml";
        _unoXaml = XDocument.Load(unoPath);
        _unoNs = _unoXaml.Root.GetDefaultNamespace();
    }

    [TestMethod]
    public void MapPage_FollowVehicleButton_BorderPropertiesMatch()
    {
        // Skip if WPF file not available
        if (_wpfXaml == null)
        {
            Assert.Inconclusive("WPF source file not available for comparison");
            return;
        }

        // Find follow_wahana_border in both files
        var wpfBorder = _wpfXaml.Descendants(_wpfNs + "Border")
            .FirstOrDefault(e => e.Attribute(XNamespace.Xml + "Name")?.Value == "follow_wahana_border" ||
                                 e.Attribute("Name")?.Value == "follow_wahana_border");

        var unoBorder = _unoXaml.Descendants(_unoNs + "Border")
            .FirstOrDefault(e => e.Attribute(XNamespace.Xml + "Name")?.Value == "follow_wahana_border" ||
                                 e.Attribute("Name")?.Value == "follow_wahana_border");

        Assert.IsNotNull(wpfBorder, "WPF follow_wahana_border not found");
        Assert.IsNotNull(unoBorder, "Uno follow_wahana_border not found");

        // Verify Border properties
        Assert.AreEqual(
            wpfBorder.Attribute("Background")?.Value,
            unoBorder.Attribute("Background")?.Value,
            "Border Background should match");

        Assert.AreEqual(
            wpfBorder.Attribute("CornerRadius")?.Value,
            unoBorder.Attribute("CornerRadius")?.Value,
            "Border CornerRadius should match");

        Assert.AreEqual(
            wpfBorder.Attribute("Width")?.Value,
            unoBorder.Attribute("Width")?.Value,
            "Border Width should match");

        Assert.AreEqual(
            wpfBorder.Attribute("Height")?.Value,
            unoBorder.Attribute("Height")?.Value,
            "Border Height should match");

        Assert.AreEqual(
            wpfBorder.Attribute("BorderThickness")?.Value,
            unoBorder.Attribute("BorderThickness")?.Value,
            "Border BorderThickness should match");
    }

    [TestMethod]
    public void MapPage_FollowVehicleButton_ButtonPropertiesMatch()
    {
        // Skip if WPF file not available
        if (_wpfXaml == null)
        {
            Assert.Inconclusive("WPF source file not available for comparison");
            return;
        }

        // Find the button inside follow_wahana_border
        var wpfBorder = _wpfXaml.Descendants(_wpfNs + "Border")
            .FirstOrDefault(e => e.Attribute(XNamespace.Xml + "Name")?.Value == "follow_wahana_border" ||
                                 e.Attribute("Name")?.Value == "follow_wahana_border");

        var unoBorder = _unoXaml.Descendants(_unoNs + "Border")
            .FirstOrDefault(e => e.Attribute(XNamespace.Xml + "Name")?.Value == "follow_wahana_border" ||
                                 e.Attribute("Name")?.Value == "follow_wahana_border");

        var wpfButton = wpfBorder?.Descendants(_wpfNs + "Button").FirstOrDefault();
        var unoButton = unoBorder?.Descendants(_unoNs + "Button").FirstOrDefault();

        Assert.IsNotNull(wpfButton, "WPF Button not found");
        Assert.IsNotNull(unoButton, "Uno Button not found");

        // Verify Button properties
        Assert.AreEqual(
            wpfButton.Attribute("ToolTip")?.Value,
            unoButton.Attribute("ToolTip")?.Value,
            "Button ToolTip should match");

        Assert.AreEqual(
            wpfButton.Attribute("BorderBrush")?.Value,
            unoButton.Attribute("BorderBrush")?.Value,
            "Button BorderBrush should match");

        Assert.AreEqual(
            wpfButton.Attribute("Background")?.Value,
            unoButton.Attribute("Background")?.Value,
            "Button Background should match");

        Assert.AreEqual(
            wpfButton.Attribute("Margin")?.Value,
            unoButton.Attribute("Margin")?.Value,
            "Button Margin should match");

        // Verify Style reference
        var wpfStyle = wpfButton.Attribute("Style")?.Value;
        var unoStyle = unoButton.Attribute("Style")?.Value;
        
        Assert.IsTrue(wpfStyle?.Contains("CustomButton") == true, "WPF Button should use CustomButton style");
        Assert.IsTrue(unoStyle?.Contains("CustomButton") == true, "Uno Button should use CustomButton style");
    }

    [TestMethod]
    public void MapPage_FollowVehicleButton_EventHandlerConnected()
    {
        // Find the button inside follow_wahana_border
        var unoBorder = _unoXaml.Descendants(_unoNs + "Border")
            .FirstOrDefault(e => e.Attribute(XNamespace.Xml + "Name")?.Value == "follow_wahana_border" ||
                                 e.Attribute("Name")?.Value == "follow_wahana_border");

        var unoButton = unoBorder?.Descendants(_unoNs + "Button").FirstOrDefault();

        Assert.IsNotNull(unoButton, "Uno Button not found");

        // Verify Click event handler is connected
        var clickHandler = unoButton.Attribute("Click")?.Value;
        Assert.IsNotNull(clickHandler, "Button Click event handler should be set");
        Assert.AreEqual("FollowVehicle_Click", clickHandler, 
            "Button should use FollowVehicle_Click event handler");
    }

    [TestMethod]
    public void MapPage_FollowVehicleButton_ImageSourceConverted()
    {
        // Skip if WPF file not available
        if (_wpfXaml == null)
        {
            Assert.Inconclusive("WPF source file not available for comparison");
            return;
        }

        // Find the image inside the button
        var wpfBorder = _wpfXaml.Descendants(_wpfNs + "Border")
            .FirstOrDefault(e => e.Attribute(XNamespace.Xml + "Name")?.Value == "follow_wahana_border" ||
                                 e.Attribute("Name")?.Value == "follow_wahana_border");

        var unoBorder = _unoXaml.Descendants(_unoNs + "Border")
            .FirstOrDefault(e => e.Attribute(XNamespace.Xml + "Name")?.Value == "follow_wahana_border" ||
                                 e.Attribute("Name")?.Value == "follow_wahana_border");

        var wpfImage = wpfBorder?.Descendants(_wpfNs + "Image").FirstOrDefault();
        var unoImage = unoBorder?.Descendants(_unoNs + "Image").FirstOrDefault();

        Assert.IsNotNull(wpfImage, "WPF Image not found");
        Assert.IsNotNull(unoImage, "Uno Image not found");

        // Verify image source is converted to ms-appx:/// format
        var unoSource = unoImage.Attribute("Source")?.Value;
        Assert.IsNotNull(unoSource, "Uno Image Source should be set");
        Assert.IsTrue(unoSource.StartsWith("ms-appx:///"), 
            "Uno Image Source should use ms-appx:/// format");
        Assert.IsTrue(unoSource.Contains("ikon-wahana-pesawat-3.png"), 
            "Uno Image Source should reference the correct icon file");
    }

    [TestMethod]
    public void MapPage_FollowVehicleButton_ContainerConvertedCorrectly()
    {
        // Skip if WPF file not available
        if (_wpfXaml == null)
        {
            Assert.Inconclusive("WPF source file not available for comparison");
            return;
        }

        // WPF uses DockPanel, Uno should use StackPanel
        var wpfContainer = _wpfXaml.Descendants(_wpfNs + "DockPanel")
            .FirstOrDefault(e => e.Descendants(_wpfNs + "Border")
                .Any(b => b.Attribute(XNamespace.Xml + "Name")?.Value == "follow_wahana_border" ||
                         b.Attribute("Name")?.Value == "follow_wahana_border"));

        var unoContainer = _unoXaml.Descendants(_unoNs + "StackPanel")
            .FirstOrDefault(e => e.Descendants(_unoNs + "Border")
                .Any(b => b.Attribute(XNamespace.Xml + "Name")?.Value == "follow_wahana_border" ||
                         b.Attribute("Name")?.Value == "follow_wahana_border"));

        Assert.IsNotNull(wpfContainer, "WPF DockPanel container not found");
        Assert.IsNotNull(unoContainer, "Uno StackPanel container not found");

        // Verify StackPanel has Horizontal orientation
        Assert.AreEqual("Horizontal", unoContainer.Attribute("Orientation")?.Value,
            "StackPanel should have Horizontal orientation");

        // Verify positioning properties match
        Assert.AreEqual(
            wpfContainer.Attribute("VerticalAlignment")?.Value,
            unoContainer.Attribute("VerticalAlignment")?.Value,
            "Container VerticalAlignment should match");

        Assert.AreEqual(
            wpfContainer.Attribute("HorizontalAlignment")?.Value,
            unoContainer.Attribute("HorizontalAlignment")?.Value,
            "Container HorizontalAlignment should match");

        Assert.AreEqual(
            wpfContainer.Attribute("Margin")?.Value,
            unoContainer.Attribute("Margin")?.Value,
            "Container Margin should match");
    }

    [TestMethod]
    public void MapPage_FollowVehicleLabel_ConvertedToTextBlock()
    {
        // Skip if WPF file not available
        if (_wpfXaml == null)
        {
            Assert.Inconclusive("WPF source file not available for comparison");
            return;
        }

        // Find follow_wahana_label in both files
        var wpfLabel = _wpfXaml.Descendants(_wpfNs + "Label")
            .FirstOrDefault(e => e.Attribute(XNamespace.Xml + "Name")?.Value == "follow_wahana_label" ||
                                 e.Attribute("Name")?.Value == "follow_wahana_label");

        var unoTextBlock = _unoXaml.Descendants(_unoNs + "TextBlock")
            .FirstOrDefault(e => e.Attribute(XNamespace.Xml + "Name")?.Value == "follow_wahana_label" ||
                                 e.Attribute("Name")?.Value == "follow_wahana_label");

        Assert.IsNotNull(wpfLabel, "WPF Label not found");
        Assert.IsNotNull(unoTextBlock, "Uno TextBlock not found");

        // Verify content/text
        var wpfContent = wpfLabel.Attribute("Content")?.Value;
        var unoText = unoTextBlock.Attribute("Text")?.Value;
        Assert.AreEqual(wpfContent, unoText, "Label Content should match TextBlock Text");

        // Verify other properties
        Assert.AreEqual(
            wpfLabel.Attribute("Foreground")?.Value,
            unoTextBlock.Attribute("Foreground")?.Value,
            "Foreground should match");

        Assert.AreEqual(
            wpfLabel.Attribute("Visibility")?.Value,
            unoTextBlock.Attribute("Visibility")?.Value,
            "Visibility should match");

        Assert.AreEqual(
            wpfLabel.Attribute("VerticalAlignment")?.Value,
            unoTextBlock.Attribute("VerticalAlignment")?.Value,
            "VerticalAlignment should match");

        Assert.AreEqual(
            wpfLabel.Attribute("HorizontalAlignment")?.Value,
            unoTextBlock.Attribute("HorizontalAlignment")?.Value,
            "HorizontalAlignment should match");
    }

    [TestMethod]
    public void MapPage_AllMapControlEventHandlers_AreConnected()
    {
        // Verify all map-related event handlers are connected
        var eventHandlers = new[]
        {
            ("FollowVehicle_Click", "follow_wahana_border button"),
            ("SendWaypointCommand", "send waypoint command button"),
            ("ResetMarkers_Click", "reset markers button"),
            ("ToggleWPDock", "waypoint dock toggle button"),
            ("Undo_Click", "undo button"),
            ("Redo_Click", "redo button"),
            ("ImportWaypoints_Click", "import button"),
            ("ExportWaypoints_Click", "export button")
        };

        foreach (var (handler, description) in eventHandlers)
        {
            var button = _unoXaml.Descendants(_unoNs + "Button")
                .FirstOrDefault(e => e.Attribute("Click")?.Value == handler);

            Assert.IsNotNull(button, 
                $"Button with Click=\"{handler}\" not found ({description})");
        }
    }
}
