using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using OpenCvSharp;
using Pigeon_Uno.Core.Helpers;
using Pigeon_Uno.Core.Services;

namespace Pigeon_Uno.Services;

public sealed class HarvestFunctionalService : IDisposable
{
    public sealed class HarvestReportRecord
    {
        public string Id { get; set; } = string.Empty;
        public string DateTime { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
        public int Detections { get; set; }
        public string Priority { get; set; } = string.Empty;
        public string OperatorNote { get; set; } = string.Empty;
        public string AiModelUsed { get; set; } = string.Empty;
        public double HealthyPercentage { get; set; }
        public double StressedPercentage { get; set; }
        public double DroughtPercentage { get; set; }
        public double BareSoilPercentage { get; set; }
        public string PriorityZonesJson { get; set; } = string.Empty;
        public string GroundTruthValidationSummary { get; set; } = string.Empty;
        public string TlogPath { get; set; } = string.Empty;
        public string GeofenceAlertsJson { get; set; } = "[]";
        public string ExportPath { get; set; } = string.Empty;
    }

    public sealed class HarvestZonePriority
    {
        public int Priority { get; set; }
        public int Row { get; set; }
        public int Col { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Severity { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
    }

    public sealed class HarvestDetectionBox
    {
        public string ClassName { get; set; } = string.Empty;
        public float Confidence { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public sealed class HarvestAnalysisResult
    {
        public string SourceImagePath { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Area { get; set; } = "Unknown";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
        public int TotalZones { get; set; }
        public double HealthyPercentage { get; set; }
        public double StressedPercentage { get; set; }
        public double DroughtPercentage { get; set; }
        public double BareSoilPercentage { get; set; }
        public int DetectionCount { get; set; }
        public int HighPriorityCount { get; set; }
        public int IrrigationTaggedCount { get; set; }
        public string OverallSeverity { get; set; } = string.Empty;
        public List<string> Recommendations { get; set; } = new();
        public List<HarvestZonePriority> Priorities { get; set; } = new();
        public List<HarvestDetectionBox> DetectionBoxes { get; set; } = new();
    }

    public sealed class HarvestGroundTruthSample
    {
        public int ZoneRow { get; set; }
        public int ZoneCol { get; set; }
        public double SoilMoisturePercent { get; set; }
    }

    public sealed class HarvestValidationResult
    {
        public int TotalSamples { get; set; }
        public int MatchedSamples { get; set; }
        public double MatchPercentage { get; set; }
        public string Summary { get; set; } = string.Empty;
    }

    private const string ReportsSubfolder = "HarvestReports";
    private const string ReportsIndexFile = "reports_index.json";
    private readonly IFileService _fileService;
    private readonly VegetationYoloAnalyzer _analyzer = new();
    private bool _initialized;
    public bool IsYoloOptionEnabled { get; private set; } = true;
    public bool IsYoloRuntimeReady => _initialized && _analyzer.IsYoloInitialized;
    public string YoloStatusMessage { get; private set; } = "YOLO standby";
    public event EventHandler<bool>? YoloOptionChanged;

    /// <summary>
    /// Runs YOLO directly on an encoded frame (JPEG/PNG bytes) without the full
    /// zone analysis pipeline. Uses SkiaSharp for decoding AND preprocessing
    /// (no OpenCvSharp dependency) so it works even when native OpenCV libs are
    /// missing on the host (Ubuntu 24.04 ABI mismatch).
    /// </summary>
    public Task<List<HarvestDetectionBox>> DetectInFrameAsync(byte[] frameData)
    {
        return Task.Run(() =>
        {
            var boxes = new List<HarvestDetectionBox>();
            if (frameData == null || frameData.Length == 0)
            {
                return boxes;
            }

            EnsureInitialized();
            if (!IsYoloOptionEnabled || !_analyzer.IsYoloInitialized || _analyzer.Detector == null)
            {
                return boxes;
            }

            try
            {
                // Decode JPEG using SkiaSharp (always available, no native OpenCV needed)
                using var skBitmap = SkiaSharp.SKBitmap.Decode(frameData);
                if (skBitmap == null)
                {
                    Serilog.Log.Warning("[HarvestFunctionalService] DetectInFrameAsync: SKBitmap.Decode returned null");
                    return boxes;
                }

                // Resize to 640x640 and build ONNX input tensor directly from SkiaSharp pixels.
                // This bypasses OpenCvSharp entirely for the live preview path.
                int inputW = 640, inputH = 640;
                using var resized = skBitmap.Resize(new SkiaSharp.SKImageInfo(inputW, inputH), SkiaSharp.SKFilterQuality.Medium);
                if (resized == null)
                {
                    return boxes;
                }

                var tensor = new Microsoft.ML.OnnxRuntime.Tensors.DenseTensor<float>(new[] { 1, 3, inputH, inputW });
                var pixels = resized.Pixels; // SKColor[] RGBA
                for (int i = 0; i < pixels.Length; i++)
                {
                    var c = pixels[i];
                    int y = i / inputW;
                    int x = i % inputW;
                    tensor[0, 0, y, x] = c.Red / 255f;
                    tensor[0, 1, y, x] = c.Green / 255f;
                    tensor[0, 2, y, x] = c.Blue / 255f;
                }

                // Run inference
                var inputs = new System.Collections.Generic.List<Microsoft.ML.OnnxRuntime.NamedOnnxValue>
                {
                    Microsoft.ML.OnnxRuntime.NamedOnnxValue.CreateFromTensor("images", tensor)
                };

                using var outputs = _analyzer.Detector.Session.Run(inputs);
                var output = outputs.First().AsTensor<float>();

                // Post-process (reuse YoloDetector logic)
                var detections = _analyzer.Detector.PostProcessPublic(output, skBitmap.Width, skBitmap.Height);
                foreach (var d in detections)
                {
                    boxes.Add(new HarvestDetectionBox
                    {
                        ClassName = d.ClassName,
                        Confidence = d.Confidence,
                        X = d.BoundingBox.X,
                        Y = d.BoundingBox.Y,
                        Width = d.BoundingBox.Width,
                        Height = d.BoundingBox.Height
                    });
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[HarvestFunctionalService] DetectInFrameAsync error");
            }
            return boxes;
        });
    }

    public HarvestFunctionalService(IFileService fileService)
    {
        _fileService = fileService;
    }

    public async Task<HarvestAnalysisResult?> AnalyzeImageAsync(string imagePath, string area, double latitude, double longitude, double altitude)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath)) return null;
        EnsureInitialized();
        _analyzer.UseYolo = IsYoloOptionEnabled;
        using var frame = Cv2.ImRead(imagePath);
        if (frame.Empty()) return null;

        var zones = _analyzer.AnalyzeFrame(frame, forceAnalyze: true) ?? new List<VegetationYoloAnalyzer.ZoneResult>();
        var stats = _analyzer.CalculateStatistics(zones);
        var tagged = _analyzer.TagIrrigationZones(zones, latitude, longitude, altitude);
        var recommendations = _analyzer.GetIrrigationRecommendations(stats);

        var priorityZones = zones
            .Where(z => z.IrrigationPriority > 0)
            .OrderBy(z => z.IrrigationPriority)
            .Select(z =>
            {
                var (zoneLat, zoneLon) = EstimateZoneCoordinate(z, zones, latitude, longitude);
                return new HarvestZonePriority
                {
                    Priority = z.IrrigationPriority,
                    Row = z.Row,
                    Col = z.Col,
                    Latitude = zoneLat,
                    Longitude = zoneLon,
                    Severity = z.Severity.ToString(),
                    Recommendation = z.Recommendation
                };
            })
            .ToList();

        var result = new HarvestAnalysisResult
        {
            SourceImagePath = imagePath,
            Timestamp = DateTime.Now,
            Area = area,
            Latitude = latitude,
            Longitude = longitude,
            Altitude = altitude,
            TotalZones = stats.TotalZones,
            HealthyPercentage = stats.TotalHealthyPercentage,
            StressedPercentage = stats.TotalStressedPercentage,
            DroughtPercentage = stats.TotalDroughtPercentage,
            BareSoilPercentage = stats.TotalBareSoilPercentage,
            DetectionCount = stats.YoloDetectionCount,
            HighPriorityCount = zones.Count(z => z.Severity >= VegetationYoloAnalyzer.DroughtSeverity.Severe),
            IrrigationTaggedCount = tagged,
            OverallSeverity = stats.OverallSeverity.ToString(),
            Recommendations = recommendations,
            Priorities = priorityZones,
            DetectionBoxes = zones.SelectMany(z => z.Detections).Select(d => new HarvestDetectionBox
            {
                ClassName = d.ClassName,
                Confidence = d.Confidence,
                X = d.BoundingBox.X,
                Y = d.BoundingBox.Y,
                Width = d.BoundingBox.Width,
                Height = d.BoundingBox.Height
            }).ToList()
        };

        await AddReportAsync(new HarvestReportRecord
        {
            Id = $"MH-{DateTime.Now:yyyyMMdd-HHmmss}",
            DateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            Area = area,
            Duration = "00:00:00",
            Detections = result.DetectionCount,
            Priority = result.OverallSeverity,
            OperatorNote = recommendations.FirstOrDefault() ?? "Analisis otomatis",
            AiModelUsed = IsYoloOptionEnabled ? "YOLOv8n ONNX" : "OpenCV HSV fallback",
            HealthyPercentage = result.HealthyPercentage,
            StressedPercentage = result.StressedPercentage,
            DroughtPercentage = result.DroughtPercentage,
            BareSoilPercentage = result.BareSoilPercentage,
            PriorityZonesJson = JsonSerializer.Serialize(result.Priorities)
        });

        return result;
    }

    public HarvestValidationResult ValidateWithGroundTruth(HarvestAnalysisResult? analysis, IReadOnlyList<HarvestGroundTruthSample> samples, double stressedThresholdPercent = 35)
    {
        if (analysis == null || samples.Count == 0) return new HarvestValidationResult { Summary = "Tidak ada data validasi" };
        var priorityZones = analysis.Priorities.ToDictionary(p => (p.Row, p.Col));
        var matched = samples.Count(sample =>
        {
            var expectedStress = sample.SoilMoisturePercent < stressedThresholdPercent;
            var predictedStress = priorityZones.ContainsKey((sample.ZoneRow, sample.ZoneCol));
            return expectedStress == predictedStress;
        });
        return new HarvestValidationResult
        {
            TotalSamples = samples.Count,
            MatchedSamples = matched,
            MatchPercentage = matched * 100.0 / samples.Count,
            Summary = $"Kesesuaian validasi: {matched * 100.0 / samples.Count:F1}% ({matched}/{samples.Count})"
        };
    }

    public async Task<string> ExportAnalysisJsonAsync(HarvestAnalysisResult result, string baseName)
    {
        return await _fileService.ExportToDownloadsAsync($"{baseName}.json", JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }

    public async Task<string> ExportAnalysisCsvAsync(HarvestAnalysisResult result, string baseName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Priority,Row,Col,Latitude,Longitude,Severity,Recommendation");
        foreach (var p in result.Priorities)
        {
            sb.AppendLine($"{p.Priority},{p.Row},{p.Col},{p.Latitude.ToString(CultureInfo.InvariantCulture)},{p.Longitude.ToString(CultureInfo.InvariantCulture)},{p.Severity},\"{p.Recommendation.Replace("\"", "''")}\"");
        }
        return await _fileService.ExportToDownloadsAsync($"{baseName}.csv", sb.ToString());
    }

    private static (double Latitude, double Longitude) EstimateZoneCoordinate(VegetationYoloAnalyzer.ZoneResult zone, IReadOnlyList<VegetationYoloAnalyzer.ZoneResult> zones, double centerLat, double centerLon)
    {
        if (Math.Abs(centerLat) < 0.000001 && Math.Abs(centerLon) < 0.000001)
        {
            return (centerLat, centerLon);
        }

        var rowCount = Math.Max(1, zones.Max(z => z.Row) + 1);
        var colCount = Math.Max(1, zones.Max(z => z.Col) + 1);

        // Represent the frame as an approximate 120 m x 120 m field footprint.
        var northMeters = ((rowCount - 1) / 2.0 - zone.Row) * (120.0 / rowCount);
        var eastMeters = (zone.Col - (colCount - 1) / 2.0) * (120.0 / colCount);

        var lat = centerLat + northMeters / 111_320.0;
        var lon = centerLon + eastMeters / (111_320.0 * Math.Max(Math.Cos(centerLat * Math.PI / 180.0), 0.1));
        return (lat, lon);
    }

    public async Task<string> ExportReportJsonAsync(HarvestReportRecord record, string baseName)
    {
        return await _fileService.ExportToDownloadsAsync($"{baseName}.json", JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }));
    }

