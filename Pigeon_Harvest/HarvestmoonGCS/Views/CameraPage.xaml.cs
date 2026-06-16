using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Dispatching;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using HarvestmoonGCS.Controls;

namespace HarvestmoonGCS.Views;

public sealed partial class CameraPage : Page
{
    private readonly ICameraService _cameraService;
    private readonly IVideoRecorderService _videoRecorderService;
    private readonly IFileService? _fileService;
    private readonly IncidentTimelineService? _timelineService;
    private readonly HarvestFunctionalService? _harvestFunctionalService;
    private DispatcherTimer _recordingTimer;
    private DateTime _recordingStartTime;
    private bool _isStreaming;
    private bool _isInitialized;
    private bool _serviceHandlersAttached;
    private readonly CameraYoloProcessor _yoloProcessor = new();
    private bool _yoloReady;
    private string _lastYoloSummary = "YOLO: IDLE";
    private bool _vegetationOverlayEnabled = true;
    private float _minConf = 0.3f;
    private string? _lastClassifySource;
    private string? _lastClassifyModel;

    public CameraPage()
    {
        Serilog.Log.Information("[CameraPage] ========== CONSTRUCTOR STARTED ==========");
        
        try
        {
            this.InitializeComponent();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[CameraPage] ✗ FATAL: InitializeComponent failed!");
            throw;
        }
        
        // Get services from DI container
        try
        {
            _cameraService = App.GetService<ICameraService>();
            _videoRecorderService = App.GetService<IVideoRecorderService>();
            _fileService = App.Current.Services.GetService<IFileService>();
            _timelineService = App.Current.Services.GetService<IncidentTimelineService>();
            _harvestFunctionalService = App.Current.Services.GetService<HarvestFunctionalService>();
            
            if (_cameraService == null)
            {
                Serilog.Log.Error("[CameraPage] ✗ ERROR: ICameraService is null!");
                return;
            }
            
            InitializeServices();

            this.Loaded += CameraPage_Loaded;
            this.Unloaded += CameraPage_Unloaded;
            this.SizeChanged += CameraPage_SizeChanged;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[CameraPage] ✗ ERROR initializing services");
        }
    }

    private void CameraPage_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyTabletLayout(ActualWidth);

        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        EnsureYoloInitialized();
        AttachServiceHandlers();
        LoadCameraSources();

