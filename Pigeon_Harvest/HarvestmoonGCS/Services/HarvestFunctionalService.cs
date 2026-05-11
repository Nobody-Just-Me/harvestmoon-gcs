using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using HarvestmoonGCS.Core.Helpers;
using HarvestmoonGCS.Core.Services;

namespace HarvestmoonGCS.Services;

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
    /// zone analysis pipeline. Uses SkiaSharp for decoding and preprocessing so
    /// it works even when native OpenCV libs are missing on the host.
    /// </summary>
    private const int AndroidRealtimeYoloMaxInputSize = 320;
    private const int AndroidYoloDetectionStride = 8;
    private const int AndroidFastGridCols = 8;
    private const int AndroidFastGridRows = 6;
    private DenseTensor<float>? _reusableTensor;
#if __ANDROID__
    private readonly object _androidDetectionLock = new();
    private int _androidDetectionFrameIndex;
    private List<HarvestDetectionBox> _lastAndroidDetectionBoxes = new();
#endif

    public Task<List<HarvestDetectionBox>> DetectInFrameAsync(byte[] frameData)
    {
        return Task.Run(() =>
        {
            var boxes = new List<HarvestDetectionBox>();
            if (frameData == null || frameData.Length == 0)
            {
                return boxes;
            }

#if __ANDROID__
            EnsureInitialized();
            if (IsYoloOptionEnabled &&
                _analyzer is { IsYoloInitialized: true, Detector: not null } &&
                IsAndroidRealtimeYoloModel())
            {
                if (!ShouldRunAndroidYoloFrame())
                {
                    return GetLastAndroidDetectionBoxes();
                }

                return DetectOnnxFrame(frameData, boxes);
            }

            // The bundled yolov8n ONNX is fixed at 640x640 and runs around 1 FPS on
            // low-power tablets. If no tablet-sized model is bundled, use the
            // lightweight realtime detector for the live dashboard instead.
            return DetectFastAndroidFrame(frameData);
#else
            EnsureInitialized();
            if (!IsYoloOptionEnabled || !_analyzer.IsYoloInitialized || _analyzer.Detector == null)
            {
                return boxes;
            }

            return DetectOnnxFrame(frameData, boxes);
#endif
        });
    }

    private List<HarvestDetectionBox> DetectOnnxFrame(byte[] frameData, List<HarvestDetectionBox> boxes)
    {
        if (_analyzer.Detector == null)
        {
            return boxes;
        }

            try
            {
                // Decode JPEG using SkiaSharp
                using var skBitmap = SkiaSharp.SKBitmap.Decode(frameData);
                if (skBitmap == null)
                {
                    return boxes;
                }

                var inputW = _analyzer.Detector.InputWidth;
                var inputH = _analyzer.Detector.InputHeight;
                using var resized = skBitmap.Resize(new SkiaSharp.SKImageInfo(inputW, inputH, SkiaSharp.SKColorType.Rgba8888), SkiaSharp.SKFilterQuality.Low);
                if (resized == null)
                {
                    return boxes;
                }

                // Reuse tensor to avoid GC allocation every frame
                if (_reusableTensor == null || _reusableTensor.Dimensions[2] != inputH || _reusableTensor.Dimensions[3] != inputW)
                {
                    _reusableTensor = new DenseTensor<float>(new[] { 1, 3, inputH, inputW });
                }
                var tensor = _reusableTensor;

                // Fill tensor through the dense backing buffer. This is much faster
                // on Android than the DenseTensor 4D indexer in a per-pixel loop.
                var pixelBytes = resized.Bytes; // RGBA8888 byte copy
                var tensorBuffer = tensor.Buffer.Span;
                int pixelCount = inputW * inputH;
                int greenOffset = pixelCount;
                int blueOffset = pixelCount * 2;
                const float scale = 0.00392156862f;

                for (int pixelIndex = 0, byteIndex = 0; pixelIndex < pixelCount; pixelIndex++, byteIndex += 4)
                {
                    tensorBuffer[pixelIndex] = pixelBytes[byteIndex] * scale;
                    tensorBuffer[greenOffset + pixelIndex] = pixelBytes[byteIndex + 1] * scale;
                    tensorBuffer[blueOffset + pixelIndex] = pixelBytes[byteIndex + 2] * scale;
                }

                // Run inference
                var inputs = new System.Collections.Generic.List<Microsoft.ML.OnnxRuntime.NamedOnnxValue>
                {
                    Microsoft.ML.OnnxRuntime.NamedOnnxValue.CreateFromTensor(_analyzer.Detector.InputName, tensor)
                };

                using var outputs = _analyzer.Detector.Session.Run(inputs);

                // Post-process with original frame dimensions for correct box scaling
                var detections = _analyzer.Detector.PostProcessPublic(outputs.Select(output => output.AsTensor<float>()).ToList(), skBitmap.Width, skBitmap.Height);
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

#if __ANDROID__
                SetLastAndroidDetectionBoxes(boxes);
#endif
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[HarvestFunctionalService] DetectInFrameAsync error");
            }
            return boxes;
    }