    public async Task<string> ExportReportCsvAsync(HarvestReportRecord record, string baseName)
    {
        var header = "Id,DateTime,Area,Duration,Detections,Priority,AiModelUsed,Healthy,Stressed,Drought,BareSoil,Validation,TlogPath,OperatorNote";
        var row = $"{record.Id},{record.DateTime},{record.Area},{record.Duration},{record.Detections},{record.Priority},{record.AiModelUsed},{record.HealthyPercentage:F2},{record.StressedPercentage:F2},{record.DroughtPercentage:F2},{record.BareSoilPercentage:F2},\"{record.GroundTruthValidationSummary.Replace("\"", "''")}\",{record.TlogPath},\"{record.OperatorNote.Replace("\"", "''")}\"";
        return await _fileService.ExportToDownloadsAsync($"{baseName}.csv", header + Environment.NewLine + row);
    }

    public async Task<string> ExportReportPdfAsync(HarvestReportRecord record, string baseName)
    {
        var text = $"Pigeon Harvest Report{Environment.NewLine}ID: {record.Id}{Environment.NewLine}Date: {record.DateTime}{Environment.NewLine}Area: {record.Area}{Environment.NewLine}Detections: {record.Detections}{Environment.NewLine}Priority: {record.Priority}{Environment.NewLine}AI Model: {record.AiModelUsed}{Environment.NewLine}Crop Health: healthy={record.HealthyPercentage:F1}%, stressed={record.StressedPercentage:F1}%, drought={record.DroughtPercentage:F1}%, bare soil={record.BareSoilPercentage:F1}%{Environment.NewLine}Ground Truth: {record.GroundTruthValidationSummary}{Environment.NewLine}TLOG: {record.TlogPath}{Environment.NewLine}Note: {record.OperatorNote}{Environment.NewLine}";
        return await _fileService.ExportToDownloadsAsync($"{baseName}.pdf", text);
    }

    public async Task<string> ExportValidationCsvAsync(HarvestValidationResult validation, string baseName)
    {
        var csv = "TotalSamples,MatchedSamples,MatchPercentage,Summary" + Environment.NewLine +
                  $"{validation.TotalSamples},{validation.MatchedSamples},{validation.MatchPercentage:F2},\"{validation.Summary.Replace("\"", "''")}\"";
        return await _fileService.ExportToDownloadsAsync($"{baseName}-validation.csv", csv);
    }

    public async Task AttachTlogToLatestReportAsync(string tlogPath)
    {
        var current = (await GetReportsAsync()).ToList();
        if (current.Count == 0) return;
        current[0].TlogPath = tlogPath;
        var json = JsonSerializer.Serialize(current, new JsonSerializerOptions { WriteIndented = true });
        await _fileService.SaveTextFileAsync(ReportsIndexFile, json, ReportsSubfolder);
    }

    public async Task<IReadOnlyList<HarvestReportRecord>> GetReportsAsync()
    {
        var json = await _fileService.LoadTextFileAsync(ReportsIndexFile, ReportsSubfolder);
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<HarvestReportRecord>();
        try { return JsonSerializer.Deserialize<List<HarvestReportRecord>>(json) ?? new List<HarvestReportRecord>(); }
        catch { return Array.Empty<HarvestReportRecord>(); }
    }

    public async Task AddReportAsync(HarvestReportRecord report)
    {
        var current = (await GetReportsAsync()).ToList();
        current.Insert(0, report);
        var json = JsonSerializer.Serialize(current.Take(50).ToList(), new JsonSerializerOptions { WriteIndented = true });
        await _fileService.SaveTextFileAsync(ReportsIndexFile, json, ReportsSubfolder);
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;
        var baseDirectory = AppContext.BaseDirectory;
        var modelClassPairs = new[]
        {
            (Model: Path.Combine(baseDirectory, "Assets", "models", "yolov8n-agri.onnx"), Classes: Path.Combine(baseDirectory, "Assets", "models", "classes-yolov8n-agri-basic.txt")),
            (Model: Path.Combine(baseDirectory, "Assets", "models", "yolov8n.onnx"), Classes: Path.Combine(baseDirectory, "Assets", "models", "classes-yolov8n-coco.txt")),
            (Model: Path.Combine(baseDirectory, "Assets", "models", "moonharvest-v2.onnx"), Classes: Path.Combine(baseDirectory, "Assets", "models", "classes-id-v2.txt")),
            (Model: Path.Combine(baseDirectory, "yolov8n-agri.onnx"), Classes: Path.Combine(baseDirectory, "classes-yolov8n-agri-basic.txt")),
            (Model: Path.Combine(baseDirectory, "yolov8n.onnx"), Classes: Path.Combine(baseDirectory, "classes-yolov8n-coco.txt"))
        };
        var pair = modelClassPairs.FirstOrDefault(candidate => File.Exists(candidate.Model) && File.Exists(candidate.Classes));
        var modelPath = pair.Model;
        var classPath = pair.Classes;

        Serilog.Log.Information("[HarvestFunctionalService] BaseDirectory: {Dir}", baseDirectory);
        Serilog.Log.Information("[HarvestFunctionalService] Selected model: {Model}", modelPath ?? "<none>");
        Serilog.Log.Information("[HarvestFunctionalService] Selected classes: {Classes}", classPath ?? "<none>");

        if (!string.IsNullOrWhiteSpace(modelPath) && !string.IsNullOrWhiteSpace(classPath))
        {
            _analyzer.Initialize(modelPath, classPath);
            YoloStatusMessage = _analyzer.IsYoloInitialized
                ? $"YOLO model loaded: {Path.GetFileName(modelPath)}"
                : $"YOLO failed to load: {Path.GetFileName(modelPath)}";
            Serilog.Log.Information("[HarvestFunctionalService] YOLO init result: {Status}", YoloStatusMessage);
        }
        else
        {
            _analyzer.Initialize();
            YoloStatusMessage = "YOLO model not found; OpenCV fallback ready";
            Serilog.Log.Warning("[HarvestFunctionalService] {Status}", YoloStatusMessage);
        }
        _analyzer.UseYolo = IsYoloOptionEnabled;
        _initialized = true;
    }

    public void SetYoloOptionEnabled(bool enabled)
    {
        if (enabled)
        {
            // If we failed to load the YOLO model last time we initialised, try again now.
            // This lets the user recover by toggling the sidebar switch once the runtime/model
            // issue is resolved (e.g. after dropping in a supported yolov8n-agri.onnx).
            if (!_initialized || !_analyzer.IsYoloInitialized)
            {
                _initialized = false;
                EnsureInitialized();
            }
        }

        IsYoloOptionEnabled = enabled;
        _analyzer.UseYolo = enabled;
        YoloStatusMessage = enabled
            ? (IsYoloRuntimeReady ? "YOLO active" : "YOLO enabled, fallback active")
            : "YOLO off";
        Serilog.Log.Information("[HarvestFunctionalService] YOLO toggle = {Enabled}, runtime ready = {Ready}, status = {Status}",
            enabled, IsYoloRuntimeReady, YoloStatusMessage);
        YoloOptionChanged?.Invoke(this, enabled);
    }

    public void Dispose() => _analyzer.Dispose();
}
