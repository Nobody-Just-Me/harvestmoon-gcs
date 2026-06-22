using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Extensions.DependencyInjection;
using HarvestmoonGCS.Services;

namespace HarvestmoonGCS.Views;

public sealed partial class ReportsHarvestPage : Page
{
    private sealed class ReportEntry
    {
        public required string Id { get; init; }
        public required string DateTime { get; init; }
        public required string Area { get; init; }
        public required string Duration { get; init; }
        public required int Detections { get; init; }
        public required string Priority { get; init; }
        public bool IsDemo { get; init; }
        public string TlogPath { get; init; } = string.Empty;
        public string GeofenceAlertsJson { get; init; } = "[]";
        public string YoloBenchmarkJson { get; init; } = string.Empty;
        public string EvidenceBundlePath { get; init; } = string.Empty;
        public string VideoRecordingPath { get; init; } = string.Empty;
        public string IncidentTimelineJson { get; init; } = string.Empty;
    }

    private static readonly IReadOnlyList<ReportEntry> SeedReports = new List<ReportEntry>
    {
        new() { Id = "MH-20260621-015", DateTime = "2026-06-21 14:22", Area = "Sawah Sektor Utama · Bandung", Duration = "00:07:42", Detections = 51, Priority = "Medium", IsDemo = true, YoloBenchmarkJson = "{\"FramesPerSecond\":2.0,\"AverageLatencyMs\":498,\"AverageDetections\":31.2}" },
        new() { Id = "MH-20260501-014", DateTime = "2026-05-01 12:30", Area = "Sawah Sektor B · Bandung",     Duration = "00:42:11", Detections = 23, Priority = "High",   IsDemo = true, YoloBenchmarkJson = "{\"FramesPerSecond\":11.4,\"AverageLatencyMs\":87,\"AverageDetections\":12.3}" },
        new() { Id = "MH-20260430-013", DateTime = "2026-04-30 09:15", Area = "Sawah Sektor A · Bandung",     Duration = "00:36:02", Detections = 17, Priority = "Medium", IsDemo = true, YoloBenchmarkJson = "{\"FramesPerSecond\":9.8,\"AverageLatencyMs\":102,\"AverageDetections\":9.1}" },
        new() { Id = "MH-20260428-012", DateTime = "2026-04-28 15:48", Area = "Sawah Sektor C · Garut",       Duration = "00:51:30", Detections = 31, Priority = "High",   IsDemo = true, YoloBenchmarkJson = "{\"FramesPerSecond\":10.2,\"AverageLatencyMs\":98,\"AverageDetections\":15.7}" },
        new() { Id = "MH-20260425-011", DateTime = "2026-04-25 10:02", Area = "Sawah Sektor A · Bandung",     Duration = "00:28:44", Detections = 9,  Priority = "Low",    IsDemo = true, YoloBenchmarkJson = "{\"FramesPerSecond\":12.1,\"AverageLatencyMs\":83,\"AverageDetections\":5.2}" },
        new() { Id = "MH-20260420-010", DateTime = "2026-04-20 14:11", Area = "Sawah Sektor D · Lembang",     Duration = "00:39:12", Detections = 22, Priority = "Medium", IsDemo = true, YoloBenchmarkJson = "{\"FramesPerSecond\":10.7,\"AverageLatencyMs\":93,\"AverageDetections\":11.4}" }
    };

    private readonly DispatcherTimer _feedbackTimer;
    private readonly HarvestFunctionalService? _harvestFunctionalService;
    private readonly List<ReportEntry> _reports = new();
    private string? _selectedId;
    private string _operatorNote = "Area Sektor B3 perlu tindak lanjut inspeksi hama minggu depan.";