#if __ANDROID__
    private bool IsAndroidRealtimeYoloModel()
    {
        if (_analyzer.Detector == null)
        {
            return false;
        }

        return _analyzer.Detector.InputWidth <= AndroidRealtimeYoloMaxInputSize ||
            string.Equals(_analyzer.Detector.InputName, "input_1", StringComparison.OrdinalIgnoreCase);
    }

    private bool ShouldRunAndroidYoloFrame()
    {
        lock (_androidDetectionLock)
        {
            _androidDetectionFrameIndex++;
            return _androidDetectionFrameIndex == 1 ||
                ((_androidDetectionFrameIndex - 1) % AndroidYoloDetectionStride) == 0 ||
                _lastAndroidDetectionBoxes.Count == 0;
        }
    }

    private List<HarvestDetectionBox> GetLastAndroidDetectionBoxes()
    {
        lock (_androidDetectionLock)
        {
            return _lastAndroidDetectionBoxes
                .Select(CloneDetectionBox)
                .ToList();
        }
    }

    private void SetLastAndroidDetectionBoxes(List<HarvestDetectionBox> boxes)
    {
        lock (_androidDetectionLock)
        {
            _lastAndroidDetectionBoxes = boxes
                .Select(CloneDetectionBox)
                .ToList();
        }
    }

    private static HarvestDetectionBox CloneDetectionBox(HarvestDetectionBox box)
        => new()
        {
            ClassName = box.ClassName,
            Confidence = box.Confidence,
            X = box.X,
            Y = box.Y,
            Width = box.Width,
            Height = box.Height
        };

    private static List<HarvestDetectionBox> DetectFastAndroidFrame(byte[] frameData)
    {
        var boxes = new List<HarvestDetectionBox>();

        try
        {
            using var bitmap = SkiaSharp.SKBitmap.Decode(frameData);
            if (bitmap == null || bitmap.Width <= 0 || bitmap.Height <= 0)
            {
                return boxes;
            }

            var cellWidth = Math.Max(1, bitmap.Width / AndroidFastGridCols);
            var cellHeight = Math.Max(1, bitmap.Height / AndroidFastGridRows);
            var stepX = Math.Max(1, cellWidth / 8);
            var stepY = Math.Max(1, cellHeight / 8);

            for (var row = 0; row < AndroidFastGridRows; row++)
            {
                for (var col = 0; col < AndroidFastGridCols; col++)
                {
                    var x0 = col * cellWidth;
                    var y0 = row * cellHeight;
                    var x1 = col == AndroidFastGridCols - 1 ? bitmap.Width : Math.Min(bitmap.Width, x0 + cellWidth);
                    var y1 = row == AndroidFastGridRows - 1 ? bitmap.Height : Math.Min(bitmap.Height, y0 + cellHeight);

                    var healthy = 0;
                    var stressed = 0;
                    var soil = 0;
                    var samples = 0;

                    for (var y = y0; y < y1; y += stepY)
                    {
                        for (var x = x0; x < x1; x += stepX)
                        {
                            var pixel = bitmap.GetPixel(x, y);
                            var r = pixel.Red;
                            var g = pixel.Green;
                            var b = pixel.Blue;
                            samples++;

                            if (g > r + 18 && g > b + 12 && g > 55)
                            {
                                healthy++;
                            }
                            else if (r > 75 && g > 55 && b < 95 && Math.Abs(r - g) < 65)
                            {
                                stressed++;
                            }
                            else if (r > 45 && g > 35 && b < 80 && r >= g)
                            {
                                soil++;
                            }
                        }
                    }

                    if (samples == 0)
                    {
                        continue;
                    }

                    var best = Math.Max(healthy, Math.Max(stressed, soil));
                    var confidence = best / (float)samples;
                    if (confidence < 0.42f)
                    {
                        continue;
                    }

                    var className = healthy == best ? "healthy_crop" : stressed == best ? "crop_stress" : "dry_soil";
                    boxes.Add(new HarvestDetectionBox
                    {
                        ClassName = className,
                        Confidence = confidence,
                        X = x0,
                        Y = y0,
                        Width = Math.Max(1, x1 - x0),
                        Height = Math.Max(1, y1 - y0)
                    });
                }
            }

            return boxes
                .OrderByDescending(box => box.Confidence)
                .Take(12)
                .ToList();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[HarvestFunctionalService] DetectFastAndroidFrame error");
            return boxes;
        }
    }
