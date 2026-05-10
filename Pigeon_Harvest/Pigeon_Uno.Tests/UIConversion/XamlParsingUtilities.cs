using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Pigeon_Uno.Tests.UIConversion;

/// <summary>
/// Utilities for parsing and comparing XAML files between WPF and Uno Platform
/// </summary>
public static class XamlParsingUtilities
{
    /// <summary>
    /// Load XAML document from file path
    /// </summary>
    public static XDocument LoadXamlDocument(string relativePath)
    {
        // Navigate up from test project to solution root
        var testProjectDir = AppDomain.CurrentDomain.BaseDirectory;
        var solutionRoot = Path.GetFullPath(Path.Combine(testProjectDir, "..", "..", "..", ".."));
        var fullPath = Path.Combine(solutionRoot, relativePath);
        
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"XAML file not found: {fullPath}");
        }

        return XDocument.Load(fullPath);
    }

    /// <summary>
    /// Find an element by its x:Name attribute
    /// </summary>
    public static XElement? FindElementByName(XDocument doc, string name)
    {
        var xNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
        return doc.Descendants()
            .FirstOrDefault(e => 
                e.Attribute("Name")?.Value == name ||
                e.Attribute(XName.Get("Name", xNamespace))?.Value == name);
    }
    /// <summary>
    /// Parse Grid structure from XAML content
    /// </summary>
    public static GridStructure ParseGridStructure(string xamlContent)
    {
        var doc = XDocument.Parse(xamlContent);
        var grid = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Grid");
        
        if (grid == null)
            return new GridStructure();

        var rowDefs = grid.Elements()
            .Where(e => e.Name.LocalName == "Grid.RowDefinitions")
            .SelectMany(e => e.Elements())
            .Select(e => e.Attribute("Height")?.Value ?? "Auto")
            .ToList();

        var colDefs = grid.Elements()
            .Where(e => e.Name.LocalName == "Grid.ColumnDefinitions")
            .SelectMany(e => e.Elements())
            .Select(e => e.Attribute("Width")?.Value ?? "Auto")
            .ToList();

        return new GridStructure
        {
            RowCount = rowDefs.Count,
            ColumnCount = colDefs.Count,
            RowHeights = rowDefs,
            ColumnWidths = colDefs
        };
    }

    /// <summary>
    /// Extract all controls from XAML content
    /// </summary>
    public static List<ControlInfo> ExtractControls(string xamlContent)
    {
        var doc = XDocument.Parse(xamlContent);
        var controls = new List<ControlInfo>();

        foreach (var element in doc.Descendants())
        {
            var localName = element.Name.LocalName;
            
            // Skip container elements and definitions
            if (IsContainerOrDefinition(localName))
                continue;

            var control = new ControlInfo
            {
                Type = localName,
                Name = element.Attribute("Name")?.Value ?? element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value,
                Properties = ExtractProperties(element)
            };

            controls.Add(control);
        }

        return controls;
    }

    /// <summary>
    /// Extract all binding expressions from XAML content
    /// </summary>
    public static List<BindingInfo> ExtractBindings(string xamlContent)
    {
        var doc = XDocument.Parse(xamlContent);
        var bindings = new List<BindingInfo>();

        foreach (var element in doc.Descendants())
        {
            foreach (var attr in element.Attributes())
            {
                var value = attr.Value;
                if (value.Contains("{Binding"))
                {
                    bindings.Add(new BindingInfo
                    {
                        ControlType = element.Name.LocalName,
                        ControlName = element.Attribute("Name")?.Value,
                        Property = attr.Name.LocalName,
                        BindingExpression = value
                    });
                }
            }
        }

        return bindings;
    }

    /// <summary>
    /// Extract all StaticResource references from XAML content
    /// </summary>
    public static List<ResourceReference> ExtractResourceReferences(string xamlContent)
    {
        var doc = XDocument.Parse(xamlContent);
        var resources = new List<ResourceReference>();

        foreach (var element in doc.Descendants())
        {
            foreach (var attr in element.Attributes())
            {
                var value = attr.Value;
                if (value.Contains("{StaticResource") || value.Contains("{DynamicResource"))
                {
                    var resourceKey = ExtractResourceKey(value);
                    resources.Add(new ResourceReference
                    {
                        ControlType = element.Name.LocalName,
                        ControlName = element.Attribute("Name")?.Value,
                        Property = attr.Name.LocalName,
                        ResourceKey = resourceKey,
                        IsDynamic = value.Contains("{DynamicResource")
                    });
                }
            }
        }

        return resources;
    }

    /// <summary>
    /// Extract all event handlers from XAML content
    /// </summary>
    public static List<EventHandlerInfo> ExtractEventHandlers(string xamlContent)
    {
        var doc = XDocument.Parse(xamlContent);
        var handlers = new List<EventHandlerInfo>();

        var eventAttributes = new[] { "Click", "Loaded", "SelectionChanged", "TextChanged", "Checked", "Unchecked" };

        foreach (var element in doc.Descendants())
        {
            foreach (var attr in element.Attributes())
            {
                if (eventAttributes.Contains(attr.Name.LocalName))
                {
                    handlers.Add(new EventHandlerInfo
                    {
                        ControlType = element.Name.LocalName,
                        ControlName = element.Attribute("Name")?.Value,
                        EventName = attr.Name.LocalName,
                        HandlerMethod = attr.Value
                    });
                }
            }
        }

        return handlers;
    }

    private static Dictionary<string, string> ExtractProperties(XElement element)
    {
        var properties = new Dictionary<string, string>();

        var propertyNames = new[] 
        { 
            "Width", "Height", "Margin", "Padding", 
            "HorizontalAlignment", "VerticalAlignment",
            "Background", "Foreground", "BorderBrush", "BorderThickness",
            "FontFamily", "FontSize", "FontWeight", "CornerRadius"
        };

        foreach (var propName in propertyNames)
        {
            var value = element.Attribute(propName)?.Value;
            if (value != null)
            {
                properties[propName] = value;
            }
        }

        return properties;
    }

    private static bool IsContainerOrDefinition(string localName)
    {
        var skipElements = new[]
        {
            "Grid.RowDefinitions", "Grid.ColumnDefinitions",
            "RowDefinition", "ColumnDefinition",
            "Page", "UserControl", "Window"
        };

        return skipElements.Contains(localName);
    }

    private static string ExtractResourceKey(string resourceExpression)
    {
        // Extract key from {StaticResource Key} or {DynamicResource Key}
        var start = resourceExpression.IndexOf(' ');
        var end = resourceExpression.IndexOf('}');
        
        if (start > 0 && end > start)
        {
            return resourceExpression.Substring(start + 1, end - start - 1).Trim();
        }

        return string.Empty;
    }
}

