using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Xml.Linq;
using System.Linq;

namespace Pigeon_Uno.Tests.UIConversion
{
    /// <summary>
    /// Tests for verifying the mission planning toolbar conversion from WPF to Uno Platform.
    /// Task 5.5: Convert mission planning toolbar
    /// Requirements: 1.3, 1.6, 6.2.3
    /// </summary>
    [TestClass]
    public class MissionPlanningToolbarConversionTests : PageConversionTestBase
    {
        private XDocument _wpfXaml;
        private XDocument _unoXaml;

        [TestInitialize]
        public void Setup()
        {
            // WPF source: Waypoint.xaml (the map control in WPF)
            // Uno target: MapPage.xaml
            _wpfXaml = LoadXamlDocument("Pigeon_WPF_cs/Pigeon_WPF_cs/Custom UserControls/Waypoint.xaml");
            _unoXaml = LoadXamlDocument("Pigeon_Uno/Pigeon_Uno/Views/MapPage.xaml");
        }

        [TestMethod]
        public void MissionToolbar_DockPanelConvertedToStackPanel()
        {
            // WPF uses DockPanel for wp_dock
            var wpfDockPanel = FindElementByName(_wpfXaml, "wp_dock");
            Assert.IsNotNull(wpfDockPanel, "WPF wp_dock should exist");
            Assert.AreEqual("DockPanel", wpfDockPanel.Name.LocalName, "WPF should use DockPanel");

            // Uno should use StackPanel
            var unoStackPanel = FindElementByName(_unoXaml, "wp_dock");
            Assert.IsNotNull(unoStackPanel, "Uno wp_dock should exist");
            Assert.AreEqual("StackPanel", unoStackPanel.Name.LocalName, "Uno should use StackPanel");
        }

        [TestMethod]
        public void MissionToolbar_AllButtonsPresent()
        {
            var unoWpDock = FindElementByName(_unoXaml, "wp_dock");
            Assert.IsNotNull(unoWpDock, "wp_dock should exist");

            // Find all buttons in the toolbar
            var buttons = unoWpDock.Descendants()
                .Where(e => e.Name.LocalName == "Button")
                .ToList();

            // Expected buttons: wp_dock_btn, reset_mark, Undo, Redo, Import, Export
            var buttonNames = buttons
                .Select(b => GetAttributeValue(b, "Name"))
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();

            Assert.IsTrue(buttonNames.Contains("wp_dock_btn"), "wp_dock_btn should exist");
            Assert.IsTrue(buttonNames.Contains("reset_mark"), "reset_mark button should exist");
            Assert.IsTrue(buttonNames.Contains("Undo"), "Undo button should exist");
            Assert.IsTrue(buttonNames.Contains("Redo"), "Redo button should exist");
            Assert.IsTrue(buttonNames.Contains("Import"), "Import button should exist");
            Assert.IsTrue(buttonNames.Contains("Export"), "Export button should exist");
        }

        [TestMethod]
        public void MissionToolbar_ResetButton_HasCorrectProperties()
        {
            var resetButton = FindElementByName(_unoXaml, "reset_mark");
            Assert.IsNotNull(resetButton, "reset_mark button should exist");

            // Verify properties
            Assert.AreEqual("Reset", GetAttributeValue(resetButton, "Content"), "Content should be 'Reset'");
            Assert.AreEqual("411,0,0,-2", GetAttributeValue(resetButton, "Margin"), "Margin should match WPF");
            Assert.AreEqual("Black", GetAttributeValue(resetButton, "Background"), "Background should be Black");
            Assert.AreEqual("White", GetAttributeValue(resetButton, "Foreground"), "Foreground should be White");
            Assert.AreEqual("Black", GetAttributeValue(resetButton, "BorderBrush"), "BorderBrush should be Black");
            Assert.AreEqual("30", GetAttributeValue(resetButton, "Height"), "Height should be 30");
            Assert.AreEqual("40", GetAttributeValue(resetButton, "Width"), "Width should be 40");
            Assert.AreEqual("Left", GetAttributeValue(resetButton, "HorizontalAlignment"), "HorizontalAlignment should be Left");
        }

        [TestMethod]
        public void MissionToolbar_UndoButton_HasCorrectProperties()
        {
            var undoButton = FindElementByName(_unoXaml, "Undo");
            Assert.IsNotNull(undoButton, "Undo button should exist");

            // Verify properties
            Assert.AreEqual("Undo", GetAttributeValue(undoButton, "Content"), "Content should be 'Undo'");
            Assert.AreEqual("331,0,0,-2", GetAttributeValue(undoButton, "Margin"), "Margin should match WPF");
            Assert.AreEqual("Black", GetAttributeValue(undoButton, "Background"), "Background should be Black");
            Assert.AreEqual("White", GetAttributeValue(undoButton, "Foreground"), "Foreground should be White");
            Assert.AreEqual("30", GetAttributeValue(undoButton, "Height"), "Height should be 30");
            Assert.AreEqual("40", GetAttributeValue(undoButton, "Width"), "Width should be 40");
        }

