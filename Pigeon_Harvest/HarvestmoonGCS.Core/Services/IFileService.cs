using System.Threading.Tasks;

namespace HarvestmoonGCS.Core.Services;

/// <summary>
/// Cross-platform file service interface
/// </summary>
public interface IFileService
{
    Task<string> SaveMissionFileAsync(string filename, string content);
    Task<string?> LoadMissionFileAsync(string filename);
    Task<string> SaveParameterFileAsync(string filename, string content);
    Task<string?> LoadParameterFileAsync(string filename);
    Task<string> ExportToDownloadsAsync(string filename, string content);
    string[] ListMissionFiles();
    string[] ListParameterFiles();
    bool DeleteMissionFile(string filename);
    
    // Additional methods used by existing code
    Task<string?> PickFileAsync(string[] allowedExtensions);
    Task<string> SaveTextFileAsync(string filename, string content, string subfolder = "");
    Task<string?> LoadTextFileAsync(string filename, string subfolder = "");
    string[] ListFiles(string subfolder, string extension);
    bool DeleteFile(string filename, string subfolder = "");
}