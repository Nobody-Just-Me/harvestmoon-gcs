using Microsoft.VisualStudio.TestTools.UnitTesting;
using HarvestmoonGCS.Tests.UIConversion;
using System.Linq;

namespace HarvestmoonGCS.Tests.UIConversion
{
    /// <summary>
    /// Unit tests for WaypointItem.xaml conversion from WPF to Uno Platform
    /// </summary>
    [TestClass]
    public class WaypointItemConversionTests : PageConversionTestBase
    {
        private const string WpfPath = "Pigeon_WPF_cs/Pigeon_WPF_cs/Custom UserControls/Waypoint.xaml";
        private const string UnoPath = "HarvestmoonGCS/HarvestmoonGCS/Views/WaypointItem.xaml";

        [TestMethod]
        public void WaypointItem_GridStructure_MatchesWpf()
        {
            // Arrange
            var wpfXaml = LoadXamlFile(WpfPath);
            var unoXaml = LoadXamlFile(UnoPath);

            // Act
            var wpfGrid = XamlParsingUtilities.ParseGridStructure(wpfXaml);
            var unoGrid = XamlParsingUtilities.ParseGridStructure(unoXaml);

            // Assert
            Assert.AreEqual(wpfGrid.RowCount, unoGrid.RowCount, "Row count should match");
            Assert.AreEqual(2, unoGrid.RowCount, "Should have 2 rows");
            
            Assert.AreEqual(wpfGrid.ColumnCount, unoGrid.ColumnCount, "Column count should match");
            Assert.AreEqual(14, unoGrid.ColumnCount, "Should have 14 columns");

            // Verify row definitions
            CollectionAssert.AreEqual(wpfGrid.RowHeights, unoGrid.RowHeights, "Row heights should match");
            
            // Verify column definitions
            CollectionAssert.AreEqual(wpfGrid.ColumnWidths, unoGrid.ColumnWidths, "Column widths should match");
        }

        [TestMethod]
        public void WaypointItem_ImageSource_IsValid()
        {
            // Arrange
            var unoXaml = LoadXamlFile(UnoPath);

            // Act
            var images = XamlParsingUtilities.ExtractControlsByType(unoXaml, "Image");
            var waypointImage = images.FirstOrDefault(img => img.GetAttribute("x:Name") == "wp_ikon");

            // Assert
            Assert.IsNotNull(waypointImage, "Waypoint image should exist");
            
            var source = waypointImage.GetAttribute("Source");
            Assert.IsNotNull(source, "Image source should be set");
            Assert.IsTrue(source.StartsWith("ms-appx:///"), "Image source should use ms-appx:/// format");
            Assert.IsTrue(source.Contains("marker-waypoint.png"), "Image source should reference marker-waypoint.png");
        }

        [TestMethod]
        public void WaypointItem_AllTextBoxControls_Exist()
        {
            // Arrange
            var unoXaml = LoadXamlFile(UnoPath);

            // Act
            var textBoxes = XamlParsingUtilities.ExtractControlsByType(unoXaml, "TextBox");

            // Assert - Should have 7 TextBoxes (param1-4, alt, lat, lon)
            Assert.IsTrue(textBoxes.Count >= 7, $"Should have at least 7 TextBoxes, found {textBoxes.Count}");

            // Verify specific TextBoxes exist
            var expectedTextBoxes = new[] { "wp_param1", "wp_param2", "wp_param3", "wp_param4", "wp_alt", "wp_lat", "wp_longt" };
            foreach (var expectedName in expectedTextBoxes)
            {
                var textBox = textBoxes.FirstOrDefault(tb => tb.GetAttribute("x:Name") == expectedName);
                Assert.IsNotNull(textBox, $"TextBox '{expectedName}' should exist");
            }
        }

        [TestMethod]
        public void WaypointItem_ComboBox_HasCorrectItems()
        {
            // Arrange
            var unoXaml = LoadXamlFile(UnoPath);

            // Act
            var comboBoxes = XamlParsingUtilities.ExtractControlsByType(unoXaml, "ComboBox");
            var commandComboBox = comboBoxes.FirstOrDefault(cb => cb.GetAttribute("x:Name") == "wp_command");

            // Assert
            Assert.IsNotNull(commandComboBox, "Command ComboBox should exist");

            var items = XamlParsingUtilities.ExtractComboBoxItems(commandComboBox);
            Assert.AreEqual(13, items.Count, "Should have 13 ComboBox items");

            var expectedItems = new[] { 
                "Waypoint", "Takeoff", "Land", "Q_Takeoff", "Q_Land", "RTL", "SetHome", 
                "Auto", "Loiter", "FBWA", "Q_Loiter", "Q_Stabilize", "Manual" 
            };
            CollectionAssert.AreEqual(expectedItems, items.ToArray(), "ComboBox items should match WPF");
        }