        [TestMethod]
        public void MissionToolbar_RedoButton_HasCorrectProperties()
        {
            var redoButton = FindElementByName(_unoXaml, "Redo");
            Assert.IsNotNull(redoButton, "Redo button should exist");

            // Verify properties
            Assert.AreEqual("Redo", GetAttributeValue(redoButton, "Content"), "Content should be 'Redo'");
            Assert.AreEqual("371,-1,0,-1", GetAttributeValue(redoButton, "Margin"), "Margin should match WPF");
            Assert.AreEqual("Black", GetAttributeValue(redoButton, "Background"), "Background should be Black");
            Assert.AreEqual("White", GetAttributeValue(redoButton, "Foreground"), "Foreground should be White");
            Assert.AreEqual("30", GetAttributeValue(redoButton, "Height"), "Height should be 30");
            Assert.AreEqual("40", GetAttributeValue(redoButton, "Width"), "Width should be 40");
        }

        [TestMethod]
        public void MissionToolbar_ImportButton_HasCorrectProperties()
        {
            var importButton = FindElementByName(_unoXaml, "Import");
            Assert.IsNotNull(importButton, "Import button should exist");

            // Verify properties
            Assert.AreEqual("Import", GetAttributeValue(importButton, "Content"), "Content should be 'Import'");
            Assert.AreEqual("499,0,0,-2", GetAttributeValue(importButton, "Margin"), "Margin should match WPF");
            Assert.AreEqual("Black", GetAttributeValue(importButton, "Background"), "Background should be Black");
            Assert.AreEqual("White", GetAttributeValue(importButton, "Foreground"), "Foreground should be White");
            Assert.AreEqual("30", GetAttributeValue(importButton, "Height"), "Height should be 30");
            Assert.AreEqual("50", GetAttributeValue(importButton, "Width"), "Width should be 50");
        }

        [TestMethod]
        public void MissionToolbar_ExportButton_HasCorrectProperties()
        {
            var exportButton = FindElementByName(_unoXaml, "Export");
            Assert.IsNotNull(exportButton, "Export button should exist");

            // Verify properties
            Assert.AreEqual("Export", GetAttributeValue(exportButton, "Content"), "Content should be 'Export'");
            Assert.AreEqual("265,0,50,0", GetAttributeValue(exportButton, "Margin"), "Margin should match WPF");
            Assert.AreEqual("Black", GetAttributeValue(exportButton, "Background"), "Background should be Black");
            Assert.AreEqual("White", GetAttributeValue(exportButton, "Foreground"), "Foreground should be White");
            Assert.AreEqual("30", GetAttributeValue(exportButton, "Height"), "Height should be 30");
            Assert.AreEqual("50", GetAttributeValue(exportButton, "Width"), "Width should be 50");
            Assert.AreEqual("Right", GetAttributeValue(exportButton, "HorizontalAlignment"), "HorizontalAlignment should be Right");
        }

        [TestMethod]
        public void MissionToolbar_AllEventHandlersConnected()
        {
            var wpDock = FindElementByName(_unoXaml, "wp_dock");
            var buttons = wpDock.Descendants()
                .Where(e => e.Name.LocalName == "Button")
                .ToList();

            // Verify each button has a Click event handler
            var wpDockBtn = FindElementByName(_unoXaml, "wp_dock_btn");
            Assert.AreEqual("ToggleWPDock", GetAttributeValue(wpDockBtn, "Click"), "wp_dock_btn should have ToggleWPDock handler");

            var resetBtn = FindElementByName(_unoXaml, "reset_mark");
            Assert.AreEqual("Button_Click", GetAttributeValue(resetBtn, "Click"), "reset_mark should have Button_Click handler");

            var undoBtn = FindElementByName(_unoXaml, "Undo");
            Assert.AreEqual("Undo_Click", GetAttributeValue(undoBtn, "Click"), "Undo should have Undo_Click handler");

            var redoBtn = FindElementByName(_unoXaml, "Redo");
            Assert.AreEqual("Redo_Click", GetAttributeValue(redoBtn, "Click"), "Redo should have Redo_Click handler");

            var importBtn = FindElementByName(_unoXaml, "Import");
            Assert.AreEqual("ImportWaypoints_Click", GetAttributeValue(importBtn, "Click"), "Import should have ImportWaypoints_Click handler");

            var exportBtn = FindElementByName(_unoXaml, "Export");
            Assert.AreEqual("ExportWaypoints_Click", GetAttributeValue(exportBtn, "Click"), "Export should have ExportWaypoints_Click handler");
        }

