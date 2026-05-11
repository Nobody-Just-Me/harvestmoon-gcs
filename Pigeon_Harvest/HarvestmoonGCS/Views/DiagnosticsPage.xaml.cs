using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Services;
using Serilog;
using Windows.UI;

namespace HarvestmoonGCS.Views;

public sealed partial class DiagnosticsPage : Page
{
    private readonly IDiagnosticsService _diagnosticsService;
    private readonly DispatcherTimer _refreshTimer;

    public DiagnosticsPage()
    {
        this.InitializeComponent();
        
        // Get or create diagnostics service
        _diagnosticsService = App.Current.Services.GetService(typeof(IDiagnosticsService)) as IDiagnosticsService
                             ?? new DiagnosticsService();
        
        // Setup auto-refresh timer
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        
        Loaded += DiagnosticsPage_Loaded;
        Unloaded += DiagnosticsPage_Unloaded;
    }

    private void DiagnosticsPage_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshAllData();
        _refreshTimer.Start();
    }

    private void DiagnosticsPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _refreshTimer.Stop();
    }

    private void RefreshTimer_Tick(object? sender, object e)
    {
        RefreshAllData();
    }

    private void RefreshAllData()
    {
        try
        {
            // Update health status
            var health = _diagnosticsService.GetSystemHealth();
            UpdateHealthDisplay(health);

            // Update system information
            var diagnosticInfo = _diagnosticsService.GetDiagnosticInfo();
            UpdateSystemInfo(diagnosticInfo);

            // Update performance metrics
            var metrics = _diagnosticsService.GetPerformanceMetrics();
            UpdatePerformanceMetrics(metrics);

            // Update error list
            var errors = _diagnosticsService.GetRecentErrors(20);
            ErrorsListView.ItemsSource = errors;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to refresh diagnostics data");
        }
    }

    private void UpdateHealthDisplay(SystemHealthStatus health)
    {
        HealthStatusText.Text = health.OverallHealth.ToString();
        LastCheckText.Text = health.LastCheck.ToString("yyyy-MM-dd HH:mm:ss");

        // Update indicator color
        var color = health.OverallHealth switch
        {
            HealthLevel.Good => Color.FromArgb(255, 76, 175, 80),      // Green
            HealthLevel.Warning => Color.FromArgb(255, 255, 152, 0),   // Orange
            HealthLevel.Error => Color.FromArgb(255, 244, 67, 54),     // Red
            HealthLevel.Critical => Color.FromArgb(255, 183, 28, 28),  // Dark Red
            _ => Color.FromArgb(255, 158, 158, 158)                    // Gray
        };

        HealthIndicator.Fill = new SolidColorBrush(color);
    }

    private void UpdateSystemInfo(DiagnosticInfo info)
    {
        AppVersionText.Text = info.AppVersion;
        PlatformText.Text = info.Platform;
        OsVersionText.Text = info.OsVersion;
        MemoryUsageText.Text = $"{info.MemoryUsageMB} MB";
        CpuUsageText.Text = $"{info.CpuUsagePercent:F2}%";
        ThreadCountText.Text = info.ThreadCount.ToString();
        UptimeText.Text = FormatTimeSpan(info.Uptime);
    }

    private void UpdatePerformanceMetrics(PerformanceMetrics metrics)
    {
        FpsText.Text = $"{metrics.AverageFps:F2}";
        ConnectionsText.Text = metrics.ActiveConnections.ToString();
        TelemetryRateText.Text = $"{metrics.TelemetryMessagesPerSecond} msg/s";
    }

    private void RefreshHealth_Click(object sender, RoutedEventArgs e)
    {
        RefreshAllData();
        ShowSuccess("Health status refreshed");
    }

    private async void ExportDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var exportPath = await _diagnosticsService.ExportLogsAsync();
            ShowSuccess($"Diagnostic report exported to:\n{exportPath}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to export diagnostics");
            ShowError("Failed to export diagnostic report");
        }
    }

    private async void ClearOldLogs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new ContentDialog
            {
                Title = "Clear Old Logs",
                Content = "This will delete log files older than 30 days. Continue?",
                PrimaryButtonText = "Clear",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await _diagnosticsService.ClearOldLogsAsync(30);
                ShowSuccess("Old logs cleared successfully");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to clear old logs");
            ShowError("Failed to clear old logs");
        }
    }

    private void ClearErrors_Click(object sender, RoutedEventArgs e)
    {
        ErrorsListView.ItemsSource = null;
        ShowSuccess("Error log cleared");
    }

    private void DebugMode_Changed(object sender, RoutedEventArgs e)
    {
        var enabled = DebugModeCheckBox.IsChecked == true;
        
        // TODO: Implement debug mode toggle
        Log.Information("Debug mode {Status}", enabled ? "enabled" : "disabled");
        
        ShowSuccess($"Debug mode {(enabled ? "enabled" : "disabled")}");
    }

    private string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan.TotalDays >= 1)
        {
            return $"{(int)timeSpan.TotalDays}d {timeSpan.Hours}h {timeSpan.Minutes}m";
        }
        else if (timeSpan.TotalHours >= 1)
        {
            return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m {timeSpan.Seconds}s";
        }
        else
        {
            return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
        }
    }

    private async void ShowSuccess(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Success",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private async void ShowError(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Error",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }
}