        // Sync button state if a demo stream was already started before this page opened
        if (_cameraService != null && _cameraService.IsStreaming)
            OnStreamingStatusChanged(this, true);
    }

    private void CameraPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;

        DetachServiceHandlers();
        _yoloProcessor.Dispose();
        _yoloReady = false;

        if (_recordingTimer != null)
        {
            _recordingTimer.Stop();
        }
    }

    private void CameraPage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyTabletLayout(e.NewSize.Width);
    }

    private void ApplyTabletLayout(double width)
    {
        if (width <= 0)
        {
            return;
        }

        var compact = width < 820;
        CameraHudGrid.Margin = compact ? new Thickness(10, 56, 10, 10) : new Thickness(16, 68, 16, 16);
        PipMapBorder.Width = compact ? 168 : 208;
        PipMapBorder.Height = compact ? 126 : 156;
        CameraControlPanelHost.MaxWidth = compact ? 360 : 440;
        SelectionPanel.Padding = compact ? new Thickness(10) : new Thickness(12);
    }

    private void InitializeServices()
    {
        _recordingTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        VideoStream.ShowFps = true;
    }

    private void EnsureYoloInitialized()
    {
        _yoloReady = _yoloProcessor.Initialize();
        _lastYoloSummary = _yoloReady ? "YOLO: READY" : $"YOLO: {_yoloProcessor.Status}";
        if (YoloStatusText != null)
        {
            YoloStatusText.Text = _lastYoloSummary;
        }
    }

    private void AttachServiceHandlers()
    {
        if (_serviceHandlersAttached)
        {
            return;
        }

        if (_cameraService != null)
        {
            _cameraService.FrameReceived += OnFrameReceived;
            _cameraService.StreamingStatusChanged += OnStreamingStatusChanged;
            _cameraService.ConnectionError += OnConnectionError;

            // Subscribe to classification summary events if available
            if (_cameraService is PythonCameraService pcs)
            {
                pcs.ClassificationSummaryChanged += OnClassificationSummaryChanged;
            }
        }

        _videoRecorderService.RecordingStatusChanged += OnRecordingStatusChanged;
        _videoRecorderService.RecordingError += OnRecordingError;
        _recordingTimer.Tick += RecordingTimer_Tick;
        _serviceHandlersAttached = true;
    }

    private void DetachServiceHandlers()
    {
        if (!_serviceHandlersAttached)
        {
            return;
        }

        if (_cameraService != null)
        {
            _cameraService.FrameReceived -= OnFrameReceived;
            _cameraService.StreamingStatusChanged -= OnStreamingStatusChanged;
            _cameraService.ConnectionError -= OnConnectionError;

            if (_cameraService is PythonCameraService pcs)
            {
                pcs.ClassificationSummaryChanged -= OnClassificationSummaryChanged;
            }
        }

        _videoRecorderService.RecordingStatusChanged -= OnRecordingStatusChanged;
        _videoRecorderService.RecordingError -= OnRecordingError;
        _recordingTimer.Tick -= RecordingTimer_Tick;
        _serviceHandlersAttached = false;
    }

    private async void LoadCameraSources()
    {
        try
        {
            await _cameraService.InitializeAsync();
            var sources = await _cameraService.GetAvailableSourcesAsync();
            
            CameraSourceComboBox.ItemsSource = sources;
            
            if (sources.Count > 0)
            {
                CameraSourceComboBox.SelectedIndex = 0;
                if (_cameraService != null && !_cameraService.IsStreaming)
                {
                    foreach (var source in sources.Where(source => source.Type is CameraSourceType.LocalCamera or CameraSourceType.USB))
                    {
                        CameraSourceComboBox.SelectedItem = source;
                        if (await StartCamera())
                        {
                            break;
                        }
                    }
                }
            }
            else
            {
                ShowError("Tidak ada kamera lokal terdeteksi. Sambungkan kamera device atau gunakan RTSP Stream.");
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[CameraPage] ✗ ERROR loading camera sources");
        }
    }

    private async void OnFrameReceived(object sender, byte[] frameData)
    {
        if (_videoRecorderService.IsRecording)
        {
            _videoRecorderService.WriteFrame(frameData);
        }

        // Check if this is a classification stream — boxes already drawn on frame
        if (_cameraService is PythonCameraService pcs && pcs.IsClassificationStream)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                VideoStream.UpdateFrame(frameData);
                // Skip YOLO processor — classification boxes already drawn
            });
            return;
        }

        YoloFrameResult? yoloResult = null;
        if (_yoloReady)
        {
            try
            {
                yoloResult = await _yoloProcessor.ProcessAsync(frameData);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[CameraPage] YOLO inference error");
                yoloResult = null;
            }
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            if (yoloResult != null)
            {
                VideoStream.UpdateFrame(yoloResult.FrameData);
                VideoStream.SetDetectionOverlays(yoloResult.Detections);
                _lastYoloSummary = $"YOLO: {yoloResult.DetectionCount} | {yoloResult.Fps:F1} FPS | {yoloResult.Summary}";
                YoloStatusText.Text = _lastYoloSummary;
            }
            else
            {
                VideoStream.UpdateFrame(frameData);
                if (!_yoloReady)
                {
                    VideoStream.SetDetectionOverlays(Array.Empty<VideoDetectionOverlay>());
                }
            }
        });
    }

    private void OnStreamingStatusChanged(object sender, bool isStreaming)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _isStreaming = isStreaming;
            UpdateLiveStatus(isStreaming);
            UpdateControlStates();
            
            if (isStreaming)
            {
                StartStopButton.Content = "Hentikan Kamera";
            }
            else
            {
                StartStopButton.Content = "Mulai Kamera";
            }
        });
    }

    private void OnRecordingStatusChanged(object sender, bool isRecording)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (isRecording)
            {
                RecordButton.Content = "⏹️";
                _recordingStartTime = DateTime.Now;
                _recordingTimer.Start();
            }
            else
            {
                RecordButton.Content = "⏺️";
                _recordingTimer.Stop();
            }
        });
    }

    private void OnConnectionError(object sender, string error)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ShowError(error);
        });
    }

    private void OnRecordingError(object sender, string error)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _timelineService?.Add("recording", $"Recording error: {error}", "warning");
            ShowError(error);
        });
    }

    private void OnClassificationSummaryChanged(object sender, string summary)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _lastYoloSummary = $"Classification: {summary}";
            if (YoloStatusText != null)
            {
                YoloStatusText.Text = _lastYoloSummary;
            }
        });
    }

    private async void StartStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isStreaming)
        {
            await StopCamera();
        }
        else
        {
            await StartCamera();
        }
    }

    private async Task<bool> StartCamera()
    {
        if (RtspTab.IsChecked == true)
        {
            string rtspUrl = RtspUrlTextBox.Text;
            if (string.IsNullOrEmpty(rtspUrl) || rtspUrl == "rtsp://")
            {
                ShowError("Masukkan URL RTSP yang valid.");
                return false;
            }

            var connected = await _cameraService.StartCameraAsync(rtspUrl);
            if (!connected)
            {
                ShowError("Gagal membuka RTSP stream. Periksa URL, jaringan, dan kredensial kamera.");
            }

            return connected;
        }

        if (VideoFileTab.IsChecked == true)
        {
            var path = VideoFilePathTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            {
                ShowError("Pilih file video MP4/MOV/AVI yang valid.");
                return false;
            }

            // If PythonCameraService is available, use health classification stream
            if (_cameraService is PythonCameraService pcs)
            {
                var modelPath = PythonCameraService.ResolveHealthModelPath();
                if (!string.IsNullOrWhiteSpace(modelPath) && File.Exists(modelPath))
                {
                    _lastClassifySource = path;
                    _lastClassifyModel = modelPath;
                    var classifyConnected = await pcs.StartClassifyStreamAsync(
                        path, modelPath,
                        showOverlay: _vegetationOverlayEnabled,
                        minConf: _minConf);
                    if (!classifyConnected)
                    {
                        ShowError("Gagal memulai classification stream. Periksa model dan koneksi.");
                    }
                    _timelineService?.Add("camera", $"Video file classification started: {path}", classifyConnected ? "success" : "warning");
                    return classifyConnected;
                }
                else
                {
                    Serilog.Log.Warning("[CameraPage] Health model not found, falling back to regular stream");
                }
            }

            var connected = await _cameraService.StartCameraAsync(path);
            if (!connected)
            {
                ShowError("Gagal membuka file video. Periksa format video dan codec OpenCV.");
            }

            _timelineService?.Add("camera", $"Video file source opened: {path}", connected ? "success" : "warning");
            return connected;
        }

        if (PythonBridgeTab.IsChecked == true)
        {
            var source = PythonBridgeSourceTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(source))
            {
                source = "0";
            }

            var connected = await _cameraService.StartCameraAsync(source);
            _timelineService?.Add("camera", $"Python bridge source opened: {source}", connected ? "success" : "warning");
            return connected;
        }

        var selectedSource = CameraSourceComboBox.SelectedItem as CameraSource;
        if (selectedSource == null)
        {
            ShowError("Please select a camera source");
            return false;
        }

        try
        {
            var success = await _cameraService.StartCameraAsync(selectedSource.Id);
            _timelineService?.Add("camera", $"Local camera source opened: {selectedSource.Name}", success ? "success" : "warning");
            if (!success)
            {
                ShowError("Gagal memulai kamera. Periksa izin kamera atau pilih source lain.");
            }
            return success;
        }
        catch (Exception ex)
        {
            ShowError($"Error starting camera: {ex.Message}");
            return false;
        }
    }

    private async Task StopCamera()
    {
        try
        {
            await _cameraService.StopCameraAsync();
        }
        catch (Exception ex)
        {
            ShowError($"Error stopping camera: {ex.Message}");
        }
    }

    private async void ScreenshotButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var success = await _cameraService.TakePictureAsync();
            if (success)
            {
                _timelineService?.Add("snapshot", "Camera screenshot saved", "success");
            }
            if (success)
            {
                ShowInfo("Screenshot saved");
            }
            else
            {
                ShowError("Failed to take screenshot");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Error taking screenshot: {ex.Message}");
        }
    }

    private async void RecordButton_Click(object sender, RoutedEventArgs e)
    {
        if (_videoRecorderService.IsRecording)
        {
            var outputPath = _videoRecorderService.CurrentRecordingPath;
            await _videoRecorderService.StopRecordingAsync();
            if (!string.IsNullOrWhiteSpace(outputPath) && _harvestFunctionalService != null)
            {
                await _harvestFunctionalService.AttachVideoRecordingToLatestReportAsync(outputPath);
            }

            _timelineService?.Add("recording", $"Video recording stopped: {outputPath}", "success");
            ShowInfo($"Recording saved: {outputPath}");
        }
        else
        {
            if (!_isStreaming)
            {
                ShowError("Mulai kamera/video dulu sebelum recording.");
                return;
            }

            var outputPath = await _videoRecorderService.GetDefaultRecordingPathAsync();
            var success = await _videoRecorderService.StartRecordingAsync(outputPath, width: 1280, height: 720, fps: 30);
            if (success)
            {
                _timelineService?.Add("recording", $"Video recording started: {outputPath}", "success");
                ShowInfo($"Recording started: {outputPath}");
            }
            else
            {
                ShowError("Failed to start recording");
            }
        }
    }

    private void RefreshSourcesButton_Click(object sender, RoutedEventArgs e)
    {
        LoadCameraSources();
    }

    private void CameraSourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateMetadata();
    }

    private void RecordingTimer_Tick(object sender, object e)
    {
        // We can add a recording timer text in the HUD if needed, 
        // but it was removed in the new HUD to match PIGEON.
    }

    private void UpdateLiveStatus(bool isLive)
    {
        if (isLive)
        {
            LiveBadge.Visibility = Visibility.Visible;
            (Resources["LivePulseAnimation"] as Storyboard)?.Begin();
            MetadataPanel.Visibility = Visibility.Visible;
            UpdateMetadata();
        }
        else
        {
            LiveBadge.Visibility = Visibility.Collapsed;
            (Resources["LivePulseAnimation"] as Storyboard)?.Stop();
            MetadataPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateMetadata()
    {
        if (RtspTab.IsChecked == true)
        {
            MetadataSourceText.Text = "SRC: RTSP_STREAM";
            MetadataDeviceText.Text = $"URL: {RtspUrlTextBox.Text}";
        }
        else if (VideoFileTab?.IsChecked == true)
        {
            MetadataSourceText.Text = "SRC: VIDEO_FILE";
            MetadataDeviceText.Text = $"FILE: {System.IO.Path.GetFileName(VideoFilePathTextBox.Text)}";
        }
        else if (PythonBridgeTab?.IsChecked == true)
        {
            MetadataSourceText.Text = "SRC: PYTHON_BRIDGE";
            MetadataDeviceText.Text = $"SRC: {PythonBridgeSourceTextBox.Text}";
        }
        else
        {
            var selectedSource = CameraSourceComboBox.SelectedItem as CameraSource;
            MetadataSourceText.Text = "SRC: LOCAL_WEBCAM";
            MetadataDeviceText.Text = $"DEV: {selectedSource?.Name ?? "None"}";
        }

        YoloStatusText.Text = _lastYoloSummary;
    }

    private void UpdateControlStates()
    {
        ScreenshotButton.IsEnabled = _isStreaming;
        RecordButton.IsEnabled = _isStreaming;
    }

    private void TogglePanelButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectionPanel.Visibility == Visibility.Visible)
        {
            SelectionPanel.Visibility = Visibility.Collapsed;
            ToggleIcon.Glyph = "⌃";
        }
        else
        {
            SelectionPanel.Visibility = Visibility.Visible;
            ToggleIcon.Glyph = "⌄";
        }
    }

    private void LocalTab_Checked(object sender, RoutedEventArgs e)
    {
        if (LocalConfigGrid != null) LocalConfigGrid.Visibility = Visibility.Visible;
        if (RtspConfigGrid != null) RtspConfigGrid.Visibility = Visibility.Collapsed;
        if (VideoFileConfigGrid != null) VideoFileConfigGrid.Visibility = Visibility.Collapsed;
        if (PythonBridgeConfigGrid != null) PythonBridgeConfigGrid.Visibility = Visibility.Collapsed;
        UpdateMetadata();
    }

    private void RtspTab_Checked(object sender, RoutedEventArgs e)
    {
        if (LocalConfigGrid != null) LocalConfigGrid.Visibility = Visibility.Collapsed;
        if (RtspConfigGrid != null) RtspConfigGrid.Visibility = Visibility.Visible;
        if (VideoFileConfigGrid != null) VideoFileConfigGrid.Visibility = Visibility.Collapsed;
        if (PythonBridgeConfigGrid != null) PythonBridgeConfigGrid.Visibility = Visibility.Collapsed;
        UpdateMetadata();
    }

    private void VideoFileTab_Checked(object sender, RoutedEventArgs e)
    {
        if (LocalConfigGrid != null) LocalConfigGrid.Visibility = Visibility.Collapsed;
        if (RtspConfigGrid != null) RtspConfigGrid.Visibility = Visibility.Collapsed;
        if (VideoFileConfigGrid != null) VideoFileConfigGrid.Visibility = Visibility.Visible;
        if (PythonBridgeConfigGrid != null) PythonBridgeConfigGrid.Visibility = Visibility.Collapsed;
        UpdateMetadata();
    }

    private void PythonBridgeTab_Checked(object sender, RoutedEventArgs e)
    {
        if (LocalConfigGrid != null) LocalConfigGrid.Visibility = Visibility.Collapsed;
        if (RtspConfigGrid != null) RtspConfigGrid.Visibility = Visibility.Collapsed;
        if (VideoFileConfigGrid != null) VideoFileConfigGrid.Visibility = Visibility.Collapsed;
        if (PythonBridgeConfigGrid != null) PythonBridgeConfigGrid.Visibility = Visibility.Visible;
        UpdateMetadata();
    }

    private async void BrowseVideoFileButton_Click(object sender, RoutedEventArgs e)
    {
        if (_fileService == null)
        {
            ShowError("File picker tidak tersedia.");
            return;
        }

        var file = await _fileService.PickFileAsync(new[] { ".mp4", ".mov", ".avi", ".mkv" });
        if (!string.IsNullOrWhiteSpace(file))
        {
            VideoFilePathTextBox.Text = file;
            UpdateMetadata();
        }
    }

    // TASK-04: Vegetation Overlay toggle
    private async void VegetationOverlayToggle_Toggled(object sender, RoutedEventArgs e)
    {
        _vegetationOverlayEnabled = VegetationOverlayToggle.IsOn;
        // Restart classify stream with new overlay flag if active
        await RestartClassifyStreamIfActive();
    }

    // TASK-06: Confidence slider
    private void ConfidenceSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        _minConf = (float)e.NewValue;
        if (ConfidenceValueText != null)
            ConfidenceValueText.Text = $"{_minConf:F2}";
    }

    private async Task RestartClassifyStreamIfActive()
    {
        if (_cameraService is not PythonCameraService pcs) return;
        if (!pcs.IsClassificationStream || !_isStreaming) return;
        if (string.IsNullOrWhiteSpace(_lastClassifySource) || string.IsNullOrWhiteSpace(_lastClassifyModel)) return;

        await pcs.StartClassifyStreamAsync(
            _lastClassifySource, _lastClassifyModel,
            showOverlay: _vegetationOverlayEnabled,
            minConf: _minConf);
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

    private async void ShowInfo(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Info",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }
}
