using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Xml.Linq;
using System.Linq;

namespace HarvestmoonGCS.Tests.UIConversion;

/// <summary>
/// Unit tests for MapPage XAML conversion from WPF to Uno Platform
/// Tests for Task 5.9: Write unit tests for MapPage conversion
/// Validates: Requirements 1.2, 1.3, 1.4, 1.6
/// </summary>
[TestClass]
public class MapPageConversionTests : PageConversionTestBase
{
    private const string UnoPath = "Views/MapPage.xaml";
    private const string PageName = "MapPage";
    private XDocument _unoXaml;

    [TestInitialize]
    public void Setup()
    {
        _unoXaml = XamlParsingUtilities.LoadXamlDocument($"HarvestmoonGCS/HarvestmoonGCS/{UnoPath}");
    }

    [TestMethod]
    public void MapPage_GridStructure_IsCorrect()
    {
        // Arrange & Act
        var mainGrid = _unoXaml.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Grid" && e.Parent?.Name.LocalName == "Page");

        // Assert
        Assert.IsNotNull(mainGrid, "Main Grid should exist");

        // Verify RowDefinitions
        var rowDefs = mainGrid.Elements()
            .Where(e => e.Name.LocalName == "Grid.RowDefinitions")
            .SelectMany(e => e.Elements())
            .ToList();

        Assert.AreEqual(2, rowDefs.Count, "Grid should have 2 rows");
        Assert.AreEqual("55", rowDefs[0].Attribute("Height")?.Value, "First row should be 55");
        Assert.IsNull(rowDefs[1].Attribute("Height"), "Second row should be * (no Height attribute)");

        // Verify ColumnDefinitions
        var colDefs = mainGrid.Elements()
            .Where(e => e.Name.LocalName == "Grid.ColumnDefinitions")
            .SelectMany(e => e.Elements())
            .ToList();

        Assert.AreEqual(2, colDefs.Count, "Grid should have 2 columns");
    }