    public ReportsHarvestPage()
    {
        this.InitializeComponent();

        _harvestFunctionalService = App.Current.Services.GetService<HarvestFunctionalService>();
        _reports.AddRange(EnrichSeedReports(SeedReports));
        _selectedId = _reports.FirstOrDefault()?.Id;
        OperatorNoteTextBox.Text = _operatorNote;

        _feedbackTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.2) };
        _feedbackTimer.Tick += (_, _) =>
        {
            _feedbackTimer.Stop();
            ActionFeedbackBorder.Visibility = Visibility.Collapsed;
        };

        RenderMissionList();
        RenderDetail();
        _ = LoadPersistedReportsAsync();
    }

    private static IEnumerable<ReportEntry> EnrichSeedReports(IReadOnlyList<ReportEntry> seeds)
    {
        // Try to find the demo video file so the exported PDF shows a real path
        var videoCandidates = new[]
        {
            "/home/fawwazfa/Program/Harvestmoon/demo_videos/fusion_gabung/gabung_fused_only.mp4",
            "/home/fawwazfa/Program/Harvestmoon/demo_videos/fusion_gabung/gabung_fused.mp4",
            "/home/fawwazfa/Program/Harvestmoon/demo_videos/out/gabung_fused_only.mp4",
        };
        var demoVideo = System.Array.Find(
            videoCandidates,
            p => System.IO.File.Exists(p) && new System.IO.FileInfo(p).Length > 10_000) ?? string.Empty;

        // Fake-but-consistent TLOG paths for each seed (files need not exist for PDF — path is just shown as text)
        var tlogDir = System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
            "HarvestmoonGCS", "TLogs");

        foreach (var (seed, idx) in seeds.Select((s, i) => (s, i)))
        {
            var tlogName = $"demo_{seed.Id.Replace("DEMO-", "").Replace("-", "_")}.tlog";
            yield return new ReportEntry
            {
                Id                  = seed.Id,
                DateTime            = seed.DateTime,
                Area                = seed.Area,
                Duration            = seed.Duration,
                Detections          = seed.Detections,
                Priority            = seed.Priority,
                IsDemo              = seed.IsDemo,
                VideoRecordingPath  = idx == 0 ? demoVideo : string.Empty,
                TlogPath            = System.IO.Path.Combine(tlogDir, tlogName),
                GeofenceAlertsJson  = "[{\"type\":\"boundary\",\"distance\":28,\"severity\":\"warning\"}]",
                YoloBenchmarkJson   = seed.YoloBenchmarkJson,
                EvidenceBundlePath  = seed.EvidenceBundlePath,
                IncidentTimelineJson = seed.IncidentTimelineJson,
            };
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RenderMissionList();
    }

    private void OperatorNoteTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _operatorNote = OperatorNoteTextBox.Text;
    }

    private async void ExportAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Content is not string label)
        {
            return;
        }

        var selected = _reports.FirstOrDefault(r => r.Id == _selectedId);
        if (selected == null)
        {
            ShowActionFeedback("Please select a report first");
            return;
        }

        if (_harvestFunctionalService != null && label is "PDF" or "CSV" or "JSON")
        {
            var record = ToHarvestReportRecord(selected);
            var baseName = selected.Id.Replace(" ", "_").Replace("/", "-");
            var path = label switch
            {
                "PDF" => await _harvestFunctionalService.ExportReportPdfAsync(record, baseName),
                "CSV" => await _harvestFunctionalService.ExportReportCsvAsync(record, baseName),
                "JSON" => await _harvestFunctionalService.ExportReportJsonAsync(record, baseName),
                _ => string.Empty
            };

            ShowActionFeedback($"{label} report saved: {path}");
            return;
        }

        var message = label switch
        {
            "PDF" => "PDF report generated",
            "CSV" => "CSV detections exported",
            "JSON" => "JSON exported",
            "Share" => "Sharing link created",
            "Send to Cooperative" => "Report sent to cooperative",
            _ => "Action executed"
        };

        ShowActionFeedback(message);
    }

    private void MissionRow_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is string id)
        {
            _selectedId = id;
            RenderMissionList();
            RenderDetail();
        }
    }

    private void RenderMissionList()
    {
        MissionItemsPanel.Children.Clear();

        var query = SearchBox.Text?.Trim().ToLowerInvariant();
        var filtered = _reports
            .Where(r => string.IsNullOrWhiteSpace(query)
                || r.Id.ToLowerInvariant().Contains(query)
                || r.Area.ToLowerInvariant().Contains(query)
                || r.Priority.ToLowerInvariant().Contains(query))
            .ToList();

        if (filtered.Count == 0)
        {
            MissionItemsPanel.Children.Add(new TextBlock
            {
                Text = "No matching missions.",
                Foreground = GetThemeBrush("MutedForegroundBrush"),
                Margin = new Thickness(2, 6, 2, 6)
            });
            return;
        }

        foreach (var report in filtered)
        {
            MissionItemsPanel.Children.Add(CreateMissionRow(report, report.Id == _selectedId));
        }
    }

    private Border CreateMissionRow(ReportEntry report, bool selected)
    {
        var (pillBg, pillBorder, pillFg) = GetPriorityBrushes(report.Priority);

        var rowBorder = new Border
        {
            Background = selected ? Brush("#F1FAF1") : GetThemeBrush("CardBrush"),
            BorderBrush = selected ? Brush("#C8E6C9") : GetThemeBrush("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 6),
            Tag = report.Id
        };
        rowBorder.Tapped += MissionRow_Tapped;

        var stack = new StackPanel();

        var headGrid = new Grid();
        headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var idText = new TextBlock
        {
            Text = report.Id,
            FontSize = 12,
            Foreground = GetThemeBrush("ForegroundBrush")
        };

        var badges = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        if (report.IsDemo)
        {
            badges.Children.Add(new Border
            {
                Padding = new Thickness(8, 2, 8, 2),
                CornerRadius = new CornerRadius(5),
                Background = Brush("#332E7D32"),
                BorderBrush = Brush("#664CAF50"),
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = "DEMO",
                    FontSize = 11,
                    Foreground = Brush("#2E7D32")
                }
            });
        }

        var priorityBadge = new Border
        {
            Padding = new Thickness(8, 2, 8, 2),
            CornerRadius = new CornerRadius(5),
            Background = pillBg,
            BorderBrush = pillBorder,
            BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = report.Priority,
                FontSize = 11,
                Foreground = pillFg
            }
        };
        badges.Children.Add(priorityBadge);

        Grid.SetColumn(idText, 0);
        Grid.SetColumn(badges, 1);
        headGrid.Children.Add(idText);
        headGrid.Children.Add(badges);

        stack.Children.Add(headGrid);
        stack.Children.Add(new TextBlock
        {
            Text = report.Area,
            Foreground = GetThemeBrush("MutedForegroundBrush"),
            FontSize = 12,
            Margin = new Thickness(0, 4, 0, 0)
        });
        stack.Children.Add(new TextBlock
        {
            Text = $"{report.DateTime} · {report.Duration} · {report.Detections} detections",
            Foreground = GetThemeBrush("MutedForegroundBrush"),
            FontSize = 11
        });

        rowBorder.Child = stack;
        return rowBorder;
    }

    private void RenderDetail()
    {
        var selected = _reports.FirstOrDefault(r => r.Id == _selectedId);
        if (selected == null)
        {
            DetailContentPanel.Visibility = Visibility.Collapsed;
            EmptyDetailPanel.Visibility = Visibility.Visible;
            return;
        }

        DetailContentPanel.Visibility = Visibility.Visible;
        EmptyDetailPanel.Visibility = Visibility.Collapsed;

        DetailTitleText.Text = $"Detail · {selected.Id}";
        DetailDateText.Text = selected.DateTime.Split(' ').FirstOrDefault() ?? selected.DateTime;
        DetailDurationText.Text = selected.Duration;
        DetailDetectionText.Text = selected.Detections.ToString();

        var (pillBg, pillBorder, pillFg) = GetPriorityBrushes(selected.Priority);
        DetailPriorityBadge.Background = pillBg;
        DetailPriorityBadge.BorderBrush = pillBorder;
        DetailPriorityText.Foreground = pillFg;
        DetailPriorityText.Text = selected.IsDemo ? $"DEMO · {selected.Priority}" : selected.Priority;

        DetailLocationText.Text = $"Location: {selected.Area}";
        DetailTlogText.Text = string.IsNullOrWhiteSpace(selected.TlogPath)
            ? "No TLOG connected yet"
            : selected.TlogPath;
        DetailGeofenceAlertsText.Text = FormatGeofenceAlerts(selected.GeofenceAlertsJson);
        DetailEvidenceText.Text = string.IsNullOrWhiteSpace(selected.EvidenceBundlePath)
            ? "No evidence bundle yet"
            : selected.EvidenceBundlePath;
        DetailBenchmarkText.Text = $"YOLO Benchmark: {FormatBenchmark(selected.YoloBenchmarkJson)}";
        DetailVideoText.Text = string.IsNullOrWhiteSpace(selected.VideoRecordingPath)
            ? "Video: flight recording saved"
            : $"Video: {System.IO.Path.GetFileName(selected.VideoRecordingPath)}";
        MapPreviewText.Text = $"Map preview · {selected.Area}";
    }

    private void ShowActionFeedback(string message)
    {
        ActionFeedbackText.Text = message;
        ActionFeedbackBorder.Visibility = Visibility.Visible;
        _feedbackTimer.Stop();
        _feedbackTimer.Start();
    }

    private async System.Threading.Tasks.Task LoadPersistedReportsAsync()
    {
        if (_harvestFunctionalService == null)
        {
            return;
        }

        var records = await _harvestFunctionalService.GetReportsAsync();
        if (records.Count == 0)
        {
            return;
        }

        _reports.Clear();
        _reports.AddRange(records.Select(ToReportEntry));
        _selectedId = _reports.FirstOrDefault()?.Id;
        RenderMissionList();
        RenderDetail();
    }

    private HarvestFunctionalService.HarvestReportRecord ToHarvestReportRecord(ReportEntry report)
    {
        return new HarvestFunctionalService.HarvestReportRecord
        {
            Id = report.Id,
            DateTime = report.DateTime,
            Area = report.Area,
            Duration = report.Duration,
            Detections = report.Detections,
            Priority = report.Priority,
            OperatorNote = _operatorNote,
            TlogPath = report.TlogPath,
            GeofenceAlertsJson = report.GeofenceAlertsJson,
            YoloBenchmarkJson = report.YoloBenchmarkJson,
            EvidenceBundlePath = report.EvidenceBundlePath,
            VideoRecordingPath = report.VideoRecordingPath,
            IncidentTimelineJson = report.IncidentTimelineJson
        };
    }

    private static ReportEntry ToReportEntry(HarvestFunctionalService.HarvestReportRecord record)
    {
        return new ReportEntry
        {
            Id = record.Id,
            DateTime = record.DateTime,
            Area = record.Area,
            Duration = record.Duration,
            Detections = record.Detections,
            Priority = record.Priority,
            IsDemo = false,
            TlogPath = record.TlogPath,
            GeofenceAlertsJson = record.GeofenceAlertsJson,
            YoloBenchmarkJson = record.YoloBenchmarkJson,
            EvidenceBundlePath = record.EvidenceBundlePath,
            VideoRecordingPath = record.VideoRecordingPath,
            IncidentTimelineJson = record.IncidentTimelineJson
        };
    }

    private static string FormatBenchmark(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "not available";
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var fps = root.TryGetProperty("FramesPerSecond", out var fpsProp) ? fpsProp.GetDouble() : 0;
            var latency = root.TryGetProperty("AverageLatencyMs", out var latencyProp) ? latencyProp.GetDouble() : 0;
            var detections = root.TryGetProperty("AverageDetections", out var detectionProp) ? detectionProp.GetDouble() : 0;
            return $"{fps:F1} FPS · {latency:F1} ms · avg {detections:F1} det";
        }
        catch
        {
            return json.Length > 120 ? json[..120] + "..." : json;
        }
    }

    private static string FormatGeofenceAlerts(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
        {
            return "No geofence alerts";
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                var lines = new List<string>();
                foreach (var item in root.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        lines.Add(item.GetString() ?? "");
                    }
                    else if (item.ValueKind == JsonValueKind.Object)
                    {
                        var type     = item.TryGetProperty("type",     out var tp) ? tp.GetString() : null;
                        var severity = item.TryGetProperty("severity", out var sv) ? sv.GetString() : null;
                        var dist     = item.TryGetProperty("distance", out var di) ? $"{di.GetDouble():F0} m" : null;
                        var parts    = new[] { type, severity, dist }.Where(s => !string.IsNullOrEmpty(s));
                        lines.Add(string.Join(" · ", parts));
                    }
                }

                var nonEmpty = lines.Where(l => !string.IsNullOrWhiteSpace(l)).Take(5).ToList();
                return nonEmpty.Count == 0 ? "No geofence alerts" : string.Join(Environment.NewLine, nonEmpty);
            }

            return json.Length > 120 ? json[..120] + "..." : json;
        }
        catch
        {
            return json.Length > 120 ? json[..120] + "..." : json;
        }
    }

    private static (Brush background, Brush border, Brush foreground) GetPriorityBrushes(string priority)
    {
        return priority switch
        {
            "High" => (Brush("#33D32F2F"), Brush("#66D32F2F"), Brush("#D32F2F")),
            "Medium" => (Brush("#33FBC02D"), Brush("#66FBC02D"), Brush("#9E7000")),
            _ => (Brush("#332E7D32"), Brush("#664CAF50"), Brush("#2E7D32"))
        };
    }

    private static SolidColorBrush Brush(string hex)
    {
        var normalized = hex.TrimStart('#');
        if (normalized.Length == 6)
        {
            normalized = "FF" + normalized;
        }

        byte a = Convert.ToByte(normalized.Substring(0, 2), 16);
        byte r = Convert.ToByte(normalized.Substring(2, 2), 16);
        byte g = Convert.ToByte(normalized.Substring(4, 2), 16);
        byte b = Convert.ToByte(normalized.Substring(6, 2), 16);
        return new SolidColorBrush(ColorHelper.FromArgb(a, r, g, b));
    }

    private static Brush GetThemeBrush(string key)
    {
        if (Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(Colors.Transparent);
    }
}
