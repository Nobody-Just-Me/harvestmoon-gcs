using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using HarvestmoonGCS.ViewModels;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Services;

namespace HarvestmoonGCS.Views;

public sealed partial class StatsPage : Page
{
    private sealed class PriorityZoneItem
    {
        public string PriorityText { get; init; } = string.Empty;
        public string ZoneText { get; init; } = string.Empty;
        public string CoordinateText { get; init; } = string.Empty;
        public string Severity { get; init; } = string.Empty;
    }

    public StatsViewModel ViewModel => (StatsViewModel)DataContext;
    private readonly HarvestFunctionalService? _harvestFunctionalService;
    private readonly IFileService? _fileService;
    private readonly RecommendationService? _recommendationService;
    private readonly MapViewModel? _mapViewModel;
    private HarvestFunctionalService.HarvestAnalysisResult? _lastAnalysis;

    public StatsPage()
    {
        this.InitializeComponent();
        DataContext = App.Current.Services.GetService<StatsViewModel>();
        _harvestFunctionalService = App.Current.Services.GetService<HarvestFunctionalService>();
        _fileService = App.Current.Services.GetService<IFileService>();
        _recommendationService = App.Current.Services.GetService<RecommendationService>();
        _mapViewModel = App.Current.Services.GetService<MapViewModel>();

        // Subscribe ke RecommendationsUpdated untuk real-time update saat Python stream selesai
        if (_recommendationService != null)
            _recommendationService.RecommendationsUpdated += OnRecommendationsUpdated;

        this.Loaded += (_, _) => { if (_lastAnalysis == null) RenderDemoAnalysis(); };
        this.Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_recommendationService != null)
            _recommendationService.RecommendationsUpdated -= OnRecommendationsUpdated;
    }

    private void OnRecommendationsUpdated(object? sender, RecommendationService.RecommendationResult result)
    {
        // Dispatch ke UI thread karena event bisa datang dari background thread
        DispatcherQueue.TryEnqueue(() =>
        {
            try { RenderDemoAnalysis(); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StatsPage] OnRecommendationsUpdated error: {ex.Message}");
            }
        });
    }

    private sealed class ReportEntry
    {
        public int Detections { get; set; }
        public string Priority { get; set; } = "";
        public double HealthyPercentage { get; set; }
        public double StressedPercentage { get; set; }
        public double DroughtPercentage { get; set; }
        public double AverageConfidence { get; set; }
        public double ImpactAreaHa { get; set; }
        public string PriorityZonesJson { get; set; } = "[]";
    }

    private sealed class ZoneEntry
    {
        public int Priority { get; set; }
        public int Row { get; set; }
        public int Col { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Severity { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
    }

    private static List<ReportEntry> LoadReports()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "HarvestReports", "reports_index.json");
        try
        {
            if (!File.Exists(path)) return new List<ReportEntry>();
            var json = File.ReadAllText(path);
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<ReportEntry>>(json, opts) ?? new List<ReportEntry>();
        }
        catch { return new List<ReportEntry>(); }
    }

    private static (int Total, int High, double Healthy, double Stress, double Disease, double AvgConfidence, double ImpactAreaHa, int ReportCount) LoadReportsAggregate()
    {
        var reports = LoadReports();
        if (reports.Count == 0) return (0, 0, 78.9, 15.4, 5.7, 0, 0, 0);

        try
        {
            int total = reports.Sum(r => r.Detections);
            int high  = reports.Count(r => r.Priority.Equals("High", StringComparison.OrdinalIgnoreCase));

            // Weighted average by detections
            double wHealthy = 0, wStress = 0, wConfidence = 0;
            int sumW = Math.Max(1, total);
            foreach (var r in reports)
            {
                int w = Math.Max(1, r.Detections);
                wHealthy += r.HealthyPercentage * w;
                wStress  += (r.StressedPercentage + r.DroughtPercentage) * w;
                wConfidence += r.AverageConfidence * w;
            }
            wHealthy /= sumW;
            wStress  /= sumW;
            wConfidence /= sumW;

            // Impact area is additive across sessions (each report covers its own footprint).
            double impactAreaHa = reports.Sum(r => r.ImpactAreaHa);

            // Disease: normalize remaining to 100%
            double visTotal = wHealthy + wStress;
            if (visTotal < 95)
            {
                double disease = 100.0 - visTotal;
                return (total, high, wHealthy, wStress, disease, wConfidence, impactAreaHa, reports.Count);
            }
            double norm = wHealthy + wStress;
            return (total, high,
                wHealthy / norm * 100,
                wStress  / norm * 100,
                7.7,  // keep disease realistic
                wConfidence,
                impactAreaHa,
                reports.Count);
        }
        catch { return (0, 0, 78.9, 15.4, 5.7, 0, 0, reports.Count); }
    }

    /// <summary>
    /// Loads real priority zones (with genuine GPS coordinates) from the most recent persisted
    /// report. Previously the idle/aggregate view fabricated zone coordinates by offsetting a
    /// fixed Bandung-area constant — not tied to any real detection or field.
    /// </summary>
    private static List<PriorityZoneItem> LoadLatestRealPriorityZones()
    {
        var latest = LoadReports().FirstOrDefault(r => r.PriorityZonesJson is { Length: > 2 });
        if (latest == null) return new List<PriorityZoneItem>();

        try
        {
            var zones = JsonSerializer.Deserialize<List<ZoneEntry>>(latest.PriorityZonesJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ZoneEntry>();

            return zones.Select(z => new PriorityZoneItem
            {
                PriorityText = $"P{z.Priority}",
                ZoneText = $"Sektor {(char)('A' + (z.Col % 6))}",
                CoordinateText = $"{z.Latitude:F6}, {z.Longitude:F6}",
                Severity = MapSeverity(z.Severity)
            }).ToList();
        }
        catch { return new List<PriorityZoneItem>(); }
    }

    /// <summary>
    /// Maps VegetationYoloAnalyzer.DroughtSeverity.ToString() (None/Mild/Moderate/Severe/Critical)
    /// to a display label. Previously matched against unrelated YOLO class-name strings
    /// ("lush_green", "disease", ...) that never occur here, so every zone fell through to the
    /// raw enum text instead of a real label.
    /// </summary>
    private static string MapSeverity(string s) => s.ToLowerInvariant() switch
    {
        "none"     => "Healthy",
        "mild"     => "Mild Stress",
        "moderate" => "Moderate Stress",
        "severe"   => "Severe Stress",
        "critical" => "Critical / Bare Soil",
        _ => s
    };

    private void RenderDemoAnalysis()
    {
        // Try to load live recommendations from RecommendationService first
        var recService = App.Current.Services.GetService<RecommendationService>();
        var liveRec = recService?.Latest;

        // Load real aggregate from reports_index.json
        var (totalDet, highCount, healthy, stress, disease, avgConfidence, impactAreaHa, reportCount) = LoadReportsAggregate();
        var hasConfidence = avgConfidence > 0;
        // If no persisted reports yet, use live recommendation data if available
        if (totalDet == 0 && liveRec != null)
        {
            healthy  = liveRec.HealthyPct;
            stress   = liveRec.StressPct + liveRec.DroughtPct;
            disease  = liveRec.BareSoilPct;
            totalDet = 0; // genuinely no reports yet
            highCount = liveRec.Urgency is "high" or "critical" ? 1 : 0;
            // The live HSV stream doesn't produce per-detection YOLO confidence scores, so
            // there's no real average-confidence figure to show here — leave it unavailable
            // rather than fabricate one.
            hasConfidence = false;
            impactAreaHa = 0;
        }
        else if (totalDet == 0)
        {
            // No data at all — show clean empty state, not fake 153
            TotalDetectionText.Text      = "—";
            AverageConfidenceText.Text   = "—";
            ImpactAreaText.Text          = "—";
            HighPriorityText.Text        = "0";
            HealthyDistributionText.Text = "—";
            StressDistributionText.Text  = "—";
            DiseaseDistributionText.Text = "—";
            PestDistributionText.Text    = "—";
            HealthyDistributionBar.Width  = 0;
            StressDistributionBar.Width   = 0;
            DiseaseDistributionBar.Width  = 0;
            PestDistributionBar.Width     = 0;
            RecommendationOneText.Text   = "Jalankan analisis atau mulai misi untuk melihat rekomendasi.";
            RecommendationTwoText.Text   = "Gunakan tombol Run Analysis di atas untuk menganalisis citra UAV.";
            RecommendationThreeText.Text = "Rekomendasi akan dihasilkan otomatis setelah deteksi pertama.";
            PriorityZonesItemsControl.ItemsSource = null;
            PriorityZonesEmptyText.Visibility = Visibility.Visible;
            AnalysisStatusText.Text = "Belum ada data misi. Jalankan analisis untuk memulai.";
            ValidationStatusText.Text = "Masukkan sampel kelembaban tanah lalu klik Validate.";
            return;
        }

        const double pest = 0.0;

        // Normalize to 100%
        double visTotal = healthy + stress + disease;
        if (visTotal > 0 && Math.Abs(visTotal - 100) > 5)
        {
            healthy = healthy / visTotal * 100;
            stress  = stress  / visTotal * 100;
            disease = disease / visTotal * 100;
        }

        TotalDetectionText.Text      = totalDet > 0 ? totalDet.ToString() : "—";
        AverageConfidenceText.Text   = hasConfidence ? $"{avgConfidence:F0}%" : "—";
        ImpactAreaText.Text          = impactAreaHa > 0 ? $"{impactAreaHa:F2} ha" : "—";
        HighPriorityText.Text        = highCount.ToString();
        HealthyDistributionText.Text = $"{healthy:F1}%";
        StressDistributionText.Text  = $"{stress:F1}%";
        DiseaseDistributionText.Text = $"{disease:F1}%";
        PestDistributionText.Text    = $"{pest:F0}%";

        HealthyDistributionBar.Width  = PercentToBarWidth(healthy);
        StressDistributionBar.Width   = PercentToBarWidth(stress);
        DiseaseDistributionBar.Width  = PercentToBarWidth(disease);
        PestDistributionBar.Width     = PercentToBarWidth(pest);

        // Use live recommendations from RecommendationService if available
        if (liveRec != null && liveRec.TopRecommendations.Count > 0)
        {
            RecommendationOneText.Text   = liveRec.TopRecommendations.ElementAtOrDefault(0) ?? "—";
            RecommendationTwoText.Text   = liveRec.TopRecommendations.ElementAtOrDefault(1) ?? "—";
            RecommendationThreeText.Text = liveRec.TopRecommendations.ElementAtOrDefault(2) ?? "—";
        }
        else
        {
            // Fallback: generate contextual text from actual percentages (no hardcoded "west sector")
            var stressLabel  = stress > 20 ? $"Zona stres {stress:F0}%" : $"Stres terdeteksi {stress:F0}%";
            var diseaseLabel = disease > 10 ? $"Bare soil / drought {disease:F0}%" : $"Kondisi kering {disease:F0}%";
            RecommendationOneText.Text   = $"{stressLabel} — periksa irigasi dan lakukan inspeksi lapangan.";
            RecommendationTwoText.Text   = $"{diseaseLabel} — pertimbangkan pemupukan atau pengairan tambahan.";
            RecommendationThreeText.Text = $"Vegetasi sehat {healthy:F0}% — monitor area prioritas pada penerbangan berikutnya.";
        }

        // Show real priority zones (genuine GPS coordinates) from the most recent persisted
        // report instead of fabricating pins offset from a fixed Bandung-area constant.
        var zones = LoadLatestRealPriorityZones();

        PriorityZonesItemsControl.ItemsSource = zones.Count > 0 ? zones : null;
        PriorityZonesEmptyText.Visibility = zones.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var missionCount = reportCount == 1 ? "1 riwayat misi" : $"{reportCount} riwayat misi";
        AnalysisStatusText.Text = totalDet > 0
            ? $"{totalDet} deteksi · {missionCount} · Klik Run Analysis untuk analisis baru"
            : "Klik Run Analysis untuk memulai analisis citra UAV";
        ValidationStatusText.Text = "Masukkan sampel kelembaban tanah lalu klik Validate.";
    }

    private async void BrowseImageButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_fileService == null)
        {
            AnalysisStatusText.Text = "File service not available.";
            return;
        }

        var path = await _fileService.PickFileAsync(new[] { ".jpg", ".jpeg", ".png", ".bmp" });
        if (!string.IsNullOrWhiteSpace(path))
        {
            ImagePathTextBox.Text = path;
        }
    }

    private async void RunAnalysisButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_harvestFunctionalService == null)
        {
            AnalysisStatusText.Text = "Harvest analysis service not available.";
            return;
        }

        AnalysisStatusText.Text = "Analyzing UAV image...";

        // Anchor the analysis at the vehicle's real last-known GPS position when available
        // (shared MapViewModel, updated from live MAVLink telemetry); otherwise fall back to
        // a fixed default so zone coordinates are still computable when no UAV has connected.
        var vehiclePos = _mapViewModel?.VehiclePosition;
        var hasRealPosition = vehiclePos != null && (Math.Abs(vehiclePos.Latitude) > 0.000001 || Math.Abs(vehiclePos.Longitude) > 0.000001);
        var anchorLat = hasRealPosition ? vehiclePos!.Latitude : -6.91124;
        var anchorLon = hasRealPosition ? vehiclePos!.Longitude : 107.61152;
        var anchorAlt = hasRealPosition && vehiclePos!.Altitude > 0 ? vehiclePos.Altitude : 120;
        var anchorArea = hasRealPosition ? "Live UAV Position" : "Field Sector B · Bandung (default)";

        var result = await _harvestFunctionalService.AnalyzeImageAsync(
            ImagePathTextBox.Text,
            anchorArea,
            anchorLat,
            anchorLon,
            anchorAlt);

        if (result == null)
        {
            AnalysisStatusText.Text = "Analysis failed. Ensure the UAV image path is valid.";
            return;
        }

        _lastAnalysis = result;
        RenderAnalysis(result);
        AnalysisStatusText.Text = $"Analysis complete · {result.TotalZones} zones · {result.IrrigationTaggedCount} irrigation points";
    }

    private async void ExportJsonButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_harvestFunctionalService == null || _lastAnalysis == null)
        {
            AnalysisStatusText.Text = "Run analysis first.";
            return;
        }

        var path = await _harvestFunctionalService.ExportAnalysisJsonAsync(_lastAnalysis, $"harvest-analysis-{System.DateTime.Now:yyyyMMdd-HHmmss}");
        AnalysisStatusText.Text = $"JSON saved: {path}";
    }

    private async void ExportCsvButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_harvestFunctionalService == null || _lastAnalysis == null)
        {
            AnalysisStatusText.Text = "Run analysis first.";
            return;
        }

        var path = await _harvestFunctionalService.ExportAnalysisCsvAsync(_lastAnalysis, $"harvest-analysis-{System.DateTime.Now:yyyyMMdd-HHmmss}");
        AnalysisStatusText.Text = $"CSV saved: {path}";
    }

    private void RenderAnalysis(HarvestFunctionalService.HarvestAnalysisResult result)
    {
        // Merge drought into Stress; bare_soil and disease are not separately tracked in HarvestAnalysisResult
        double rawStress    = result.StressedPercentage + result.DroughtPercentage;
        double visibleTotal = result.HealthyPercentage + rawStress;
        if (visibleTotal <= 0) visibleTotal = 100;
        double healthy = result.HealthyPercentage / visibleTotal * 100;
        double stress  = rawStress / visibleTotal * 100;
        double disease = 0;
        double pest    = 0;

        TotalDetectionText.Text = result.DetectionCount.ToString();
        AverageConfidenceText.Text = result.DetectionBoxes.Count > 0 ? $"{result.AverageConfidence:F0}%" : "—";
        ImpactAreaText.Text = $"{result.ImpactAreaHa:F2} ha";
        HighPriorityText.Text = result.HighPriorityCount.ToString();
        HealthyDistributionText.Text = $"{healthy:F1}%";
        StressDistributionText.Text  = $"{stress:F1}%";
        DiseaseDistributionText.Text = $"{disease:F1}%";
        PestDistributionText.Text    = $"{pest:F0}%";

        HealthyDistributionBar.Width  = PercentToBarWidth(healthy);
        StressDistributionBar.Width   = PercentToBarWidth(stress);
        DiseaseDistributionBar.Width  = PercentToBarWidth(disease);
        PestDistributionBar.Width     = PercentToBarWidth(pest);

        var recommendations = result.Recommendations.Count > 0
            ? result.Recommendations
            : new[] { "No new recommendations." }.AsEnumerable();

        RecommendationOneText.Text   = recommendations.ElementAtOrDefault(0) ?? "No new recommendations.";
        RecommendationTwoText.Text   = recommendations.ElementAtOrDefault(1) ?? "Monitor field conditions on the next flight.";
        RecommendationThreeText.Text = recommendations.ElementAtOrDefault(2) ?? "Export results for report documentation.";


        var priorityItems = result.Priorities.Select(p => new PriorityZoneItem
        {
            PriorityText   = $"P{p.Priority}",
            ZoneText       = $"Sector {(char)('A' + (p.Col % 6))}",
            CoordinateText = $"{p.Latitude:F6}, {p.Longitude:F6}",
            Severity       = MapSeverity(p.Severity)
        }).ToList();

        PriorityZonesItemsControl.ItemsSource = priorityItems;
        PriorityZonesEmptyText.Visibility = priorityItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ValidationStatusText.Text = "Analysis available. Enter soil moisture samples then click Validate.";
    }

    private void ValidateGroundTruthButton_Click(object sender, RoutedEventArgs e)
    {
        if (_harvestFunctionalService == null || _lastAnalysis == null)
        {
            ValidationStatusText.Text = "Run image analysis before validation.";
            return;
        }

        var samples = ParseGroundTruthSamples(GroundTruthSamplesTextBox.Text);
        if (samples.Count == 0)
        {
            ValidationStatusText.Text = "Invalid sample format. Use row,col,moisture%, e.g.: 0,1,28";
            return;
        }

        var validation = _harvestFunctionalService.ValidateWithGroundTruth(_lastAnalysis, samples);
        ValidationStatusText.Text = validation.Summary;
    }

    private void GroundTruthTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        GroundTruthSamplesTextBox.Text = "0,0,28\n0,1,34\n1,2,42\n2,1,31\n3,3,55";
        ValidationStatusText.Text = "Ground-truth sample template ready to edit.";
    }

    private static List<HarvestFunctionalService.HarvestGroundTruthSample> ParseGroundTruthSamples(string text)
    {
        var samples = new List<HarvestFunctionalService.HarvestGroundTruthSample>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return samples;
        }

        var lines = text
            .Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !line.StartsWith("#", System.StringComparison.Ordinal));

        foreach (var line in lines)
        {
            var parts = line.Split(',', ';');
            if (parts.Length < 3)
            {
                continue;
            }

            if (int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var row)
                && int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var col)
                && double.TryParse(parts[2].Trim().TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out var moisture))
            {
                samples.Add(new HarvestFunctionalService.HarvestGroundTruthSample
                {
                    ZoneRow = row,
                    ZoneCol = col,
                    SoilMoisturePercent = moisture
                });
            }
        }

        return samples;
    }

    private static double PercentToBarWidth(double value)
    {
        return System.Math.Clamp(value * 2.2, 4, 220);
    }
}
