using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Dispatching;
using Pigeon_Uno.Core.Services;
using Pigeon_Uno.Core.Models;
using Windows.Storage;
using Windows.Storage.Pickers;
using Pigeon_Uno.Controls;

namespace Pigeon_Uno.Views;

public sealed partial class CameraPage : Page
{
    private readonly ICameraService _cameraService;
    private readonly IVideoRecorderService _videoRecorderService;
    private DispatcherTimer _recordingTimer;
    private DateTime _recordingStartTime;
    private bool _isStreaming;
    private bool _isInitialized;
    private bool _serviceHandlersAttached;

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
        AttachServiceHandlers();
        LoadCameraSources();
    }

    private void CameraPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;

        DetachServiceHandlers();

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
            _cameraService.RecordingStatusChanged += OnRecordingStatusChanged;
            _cameraService.ConnectionError += OnConnectionError;
        }

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
            _cameraService.RecordingStatusChanged -= OnRecordingStatusChanged;
            _cameraService.ConnectionError -= OnConnectionError;
        }

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

    private void OnFrameReceived(object sender, byte[] frameData)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            VideoStream.UpdateFrame(frameData);
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
            // RTSP Logic - simulated for now or passed to service if supported
            string rtspUrl = RtspUrlTextBox.Text;
            if (string.IsNullOrEmpty(rtspUrl) || rtspUrl == "rtsp://")
            {
                ShowError("Masukkan URL RTSP yang valid.");
                return false;
            }
            
            // Assuming the service can handle URLs
            // For now just show connecting status
            await Task.Delay(1000); // Simulate connection
            OnStreamingStatusChanged(this, true);
            return true;
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
            // If it was RTSP simulation
            if (RtspTab.IsChecked == true)
            {
                OnStreamingStatusChanged(this, false);
            }
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
        if (_cameraService.IsRecording)
        {
            await _cameraService.StopRecordingAsync();
            ShowInfo("Recording stopped");
        }
        else
        {
            var success = await _cameraService.StartRecordingAsync();
            if (success)
            {
                ShowInfo("Recording started");
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
        else
        {
            var selectedSource = CameraSourceComboBox.SelectedItem as CameraSource;
            MetadataSourceText.Text = "SRC: LOCAL_WEBCAM";
            MetadataDeviceText.Text = $"DEV: {selectedSource?.Name ?? "None"}";
        }
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
        UpdateMetadata();
    }

    private void RtspTab_Checked(object sender, RoutedEventArgs e)
    {
        if (LocalConfigGrid != null) LocalConfigGrid.Visibility = Visibility.Collapsed;
        if (RtspConfigGrid != null) RtspConfigGrid.Visibility = Visibility.Visible;
        UpdateMetadata();
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
