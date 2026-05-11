using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;
using HarvestmoonGCS.ViewModels;
using SkiaSharp;

namespace HarvestmoonGCS.Views
{
    public sealed partial class StatisticControl : UserControl
    {
        private readonly List<double> _timeData = new();
        private readonly List<double> _yawData = new();
        private readonly List<double> _pitchData = new();
        private readonly List<double> _rollData = new();

        private DispatcherTimer _updateTimer;
        private bool _isAutoUpdateEnabled = false;
        private bool _testMode = false; // Use real data from ViewModel
        private DateTime _testStartTime = DateTime.Now;

        public StatisticControl()
        {
            this.InitializeComponent();

            _updateTimer = new DispatcherTimer();
            _updateTimer.Interval = TimeSpan.FromMilliseconds(500);
            _updateTimer.Tick += UpdateTimer_Tick;

            this.Loaded += StatisticControl_Loaded;
            this.Unloaded += StatisticControl_Unloaded;
        }

        private void StatisticControl_Loaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("[StatisticControl] ========== CONTROL LOADED ==========");
            Debug.WriteLine($"[StatisticControl] DataContext type: {DataContext?.GetType().Name ?? "NULL"}");
            Debug.WriteLine($"[StatisticControl] Test mode: {_testMode}");
            
            // Initialize charts
            InitializeCharts();
            
            StartAutoUpdate();
            
            btn_start_stats.IsEnabled = false;
            btn_stop_stats.IsEnabled = true;
            
            Debug.WriteLine("[StatisticControl] Auto-update started");
        }
        
        private void InitializeCharts()
        {
            // Setup Yaw chart
            yawChart.Title = "Heading (Yaw)";
            yawChart.YAxisLabel = "Degrees (°)";
            yawChart.SetYRange(-180, 180);
            yawChart.AddSeries("Yaw", SKColor.Parse("#15008B"));
            
            // Setup Pitch & Roll chart
            pitchRollChart.Title = "Pitch & Roll";
            pitchRollChart.YAxisLabel = "Degrees (°)";
            pitchRollChart.SetYRange(-90, 90);
            pitchRollChart.AddSeries("Pitch", SKColors.Red);
            pitchRollChart.AddSeries("Roll", SKColors.Green);
            
            Debug.WriteLine("[StatisticControl] Charts initialized");
        }

        private void StatisticControl_Unloaded(object sender, RoutedEventArgs e)
        {
            StopAutoUpdate();
        }

        private void UpdateTimer_Tick(object sender, object e)
        {
            if (_isAutoUpdateEnabled)
            {
                AddToStatistik();
            }
        }

        public void AddToStatistik()
        {
            try
            {
                double time;
                float yaw, pitch, roll;

                if (_testMode)
                {
                    var elapsed = DateTime.Now - _testStartTime;
                    time = elapsed.TotalSeconds;

                    yaw = (float)(Math.Sin(elapsed.TotalSeconds * 0.5) * 180);
                    pitch = (float)(Math.Sin(elapsed.TotalSeconds * 1.2) * 45);
                    roll = (float)(Math.Cos(elapsed.TotalSeconds * 0.8) * 45);
                    
                    Debug.WriteLine($"[StatisticControl] TEST MODE data: Yaw={yaw:F1}°, Pitch={pitch:F1}°, Roll={roll:F1}°");
                }
                else
                {
                    if (DataContext is not StatsViewModel vm)
                    {
                        Debug.WriteLine($"[StatisticControl] WARNING: DataContext is not StatsViewModel! Type={DataContext?.GetType().Name ?? "NULL"}");
                        return;
                    }

                    var elapsed = DateTime.Now - _testStartTime; 
                    time = elapsed.TotalSeconds;

                    yaw = (float)vm.CurrentYaw;
                    pitch = (float)vm.CurrentPitch;
                    roll = (float)vm.CurrentRoll;
                    
                    if (_timeData.Count % 5 == 0) // Log every 5 samples
                    {
                        Debug.WriteLine($"[StatisticControl] REAL data from VM: Yaw={yaw:F1}°, Pitch={pitch:F1}°, Roll={roll:F1}°");
                    }
                }

                _timeData.Add(time);
                _yawData.Add(yaw);
                _pitchData.Add(pitch);
                _rollData.Add(roll);

                if (_timeData.Count > 300)
                {
                    _timeData.RemoveAt(0);
                    _yawData.RemoveAt(0);
                    _pitchData.RemoveAt(0);
                    _rollData.RemoveAt(0);
                }
                
                // Update charts
                yawChart.AddDataPoint("Yaw", yaw);
                pitchRollChart.AddDataPoint("Pitch", pitch);
                pitchRollChart.AddDataPoint("Roll", roll);

                if (_timeData.Count % 10 == 0)
                {
                    Debug.WriteLine($"[StatisticControl] Data points: {_timeData.Count}, Time: {time:F1}s, Yaw: {yaw:F1}°, Pitch: {pitch:F1}°, Roll: {roll:F1}°");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StatisticControl] Error: {ex.Message}");
            }
        }