        [TestMethod]
        public void MissionToolbar_FontFamilyCorrectlyConverted()
        {
            // WPF uses: FontFamily="Raleway SemiBold"
            // Uno should use: FontFamily="{StaticResource RalewayFont}" FontWeight="SemiBold"

            var resetButton = FindElementByName(_unoXaml, "reset_mark");
            var fontFamily = GetAttributeValue(resetButton, "FontFamily");
            var fontWeight = GetAttributeValue(resetButton, "FontWeight");

            // Uno should use StaticResource for font
            Assert.IsTrue(fontFamily.Contains("StaticResource") || fontFamily.Contains("RalewayFont"), 
                "FontFamily should use StaticResource RalewayFont");
            Assert.AreEqual("SemiBold", fontWeight, "FontWeight should be SemiBold");
        }

        [TestMethod]
        public void MissionToolbar_GridStructureMatches()
        {
            var wpfGrid = _wpfXaml.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "DockPanel" && GetAttributeValue(e, "Name") == "wp_dock")
                ?.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "Grid");

            var unoGrid = _unoXaml.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "StackPanel" && GetAttributeValue(e, "Name") == "wp_dock")
                ?.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "Grid");

            Assert.IsNotNull(wpfGrid, "WPF Grid should exist in wp_dock");
            Assert.IsNotNull(unoGrid, "Uno Grid should exist in wp_dock");

            // Verify Grid properties
            Assert.AreEqual("28", GetAttributeValue(unoGrid, "Height"), "Grid Height should be 28");
            Assert.AreEqual("550", GetAttributeValue(unoGrid, "Width"), "Grid Width should be 550");

            // Verify ColumnDefinitions
            var unoColumns = unoGrid.Descendants()
                .Where(e => e.Name.LocalName == "ColumnDefinition")
                .ToList();
            Assert.AreEqual(2, unoColumns.Count, "Grid should have 2 columns");
        }

        [TestMethod]
        public void MissionToolbar_PolygonDecoratorPresent()
        {
            // Both WPF and Uno should have the Polygon decorator
            var wpfPolygon = _wpfXaml.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "Polygon" && 
                                   GetAttributeValue(e, "Points") == "0,22.5 20,0 100,0 120,22.5");

            var unoPolygon = _unoXaml.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "Polygon" && 
                                   GetAttributeValue(e, "Points") == "0,22.5 20,0 100,0 120,22.5");

            Assert.IsNotNull(wpfPolygon, "WPF Polygon should exist");
            Assert.IsNotNull(unoPolygon, "Uno Polygon should exist");

            // Verify Polygon properties
            Assert.AreEqual("#CC000000", GetAttributeValue(unoPolygon, "Fill"), "Polygon Fill should be #CC000000");
            Assert.AreEqual("70,0,0,0", GetAttributeValue(unoPolygon, "Margin"), "Polygon Margin should match");
        }

        [TestMethod]
        public void MissionToolbar_ScrollViewerStructureCorrect()
        {
            var unoWpDock = FindElementByName(_unoXaml, "wp_dock");
            
            // Find Border containing ScrollViewer
            var border = unoWpDock.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "Border" && 
                                   GetAttributeValue(e, "BorderBrush") == "Black");

            Assert.IsNotNull(border, "Border should exist");
            Assert.AreEqual("0,0,0,1", GetAttributeValue(border, "BorderThickness"), "BorderThickness should be 0,0,0,1");
            Assert.AreEqual("#CC000000", GetAttributeValue(border, "Background"), "Background should be #CC000000");
            Assert.AreEqual("5,5,0,0", GetAttributeValue(border, "CornerRadius"), "CornerRadius should be 5,5,0,0");

            // Find ScrollViewer
            var scrollViewer = border.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "ScrollViewer");
            Assert.IsNotNull(scrollViewer, "ScrollViewer should exist");

            // Find wp_dock_stack StackPanel
            var wpDockStack = FindElementByName(_unoXaml, "wp_dock_stack");
            Assert.IsNotNull(wpDockStack, "wp_dock_stack should exist");
            Assert.AreEqual("StackPanel", wpDockStack.Name.LocalName, "wp_dock_stack should be a StackPanel");
        }
    }
}