#endif

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

        // On Android, assets are embedded in the APK and AppContext.BaseDirectory doesn't
        // contain them as regular files. We need to extract from the app's data directory
        // or use the Uno Platform asset resolution path.
        var assetSearchPaths = new List<string> { baseDirectory };

#if __ANDROID__
        // Android: assets get extracted to the app's internal files directory
        try
        {
            var ctx = Android.App.Application.Context;
            var filesDir = ctx.FilesDir?.AbsolutePath ?? "";
            var cacheDir = ctx.CacheDir?.AbsolutePath ?? "";
            assetSearchPaths.Add(filesDir);
            assetSearchPaths.Add(cacheDir);
            // Also try the app's native library directory parent
            var appInfo = ctx.ApplicationInfo;
            if (appInfo?.NativeLibraryDir != null)
            {
                assetSearchPaths.Add(Path.GetDirectoryName(appInfo.NativeLibraryDir) ?? "");
            }

            // Extract Android/tablet model candidates from APK assets to cache.
            // Small 320/INT8 models are preferred on low-power tablets; desktop
            // keeps the normal model order below.
            var androidModelAssets = new[]
            {
                ("yolov8n-agri-320.onnx", "classes-yolov8n-agri-basic.txt"),
                ("yolov8n-320.onnx", "classes-yolov8n-coco.txt"),
                ("yolov8n.onnx", "classes-yolov8n-coco.txt")
            };

            foreach (var (modelFile, classFile) in androidModelAssets)
            {
                var targetModelPath = Path.Combine(cacheDir, modelFile);
                var targetClassPath = Path.Combine(cacheDir, classFile);
                var shouldLogMissing = modelFile == "yolov8n.onnx";

                if (IsMissingOrEmpty(targetModelPath))
                {
                    ExtractAndroidAsset(ctx, $"Assets/models/{modelFile}", targetModelPath, shouldLogMissing);
                }
                if (IsMissingOrEmpty(targetClassPath))
                {
                    ExtractAndroidAsset(ctx, $"Assets/models/{classFile}", targetClassPath, shouldLogMissing);
                }
            }

            Android.Util.Log.Info("HarvestmoonGCS", $"[YOLO] CacheDir={cacheDir}");
            assetSearchPaths.Insert(0, cacheDir);
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("HarvestmoonGCS", $"[YOLO] Android asset extraction failed: {ex.Message}");
        }
#endif

        var modelClassPairs = new List<(string Model, string Classes)>();
        foreach (var dir in assetSearchPaths.Where(d => !string.IsNullOrWhiteSpace(d)))
        {
#if __ANDROID__
            modelClassPairs.Add((Path.Combine(dir, "Assets", "models", "yolov8n-agri-320.onnx"), Path.Combine(dir, "Assets", "models", "classes-yolov8n-agri-basic.txt")));
            modelClassPairs.Add((Path.Combine(dir, "Assets", "models", "yolov8n-320.onnx"), Path.Combine(dir, "Assets", "models", "classes-yolov8n-coco.txt")));
            modelClassPairs.Add((Path.Combine(dir, "yolov8n-agri-320.onnx"), Path.Combine(dir, "classes-yolov8n-agri-basic.txt")));
            modelClassPairs.Add((Path.Combine(dir, "yolov8n-320.onnx"), Path.Combine(dir, "classes-yolov8n-coco.txt")));
#endif
            modelClassPairs.Add((Path.Combine(dir, "Assets", "models", "yolov8n-agri.onnx"), Path.Combine(dir, "Assets", "models", "classes-yolov8n-agri-basic.txt")));
            modelClassPairs.Add((Path.Combine(dir, "Assets", "models", "yolov8n.onnx"), Path.Combine(dir, "Assets", "models", "classes-yolov8n-coco.txt")));
            modelClassPairs.Add((Path.Combine(dir, "yolov8n-agri.onnx"), Path.Combine(dir, "classes-yolov8n-agri-basic.txt")));
            modelClassPairs.Add((Path.Combine(dir, "yolov8n.onnx"), Path.Combine(dir, "classes-yolov8n-coco.txt")));
        }

        var pair = modelClassPairs.FirstOrDefault(candidate => File.Exists(candidate.Model) && File.Exists(candidate.Classes));
        var modelPath = pair.Model;
        var classPath = pair.Classes;