/// <summary>
/// Represents Grid structure information
/// </summary>
public class GridStructure
{
    public int RowCount { get; set; }
    public int ColumnCount { get; set; }
    public List<string> RowHeights { get; set; } = new();
    public List<string> ColumnWidths { get; set; } = new();

    public override bool Equals(object? obj)
    {
        if (obj is not GridStructure other)
            return false;

        return RowCount == other.RowCount &&
               ColumnCount == other.ColumnCount &&
               RowHeights.SequenceEqual(other.RowHeights) &&
               ColumnWidths.SequenceEqual(other.ColumnWidths);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(RowCount, ColumnCount);
    }
}

/// <summary>
/// Represents control information
/// </summary>
public class ControlInfo
{
    public string Type { get; set; } = string.Empty;
    public string? Name { get; set; }
    public Dictionary<string, string> Properties { get; set; } = new();
}

/// <summary>
/// Represents binding information
/// </summary>
public class BindingInfo
{
    public string ControlType { get; set; } = string.Empty;
    public string? ControlName { get; set; }
    public string Property { get; set; } = string.Empty;
    public string BindingExpression { get; set; } = string.Empty;
}

/// <summary>
/// Represents resource reference information
/// </summary>
public class ResourceReference
{
    public string ControlType { get; set; } = string.Empty;
    public string? ControlName { get; set; }
    public string Property { get; set; } = string.Empty;
    public string ResourceKey { get; set; } = string.Empty;
    public bool IsDynamic { get; set; }
}

/// <summary>
/// Represents event handler information
/// </summary>
public class EventHandlerInfo
{
    public string ControlType { get; set; } = string.Empty;
    public string? ControlName { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string HandlerMethod { get; set; } = string.Empty;
}
