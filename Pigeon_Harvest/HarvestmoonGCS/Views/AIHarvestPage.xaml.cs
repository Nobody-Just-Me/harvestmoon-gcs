using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Services;

namespace HarvestmoonGCS.Views;

public sealed partial class AIHarvestPage : Page
{
    private readonly HarvestFunctionalService? _harvestFunctionalService;
    private readonly IFileService? _fileService;
    private string? _selectedFramePath;
    private readonly Dictionary<string, string> _modelPathsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _classPathsByName = new(StringComparer.OrdinalIgnoreCase);

    public AIHarvestPage()
    {
        this.InitializeComponent();
        _harvestFunctionalService = App.Current.Services.GetService<HarvestFunctionalService>();
        _fileService = App.Current.Services.GetService<IFileService>();
        Loaded += AIHarvestPage_Loaded;
    }

    private void AIHarvestPage_Loaded(object sender, RoutedEventArgs e)
    {
        LoadRuntimeOptions();
        SyncRuntimeControls();
        RefreshRuntimeStatus();
    }

    private void LoadRuntimeOptions()
    {
        _modelPathsByName.Clear();
        _classPathsByName.Clear();

        var modelFiles = FindModelFiles();
        ModelComboBox.Items.Clear();
        foreach (var model in modelFiles)
        {
            var name = Path.GetFileName(model);
            _modelPathsByName[name] = model;
            ModelComboBox.Items.Add(name);
        }

        if (ModelComboBox.Items.Count == 0)
        {
            ModelComboBox.Items.Add("No ONNX model found");
            ModelComboBox.IsEnabled = false;
        }
        else
        {
            ModelComboBox.IsEnabled = true;
        }

        foreach (var classFile in FindClassFiles())
        {
            _classPathsByName[Path.GetFileName(classFile)] = classFile;
        }
    }

    private void SyncRuntimeControls()
    {
        if (_harvestFunctionalService == null)
        {
            return;
        }

        SelectComboItem(ModelComboBox, Path.GetFileName(_harvestFunctionalService.RuntimeModelPath));
        ClassFileTextBox.Text = Path.GetFileName(_harvestFunctionalService.RuntimeClassPath)
            ?? GuessClassFileName(Path.GetFileName(GetSelectedModelPath()));
        ConfidenceSlider.Value = Math.Round(_harvestFunctionalService.RuntimeConfidenceThreshold * 100);
        NmsSlider.Value = Math.Round(_harvestFunctionalService.RuntimeNmsThreshold * 100);
        UpdateThresholdLabels();
        RefreshClassLabels();
    }

    private void RefreshRuntimeStatus()
    {
        if (_harvestFunctionalService == null)
        {
            RuntimeStatusText.Text = "Service missing";
            TestStatusValue.Text = "Unavailable";
            return;
        }

        RuntimeStatusText.Text = _harvestFunctionalService.IsYoloRuntimeReady
            ? "YOLO ready"
            : _harvestFunctionalService.YoloStatusMessage;
        ClassCountText.Text = ReadClassCount(GetSelectedClassPath()).ToString();
    }

