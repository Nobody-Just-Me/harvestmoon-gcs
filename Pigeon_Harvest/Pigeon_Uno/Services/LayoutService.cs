using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Pigeon_Uno.Core.Services;
using Serilog;

namespace Pigeon_Uno.Services;

/// <summary>
/// Implementation of layout management service
/// </summary>
public class LayoutService : ILayoutService
{
    private readonly ISettingsService _settingsService;
    private readonly string _layoutsFolder;
    private const string DefaultLayoutName = "Default";

    public LayoutService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        
        // Get layouts folder path
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _layoutsFolder = Path.Combine(appDataPath, "Pigeon_Uno", "Layouts");
        
        // Ensure folder exists
        Directory.CreateDirectory(_layoutsFolder);
    }

    public async Task SaveLayoutAsync(string layoutName, LayoutConfiguration layout)
    {
        try
        {
            layout.Name = layoutName;
            layout.LastModified = DateTime.Now;

            var filePath = GetLayoutFilePath(layoutName);
            var json = JsonSerializer.Serialize(layout, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(filePath, json);
            
            // Save current layout name
            await _settingsService.SetSettingAsync("CurrentLayout", layoutName);
            
            Log.Information("Layout saved: {LayoutName}", layoutName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save layout: {LayoutName}", layoutName);
            throw;
        }
    }

    public async Task<LayoutConfiguration?> LoadLayoutAsync(string layoutName)
    {
        try
        {
            var filePath = GetLayoutFilePath(layoutName);
            
            if (!File.Exists(filePath))
            {
                Log.Warning("Layout file not found: {LayoutName}", layoutName);
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var layout = JsonSerializer.Deserialize<LayoutConfiguration>(json);
            
            Log.Information("Layout loaded: {LayoutName}", layoutName);
            return layout;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load layout: {LayoutName}", layoutName);
            return null;
        }
    }

    public async Task<List<string>> GetSavedLayoutsAsync()
    {
        try
        {
            var files = Directory.GetFiles(_layoutsFolder, "*.json");
            var layouts = files.Select(f => Path.GetFileNameWithoutExtension(f)).ToList();
            
            return await Task.FromResult(layouts);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to get saved layouts");
            return new List<string>();
        }
    }

    public async Task DeleteLayoutAsync(string layoutName)
    {
        try
        {
            if (layoutName == DefaultLayoutName)
            {
                Log.Warning("Cannot delete default layout");
                return;
            }

            var filePath = GetLayoutFilePath(layoutName);
            
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                Log.Information("Layout deleted: {LayoutName}", layoutName);
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to delete layout: {LayoutName}", layoutName);
            throw;
        }
    }

    public async Task<string> ExportLayoutAsync(string layoutName)
    {
        try
        {
            var layout = await LoadLayoutAsync(layoutName);
            if (layout == null)
            {
                throw new FileNotFoundException($"Layout not found: {layoutName}");
            }

            var exportPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                $"Pigeon_Layout_{layoutName}_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            );

            var json = JsonSerializer.Serialize(layout, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(exportPath, json);
            
            Log.Information("Layout exported: {LayoutName} to {ExportPath}", layoutName, exportPath);
            return exportPath;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to export layout: {LayoutName}", layoutName);
            throw;
        }
    }

    public async Task<bool> ImportLayoutAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Log.Warning("Import file not found: {FilePath}", filePath);
                return false;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var layout = JsonSerializer.Deserialize<LayoutConfiguration>(json);

            if (layout == null)
            {
                Log.Warning("Failed to deserialize layout from: {FilePath}", filePath);
                return false;
            }

            // Save with imported name or generate new name
            var layoutName = layout.Name;
            if (string.IsNullOrEmpty(layoutName))
            {
                layoutName = $"Imported_{DateTime.Now:yyyyMMdd_HHmmss}";
            }

            await SaveLayoutAsync(layoutName, layout);
            
            Log.Information("Layout imported: {LayoutName} from {FilePath}", layoutName, filePath);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to import layout from: {FilePath}", filePath);
            return false;
        }
    }

    public async Task ResetLayoutAsync()
    {
        try
        {
            // Create default layout
            var defaultLayout = CreateDefaultLayout();
            await SaveLayoutAsync(DefaultLayoutName, defaultLayout);
            
            Log.Information("Layout reset to default");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to reset layout");
            throw;
        }
    }

    private string GetLayoutFilePath(string layoutName)
    {
        return Path.Combine(_layoutsFolder, $"{layoutName}.json");
    }

    private LayoutConfiguration CreateDefaultLayout()
    {
        return new LayoutConfiguration
        {
            Name = DefaultLayoutName,
            Panels = new Dictionary<string, PanelConfiguration>
            {
                ["LeftPanel"] = new PanelConfiguration
                {
                    PanelId = "LeftPanel",
                    Width = 80,
                    IsVisible = true,
                    Position = 0
                },
                ["MiddlePanel"] = new PanelConfiguration
                {
                    PanelId = "MiddlePanel",
                    Width = 270,
                    IsVisible = true,
                    Position = 1
                },
                ["RightPanel"] = new PanelConfiguration
                {
                    PanelId = "RightPanel",
                    Width = double.NaN, // Fill remaining space
                    IsVisible = true,
                    Position = 2
                }
            }
        };
    }
}
