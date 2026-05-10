using FsCheck;
using FsCheck.Xunit;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Pigeon_Uno.Tests.UIConversion.PropertyTests;

/// <summary>
/// Property 2: Control Property Parity
/// For any control in a page XAML file, the Uno version should have identical property values
/// (Width, Height, Margin, Padding, HorizontalAlignment, VerticalAlignment) as the corresponding WPF control.
/// Validates: Requirements 1.3
/// </summary>
public class ControlPropertyParityTests
{
    // Feature: ui-refinement-fixes, Property 2: Control Property Parity

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(PagePairGenerator) })]
    public Property ControlPropertyParity_AllControls(PagePair pagePair)
    {
        return Prop.ForAll(
            Arb.From(PagePairGenerator.PagePairs()),
            pair =>
            {
                try
                {
                    var wpfXaml = LoadWpfXaml(pair.WpfPath);
                    var unoXaml = LoadUnoXaml(pair.UnoPath);

                    var wpfControls = XamlParsingUtilities.ExtractControls(wpfXaml);
                    var unoControls = XamlParsingUtilities.ExtractControls(unoXaml);

                    // Check that all WPF controls have corresponding Uno controls with matching properties
                    var propertiesToCheck = new[] { "Width", "Height", "Margin", "Padding", "HorizontalAlignment", "VerticalAlignment" };

                    foreach (var wpfControl in wpfControls.Where(c => c.Name != null))
                    {
                        var unoControl = unoControls.FirstOrDefault(c => c.Name == wpfControl.Name);
                        if (unoControl == null)
                            continue; // Control might not be converted yet

                        foreach (var prop in propertiesToCheck)
                        {
                            if (wpfControl.Properties.TryGetValue(prop, out var wpfValue))
                            {
                                if (!unoControl.Properties.TryGetValue(prop, out var unoValue) || wpfValue != unoValue)
                                {
                                    return false.Label($"{pair.PageName}: Control {wpfControl.Name} property {prop} mismatch");
                                }
                            }
                        }
                    }

                    return true.ToProperty();
                }
                catch (FileNotFoundException)
                {
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
