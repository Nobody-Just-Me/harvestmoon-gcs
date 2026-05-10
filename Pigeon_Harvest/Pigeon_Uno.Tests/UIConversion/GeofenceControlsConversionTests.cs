using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Xml.Linq;

namespace Pigeon_Uno.Tests.UIConversion
{
    /// <summary>
    /// Tests to verify the geofence controls panel conversion from WPF to Uno Platform.
    /// Task 5.6: Convert geofence controls panel
    /// Requirements: 1.2, 1.3, 6.2.4
    /// </summary>
    [TestClass]
    public class GeofenceControlsConversionTests : PageConversionTestBase
    {
        private XDocument? _wpfDoc;
        private XDocument? _unoDoc;

        [TestInitialize]
        public void Setup()
        {
            _wpfDoc = XamlParsingUtilities.LoadXamlDocument("Pigeon_WPF_cs/Pigeon_WPF_cs/Custom UserControls/Waypoint.xaml");
            _unoDoc = XamlParsingUtilities.LoadXamlDocument("Pigeon_Uno/Pigeon_Uno/Views/MapPage.xaml");
        }

        [TestMethod]
        public void GeofencePanel_GridStructure_MatchesWpf()
        {
            // Arrange
            var wpfPanel = XamlParsingUtilities.FindElementByName(_wpfDoc!, "geofence_panel");
            var unoPanel = XamlParsingUtilities.FindElementByName(_unoDoc!, "geofence_panel");

            Assert.IsNotNull(wpfPanel, "WPF geofence_panel not found");
            Assert.IsNotNull(unoPanel, "Uno geofence_panel not found");

            // Get the Grid inside the Border
            var wpfGrid = wpfPanel.Descendants().FirstOrDefault(e => e.Name.LocalName == "Grid");
            var unoGrid = unoPanel.Descendants().FirstOrDefault(e => e.Name.LocalName == "Grid");

            Assert.IsNotNull(wpfGrid, "WPF Grid inside geofence_panel not found");
            Assert.IsNotNull(unoGrid, "Uno Grid inside geofence_panel not found");

            // Act
            var wpfRowDefs = wpfGrid.Descendants()
                .Where(e => e.Name.LocalName == "RowDefinition")
                .ToList();
            var unoRowDefs = unoGrid.Descendants()
                .Where(e => e.Name.LocalName == "RowDefinition")
                .ToList();

            // Assert
            Assert.AreEqual(wpfRowDefs.Count, unoRowDefs.Count, 
                "Grid RowDefinitions count should match");
            Assert.AreEqual(5, unoRowDefs.Count, 
                "Geofence panel Grid should have 5 rows");

            // Verify all rows are Auto height
            foreach (var rowDef in unoRowDefs)
            {
                var height = rowDef.Attribute("Height")?.Value ?? "Auto";
                Assert.AreEqual("Auto", height, "All rows should have Height='Auto'");
            }
        }

        [TestMethod]
        public void GeofencePanel_NoDockPanels_OnlyStackPanels()
        {
            // Arrange
            var unoPanel = XamlParsingUtilities.FindElementByName(_unoDoc!, "geofence_panel");
            Assert.IsNotNull(unoPanel, "Uno geofence_panel not found");

            // Act
            var dockPanels = unoPanel.Descendants()
                .Where(e => e.Name.LocalName == "DockPanel")
                .ToList();

            var stackPanels = unoPanel.Descendants()
                .Where(e => e.Name.LocalName == "StackPanel")
                .ToList();

            // Assert
            Assert.AreEqual(0, dockPanels.Count, 
                "Geofence panel should not contain any DockPanels (should be converted to StackPanels)");
            Assert.IsTrue(stackPanels.Count > 0, 
                "Geofence panel should contain StackPanels");
        }

