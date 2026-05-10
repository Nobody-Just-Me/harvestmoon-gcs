using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Pigeon_Uno.Core.ViewModels;
using Pigeon_Uno.Core.Models;
using Pigeon_Uno.Core.Helpers;
using Pigeon_Uno.Core.Services.MavLink;
using Pigeon_Uno.Core.Services;

namespace Pigeon_Uno.Controls;

/// <summary>
/// Flight Control - parity dengan Pigeon WPF FlightControl (XAML + backend).
/// </summary>
public sealed partial class FlightControl : UserControl
{
    private FlightViewModel? _viewModel;
    private double _zoomLevel = 1.0;
    private const double ZoomStep = 0.2;
    private const double ZoomMin = 1.0;
    private const double ZoomMax = 4.0;

    /// <summary> Tipe koneksi dipilih dari combo (sama WPF: KONEKSI, UDP, WIFI, ..REFRESH..). </summary>
    private ConnectionType _selectedConnectionType = ConnectionType.UDP;
    /// <summary> Nama COM port yang dipilih (untuk Serial connection). </summary>
    private string _selectedSerialPort = "";
    
    /// <summary> Alert message queue for display. </summary>
    private readonly Queue<AlertMessage> _alertMessageQueue = new();
    /// <summary> Current active alerts being displayed. </summary>
    private readonly List<AlertMessage> _activeAlerts = new();
    /// <summary> Timer for alert message rotation. </summary>
    private DispatcherTimer? _alertRotationTimer;
    /// <summary> Previous telemetry state for alert triggering. </summary>
    private TelemetryState _previousState = new();
    private bool _isInitialized;
    private bool _isRefreshingPorts;
    private int _lastMapProviderIndex = -1;
    private TelemetryData? _telemetrySource;
    private DateTime _lastTelemetryUiUpdate = DateTime.MinValue;
    private const int TelemetryUiUpdateIntervalMs = 100;

    public FlightViewModel? ViewModel => _viewModel;

    public FlightControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        btn_conn.Click -= OnToggleConnection;
        btn_arm_disarm.Click -= OnArmDisarmClick;
        if (stream_panel_read_btn != null)
        {
            stream_panel_read_btn.Click -= ReadParams_Click;
        }
        btn_take_picture.Click -= OnTakePictureClick;
        btn_record.Click -= OnRecordClick;
        btn_livestream.Click -= OnLivestreamClick;
        if (btn_follow_wahana != null)
        {
            btn_follow_wahana.Click -= ToggleFollowWahana_Click;
        }

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        AttachTelemetrySource(null);

        if (cb_ports != null)
        {
            cb_ports.SelectionChanged -= ConnSelection_Changed;
        }

        if (cb_flight_map_type != null)
        {
            cb_flight_map_type.SelectionChanged -= FlightMapType_SelectionChanged;
        }

