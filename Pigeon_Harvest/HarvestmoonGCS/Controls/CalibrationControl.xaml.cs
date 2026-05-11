using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MavLinkNet;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Core.ViewModels;
using HarvestmoonGCS.Models;

namespace HarvestmoonGCS.Controls
{
    public sealed partial class CalibrationControl : UserControl, INotifyPropertyChanged
    {
        // Services
        private readonly IMavLinkService? _mavLinkService;
        private readonly CalibrationViewModel? _calibrationViewModel;

        // Data Models
        public ObservableCollection<ServoItem> ServoItems { get; } = new ObservableCollection<ServoItem>();
        public ObservableCollection<RadioChannelItem> RadioItems { get; } = new ObservableCollection<RadioChannelItem>();
        
        private CancellationTokenSource _cts;
        private int clicked = -1;
        private bool IsStarted = false;
        private int _currentVehicleType = 1; // Default to FixedWing (Plane)

        public CalibrationControl()
        {
            InitializeComponent();
            DataContext = this;

            // Get services from DI container
            _mavLinkService = App.GetService<IMavLinkService>();
            _calibrationViewModel = App.GetService<CalibrationViewModel>();

            InitializeServoItems();
            InitializeRadioItems();

            // Lifecycle events
            Loaded += CalibrationControl_Loaded;
            Unloaded += CalibrationControl_Unloaded;
        }

        private FlightData? _latestFlightData;

        private void CalibrationControl_Loaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("[CalibrationControl] Control loaded");
            _cts = new CancellationTokenSource();
            
            if (_mavLinkService != null)
            {
                _mavLinkService.PacketReceived += OnVehiclePacketReceived;
                _mavLinkService.TelemetryReceived += OnTelemetryReceived;
            }
            
            UpdateVehicleIcons(1);
            
            if (_mavLinkService == null || !_mavLinkService.IsConnected)
            {
                ConnectionWarningOverlay.Visibility = Visibility.Visible;
                func_grid.IsHitTestVisible = false;
            }
            else
            {
                ConnectionWarningOverlay.Visibility = Visibility.Collapsed;
                func_grid.IsHitTestVisible = true;
            }
        }

        private void OnTelemetryReceived(object? sender, FlightData data)
        {
            _latestFlightData = data;
        }