        [TestMethod]
        public void GeofencePanel_NoLabels_OnlyTextBlocks()
        {
            // Arrange
            var unoPanel = XamlParsingUtilities.FindElementByName(_unoDoc!, "geofence_panel");
            Assert.IsNotNull(unoPanel, "Uno geofence_panel not found");

            // Act
            var labels = unoPanel.Descendants()
                .Where(e => e.Name.LocalName == "Label")
                .ToList();

            var textBlocks = unoPanel.Descendants()
                .Where(e => e.Name.LocalName == "TextBlock")
                .ToList();

            // Assert
            Assert.AreEqual(0, labels.Count, 
                "Geofence panel should not contain any Labels (should be converted to TextBlocks)");
            Assert.IsTrue(textBlocks.Count > 0, 
                "Geofence panel should contain TextBlocks");
        }

        [TestMethod]
        public void GeofencePanel_LatitudeTextBox_HasCorrectProperties()
        {
            // Arrange
            var unoTextBox = XamlParsingUtilities.FindElementByName(_unoDoc!, "tb_geofence_lat");
            Assert.IsNotNull(unoTextBox, "tb_geofence_lat not found in Uno XAML");

            // Act & Assert
            Assert.AreEqual("TextBox", unoTextBox.Name.LocalName, "Element should be a TextBox");
            Assert.AreEqual("100", unoTextBox.Attribute("Width")?.Value, "Width should be 100");
            Assert.AreEqual("22", unoTextBox.Attribute("Height")?.Value, "Height should be 22");
            Assert.AreEqual("11", unoTextBox.Attribute("FontSize")?.Value, "FontSize should be 11");
            Assert.AreEqual("", unoTextBox.Attribute("Text")?.Value, "Text should be empty string");
        }

        [TestMethod]
        public void GeofencePanel_LongitudeTextBox_HasCorrectProperties()
        {
            // Arrange
            var unoTextBox = XamlParsingUtilities.FindElementByName(_unoDoc!, "tb_geofence_lon");
            Assert.IsNotNull(unoTextBox, "tb_geofence_lon not found in Uno XAML");

            // Act & Assert
            Assert.AreEqual("TextBox", unoTextBox.Name.LocalName, "Element should be a TextBox");
            Assert.AreEqual("100", unoTextBox.Attribute("Width")?.Value, "Width should be 100");
            Assert.AreEqual("22", unoTextBox.Attribute("Height")?.Value, "Height should be 22");
            Assert.AreEqual("11", unoTextBox.Attribute("FontSize")?.Value, "FontSize should be 11");
            Assert.AreEqual("", unoTextBox.Attribute("Text")?.Value, "Text should be empty string");
        }

        [TestMethod]
        public void GeofencePanel_RadiusTextBox_HasCorrectProperties()
        {
            // Arrange
            var unoTextBox = XamlParsingUtilities.FindElementByName(_unoDoc!, "tb_geofence_radius");
            Assert.IsNotNull(unoTextBox, "tb_geofence_radius not found in Uno XAML");

            // Act & Assert
            Assert.AreEqual("TextBox", unoTextBox.Name.LocalName, "Element should be a TextBox");
            Assert.AreEqual("45", unoTextBox.Attribute("Width")?.Value, "Width should be 45");
            Assert.AreEqual("22", unoTextBox.Attribute("Height")?.Value, "Height should be 22");
            Assert.AreEqual("11", unoTextBox.Attribute("FontSize")?.Value, "FontSize should be 11");
            Assert.AreEqual("500", unoTextBox.Attribute("Text")?.Value, "Default text should be 500");
        }

        [TestMethod]
        public void GeofencePanel_AltitudeTextBox_HasCorrectProperties()
        {
            // Arrange
            var unoTextBox = XamlParsingUtilities.FindElementByName(_unoDoc!, "tb_geofence_altitude");
            Assert.IsNotNull(unoTextBox, "tb_geofence_altitude not found in Uno XAML");

            // Act & Assert
            Assert.AreEqual("TextBox", unoTextBox.Name.LocalName, "Element should be a TextBox");
            Assert.AreEqual("45", unoTextBox.Attribute("Width")?.Value, "Width should be 45");
            Assert.AreEqual("22", unoTextBox.Attribute("Height")?.Value, "Height should be 22");
            Assert.AreEqual("11", unoTextBox.Attribute("FontSize")?.Value, "FontSize should be 11");
            Assert.AreEqual("100", unoTextBox.Attribute("Text")?.Value, "Default text should be 100");
        }