        // Stop alert rotation timer
        _alertRotationTimer?.Stop();
        _alertRotationTimer = null;
        _isInitialized = false;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isInitialized)
            return;

        _isInitialized = true;

        _viewModel = App.Current.Services.GetService<FlightViewModel>();
        if (_viewModel == null)
            return;

        DataContext = _viewModel;

        // ConnList sama WPF: KONEKSI, ..REFRESH.., WIFI, UDP
        PrepConnection();

        serverIP.Text = _viewModel.ConnectionAddress;
        udpPort.Text = _viewModel.ConnectionPort.ToString();

        // Connection button
        btn_conn.Click -= OnToggleConnection;
        btn_conn.Click += OnToggleConnection;

        // ARM / DISARM (sama WPF: ToggleArmDisarm)
        btn_arm_disarm.Click -= OnArmDisarmClick;
        btn_arm_disarm.Click += OnArmDisarmClick;

        // READ button (sama WPF: stream_panel_read_btn)
        if (stream_panel_read_btn != null)
        {
            stream_panel_read_btn.Click -= ReadParams_Click;
            stream_panel_read_btn.Click += ReadParams_Click;
        }

        // Camera
        btn_take_picture.Click -= OnTakePictureClick;
        btn_take_picture.Click += OnTakePictureClick;
        btn_record.Click -= OnRecordClick;
        btn_record.Click += OnRecordClick;
        btn_livestream.Click -= OnLivestreamClick;
        btn_livestream.Click += OnLivestreamClick;

        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        AttachTelemetrySource(_viewModel.TelemetryData);

        UpdateConnectionStatus();
        UpdateArmButton();
        UpdateTelemetryFromViewModel();
        UpdateStreamPanelEnabled();

        mapControl?.SetCenter(-7.2754, 112.7947, 15);
        
        // Map controls
        if (btn_follow_wahana != null)
        {
            btn_follow_wahana.Click -= ToggleFollowWahana_Click;
            btn_follow_wahana.Click += ToggleFollowWahana_Click;
        }
        if (cb_flight_map_type != null)
        {
            cb_flight_map_type.SelectionChanged -= FlightMapType_SelectionChanged;
            cb_flight_map_type.SelectionChanged += FlightMapType_SelectionChanged;
        }
        
        // Initialize alert system
        InitializeAlertSystem();
    }

    /// <summary> Populate cb_ports dengan ConnList seperti WPF PrepConnection. </summary>
    private void PrepConnection()
    {
        if (cb_ports == null) return;
        
        System.Diagnostics.Debug.WriteLine("[FlightPage] PrepConnection started");
        
        cb_ports.Items.Clear();
        cb_ports.Items.Add(CreateComboItem("KONEKSI"));
        cb_ports.Items.Add(CreateComboItem("..REFRESH.."));
        cb_ports.Items.Add(CreateComboItem("WIFI"));
        cb_ports.Items.Add(CreateComboItem("UDP"));
        cb_ports.Items.Add(CreateComboItem("LoRa"));
        
        // Deteksi dan tambahkan COM ports (sama WPF: ListAllSerialPorts)
        try
        {
            System.Diagnostics.Debug.WriteLine("[FlightPage] Getting serial port names using SerialPortHelper...");
            var ports = SerialPortHelper.GetAvailablePorts();
            System.Diagnostics.Debug.WriteLine($"[FlightPage] Found {ports.Length} serial ports");
            
            foreach (var port in ports)
            {
                cb_ports.Items.Add(CreateComboItem(port));
                System.Diagnostics.Debug.WriteLine($"[FlightPage] Adding port: {port}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FlightPage] Error detecting serial ports: {ex.Message}");
        }
        
        cb_ports.SelectedIndex = 0;
        cb_ports.SelectionChanged += ConnSelection_Changed;
        
        System.Diagnostics.Debug.WriteLine($"[FlightPage] PrepConnection completed successfully - ComboBox has {cb_ports.Items.Count} items");
    }

    private static ComboBoxItem CreateComboItem(string content)
    {
        var item = new ComboBoxItem { Content = content };
        return item;
    }

    private async Task RefreshSerialPortsAsync()
    {
        if (_isRefreshingPorts || cb_ports == null)
        {
            return;
        }

        _isRefreshingPorts = true;
        try
        {
            var ports = await Task.Run(() => SerialPortHelper.GetAvailablePorts());
            DispatcherQueue.TryEnqueue(() =>
            {
                if (cb_ports == null)
                {
                    return;
                }

                while (cb_ports.Items.Count > 5)
                {
                    cb_ports.Items.RemoveAt(5);
                }

                foreach (var port in ports)
                {
                    cb_ports.Items.Add(CreateComboItem(port));
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FlightPage] Error refreshing serial ports: {ex.Message}");
        }
        finally
        {
            _isRefreshingPorts = false;
        }
    }

    /// <summary> Sama WPF ConnSelection_Changed: set ConnectionType, enable/disable controls based on connection type. </summary>
    private void ConnSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (cb_ports?.SelectedItem is not ComboBoxItem item) return;
        var content = (item.Content as string) ?? "";
        
        // Hide all optional controls first
        HideAllConnectionOptions();
        
        switch (content)
        {
            case "..REFRESH..":
                // Refresh: remove all COM ports and re-detect
                if (cb_ports.Items.Count > 5) // Now we have 5 fixed items (KONEKSI, REFRESH, WIFI, UDP, LoRa)
                {
                    while (cb_ports.Items.Count > 5)
                    {
                        cb_ports.Items.RemoveAt(5);
                    }
                }
                _ = RefreshSerialPortsAsync();
                cb_ports.SelectedIndex = 0;
                break;
                
            case "WIFI":
                // TCP connection - show IP and Port
                ShowNetworkOptions();
                _selectedConnectionType = ConnectionType.TCP;
                _selectedSerialPort = "";
                break;
                
            case "UDP":
                // UDP connection - show IP and Port
                ShowNetworkOptions();
                _selectedConnectionType = ConnectionType.UDP;
                _selectedSerialPort = "";
                break;
                
            case "LoRa":
                // LoRa connection - show LoRa options
                ShowLoRaOptions();
                _selectedConnectionType = ConnectionType.LoRa;
                _selectedSerialPort = "";
                break;
                
            default:
                // COM port selected - show baud rate
                ShowSerialOptions();
                _selectedConnectionType = ConnectionType.Serial;
                _selectedSerialPort = content;
                System.Diagnostics.Debug.WriteLine($"[FlightPage] Serial port selected: {_selectedSerialPort}");
                break;
        }
    }
    
    private void HideAllConnectionOptions()
    {
        // Hide serial options
        if (tb_bauds != null) tb_bauds.Visibility = Visibility.Collapsed;
        
        // Hide network options - COMMENTED OUT: UI elements not in XAML
        // if (lbl_ip != null) lbl_ip.Visibility = Visibility.Collapsed;
        if (serverIP != null) serverIP.Visibility = Visibility.Collapsed;
        // if (lbl_port != null) lbl_port.Visibility = Visibility.Collapsed;
        if (udpPort != null) udpPort.Visibility = Visibility.Collapsed;
        
        // Hide LoRa options - COMMENTED OUT: UI elements not in XAML
        // if (lbl_lora_freq != null) lbl_lora_freq.Visibility = Visibility.Collapsed;
        // if (cb_lora_freq != null) cb_lora_freq.Visibility = Visibility.Collapsed;
        // if (lbl_lora_bw != null) lbl_lora_bw.Visibility = Visibility.Collapsed;
        // if (cb_lora_bw != null) cb_lora_bw.Visibility = Visibility.Collapsed;
    }
    
    private void ShowSerialOptions()
    {
        if (tb_bauds != null) tb_bauds.Visibility = Visibility.Visible;
    }
    
    private void ShowNetworkOptions()
    {
        // COMMENTED OUT: UI elements not in XAML
        // if (lbl_ip != null) lbl_ip.Visibility = Visibility.Visible;
        if (serverIP != null) serverIP.Visibility = Visibility.Visible;
        // if (lbl_port != null) lbl_port.Visibility = Visibility.Visible;
        if (udpPort != null) udpPort.Visibility = Visibility.Visible;
    }
    
    private void ShowLoRaOptions()
    {
        // COMMENTED OUT: UI elements not in XAML
        // if (lbl_lora_freq != null) lbl_lora_freq.Visibility = Visibility.Visible;
        // if (cb_lora_freq != null) cb_lora_freq.Visibility = Visibility.Visible;
        // if (lbl_lora_bw != null) lbl_lora_bw.Visibility = Visibility.Visible;
        // if (cb_lora_bw != null) cb_lora_bw.Visibility = Visibility.Visible;
    }

    private void OnToggleConnection(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        SyncConnectionSettings();
        if (_viewModel.IsConnected)
            _viewModel.Disconnect();
        else
            _viewModel.Connect();
    }

    private void SyncConnectionSettings()
    {
        if (_viewModel == null) return;
        _viewModel.ConnectionType = _selectedConnectionType;
        
        if (_selectedConnectionType == ConnectionType.Serial)
        {
            // Serial connection: use SerialPort and BaudRate
            // ConnectionAddress will be used to store SerialPort name
            _viewModel.ConnectionAddress = _selectedSerialPort;
            // Parse baudrate from ComboBox
            int baudRate = 57600; // Default baudrate
            if (tb_bauds?.SelectedItem is ComboBoxItem baudItem)
            {
                var baudText = (baudItem.Content as string) ?? "57600";
                // Skip "BAUDRATE" header
                if (baudText != "BAUDRATE" && int.TryParse(baudText, out var baud))
                {
                    baudRate = baud;
                }
            }
            _viewModel.ConnectionPort = baudRate; // Use Port field for BaudRate
            System.Diagnostics.Debug.WriteLine($"[FlightPage] Serial settings: Port={_selectedSerialPort}, BaudRate={baudRate}");
        }
        else
        {
            // Network connection: use Address and Port
            _viewModel.ConnectionAddress = serverIP?.Text ?? "127.0.0.1";
            _viewModel.ConnectionPort = int.TryParse(udpPort?.Text, out var p) ? p : 14550;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FlightViewModel.IsConnected))
        {
            UpdateConnectionStatus();
            UpdateStreamPanelEnabled();
        }
        else if (e.PropertyName == nameof(FlightViewModel.IsArmed))
        {
            UpdateArmButton();
        }
        else if (e.PropertyName == nameof(FlightViewModel.TelemetryData))
        {
            AttachTelemetrySource(_viewModel?.TelemetryData);
            UpdateTelemetryFromViewModel();
        }
    }

    private void AttachTelemetrySource(TelemetryData? telemetry)
    {
        if (_telemetrySource != null)
        {
            _telemetrySource.PropertyChanged -= Telemetry_PropertyChanged;
        }

        _telemetrySource = telemetry;

        if (_telemetrySource != null)
        {
            _telemetrySource.PropertyChanged += Telemetry_PropertyChanged;
        }
    }

    private void Telemetry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateTelemetryFromViewModel();
    }

    private void UpdateConnectionStatus()
    {
        if (_viewModel == null || ind_conn_status == null || btn_conn == null || img_conn == null) return;
        
        ind_conn_status.Text = _viewModel.IsConnected ? "Connected" : "Disconnected";
        
        // Update connection icon
        var iconPath = _viewModel.IsConnected 
            ? "ms-appx:///Assets/icons/icons8-connected-80.png"
            : "ms-appx:///Assets/icons/icons8-disconnected-80.png";
        img_conn.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(iconPath));
        
        // Update button background color
        btn_conn.Background = _viewModel.IsConnected
            ? new SolidColorBrush(Microsoft.UI.Colors.LightGreen)
            : new SolidColorBrush(Microsoft.UI.Colors.LightGray);
    }

    private void UpdateArmButton()
    {
        if (_viewModel == null || btn_arm_disarm == null) return;
        btn_arm_disarm.Content = _viewModel.IsArmed ? "DISARM" : "ARM";
        btn_arm_disarm.Background = _viewModel.IsArmed
            ? new SolidColorBrush(Microsoft.UI.Colors.OrangeRed)
            : new SolidColorBrush(Microsoft.UI.Colors.AliceBlue);
    }

    /// <summary> Sama WPF: stream_panel IsEnabled when connected. </summary>
    private void UpdateStreamPanelEnabled()
    {
        if (_viewModel == null) return;
        var enabled = _viewModel.IsConnected;
        if (out_stream != null) out_stream.IsEnabled = enabled;
        if (btn_send_command != null) btn_send_command.IsEnabled = enabled;
        if (stream_panel_read_btn != null) stream_panel_read_btn.IsEnabled = enabled;
        if (in_stream != null) in_stream.IsEnabled = enabled;
    }

    private void UpdateTelemetryFromViewModel()
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(UpdateTelemetryFromViewModel);
            return;
        }

        var now = DateTime.UtcNow;
        if ((now - _lastTelemetryUiUpdate).TotalMilliseconds < TelemetryUiUpdateIntervalMs)
        {
            return;
        }

        _lastTelemetryUiUpdate = now;

        if (_viewModel?.TelemetryData == null) return;

        var t = _viewModel.TelemetryData;

        // GPS Info
        if (tb_hdop != null) tb_hdop.Text = t.HDOP > 0 ? $"{t.HDOP:F2}" : "N/A";
        if (tb_sats != null) tb_sats.Text = t.SatelliteCount > 0 ? t.SatelliteCount.ToString() : "N/A";
        if (tb_lat != null) tb_lat.Text = Math.Abs(t.Latitude) > 1e-6 ? $"{t.Latitude:F6}" : "N/A";
        if (tb_longt != null) tb_longt.Text = Math.Abs(t.Longitude) > 1e-6 ? $"{t.Longitude:F6}" : "N/A";

        // PERBAIKAN: Flight Mode - Pastikan tampil dengan format yang benar dan uppercase
        if (lbl_fmode != null)
        {
            var flightModeText = t.FlightMode.ToString().ToUpper();
            lbl_fmode.Text = flightModeText;
        }
        
        // Attitude
        if (tb_yaw != null) tb_yaw.Text = $"{((t.Yaw % 360) + 360) % 360:F2}°";
        if (tb_pitch != null) tb_pitch.Text = $"{t.Pitch:F2}°";
        if (tb_roll != null) tb_roll.Text = $"{t.Roll:F2}°";

        // Speed (GPS / IMU) - single field matching Avalonia
        if (tb_airspeed != null) tb_airspeed.Text = t.AirSpeed >= 0 ? $"{t.AirSpeed:F1} m/s" : "N/A";
        
        // Altitude
        if (tb_alti != null) tb_alti.Text = t.Altitude >= 0 ? $"{t.Altitude:F1} m" : "N/A";
        
        // Barometers: display Barometers if available, else RelativeAltitude (matching Avalonia)
        if (tb_baro != null) tb_baro.Text = t.Barometers > 0 ? $"{t.Barometers:F1} m" : (t.RelativeAltitude >= 0 ? $"{t.RelativeAltitude:F1} m" : "N/A");

        // Throttle
        if (pb_rc_throttle != null)
            pb_rc_throttle.Value = Math.Clamp(t.ThrottlePercent, 0, 100);

        // Avionics Indicators
        if (ind_attitude != null)
        {
            ind_attitude.PitchAngle = t.Pitch;
            ind_attitude.RollAngle = t.Roll;
        }
        if (ind_heading != null)
            ind_heading.Heading = (int)Math.Round(((t.Heading % 360) + 360) % 360);
        if (ind_airspeed_indicator != null)
            ind_airspeed_indicator.Airspeed = (int)Math.Round(t.AirSpeed * 1.944); // Convert m/s to knots

        // Update Map
        if (mapControl != null && Math.Abs(t.Latitude) > 1e-6 && Math.Abs(t.Longitude) > 1e-6)
            mapControl.UpdateVehiclePosition(t.Latitude, t.Longitude);
        
        // Check and trigger alerts based on telemetry changes
        CheckAndTriggerAlerts();
    }

    private void OnArmDisarmClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null)
        {
            return;
        }

        if (_viewModel.IsArmed)
        {
            _viewModel.DisarmCommand.Execute(null);
        }
        else
        {
            _viewModel.ArmCommand.Execute(null);
        }
    }

    private void OnTakePictureClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.TakePictureCommand.Execute(null);
    }

    private void OnRecordClick(object sender, RoutedEventArgs e)
    {
    }

    private void OnLivestreamClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.StartStreamCommand.Execute(null);
    }

    /// <summary> Sama WPF SendSelectedCommand: pilih command dari ComboBox content (Land, Take Off, Loiter, Start Mission, Get Param, RTL, etc). </summary>
    private void OnSendCommand(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null || out_stream == null) return;

        var content = (out_stream.SelectedItem as ComboBoxItem)?.Content as string ?? "";
        Command cmd = content switch
        {
            "Land" => Command.LAND,
            "Take Off" => Command.TAKE_OFF,
            "RTL" => Command.RTL,
            "Loiter" => Command.PAUSE,
            "Start Mission" => Command.CONTINUE,
            "Get Param" => Command.RTL, // Request parameters (uses RTL command in original)
            "Stabilize" => Command.PAUSE,
            "Guided" => Command.PAUSE, // Guided mode
            "Auto" => Command.CONTINUE, // Auto mode (mission)
            "Alt Hold" => Command.PAUSE, // Altitude hold
            "Position Hold" => Command.PAUSE, // Position hold
            "Brake" => Command.PAUSE, // Brake mode
            _ => Command.PAUSE
        };
        _viewModel.SendCommand(cmd);
        if (in_stream != null)
            in_stream.Text = $"Sent: {content}\r\n" + (string.IsNullOrEmpty(in_stream.Text) ? "" : in_stream.Text + "\r\n");
    }
    
    /// <summary> READ button handler - requests parameters from the drone. </summary>
    private void ReadParams_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        _viewModel.RequestParamsCommand?.Execute(null);
        if (in_stream != null)
            in_stream.Text = "Requesting parameters...\r\n" + (string.IsNullOrEmpty(in_stream.Text) ? "" : in_stream.Text + "\r\n");
    }
    
    /// <summary> Send command button handler. </summary>
    private void SendCommand_Click(object sender, RoutedEventArgs e)
    {
        OnSendCommand(sender, e);
    }

    /// <summary> Sama WPF CamSourceSelection_Changed: tampilkan tb_network_cam_url bila Network. </summary>
    private void CamSourceSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (cb_cam_source == null || tb_network_cam_url == null || cb_cams == null) return;
        var isNetwork = cb_cam_source.SelectedIndex == 1;
        tb_network_cam_url.Visibility = isNetwork ? Visibility.Visible : Visibility.Collapsed;
        cb_cams.Visibility = isNetwork ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary> Sama WPF BtnZoomIn_Click. </summary>
    private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
    {
        if (_zoomLevel < ZoomMax)
        {
            _zoomLevel += ZoomStep;
            UpdateLiveCamZoom();
        }
    }

    /// <summary> Sama WPF BtnZoomOut_Click. </summary>
    private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
    {
        if (_zoomLevel > ZoomMin)
        {
            _zoomLevel -= ZoomStep;
            UpdateLiveCamZoom();
        }
    }

    /// <summary> Sama WPF ResetZoom_Click. </summary>
    private void ResetZoom_Click(object sender, RoutedEventArgs e)
    {
        _zoomLevel = 1.0;
        UpdateLiveCamZoom();
    }

    private void UpdateLiveCamZoom()
    {
        // TODO: apply zoom to live camera stream when implemented
        System.Diagnostics.Debug.WriteLine($"[FlightPage] Zoom level: {_zoomLevel}");
    }
    
    /// <summary> Toggle follow wahana mode on map. </summary>
    private void ToggleFollowWahana_Click(object sender, RoutedEventArgs e)
    {
        if (btn_follow_wahana == null || mapControl == null) return;
        
        var isFollowing = btn_follow_wahana.IsChecked ?? false;
        btn_follow_wahana.Content = isFollowing ? "📍 Following" : "📍 Follow";
        
        // TODO: Implement follow mode in SkiaMapControl
        if (isFollowing && _viewModel?.TelemetryData != null)
        {
            var lat = _viewModel.TelemetryData.Latitude;
            var lon = _viewModel.TelemetryData.Longitude;
            if (Math.Abs(lat) > 1e-6 && Math.Abs(lon) > 1e-6)
            {
                mapControl.SetCenter(lat, lon, mapControl.ZoomLevel);
            }
        }
        
        System.Diagnostics.Debug.WriteLine($"[FlightPage] Follow mode: {isFollowing}");
    }
    
    /// <summary> Change map provider (OpenStreetMap, Google Satellite, Google Hybrid). </summary>
    private void FlightMapType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cb_flight_map_type == null || mapControl == null) return;
        
        var selectedIndex = cb_flight_map_type.SelectedIndex;
        if (selectedIndex == _lastMapProviderIndex)
        {
            return;
        }

        _lastMapProviderIndex = selectedIndex;
        // TODO: Implement map provider switching in SkiaMapControl
        // 0 = OpenStreetMap
        // 1 = Google Satellite
        // 2 = Google Hybrid
        
        System.Diagnostics.Debug.WriteLine($"[FlightPage] Map provider changed to index: {selectedIndex}");
    }
    
    #region Alert Message Display System
    
    /// <summary>
    /// Initialize the alert message display system.
    /// </summary>
    private void InitializeAlertSystem()
    {
        // Create timer for rotating alert messages
        _alertRotationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3) // Rotate alerts every 3 seconds
        };
        _alertRotationTimer.Tick += AlertRotationTimer_Tick;
        _alertRotationTimer.Start();
        
        System.Diagnostics.Debug.WriteLine("[FlightPage] Alert system initialized");
    }
    
    /// <summary>
    /// Timer tick handler for rotating alert messages.
    /// </summary>
    private void AlertRotationTimer_Tick(object? sender, object e)
    {
        UpdateAlertDisplay();
    }
    
    /// <summary>
    /// Add an alert message to the display queue.
    /// </summary>
    private void AddAlertMessage(string message, AlertSeverity severity)
    {
        var alert = new AlertMessage
        {
            Message = message,
            Severity = severity,
            Timestamp = DateTime.Now
        };
        
        // Check if similar alert already exists (prevent spam)
        if (_activeAlerts.Any(a => a.Message == message && 
            (DateTime.Now - a.Timestamp).TotalSeconds < 10))
        {
            return;
        }
        
        _alertMessageQueue.Enqueue(alert);
        _activeAlerts.Add(alert);
        
        // Remove old alerts (keep only last 5 minutes)
        _activeAlerts.RemoveAll(a => (DateTime.Now - a.Timestamp).TotalMinutes > 5);
        
        UpdateAlertDisplay();
        
        System.Diagnostics.Debug.WriteLine($"[FlightPage] Alert added: {message} ({severity})");
    }
    
    /// <summary>
    /// Clear all alert messages.
    /// </summary>
    private void ClearAlertMessages()
    {
        _alertMessageQueue.Clear();
        _activeAlerts.Clear();
        UpdateAlertDisplay();
    }
    
    /// <summary>
    /// Update the alert message display.
    /// </summary>
    private void UpdateAlertDisplay()
    {
        // COMMENTED OUT: txt_alert_messages UI element not in XAML
        // if (txt_alert_messages == null) return;
        
        // Get active alerts (last 30 seconds)
        var recentAlerts = _activeAlerts
            .Where(a => (DateTime.Now - a.Timestamp).TotalSeconds < 30)
            .OrderByDescending(a => a.Severity)
            .ThenByDescending(a => a.Timestamp)
            .Take(3) // Show max 3 alerts at once
            .ToList();
        
        if (recentAlerts.Count == 0)
        {
            // txt_alert_messages.Text = "";
            return;
        }
        
        // Build alert text with separators
        var alertTexts = recentAlerts.Select(a => 
        {
            var icon = a.Severity switch
            {
                AlertSeverity.Critical => "🔴",
                AlertSeverity.Warning => "⚠️",
                AlertSeverity.Info => "ℹ️",
                _ => "•"
            };
            return $"{icon} {a.Message}";
        });
        
        // txt_alert_messages.Text = string.Join("  |  ", alertTexts);
        
        // Set color based on highest severity
        var highestSeverity = recentAlerts.First().Severity;
        /* txt_alert_messages.Foreground = highestSeverity switch
        {
            AlertSeverity.Critical => new SolidColorBrush(Microsoft.UI.Colors.Red),
            AlertSeverity.Warning => new SolidColorBrush(Microsoft.UI.Colors.Yellow),
            AlertSeverity.Info => new SolidColorBrush(Microsoft.UI.Colors.Cyan),
            _ => new SolidColorBrush(Microsoft.UI.Colors.White)
        }; */
        
        // Animate alert (pulse effect for critical alerts)
        if (highestSeverity == AlertSeverity.Critical)
        {
            AnimateAlertPulse();
        }
    }
    
    /// <summary>
    /// Animate alert message with pulse effect.
    /// </summary>
    private void AnimateAlertPulse()
    {
        // COMMENTED OUT: txt_alert_messages UI element not in XAML
        // if (txt_alert_messages == null) return;
        
        /* var storyboard = new Storyboard();
        
        // Opacity animation (pulse)
        var opacityAnimation = new DoubleAnimation
        {
            From = 1.0,
            To = 0.3,
            Duration = TimeSpan.FromMilliseconds(500),
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(2) // Pulse twice
        };
        
        Storyboard.SetTarget(opacityAnimation, txt_alert_messages);
        Storyboard.SetTargetProperty(opacityAnimation, "Opacity");
        storyboard.Children.Add(opacityAnimation);
        
        storyboard.Begin(); */
    }
    
    /// <summary>
    /// Check telemetry for alert conditions and trigger alerts.
    /// </summary>
    private void CheckAndTriggerAlerts()
    {
        if (_viewModel?.TelemetryData == null) return;
        
        var t = _viewModel.TelemetryData;
        
        // Check Battery Level
        CheckBatteryAlerts(t);
        
        // Check GPS Status
        CheckGPSAlerts(t);
        
        // Check Connection Status
        CheckConnectionAlerts();
        
        // Check Flight Mode Changes
        CheckFlightModeAlerts(t);
        
        // Update previous state
        _previousState.BatteryPercent = t.BatteryPercent;
        _previousState.GPSFixType = t.GPSFixType;
        _previousState.IsConnected = _viewModel.IsConnected;
        _previousState.FlightMode = t.FlightMode;
    }
    
    /// <summary>
    /// Check battery level and trigger alerts if needed.
    /// </summary>
    private void CheckBatteryAlerts(TelemetryData t)
    {
        var batteryPercent = t.BatteryPercent;
        
        // Critical battery warning (< 15%)
        if (batteryPercent >= 0 && batteryPercent < 15 && _previousState.BatteryPercent >= 15)
        {
            AddAlertMessage($"CRITICAL BATTERY: {batteryPercent:F0}%", AlertSeverity.Critical);
        }
        // Low battery warning (< 25%)
        else if (batteryPercent >= 0 && batteryPercent < 25 && _previousState.BatteryPercent >= 25)
        {
            AddAlertMessage($"LOW BATTERY: {batteryPercent:F0}%", AlertSeverity.Warning);
        }
        // Battery warning (< 40%)
        else if (batteryPercent >= 0 && batteryPercent < 40 && _previousState.BatteryPercent >= 40)
        {
            AddAlertMessage($"Battery Warning: {batteryPercent:F0}%", AlertSeverity.Info);
        }
    }
    
    /// <summary>
    /// Check GPS status and trigger alerts if needed.
    /// </summary>
    private void CheckGPSAlerts(TelemetryData t)
    {
        var fixType = t.GPSFixType;
        
        // GPS lost (no fix)
        if (fixType < 2 && _previousState.GPSFixType >= 2)
        {
            AddAlertMessage("GPS SIGNAL LOST", AlertSeverity.Critical);
        }
        // GPS degraded (2D fix only)
        else if (fixType == 2 && _previousState.GPSFixType >= 3)
        {
            AddAlertMessage("GPS DEGRADED (2D Fix)", AlertSeverity.Warning);
        }
        // GPS recovered
        else if (fixType >= 3 && _previousState.GPSFixType < 3)
        {
            AddAlertMessage("GPS Signal Restored", AlertSeverity.Info);
        }
    }
    
    /// <summary>
    /// Check connection status and trigger alerts if needed.
    /// </summary>
    private void CheckConnectionAlerts()
    {
        var isConnected = _viewModel?.IsConnected ?? false;
        
        // Connection lost
        if (!isConnected && _previousState.IsConnected)
        {
            AddAlertMessage("CONNECTION LOST", AlertSeverity.Critical);
        }
        // Connection restored
        else if (isConnected && !_previousState.IsConnected)
        {
            AddAlertMessage("Connection Restored", AlertSeverity.Info);
        }
    }
    
    /// <summary>
    /// Check flight mode changes and trigger alerts if needed.
    /// </summary>
    private void CheckFlightModeAlerts(TelemetryData t)
    {
        var mode = t.FlightMode;
        
        // Flight mode changed
        if (mode != _previousState.FlightMode && _previousState.FlightMode != FlightMode.UNKNOWN)
        {
            // Critical modes (RTL, Land)
            if (mode == FlightMode.RTL || mode == FlightMode.LAND)
            {
                AddAlertMessage($"MODE: {mode}", AlertSeverity.Critical);
            }
            // Warning modes (Loiter, Brake)
            else if (mode == FlightMode.LOITER || mode == FlightMode.BRAKE)
            {
                AddAlertMessage($"Mode: {mode}", AlertSeverity.Warning);
            }
            // Info for other modes
            else
            {
                AddAlertMessage($"Mode: {mode}", AlertSeverity.Info);
            }
        }
    }
    
    /// <summary>
    /// Trigger a geofence violation alert.
    /// </summary>
    public void TriggerGeofenceViolationAlert()
    {
        AddAlertMessage("GEOFENCE VIOLATION", AlertSeverity.Critical);
    }
    
    #endregion
}

/// <summary>
/// Represents an alert message.
/// </summary>
internal class AlertMessage
{
    public string Message { get; set; } = "";
    public AlertSeverity Severity { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Alert severity levels.
/// </summary>
internal enum AlertSeverity
{
    Info,
    Warning,
    Critical
}

/// <summary>
/// Stores previous telemetry state for alert triggering.
/// </summary>
internal class TelemetryState
{
    public double BatteryPercent { get; set; } = 100;
    public int GPSFixType { get; set; } = 0;
    public bool IsConnected { get; set; } = false;
    public FlightMode FlightMode { get; set; } = FlightMode.UNKNOWN;
}
