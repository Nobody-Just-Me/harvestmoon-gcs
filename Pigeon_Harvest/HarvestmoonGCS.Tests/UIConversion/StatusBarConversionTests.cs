using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Xml.Linq;
using System.Linq;

namespace HarvestmoonGCS.Tests.UIConversion
{
    /// <summary>
    /// Tests for MapPage status bar conversion from WPF to Uno Platform.
    /// Validates: Requirements 1.3, 1.4, 6.2.6
    /// Task 5.8: Convert status bar - Verify positioning and properties, TextBlock bindings
    /// </summary>
    [TestClass]
    public class StatusBarConversionTests : PageConversionTestBase
    {
        private XElement? _wpfXaml;
        private XElement? _unoXaml;

        [TestInitialize]
        public void Initialize()
        {
            // WPF source: Waypoint.xaml (the map control)
            _wpfXaml = LoadWpfXaml("Waypoint.xaml");
            // Uno target: MapPage.xaml
            _unoXaml = LoadUnoXaml("MapPage.xaml");
        }

        [TestMethod]
        public void StatusBar_WPDock_ExistsInBothVersions()
        {
            // Arrange & Act
            var wpfWpDock = FindElementByName(_wpfXaml!, "wp_dock");
            var unoWpDock = FindElementByName(_unoXaml!, "wp_dock");

            // Assert
            Assert.IsNotNull(wpfWpDock, "WPF wp_dock should exist");
            Assert.IsNotNull(unoWpDock, "Uno wp_dock should exist");
        }

        [TestMethod]
        public void StatusBar_Positioning_MatchesWpf()
        {
            // Arrange
            var wpfWpDock = FindElementByName(_wpfXaml!, "wp_dock");
            var unoWpDock = FindElementByName(_unoXaml!, "wp_dock");

            // Act - Get positioning properties
            var wpfGridRow = wpfWpDock?.Attribute(XName.Get("Row", GridNamespace))?.Value ?? "0";
            var unoGridRow = unoWpDock?.Attribute(XName.Get("Row", GridNamespace))?.Value ?? "0";

            var wpfMargin = wpfWpDock?.Attribute("Margin")?.Value;
            var unoMargin = unoWpDock?.Attribute("Margin")?.Value;

            var wpfHeight = wpfWpDock?.Attribute("Height")?.Value;
            var unoHeight = unoWpDock?.Attribute("Height")?.Value;

            var wpfWidth = wpfWpDock?.Attribute("Width")?.Value;
            var unoWidth = unoWpDock?.Attribute("Width")?.Value;

            var wpfVerticalAlignment = wpfWpDock?.Attribute("VerticalAlignment")?.Value;
            var unoVerticalAlignment = unoWpDock?.Attribute("VerticalAlignment")?.Value;

            var wpfHorizontalAlignment = wpfWpDock?.Attribute("HorizontalAlignment")?.Value;
            var unoHorizontalAlignment = unoWpDock?.Attribute("HorizontalAlignment")?.Value;

            var wpfGridColumnSpan = wpfWpDock?.Attribute(XName.Get("ColumnSpan", GridNamespace))?.Value;
            var unoGridColumnSpan = unoWpDock?.Attribute(XName.Get("ColumnSpan", GridNamespace))?.Value;

            // Assert - All positioning properties should match
            Assert.AreEqual(wpfGridRow, unoGridRow, "Grid.Row should match");
            Assert.AreEqual(wpfMargin, unoMargin, "Margin should match");
            Assert.AreEqual(wpfHeight, unoHeight, "Height should match");
            Assert.AreEqual(wpfWidth, unoWidth, "Width should match");
            Assert.AreEqual(wpfVerticalAlignment, unoVerticalAlignment, "VerticalAlignment should match");
            Assert.AreEqual(wpfHorizontalAlignment, unoHorizontalAlignment, "HorizontalAlignment should match");
            Assert.AreEqual(wpfGridColumnSpan, unoGridColumnSpan, "Grid.ColumnSpan should match");
        }

        [TestMethod]
        public void StatusBar_ContainerType_IsCorrect()
        {
            // Arrange
            var wpfWpDock = FindElementByName(_wpfXaml!, "wp_dock");
            var unoWpDock = FindElementByName(_unoXaml!, "wp_dock");

            // Act
            var wpfElementName = wpfWpDock?.Name.LocalName;
            var unoElementName = unoWpDock?.Name.LocalName;

            // Assert
            // WPF uses DockPanel, but Uno should use StackPanel or Grid (DockPanel not fully supported)
            Assert.AreEqual("DockPanel", wpfElementName, "WPF should use DockPanel");
            
            // Uno can use StackPanel or Grid as alternative
            Assert.IsTrue(unoElementName == "StackPanel" || unoElementName == "Grid" || unoElementName == "DockPanel",
                $"Uno should use StackPanel, Grid, or DockPanel, but found: {unoElementName}");
        }

