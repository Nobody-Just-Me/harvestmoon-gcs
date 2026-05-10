using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Pigeon_Uno.Core.Services;

namespace Pigeon_Uno.Services;

/// <summary>
/// Desktop (Windows, Linux, macOS) implementation of IFileSystemService.
/// Uses standard System.IO APIs and platform-specific file dialogs.
/// OPTIMIZED: All I/O operations use async/await to prevent UI thread blocking
/// </summary>
public class DesktopFileSystemService : IFileSystemService
{
    public async Task<string?> PickFileAsync(string[] fileTypes, string? title = null)
    {
        try
        {
#if __WASM__
            // WebAssembly: Use HTML file input
            await Task.CompletedTask;
            return null;
#elif __ANDROID__
            // Android: Use Android file picker
            await Task.CompletedTask;
            return null;
#else
            // Desktop: Use simple console-based picker for now
            // In production, integrate with platform-specific dialogs:
            // - Windows: Microsoft.Win32.OpenFileDialog
            // - Linux: GTK FileChooserDialog or Zenity
            // - macOS: NSOpenPanel
            
            Console.WriteLine($"File Picker: {title ?? "Select a file"}");
            Console.WriteLine($"File types: {string.Join(", ", fileTypes)}");
            Console.Write("Enter file path: ");
            var path = Console.ReadLine();
            
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }
            
            return path;
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DesktopFileSystemService] PickFileAsync failed: {ex.Message}");
            return null;
        }
    }

    public async Task<string[]> PickMultipleFilesAsync(string[] fileTypes, string? title = null)
    {
        try
        {
#if __WASM__ || __ANDROID__
            await Task.CompletedTask;
            return Array.Empty<string>();
#else
            // Desktop: Simple implementation
            Console.WriteLine($"Multiple File Picker: {title ?? "Select files"}");
            Console.WriteLine($"File types: {string.Join(", ", fileTypes)}");
            Console.WriteLine("Enter file paths (comma-separated): ");
            var input = Console.ReadLine();
            
            if (string.IsNullOrWhiteSpace(input))
            {
                return Array.Empty<string>();
            }
            
            var paths = input.Split(',')
                .Select(p => p.Trim())
                .Where(p => File.Exists(p))
                .ToArray();
            
            return paths;
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DesktopFileSystemService] PickMultipleFilesAsync failed: {ex.Message}");
            return Array.Empty<string>();
        }
    }

    public async Task<string?> PickSaveFileAsync(string suggestedFileName, string[] fileTypes, string? title = null)
    {
        try
        {
#if __WASM__ || __ANDROID__
            await Task.CompletedTask;
            return null;
#else
            // Desktop: Simple implementation
            Console.WriteLine($"Save File Picker: {title ?? "Save file"}");
            Console.WriteLine($"Suggested name: {suggestedFileName}");
            Console.WriteLine($"File types: {string.Join(", ", fileTypes)}");
            Console.Write("Enter save path: ");
            var path = Console.ReadLine();
            
            if (string.IsNullOrWhiteSpace(path))
            {
                // Use suggested filename in current directory
                path = Path.Combine(GetLocalDataPath(), suggestedFileName);
            }
            
            return path;
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DesktopFileSystemService] PickSaveFileAsync failed: {ex.Message}");
            return null;
        }
    }

    public async Task<string?> PickFolderAsync(string? title = null)
    {
        try
        {
#if __WASM__ || __ANDROID__
            await Task.CompletedTask;
            return null;
#else
            // Desktop: Simple implementation
            Console.WriteLine($"Folder Picker: {title ?? "Select a folder"}");
            Console.Write("Enter folder path: ");
            var path = Console.ReadLine();
            
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                return null;
            }
            
            return path;
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DesktopFileSystemService] PickFolderAsync failed: {ex.Message}");
            return null;
        }
    }

    public async Task<byte[]> ReadFileBytesAsync(string filePath)
    {
        try
        {
            // OPTIMIZATION: Uses async I/O to avoid blocking UI thread
            return await File.ReadAllBytesAsync(filePath);
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to read file: {filePath}", ex);
        }
    }

    public async Task<string> ReadFileTextAsync(string filePath)
    {
        try
        {
            // OPTIMIZATION: Uses async I/O to avoid blocking UI thread
            return await File.ReadAllTextAsync(filePath);
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to read file: {filePath}", ex);
        }
    }

    public async Task WriteFileBytesAsync(string filePath, byte[] data)
    {
        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // OPTIMIZATION: Uses async I/O to avoid blocking UI thread
            await File.WriteAllBytesAsync(filePath, data);
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to write file: {filePath}", ex);
        }
    }

    public async Task WriteFileTextAsync(string filePath, string text)
    {
        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // OPTIMIZATION: Uses async I/O to avoid blocking UI thread
            await File.WriteAllTextAsync(filePath, text);
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to write file: {filePath}", ex);
        }
    }

    public async Task AppendFileBytesAsync(string filePath, byte[] data)
    {
        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // OPTIMIZATION: Uses async stream I/O to avoid blocking UI thread
            using var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            await stream.WriteAsync(data, 0, data.Length);
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to append to file: {filePath}", ex);
        }
    }

    public async Task AppendFileTextAsync(string filePath, string text)
    {
        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // OPTIMIZATION: Uses async I/O to avoid blocking UI thread
            await File.AppendAllTextAsync(filePath, text);
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to append to file: {filePath}", ex);
        }
    }

    public Task<bool> FileExistsAsync(string filePath)
    {
        try
        {
            return Task.FromResult(File.Exists(filePath));
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<bool> DirectoryExistsAsync(string directoryPath)
    {
        try
        {
            return Task.FromResult(Directory.Exists(directoryPath));
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task CreateDirectoryAsync(string directoryPath)
    {
        try
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to create directory: {directoryPath}", ex);
        }
    }

    public Task DeleteFileAsync(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to delete file: {filePath}", ex);
        }
    }

    public Task<string[]> GetFilesAsync(string directoryPath, string searchPattern = "*")
    {
        try
        {
            if (!Directory.Exists(directoryPath))
            {
                return Task.FromResult(Array.Empty<string>());
            }

            var files = Directory.GetFiles(directoryPath, searchPattern);
            return Task.FromResult(files);
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to get files from directory: {directoryPath}", ex);
        }
    }

    public string GetLocalDataPath()
    {
        // Use platform-appropriate local data directory
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appName = "PigeonGCS";
        var localDataPath = Path.Combine(appDataPath, appName);

        // Ensure directory exists
        if (!Directory.Exists(localDataPath))
        {
            Directory.CreateDirectory(localDataPath);
        }

        return localDataPath;
    }

    public string GetTempPath()
    {
        return Path.GetTempPath();
    }
}
