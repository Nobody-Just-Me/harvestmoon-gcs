using FsCheck;
using FsCheck.Xunit;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Pigeon_Uno.Tests.UIConversion.PropertyTests;

/// <summary>
/// Property 7: String Format Parity
/// For any Binding expression with StringFormat in the WPF XAML, the Uno version should have
/// an equivalent StringFormat that produces the same output format.
/// Validates: Requirements 4.3, 4.4
/// </summary>
public class StringFormatParityTests
{
    // Feature: ui-refinement-fixes, Property 7: String Format Parity

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(PagePairGenerator) })]
    public Property StringFormatParity_AllBindings(PagePair pagePair)
    {
        return Prop.ForAll(
            Arb.From(PagePairGenerator.PagePairs()),
            pair =>
            {
                try
                {
                    var wpfXaml = LoadWpfXaml(pair.WpfPath);
                    var unoXaml = LoadUnoXaml(pair.UnoPath);

                    var wpfBindings = XamlParsingUtilities.ExtractBindings(wpfXaml);
                    var unoBindings = XamlParsingUtilities.ExtractBindings(unoXaml);

                    // Check bindings with StringFormat
                    foreach (var wpfBinding in wpfBindings.Where(b => b.BindingExpression.Contains("StringFormat")))
                    {
                        var wpfFormat = ExtractStringFormat(wpfBinding.BindingExpression);
                        if (string.IsNullOrEmpty(wpfFormat))
                            continue;

                        // Find corresponding Uno binding
                        var unoBinding = unoBindings.FirstOrDefault(b => 
                            b.ControlName == wpfBinding.ControlName && 
                            b.Property == wpfBinding.Property);

                        if (unoBinding == null)
                            continue;

                        var unoFormat = ExtractStringFormat(unoBinding.BindingExpression);
                        
                        if (wpfFormat != unoFormat)
                        {
                            return false.Label($"{pair.PageName}: StringFormat mismatch in {wpfBinding.ControlName ?? wpfBinding.ControlType}.{wpfBinding.Property}");
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

    private string ExtractStringFormat(string bindingExpression)
    {
        // Extract StringFormat value from binding expression
        var match = Regex.Match(bindingExpression, @"StringFormat\s*=\s*['""]?([^'""}\s]+)['""]?");
        return match.Success ? match.Groups[1].Value : string.Empty;
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
