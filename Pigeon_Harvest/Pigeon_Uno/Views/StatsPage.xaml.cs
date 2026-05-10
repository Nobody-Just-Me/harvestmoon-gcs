using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pigeon_Uno.ViewModels;
using Pigeon_Uno.Core.Services;
using Pigeon_Uno.Services;

namespace Pigeon_Uno.Views;

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
    private HarvestFunctionalService.HarvestAnalysisResult? _lastAnalysis;

    public StatsPage()
    {
        this.InitializeComponent();
        DataContext = App.Current.Services.GetService<StatsViewModel>();
        _harvestFunctionalService = App.Current.Services.GetService<HarvestFunctionalService>();
        _fileService = App.Current.Services.GetService<IFileService>();
    }

    private async void BrowseImageButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_fileService == null)
        {
            AnalysisStatusText.Text = "File service tidak tersedia.";
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
            AnalysisStatusText.Text = "Harvest analysis service tidak tersedia.";
            return;
        }

        AnalysisStatusText.Text = "Menganalisis citra UAV...";
        var result = await _harvestFunctionalService.AnalyzeImageAsync(
            ImagePathTextBox.Text,
            "Field Sector B · Bandung",
            -6.91124,
            107.61152,
            120);

        if (result == null)
        {
            AnalysisStatusText.Text = "Analisis gagal. Pastikan path citra UAV valid.";
            return;
        }

        _lastAnalysis = result;
        RenderAnalysis(result);
        AnalysisStatusText.Text = $"Analisis selesai · {result.TotalZones} zona · {result.IrrigationTaggedCount} titik irigasi";
    }

    private async void ExportJsonButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_harvestFunctionalService == null || _lastAnalysis == null)
        {
            AnalysisStatusText.Text = "Jalankan analisis terlebih dahulu.";
            return;
        }

        var path = await _harvestFunctionalService.ExportAnalysisJsonAsync(_lastAnalysis, $"harvest-analysis-{System.DateTime.Now:yyyyMMdd-HHmmss}");
        AnalysisStatusText.Text = $"JSON tersimpan: {path}";
    }

    private async void ExportCsvButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_harvestFunctionalService == null || _lastAnalysis == null)
        {
            AnalysisStatusText.Text = "Jalankan analisis terlebih dahulu.";
            return;
        }

        var path = await _harvestFunctionalService.ExportAnalysisCsvAsync(_lastAnalysis, $"harvest-analysis-{System.DateTime.Now:yyyyMMdd-HHmmss}");
        AnalysisStatusText.Text = $"CSV tersimpan: {path}";
    }

    private void RenderAnalysis(HarvestFunctionalService.HarvestAnalysisResult result)
    {
        var affectedPercent = result.StressedPercentage + result.DroughtPercentage + result.BareSoilPercentage;
        var confidence = System.Math.Clamp(100 - result.DroughtPercentage, 0, 100);

        TotalDetectionText.Text = result.DetectionCount.ToString();
        AverageConfidenceText.Text = $"{confidence:F0}%";
        ImpactAreaText.Text = $"{affectedPercent * 0.024:F1} ha";
        HighPriorityText.Text = result.HighPriorityCount.ToString();
        HealthyDistributionText.Text = $"{result.HealthyPercentage:F1}%";
        StressDistributionText.Text = $"{result.StressedPercentage:F1}%";
        DroughtDistributionText.Text = $"{result.DroughtPercentage:F1}%";
        BareSoilDistributionText.Text = $"{result.BareSoilPercentage:F1}%";
        YoloDistributionText.Text = $"{result.DetectionCount} box";

        HealthyDistributionBar.Width = PercentToBarWidth(result.HealthyPercentage);
        StressDistributionBar.Width = PercentToBarWidth(result.StressedPercentage);
        DroughtDistributionBar.Width = PercentToBarWidth(result.DroughtPercentage);
        BareSoilDistributionBar.Width = PercentToBarWidth(result.BareSoilPercentage);
        YoloDistributionBar.Width = PercentToBarWidth(System.Math.Min(100, result.DetectionCount * 4));

        var recommendations = result.Recommendations.Count > 0
            ? result.Recommendations
            : new[] { "Tidak ada rekomendasi baru." }.AsEnumerable();

        RecommendationOneText.Text = recommendations.ElementAtOrDefault(0) ?? "Tidak ada rekomendasi baru.";
        RecommendationTwoText.Text = recommendations.ElementAtOrDefault(1) ?? "Pantau kondisi lahan pada penerbangan berikutnya.";
        RecommendationThreeText.Text = recommendations.ElementAtOrDefault(2) ?? "Export hasil untuk dokumentasi laporan.";

        var priorityItems = result.Priorities.Select(p => new PriorityZoneItem
        {
            PriorityText = $"P{p.Priority}",
            ZoneText = $"R{p.Row + 1} C{p.Col + 1}",
            CoordinateText = $"{p.Latitude:F6}, {p.Longitude:F6}",
            Severity = p.Severity
        }).ToList();

        PriorityZonesItemsControl.ItemsSource = priorityItems;
        PriorityZonesEmptyText.Visibility = priorityItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ValidationStatusText.Text = "Analisis tersedia. Masukkan sampel kelembaban tanah lalu klik Validasi.";
    }

    private void ValidateGroundTruthButton_Click(object sender, RoutedEventArgs e)
    {
        if (_harvestFunctionalService == null || _lastAnalysis == null)
        {
            ValidationStatusText.Text = "Jalankan analisis citra sebelum validasi.";
            return;
        }

        var samples = ParseGroundTruthSamples(GroundTruthSamplesTextBox.Text);
        if (samples.Count == 0)
        {
            ValidationStatusText.Text = "Format sampel belum valid. Gunakan row,col,moisture%, contoh: 0,1,28";
            return;
        }

        var validation = _harvestFunctionalService.ValidateWithGroundTruth(_lastAnalysis, samples);
        ValidationStatusText.Text = validation.Summary;
    }

    private void GroundTruthTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        GroundTruthSamplesTextBox.Text = "0,0,28\n0,1,34\n1,2,42\n2,1,31\n3,3,55";
        ValidationStatusText.Text = "Template sampel ground-truth siap diedit.";
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