    [TestMethod]
    public void MapPage_MapPlaceholder_Exists()
    {
        // Arrange & Act
        var mapPlaceholder = XamlParsingUtilities.FindElementByName(_unoXaml, "map_placeholder");

        // Assert
        Assert.IsNotNull(mapPlaceholder, "Map placeholder Border should exist");
        Assert.AreEqual("Border", mapPlaceholder.Name.LocalName, "Map placeholder should be a Border");

        // Verify properties
        Assert.IsNotNull(mapPlaceholder.Attribute("Background")?.Value, "Map placeholder should have Background");
        Assert.IsNotNull(mapPlaceholder.Attribute("BorderBrush")?.Value, "Map placeholder should have BorderBrush");
        Assert.IsNotNull(mapPlaceholder.Attribute("BorderThickness")?.Value, "Map placeholder should have BorderThickness");

        // Verify it contains a TextBlock with placeholder text
        var textBlock = mapPlaceholder.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "TextBlock");
        Assert.IsNotNull(textBlock, "Map placeholder should contain a TextBlock");
        Assert.IsTrue(textBlock.Attribute("Text")?.Value.Contains("Map Placeholder"), 
            "TextBlock should contain placeholder text");
    }

    [TestMethod]
    public void MapPage_WaypointListPanel_HasCorrectStructure()
    {
        // Arrange & Act
        var wpDock = XamlParsingUtilities.FindElementByName(_unoXaml, "wp_dock");

        // Assert
        Assert.IsNotNull(wpDock, "Waypoint dock panel should exist");
        Assert.AreEqual("StackPanel", wpDock.Name.LocalName, "Waypoint dock should be a StackPanel");

        // Verify positioning
        Assert.AreEqual("1", wpDock.Attribute("Grid.Row")?.Value, "wp_dock should be in Grid.Row 1");
        Assert.AreEqual("Bottom", wpDock.Attribute("VerticalAlignment")?.Value, 
            "wp_dock should be aligned to Bottom");

        // Verify it contains a Grid with buttons
        var buttonGrid = wpDock.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Grid");
        Assert.IsNotNull(buttonGrid, "wp_dock should contain a Grid for buttons");

        // Verify it contains a Border with ScrollViewer
        var border = wpDock.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Border");
        Assert.IsNotNull(border, "wp_dock should contain a Border");

        var scrollViewer = border.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "ScrollViewer");
        Assert.IsNotNull(scrollViewer, "Border should contain a ScrollViewer");

        // Verify wp_dock_stack exists
        var wpDockStack = XamlParsingUtilities.FindElementByName(_unoXaml, "wp_dock_stack");
        Assert.IsNotNull(wpDockStack, "wp_dock_stack should exist");
        Assert.AreEqual("StackPanel", wpDockStack.Name.LocalName, "wp_dock_stack should be a StackPanel");
    }

    [TestMethod]
    public void MapPage_WaypointListDataTemplate_IsValid()
    {
        // Arrange & Act
        var geofencePointsList = XamlParsingUtilities.FindElementByName(_unoXaml, "geofence_points_list");

        // Assert
        Assert.IsNotNull(geofencePointsList, "geofence_points_list should exist");
        Assert.AreEqual("ItemsControl", geofencePointsList.Name.LocalName, 
            "geofence_points_list should be an ItemsControl");

        // Verify DataTemplate exists
        var dataTemplate = geofencePointsList.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "DataTemplate");
        Assert.IsNotNull(dataTemplate, "ItemsControl should have a DataTemplate");

        // Verify DataTemplate structure contains bindings
        var bindings = dataTemplate.Descendants()
            .SelectMany(e => e.Attributes())
            .Where(a => a.Value.Contains("{Binding"))
            .ToList();
        Assert.IsTrue(bindings.Count > 0, "DataTemplate should contain bindings");

        // Verify specific bindings exist (Index, LatFormatted, LonFormatted)
        var bindingPaths = bindings.Select(b => b.Value).ToList();
        Assert.IsTrue(bindingPaths.Any(b => b.Contains("Index")), 
            "DataTemplate should bind to Index");
        Assert.IsTrue(bindingPaths.Any(b => b.Contains("LatFormatted")), 
            "DataTemplate should bind to LatFormatted");
        Assert.IsTrue(bindingPaths.Any(b => b.Contains("LonFormatted")), 
            "DataTemplate should bind to LonFormatted");
    }

    [TestMethod]
    public void MapPage_AllBindings_AreValid()
    {
        // Arrange
        var unoXaml = LoadUnoXaml(UnoPath);

        // Act
        var bindings = XamlParsingUtilities.ExtractBindings(unoXaml);

        // Assert
        Assert.IsTrue(bindings.Count > 0, "MapPage should have bindings");

        foreach (var binding in bindings)
        {
            Assert.IsTrue(binding.BindingExpression.StartsWith("{Binding"), 
                $"Invalid binding syntax in {binding.ControlName ?? binding.ControlType}.{binding.Property}");
            Assert.IsTrue(binding.BindingExpression.EndsWith("}"), 
                $"Invalid binding syntax in {binding.ControlName ?? binding.ControlType}.{binding.Property}");
        }
    }

    [TestMethod]
    public void MapPage_AllEventHandlers_AreConnected()
    {
        // Arrange
        var unoXaml = LoadUnoXaml(UnoPath);

        // Act
        var handlers = XamlParsingUtilities.ExtractEventHandlers(unoXaml);

        // Assert
        Assert.IsTrue(handlers.Count > 0, "MapPage should have event handlers");

        foreach (var handler in handlers)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(handler.HandlerMethod), 
                $"Event handler for {handler.EventName} on {handler.ControlName ?? handler.ControlType} is empty");
        }

        // Verify specific critical event handlers exist
        var handlerMethods = handlers.Select(h => h.HandlerMethod).ToList();
        Assert.IsTrue(handlerMethods.Contains("ChooseMap"), "ChooseMap handler should exist");
        Assert.IsTrue(handlerMethods.Contains("SendWaypointCommand"), "SendWaypointCommand handler should exist");
        Assert.IsTrue(handlerMethods.Contains("FollowVehicle_Click"), "FollowVehicle_Click handler should exist");
        Assert.IsTrue(handlerMethods.Contains("ToggleGeofencePanel_Click"), 
            "ToggleGeofencePanel_Click handler should exist");
        Assert.IsTrue(handlerMethods.Contains("ResetMarkers_Click"), "ResetMarkers_Click handler should exist");
        Assert.IsTrue(handlerMethods.Contains("Undo_Click"), "Undo_Click handler should exist");
        Assert.IsTrue(handlerMethods.Contains("Redo_Click"), "Redo_Click handler should exist");
        Assert.IsTrue(handlerMethods.Contains("ImportWaypoints_Click"), "ImportWaypoints_Click handler should exist");
        Assert.IsTrue(handlerMethods.Contains("ExportWaypoints_Click"), "ExportWaypoints_Click handler should exist");
    }

    [TestMethod]
    public void MapPage_GeofencePanel_HasCorrectStructure()
    {
        // Arrange & Act
        var geofencePanel = XamlParsingUtilities.FindElementByName(_unoXaml, "geofence_panel");

        // Assert
        Assert.IsNotNull(geofencePanel, "Geofence panel should exist");
        Assert.AreEqual("Border", geofencePanel.Name.LocalName, "Geofence panel should be a Border");

        // Verify positioning
        Assert.AreEqual("1", geofencePanel.Attribute("Grid.Row")?.Value, 
            "geofence_panel should be in Grid.Row 1");
        Assert.AreEqual("Right", geofencePanel.Attribute("HorizontalAlignment")?.Value, 
            "geofence_panel should be aligned to Right");
        Assert.AreEqual("Top", geofencePanel.Attribute("VerticalAlignment")?.Value, 
            "geofence_panel should be aligned to Top");

        // Verify it contains a Grid
        var grid = geofencePanel.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Grid");
        Assert.IsNotNull(grid, "Geofence panel should contain a Grid");

        // Verify Grid has RowDefinitions
        var rowDefs = grid.Elements()
            .Where(e => e.Name.LocalName == "Grid.RowDefinitions")
            .SelectMany(e => e.Elements())
            .ToList();
        Assert.IsTrue(rowDefs.Count >= 4, "Geofence panel Grid should have at least 4 rows");

        // Verify key controls exist
        var cbGeofenceType = XamlParsingUtilities.FindElementByName(_unoXaml, "cb_geofence_type");
        Assert.IsNotNull(cbGeofenceType, "cb_geofence_type should exist");

        var tbGeofenceRadius = XamlParsingUtilities.FindElementByName(_unoXaml, "tb_geofence_radius");
        Assert.IsNotNull(tbGeofenceRadius, "tb_geofence_radius should exist");

        var tbGeofenceAltitude = XamlParsingUtilities.FindElementByName(_unoXaml, "tb_geofence_altitude");
        Assert.IsNotNull(tbGeofenceAltitude, "tb_geofence_altitude should exist");

        var tbGeofenceLat = XamlParsingUtilities.FindElementByName(_unoXaml, "tb_geofence_lat");
        Assert.IsNotNull(tbGeofenceLat, "tb_geofence_lat should exist");

        var tbGeofenceLon = XamlParsingUtilities.FindElementByName(_unoXaml, "tb_geofence_lon");
        Assert.IsNotNull(tbGeofenceLon, "tb_geofence_lon should exist");
    }

    [TestMethod]
    public void MapPage_GeofenceButtons_HaveEventHandlers()
    {
        // Arrange & Act
        var btnGeofenceToggle = XamlParsingUtilities.FindElementByName(_unoXaml, "btn_geofence_toggle");
        var btnSetCenter = XamlParsingUtilities.FindElementByName(_unoXaml, "btn_set_center");
        var btnGeofenceMode = XamlParsingUtilities.FindElementByName(_unoXaml, "btn_geofence_mode");
        var btnCompleteGeofence = XamlParsingUtilities.FindElementByName(_unoXaml, "btn_complete_geofence");
        var btnSendGeofence = XamlParsingUtilities.FindElementByName(_unoXaml, "btn_send_geofence");
        var btnClearGeofence = XamlParsingUtilities.FindElementByName(_unoXaml, "btn_clear_geofence");

        // Assert
        Assert.IsNotNull(btnGeofenceToggle, "btn_geofence_toggle should exist");
        Assert.IsNotNull(btnGeofenceToggle.Attribute("Click")?.Value, 
            "btn_geofence_toggle should have Click handler");

        Assert.IsNotNull(btnSetCenter, "btn_set_center should exist");
        Assert.IsNotNull(btnSetCenter.Attribute("Click")?.Value, 
            "btn_set_center should have Click handler");

        Assert.IsNotNull(btnGeofenceMode, "btn_geofence_mode should exist");
        Assert.IsNotNull(btnGeofenceMode.Attribute("Click")?.Value, 
            "btn_geofence_mode should have Click handler");

        Assert.IsNotNull(btnCompleteGeofence, "btn_complete_geofence should exist");
        Assert.IsNotNull(btnCompleteGeofence.Attribute("Click")?.Value, 
            "btn_complete_geofence should have Click handler");

        Assert.IsNotNull(btnSendGeofence, "btn_send_geofence should exist");
        Assert.IsNotNull(btnSendGeofence.Attribute("Click")?.Value, 
            "btn_send_geofence should have Click handler");

        Assert.IsNotNull(btnClearGeofence, "btn_clear_geofence should exist");
        Assert.IsNotNull(btnClearGeofence.Attribute("Click")?.Value, 
            "btn_clear_geofence should have Click handler");
    }

    [TestMethod]
    public void MapPage_WaypointButtons_HaveEventHandlers()
    {
        // Arrange & Act
        var wpDockBtn = XamlParsingUtilities.FindElementByName(_unoXaml, "wp_dock_btn");
        var resetMark = XamlParsingUtilities.FindElementByName(_unoXaml, "reset_mark");
        var undoBtn = XamlParsingUtilities.FindElementByName(_unoXaml, "Undo");
        var redoBtn = XamlParsingUtilities.FindElementByName(_unoXaml, "Redo");
        var importBtn = XamlParsingUtilities.FindElementByName(_unoXaml, "Import");
        var exportBtn = XamlParsingUtilities.FindElementByName(_unoXaml, "Export");

        // Assert
        Assert.IsNotNull(wpDockBtn, "wp_dock_btn should exist");
        Assert.IsNotNull(wpDockBtn.Attribute("Click")?.Value, "wp_dock_btn should have Click handler");

        Assert.IsNotNull(resetMark, "reset_mark should exist");
        Assert.IsNotNull(resetMark.Attribute("Click")?.Value, "reset_mark should have Click handler");

        Assert.IsNotNull(undoBtn, "Undo button should exist");
        Assert.IsNotNull(undoBtn.Attribute("Click")?.Value, "Undo button should have Click handler");

        Assert.IsNotNull(redoBtn, "Redo button should exist");
        Assert.IsNotNull(redoBtn.Attribute("Click")?.Value, "Redo button should have Click handler");

        Assert.IsNotNull(importBtn, "Import button should exist");
        Assert.IsNotNull(importBtn.Attribute("Click")?.Value, "Import button should have Click handler");

        Assert.IsNotNull(exportBtn, "Export button should exist");
        Assert.IsNotNull(exportBtn.Attribute("Click")?.Value, "Export button should have Click handler");
    }

    [TestMethod]
    public void MapPage_MapTypeComboBox_HasCorrectItems()
    {
        // Arrange & Act
        var cbMapType = XamlParsingUtilities.FindElementByName(_unoXaml, "cb_map_type");

        // Assert
        Assert.IsNotNull(cbMapType, "cb_map_type should exist");
        Assert.AreEqual("ComboBox", cbMapType.Name.LocalName, "cb_map_type should be a ComboBox");

        // Verify it has SelectionChanged handler
        Assert.IsNotNull(cbMapType.Attribute("SelectionChanged")?.Value, 
            "cb_map_type should have SelectionChanged handler");

        // Verify ComboBoxItems exist
        var items = cbMapType.Descendants()
            .Where(e => e.Name.LocalName == "ComboBoxItem")
            .ToList();
        Assert.IsTrue(items.Count >= 5, "cb_map_type should have at least 5 map type options");

        // Verify specific map types
        var itemContents = items.Select(i => i.Attribute("Content")?.Value).ToList();
        Assert.IsTrue(itemContents.Any(c => c?.Contains("Satellite") == true), 
            "Should have Satellite map option");
        Assert.IsTrue(itemContents.Any(c => c?.Contains("Terrain") == true || c?.Contains("Topografi") == true), 
            "Should have Terrain/Topografi map option");
    }

    [TestMethod]
    public void MapPage_StatusBar_HasCorrectControls()
    {
        // Arrange & Act
        var tbRadius = XamlParsingUtilities.FindElementByName(_unoXaml, "tb_radius");
        var tbTotalDistance = XamlParsingUtilities.FindElementByName(_unoXaml, "tb_total_distance");

        // Assert
        Assert.IsNotNull(tbRadius, "tb_radius should exist");
        Assert.AreEqual("TextBox", tbRadius.Name.LocalName, "tb_radius should be a TextBox");

        Assert.IsNotNull(tbTotalDistance, "tb_total_distance should exist");
        Assert.AreEqual("TextBlock", tbTotalDistance.Name.LocalName, "tb_total_distance should be a TextBlock");

        // Verify default values
        Assert.IsNotNull(tbRadius.Attribute("Text")?.Value, "tb_radius should have default Text value");
        Assert.IsNotNull(tbTotalDistance.Attribute("Text")?.Value, 
            "tb_total_distance should have default Text value");
    }

    [TestMethod]
    public void MapPage_FollowVehicleButton_HasCorrectStructure()
    {
        // Arrange & Act
        var followWahanaBorder = XamlParsingUtilities.FindElementByName(_unoXaml, "follow_wahana_border");

        // Assert
        Assert.IsNotNull(followWahanaBorder, "follow_wahana_border should exist");
        Assert.AreEqual("Border", followWahanaBorder.Name.LocalName, 
            "follow_wahana_border should be a Border");

        // Verify styling
        Assert.IsNotNull(followWahanaBorder.Attribute("Background")?.Value, 
            "follow_wahana_border should have Background");
        Assert.IsNotNull(followWahanaBorder.Attribute("CornerRadius")?.Value, 
            "follow_wahana_border should have CornerRadius");

        // Verify it contains a Button
        var button = followWahanaBorder.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Button");
        Assert.IsNotNull(button, "follow_wahana_border should contain a Button");
        Assert.IsNotNull(button.Attribute("Click")?.Value, "Button should have Click handler");

        // Verify button contains an Image
        var image = button.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Image");
        Assert.IsNotNull(image, "Button should contain an Image");
        Assert.IsNotNull(image.Attribute("Source")?.Value, "Image should have Source");
    }

    [TestMethod]
    public void MapPage_AllStaticResources_AreValid()
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

        // Verify specific resources exist
        var resourceKeys = resources.Select(r => r.ResourceKey).ToList();
        Assert.IsTrue(resourceKeys.Contains("CustomButton"), "CustomButton style should be referenced");
        Assert.IsTrue(resourceKeys.Contains("RalewayFont"), "RalewayFont should be referenced");
    }

    [TestMethod]
    public void MapPage_Title_HasCorrectProperties()
    {
        // Arrange & Act
        var judulMap = XamlParsingUtilities.FindElementByName(_unoXaml, "judul_map");

        // Assert
        Assert.IsNotNull(judulMap, "judul_map should exist");
        Assert.AreEqual("TextBlock", judulMap.Name.LocalName, "judul_map should be a TextBlock");

        // Verify text content
        Assert.AreEqual("Map View", judulMap.Attribute("Text")?.Value, 
            "judul_map should have 'Map View' text");

        // Verify styling
        Assert.IsNotNull(judulMap.Attribute("FontSize")?.Value, "judul_map should have FontSize");
        Assert.IsNotNull(judulMap.Attribute("FontFamily")?.Value, "judul_map should have FontFamily");
        Assert.IsNotNull(judulMap.Attribute("FontWeight")?.Value, "judul_map should have FontWeight");
        Assert.IsNotNull(judulMap.Attribute("Foreground")?.Value, "judul_map should have Foreground");
    }

    [TestMethod]
    public void MapPage_NoWpfSpecificControls_Present()
    {
        // Arrange
        var unoXaml = LoadUnoXaml(UnoPath);

        // Act & Assert
        // Verify no GMap.NET control
        Assert.IsFalse(unoXaml.Contains("GMapControl"), 
            "MapPage should not contain GMapControl (WPF-specific)");
        Assert.IsFalse(unoXaml.Contains("gmaps:"), 
            "MapPage should not contain gmaps namespace (WPF-specific)");

        // Verify no DockPanel (not well supported in Uno)
        var dockPanels = _unoXaml.Descendants()
            .Where(e => e.Name.LocalName == "DockPanel")
            .ToList();
        Assert.AreEqual(0, dockPanels.Count, 
            "MapPage should not use DockPanel (use StackPanel or Grid instead)");
    }
}
