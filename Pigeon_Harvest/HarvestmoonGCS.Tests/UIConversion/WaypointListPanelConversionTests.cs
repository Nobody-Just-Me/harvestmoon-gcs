using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Xml.Linq;
using System.Linq;

namespace HarvestmoonGCS.Tests.UIConversion
{
    /// <summary>
    /// Tests for Task 5.4: Convert waypoint list panel from WPF to Uno Platform
    /// Validates: Requirements 1.3, 1.4, 6.2.2
    /// </summary>
    [TestClass]
    public class WaypointListPanelConversionTests : PageConversionTestBase
    {
        private XDocument _wpfXaml;
        private XDocument _unoXaml;

        [TestInitialize]
        public void Setup()
        {
            _wpfXaml = LoadXaml("Pigeon_WPF_cs/Pigeon_WPF_cs/Custom UserControls/Waypoint.xaml");
            _unoXaml = LoadXaml("HarvestmoonGCS/HarvestmoonGCS/Views/MapPage.xaml");
        }

        [TestMethod]
        public void WaypointListPanel_ScrollViewerProperties_Match()
        {
            // Find the ScrollViewer in the waypoint dock panel
            var wpfScrollViewer = _wpfXaml.Descendants()
                .Where(e => e.Name.LocalName == "ScrollViewer" &&
                           e.Ancestors().Any(a => a.Attribute(XmlNamespace + "Name")?.Value == "wp_dock"))
                .FirstOrDefault();

            var unoScrollViewer = _unoXaml.Descendants()
                .Where(e => e.Name.LocalName == "ScrollViewer" &&
                           e.Ancestors().Any(a => a.Attribute(XmlNamespace + "Name")?.Value == "wp_dock"))
                .FirstOrDefault();

            Assert.IsNotNull(wpfScrollViewer, "WPF ScrollViewer not found in wp_dock");
            Assert.IsNotNull(unoScrollViewer, "Uno ScrollViewer not found in wp_dock");

            // Verify VerticalScrollBarVisibility
            var wpfVerticalScroll = wpfScrollViewer.Attribute("VerticalScrollBarVisibility")?.Value;
            var unoVerticalScroll = unoScrollViewer.Attribute("VerticalScrollBarVisibility")?.Value;
            Assert.AreEqual(wpfVerticalScroll, unoVerticalScroll, 
                "ScrollViewer VerticalScrollBarVisibility should match");

            // Verify Margin
            var wpfMargin = wpfScrollViewer.Attribute("Margin")?.Value;
            var unoMargin = unoScrollViewer.Attribute("Margin")?.Value;
            Assert.AreEqual(wpfMargin, unoMargin, 
                "ScrollViewer Margin should match");
        }

        [TestMethod]
        public void WaypointListPanel_ItemsControlExists()
        {
            // The ItemsControl is the StackPanel named wp_dock_stack
            var wpfItemsControl = _wpfXaml.Descendants()
                .FirstOrDefault(e => e.Attribute(XmlNamespace + "Name")?.Value == "wp_dock_stack");

            var unoItemsControl = _unoXaml.Descendants()
                .FirstOrDefault(e => e.Attribute(XmlNamespace + "Name")?.Value == "wp_dock_stack");

            Assert.IsNotNull(wpfItemsControl, "WPF wp_dock_stack not found");
            Assert.IsNotNull(unoItemsControl, "Uno wp_dock_stack not found");

            // Verify it's a StackPanel
            Assert.AreEqual("StackPanel", wpfItemsControl.Name.LocalName);
            Assert.AreEqual("StackPanel", unoItemsControl.Name.LocalName);
        }

        [TestMethod]
        public void WaypointListPanel_BorderProperties_Match()
        {
            // Find the Border that wraps the ScrollViewer
            var wpfBorder = _wpfXaml.Descendants()
                .Where(e => e.Name.LocalName == "Border" &&
                           e.Descendants().Any(d => d.Attribute(XmlNamespace + "Name")?.Value == "wp_dock_stack"))
                .FirstOrDefault();

            var unoBorder = _unoXaml.Descendants()
                .Where(e => e.Name.LocalName == "Border" &&
                           e.Descendants().Any(d => d.Attribute(XmlNamespace + "Name")?.Value == "wp_dock_stack"))
                .FirstOrDefault();

            Assert.IsNotNull(wpfBorder, "WPF Border not found");
            Assert.IsNotNull(unoBorder, "Uno Border not found");

            // Verify BorderBrush
            var wpfBorderBrush = wpfBorder.Attribute("BorderBrush")?.Value;
            var unoBorderBrush = unoBorder.Attribute("BorderBrush")?.Value;
            Assert.AreEqual(wpfBorderBrush, unoBorderBrush, 
                "Border BorderBrush should match");

            // Verify BorderThickness
            var wpfBorderThickness = wpfBorder.Attribute("BorderThickness")?.Value;
            var unoBorderThickness = unoBorder.Attribute("BorderThickness")?.Value;
            Assert.AreEqual(wpfBorderThickness, unoBorderThickness, 
                "Border BorderThickness should match");

            // Verify Background
            var wpfBackground = wpfBorder.Attribute("Background")?.Value;
            var unoBackground = unoBorder.Attribute("Background")?.Value;
            Assert.AreEqual(wpfBackground, unoBackground, 
                "Border Background should match");

            // Verify CornerRadius (normalize spacing)
            var wpfCornerRadius = wpfBorder.Attribute("CornerRadius")?.Value?.Replace(" ", "");
            var unoCornerRadius = unoBorder.Attribute("CornerRadius")?.Value?.Replace(" ", "");
            Assert.AreEqual(wpfCornerRadius, unoCornerRadius, 
                "Border CornerRadius should match");
        }

