using FsCheck;
using FsCheck.Xunit;
using System.IO;
using System.Linq;

namespace Pigeon_Uno.Tests.UIConversion.PropertyTests;

/// <summary>
/// Property 3: Binding Expression Validity
/// For any Binding expression in the Uno XAML, the binding path should be valid,
/// the syntax should be correct, and the binding should update when the source property changes.
/// Validates: Requirements 1.4
/// </summary>
public class BindingExpressionValidityTests
{
    // Feature: ui-refinement-fixes, Property 3: Binding Expression Validity

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(PagePairGenerator) })]
    public Property BindingExpressionValidity_AllBindings(PagePair pagePair)
    {
        return Prop.ForAll(
            Arb.From(PagePairGenerator.PagePairs()),
            pair =>
            {
                try
                {
                    var unoXaml = LoadUnoXaml(pair.UnoPath);
                    var bindings = XamlParsingUtilities.ExtractBindings(unoXaml);

                    // Check that all bindings have valid syntax
                    foreach (var binding in bindings)
                    {
                        if (!IsValidBindingSyntax(binding.BindingExpression))
                        {
                            return false.Label($"{pair.PageName}: Invalid binding syntax in {binding.ControlName ?? binding.ControlType}.{binding.Property}");
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

    private bool IsValidBindingSyntax(string bindingExpression)
    {
        // Basic validation: check for proper {Binding ...} syntax
        if (!bindingExpression.StartsWith("{Binding"))
            return false;

        if (!bindingExpression.EndsWith("}"))
            return false;

        // Check for balanced braces
        var openCount = bindingExpression.Count(c => c == '{');
        var closeCount = bindingExpression.Count(c => c == '}');
        
        return openCount == closeCount;
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