#if __ANDROID__
        Android.Util.Log.Info("HarvestmoonGCS", $"[YOLO] Selected model: {modelPath ?? "NONE"}, classes: {classPath ?? "NONE"}");
#endif
        Serilog.Log.Information("[HarvestFunctionalService] BaseDirectory: {Dir}", baseDirectory);
        Serilog.Log.Information("[HarvestFunctionalService] Selected model: {Model}", modelPath ?? "<none>");
        Serilog.Log.Information("[HarvestFunctionalService] Selected classes: {Classes}", classPath ?? "<none>");

        if (!string.IsNullOrWhiteSpace(modelPath) && !string.IsNullOrWhiteSpace(classPath))
        {
            _analyzer.Initialize(modelPath, classPath);
#if __ANDROID__
            if (_analyzer.IsYoloInitialized)
            {
                // Performance mode for low-power Android tablets: fewer weak candidates
                // means less post-processing/NMS work after every inference.
                _analyzer.Detector.SetConfidenceThreshold(0.4f);
                _analyzer.Detector.SetNmsThreshold(0.4f);
            }
#endif
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

#if __ANDROID__
    private static bool ExtractAndroidAsset(Android.Content.Context ctx, string assetPath, string targetPath, bool logMissing = true)
    {
        try
        {
            var dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            // Uno/Android asset packaging normalizes some filenames by replacing
            // hyphens with underscores, so probe both the source filename and the
            // packaged filename before giving up.
            var fileName = Path.GetFileName(assetPath);
            var packagedFileName = fileName.Replace("-", "_");
            var assetDirectory = Path.GetDirectoryName(assetPath)?.Replace("\\", "/") ?? string.Empty;
            var packagedAssetPath = string.IsNullOrWhiteSpace(assetDirectory)
                ? packagedFileName
                : $"{assetDirectory}/{packagedFileName}";

            var pathsToTry = new[]
            {
                assetPath,
                packagedAssetPath,
                assetPath.Replace("Assets/", "assets/"),
                packagedAssetPath.Replace("Assets/", "assets/"),
                assetPath.Replace("Assets/models/", "models/"),
                packagedAssetPath.Replace("Assets/models/", "models/"),
                $"models/{Path.GetFileName(assetPath)}",
                $"models/{packagedFileName}",
                fileName,
                packagedFileName
            }.Distinct().ToArray();

            foreach (var path in pathsToTry)
            {
                try
                {
                    using var assetStream = ctx.Assets?.Open(path);
                    if (assetStream == null) continue;
                    using var fileStream = File.Create(targetPath);
                    assetStream.CopyTo(fileStream);
                    Android.Util.Log.Info("HarvestmoonGCS", $"[YOLO] Extracted asset '{path}' -> {targetPath} ({new FileInfo(targetPath).Length} bytes)");
                    return true;
                }
                catch (Java.IO.FileNotFoundException)
                {
                    continue;
                }
            }

            // If none worked, list available assets for debugging
            if (logMissing)
            {
                try
                {
                    var list = ctx.Assets?.List("") ?? Array.Empty<string>();
                    Android.Util.Log.Warn("HarvestmoonGCS", $"[YOLO] Asset not found. Root assets: [{string.Join(", ", list.Take(20))}]");
                    var modelsList = ctx.Assets?.List("Assets/models") ?? ctx.Assets?.List("models") ?? Array.Empty<string>();
                    Android.Util.Log.Warn("HarvestmoonGCS", $"[YOLO] Models folder assets: [{string.Join(", ", modelsList)}]");
                }
                catch { }

                Android.Util.Log.Error("HarvestmoonGCS", $"[YOLO] Failed to find asset in any path variant for: {assetPath}");
            }

            return false;
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("HarvestmoonGCS", $"[YOLO] Extract error: {ex.Message}");
            return false;
        }
    }

    private static bool IsMissingOrEmpty(string path)
    {
        try
        {
            return !File.Exists(path) || new FileInfo(path).Length == 0;
        }
        catch
        {
            return true;
        }
    }
#endif

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