        [TestMethod]
        public void WaypointListPanel_AddRemoveButtons_Exist()
        {
            // Find the button grid in wp_dock
            var wpfButtonGrid = _wpfXaml.Descendants()
                .Where(e => e.Name.LocalName == "Grid" &&
                           e.Parent?.Attribute(XmlNamespace + "Name")?.Value == "wp_dock")
                .FirstOrDefault();

            var unoButtonGrid = _unoXaml.Descendants()
                .Where(e => e.Name.LocalName == "Grid" &&
                           e.Parent?.Attribute(XmlNamespace + "Name")?.Value == "wp_dock")
                .FirstOrDefault();

            Assert.IsNotNull(wpfButtonGrid, "WPF button Grid not found");
            Assert.IsNotNull(unoButtonGrid, "Uno button Grid not found");

            // Verify button names exist
            string[] buttonNames = { "wp_dock_btn", "reset_mark", "Undo", "Redo", "Import", "Export" };
            
            foreach (var buttonName in buttonNames)
            {
                var wpfButton = wpfButtonGrid.Descendants()
                    .FirstOrDefault(e => e.Attribute(XmlNamespace + "Name")?.Value == buttonName);
                var unoButton = unoButtonGrid.Descendants()
                    .FirstOrDefault(e => e.Attribute(XmlNamespace + "Name")?.Value == buttonName);

                Assert.IsNotNull(wpfButton, $"WPF button '{buttonName}' not found");
                Assert.IsNotNull(unoButton, $"Uno button '{buttonName}' not found");
            }
        }

        [TestMethod]
        public void WaypointListPanel_ButtonProperties_Match()
        {
            // Test specific button properties
            var wpfResetButton = _wpfXaml.Descendants()
                .FirstOrDefault(e => e.Attribute(XmlNamespace + "Name")?.Value == "reset_mark");
            var unoResetButton = _unoXaml.Descendants()
                .FirstOrDefault(e => e.Attribute(XmlNamespace + "Name")?.Value == "reset_mark");

            Assert.IsNotNull(wpfResetButton, "WPF reset_mark button not found");
            Assert.IsNotNull(unoResetButton, "Uno reset_mark button not found");

            // Verify Content
            var wpfContent = wpfResetButton.Attribute("Content")?.Value;
            var unoContent = unoResetButton.Attribute("Content")?.Value;
            Assert.AreEqual(wpfContent, unoContent, "reset_mark Content should match");

            // Verify Background
            var wpfBackground = wpfResetButton.Attribute("Background")?.Value;
            var unoBackground = unoResetButton.Attribute("Background")?.Value;
            Assert.AreEqual(wpfBackground, unoBackground, "reset_mark Background should match");

            // Verify Foreground
            var wpfForeground = wpfResetButton.Attribute("Foreground")?.Value;
            var unoForeground = unoResetButton.Attribute("Foreground")?.Value;
            Assert.AreEqual(wpfForeground, unoForeground, "reset_mark Foreground should match");

            // Verify BorderBrush
            var wpfBorderBrush = wpfResetButton.Attribute("BorderBrush")?.Value;
            var unoBorderBrush = unoResetButton.Attribute("BorderBrush")?.Value;
            Assert.AreEqual(wpfBorderBrush, unoBorderBrush, "reset_mark BorderBrush should match");

            // Verify Height
            var wpfHeight = wpfResetButton.Attribute("Height")?.Value;
            var unoHeight = unoResetButton.Attribute("Height")?.Value;
            Assert.AreEqual(wpfHeight, unoHeight, "reset_mark Height should match");

            // Verify Width
            var wpfWidth = wpfResetButton.Attribute("Width")?.Value;
            var unoWidth = unoResetButton.Attribute("Width")?.Value;
            Assert.AreEqual(wpfWidth, unoWidth, "reset_mark Width should match");
        }