    private async void RunTestButton_Click(object sender, RoutedEventArgs e)
    {
        if (_harvestFunctionalService == null)
        {
            TestStatusValue.Text = "Service missing";
            return;
        }

        ApplyRuntimeSettings(showStatus: false);

        var sampleImagePath = !string.IsNullOrWhiteSpace(_selectedFramePath) && File.Exists(_selectedFramePath)
            ? _selectedFramePath
            : FindSampleImagePath();
        if (sampleImagePath == null)
        {
            TestStatusValue.Text = "No sample";
            RuntimeStatusText.Text = "Sample image not found";
            return;
        }

        RunTestButton.IsEnabled = false;
        TestStatusValue.Text = "Running";
        LatencyValue.Text = "-";
        DetectionValue.Text = "-";

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await _harvestFunctionalService.AnalyzeImageAsync(
                sampleImagePath,
                "MoonHarvest Sample Field",
                -7.2756,
                112.6426,
                80);
            stopwatch.Stop();

            RefreshRuntimeStatus();
            LatencyValue.Text = $"{stopwatch.ElapsedMilliseconds} ms";
            DetectionValue.Text = result?.DetectionCount.ToString() ?? "0";
            TestStatusValue.Text = result == null ? "Failed" : "Done";
            RuntimeStatusText.Text = result == null
                ? "Analysis failed"
                : $"{result.OverallSeverity} · {result.Priorities.Count} priority zones";
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[AIHarvestPage] Run test failed");
            TestStatusValue.Text = "Error";
            RuntimeStatusText.Text = ex.Message;
        }
        finally
        {
            RunTestButton.IsEnabled = true;
        }
    }

    private void UseCameraButton_Click(object sender, RoutedEventArgs e)
    {
        Frame?.Navigate(typeof(CameraPage));
    }

    private async void BenchmarkButton_Click(object sender, RoutedEventArgs e)
    {
        if (_harvestFunctionalService == null)
        {
            TestStatusValue.Text = "Service missing";
            return;
        }

        ApplyRuntimeSettings(showStatus: false);
        var imagePath = !string.IsNullOrWhiteSpace(_selectedFramePath) && File.Exists(_selectedFramePath)
            ? _selectedFramePath
            : FindSampleImagePath();

        if (imagePath == null)
        {
            TestStatusValue.Text = "No sample";
            RuntimeStatusText.Text = "Upload frame untuk benchmark";
            return;
        }

        BenchmarkButton.IsEnabled = false;
        TestStatusValue.Text = "Benchmark";
        RuntimeStatusText.Text = "Mengukur FPS YOLO...";

        try
        {
            var result = await _harvestFunctionalService.BenchmarkImageAsync(imagePath, iterations: 30);
            if (result == null)
            {
                TestStatusValue.Text = "Failed";
                RuntimeStatusText.Text = "Benchmark gagal";
                return;
            }

            TestStatusValue.Text = result.FramesPerSecond >= 15 ? "Pass" : "Below target";
            LatencyValue.Text = $"{result.AverageLatencyMs:F1} ms";
            DetectionValue.Text = $"{result.AverageDetections:F1} avg";
            RuntimeStatusText.Text = result.Summary;
            await _harvestFunctionalService.AttachYoloBenchmarkToLatestReportAsync(result);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[AIHarvestPage] Benchmark failed");
            TestStatusValue.Text = "Error";
            RuntimeStatusText.Text = ex.Message;
        }
        finally
        {
            BenchmarkButton.IsEnabled = true;
        }
    }

    private void ApplyRuntimeButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyRuntimeSettings(showStatus: true);
    }

    private void RuntimeSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        UpdateThresholdLabels();
    }

    private void PerformanceModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        switch (PerformanceModeComboBox.SelectedIndex)
        {
            case 0:
                ConfidenceSlider.Value = 30;
                NmsSlider.Value = 35;
                break;
            case 2:
                ConfidenceSlider.Value = 55;
                NmsSlider.Value = 50;
                break;
            default:
                ConfidenceSlider.Value = 40;
                NmsSlider.Value = 40;
                break;
        }

        UpdateThresholdLabels();
    }

    private void ModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var modelName = ModelComboBox.SelectedItem?.ToString();
        if (string.IsNullOrWhiteSpace(modelName) || modelName == "No ONNX model found")
        {
            return;
        }

        var className = GuessClassFileName(modelName);
        if (_classPathsByName.ContainsKey(className) || File.Exists(className))
        {
            ClassFileTextBox.Text = className;
        }

        RefreshClassLabels();
        RefreshRuntimeStatus();
    }

    private bool ApplyRuntimeSettings(bool showStatus)
    {
        if (_harvestFunctionalService == null)
        {
            RuntimeStatusText.Text = "Service missing";
            return false;
        }

        var modelPath = GetSelectedModelPath();
        var classPath = GetSelectedClassPath();
        var confidence = (float)(ConfidenceSlider.Value / 100.0);
        var nms = (float)(NmsSlider.Value / 100.0);

        if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
        {
            RuntimeStatusText.Text = "Model ONNX tidak ditemukan";
            return false;
        }

        if (string.IsNullOrWhiteSpace(classPath) || !File.Exists(classPath))
        {
            RuntimeStatusText.Text = "Class file tidak ditemukan";
            return false;
        }

        var ready = _harvestFunctionalService.ConfigureYoloRuntime(modelPath, classPath, confidence, nms);
        RefreshRuntimeStatus();
        RefreshClassLabels();

        if (showStatus)
        {
            TestStatusValue.Text = ready ? "Applied" : "Fallback";
            RuntimeStatusText.Text = ready
                ? $"Runtime aktif: {Path.GetFileName(modelPath)}"
                : _harvestFunctionalService.YoloStatusMessage;
        }

        return ready;
    }

    private async void UploadFrameButton_Click(object sender, RoutedEventArgs e)
    {
        if (_fileService == null)
        {
            RuntimeStatusText.Text = "File picker tidak tersedia";
            return;
        }

        var path = await _fileService.PickFileAsync(new[] { ".jpg", ".jpeg", ".png", ".bmp" });
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        _selectedFramePath = path;
        SelectedFrameText.Text = $"Frame: {Path.GetFileName(path)}";
        TestStatusValue.Text = "Ready";
        RuntimeStatusText.Text = "Frame custom siap dianalisis";
    }

    private async void BrowseModelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_fileService == null)
        {
            RuntimeStatusText.Text = "File picker tidak tersedia";
            return;
        }

        var path = await _fileService.PickFileAsync(new[] { ".onnx" });
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var name = Path.GetFileName(path);
        _modelPathsByName[name] = path;
        if (!ModelComboBox.Items.Cast<object>().Any(item => string.Equals(item?.ToString(), name, StringComparison.OrdinalIgnoreCase)))
        {
            ModelComboBox.Items.Add(name);
        }

        ModelComboBox.SelectedItem = name;
        ClassFileTextBox.Text = GuessClassFileName(name);
        RuntimeStatusText.Text = $"Model custom dipilih: {name}";
    }

    private async void BrowseClassButton_Click(object sender, RoutedEventArgs e)
    {
        if (_fileService == null)
        {
            RuntimeStatusText.Text = "File picker tidak tersedia";
            return;
        }

        var path = await _fileService.PickFileAsync(new[] { ".txt", ".names" });
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var name = Path.GetFileName(path);
        _classPathsByName[name] = path;
        ClassFileTextBox.Text = name;
        RefreshClassLabels();
        RuntimeStatusText.Text = $"Class file custom dipilih: {name}";
    }

    private string? GetSelectedModelPath()
    {
        var selectedName = ModelComboBox.SelectedItem?.ToString();
        if (!string.IsNullOrWhiteSpace(selectedName) && _modelPathsByName.TryGetValue(selectedName, out var path))
        {
            return path;
        }

        return _harvestFunctionalService?.RuntimeModelPath;
    }

    private string? GetSelectedClassPath()
    {
        var raw = ClassFileTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = GuessClassFileName(Path.GetFileName(GetSelectedModelPath()));
        }

        if (!string.IsNullOrWhiteSpace(raw) && File.Exists(raw))
        {
            return raw;
        }

        if (!string.IsNullOrWhiteSpace(raw) && _classPathsByName.TryGetValue(raw, out var path))
        {
            return path;
        }

        return _harvestFunctionalService?.RuntimeClassPath;
    }

    private static IReadOnlyList<string> FindModelFiles()
    {
        return GetModelDirectories()
            .Where(Directory.Exists)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.onnx"))
            .OrderByDescending(path => GetModelPriority(Path.GetFileName(path)))
            .ThenBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> FindClassFiles()
    {
        return GetModelDirectories()
            .Where(Directory.Exists)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.txt")
                .Concat(Directory.EnumerateFiles(directory, "*.names")))
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<string> GetModelDirectories()
    {
        var baseDirectory = AppContext.BaseDirectory;
        yield return Path.Combine(baseDirectory, "Assets", "models");
        yield return Path.Combine(Directory.GetCurrentDirectory(), "HarvestmoonGCS", "Assets", "models");
    }

    private static string GuessClassFileName(string? modelFileName)
    {
        return modelFileName?.ToLowerInvariant() switch
        {
            string name when name.Contains("moonharvest-uav") || name.Contains("uav-det") => "classes-moonharvest-uav-det.txt",
            string name when name.Contains("moonharvest-health") || name.Contains("health-cls") => "classes-moonharvest-health.txt",
            string name when name.Contains("crop-weed") => "classes-crop-weed.txt",
            string name when name.Contains("agri") => "classes-yolov8n-agri-basic.txt",
            string name when name.Contains("320") => "classes-yolov8n-coco.txt",
            _ => "classes-yolov8n-coco.txt"
        };
    }

    private static int GetModelPriority(string? modelFileName)
    {
        var name = modelFileName?.ToLowerInvariant() ?? string.Empty;
        if (name.Contains("moonharvest-uav") || name.Contains("uav-det")) return 100;
        if (name.Contains("crop-weed")) return 80;
        if (name.Contains("agri")) return 70;
        if (name.Contains("320")) return 60;
        if (name.Contains("moonharvest-health") || name.Contains("health-cls")) return 20;
        return 0;
    }

    private static void SelectComboItem(ComboBox comboBox, string? itemText)
    {
        if (string.IsNullOrWhiteSpace(itemText) || comboBox.Items.Count == 0)
        {
            comboBox.SelectedIndex = comboBox.Items.Count > 0 ? 0 : -1;
            return;
        }

        for (var index = 0; index < comboBox.Items.Count; index++)
        {
            if (string.Equals(comboBox.Items[index]?.ToString(), itemText, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedIndex = index;
                return;
            }
        }

        comboBox.SelectedIndex = 0;
    }

    private void UpdateThresholdLabels()
    {
        if (ConfidenceLabelText == null || NmsLabelText == null)
        {
            return;
        }

        ConfidenceLabelText.Text = $"Confidence threshold {ConfidenceSlider.Value:F0}%";
        NmsLabelText.Text = $"NMS threshold {NmsSlider.Value:F0}%";
    }

    private void RefreshClassLabels()
    {
        var classPath = GetSelectedClassPath();
        var labels = !string.IsNullOrWhiteSpace(classPath) && File.Exists(classPath)
            ? File.ReadLines(classPath).Where(line => !string.IsNullOrWhiteSpace(line)).Take(32).ToList()
            : new List<string> { "crop", "weed" };

        ClassLabelsItems.ItemsSource = labels;
    }

    private static string? FindSampleImagePath()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDirectory, "Assets", "images", "test_webcam_fullhd.jpeg"),
            Path.Combine(baseDirectory, "Assets", "images", "Header.png"),
            Path.Combine(baseDirectory, "Assets", "images", "header-new.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "HarvestmoonGCS", "Assets", "images", "test_webcam_fullhd.jpeg")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static int ReadClassCount(string? selectedClassPath)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(selectedClassPath))
        {
            candidates.Add(selectedClassPath);
        }

        var baseDirectory = AppContext.BaseDirectory;
        candidates.AddRange(new[]
        {
            Path.Combine(baseDirectory, "Assets", "models", "classes-crop-weed.txt"),
            Path.Combine(baseDirectory, "Assets", "models", "classes-yolov8n-agri-basic.txt"),
            Path.Combine(Directory.GetCurrentDirectory(), "HarvestmoonGCS", "Assets", "models", "classes-crop-weed.txt")
        });

        var classFile = candidates.FirstOrDefault(File.Exists);
        if (classFile == null)
        {
            return 0;
        }

        return File.ReadLines(classFile).Count(line => !string.IsNullOrWhiteSpace(line));
    }
}
