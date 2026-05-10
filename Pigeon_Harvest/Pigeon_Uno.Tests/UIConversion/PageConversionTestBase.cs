using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Pigeon_Uno.Tests.UIConversion;

/// <summary>
/// Base class for page conversion tests
/// </summary>
public abstract class PageConversionTestBase
{
    protected string WpfProjectPath { get; }
    protected string UnoProjectPath { get; }

    protected PageConversionTestBase()
    {
        // Navigate up from test project to solution root
        var testProjectDir = AppDomain.CurrentDomain.BaseDirectory;
        var solutionRoot = Path.GetFullPath(Path.Combine(testProjectDir, "..", "..", "..", ".."));
        
        WpfProjectPath = Path.Combine(solutionRoot, "Pigeon_WPF_cs", "Pigeon_WPF_cs");
        UnoProjectPath = Path.Combine(solutionRoot, "Pigeon_Uno", "Pigeon_Uno");
    }

    /// <summary>
    /// Load XAML content from WPF project
    /// </summary>
    protected string LoadWpfXaml(string relativePath)
    {
        var fullPath = Path.Combine(WpfProjectPath, relativePath);
        
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"WPF XAML file not found: {fullPath}");
        }

        return File.ReadAllText(fullPath);
    }

    /// <summary>
    /// Load XAML content from Uno project
    /// </summary>
    protected string LoadUnoXaml(string relativePath)
    {
        var fullPath = Path.Combine(UnoProjectPath, relativePath);
        
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Uno XAML file not found: {fullPath}");
        }

        return File.ReadAllText(fullPath);
    }

    /// <summary>
    /// Assert that two Grid structures are equal
    /// </summary>
    protected void AssertGridStructureEquals(GridStructure wpfGrid, GridStructure unoGrid, string pageName)
    {
        Assert.AreEqual(wpfGrid.RowCount, unoGrid.RowCount, 
            $"{pageName}: Row count mismatch");
        
        Assert.AreEqual(wpfGrid.ColumnCount, unoGrid.ColumnCount, 
            $"{pageName}: Column count mismatch");
        
        CollectionAssert.AreEqual(wpfGrid.RowHeights, unoGrid.RowHeights, 
            $"{pageName}: Row heights mismatch");
        
        CollectionAssert.AreEqual(wpfGrid.ColumnWidths, unoGrid.ColumnWidths, 
            $"{pageName}: Column widths mismatch");
    }

    /// <summary>
    /// Assert that control properties match
    /// </summary>
    protected void AssertControlPropertiesMatch(ControlInfo wpfControl, ControlInfo unoControl, string pageName)
    {
        Assert.AreEqual(wpfControl.Type, unoControl.Type, 
            $"{pageName}: Control type mismatch for {wpfControl.Name}");

        var propertiesToCheck = new[] 
        { 
            "Width", "Height", "Margin", "Padding", 
            "HorizontalAlignment", "VerticalAlignment" 
        };

        foreach (var prop in propertiesToCheck)
        {
            if (wpfControl.Properties.TryGetValue(prop, out var wpfValue))
            {
                Assert.IsTrue(unoControl.Properties.ContainsKey(prop), 
                    $"{pageName}: Property {prop} missing in Uno control {unoControl.Name}");
                
                Assert.AreEqual(wpfValue, unoControl.Properties[prop], 
                    $"{pageName}: Property {prop} value mismatch for {unoControl.Name}");
            }
        }
    }

    /// <summary>
    /// Assert that styling properties match
    /// </summary>
    protected void AssertStylingPropertiesMatch(ControlInfo wpfControl, ControlInfo unoControl, string pageName)
    {
        var stylingProperties = new[] 
        { 
            "Background", "Foreground", "BorderBrush", "BorderThickness",
            "FontFamily", "FontSize", "FontWeight", "CornerRadius"
        };

        foreach (var prop in stylingProperties)
        {
            if (wpfControl.Properties.TryGetValue(prop, out var wpfValue))
            {
                if (unoControl.Properties.TryGetValue(prop, out var unoValue))
                {
                    Assert.AreEqual(wpfValue, unoValue, 
                        $"{pageName}: Styling property {prop} mismatch for {unoControl.Name}");
                }
            }
        }
    }
}
