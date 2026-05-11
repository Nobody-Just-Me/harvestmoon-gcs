#if __ANDROID__
using Android.Content;
using Android.OS;
using Android.Provider;
using AndroidX.DocumentFile.Provider;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Services;

namespace HarvestmoonGCS.Platforms.Android.Services;

/// <summary>
/// Android-specific file service for handling file operations
/// </summary>
public class AndroidFileService : IFileService
{
    private readonly Context _context;
    
    public AndroidFileService(Context context)
    {
        _context = context;
    }
    
    /// <summary>
    /// Get app-specific storage directory (always accessible)
    /// </summary>
    public string GetAppStorageDirectory()
    {
        return _context.GetExternalFilesDir(null)?.AbsolutePath 
            ?? _context.FilesDir?.AbsolutePath 
            ?? System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
    }
    
    /// <summary>
    /// Get Downloads directory
    /// </summary>
    public string GetDownloadsDirectory()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
        {
            var downloadsDir = global::Android.OS.Environment.GetExternalStoragePublicDirectory(global::Android.OS.Environment.DirectoryDownloads);
            return downloadsDir?.AbsolutePath ?? "/storage/emulated/0/Download";
        }
        else
        {
            return System.IO.Path.Combine(global::Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath ?? "", "Download");
        }
    }
    
    /// <summary>
    /// Save file to app storage
    /// </summary>
    public async Task<string> SaveFileAsync(string filename, byte[] data, string? subfolder = null)
    {
        var basePath = GetAppStorageDirectory();
        
        if (!string.IsNullOrEmpty(subfolder))
        {
            basePath = Path.Combine(basePath, subfolder);
            Directory.CreateDirectory(basePath);
        }
        
        var filePath = Path.Combine(basePath, filename);
        await File.WriteAllBytesAsync(filePath, data);
        
        return filePath;
    }
    
    /// <summary>
    /// Save text file to app storage
    /// </summary>
    public async Task<string> SaveTextFileAsync(string filename, string content, string? subfolder = null)
    {
        var basePath = GetAppStorageDirectory();
        
        if (!string.IsNullOrEmpty(subfolder))
        {
            basePath = Path.Combine(basePath, subfolder);
            Directory.CreateDirectory(basePath);
        }
        
        var filePath = Path.Combine(basePath, filename);
        await File.WriteAllTextAsync(filePath, content);
        
        return filePath;
    }
    
    /// <summary>
    /// Load file from app storage
    /// </summary>
    public async Task<byte[]?> LoadFileAsync(string filename, string? subfolder = null)
    {
        var basePath = GetAppStorageDirectory();
        
        if (!string.IsNullOrEmpty(subfolder))
        {
            basePath = Path.Combine(basePath, subfolder);
        }
        
        var filePath = Path.Combine(basePath, filename);
        
        if (!File.Exists(filePath))
            return null;
        
        return await File.ReadAllBytesAsync(filePath);
    }
    
    /// <summary>
    /// Load text file from app storage
    /// </summary>
    public async Task<string?> LoadTextFileAsync(string filename, string? subfolder = null)
    {
        var basePath = GetAppStorageDirectory();
        
        if (!string.IsNullOrEmpty(subfolder))
        {
            basePath = Path.Combine(basePath, subfolder);
        }
        
        var filePath = Path.Combine(basePath, filename);
        
        if (!File.Exists(filePath))
            return null;
        
        return await File.ReadAllTextAsync(filePath);
    }
    
    /// <summary>
    /// List files in app storage
    /// </summary>
    public List<string> ListFiles(string? subfolder = null, string? extension = null)
    {
        var basePath = GetAppStorageDirectory();
        
        if (!string.IsNullOrEmpty(subfolder))
        {
            basePath = Path.Combine(basePath, subfolder);
        }
        
        if (!Directory.Exists(basePath))
            return new List<string>();
        
        var files = Directory.GetFiles(basePath);
        
        if (!string.IsNullOrEmpty(extension))
        {
            files = files.Where(f => Path.GetExtension(f).Equals(extension, StringComparison.OrdinalIgnoreCase)).ToArray();
        }
        
        return files.Select(Path.GetFileName).Where(f => f != null).Cast<string>().ToList();
    }
    
    /// <summary>
    /// Delete file from app storage
    /// </summary>
    public bool DeleteFile(string filename, string? subfolder = null)
    {
        var basePath = GetAppStorageDirectory();
        
        if (!string.IsNullOrEmpty(subfolder))
        {
            basePath = Path.Combine(basePath, subfolder);
        }
        
        var filePath = Path.Combine(basePath, filename);
        
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
    /// Export file to Downloads folder
    /// </summary>
    public async Task<string> ExportToDownloadsAsync(string filename, byte[] data)
    {
        var downloadsPath = GetDownloadsDirectory();
        var filePath = Path.Combine(downloadsPath, filename);
        
        await File.WriteAllBytesAsync(filePath, data);
        
        // Notify media scanner (for Android < 10)
        if (Build.VERSION.SdkInt < BuildVersionCodes.Q)
        {
            var intent = new Intent(Intent.ActionMediaScannerScanFile);
            intent.SetData(global::Android.Net.Uri.FromFile(new Java.IO.File(filePath)));
            _context.SendBroadcast(intent);
        }
        
        return filePath;
    }
    
    /// <summary>
    /// Export text file to Downloads folder
    /// </summary>
    public async Task<string> ExportTextToDownloadsAsync(string filename, string content)
    {
        var downloadsPath = GetDownloadsDirectory();
        var filePath = Path.Combine(downloadsPath, filename);
        
        await File.WriteAllTextAsync(filePath, content);
        
        // Notify media scanner (for Android < 10)
        if (Build.VERSION.SdkInt < BuildVersionCodes.Q)
        {
            var intent = new Intent(Intent.ActionMediaScannerScanFile);
            intent.SetData(global::Android.Net.Uri.FromFile(new Java.IO.File(filePath)));
            _context.SendBroadcast(intent);
        }
        
        return filePath;
    }
    
    /// <summary>
    /// Check if file exists in app storage
    /// </summary>
    public bool FileExists(string filename, string? subfolder = null)
    {
        var basePath = GetAppStorageDirectory();
        
        if (!string.IsNullOrEmpty(subfolder))
        {
            basePath = Path.Combine(basePath, subfolder);
        }
        
        var filePath = Path.Combine(basePath, filename);
        return File.Exists(filePath);
    }
    
    /// <summary>
    /// Get file size
    /// </summary>
    public long GetFileSize(string filename, string? subfolder = null)
    {
        var basePath = GetAppStorageDirectory();
        
        if (!string.IsNullOrEmpty(subfolder))
        {
            basePath = Path.Combine(basePath, subfolder);
        }
        
        var filePath = Path.Combine(basePath, filename);
        
        if (!File.Exists(filePath))
            return 0;
        
        return new FileInfo(filePath).Length;
    }
    
    // IFileService implementation
    public async Task<string> SaveMissionFileAsync(string filename, string content)
    {
        return await SaveTextFileAsync(filename, content, "missions");
    }
    
    public async Task<string?> LoadMissionFileAsync(string filename)
    {
        return await LoadTextFileAsync(filename, "missions");
    }
    
    public async Task<string> SaveParameterFileAsync(string filename, string content)
    {
        return await SaveTextFileAsync(filename, content, "parameters");
    }
    
    public async Task<string?> LoadParameterFileAsync(string filename)
    {
        return await LoadTextFileAsync(filename, "parameters");
    }
    
    public async Task<string> ExportToDownloadsAsync(string filename, string content)
    {
        return await ExportTextToDownloadsAsync(filename, content);
    }
    
    public string[] ListMissionFiles()
    {
        return ListFiles("missions", ".waypoints").ToArray();
    }
    
    public string[] ListParameterFiles()
    {
        return ListFiles("parameters", ".param").ToArray();
    }
    
    public bool DeleteMissionFile(string filename)
    {
        return DeleteFile(filename, "missions");
    }
    
    public async Task<string?> PickFileAsync(string[] allowedExtensions)
    {
        // Android file picker implementation would go here
        // For now, return null as this requires Android-specific UI
        await Task.CompletedTask;
        return null;
    }
    
    // IFileService overloads with different signatures
    async Task<string> IFileService.SaveTextFileAsync(string filename, string content, string subfolder)
    {
        return await SaveTextFileAsync(filename, content, string.IsNullOrEmpty(subfolder) ? null : subfolder);
    }
    
    async Task<string?> IFileService.LoadTextFileAsync(string filename, string subfolder)
    {
        return await LoadTextFileAsync(filename, string.IsNullOrEmpty(subfolder) ? null : subfolder);
    }
    
    string[] IFileService.ListFiles(string subfolder, string extension)
    {
        return ListFiles(string.IsNullOrEmpty(subfolder) ? null : subfolder, extension).ToArray();
    }
    
    bool IFileService.DeleteFile(string filename, string subfolder)
    {
        return DeleteFile(filename, string.IsNullOrEmpty(subfolder) ? null : subfolder);
    }
}
#endif
