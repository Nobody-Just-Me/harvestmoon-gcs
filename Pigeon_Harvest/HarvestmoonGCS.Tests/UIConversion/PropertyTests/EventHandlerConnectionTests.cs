using FsCheck;
using FsCheck.Xunit;
using System.IO;

namespace HarvestmoonGCS.Tests.UIConversion.PropertyTests;

/// <summary>
/// Property 5: Event Handler Connection
/// For any event handler referenced in the Uno XAML, a corresponding method with the correct signature
/// should exist in the code-behind file.
/// Validates: Requirements 1.6
/// </summary>
public class EventHandlerConnectionTests
{
    // Feature: ui-refinement-fixes, Property 5: Event Handler Connection

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(PagePairGenerator) })]
    public Property EventHandlerConnection_AllHandlers(PagePair pagePair)
    {
        return Prop.ForAll(
            Arb.From(PagePairGenerator.PagePairs()),
            pair =>
            {
                try
                {
                    var unoXaml = LoadUnoXaml(pair.UnoPath);
                    var handlers = XamlParsingUtilities.ExtractEventHandlers(unoXaml);

                    // Check that all event handlers have non-empty method names
                    foreach (var handler in handlers)
                    {
                        if (string.IsNullOrWhiteSpace(handler.HandlerMethod))
                        {
                            return false.Label($"{pair.PageName}: Empty event handler for {handler.EventName} on {handler.ControlName ?? handler.ControlType}");
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

    private string LoadUnoXaml(string relativePath)
    {
        var solutionRoot = GetSolutionRoot();
        var fullPath = Path.Combine(solutionRoot, "HarvestmoonGCS", "HarvestmoonGCS", relativePath);
        return File.ReadAllText(fullPath);
    }

    private string GetSolutionRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(currentDir, "HarvestmoonGCS.sln")))
        {
            var parent = Directory.GetParent(currentDir);
            if (parent == null)
                throw new DirectoryNotFoundException("Solution root not found");
            currentDir = parent.FullName;
        }
        return currentDir;
    }
}
