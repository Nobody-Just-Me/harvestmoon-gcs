using System;
using System.Threading.Tasks;

namespace Pigeon_Uno.Core.Services;

/// <summary>
/// Computer vision service interface for object detection and vegetation analysis
/// </summary>
public interface IVisionService
{
    // Properties
    bool IsModelLoaded { get; }
    bool IsObjectDetectionActive { get; }
    bool IsVegetationAnalysisActive { get; }
    
    // Events
    event EventHandler<object> DetectionResultReceived;
    event EventHandler<object> VegetationAnalysisResultReceived;
    event EventHandler<string> VisionError;
    
    // Object Detection Methods
    Task<bool> InitializeObjectDetectionAsync(string modelPath);
    Task<object[]> DetectObjectsAsync(byte[] imageData);
    Task StopObjectDetectionAsync();
    
    // Vegetation Analysis Methods
    Task<bool> InitializeVegetationAnalysisAsync();
    Task<double> AnalyzeVegetationAsync(byte[] imageData);
    Task StopVegetationAnalysisAsync();
    Task<bool> ExportVegetationReportAsync(string filepath, object analysisData);
    
    // Legacy Methods (for backward compatibility)
    Task LoadModelAsync(string modelPath);
}

