using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HarvestmoonGCS.Core.Services;

/// <summary>
/// Platform abstraction for file system operations.
/// Provides cross-platform access to file picking, reading, and writing.
/// </summary>
public interface IFileSystemService
{
    /// <summary>
    /// Opens a file picker dialog for the user to select a file.
    /// </summary>
    /// <param name="fileTypes">Array of file type filters (e.g., ".tlog", ".csv", ".json")</param>
    /// <param name="title">Title for the file picker dialog</param>
    /// <returns>The selected file path, or null if cancelled</returns>
    Task<string?> PickFileAsync(string[] fileTypes, string? title = null);

    /// <summary>
    /// Opens a file picker dialog for the user to select multiple files.
    /// </summary>
    /// <param name="fileTypes">Array of file type filters (e.g., ".tlog", ".csv", ".json")</param>
    /// <param name="title">Title for the file picker dialog</param>
    /// <returns>Array of selected file paths, or empty array if cancelled</returns>
    Task<string[]> PickMultipleFilesAsync(string[] fileTypes, string? title = null);

    /// <summary>
    /// Opens a save file dialog for the user to specify where to save a file.
    /// </summary>
    /// <param name="suggestedFileName">Suggested file name</param>
    /// <param name="fileTypes">Array of file type filters (e.g., ".tlog", ".csv", ".json")</param>
    /// <param name="title">Title for the save dialog</param>
    /// <returns>The selected file path, or null if cancelled</returns>
    Task<string?> PickSaveFileAsync(string suggestedFileName, string[] fileTypes, string? title = null);

    /// <summary>
    /// Opens a folder picker dialog for the user to select a directory.
    /// </summary>
    /// <param name="title">Title for the folder picker dialog</param>
    /// <returns>The selected folder path, or null if cancelled</returns>
    Task<string?> PickFolderAsync(string? title = null);

    /// <summary>
    /// Reads all bytes from a file.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns>Byte array containing the file contents</returns>
    Task<byte[]> ReadFileBytesAsync(string filePath);

    /// <summary>
    /// Reads all text from a file.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns>String containing the file contents</returns>
    Task<string> ReadFileTextAsync(string filePath);

    /// <summary>
    /// Writes bytes to a file, creating or overwriting it.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <param name="data">Data to write</param>
    Task WriteFileBytesAsync(string filePath, byte[] data);

    /// <summary>
    /// Writes text to a file, creating or overwriting it.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <param name="text">Text to write</param>
    Task WriteFileTextAsync(string filePath, string text);

    /// <summary>
    /// Appends bytes to a file.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <param name="data">Data to append</param>
    Task AppendFileBytesAsync(string filePath, byte[] data);

    /// <summary>
    /// Appends text to a file.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <param name="text">Text to append</param>
    Task AppendFileTextAsync(string filePath, string text);

    /// <summary>
    /// Checks if a file exists.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns>True if the file exists, false otherwise</returns>
    Task<bool> FileExistsAsync(string filePath);

    /// <summary>
    /// Checks if a directory exists.
    /// </summary>
    /// <param name="directoryPath">Path to the directory</param>
    /// <returns>True if the directory exists, false otherwise</returns>
    Task<bool> DirectoryExistsAsync(string directoryPath);

    /// <summary>
    /// Creates a directory if it doesn't exist.
    /// </summary>
    /// <param name="directoryPath">Path to the directory</param>
    Task CreateDirectoryAsync(string directoryPath);

    /// <summary>
    /// Deletes a file.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    Task DeleteFileAsync(string filePath);

    /// <summary>
    /// Gets files in a directory.
    /// </summary>
    /// <param name="directoryPath">Path to the directory</param>
    /// <param name="searchPattern">Search pattern (e.g., "*.tlog")</param>
    /// <returns>Array of file paths</returns>
    Task<string[]> GetFilesAsync(string directoryPath, string searchPattern = "*");

    /// <summary>
    /// Gets the application's local data directory path.
    /// This is a platform-appropriate location for storing application data.
    /// </summary>
    /// <returns>Path to the local data directory</returns>
    string GetLocalDataPath();

    /// <summary>
    /// Gets the application's temporary directory path.
    /// </summary>
    /// <returns>Path to the temporary directory</returns>
    string GetTempPath();
}

/// <summary>
/// File type descriptor for file picker dialogs.
/// </summary>
public class FileTypeFilter
{
    /// <summary>
    /// Display name for the file type (e.g., "Telemetry Log Files").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// File extensions for this type (e.g., [".tlog", ".log"]).
    /// </summary>
    public List<string> Extensions { get; set; } = new();

    public FileTypeFilter()
    {
    }

    public FileTypeFilter(string name, params string[] extensions)
    {
        Name = name;
        Extensions = new List<string>(extensions);
    }
}