        [TestMethod]
        public void WaypointItem_LatLonTextBoxes_HaveCorrectProperties()
        {
            // Arrange
            var unoXaml = LoadXamlFile(UnoPath);

            // Act
            var textBoxes = XamlParsingUtilities.ExtractControlsByType(unoXaml, "TextBox");
            var latTextBox = textBoxes.FirstOrDefault(tb => tb.GetAttribute("x:Name") == "wp_lat");
            var lonTextBox = textBoxes.FirstOrDefault(tb => tb.GetAttribute("x:Name") == "wp_longt");

            // Assert
            Assert.IsNotNull(latTextBox, "Latitude TextBox should exist");
            Assert.IsNotNull(lonTextBox, "Longitude TextBox should exist");

            // Verify properties
            Assert.AreEqual("Trebuchet MS", latTextBox.GetAttribute("FontFamily"), "Lat TextBox should have correct FontFamily");
            Assert.AreEqual("14", latTextBox.GetAttribute("FontSize"), "Lat TextBox should have correct FontSize");
            Assert.AreEqual("#FF15008B", latTextBox.GetAttribute("Foreground"), "Lat TextBox should have correct Foreground");

            Assert.AreEqual("Trebuchet MS", lonTextBox.GetAttribute("FontFamily"), "Lon TextBox should have correct FontFamily");
            Assert.AreEqual("14", lonTextBox.GetAttribute("FontSize"), "Lon TextBox should have correct FontSize");
            Assert.AreEqual("#FF15008B", lonTextBox.GetAttribute("Foreground"), "Lon TextBox should have correct Foreground");
        }

        [TestMethod]
        public void WaypointItem_EventHandlers_AreConnected()
        {
            // Arrange
            var unoXaml = LoadXamlFile(UnoPath);

            // Act
            var comboBox = XamlParsingUtilities.ExtractControlsByType(unoXaml, "ComboBox")
                .FirstOrDefault(cb => cb.GetAttribute("x:Name") == "wp_command");
            var latTextBox = XamlParsingUtilities.ExtractControlsByType(unoXaml, "TextBox")
                .FirstOrDefault(tb => tb.GetAttribute("x:Name") == "wp_lat");
            var lonTextBox = XamlParsingUtilities.ExtractControlsByType(unoXaml, "TextBox")
                .FirstOrDefault(tb => tb.GetAttribute("x:Name") == "wp_longt");
            var buttons = XamlParsingUtilities.ExtractControlsByType(unoXaml, "Button");

            // Assert
            Assert.IsNotNull(comboBox, "ComboBox should exist");
            Assert.AreEqual("wp_command_SelectionChanged", comboBox.GetAttribute("SelectionChanged"), 
                "ComboBox should have SelectionChanged event handler");

            Assert.IsNotNull(latTextBox, "Lat TextBox should exist");
            Assert.AreEqual("wp_lat_TextChanged", latTextBox.GetAttribute("TextChanged"), 
                "Lat TextBox should have TextChanged event handler");

            Assert.IsNotNull(lonTextBox, "Lon TextBox should exist");
            Assert.AreEqual("wp_longt_TextChanged", lonTextBox.GetAttribute("TextChanged"), 
                "Lon TextBox should have TextChanged event handler");

            // Verify move up/down buttons
            var moveUpButton = buttons.FirstOrDefault(b => b.GetAttribute("Click") == "MoveUp_Click");
            var moveDownButton = buttons.FirstOrDefault(b => b.GetAttribute("Click") == "MoveDown_Click");
            Assert.IsNotNull(moveUpButton, "Move up button should exist");
            Assert.IsNotNull(moveDownButton, "Move down button should exist");
        }

        [TestMethod]
        public void WaypointItem_BorderProperties_MatchWpf()
        {
            // Arrange
            var unoXaml = LoadXamlFile(UnoPath);

            // Act
            var borders = XamlParsingUtilities.ExtractControlsByType(unoXaml, "Border");
            var mainBorder = borders.FirstOrDefault();

            // Assert
            Assert.IsNotNull(mainBorder, "Main Border should exist");
            Assert.AreEqual("#FFEBEBEB", mainBorder.GetAttribute("Background"), "Border should have correct Background");
            Assert.AreEqual("10", mainBorder.GetAttribute("CornerRadius"), "Border should have correct CornerRadius");
        }

        [TestMethod]
        public void WaypointItem_StackPanels_HaveCorrectLayout()
        {
            // Arrange
            var unoXaml = LoadXamlFile(UnoPath);

            // Act
            var stackPanels = XamlParsingUtilities.ExtractControlsByType(unoXaml, "StackPanel");

            // Assert
            Assert.IsTrue(stackPanels.Count >= 8, $"Should have at least 8 StackPanels, found {stackPanels.Count}");

            // Verify StackPanels are used instead of DockPanels (Uno conversion pattern)
            var dockPanels = XamlParsingUtilities.ExtractControlsByType(unoXaml, "DockPanel");
            Assert.AreEqual(0, dockPanels.Count, "Should not have any DockPanels (converted to StackPanels)");
        }