        [TestMethod]
        public void GeofencePanel_TypeRow_LabelsConvertedToTextBlocks()
        {
            // Arrange
            var unoPanel = XamlParsingUtilities.FindElementByName(_unoDoc!, "geofence_panel");
            Assert.IsNotNull(unoPanel, "Uno geofence_panel not found");

            // Find the StackPanel in Grid.Row="1" (Type, Radius, and Altitude Row)
            var typeRow = unoPanel.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "StackPanel" && 
                                    e.Attribute(XName.Get("Row", "http://schemas.microsoft.com/winfx/2006/xaml/presentation"))?.Value == "1");

            Assert.IsNotNull(typeRow, "Type row StackPanel not found");

            // Act
            var labels = typeRow.Descendants()
                .Where(e => e.Name.LocalName == "Label")
                .ToList();

            var textBlocks = typeRow.Descendants()
                .Where(e => e.Name.LocalName == "TextBlock")
                .ToList();

            // Assert
            Assert.AreEqual(0, labels.Count, 
                "Type row should not contain any Labels");
            Assert.IsTrue(textBlocks.Count >= 4, 
                "Type row should contain at least 4 TextBlocks (Type:, Radius:, m, Max Alt:, m)");

            // Verify specific TextBlocks
            var typeLabel = textBlocks.FirstOrDefault(tb => tb.Attribute("Text")?.Value == "Type:");
            Assert.IsNotNull(typeLabel, "Type: TextBlock should exist");

            var radiusLabel = textBlocks.FirstOrDefault(tb => tb.Attribute("Text")?.Value == "Radius:");
            Assert.IsNotNull(radiusLabel, "Radius: TextBlock should exist");

            var maxAltLabel = textBlocks.FirstOrDefault(tb => tb.Attribute("Text")?.Value == "Max Alt:");
            Assert.IsNotNull(maxAltLabel, "Max Alt: TextBlock should exist");
        }

        [TestMethod]
        public void GeofencePanel_CoordinateRow_LabelsConvertedToTextBlocks()
        {
            // Arrange
            var unoPanel = XamlParsingUtilities.FindElementByName(_unoDoc!, "geofence_panel");
            Assert.IsNotNull(unoPanel, "Uno geofence_panel not found");

            // Find the Border in Grid.Row="2" (Coordinate Input)
            var coordBorder = unoPanel.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "Border" && 
                                    e.Attribute(XName.Get("Row", "http://schemas.microsoft.com/winfx/2006/xaml/presentation"))?.Value == "2");

            Assert.IsNotNull(coordBorder, "Coordinate Border not found");

            // Act
            var labels = coordBorder.Descendants()
                .Where(e => e.Name.LocalName == "Label")
                .ToList();

            var textBlocks = coordBorder.Descendants()
                .Where(e => e.Name.LocalName == "TextBlock")
                .ToList();

            // Assert
            Assert.AreEqual(0, labels.Count, 
                "Coordinate row should not contain any Labels");
            Assert.IsTrue(textBlocks.Count >= 2, 
                "Coordinate row should contain at least 2 TextBlocks (Lat:, Lon:)");

            // Verify specific TextBlocks
            var latLabel = textBlocks.FirstOrDefault(tb => tb.Attribute("Text")?.Value == "Lat:");
            Assert.IsNotNull(latLabel, "Lat: TextBlock should exist");