        [TestMethod]
        public void WaypointListPanel_EventHandlers_Connected()
        {
            // Verify event handlers are present
            var unoResetButton = _unoXaml.Descendants()
                .FirstOrDefault(e => e.Attribute(XmlNamespace + "Name")?.Value == "reset_mark");
            var unoUndoButton = _unoXaml.Descendants()
                .FirstOrDefault(e => e.Attribute(XmlNamespace + "Name")?.Value == "Undo");
            var unoRedoButton = _unoXaml.Descendants()
                .FirstOrDefault(e => e.Attribute(XmlNamespace + "Name")?.Value == "Redo");
            var unoImportButton = _unoXaml.Descendants()
                .FirstOrDefault(e => e.Attribute(XmlNamespace + "Name")?.Value == "Import");
            var unoExportButton = _unoXaml.Descendants()
                .FirstOrDefault(e => e.Attribute(XmlNamespace + "Name")?.Value == "Export");

            Assert.IsNotNull(unoResetButton?.Attribute("Click")?.Value, "reset_mark Click handler missing");
            Assert.IsNotNull(unoUndoButton?.Attribute("Click")?.Value, "Undo Click handler missing");
            Assert.IsNotNull(unoRedoButton?.Attribute("Click")?.Value, "Redo Click handler missing");
            Assert.IsNotNull(unoImportButton?.Attribute("Click")?.Value, "Import Click handler missing");
            Assert.IsNotNull(unoExportButton?.Attribute("Click")?.Value, "Export Click handler missing");

            // Verify handler names match
            var wpfResetButton = _wpfXaml.Descendants()
                .FirstOrDefault(e => e.Attribute(XmlNamespace + "Name")?.Value == "reset_mark");
            Assert.AreEqual(wpfResetButton?.Attribute("Click")?.Value, 
                           unoResetButton?.Attribute("Click")?.Value,
                           "reset_mark Click handler name should match");
        }

        [TestMethod]
        public void WaypointListPanel_ContainerStructure_IsCorrect()
        {
            // WPF uses DockPanel, Uno should use StackPanel (since DockPanel is not well supported)
            var wpfContainer = _wpfXaml.Descendants()
                .FirstOrDefault(e => e.Attribute(XmlNamespace + "Name")?.Value == "wp_dock");
            var unoContainer = _unoXaml.Descendants()
                .FirstOrDefault(e => e.Attribute(XmlNamespace + "Name")?.Value == "wp_dock");

            Assert.IsNotNull(wpfContainer, "WPF wp_dock not found");
            Assert.IsNotNull(unoContainer, "Uno wp_dock not found");

            // WPF uses DockPanel
            Assert.AreEqual("DockPanel", wpfContainer.Name.LocalName, "WPF should use DockPanel");
            
            // Uno should use StackPanel (acceptable conversion)
            Assert.AreEqual("StackPanel", unoContainer.Name.LocalName, 
                "Uno should use StackPanel as DockPanel alternative");

            // Verify positioning properties match
            Assert.AreEqual(wpfContainer.Attribute("Grid.Row")?.Value, 
                           unoContainer.Attribute("Grid.Row")?.Value,
                           "Grid.Row should match");
            Assert.AreEqual(wpfContainer.Attribute("Grid.ColumnSpan")?.Value, 
                           unoContainer.Attribute("Grid.ColumnSpan")?.Value,
                           "Grid.ColumnSpan should match");
            Assert.AreEqual(wpfContainer.Attribute("VerticalAlignment")?.Value, 
                           unoContainer.Attribute("VerticalAlignment")?.Value,
                           "VerticalAlignment should match");
            Assert.AreEqual(wpfContainer.Attribute("HorizontalAlignment")?.Value, 
                           unoContainer.Attribute("HorizontalAlignment")?.Value,
                           "HorizontalAlignment should match");
        }

        [TestMethod]
        public void WaypointListPanel_FontFamily_References_Valid()
        {
            // Check that FontFamily references are valid in Uno
            var unoButtons = _unoXaml.Descendants()
                .Where(e => e.Name.LocalName == "Button" &&
                           e.Ancestors().Any(a => a.Attribute(XmlNamespace + "Name")?.Value == "wp_dock"))
                .ToList();

            foreach (var button in unoButtons)
            {
                var fontFamily = button.Attribute("FontFamily")?.Value;
                if (!string.IsNullOrEmpty(fontFamily))
                {
                    // WPF uses "Raleway SemiBold", Uno should use StaticResource reference
                    Assert.IsFalse(fontFamily.Contains("/PIGEON GCS;component/"), 
                        $"Button {button.Attribute(XmlNamespace + "Name")?.Value} should not use WPF-style font path");
                    
                    // Should use StaticResource or simple font name
                    Assert.IsTrue(fontFamily.StartsWith("{StaticResource") || !fontFamily.Contains("/"),
                        $"Button {button.Attribute(XmlNamespace + "Name")?.Value} should use StaticResource or simple font name");
                }
            }
        }
    }
}