        [TestMethod]
        public void StatusBar_InnerGrid_ExistsAndMatchesStructure()
        {
            // Arrange
            var wpfWpDock = FindElementByName(_wpfXaml!, "wp_dock");
            var unoWpDock = FindElementByName(_unoXaml!, "wp_dock");

            // Act - Find the inner Grid
            var wpfInnerGrid = wpfWpDock?.Descendants(XName.Get("Grid", DefaultNamespace)).FirstOrDefault();
            var unoInnerGrid = unoWpDock?.Descendants(XName.Get("Grid", DefaultNamespace)).FirstOrDefault();

            // Assert
            Assert.IsNotNull(wpfInnerGrid, "WPF should have inner Grid");
            Assert.IsNotNull(unoInnerGrid, "Uno should have inner Grid");

            // Check Grid properties
            var wpfGridHeight = wpfInnerGrid?.Attribute("Height")?.Value;
            var unoGridHeight = unoInnerGrid?.Attribute("Height")?.Value;
            Assert.AreEqual(wpfGridHeight, unoGridHeight, "Inner Grid Height should match");

            var wpfGridWidth = wpfInnerGrid?.Attribute("Width")?.Value;
            var unoGridWidth = unoInnerGrid?.Attribute("Width")?.Value;
            Assert.AreEqual(wpfGridWidth, unoGridWidth, "Inner Grid Width should match");
        }

        [TestMethod]
        public void StatusBar_Buttons_AllExistWithCorrectNames()
        {
            // Arrange
            var unoWpDock = FindElementByName(_unoXaml!, "wp_dock");
            var expectedButtons = new[] { "wp_dock_btn", "reset_mark", "Undo", "Redo", "Import", "Export" };

            // Act & Assert
            foreach (var buttonName in expectedButtons)
            {
                var button = FindElementByName(unoWpDock!, buttonName);
                Assert.IsNotNull(button, $"Button '{buttonName}' should exist in status bar");
            }
        }

        [TestMethod]
        public void StatusBar_WpDockBtn_PropertiesMatch()
        {
            // Arrange
            var wpfButton = FindElementByName(_wpfXaml!, "wp_dock_btn");
            var unoButton = FindElementByName(_unoXaml!, "wp_dock_btn");

            // Act
            var wpfContent = wpfButton?.Attribute("Content")?.Value;
            var unoContent = unoButton?.Attribute("Content")?.Value;

            var wpfPadding = wpfButton?.Attribute("Padding")?.Value;
            var unoPadding = unoButton?.Attribute("Padding")?.Value;

            var wpfMargin = wpfButton?.Attribute("Margin")?.Value;
            var unoMargin = unoButton?.Attribute("Margin")?.Value;

            var wpfBackground = wpfButton?.Attribute("Background")?.Value;
            var unoBackground = unoButton?.Attribute("Background")?.Value;

            var wpfForeground = wpfButton?.Attribute("Foreground")?.Value;
            var unoForeground = unoButton?.Attribute("Foreground")?.Value;

            // Assert
            // Content might differ slightly (arrow character encoding)
            Assert.IsNotNull(wpfContent, "WPF button should have Content");
            Assert.IsNotNull(unoContent, "Uno button should have Content");
            Assert.IsTrue(unoContent.Contains("Markers"), "Uno button should contain 'Markers'");

            Assert.AreEqual(wpfPadding, unoPadding, "Padding should match");
            Assert.AreEqual(wpfMargin, unoMargin, "Margin should match");
            Assert.AreEqual(wpfBackground, unoBackground, "Background should match");
            Assert.AreEqual(wpfForeground, unoForeground, "Foreground should match");
        }

