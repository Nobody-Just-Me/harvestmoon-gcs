using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HarvestmoonGCS.Core.Services;

/// <summary>
/// Service for managing UI layout and panel configurations
/// </summary>
public interface ILayoutService
{
    /// <summary>
    /// Saves the current layout configuration
    /// </summary>
    Task SaveLayoutAsync(string layoutName, LayoutConfiguration layout);

    /// <summary>
    /// Loads a saved layout configuration
    /// </summary>
    Task<LayoutConfiguration?> LoadLayoutAsync(string layoutName);

    /// <summary>
    /// Gets all saved layout names
    /// </summary>
    Task<List<string>> GetSavedLayoutsAsync();

    /// <summary>
    /// Deletes a saved layout
    /// </summary>
    Task DeleteLayoutAsync(string layoutName);

    /// <summary>
    /// Exports layout to file
    /// </summary>
    Task<string> ExportLayoutAsync(string layoutName);

    /// <summary>
    /// Imports layout from file
    /// </summary>
    Task<bool> ImportLayoutAsync(string filePath);

    /// <summary>
    /// Resets layout to default
    /// </summary>
    Task ResetLayoutAsync();
}

/// <summary>
/// Layout configuration data
/// </summary>
public class LayoutConfiguration
{
    public string Name { get; set; } = "Default";
    public Dictionary<string, PanelConfiguration> Panels { get; set; } = new();
    public DateTime LastModified { get; set; } = DateTime.Now;
}

/// <summary>
/// Panel configuration data
/// </summary>
public class PanelConfiguration
{
    public string PanelId { get; set; } = string.Empty;
    public double Width { get; set; }
    public double Height { get; set; }
    public bool IsVisible { get; set; } = true;
    public int Position { get; set; }
    public Dictionary<string, object> CustomProperties { get; set; } = new();
}
