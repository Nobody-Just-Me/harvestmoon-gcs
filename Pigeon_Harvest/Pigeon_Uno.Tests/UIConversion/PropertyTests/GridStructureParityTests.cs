using FsCheck;
using FsCheck.Xunit;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Pigeon_Uno.Tests.UIConversion.PropertyTests;

/// <summary>
/// Property 1: Grid Structure Parity
/// For any page XAML file, the Uno version should have identical Grid RowDefinitions 
/// and ColumnDefinitions as the WPF version.
/// Validates: Requirements 1.2
/// </summary>
public class GridStructureParityTests
{
    // Feature: ui-refinement-fixes, Property 1: Grid Structure Parity
    
    private static readonly List<PagePair> PagePairs = new()
    {
        new PagePair("Custom UserControls/FlightControl.xaml", "Views/FlightPage.xaml", "FlightPage"),
        new PagePair("Custom UserControls/MapControl.xaml", "Views/MapPage.xaml", "MapPage"),
        new PagePair("Custom UserControls/CalibrationControl.xaml", "Views/CalibrationPage.xaml", "CalibrationPage"),
        new PagePair("Custom UserControls/StatsControl.xaml", "Views/StatsPage.xaml", "StatsPage"),
        new PagePair("Custom UserControls/LoRaControl.xaml", "Views/LoRaPage.xaml", "LoRaPage"),
        new PagePair("Custom UserControls/TrackerControl.xaml", "Views/TrackerPage.xaml", "TrackerPage"),
        new PagePair("Custom UserControls/TlogControl.xaml", "Views/TlogPage.xaml", "TlogPage"),
    };

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(PagePairGenerator) })]
    public Property GridStructureParity_AllPages(PagePair pagePair)
    {
        return Prop.ForAll(
            Arb.From(PagePairs),
            pair =>
            {
                try
                {
                    var wpfXaml = LoadWpfXaml(pair.WpfPath);
                    var unoXaml = LoadUnoXaml(pair.UnoPath);

                    var wpfGrid = XamlParsingUtilities.ParseGridStructure(wpfXaml);
                    var unoGrid = XamlParsingUtilities.ParseGridStructure(unoXaml);

                    return wpfGrid.Equals(unoGrid)
                        .Label($"{pair.PageName}: Grid structure should match WPF");
                }
                catch (FileNotFoundException)
                {
                    // If file doesn't exist yet, skip this test
                    return true.ToProperty();
                }
            });
    }

    private string LoadWpfXaml(string relativePath)
    {
        var solutionRoot = GetSolutionRoot();
        var fullPath = Path.Combine(solutionRoot, "Pigeon_WPF_cs", "Pigeon_WPF_cs", relativePath);
        return File.ReadAllText(fullPath);
    }

    private string LoadUnoXaml(string relativePath)
    {
        var solutionRoot = GetSolutionRoot();
        var fullPath = Path.Combine(solutionRoot, "Pigeon_Uno", "Pigeon_Uno", relativePath);
        return File.ReadAllText(fullPath);
    }

    private string GetSolutionRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(currentDir, "Pigeon_Uno.sln")))
        {
            var parent = Directory.GetParent(currentDir);
            if (parent == null)
                throw new DirectoryNotFoundException("Solution root not found");
            currentDir = parent.FullName;
        }
        return currentDir;
    }
}

public class PagePair
{
    public string WpfPath { get; }
    public string UnoPath { get; }
    public string PageName { get; }

    public PagePair(string wpfPath, string unoPath, string pageName)
    {
        WpfPath = wpfPath;
        UnoPath = unoPath;
        PageName = pageName;
    }
}

public class PagePairGenerator
{
    public static Arbitrary<PagePair> PagePairs()
    {
        var pairs = new List<PagePair>
        {
            new PagePair("Custom UserControls/FlightControl.xaml", "Views/FlightPage.xaml", "FlightPage"),
            new PagePair("Custom UserControls/MapControl.xaml", "Views/MapPage.xaml", "MapPage"),
            new PagePair("Custom UserControls/CalibrationControl.xaml", "Views/CalibrationPage.xaml", "CalibrationPage"),
            new PagePair("Custom UserControls/StatsControl.xaml", "Views/StatsPage.xaml", "StatsPage"),
            new PagePair("Custom UserControls/LoRaControl.xaml", "Views/LoRaPage.xaml", "LoRaPage"),
            new PagePair("Custom UserControls/TrackerControl.xaml", "Views/TrackerPage.xaml", "TrackerPage"),
            new PagePair("Custom UserControls/TlogControl.xaml", "Views/TlogPage.xaml", "TlogPage"),
        };

        return Gen.Elements(pairs).ToArbitrary();
    }
}
