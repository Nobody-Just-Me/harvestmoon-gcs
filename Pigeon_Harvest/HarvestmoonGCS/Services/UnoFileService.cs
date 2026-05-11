using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;
using HarvestmoonGCS.Core.Services;

namespace HarvestmoonGCS.Services;

public class UnoFileService : IFileService
{
    private string BasePath => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    private string MissionsPath => Path.Combine(BasePath, "Missions");
    private string ParamsPath => Path.Combine(BasePath, "Parameters");
    
    public UnoFileService()
    {
        Directory.CreateDirectory(MissionsPath);
        Directory.CreateDirectory(ParamsPath);
    }
    
    public async Task<string?> PickFileAsync(string[] extensions)
    {
        var picker = new FileOpenPicker();
        
        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        picker.ViewMode = PickerViewMode.List;
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        
        foreach (var ext in extensions)
        {
            picker.FileTypeFilter.Add(ext);
        }

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    public async Task<string?> SaveFileAsync(string suggestedName, string extension)
    {
        var picker = new FileSavePicker();
        
        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.SuggestedFileName = suggestedName;
        picker.FileTypeChoices.Add("File", new[] { extension });

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    public Task<string> SaveMissionFileAsync(string filename, string content)
    {
        var path = Path.Combine(MissionsPath, filename);
        File.WriteAllText(path, content);
        return Task.FromResult(path);
    }

    public async Task<string?> LoadMissionFileAsync(string filename)
    {
        var path = Path.Combine(MissionsPath, filename);
        if (File.Exists(path))
            return await File.ReadAllTextAsync(path);
        return null;
    }

    public Task<string> SaveParameterFileAsync(string filename, string content)
    {
        var path = Path.Combine(ParamsPath, filename);
        File.WriteAllText(path, content);
        return Task.FromResult(path);
    }

    public async Task<string?> LoadParameterFileAsync(string filename)
    {
        var path = Path.Combine(ParamsPath, filename);
        if (File.Exists(path))
            return await File.ReadAllTextAsync(path);
        return null;
    }

    public Task<string> ExportToDownloadsAsync(string filename, string content)
    {
        var downloads = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var path = Path.Combine(downloads, filename);
        File.WriteAllText(path, content);
        return Task.FromResult(path);
    }

    public string[] ListMissionFiles()
    {
        return Directory.Exists(MissionsPath) 
            ? Directory.GetFiles(MissionsPath, "*.json") 
            : Array.Empty<string>();
    }

    public string[] ListParameterFiles()
    {
        return Directory.Exists(ParamsPath) 
            ? Directory.GetFiles(ParamsPath, "*.json") 
            : Array.Empty<string>();
    }

    public bool DeleteMissionFile(string filename)
    {
        var path = Path.Combine(MissionsPath, filename);
        if (File.Exists(path))
        {
            File.Delete(path);
            return true;
        }
        return false;
    }

    public Task<string> SaveTextFileAsync(string filename, string content, string subfolder = "")
    {
        var path = Path.Combine(BasePath, subfolder);
        Directory.CreateDirectory(path);
        path = Path.Combine(path, filename);
        File.WriteAllText(path, content);
        return Task.FromResult(path);
    }

    public async Task<string?> LoadTextFileAsync(string filename, string subfolder = "")
    {
        var path = Path.Combine(BasePath, subfolder, filename);
        if (File.Exists(path))
            return await File.ReadAllTextAsync(path);
        return null;
    }

    public string[] ListFiles(string subfolder, string extension)
    {
        var path = Path.Combine(BasePath, subfolder);
        if (Directory.Exists(path))
            return Directory.GetFiles(path, $"*{extension}");
        return Array.Empty<string>();
    }

    public bool DeleteFile(string filename, string subfolder = "")
    {
        var path = Path.Combine(BasePath, subfolder, filename);
        if (File.Exists(path))
        {
            File.Delete(path);
            return true;
        }
        return false;
    }
}