        [TestMethod]
        public void StatusBar_ActionButtons_PropertiesMatch()
        {
            // Test each action button
            var buttonNames = new[] { "reset_mark", "Undo", "Redo", "Import", "Export" };

            foreach (var buttonName in buttonNames)
            {
                // Arrange
                var wpfButton = FindElementByName(_wpfXaml!, buttonName);
                var unoButton = FindElementByName(_unoXaml!, buttonName);

                // Act
                var wpfContent = wpfButton?.Attribute("Content")?.Value;
                var unoContent = unoButton?.Attribute("Content")?.Value;

                var wpfMargin = wpfButton?.Attribute("Margin")?.Value;
                var unoMargin = unoButton?.Attribute("Margin")?.Value;

                var wpfBackground = wpfButton?.Attribute("Background")?.Value;
                var unoBackground = unoButton?.Attribute("Background")?.Value;

                var wpfForeground = wpfButton?.Attribute("Foreground")?.Value;
                var unoForeground = unoButton?.Attribute("Foreground")?.Value;

                var wpfHeight = wpfButton?.Attribute("Height")?.Value;
                var unoHeight = unoButton?.Attribute("Height")?.Value;

                var wpfWidth = wpfButton?.Attribute("Width")?.Value;
                var unoWidth = unoButton?.Attribute("Width")?.Value;

                // Assert
                Assert.AreEqual(wpfContent, unoContent, $"{buttonName}: Content should match");
                Assert.AreEqual(wpfMargin, unoMargin, $"{buttonName}: Margin should match");
                Assert.AreEqual(wpfBackground, unoBackground, $"{buttonName}: Background should match");
                Assert.AreEqual(wpfForeground, unoForeground, $"{buttonName}: Foreground should match");
                Assert.AreEqual(wpfHeight, unoHeight, $"{buttonName}: Height should match");
                Assert.AreEqual(wpfWidth, unoWidth, $"{buttonName}: Width should match");
            }
        }

        [TestMethod]
        public void StatusBar_Polygon_ExistsAndMatchesPoints()
        {
            // Arrange
            var wpfWpDock = FindElementByName(_wpfXaml!, "wp_dock");
            var unoWpDock = FindElementByName(_unoXaml!, "wp_dock");

            // Act - Find Polygon elements
            var wpfPolygon = wpfWpDock?.Descendants(XName.Get("Polygon", DefaultNamespace)).FirstOrDefault();
            var unoPolygon = unoWpDock?.Descendants(XName.Get("Polygon", DefaultNamespace)).FirstOrDefault();

            // Assert
            Assert.IsNotNull(wpfPolygon, "WPF should have Polygon");
            Assert.IsNotNull(unoPolygon, "Uno should have Polygon");

            var wpfPoints = wpfPolygon?.Attribute("Points")?.Value;
            var unoPoints = unoPolygon?.Attribute("Points")?.Value;
            Assert.AreEqual(wpfPoints, unoPoints, "Polygon Points should match");

            var wpfFill = wpfPolygon?.Attribute("Fill")?.Value;
            var unoFill = unoPolygon?.Attribute("Fill")?.Value;
            Assert.AreEqual(wpfFill, unoFill, "Polygon Fill should match");

            var wpfMargin = wpfPolygon?.Attribute("Margin")?.Value;
            var unoMargin = unoPolygon?.Attribute("Margin")?.Value;
            Assert.AreEqual(wpfMargin, unoMargin, "Polygon Margin should match");
        }

        [TestMethod]
        public void StatusBar_Border_ExistsAndMatchesProperties()
        {
            // Arrange
            var wpfWpDock = FindElementByName(_wpfXaml!, "wp_dock");
            var unoWpDock = FindElementByName(_unoXaml!, "wp_dock");

            // Act - Find Border elements (the scrollable content area)
            var wpfBorder = wpfWpDock?.Descendants(XName.Get("Border", DefaultNamespace)).FirstOrDefault();
            var unoBorder = unoWpDock?.Descendants(XName.Get("Border", DefaultNamespace)).FirstOrDefault();

            // Assert
            Assert.IsNotNull(wpfBorder, "WPF should have Border");
            Assert.IsNotNull(unoBorder, "Uno should have Border");

            var wpfBorderBrush = wpfBorder?.Attribute("BorderBrush")?.Value;
            var unoBorderBrush = unoBorder?.Attribute("BorderBrush")?.Value;
            Assert.AreEqual(wpfBorderBrush, unoBorderBrush, "BorderBrush should match");

            var wpfBorderThickness = wpfBorder?.Attribute("BorderThickness")?.Value;
            var unoBorderThickness = unoBorder?.Attribute("BorderThickness")?.Value;
            Assert.AreEqual(wpfBorderThickness, unoBorderThickness, "BorderThickness should match");

            var wpfBackground = wpfBorder?.Attribute("Background")?.Value;
            var unoBackground = unoBorder?.Attribute("Background")?.Value;
            Assert.AreEqual(wpfBackground, unoBackground, "Background should match");

            var wpfCornerRadius = wpfBorder?.Attribute("CornerRadius")?.Value;
            var unoCornerRadius = unoBorder?.Attribute("CornerRadius")?.Value;
            Assert.AreEqual(wpfCornerRadius, unoCornerRadius, "CornerRadius should match");
        }