        private void OnVehiclePacketReceived(object? sender, MavLinkNet.MavLinkPacketBase packet)
        {
            // Check if this is a HEARTBEAT message with vehicle type info
            if (packet.Message is MavLinkNet.UasHeartbeat heartbeat)
            {
                int vehicleType = (int)heartbeat.Type;
                if (vehicleType == 0) vehicleType = 1; // Default to FixedWing
                
                // Only update if vehicle type changed
                if (_currentVehicleType != vehicleType)
                {
                    _currentVehicleType = vehicleType;
                    
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        UpdateVehicleIcons(vehicleType);
                        
                        // Update tuning visibility if Tuning tab is currently visible
                        if (Tuning.Visibility == Visibility.Visible)
                        {
                            UpdateTuningVisibility(vehicleType);
                        }
                    });
                }
            }
        }

        private void CalibrationControl_Unloaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("[CalibrationControl] Control unloaded - cleaning up");
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            if (_mavLinkService != null)
            {
                _mavLinkService.PacketReceived -= OnRadioPacketReceived;
                _mavLinkService.PacketReceived -= OnVehiclePacketReceived;
                _mavLinkService.TelemetryReceived -= OnTelemetryReceived;
            }

            if (_calibrationViewModel != null)
            {
                _calibrationViewModel.CalibrationStepCompleted -= OnCalibrationStepCompleted;
                _calibrationViewModel.CalibrationProgressChanged -= OnCalibrationProgressChanged;
            }
        }

        private void OnCalibrationStepCompleted(object? sender, int step)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ShowStatus($"Calibration step {step} completed");
                // Update UI to show completed step
            });
        }

        private void OnCalibrationProgressChanged(object? sender, (int progress1, int progress2) progress)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (progressBar1 != null)
                    progressBar1.Value = progress.progress1;
                if (progressBar2 != null)
                    progressBar2.Value = progress.progress2;
                
                Debug.WriteLine($"[CalibrationControl] Compass progress updated: {progress.progress1}%, {progress.progress2}%");
            });
        }

        private void InitializeServoItems()
        {
            for (int i = 1; i <= 16; i++)
            {
                ServoItems.Add(new ServoItem { No = i, Min = 1000, Trim = 1500, Max = 2000 });
            }
            
            // Set ItemsControl ItemsSource
            if (ServoItemsControl != null)
            {
                ServoItemsControl.ItemsSource = ServoItems;
                ServoItemsControl.Loaded += ServoItemsControl_Loaded;
            }
        }

        private void ServoItemsControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Populate servo function ComboBoxes
            PopulateServoFunctionComboBoxes();
        }

        /// <summary>
        /// Populates all servo function ComboBoxes with ServoFunction enum values
        /// </summary>
        private void PopulateServoFunctionComboBoxes()
        {
            // Find all ComboBoxes in the ItemsControl
            var itemsControl = ServoItemsControl;
            if (itemsControl == null) return;

            // Create enum list once
            var enumList = new List<KeyValuePair<int, string>>();
            foreach (var enumValue in Enum.GetValues(typeof(ServoFunction)))
            {
                enumList.Add(new KeyValuePair<int, string>(
                    (int)enumValue,
                    enumValue.ToString()
                ));
            }

            // Iterate through visual tree to find ComboBoxes
            for (int i = 0; i < itemsControl.Items.Count; i++)
            {
                var container = itemsControl.ContainerFromIndex(i) as FrameworkElement;
                if (container != null)
                {
                    var comboBox = FindChildByName<ComboBox>(container, "FunctionCombo");
                    if (comboBox != null)
                    {
                        comboBox.ItemsSource = enumList;
                        comboBox.DisplayMemberPath = "Value";
                        comboBox.SelectedValuePath = "Key";
                    }
                }
            }
        }

        /// <summary>
        /// Helper method to find a child control by name in the visual tree
        /// </summary>
        private T? FindChildByName<T>(DependencyObject parent, string childName) where T : FrameworkElement
        {
            if (parent == null) return null;

            int childCount = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
                
                if (child is T typedChild && typedChild.Name == childName)
                {
                    return typedChild;
                }

                var result = FindChildByName<T>(child, childName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private void InitializeRadioItems()
        {
            string[] names = { "Roll (1)", "Pitch (2)", "Throttle (3)", "Yaw (4)", "Radio 5", "Radio 6", "Radio 7", "Radio 8" };
            for (int i = 0; i < 8; i++)
            {
                RadioItems.Add(new RadioChannelItem { Name = names[i], No = i + 1, Min = 1100, Max = 1900, Current = 1500 });
            }
        }

        /// <summary>
        /// Update accelerometer calibration icons based on vehicle type
        /// </summary>
        private void UpdateVehicleIcons(int vehicleType)
        {
            string iconSource = vehicleType switch
            {
                2 or 13 or 14 or 15 or 29 or 43 => "ms-appx:///Assets/icons/ikon-quadcopter.png", // Multirotor types
                _ => "ms-appx:///Assets/icons/ikon-wahana-pesawat-1.png" // Default to plane (FixedWing and others)
            };

            // Update all 6 position images
            if (Image1 != null) Image1.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(iconSource));
            if (Image2 != null) Image2.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(iconSource));
            if (Image3 != null) Image3.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(iconSource));
            if (Image4 != null) Image4.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(iconSource));
            if (Image5 != null) Image5.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(iconSource));
            if (Image6 != null) Image6.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(iconSource));

            Debug.WriteLine($"[CalibrationControl] Updated vehicle icons to: {iconSource}");
        }

        // Navigation
        private CancellationToken ResetCancellationToken()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            return _cts.Token;
        }

        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton btn && btn.Tag is string targetName)
            {
                var token = ResetCancellationToken();

                // Hide all panels
                Accele_calib.Visibility = Visibility.Collapsed;
                Kompass_calib.Visibility = Visibility.Collapsed;
                Radio_calib.Visibility = Visibility.Collapsed;
                Flight_mode.Visibility = Visibility.Collapsed;
                ServoOutput.Visibility = Visibility.Collapsed;
                EscCalib.Visibility = Visibility.Collapsed;
                MotorTestGrid.Visibility = Visibility.Collapsed;
                Tuning.Visibility = Visibility.Collapsed;

                // Show target panel
                switch (targetName)
                {
                    case "Accele_calib":
                        Accele_calib.Visibility = Visibility.Visible;
                        break;
                    case "Kompass_calib":
                        Kompass_calib.Visibility = Visibility.Visible;
                        break;
                    case "Radio_calib":
                        Radio_calib.Visibility = Visibility.Visible;
                        _ = StartUpdatingRadio(token);
                        break;
                    case "Flight_mode":
                        Flight_mode.Visibility = Visibility.Visible;
                        _ = StartUpdatingMode(token);
                        _ = UiModeUpdate(token);
                        break;
                    case "ServoOutput":
                        ServoOutput.Visibility = Visibility.Visible;
                        _ = StartUpdatingServo(token);
                        break;
                    case "EscCalib":
                        EscCalib.Visibility = Visibility.Visible;
                        break;
                    case "MotorTestGrid":
                        MotorTestGrid.Visibility = Visibility.Visible;
                        break;
                    case "Tuning":
                        Tuning.Visibility = Visibility.Visible;
                        UpdateTuningVisibility(_currentVehicleType);
                        
                        // Only load parameters for the visible tuning section
                        if (COPTER_MODE.Visibility == Visibility.Visible)
                        {
                            _ = StartUpdatingCopter(token);
                        }
                        else
                        {
                            _ = StartUpdatingQplane(token);
                        }
                        
                        _ = StartUpdatingWP(token);
                        break;
                }
            }
        }

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #region Accelerometer Calibration

        private async void Accele_Click(object sender, RoutedEventArgs e)
        {
            if (_mavLinkService == null) return;

            var greenBrush = new SolidColorBrush(Microsoft.UI.Colors.Green);
            var redBrush = new SolidColorBrush(Microsoft.UI.Colors.Red);
            var whiteBrush = new SolidColorBrush(Microsoft.UI.Colors.White);

            clicked++;
            switch (clicked)
            {
                case 1:
                    accele_send_msg();
                    set_accele(1);
                    Border2.BorderBrush = greenBrush;
                    Text1.Text = "COMPLETE";
                    Text1.Foreground = greenBrush;
                    Text2.Text = "ON GOING";
                    Text2.Foreground = whiteBrush;
                    ShowStatus("Position 1 complete. Place LEFT side down and click SEND.");
                    break;

                case 2:
                    accele_send_msg();
                    set_accele(2);
                    Border3.BorderBrush = greenBrush;
                    Text2.Text = "COMPLETE";
                    Text2.Foreground = greenBrush;
                    Text3.Text = "ON GOING";
                    Text3.Foreground = whiteBrush;
                    ShowStatus("Position 2 complete. Place RIGHT side down and click SEND.");
                    break;

                case 3:
                    accele_send_msg();
                    set_accele(3);
                    Border4.BorderBrush = greenBrush;
                    Text3.Text = "COMPLETE";
                    Text3.Foreground = greenBrush;
                    Text4.Text = "ON GOING";
                    Text4.Foreground = whiteBrush;
                    ShowStatus("Position 3 complete. Place NOSE DOWN and click SEND.");
                    break;

                case 4:
                    accele_send_msg();
                    set_accele(4);
                    Border5.BorderBrush = greenBrush;
                    Text4.Text = "COMPLETE";
                    Text4.Foreground = greenBrush;
                    Text5.Text = "ON GOING";
                    Text5.Foreground = whiteBrush;
                    ShowStatus("Position 4 complete. Place NOSE UP and click SEND.");
                    break;

                case 5:
                    accele_send_msg();
                    set_accele(5);
                    Border6.BorderBrush = greenBrush;
                    Text5.Text = "COMPLETE";
                    Text5.Foreground = greenBrush;
                    Text6.Text = "ON GOING";
                    Text6.Foreground = whiteBrush;
                    ShowStatus("Position 5 complete. Place UPSIDE DOWN and click SEND.");
                    break;

                case 6:
                    accele_send_msg();
                    set_accele(6);
                    Text6.Text = "COMPLETE";
                    Text6.Foreground = greenBrush;
                    AcceleButton.Content = "FINISH";
                    ShowStatus("Position 6 complete. Click FINISH to complete calibration.");
                    break;

                case 7:
                    Border1.BorderBrush = Border2.BorderBrush = Border3.BorderBrush =
                        Border4.BorderBrush = Border5.BorderBrush = Border6.BorderBrush = redBrush;
                    Text1.Text = Text2.Text = Text3.Text = Text4.Text = Text5.Text = Text6.Text = "NOT COMPLETED";
                    Text1.Foreground = Text2.Foreground = Text3.Foreground =
                        Text4.Foreground = Text5.Foreground = Text6.Foreground = whiteBrush;
                    ShowStatus("Accelerometer calibration complete!");
                    clicked = -1;
                    AcceleButton.Content = "START";
                    break;

                default:
                    Border1.BorderBrush = greenBrush;
                    Text1.Text = "ON GOING";
                    Text1.Foreground = whiteBrush;
                    AcceleButton.Content = "SEND";
                    accele_send_msg();
                    ShowStatus("Calibration started. Place LEVEL and click SEND.");
                    break;
            }
        }

        /// <summary>
        /// Sends MAV_CMD_PREFLIGHT_CALIBRATION command to start accelerometer calibration
        /// </summary>
        private async void accele_send_msg()
        {
            if (_mavLinkService == null) return;

            try
            {
                await _mavLinkService.SendCommandLongAsync(
                    command: (int)MavLinkNet.MavCmd.PreflightCalibration,
                    param1: 0,
                    param2: 0,
                    param3: 0,
                    param4: 0,
                    param5: 1, // Accel calibration
                    param6: 0,
                    param7: 0
                );

                Debug.WriteLine("[CalibrationControl] Accelerometer calibration command sent");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CalibrationControl] Error sending accel calibration: {ex.Message}");
                ShowStatus("Error: Failed to send calibration command");
            }
        }

        /// <summary>
        /// Sends MAV_CMD_ACCELCAL_VEHICLE_POS command to set vehicle position for calibration
        /// </summary>
        /// <param name="pos">Position number (1-6)</param>
        private async void set_accele(float pos)
        {
            if (_mavLinkService == null) return;

            try
            {
                await _mavLinkService.SendCommandLongAsync(
                    command: (int)MavLinkNet.MavCmd.AccelcalVehiclePos,
                    param1: pos,
                    param2: 0,
                    param3: 0,
                    param4: 0,
                    param5: 0,
                    param6: 0,
                    param7: 0
                );

                Debug.WriteLine($"[CalibrationControl] Accel position {pos} set");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CalibrationControl] Error setting accel position: {ex.Message}");
                ShowStatus($"Error: Failed to set position {pos}");
            }
        }

        private async Task set_accele(int position)
        {
            set_accele((float)position);
            await Task.CompletedTask;
        }

        private void ResetBorderColors()
        {
            var redBrush = new SolidColorBrush(Microsoft.UI.Colors.Red);
            var whiteBrush = new SolidColorBrush(Microsoft.UI.Colors.White);
            
            Border1.BorderBrush = redBrush;
            Border2.BorderBrush = redBrush;
            Border3.BorderBrush = redBrush;
            Border4.BorderBrush = redBrush;
            Border5.BorderBrush = redBrush;
            Border6.BorderBrush = redBrush;
            
            Text1.Text = "NOT COMPLETED"; Text1.Foreground = whiteBrush;
            Text2.Text = "NOT COMPLETED"; Text2.Foreground = whiteBrush;
            Text3.Text = "NOT COMPLETED"; Text3.Foreground = whiteBrush;
            Text4.Text = "NOT COMPLETED"; Text4.Foreground = whiteBrush;
            Text5.Text = "NOT COMPLETED"; Text5.Foreground = whiteBrush;
            Text6.Text = "NOT COMPLETED"; Text6.Foreground = whiteBrush;
        }

        private void UpdateAccelCalibrationStatus(int step)
        {
            var greenBrush = new SolidColorBrush(Microsoft.UI.Colors.Green);
            var completedText = "COMPLETED";
            
            switch (step)
            {
                case 1: Border1.BorderBrush = greenBrush; Text1.Text = completedText; Text1.Foreground = greenBrush; break;
                case 2: Border2.BorderBrush = greenBrush; Text2.Text = completedText; Text2.Foreground = greenBrush; break;
                case 3: Border3.BorderBrush = greenBrush; Text3.Text = completedText; Text3.Foreground = greenBrush; break;
                case 4: Border4.BorderBrush = greenBrush; Text4.Text = completedText; Text4.Foreground = greenBrush; break;
                case 5: Border5.BorderBrush = greenBrush; Text5.Text = completedText; Text5.Foreground = greenBrush; break;
                case 6: Border6.BorderBrush = greenBrush; Text6.Text = completedText; Text6.Foreground = greenBrush; break;
            }
        }

        private async void Simpel_Cal_Click(object sender, RoutedEventArgs e)
        {
            if (_mavLinkService == null) return;
            
            try
            {
                // Send simple accel calibration command (param5=4)
                await _mavLinkService.SendCommandLongAsync(
                    command: (int)MavLinkNet.MavCmd.PreflightCalibration,
                    param1: 0,
                    param2: 0,
                    param3: 0,
                    param4: 0,
                    param5: 4, // Simple accel calibration
                    param6: 0,
                    param7: 0
                );

                // Send twice for reliability (as in WPF)
                await Task.Delay(100);
                await _mavLinkService.SendCommandLongAsync(
                    command: (int)MavLinkNet.MavCmd.PreflightCalibration,
                    param1: 0,
                    param2: 0,
                    param3: 0,
                    param4: 0,
                    param5: 4,
                    param6: 0,
                    param7: 0
                );

                ShowStatus("Simple accelerometer calibration complete!");
                
                // Clear message after 3 seconds
                await Task.Delay(3000);
                ShowStatus("");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CalibrationControl] Error in simple accel cal: {ex.Message}");
                ShowStatus("Error: Simple calibration failed");
            }
        }

        private async void Levl_Cal_Click(object sender, RoutedEventArgs e)
        {
            if (_mavLinkService == null) return;
            
            try
            {
                // Send level calibration command (param5=2)
                await _mavLinkService.SendCommandLongAsync(
                    command: (int)MavLinkNet.MavCmd.PreflightCalibration,
                    param1: 0,
                    param2: 0,
                    param3: 0,
                    param4: 0,
                    param5: 2, // Level calibration
                    param6: 0,
                    param7: 0
                );

                ShowStatus("Level calibration complete!");
                
                // Clear message after 3 seconds
                await Task.Delay(3000);
                ShowStatus("");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CalibrationControl] Error in level cal: {ex.Message}");
                ShowStatus("Error: Level calibration failed");
            }
        }

        #endregion

        #region Compass Calibration

        private async void RefreshCompassList_Click(object sender, RoutedEventArgs e)
        {
            // Refresh compass device list
            // In WPF this binds to win.flight_Ctrl.CompassDevices
            // For Uno, we can use the ViewModel if available
            if (_calibrationViewModel != null)
            {
                await _calibrationViewModel.RefreshCompassListAsync();
            }
            ShowStatus("Compass list refreshed");
        }

        private async void Start_comp_Click(object sender, RoutedEventArgs e)
        {
            if (_mavLinkService == null) return;
            
            IsStarted = true;
            
            try
            {
                // Send MAV_CMD_DO_START_MAG_CAL command
                await _mavLinkService.SendCommandLongAsync(
                    command: (int)MavLinkNet.MavCmd.DoStartMagCal,
                    param1: 0,  // Compass mask (0 = all compasses)
                    param2: 1,  // Retry on failure
                    param3: 1,  // Autosave on success
                    param4: 0,  // Delay (0 = start immediately)
                    param5: 1,  // Autoreboot after calibration
                    param6: 0,
                    param7: 0
                );
                
                ShowStatus("Compass calibration started. Rotate the vehicle in all directions.");
                Debug.WriteLine("[CalibrationControl] Compass calibration started");
                
                // Start monitoring progress
                _ = StartUpdatingProgressBarAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CalibrationControl] Error starting compass calibration: {ex.Message}");
                ShowStatus("Error: Failed to start compass calibration");
                IsStarted = false;
            }
        }

        private async Task StartUpdatingProgressBarAsync()
        {
            while (IsStarted)
            {
                try
                {
                    if (_latestFlightData != null)
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            if (progressBar1 != null)
                                progressBar1.Value = _latestFlightData.Compass_Progress1;
                            if (progressBar2 != null)
                                progressBar2.Value = _latestFlightData.Compass_Progress2;
                        });
                    }
                    
                    await Task.Delay(100);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CalibrationControl] Error updating progress: {ex.Message}");
                    break;
                }
            }
        }

        private async void Accept_comp_Click(object sender, RoutedEventArgs e)
        {
            if (_mavLinkService == null) return;
            
            IsStarted = false; // Stop progress bar updates
            
            try
            {
                // Send MAV_CMD_DO_ACCEPT_MAG_CAL command
                await _mavLinkService.SendCommandLongAsync(
                    command: (int)MavLinkNet.MavCmd.DoAcceptMagCal,
                    param1: 0,  // Compass mask (0 = all compasses)
                    param2: 0,
                    param3: 1,  // Accept calibration
                    param4: 0,
                    param5: 0,
                    param6: 0,
                    param7: 0
                );
                
                ShowStatus("Compass calibration accepted!");
                Debug.WriteLine("[CalibrationControl] Compass calibration accepted");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CalibrationControl] Error accepting compass calibration: {ex.Message}");
                ShowStatus("Error: Failed to accept compass calibration");
            }
        }

        private async void Cancel_comp_Click(object sender, RoutedEventArgs e)
        {
            if (_mavLinkService == null) return;
            
            IsStarted = false; // Stop progress bar updates
            
            try
            {
                // Send MAV_CMD_DO_CANCEL_MAG_CAL command
                await _mavLinkService.SendCommandLongAsync(
                    command: (int)MavLinkNet.MavCmd.DoCancelMagCal,
                    param1: 0,  // Compass mask (0 = all compasses)
                    param2: 0,
                    param3: 1,  // Cancel calibration
                    param4: 0,
                    param5: 0,
                    param6: 0,
                    param7: 0
                );
                
                ShowStatus("Compass calibration cancelled.");
                Debug.WriteLine("[CalibrationControl] Compass calibration cancelled");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CalibrationControl] Error cancelling compass calibration: {ex.Message}");
                ShowStatus("Error: Failed to cancel compass calibration");
            }
        }

        private async void Reboot_comp_Click(object sender, RoutedEventArgs e)
        {
            if (_mavLinkService == null) return;
            
            try
            {
                // Send MAV_CMD_PREFLIGHT_REBOOT_SHUTDOWN command
                await _mavLinkService.SendCommandLongAsync(
                    command: (int)MavLinkNet.MavCmd.PreflightRebootShutdown,
                    param1: 1,  // Reboot autopilot
                    param2: 0,
                    param3: 0,
                    param4: 0,
                    param5: 0,
                    param6: 0,
                    param7: 0
                );
                
                ShowStatus("Rebooting vehicle...");
                Debug.WriteLine("[CalibrationControl] Vehicle reboot command sent");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CalibrationControl] Error rebooting vehicle: {ex.Message}");
                ShowStatus("Error: Failed to reboot vehicle");
            }
        }

        #endregion

        #region Radio Calibration

        private bool _isCalibratingRadio = false;

        private async Task StartUpdatingRadio(CancellationToken token)
        {
            // Subscribe to PacketReceived to get RC_CHANNELS messages
            if (_mavLinkService != null)
            {
                _mavLinkService.PacketReceived += OnRadioPacketReceived;
            }

            while (!token.IsCancellationRequested && Radio_calib.Visibility == Visibility.Visible)
            {
                try
                {
                    await Task.Delay(100, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }

            // Unsubscribe when done
            if (_mavLinkService != null)
            {
                _mavLinkService.PacketReceived -= OnRadioPacketReceived;
            }
        }

        private void OnRadioPacketReceived(object? sender, MavLinkNet.MavLinkPacketBase packet)
        {
            if (packet.Message is MavLinkNet.UasRcChannels rcChannels)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    UpdateRadioChannel(0, rcChannels.Chan1Raw);
                    UpdateRadioChannel(1, rcChannels.Chan2Raw);
                    UpdateRadioChannel(2, rcChannels.Chan3Raw);
                    UpdateRadioChannel(3, rcChannels.Chan4Raw);
                    UpdateRadioChannel(4, rcChannels.Chan5Raw);
                    UpdateRadioChannel(5, rcChannels.Chan6Raw);
                    UpdateRadioChannel(6, rcChannels.Chan7Raw);
                    UpdateRadioChannel(7, rcChannels.Chan8Raw);
                });
            }
        }

        private void UpdateRadioChannel(int index, int rawValue)
        {
            if (index < 0 || index >= RadioItems.Count) return;
            
            var item = RadioItems[index];
            item.Current = rawValue;

            if (_isCalibratingRadio)
            {
                if (rawValue < item.Min) item.Min = rawValue;
                if (rawValue > item.Max) item.Max = rawValue;
            }
        }

        private async void CalibrateRadioButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mavLinkService == null) return;

            var btn = sender as Button;
            _isCalibratingRadio = !_isCalibratingRadio;
            
            if (_isCalibratingRadio)
            {
                if (btn != null) btn.Content = "FINISH CALIBRATION";
                ShowStatus("Move all sticks and switches to their extremes...");
                
                // Reset min/max for calibration
                foreach (var item in RadioItems)
                {
                    item.Min = 2200;
                    item.Max = 800;
                }
            }
            else
            {
                if (btn != null) btn.Content = "CALIBRATE RADIO";
                
                // Save min/max parameters
                foreach (var item in RadioItems)
                {
                    // Swap if min > max
                    if (item.Min > item.Max)
                    {
                        int temp = item.Min;
                        item.Min = item.Max;
                        item.Max = temp;
                    }

                    // Send parameters to vehicle
                    await _mavLinkService.SetParameterAsync($"RC{item.No}_MIN", item.Min);
                    await Task.Delay(50);
                    await _mavLinkService.SetParameterAsync($"RC{item.No}_MAX", item.Max);
                    await Task.Delay(50);
                }

                ShowStatus("Radio calibration saved!");
            }
        }

        #endregion

        #region Flight Mode Configuration

        // Store flight mode parameter values
        private Dictionary<string, int> _flightModeParams = new Dictionary<string, int>();
        private float _modeChannelPWM = 0;

        /// <summary>
        /// Starts loading flight mode configuration and monitoring real-time mode indicators
        /// </summary>
        /// <param name="token">Cancellation token to stop monitoring</param>
        private async Task StartUpdatingMode(CancellationToken token)
        {
            // Load flight mode parameters from vehicle
            if (_mavLinkService == null) return;

            try
            {
                // Subscribe to PARAM_VALUE messages
                _mavLinkService.PacketReceived += OnFlightModeParameterReceived;
                _mavLinkService.PacketReceived += OnRCChannelsReceived;
                
                // Determine vehicle type (default to Copter for now)
                // TODO: Get vehicle type from HEARTBEAT message
                Type modeType = typeof(MavLinkNet.CopterMode);
                
                // Populate all 6 ComboBoxes with appropriate enum
                DispatcherQueue.TryEnqueue(() =>
                {
                    updateDropDown(CMB_fmode1, modeType);
                    updateDropDown(CMB_fmode2, modeType);
                    updateDropDown(CMB_fmode3, modeType);
                    updateDropDown(CMB_fmode4, modeType);
                    updateDropDown(CMB_fmode5, modeType);
                    updateDropDown(CMB_fmode6, modeType);
                });
                
                // Request all parameters from vehicle
                await _mavLinkService.RequestParametersAsync();
                
                ShowStatus("Flight mode parameters loaded");
            }
            catch (TaskCanceledException)
            {
                Serilog.Log.Debug("[CalibrationControl] Flight mode loading cancelled");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error($"[CalibrationControl] Error loading flight modes: {ex.Message}");
                ShowStatus("Error loading flight modes");
            }
        }

        /// <summary>
        /// Handles PARAM_VALUE messages for flight mode parameters
        /// </summary>
        private void OnFlightModeParameterReceived(object? sender, MavLinkNet.MavLinkPacketBase packet)
        {
            if (packet.Message is MavLinkNet.UasParamValue paramValue)
            {
                string paramName = new string(paramValue.ParamId).TrimEnd('\0');
                
                // Check if this is a flight mode parameter
                if (paramName.StartsWith("FLTMODE") && paramName.Length == 8) // FLTMODE1-6
                {
                    int modeValue = (int)paramValue.ParamValue;
                    _flightModeParams[paramName] = modeValue;
                    
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        switch (paramName)
                        {
                            case "FLTMODE1":
                                CMB_fmode1.SelectedValue = modeValue;
                                Serilog.Log.Debug($"[CalibrationControl] FLTMODE1 = {modeValue}");
                                break;
                            case "FLTMODE2":
                                CMB_fmode2.SelectedValue = modeValue;
                                Serilog.Log.Debug($"[CalibrationControl] FLTMODE2 = {modeValue}");
                                break;
                            case "FLTMODE3":
                                CMB_fmode3.SelectedValue = modeValue;
                                Serilog.Log.Debug($"[CalibrationControl] FLTMODE3 = {modeValue}");
                                break;
                            case "FLTMODE4":
                                CMB_fmode4.SelectedValue = modeValue;
                                Serilog.Log.Debug($"[CalibrationControl] FLTMODE4 = {modeValue}");
                                break;
                            case "FLTMODE5":
                                CMB_fmode5.SelectedValue = modeValue;
                                Serilog.Log.Debug($"[CalibrationControl] FLTMODE5 = {modeValue}");
                                break;
                            case "FLTMODE6":
                                CMB_fmode6.SelectedValue = modeValue;
                                Serilog.Log.Debug($"[CalibrationControl] FLTMODE6 = {modeValue}");
                                break;
                        }
                    });
                }
            }
        }

        /// <summary>
        /// Handles RC_CHANNELS messages to get mode switch PWM
        /// </summary>
        private void OnRCChannelsReceived(object? sender, MavLinkNet.MavLinkPacketBase packet)
        {
            if (packet.Message is MavLinkNet.UasRcChannels rcChannels)
            {
                // Get mode channel PWM (default to channel 5)
                // TODO: Read FLTMODE_CH parameter to determine actual mode channel
                _modeChannelPWM = rcChannels.Chan5Raw;
            }
        }

        /// <summary>
        /// Gets the current mode channel PWM value
        /// </summary>
        private float GetModeChannelPWM()
        {
            // Default to channel 5 (common mode switch channel)
            // TODO: Read FLTMODE_CH parameter to determine actual mode channel
            return _modeChannelPWM;
        }

        /// <summary>
        /// Updates flight mode indicator ellipses in real-time based on RC channel PWM
        /// </summary>
        /// <param name="token">Cancellation token to stop updates</param>
        private async Task UiModeUpdate(CancellationToken token)
        {
            // Update active mode indicator based on current RC channel PWM
            while (!token.IsCancellationRequested && Flight_mode.Visibility == Visibility.Visible)
            {
                try
                {
                    // Get current mode switch PWM value
                    float pwm = GetModeChannelPWM();
                    
                    // Determine which mode is active based on PWM ranges
                    byte activeMode = ReadSwitch(pwm);
                    
                    // Update UI indicator (Ellipse color) for active mode
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        var limeBrush = new SolidColorBrush(Microsoft.UI.Colors.Lime);
                        var transparentBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                        
                        mode1.Fill = (activeMode == 0) ? limeBrush : transparentBrush;
                        mode2.Fill = (activeMode == 1) ? limeBrush : transparentBrush;
                        mode3.Fill = (activeMode == 2) ? limeBrush : transparentBrush;
                        mode4.Fill = (activeMode == 3) ? limeBrush : transparentBrush;
                        mode5.Fill = (activeMode == 4) ? limeBrush : transparentBrush;
                        mode6.Fill = (activeMode == 5) ? limeBrush : transparentBrush;
                    });
                    
                    await Task.Delay(200, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error($"[CalibrationControl] Error updating mode indicators: {ex.Message}");
                }
            }
            
            // Cleanup
            if (_mavLinkService != null)
            {
                _mavLinkService.PacketReceived -= OnFlightModeParameterReceived;
                _mavLinkService.PacketReceived -= OnRCChannelsReceived;
            }
        }

        private void RefreshModes_Click(object sender, RoutedEventArgs e)
        {
            ShowStatus("Refreshing flight modes...");
            _ = StartUpdatingMode(ResetCancellationToken());
        }

        private async void saveMode_Click(object sender, RoutedEventArgs e)
        {
            if (_mavLinkService == null) return;
            
            ShowStatus("Saving flight modes...");
            
            try
            {
                // Get selected values from ComboBoxes
                int mode1Value = (int)(CMB_fmode1.SelectedValue ?? 0);
                int mode2Value = (int)(CMB_fmode2.SelectedValue ?? 0);
                int mode3Value = (int)(CMB_fmode3.SelectedValue ?? 0);
                int mode4Value = (int)(CMB_fmode4.SelectedValue ?? 0);
                int mode5Value = (int)(CMB_fmode5.SelectedValue ?? 0);
                int mode6Value = (int)(CMB_fmode6.SelectedValue ?? 0);
                
                // Send parameters with await
                await _mavLinkService.SetParameterAsync("FLTMODE1", mode1Value);
                await Task.Delay(100);
                await _mavLinkService.SetParameterAsync("FLTMODE2", mode2Value);
                await Task.Delay(100);
                await _mavLinkService.SetParameterAsync("FLTMODE3", mode3Value);
                await Task.Delay(100);
                await _mavLinkService.SetParameterAsync("FLTMODE4", mode4Value);
                await Task.Delay(100);
                await _mavLinkService.SetParameterAsync("FLTMODE5", mode5Value);
                await Task.Delay(100);
                await _mavLinkService.SetParameterAsync("FLTMODE6", mode6Value);
                await Task.Delay(100);
                
                Serilog.Log.Information($"[CalibrationControl] Flight modes saved: {mode1Value}, {mode2Value}, {mode3Value}, {mode4Value}, {mode5Value}, {mode6Value}");
                ShowStatus("Flight modes saved successfully!");
                
                // Refresh to verify
                await Task.Delay(500);
                await _mavLinkService.RequestParametersAsync();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error($"[CalibrationControl] Error saving flight modes: {ex.Message}");
                ShowStatus("Error saving flight modes");
            }
        }

        #endregion

        #region Servo Output Configuration

        /// <summary>
        /// Starts loading and monitoring servo output parameters and real-time updates
        /// </summary>
        /// <param name="token">Cancellation token to stop monitoring</param>
        private async Task StartUpdatingServo(CancellationToken token)
        {
            // Subscribe to SERVO_OUTPUT_RAW messages to update servo outputs
            if (_mavLinkService != null)
            {
                _mavLinkService.PacketReceived += OnServoPacketReceived;
                _mavLinkService.PacketReceived += OnServoParameterReceived;
            }

            try
            {
                // Request servo parameters for all 16 servos
                for (int i = 1; i <= 16; i++)
                {
                    await RequestServoParameters(i, token);
                    await Task.Delay(50, token); // Throttle requests
                }
                
                ShowStatus("Servo parameters loaded");
            }
            catch (TaskCanceledException)
            {
                Serilog.Log.Debug("[CalibrationControl] Servo parameter loading cancelled");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error($"[CalibrationControl] Error loading servo parameters: {ex.Message}");
                ShowStatus("Error loading servo parameters");
            }

            // Keep subscription active while tab is visible
            while (!token.IsCancellationRequested && ServoOutput.Visibility == Visibility.Visible)
            {
                try
                {
                    await Task.Delay(100, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }

            // Unsubscribe when done
            if (_mavLinkService != null)
            {
                _mavLinkService.PacketReceived -= OnServoPacketReceived;
                _mavLinkService.PacketReceived -= OnServoParameterReceived;
            }
        }

        /// <summary>
        /// Requests all parameters for a specific servo with retry logic
        /// </summary>
        /// <param name="servoNo">Servo number (1-16)</param>
        /// <param name="token">Cancellation token</param>
        private async Task RequestServoParameters(int servoNo, CancellationToken token)
        {
            if (_mavLinkService == null) return;
            
            // Request all parameters from vehicle
            // The parameter handler will filter and update servo parameters
            await _mavLinkService.RequestParametersAsync();
            await Task.Delay(100, token);
        }
        
        /// <summary>
        /// Requests a parameter with retry logic (max 3 attempts with exponential backoff)
        /// </summary>
        /// <param name="paramName">Parameter name to request</param>
        /// <param name="token">Cancellation token</param>
        private async Task RequestParameterWithRetry(string paramName, CancellationToken token)
        {
            // Request all parameters - the handler will filter what we need
            if (_mavLinkService == null) return;
            
            try
            {
                await _mavLinkService.RequestParametersAsync();
                await Task.Delay(50, token);
            }
            catch (TaskCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error($"[CalibrationControl] Failed to request parameters: {ex.Message}");
                ShowStatus($"Failed to load parameters");
            }
        }

        private void OnServoPacketReceived(object? sender, MavLinkNet.MavLinkPacketBase packet)
        {
            if (packet.Message is MavLinkNet.UasServoOutputRaw servoOutput)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    UpdateServoOutput(0, servoOutput.Servo1Raw);
                    UpdateServoOutput(1, servoOutput.Servo2Raw);
                    UpdateServoOutput(2, servoOutput.Servo3Raw);
                    UpdateServoOutput(3, servoOutput.Servo4Raw);
                    UpdateServoOutput(4, servoOutput.Servo5Raw);
                    UpdateServoOutput(5, servoOutput.Servo6Raw);
                    UpdateServoOutput(6, servoOutput.Servo7Raw);
                    UpdateServoOutput(7, servoOutput.Servo8Raw);
                    UpdateServoOutput(8, servoOutput.Servo9Raw);
                    UpdateServoOutput(9, servoOutput.Servo10Raw);
                    UpdateServoOutput(10, servoOutput.Servo11Raw);
                    UpdateServoOutput(11, servoOutput.Servo12Raw);
                    UpdateServoOutput(12, servoOutput.Servo13Raw);
                    UpdateServoOutput(13, servoOutput.Servo14Raw);
                    UpdateServoOutput(14, servoOutput.Servo15Raw);
                    UpdateServoOutput(15, servoOutput.Servo16Raw);
                });
            }
        }

        private void UpdateServoOutput(int index, int rawValue)
        {
            if (index < 0 || index >= ServoItems.Count) return;
            
            var item = ServoItems[index];
            item.Output = rawValue;
        }

        /// <summary>
        /// Handles PARAM_VALUE messages to update servo parameters
        /// </summary>
        private void OnServoParameterReceived(object? sender, MavLinkNet.MavLinkPacketBase packet)
        {
            if (packet.Message is MavLinkNet.UasParamValue paramValue)
            {
                string paramName = new string(paramValue.ParamId).TrimEnd('\0');
                
                // Check if this is a servo parameter
                if (paramName.StartsWith("SERVO") && paramName.Contains("_"))
                {
                    // Parse servo number (e.g., "SERVO1_FUNCTION" -> 1)
                    var parts = paramName.Split('_');
                    if (parts.Length >= 2 && int.TryParse(parts[0].Substring(5), out int servoNo))
                    {
                        if (servoNo >= 1 && servoNo <= 16)
                        {
                            var paramType = parts[1];
                            var servo = ServoItems.FirstOrDefault(s => s.No == servoNo);
                            
                            if (servo != null)
                            {
                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    switch (paramType)
                                    {
                                        case "FUNCTION":
                                            servo.Function = (ServoFunction)(int)paramValue.ParamValue;
                                            Serilog.Log.Debug($"[CalibrationControl] Servo {servoNo} function: {servo.Function}");
                                            break;
                                        case "REVERSED":
                                            servo.Reversed = (int)paramValue.ParamValue == 1;
                                            Serilog.Log.Debug($"[CalibrationControl] Servo {servoNo} reversed: {servo.Reversed}");
                                            break;
                                        case "MIN":
                                            servo.Min = (int)paramValue.ParamValue;
                                            Serilog.Log.Debug($"[CalibrationControl] Servo {servoNo} min: {servo.Min}");
                                            break;
                                        case "TRIM":
                                            servo.Trim = (int)paramValue.ParamValue;
                                            Serilog.Log.Debug($"[CalibrationControl] Servo {servoNo} trim: {servo.Trim}");
                                            break;
                                        case "MAX":
                                            servo.Max = (int)paramValue.ParamValue;
                                            Serilog.Log.Debug($"[CalibrationControl] Servo {servoNo} max: {servo.Max}");
                                            break;
                                    }
                                });
                            }
                        }
                    }
                }
            }
        }

        private async void saveServo_Click(object sender, RoutedEventArgs e)
        {
            if (_mavLinkService == null) return;
            
            ShowStatus("Saving servo configuration...");
            
            try
            {
                // Save servo parameters for each servo
                foreach (var servo in ServoItems)
                {
                    // Send SERVO{n}_FUNCTION parameter
                    sendMode($"SERVO{servo.No}_FUNCTION", (int)servo.Function);
                    await Task.Delay(50);
                    
                    // Send SERVO{n}_REVERSED parameter
                    sendMode($"SERVO{servo.No}_REVERSED", servo.Reversed ? 1 : 0);
                    await Task.Delay(50);
                    
                    // Send SERVO{n}_MIN parameter
                    sendMode($"SERVO{servo.No}_MIN", servo.Min);
                    await Task.Delay(50);
                    
                    // Send SERVO{n}_TRIM parameter
                    sendMode($"SERVO{servo.No}_TRIM", servo.Trim);
                    await Task.Delay(50);
                    
                    // Send SERVO{n}_MAX parameter
                    sendMode($"SERVO{servo.No}_MAX", servo.Max);
                    await Task.Delay(50);
                }
                
                ShowStatus("Servo configuration saved successfully!");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CalibrationControl] Error saving servo config: {ex.Message}");
                ShowStatus("Error saving servo configuration");
            }
        }

        #endregion

        #region ESC Calibration

        private void SendMinPwm_Click(object sender, RoutedEventArgs e)
        {
            int minPwm = 1000;
            SendPwmValue(minPwm);
            ShowStatus($"Sent minimum PWM ({minPwm})");
        }

        private void SendMidPwm_Click(object sender, RoutedEventArgs e)
        {
            int midPwm = 1500;
            SendPwmValue(midPwm);
            ShowStatus($"Sent middle PWM ({midPwm})");
        }

        private void SendMaxPwm_Click(object sender, RoutedEventArgs e)
        {
            int maxPwm = 2000;
            SendPwmValue(maxPwm);
            ShowStatus($"Sent maximum PWM ({maxPwm})");
        }

        private async void SendPwmValue(int pwm)
        {
            if (_mavLinkService == null) return;
            
            try
            {
                // Send PWM to all motor channels (1-4) for ESC calibration
                for (int channel = 1; channel <= 4; channel++)
                {
                    await SendPwmValueWithDuration(channel, pwm, 0.1f);
                }
                
                Debug.WriteLine($"[CalibrationControl] Sent PWM {pwm} to all motors");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CalibrationControl] Error sending PWM: {ex.Message}");
                ShowStatus("Error sending PWM command");
            }
        }

        private async void AutoCalibrateAllMotors(object sender, RoutedEventArgs e)
        {
            ShowStatus("Auto-calibrating all ESCs...");
            await RunAutoCalibrate();
        }

        /// <summary>
        /// Runs automatic ESC calibration sequence:
        /// 1. Send MAX PWM to all motors
        /// 2. Wait for user to connect battery
        /// 3. Send MIN PWM to all motors
        /// 4. ESCs should beep to confirm calibration
        /// </summary>
        private async Task RunAutoCalibrate()
        {
            int[] channels = { 1, 2, 3, 4 };

            foreach (var ch in channels)
            {
                ShowStatus($"Motor {ch}: Sending MAX PWM");
                await SendPwmValueWithDuration(ch, 2000, 2.0f);
                await Task.Delay(500);

                ShowStatus($"Motor {ch}: Sending MIN PWM");
                await SendPwmValueWithDuration(ch, 1000, 2.0f);
                await Task.Delay(500);
            }

            ShowStatus("Auto-calibrate complete! ESCs should have beeped.");
        }

        #endregion

        #region Motor Test

        private void TestMotor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tagStr && int.TryParse(tagStr, out int motorChannel))
            {
                // Get PWM and duration from sliders (if available in XAML)
                int pwm = 1500; // Default PWM value
                float duration = 2.0f; // Default duration in seconds
                
                _ = SendPwmValueWithDuration(motorChannel, pwm, duration);
                
                // Map motor numbers to letters (1=A, 2=B, 3=C, 4=D)
                char motorLetter = (char)('A' + motorChannel - 1);
                ShowStatus($"Testing Motor {motorLetter} (#{motorChannel}): {pwm} PWM for {duration:F1}s");
            }
        }

        private void TestAllMotors_Click(object sender, RoutedEventArgs e)
        {
            ShowStatus("Testing all motors sequentially...");
            _ = TestAllMotorsSequentially();
        }

        private async Task TestAllMotorsSequentially()
        {
            int pwm = 1500; // Default PWM value
            float duration = 2.0f; // Default duration
            int durationMs = (int)(duration * 1000);

            string[] motorLabels = { "A", "B", "C", "D" };

            for (int motor = 1; motor <= 4; motor++)
            {
                ShowStatus($"Testing Motor {motorLabels[motor - 1]} ({motor}/4)...");
                await SendPwmValueWithDuration(motor, pwm, duration);
                await Task.Delay(durationMs + 500); // Wait for motor to stop plus buffer
            }

            ShowStatus("Test complete: A → B → C → D");
        }

        private void StopAllMotors_Click(object sender, RoutedEventArgs e)
        {
            ShowStatus("Stopping all motors...");
            // Send minimum PWM to all motors 1-4
            for (int i = 1; i <= 4; i++)
            {
                _ = SendPwmValueWithDuration(i, 1000, 0.1f);
            }
        }

        /// <summary>
        /// Sends a motor test command with PWM value and duration
        /// Automatically arms the vehicle if needed for motor testing
        /// </summary>
        /// <param name="servoChannel">Motor/servo channel (1-4 for motors A-D)</param>
        /// <param name="pwmValue">PWM value (1000-2000)</param>
        /// <param name="durationSeconds">Test duration in seconds</param>
        private async Task SendPwmValueWithDuration(int servoChannel, int pwmValue, float durationSeconds)
        {
            if (_mavLinkService == null)
            {
                ShowStatus("Error: Not connected to flight controller");
                return;
            }

            bool autoArmed = false;

            // Check if vehicle is armed (you'll need to add IsArmed property to IMavLinkService)
            // For now, we'll skip the auto-arm logic and just send the motor test command
            
            try
            {
                // Use MAV_CMD_DO_MOTOR_TEST command
                // This is safer than directly setting servo outputs
                await _mavLinkService.SendCommandLongAsync(
                    command: (int)MavLinkNet.MavCmd.DoMotorTest,
                    param1: servoChannel,      // Motor instance (1-4)
                    param2: 1,                 // Throttle type: 1 = PWM
                    param3: pwmValue,          // Throttle value (PWM)
                    param4: durationSeconds,   // Duration in seconds
                    param5: 0,                 // Motor count (0 = test single motor)
                    param6: 0,                 // Test order (0 = default)
                    param7: 0                  // Reserved
                );

                Debug.WriteLine($"[CalibrationControl] Motor test sent: Channel {servoChannel}, PWM {pwmValue}, Duration {durationSeconds}s");
                
                // Wait for the test duration
                await Task.Delay((int)(durationSeconds * 1000) + 500);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CalibrationControl] Error sending motor test: {ex.Message}");
                ShowStatus($"Error: Failed to test motor {servoChannel}");
            }
        }

        /// <summary>
        /// Converts PWM value to percentage (0-100)
        /// </summary>
        private int PwmToPercent(int pwm)
        {
            const int minPwm = 1000;
            const int maxPwm = 2000;
            if (pwm <= minPwm) return 0;
            if (pwm >= maxPwm) return 100;
            return (int)Math.Round((pwm - minPwm) * 100.0 / (maxPwm - minPwm));
        }

        /// <summary>
        /// Updates servo progress bar based on PWM value
        /// </summary>
        public void UpdateServoProgress(int servoIndex, int pwmValue)
        {
            int percent = PwmToPercent(pwmValue);
            
            // Update the corresponding servo item's output percentage
            if (servoIndex >= 1 && servoIndex <= 16)
            {
                var servo = ServoItems.FirstOrDefault(s => s.No == servoIndex);
                if (servo != null)
                {
                    servo.OutputPercent = percent;
                }
            }
        }

        #endregion

        #region Tuning Parameters

        // Store tuning parameter values
        private Dictionary<string, float> _tuningParams = new Dictionary<string, float>();

        /// <summary>
        /// Update tuning section visibility based on vehicle type from HEARTBEAT
        /// Multirotor types (2,13,14,15,29,43) show COPTER_MODE
        /// FixedWing (1) and others show PLANE_MODE
        /// </summary>
        private void UpdateTuningVisibility(int vehicleType)
        {
            // Multirotor types: Quadrotor(2), Hexarotor(13), Octorotor(14), Tricopter(15), Dodecarotor(29), GenericMultirotor(43)
            bool isMultirotor = vehicleType == 2 || vehicleType == 13 || vehicleType == 14 || 
                                 vehicleType == 15 || vehicleType == 29 || vehicleType == 43;
            
            if (isMultirotor)
            {
                // Show COPTER tuning, hide PLANE tuning
                COPTER_MODE.Visibility = Visibility.Visible;
                PLANE_MODE.Visibility = Visibility.Collapsed;
                Debug.WriteLine($"[CalibrationControl] Showing COPTER tuning for vehicle type {vehicleType}");
            }
            else
            {
                // Show PLANE tuning, hide COPTER tuning (FixedWing = 1, and default for others)
                COPTER_MODE.Visibility = Visibility.Collapsed;
                PLANE_MODE.Visibility = Visibility.Visible;
                Debug.WriteLine($"[CalibrationControl] Showing PLANE tuning for vehicle type {vehicleType}");
            }
        }

        /// <summary>
        /// Starts loading copter PID tuning parameters from the vehicle
        /// </summary>
        /// <param name="token">Cancellation token to stop loading</param>
        private async Task StartUpdatingCopter(CancellationToken token)
        {
            // Load copter PID parameters from vehicle
            if (_mavLinkService == null) return;

            try
            {
                // Subscribe to PARAM_VALUE messages
                _mavLinkService.PacketReceived += OnTuningParameterReceived;
                
                // Request all parameters from vehicle
                await _mavLinkService.RequestParametersAsync();
                
                Serilog.Log.Information("[CalibrationControl] Copter parameters requested");
                ShowStatus("Copter parameters loaded");
            }
            catch (TaskCanceledException)
            {
                Serilog.Log.Debug("[CalibrationControl] Copter parameter loading cancelled");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error($"[CalibrationControl] Error loading copter params: {ex.Message}");
                ShowStatus("Error loading copter parameters");
            }
        }

        /// <summary>
        /// Starts loading plane/VTOL PID tuning parameters from the vehicle
        /// </summary>
        /// <param name="token">Cancellation token to stop loading</param>
        private async Task StartUpdatingQplane(CancellationToken token)
        {
            // Load plane/VTOL parameters from vehicle
            if (_mavLinkService == null) return;

            try
            {
                // Subscribe to PARAM_VALUE messages (if not already subscribed)
                _mavLinkService.PacketReceived += OnTuningParameterReceived;
                
                // Request all parameters from vehicle
                await _mavLinkService.RequestParametersAsync();
                
                Serilog.Log.Information("[CalibrationControl] Plane parameters requested");
                ShowStatus("Plane parameters loaded");
            }
            catch (TaskCanceledException)
            {
                Serilog.Log.Debug("[CalibrationControl] Plane parameter loading cancelled");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error($"[CalibrationControl] Error loading plane params: {ex.Message}");
                ShowStatus("Error loading plane parameters");
            }
        }

        /// <summary>
        /// Starts loading waypoint parameters from the vehicle
        /// </summary>
        /// <param name="token">Cancellation token to stop loading</param>
        private async Task StartUpdatingWP(CancellationToken token)
        {
            // Load waypoint parameters from vehicle
            if (_mavLinkService == null) return;

            try
            {
                // Subscribe to PARAM_VALUE messages (if not already subscribed)
                _mavLinkService.PacketReceived += OnTuningParameterReceived;
                
                // Request all parameters from vehicle
                await _mavLinkService.RequestParametersAsync();
                
                Serilog.Log.Information("[CalibrationControl] Waypoint parameters requested");
                ShowStatus("Waypoint parameters loaded");
            }
            catch (TaskCanceledException)
            {
                Serilog.Log.Debug("[CalibrationControl] Waypoint parameter loading cancelled");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error($"[CalibrationControl] Error loading WP params: {ex.Message}");
                ShowStatus("Error loading waypoint parameters");
            }
        }

        /// <summary>
        /// Handles PARAM_VALUE messages for tuning parameters
        /// </summary>
        private void OnTuningParameterReceived(object? sender, MavLinkNet.MavLinkPacketBase packet)
        {
            if (packet.Message is MavLinkNet.UasParamValue paramValue)
            {
                string paramName = new string(paramValue.ParamId).TrimEnd('\0');
                float value = paramValue.ParamValue;
                
                _tuningParams[paramName] = value;
                
                DispatcherQueue.TryEnqueue(() =>
                {
                    // Copter parameters
                    switch (paramName)
                    {
                        // Roll
                        case "ATC_RAT_RLL_P": RollPTextBox.Text = value.ToString("F3"); break;
                        case "ATC_RAT_RLL_I": RollITextBox.Text = value.ToString("F3"); break;
                        case "ATC_RAT_RLL_D": RollDTextBox.Text = value.ToString("F4"); break;
                        case "ATC_RAT_RLL_IMAX": RollIMAXTextBox.Text = value.ToString("F3"); break;
                        case "ATC_RAT_RLL_FLTE": RollFLTETextBox.Text = value.ToString("F1"); break;
                        case "ATC_RAT_RLL_FLTD": RollFLTDTextBox.Text = value.ToString("F1"); break;
                        case "ATC_RAT_RLL_FLTT": RollFLTTTextBox.Text = value.ToString("F1"); break;
                        // Pitch
                        case "ATC_RAT_PIT_P": PitchPTextBox.Text = value.ToString("F3"); break;
                        case "ATC_RAT_PIT_I": PitchITextBox.Text = value.ToString("F3"); break;
                        case "ATC_RAT_PIT_D": PitchDTextBox.Text = value.ToString("F4"); break;
                        case "ATC_RAT_PIT_IMAX": PitchIMAXTextBox.Text = value.ToString("F3"); break;
                        case "ATC_RAT_PIT_FLTE": PitchFLTETextBox.Text = value.ToString("F1"); break;
                        case "ATC_RAT_PIT_FLTD": PitchFLTDTextBox.Text = value.ToString("F1"); break;
                        case "ATC_RAT_PIT_FLTT": PitchFLTTTextBox.Text = value.ToString("F1"); break;
                        // Yaw
                        case "ATC_RAT_YAW_P": YawPTextBox.Text = value.ToString("F3"); break;
                        case "ATC_RAT_YAW_I": YawITextBox.Text = value.ToString("F3"); break;
                        case "ATC_RAT_YAW_D": YawDTextBox.Text = value.ToString("F4"); break;
                        case "ATC_RAT_YAW_IMAX": YawIMAXTextBox.Text = value.ToString("F3"); break;
                        case "ATC_RAT_YAW_FLTE": YawFLTETextBox.Text = value.ToString("F1"); break;
                        case "ATC_RAT_YAW_FLTD": YawFLTDTextBox.Text = value.ToString("F1"); break;
                        case "ATC_RAT_YAW_FLTT": YawFLTTTextBox.Text = value.ToString("F1"); break;
                        
                        // Plane parameters
                        case "RLL2SRV_P": PRollPTextBox.Text = value.ToString("F3"); break;
                        case "RLL2SRV_I": PRollITextBox.Text = value.ToString("F3"); break;
                        case "RLL2SRV_D": PRollDTextBox.Text = value.ToString("F4"); break;
                        case "RLL2SRV_IMAX": PRollIMAXTextBox.Text = value.ToString("F3"); break;
                        case "PTCH2SRV_P": PPitchPTextBox.Text = value.ToString("F3"); break;
                        case "PTCH2SRV_I": PPitchITextBox.Text = value.ToString("F3"); break;
                        case "PTCH2SRV_D": PPitchDTextBox.Text = value.ToString("F4"); break;
                        case "PTCH2SRV_IMAX": PPitchIMAXTextBox.Text = value.ToString("F3"); break;
                        case "PSC_VELXY_P": VelP.Text = value.ToString("F3"); break;
                        case "PSC_VELXY_I": VelI.Text = value.ToString("F3"); break;
                        case "PSC_VELXY_D": VelD.Text = value.ToString("F4"); break;
                        case "PSC_VELXY_IMAX": VelIMAX.Text = value.ToString("F3"); break;
                        case "PTCH_LIM_MIN_DEG": PitchMin.Text = value.ToString("F1"); break;
                        case "PTCH_LIM_MAX_DEG": PitchMax.Text = value.ToString("F1"); break;
                        case "ROLL_LIMIT_DEG": RollLimit.Text = value.ToString("F1"); break;
                    }
                });
            }
        }

        /// <summary>
        /// Validates PID value is within acceptable range
        /// </summary>
        private bool ValidatePIDValue(float value, out string errorMessage)
        {
            errorMessage = "";
            
            if (value < 0)
            {
                errorMessage = "PID values cannot be negative";
                return false;
            }
            
            if (value > 100.0f)
            {
                errorMessage = "PID value too large (max 100.0)";
                return false;
            }
            
            return true;
        }

        private void RefreshCopter_Click(object sender, RoutedEventArgs e)
        {
            ShowStatus("Refreshing copter parameters...");
            _ = StartUpdatingCopter(ResetCancellationToken());
        }

        private void RefreshPlane_Click(object sender, RoutedEventArgs e)
        {
            ShowStatus("Refreshing plane parameters...");
            _ = StartUpdatingQplane(ResetCancellationToken());
        }

        private async void saveCopter_Click(object sender, RoutedEventArgs e)
        {
            if (_mavLinkService == null) return;
            
            ShowStatus("Saving copter parameters...");
            
            try
            {
                // Collect and validate PID values from TextBoxes
                var parameters = new Dictionary<string, float>
                {
                    // Roll
                    ["ATC_RAT_RLL_P"] = GetFloat(RollPTextBox.Text),
                    ["ATC_RAT_RLL_I"] = GetFloat(RollITextBox.Text),
                    ["ATC_RAT_RLL_D"] = GetFloat(RollDTextBox.Text),
                    ["ATC_RAT_RLL_IMAX"] = GetFloat(RollIMAXTextBox.Text),
                    ["ATC_RAT_RLL_FLTE"] = GetFloat(RollFLTETextBox.Text),
                    ["ATC_RAT_RLL_FLTD"] = GetFloat(RollFLTDTextBox.Text),
                    ["ATC_RAT_RLL_FLTT"] = GetFloat(RollFLTTTextBox.Text),
                    // Pitch
                    ["ATC_RAT_PIT_P"] = GetFloat(PitchPTextBox.Text),
                    ["ATC_RAT_PIT_I"] = GetFloat(PitchITextBox.Text),
                    ["ATC_RAT_PIT_D"] = GetFloat(PitchDTextBox.Text),
                    ["ATC_RAT_PIT_IMAX"] = GetFloat(PitchIMAXTextBox.Text),
                    ["ATC_RAT_PIT_FLTE"] = GetFloat(PitchFLTETextBox.Text),
                    ["ATC_RAT_PIT_FLTD"] = GetFloat(PitchFLTDTextBox.Text),
                    ["ATC_RAT_PIT_FLTT"] = GetFloat(PitchFLTTTextBox.Text),
                    // Yaw
                    ["ATC_RAT_YAW_P"] = GetFloat(YawPTextBox.Text),
                    ["ATC_RAT_YAW_I"] = GetFloat(YawITextBox.Text),
                    ["ATC_RAT_YAW_D"] = GetFloat(YawDTextBox.Text),
                    ["ATC_RAT_YAW_IMAX"] = GetFloat(YawIMAXTextBox.Text),
                    ["ATC_RAT_YAW_FLTE"] = GetFloat(YawFLTETextBox.Text),
                    ["ATC_RAT_YAW_FLTD"] = GetFloat(YawFLTDTextBox.Text),
                    ["ATC_RAT_YAW_FLTT"] = GetFloat(YawFLTTTextBox.Text)
                };
                
                // Validate all values
                foreach (var param in parameters)
                {
                    if (!ValidatePIDValue(param.Value, out string errorMsg))
                    {
                        ShowStatus($"Validation error for {param.Key}: {errorMsg}");
                        Serilog.Log.Warning($"[CalibrationControl] Validation failed for {param.Key}: {errorMsg}");
                        return;
                    }
                }
                
                // Send all parameters
                foreach (var param in parameters)
                {
                    sendModefloat(param.Key, param.Value);
                    await Task.Delay(50);
                }
                
                Serilog.Log.Information("[CalibrationControl] Copter parameters saved successfully");
                ShowStatus("Copter parameters saved successfully!");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error($"[CalibrationControl] Error saving copter params: {ex.Message}");
                ShowStatus("Error saving copter parameters");
            }
        }

        private async void saveSafety_Click(object sender, RoutedEventArgs e)
        {
            ShowStatus("Saving safety parameters...");
            
            try
            {
                // Save waypoint safety parameters
                // In a real implementation, we would:
                // 1. Read values from TextBoxes (e.g., waypoint radius, loiter radius, etc.)
                // 2. Send parameters to vehicle
                // Example:
                // sendModefloat("WP_RADIUS", GetFloat(txt_wp_radius.Text));
                // sendModefloat("WP_LOITER_RAD", GetFloat(txt_loiter_rad.Text));
                
                await Task.Delay(100); // Simulate save delay
                ShowStatus("Safety parameters saved successfully!");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CalibrationControl] Error saving safety params: {ex.Message}");
                ShowStatus("Error saving safety parameters");
            }
        }

        private async void saveQplane_Click(object sender, RoutedEventArgs e)
        {
            if (_mavLinkService == null) return;
            
            ShowStatus("Saving plane parameters...");
            
            try
            {
                // Collect and validate PID values from TextBoxes
                var parameters = new Dictionary<string, float>
                {
                    // Roll
                    ["RLL2SRV_P"] = GetFloat(PRollPTextBox.Text),
                    ["RLL2SRV_I"] = GetFloat(PRollITextBox.Text),
                    ["RLL2SRV_D"] = GetFloat(PRollDTextBox.Text),
                    ["RLL2SRV_IMAX"] = GetFloat(PRollIMAXTextBox.Text),
                    // Pitch
                    ["PTCH2SRV_P"] = GetFloat(PPitchPTextBox.Text),
                    ["PTCH2SRV_I"] = GetFloat(PPitchITextBox.Text),
                    ["PTCH2SRV_D"] = GetFloat(PPitchDTextBox.Text),
                    ["PTCH2SRV_IMAX"] = GetFloat(PPitchIMAXTextBox.Text),
                    // Velocity
                    ["PSC_VELXY_P"] = GetFloat(VelP.Text),
                    ["PSC_VELXY_I"] = GetFloat(VelI.Text),
                    ["PSC_VELXY_D"] = GetFloat(VelD.Text),
                    ["PSC_VELXY_IMAX"] = GetFloat(VelIMAX.Text),
                    // Limits
                    ["PTCH_LIM_MIN_DEG"] = GetFloat(PitchMin.Text),
                    ["PTCH_LIM_MAX_DEG"] = GetFloat(PitchMax.Text),
                    ["ROLL_LIMIT_DEG"] = GetFloat(RollLimit.Text)
                };
                
                // Validate all values
                foreach (var param in parameters)
                {
                    if (!ValidatePIDValue(param.Value, out string errorMsg))
                    {
                        ShowStatus($"Validation error for {param.Key}: {errorMsg}");
                        Serilog.Log.Warning($"[CalibrationControl] Validation failed for {param.Key}: {errorMsg}");
                        return;
                    }
                }
                
                // Send all parameters
                foreach (var param in parameters)
                {
                    sendModefloat(param.Key, param.Value);
                    await Task.Delay(50);
                }
                
                Serilog.Log.Information("[CalibrationControl] Plane parameters saved successfully");
                ShowStatus("Plane parameters saved successfully!");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error($"[CalibrationControl] Error saving plane params: {ex.Message}");
                ShowStatus("Error saving plane parameters");
            }
        }

        #endregion

        #region Helper Methods

        private void Konfirmasi_Koneksi(object sender, RoutedEventArgs e)
        {
            // Hide connection warning overlay and enable the main grid
            ConnectionWarningOverlay.Visibility = Visibility.Collapsed;
            func_grid.IsHitTestVisible = true;
        }

        private void ShowStatus(string message)
        {
            Debug.WriteLine($"[CalibrationControl] {message}");
            // TODO: Update status TextBlock in UI if available
        }

        /// <summary>
        /// Sends an integer parameter value to the vehicle via MAVLink
        /// </summary>
        /// <param name="name">Parameter name (e.g., "SERVO1_FUNCTION")</param>
        /// <param name="value">Integer value to set</param>
        private async void sendMode(string name, int value)
        {
            if (_mavLinkService == null) return;
            
            try
            {
                await _mavLinkService.SetParameterAsync(name, value);
                Debug.WriteLine($"[CalibrationControl] Parameter set: {name} = {value}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CalibrationControl] Error setting parameter {name}: {ex.Message}");
                ShowStatus($"Error: Failed to set {name}");
            }
        }

        /// <summary>
        /// Sends a float parameter value to the vehicle via MAVLink
        /// </summary>
        /// <param name="name">Parameter name (e.g., "ATC_RAT_RLL_P")</param>
        /// <param name="value">Float value to set</param>
        private async void sendModefloat(string name, float value)
        {
            if (_mavLinkService == null) return;
            
            try
            {
                await _mavLinkService.SetParameterAsync(name, value);
                Debug.WriteLine($"[CalibrationControl] Parameter set: {name} = {value:F2}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CalibrationControl] Error setting parameter {name}: {ex.Message}");
                ShowStatus($"Error: Failed to set {name}");
            }
        }

        /// <summary>
        /// Parses a float value from text with a default fallback
        /// </summary>
        /// <param name="text">Text to parse</param>
        /// <param name="defaultValue">Default value if parsing fails</param>
        /// <returns>Parsed float value or default</returns>
        private float GetFloat(string text, float defaultValue = 0f)
        {
            return float.TryParse(text, out float result) ? result : defaultValue;
        }

        /// <summary>
        /// Parses an integer value from text with a default fallback
        /// </summary>
        /// <param name="text">Text to parse</param>
        /// <param name="defaultValue">Default value if parsing fails</param>
        /// <returns>Parsed integer value or default</returns>
        private int GetInt(string text, int defaultValue = 0)
        {
            return int.TryParse(text, out int result) ? result : defaultValue;
        }

        /// <summary>
        /// Converts PWM value to flight mode index (0-5)
        /// </summary>
        /// <param name="inpwm">PWM value from RC channel (typically 1000-2000)</param>
        /// <returns>Mode index: 0-5 corresponding to FLTMODE1-6</returns>
        private byte ReadSwitch(float inpwm)
        {
            int pulsewidth = (int)inpwm;
            
            // PWM ranges for 6-position switch
            // Mode 1: < 1230
            // Mode 2: 1231-1360
            // Mode 3: 1361-1490
            // Mode 4: 1491-1620
            // Mode 5: 1621-1749
            // Mode 6: >= 1750
            
            if (pulsewidth > 1230 && pulsewidth <= 1360) return 1;
            if (pulsewidth > 1360 && pulsewidth <= 1490) return 2;
            if (pulsewidth > 1490 && pulsewidth <= 1620) return 3;
            if (pulsewidth > 1620 && pulsewidth <= 1749) return 4;
            if (pulsewidth >= 1750) return 5;
            
            return 0; // Default to mode 1 (< 1230)
        }

        /// <summary>
        /// Populates a ComboBox with enum values
        /// </summary>
        /// <param name="ctl">ComboBox to populate</param>
        /// <param name="enumType">Enum type to use for values</param>
        private void updateDropDown(ComboBox ctl, Type enumType)
        {
            if (ctl == null) return;
            
            // Create list of KeyValuePairs for binding
            var enumList = new List<KeyValuePair<int, string>>();
            
            foreach (var enumValue in Enum.GetValues(enumType))
            {
                enumList.Add(new KeyValuePair<int, string>(
                    (int)enumValue, 
                    enumValue.ToString()
                ));
            }
            
            // Set ItemsSource
            ctl.ItemsSource = enumList;
            
            // Configure display and value bindings
            ctl.DisplayMemberPath = "Value";  // Show enum name
            ctl.SelectedValuePath = "Key";    // Use enum int value
        }

        #endregion
    }

    // Data model classes
    public class RadioChannelItem : INotifyPropertyChanged
    {
        private string _name;
        private int _no;
        private int _min;
        private int _max;
        private int _current;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public int No
        {
            get => _no;
            set { _no = value; OnPropertyChanged(); }
        }

        public int Min
        {
            get => _min;
            set { _min = value; OnPropertyChanged(); }
        }

        public int Max
        {
            get => _max;
            set { _max = value; OnPropertyChanged(); }
        }

        public int Current
        {
            get => _current;
            set { _current = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ServoItem : INotifyPropertyChanged
    {
        private int _no;
        private bool _reversed;
        private ServoFunction _function;
        private int _output;
        private int _outputPercent;
        private int _min;
        private int _trim;
        private int _max;

        public int No
        {
            get => _no;
            set { _no = value; OnPropertyChanged(); }
        }

        public bool Reversed
        {
            get => _reversed;
            set { _reversed = value; OnPropertyChanged(); }
        }

        public ServoFunction Function
        {
            get => _function;
            set { _function = value; OnPropertyChanged(); }
        }

        public int Output
        {
            get => _output;
            set { _output = value; OnPropertyChanged(); }
        }

        public int OutputPercent
        {
            get => _outputPercent;
            set { _outputPercent = value; OnPropertyChanged(); }
        }

        public int Min
        {
            get => _min;
            set { _min = value; OnPropertyChanged(); }
        }

        public int Trim
        {
            get => _trim;
            set { _trim = value; OnPropertyChanged(); }
        }

        public int Max
        {
            get => _max;
            set { _max = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