            var lonLabel = textBlocks.FirstOrDefault(tb => tb.Attribute("Text")?.Value == "Lon:");
            Assert.IsNotNull(lonLabel, "Lon: TextBlock should exist");
        }

        [TestMethod]
        public void GeofencePanel_AllTextBoxes_HaveToolTips()
        {
            // Arrange
            var unoPanel = XamlParsingUtilities.FindElementByName(_unoDoc!, "geofence_panel");
            Assert.IsNotNull(unoPanel, "Uno geofence_panel not found");

            // Act
            var latTextBox = XamlParsingUtilities.FindElementByName(_unoDoc!, "tb_geofence_lat");
            var lonTextBox = XamlParsingUtilities.FindElementByName(_unoDoc!, "tb_geofence_lon");
            var radiusTextBox = XamlParsingUtilities.FindElementByName(_unoDoc!, "tb_geofence_radius");
            var altitudeTextBox = XamlParsingUtilities.FindElementByName(_unoDoc!, "tb_geofence_altitude");

            // Assert - Check for ToolTipService.ToolTip attribute (Uno style)
            var latToolTip = latTextBox?.Attribute(XName.Get("ToolTip", "http://schemas.microsoft.com/winfx/2006/xaml/presentation"))?.Value ??
                            latTextBox?.Attribute(XName.Get("ToolTip", "http://schemas.microsoft.com/winfx/2006/xaml/presentation/toolkit"))?.Value;
            var lonToolTip = lonTextBox?.Attribute(XName.Get("ToolTip", "http://schemas.microsoft.com/winfx/2006/xaml/presentation"))?.Value ??
                            lonTextBox?.Attribute(XName.Get("ToolTip", "http://schemas.microsoft.com/winfx/2006/xaml/presentation/toolkit"))?.Value;
            var radiusToolTip = radiusTextBox?.Attribute(XName.Get("ToolTip", "http://schemas.microsoft.com/winfx/2006/xaml/presentation"))?.Value ??
                               radiusTextBox?.Attribute(XName.Get("ToolTip", "http://schemas.microsoft.com/winfx/2006/xaml/presentation/toolkit"))?.Value;
            var altitudeToolTip = altitudeTextBox?.Attribute(XName.Get("ToolTip", "http://schemas.microsoft.com/winfx/2006/xaml/presentation"))?.Value ??
                                 altitudeTextBox?.Attribute(XName.Get("ToolTip", "http://schemas.microsoft.com/winfx/2006/xaml/presentation/toolkit"))?.Value;

            // In Uno, ToolTip is set using ToolTipService.ToolTip
            // Check for the attribute without namespace or with ToolTipService namespace
            if (latToolTip == null)
            {
                latToolTip = latTextBox?.Attributes()
                    .FirstOrDefault(a => a.Name.LocalName == "ToolTip")?.Value;
            }
            if (lonToolTip == null)
            {
                lonToolTip = lonTextBox?.Attributes()
                    .FirstOrDefault(a => a.Name.LocalName == "ToolTip")?.Value;
            }
            if (radiusToolTip == null)
            {
                radiusToolTip = radiusTextBox?.Attributes()
                    .FirstOrDefault(a => a.Name.LocalName == "ToolTip")?.Value;
            }
            if (altitudeToolTip == null)
            {
                altitudeToolTip = altitudeTextBox?.Attributes()
                    .FirstOrDefault(a => a.Name.LocalName == "ToolTip")?.Value;
            }

            Assert.IsNotNull(latToolTip, "Latitude TextBox should have a ToolTip");
            Assert.IsNotNull(lonToolTip, "Longitude TextBox should have a ToolTip");
            Assert.IsNotNull(radiusToolTip, "Radius TextBox should have a ToolTip");
            Assert.IsNotNull(altitudeToolTip, "Altitude TextBox should have a ToolTip");
        }

        [TestMethod]
        public void GeofencePanel_BorderProperties_MatchWpf()
        {
            // Arrange
            var wpfPanel = XamlParsingUtilities.FindElementByName(_wpfDoc!, "geofence_panel");
            var unoPanel = XamlParsingUtilities.FindElementByName(_unoDoc!, "geofence_panel");

            Assert.IsNotNull(wpfPanel, "WPF geofence_panel not found");
            Assert.IsNotNull(unoPanel, "Uno geofence_panel not found");

            // Act & Assert - Compare Border properties
            Assert.AreEqual(
                wpfPanel.Attribute("Background")?.Value,
                unoPanel.Attribute("Background")?.Value,
                "Background color should match");

            Assert.AreEqual(
                wpfPanel.Attribute("CornerRadius")?.Value,
                unoPanel.Attribute("CornerRadius")?.Value,
                "CornerRadius should match");

            Assert.AreEqual(
                wpfPanel.Attribute("Padding")?.Value,
                unoPanel.Attribute("Padding")?.Value,
                "Padding should match");

            Assert.AreEqual(
                wpfPanel.Attribute("HorizontalAlignment")?.Value,
                unoPanel.Attribute("HorizontalAlignment")?.Value,
                "HorizontalAlignment should match");

            Assert.AreEqual(
                wpfPanel.Attribute("VerticalAlignment")?.Value,
                unoPanel.Attribute("VerticalAlignment")?.Value,
                "VerticalAlignment should match");
        }

        [TestMethod]
        public void GeofencePanel_AllButtons_HaveCorrectEventHandlers()
        {
            // Arrange
            var unoPanel = XamlParsingUtilities.FindElementByName(_unoDoc!, "geofence_panel");
            Assert.IsNotNull(unoPanel, "Uno geofence_panel not found");

            // Act
            var setCenterBtn = XamlParsingUtilities.FindElementByName(_unoDoc!, "btn_set_center");
            var geofenceModeBtn = XamlParsingUtilities.FindElementByName(_unoDoc!, "btn_geofence_mode");
            var completeBtn = XamlParsingUtilities.FindElementByName(_unoDoc!, "btn_complete_geofence");
            var sendBtn = XamlParsingUtilities.FindElementByName(_unoDoc!, "btn_send_geofence");
            var clearBtn = XamlParsingUtilities.FindElementByName(_unoDoc!, "btn_clear_geofence");

            // Assert
            Assert.IsNotNull(setCenterBtn, "btn_set_center should exist");
            Assert.AreEqual("SetGeofenceCenter_Click", 
                setCenterBtn.Attribute("Click")?.Value, 
                "btn_set_center should have SetGeofenceCenter_Click handler");

            Assert.IsNotNull(geofenceModeBtn, "btn_geofence_mode should exist");
            Assert.AreEqual("ToggleGeofenceMode_Click", 
                geofenceModeBtn.Attribute("Click")?.Value, 
                "btn_geofence_mode should have ToggleGeofenceMode_Click handler");

            Assert.IsNotNull(completeBtn, "btn_complete_geofence should exist");
            Assert.AreEqual("CompleteGeofence_Click", 
                completeBtn.Attribute("Click")?.Value, 
                "btn_complete_geofence should have CompleteGeofence_Click handler");

            Assert.IsNotNull(sendBtn, "btn_send_geofence should exist");
            Assert.AreEqual("SendGeofence_Click", 
                sendBtn.Attribute("Click")?.Value, 
                "btn_send_geofence should have SendGeofence_Click handler");

            Assert.IsNotNull(clearBtn, "btn_clear_geofence should exist");
            Assert.AreEqual("ClearGeofence_Click", 
                clearBtn.Attribute("Click")?.Value, 
                "btn_clear_geofence should have ClearGeofence_Click handler");
        }

        [TestMethod]
        public void GeofencePanel_ComboBox_HasCorrectItems()
        {
            // Arrange
            var unoComboBox = XamlParsingUtilities.FindElementByName(_unoDoc!, "cb_geofence_type");
            Assert.IsNotNull(unoComboBox, "cb_geofence_type not found in Uno XAML");

            // Act
            var items = unoComboBox.Descendants()
                .Where(e => e.Name.LocalName == "ComboBoxItem")
                .ToList();

            // Assert
            Assert.AreEqual(2, items.Count, "ComboBox should have 2 items");
            Assert.AreEqual("Circular", items[0].Attribute("Content")?.Value, 
                "First item should be 'Circular'");
            Assert.AreEqual("Polygon", items[1].Attribute("Content")?.Value, 
                "Second item should be 'Polygon'");
            Assert.AreEqual("0", unoComboBox.Attribute("SelectedIndex")?.Value, 
                "SelectedIndex should be 0");
        }
    }
}
