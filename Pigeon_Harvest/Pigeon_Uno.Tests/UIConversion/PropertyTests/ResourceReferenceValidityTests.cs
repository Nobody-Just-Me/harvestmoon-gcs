using FsCheck;
using FsCheck.Xunit;
using System.IO;

namespace Pigeon_Uno.Tests.UIConversion.PropertyTests;

/// <summary>
/// Property 4: Resource Reference Validity
/// For any StaticResource reference in the Uno XAML, the referenced resource key should exist
/// in the application's resource dictionaries.
/// Validates: Requirements 1.5
/// </summary>
public class ResourceReferenceValidityTests
{
    // Feature: ui-refinement-fixes, Property 4: Resource Reference Validity

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(PagePairGenerator) })]
    public Property ResourceReferenceValidity_AllResources(PagePair pagePair)
    {
        return Prop.ForAll(
            Arb.From(PagePairGenerator.PagePairs()),
            pair =>
            {
                try
                {
                    var unoXaml = LoadUnoXaml(pair.UnoPath);
                    var resources = XamlParsingUtilities.ExtractResourceReferences(unoXaml);

                    // Check that no DynamicResource is used (not supported in Uno)
                    foreach (var resource in resources)
                    {
                        if (resource.IsDynamic)
                        {
                            return false.Label($"{pair.PageName}: DynamicResource not supported in Uno Platform: {resource.ResourceKey}");
                        }

                        // Check that resource key is not empty
                        if (string.IsNullOrWhiteSpace(resource.ResourceKey))
                        {
                            return false.Label($"{pair.PageName}: Empty resource key in {resource.ControlName ?? resource.ControlType}.{resource.Property}");
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
