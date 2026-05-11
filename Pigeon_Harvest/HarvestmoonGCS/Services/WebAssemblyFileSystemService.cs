using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Services;

namespace HarvestmoonGCS.Services;

/// <summary>
/// WebAssembly implementation of IFileSystemService.
/// Uses browser APIs for file access (File API, IndexedDB, etc.).
/// Note: WebAssembly has limited file system access due to browser security restrictions.
/// </summary>
public class WebAssemblyFileSystemService : IFileSystemService
{
    // In-memory storage for WebAssembly (could be replaced with IndexedDB)
    private readonly string _localDataPath = "/local-data";
    private readonly string _tempPath = "/temp";
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    public async Task<string?> PickFileAsync(string[] fileTypes, string? title = null)
    {
        // TODO: Implement using HTML file input element via JavaScript interop
        // Example:
        // var result = await JSRuntime.InvokeAsync<string>("pickFile", fileTypes);
        // return result;
        
        await Task.CompletedTask;
        return null;
    }

    public async Task<string[]> PickMultipleFilesAsync(string[] fileTypes, string? title = null)
    {
        // TODO: Implement using HTML file input element with multiple attribute
        await Task.CompletedTask;
        return Array.Empty<string>();
    }

    public async Task<string?> PickSaveFileAsync(string suggestedFileName, string[] fileTypes, string? title = null)
    {
        // TODO: Implement using File System Access API or download trigger
        // In WebAssembly, "saving" typically means triggering a download
        await Task.CompletedTask;
        return null;
    }

    public async Task<string?> PickFolderAsync(string? title = null)
    {
        // TODO: Implement using File System Access API (if available)
        // Note: Not all browsers support this API
        await Task.CompletedTask;
        return null;
    }

    public async Task<byte[]> ReadFileBytesAsync(string filePath)
    {
        try
        {
            await Task.CompletedTask;
            var normalizedPath = NormalizePath(filePath);
            if (!_files.TryGetValue(normalizedPath, out var data))
            {
                throw new FileNotFoundException("File not found in WebAssembly storage.", normalizedPath);
            }

            return data.ToArray();
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
            var bytes = await ReadFileBytesAsync(filePath);
            return Encoding.UTF8.GetString(bytes);
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
            await Task.CompletedTask;
            _files[NormalizePath(filePath)] = (data ?? Array.Empty<byte>()).ToArray();
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
            var bytes = Encoding.UTF8.GetBytes(text);
            await WriteFileBytesAsync(filePath, bytes);
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
            await Task.CompletedTask;
            var normalizedPath = NormalizePath(filePath);
            var existing = _files.TryGetValue(normalizedPath, out var current)
                ? current
                : Array.Empty<byte>();

            _files[normalizedPath] = existing.Concat(data ?? Array.Empty<byte>()).ToArray();
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
            var bytes = Encoding.UTF8.GetBytes(text);
            await AppendFileBytesAsync(filePath, bytes);
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to append to file: {filePath}", ex);
        }
    }

    public Task<bool> FileExistsAsync(string filePath)
    {
        return Task.FromResult(_files.ContainsKey(NormalizePath(filePath)));
    }

    public Task<bool> DirectoryExistsAsync(string directoryPath)
    {
        var normalizedPath = NormalizePath(directoryPath);
        var exists = normalizedPath == _localDataPath ||
            normalizedPath == _tempPath ||
            _files.Keys.Any(path => path.StartsWith(normalizedPath.TrimEnd('/') + "/", StringComparison.Ordinal));
        return Task.FromResult(exists);
    }

    public Task CreateDirectoryAsync(string directoryPath)
    {
        return Task.CompletedTask;
    }

    public Task DeleteFileAsync(string filePath)
    {
        _files.Remove(NormalizePath(filePath));
        return Task.CompletedTask;
    }

    public Task<string[]> GetFilesAsync(string directoryPath, string searchPattern = "*")
    {
        var normalizedDirectory = NormalizePath(directoryPath).TrimEnd('/') + "/";
        var files = _files.Keys
            .Where(path => path.StartsWith(normalizedDirectory, StringComparison.Ordinal))
            .Where(path => !path[normalizedDirectory.Length..].Contains('/'))
            .Where(path => MatchesSearchPattern(Path.GetFileName(path), searchPattern))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult(files);
    }

    public string GetLocalDataPath()
    {
        // In WebAssembly, use a virtual path
        // Actual storage would be in IndexedDB
        return _localDataPath;
    }

    public string GetTempPath()
    {
        // In WebAssembly, use a virtual path
        return _tempPath;
    }

    private static string NormalizePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Path cannot be empty.", nameof(filePath));
        }

        var normalized = filePath.Replace('\\', '/').Trim();
        return normalized.StartsWith("/", StringComparison.Ordinal)
            ? normalized
            : "/" + normalized;
    }

    private static bool MatchesSearchPattern(string fileName, string searchPattern)
    {
        if (string.IsNullOrWhiteSpace(searchPattern) || searchPattern == "*")
        {
            return true;
        }

        if (searchPattern.StartsWith("*.", StringComparison.Ordinal))
        {
            return fileName.EndsWith(searchPattern[1..], StringComparison.OrdinalIgnoreCase);
        }

        return fileName.Equals(searchPattern, StringComparison.OrdinalIgnoreCase);
    }
}

/* JavaScript code that would be needed for file operations:

// File picker
async function pickFile(fileTypes) {
    return new Promise((resolve) => {
        const input = document.createElement('input');
        input.type = 'file';
        if (fileTypes && fileTypes.length > 0) {
            input.accept = fileTypes.join(',');
        }
        input.onchange = async (e) => {
            const file = e.target.files[0];
            if (file) {
                // Store file in IndexedDB or return file name
                resolve(file.name);
            } else {
                resolve(null);
            }
        };
        input.click();
    });
}

// Download file (for "saving" in WebAssembly)
function downloadFile(fileName, base64Data) {
    const byteCharacters = atob(base64Data);
    const byteNumbers = new Array(byteCharacters.length);
    for (let i = 0; i < byteCharacters.length; i++) {
        byteNumbers[i] = byteCharacters.charCodeAt(i);
    }
    const byteArray = new Uint8Array(byteNumbers);
    const blob = new Blob([byteArray]);
    
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(link.href);
}

// IndexedDB operations would go here for persistent storage
// This would include functions to:
// - Store files in IndexedDB
// - Retrieve files from IndexedDB
// - List files in IndexedDB
// - Delete files from IndexedDB

*/
