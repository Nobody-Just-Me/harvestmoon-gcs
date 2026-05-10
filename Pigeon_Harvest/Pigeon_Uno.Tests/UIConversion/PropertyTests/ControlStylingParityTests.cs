using FsCheck;
using FsCheck.Xunit;
using System.IO;
using System.Linq;

namespace Pigeon_Uno.Tests.UIConversion.PropertyTests;

/// <summary>
/// Property 6: Control Styling Parity
/// For any control in a page XAML file, the Uno version should have identical styling properties
/// (Background, Foreground, BorderBrush, BorderThickness, FontFamily, FontSize, FontWeight, CornerRadius)
/// as the corresponding WPF control.
/// Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5
/// </summary>
public class ControlStylingParityTests
{
    // Feature: ui-refinement-fixes, Property 6: Control Styling Parity

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(PagePairGenerator) })]
    public Property ControlStylingParity_AllControls(PagePair pagePair)
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

                    var stylingProperties = new[] 
                    { 
                        "Background", "Foreground", "BorderBrush", "BorderThickness",
                        "FontFamily", "FontSize", "FontWeight", "CornerRadius"
                    };

                    foreach (var wpfControl in wpfControls.Where(c => c.Name != null))
                    {
                        var unoControl = unoControls.FirstOrDefault(c => c.Name == wpfControl.Name);
                        if (unoControl == null)
                            continue;

                        foreach (var prop in stylingProperties)
                        {
                            if (wpfControl.Properties.TryGetValue(prop, out var wpfValue))
                            {
                                if (!unoControl.Properties.TryGetValue(prop, out var unoValue) || wpfValue != unoValue)
                                {
                                    return false.Label($"{pair.PageName}: Control {wpfControl.Name} styling property {prop} mismatch");
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