        [TestMethod]
        public void WaypointItem_TextBlocks_UsedInsteadOfLabels()
        {
            // Arrange
            var unoXaml = LoadXamlFile(UnoPath);

            // Act
            var labels = XamlParsingUtilities.ExtractControlsByType(unoXaml, "Label");
            var textBlocks = XamlParsingUtilities.ExtractControlsByType(unoXaml, "TextBlock");

            // Assert
            Assert.AreEqual(0, labels.Count, "Should not have any Labels (converted to TextBlocks)");
            Assert.IsTrue(textBlocks.Count > 0, "Should have TextBlocks instead of Labels");
        }

        [TestMethod]
        public void WaypointItem_MoveUpDownButtons_Exist()
        {
            // Arrange
            var unoXaml = LoadXamlFile(UnoPath);

            // Act
            var buttons = XamlParsingUtilities.ExtractControlsByType(unoXaml, "Button");
            var moveUpButton = buttons.FirstOrDefault(b => b.GetAttribute("Content") == "↑");
            var moveDownButton = buttons.FirstOrDefault(b => b.GetAttribute("Content") == "↓");

            // Assert
            Assert.IsNotNull(moveUpButton, "Move up button should exist");
            Assert.IsNotNull(moveDownButton, "Move down button should exist");
            Assert.AreEqual("MoveUp_Click", moveUpButton.GetAttribute("Click"), "Move up button should have correct event handler");
            Assert.AreEqual("MoveDown_Click", moveDownButton.GetAttribute("Click"), "Move down button should have correct event handler");
        }

        [TestMethod]
        public void WaypointItem_ControlMargins_MatchWpf()
        {
            // Arrange
            var wpfXaml = LoadXamlFile(WpfPath);
            var unoXaml = LoadXamlFile(UnoPath);

            // Act
            var wpfImage = XamlParsingUtilities.ExtractControlsByType(wpfXaml, "Image")
                .FirstOrDefault(img => img.GetAttribute("x:Name") == "wp_ikon");
            var unoImage = XamlParsingUtilities.ExtractControlsByType(unoXaml, "Image")
                .FirstOrDefault(img => img.GetAttribute("x:Name") == "wp_ikon");

            // Assert
            Assert.IsNotNull(wpfImage, "WPF image should exist");
            Assert.IsNotNull(unoImage, "Uno image should exist");
            Assert.AreEqual(wpfImage.GetAttribute("Margin"), unoImage.GetAttribute("Margin"), 
                "Image margins should match");
        }

        [TestMethod]
        public void WaypointItem_ParamTextBoxes_HaveCorrectProperties()
        {
            // Arrange
            var unoXaml = LoadXamlFile(UnoPath);

            // Act
            var textBoxes = XamlParsingUtilities.ExtractControlsByType(unoXaml, "TextBox");
            var param1 = textBoxes.FirstOrDefault(tb => tb.GetAttribute("x:Name") == "wp_param1");
            var param2 = textBoxes.FirstOrDefault(tb => tb.GetAttribute("x:Name") == "wp_param2");
            var param3 = textBoxes.FirstOrDefault(tb => tb.GetAttribute("x:Name") == "wp_param3");
            var param4 = textBoxes.FirstOrDefault(tb => tb.GetAttribute("x:Name") == "wp_param4");

            // Assert
            Assert.IsNotNull(param1, "Param1 TextBox should exist");
            Assert.IsNotNull(param2, "Param2 TextBox should exist");
            Assert.IsNotNull(param3, "Param3 TextBox should exist");
            Assert.IsNotNull(param4, "Param4 TextBox should exist");

            // Verify common properties
            foreach (var param in new[] { param1, param2, param3, param4 })
            {
                Assert.AreEqual("Trebuchet MS", param.GetAttribute("FontFamily"), "Param TextBox should have correct FontFamily");
                Assert.AreEqual("14", param.GetAttribute("FontSize"), "Param TextBox should have correct FontSize");
                Assert.AreEqual("#FF15008B", param.GetAttribute("Foreground"), "Param TextBox should have correct Foreground");
                Assert.AreEqual("Center", param.GetAttribute("HorizontalTextAlignment"), "Param TextBox should have centered text");
            }
        }

        [TestMethod]
        public void WaypointItem_AltitudeTextBox_HasCorrectProperties()
        {
            // Arrange
            var unoXaml = LoadXamlFile(UnoPath);

            // Act
            var textBoxes = XamlParsingUtilities.ExtractControlsByType(unoXaml, "TextBox");
            var altTextBox = textBoxes.FirstOrDefault(tb => tb.GetAttribute("x:Name") == "wp_alt");

            // Assert
            Assert.IsNotNull(altTextBox, "Altitude TextBox should exist");
            Assert.AreEqual("Trebuchet MS", altTextBox.GetAttribute("FontFamily"), "Alt TextBox should have correct FontFamily");
            Assert.AreEqual("14", altTextBox.GetAttribute("FontSize"), "Alt TextBox should have correct FontSize");
            Assert.AreEqual("#FF15008B", altTextBox.GetAttribute("Foreground"), "Alt TextBox should have correct Foreground");
        }
    }
}
