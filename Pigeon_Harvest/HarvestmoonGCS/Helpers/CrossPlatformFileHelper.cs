using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Services;

namespace HarvestmoonGCS.Helpers;

/// <summary>
/// Cross-platform file helper that works on Desktop, Android, and WebAssembly
/// </summary>
public static class CrossPlatformFileHelper
{
    /// <summary>
    /// Get file service based on platform
    /// </summary>
    private static IFileService? GetFileService()
    {
#if __ANDROID__
        return new Platforms.Android.Services.AndroidFileService(
            Android.App.Application.Context);
#elif !__WASM__
        return new Platforms.Desktop.Services.DesktopFileService();
#else
        return null; // WebAssembly - use fallback
#endif
    }
    /// <summary>
    /// Save mission file
    /// </summary>
    public static async Task<string> SaveMissionFileAsync(string filename, string content)
    {
        var fileService = GetFileService();
        if (fileService != null)
        {
            return await fileService.SaveMissionFileAsync(filename, content);
        }
        
        // WebAssembly/Fallback
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var missionsPath = Path.Combine(appData, "Pigeon", "missions");
        Directory.CreateDirectory(missionsPath);
        
        var filePath = Path.Combine(missionsPath, filename);
        await File.WriteAllTextAsync(filePath, content);
        return filePath;
    }
    
    /// <summary>
    /// Load mission file
    /// </summary>
    public static async Task<string?> LoadMissionFileAsync(string filename)
    {
#if __ANDROID__
        var fileService = Platforms.Android.AndroidCompatibility.FileService;
        if (fileService != null)
        {
            return await fileService.LoadTextFileAsync(filename, "missions");
        }
#endif
        
        // Desktop fallback
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var filePath = Path.Combine(appData, "Pigeon", "missions", filename);
        
        if (!File.Exists(filePath))
            return null;
        
        return await File.ReadAllTextAsync(filePath);
    }
    
    /// <summary>
    /// Save parameter file
    /// </summary>
    public static async Task<string> SaveParameterFileAsync(string filename, string content)
    {
#if __ANDROID__
        var fileService = Platforms.Android.AndroidCompatibility.FileService;
        if (fileService != null)
        {
            return await fileService.SaveTextFileAsync(filename, content, "parameters");
        }
#endif
        
        // Desktop fallback
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var parametersPath = Path.Combine(appData, "Pigeon", "parameters");
        Directory.CreateDirectory(parametersPath);
        
        var filePath = Path.Combine(parametersPath, filename);
        await File.WriteAllTextAsync(filePath, content);
        return filePath;
    }
    
    /// <summary>
    /// Load parameter file
    /// </summary>
    public static async Task<string?> LoadParameterFileAsync(string filename)
    {
#if __ANDROID__
        var fileService = Platforms.Android.AndroidCompatibility.FileService;
        if (fileService != null)
        {
            return await fileService.LoadTextFileAsync(filename, "parameters");
        }
#endif
        
        // Desktop fallback
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var filePath = Path.Combine(appData, "Pigeon", "parameters", filename);
        
        if (!File.Exists(filePath))
            return null;
        
        return await File.ReadAllTextAsync(filePath);
    }
    
    /// <summary>
    /// Export to Downloads folder
    /// </summary>
    public static async Task<string> ExportToDownloadsAsync(string filename, string content)
    {
#if __ANDROID__
        var fileService = Platforms.Android.AndroidCompatibility.FileService;
        if (fileService != null)
        {
            return await fileService.ExportTextToDownloadsAsync(filename, content);
        }
#endif
        
        // Desktop fallback
        var downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        Directory.CreateDirectory(downloadsPath);
        
        var filePath = Path.Combine(downloadsPath, filename);
        await File.WriteAllTextAsync(filePath, content);
        return filePath;
    }
    
    /// <summary>
    /// List mission files
    /// </summary>
    public static string[] ListMissionFiles()
    {
#if __ANDROID__
        var fileService = Platforms.Android.AndroidCompatibility.FileService;
        if (fileService != null)
        {
            return fileService.ListFiles("missions", ".txt").ToArray();
        }
#endif
        
        // Desktop fallback
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var missionsPath = Path.Combine(appData, "Pigeon", "missions");
        
        if (!Directory.Exists(missionsPath))
            return Array.Empty<string>();
        
        return Directory.GetFiles(missionsPath, "*.txt")
            .Select(Path.GetFileName)
            .Where(f => f != null)
            .Cast<string>()
            .ToArray();
    }
    
    /// <summary>
    /// List parameter files
    /// </summary>
    public static string[] ListParameterFiles()
    {
#if __ANDROID__
        var fileService = Platforms.Android.AndroidCompatibility.FileService;
        if (fileService != null)
        {
            return fileService.ListFiles("parameters", ".param").ToArray();
        }
#endif
        
        // Desktop fallback
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var parametersPath = Path.Combine(appData, "Pigeon", "parameters");
        
        if (!Directory.Exists(parametersPath))
            return Array.Empty<string>();
        
        return Directory.GetFiles(parametersPath, "*.param")
            .Select(Path.GetFileName)
            .Where(f => f != null)
            .Cast<string>()
            .ToArray();
    }
    
    /// <summary>
    /// Delete mission file
    /// </summary>
    public static bool DeleteMissionFile(string filename)
    {
#if __ANDROID__
        var fileService = Platforms.Android.AndroidCompatibility.FileService;
        if (fileService != null)
        {
            return fileService.DeleteFile(filename, "missions");
        }
#endif
        
        // Desktop fallback
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var filePath = Path.Combine(appData, "Pigeon", "missions", filename);
        
        if (!File.Exists(filePath))
            return false;
        
        try
        {
            File.Delete(filePath);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Show toast notification (Android only)
    /// </summary>
    public static void ShowToast(string message)
    {
#if __ANDROID__
        Platforms.Android.AndroidCompatibility.ShowToast(message);
#else
        // Desktop: Could show a notification or log
        System.Diagnostics.Debug.WriteLine($"[Toast] {message}");
#endif
    }
}