        private void StartAutoUpdate_Click(object sender, RoutedEventArgs e)
        {
            StartAutoUpdate();
            btn_start_stats.IsEnabled = false;
            btn_stop_stats.IsEnabled = true;
        }

        private void StopAutoUpdate_Click(object sender, RoutedEventArgs e)
        {
            StopAutoUpdate();
            btn_start_stats.IsEnabled = true;
            btn_stop_stats.IsEnabled = false;
        }

        private void ResetStats_Click(object sender, RoutedEventArgs e)
        {
            _timeData.Clear();
            _yawData.Clear();
            _pitchData.Clear();
            _rollData.Clear();
            
            // Clear charts
            yawChart.Clear();
            pitchRollChart.Clear();
            
            if (_testMode)
            {
                _testStartTime = DateTime.Now;
            }
        }

        private async void ExportCSV_Click(object sender, RoutedEventArgs e)
        {
            try
            {
#if __ANDROID__
                // Android: Save to Downloads folder directly
                var downloadsPath = global::Android.OS.Environment.GetExternalStoragePublicDirectory(global::Android.OS.Environment.DirectoryDownloads)?.AbsolutePath;
                if (string.IsNullOrEmpty(downloadsPath))
                {
                    Debug.WriteLine("[StatisticControl] Export Error: Cannot access Downloads folder");
                    return;
                }
                
                var fileName = $"Statistics_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var filePath = System.IO.Path.Combine(downloadsPath, fileName);
                
                var lines = new List<string> { "Time (s),Heading (deg),Pitch (deg),Roll (deg)" };
                for (int i = 0; i < _timeData.Count; i++)
                {
                    lines.Add($"{_timeData[i]:F2},{_yawData[i]:F2},{_pitchData[i]:F2},{_rollData[i]:F2}");
                }
                
                await System.IO.File.WriteAllLinesAsync(filePath, lines);
                Debug.WriteLine($"[StatisticControl] Export Success: {lines.Count - 1} records saved to {filePath}");
#else
                // Windows/Desktop: Use file picker
                var savePicker = new FileSavePicker();
                savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                savePicker.FileTypeChoices.Add("CSV Files", new List<string>() { ".csv" });
                savePicker.SuggestedFileName = $"Statistics_{DateTime.Now:yyyyMMdd_HHmmss}";

#if WINDOWS
                var window = App.MainWindow;
                if (window != null)
                {
                    var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                    WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);
                }
#endif

                var file = await savePicker.PickSaveFileAsync();

                if (file != null)
                {
                    var lines = new List<string> { "Time (s),Heading (deg),Pitch (deg),Roll (deg)" };

                    for (int i = 0; i < _timeData.Count; i++)
                    {
                        lines.Add($"{_timeData[i]:F2},{_yawData[i]:F2},{_pitchData[i]:F2},{_rollData[i]:F2}");
                    }

                    await Windows.Storage.FileIO.WriteLinesAsync(file, lines);
                    Debug.WriteLine($"[StatisticControl] Export Success: {lines.Count - 1} records saved.");
                }
#endif
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StatisticControl] Export Error: {ex.Message}");
            }
        }

        public void StartAutoUpdate()
        {
            _isAutoUpdateEnabled = true;
            _updateTimer.Start();
        }

        public void StopAutoUpdate()
        {
            _isAutoUpdateEnabled = false;
            _updateTimer.Stop();
        }
    }
}
