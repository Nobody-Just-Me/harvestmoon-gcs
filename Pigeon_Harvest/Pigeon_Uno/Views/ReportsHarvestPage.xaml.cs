using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Extensions.DependencyInjection;
using Pigeon_Uno.Services;

namespace Pigeon_Uno.Views;

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
    }

    private static readonly IReadOnlyList<ReportEntry> SeedReports = new List<ReportEntry>
    {
        new() { Id = "MH-20260501-014", DateTime = "2026-05-01 12:30", Area = "Sector B · Bandung", Duration = "00:42:11", Detections = 23, Priority = "High" },
        new() { Id = "MH-20260430-013", DateTime = "2026-04-30 09:15", Area = "Sector A · Bandung", Duration = "00:36:02", Detections = 17, Priority = "Medium" },
        new() { Id = "MH-20260428-012", DateTime = "2026-04-28 15:48", Area = "Sector C · Garut", Duration = "00:51:30", Detections = 31, Priority = "High" },
        new() { Id = "MH-20260425-011", DateTime = "2026-04-25 10:02", Area = "Sector A · Bandung", Duration = "00:28:44", Detections = 9, Priority = "Low" },
        new() { Id = "MH-20260420-010", DateTime = "2026-04-20 14:11", Area = "Sector D · Lembang", Duration = "00:39:12", Detections = 22, Priority = "Medium" }
    };

    private readonly DispatcherTimer _feedbackTimer;
    private readonly HarvestFunctionalService? _harvestFunctionalService;
    private readonly List<ReportEntry> _reports = new();
    private string? _selectedId;
    private string _operatorNote = "Sektor B3 perlu inspeksi hama lanjutan minggu depan.";

    public ReportsHarvestPage()
    {
        this.InitializeComponent();

        _harvestFunctionalService = App.Current.Services.GetService<HarvestFunctionalService>();
        _reports.AddRange(SeedReports);
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
            ShowActionFeedback("Pilih report terlebih dahulu");
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

            ShowActionFeedback($"{label} report tersimpan: {path}");
            return;
        }

        var message = label switch
        {
            "PDF" => "PDF report di-generate",
            "CSV" => "CSV detections di-export",
            "JSON" => "JSON di-export",
            "Bagikan" => "Link sharing dibuat",
            "Kirim ke Koperasi" => "Report dikirim ke koperasi",
            _ => "Aksi dijalankan"
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
                Text = "Tidak ada misi yang cocok.",
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

        var priorityBadge = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Right,
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

        Grid.SetColumn(idText, 0);
        Grid.SetColumn(priorityBadge, 1);
        headGrid.Children.Add(idText);
        headGrid.Children.Add(priorityBadge);

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
            Text = $"{report.DateTime} · {report.Duration} · {report.Detections} deteksi",
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
        DetailPriorityText.Text = selected.Priority;

        DetailLocationText.Text = $"Lokasi: {selected.Area}";
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
            OperatorNote = _operatorNote
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
            Priority = record.Priority
        };
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