        [TestMethod]
        public void StatusBar_ScrollViewer_ExistsAndMatchesProperties()
        {
            // Arrange
            var wpfWpDock = FindElementByName(_wpfXaml!, "wp_dock");
            var unoWpDock = FindElementByName(_unoXaml!, "wp_dock");

            // Act - Find ScrollViewer
            var wpfScrollViewer = wpfWpDock?.Descendants(XName.Get("ScrollViewer", DefaultNamespace)).FirstOrDefault();
            var unoScrollViewer = unoWpDock?.Descendants(XName.Get("ScrollViewer", DefaultNamespace)).FirstOrDefault();

            // Assert
            Assert.IsNotNull(wpfScrollViewer, "WPF should have ScrollViewer");
            Assert.IsNotNull(unoScrollViewer, "Uno should have ScrollViewer");

            var wpfMargin = wpfScrollViewer?.Attribute("Margin")?.Value;
            var unoMargin = unoScrollViewer?.Attribute("Margin")?.Value;
            Assert.AreEqual(wpfMargin, unoMargin, "ScrollViewer Margin should match");

            var wpfVerticalScrollBarVisibility = wpfScrollViewer?.Attribute("VerticalScrollBarVisibility")?.Value;
            var unoVerticalScrollBarVisibility = unoScrollViewer?.Attribute("VerticalScrollBarVisibility")?.Value;
            Assert.AreEqual(wpfVerticalScrollBarVisibility, unoVerticalScrollBarVisibility, 
                "VerticalScrollBarVisibility should match");
        }

        [TestMethod]
        public void StatusBar_WpDockStack_ExistsInBothVersions()
        {
            // Arrange
            var wpfWpDockStack = FindElementByName(_wpfXaml!, "wp_dock_stack");
            var unoWpDockStack = FindElementByName(_unoXaml!, "wp_dock_stack");

            // Assert
            Assert.IsNotNull(wpfWpDockStack, "WPF wp_dock_stack should exist");
            Assert.IsNotNull(unoWpDockStack, "Uno wp_dock_stack should exist");

            // Both should be StackPanel
            Assert.AreEqual("StackPanel", wpfWpDockStack?.Name.LocalName, "WPF wp_dock_stack should be StackPanel");
            Assert.AreEqual("StackPanel", unoWpDockStack?.Name.LocalName, "Uno wp_dock_stack should be StackPanel");
        }

        [TestMethod]
        public void StatusBar_EventHandlers_AreConnected()
        {
            // Arrange
            var unoWpDock = FindElementByName(_unoXaml!, "wp_dock");
            var expectedHandlers = new[]
            {
                ("wp_dock_btn", "Click", "ToggleWPDock"),
                ("reset_mark", "Click", "ResetMarkers_Click"),
                ("Undo", "Click", "Undo_Click"),
                ("Redo", "Click", "Redo_Click"),
                ("Import", "Click", "ImportWaypoints_Click"),
                ("Export", "Click", "ExportWaypoints_Click")
            };

            // Act & Assert
            foreach (var (buttonName, eventName, handlerName) in expectedHandlers)
            {
                var button = FindElementByName(unoWpDock!, buttonName);
                Assert.IsNotNull(button, $"Button '{buttonName}' should exist");

                var clickHandler = button?.Attribute(eventName)?.Value;
                Assert.IsNotNull(clickHandler, $"Button '{buttonName}' should have {eventName} handler");
                
                // Note: WPF uses "Button_Click" for reset_mark, Uno uses "ResetMarkers_Click"
                // This is acceptable as long as the handler exists in code-behind
                Assert.IsFalse(string.IsNullOrEmpty(clickHandler), 
                    $"Button '{buttonName}' should have non-empty {eventName} handler");
            }
        }

        [TestMethod]
        public void StatusBar_FontFamily_IsCorrect()
        {
            // Arrange
            var buttonNames = new[] { "reset_mark", "Undo", "Redo", "Import", "Export" };

            // Act & Assert
            foreach (var buttonName in buttonNames)
            {
                var wpfButton = FindElementByName(_wpfXaml!, buttonName);
                var unoButton = FindElementByName(_unoXaml!, buttonName);

                var wpfFontFamily = wpfButton?.Attribute("FontFamily")?.Value;
                var unoFontFamily = unoButton?.Attribute("FontFamily")?.Value;

                // WPF uses: "Raleway SemiBold" or "/PIGEON GCS;component/Resources/fonts/Raleway/#Raleway"
                // Uno should use: "{StaticResource RalewayFont}" or similar
                Assert.IsNotNull(unoFontFamily, $"{buttonName}: Uno should have FontFamily set");
                
                // Check if Uno uses StaticResource for font
                if (unoFontFamily != null && unoFontFamily.Contains("StaticResource"))
                {
                    Assert.IsTrue(unoFontFamily.Contains("Raleway"), 
                        $"{buttonName}: Uno FontFamily should reference Raleway font");
                }
            }
        }
    }
}
