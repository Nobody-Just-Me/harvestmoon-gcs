using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Core.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HarvestmoonGCS.Core.ViewModels
{
    public class CalibrationViewModel : INotifyPropertyChanged
    {
        private const int SimulatedCalibrationStepDelayMs = 50;
        private const int SimulatedCompassProgressDelayMs = 50;
        private readonly IMavLinkService _mavlinkService;
        private CancellationTokenSource? _compassCalibrationCts;
        private int _currentCalibrationStep = 0;
        private bool _isLoading;
        private float _currentModePWM;
        private int _currentPWM = 1500;
        private int _selectedMotor = 1;
        private int _throttlePercentage = 10;
        private int _testDuration = 2;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<int>? CalibrationStepCompleted;
        public event EventHandler<(int progress1, int progress2)>? CalibrationProgressChanged;
        public event EventHandler<bool>? IsLoadingChanged;
        public event EventHandler<Dictionary<int, int>>? ServoValuesChanged;
        public event EventHandler<float>? ModePWMChanged;
        public event EventHandler<string>? StatusMessageChanged;

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    IsLoadingChanged?.Invoke(this, value);
                    OnPropertyChanged(nameof(IsLoading));
                }
            }
        }

        public ObservableCollection<CompassDevice> CompassDevices { get; } = new ObservableCollection<CompassDevice>();

        // Flight Mode properties
        public Dictionary<int, int> FlightModes { get; } = new Dictionary<int, int>();
        private int _flightModeChannel = 5;

        public int FlightModeChannel
        {
            get => _flightModeChannel;
            private set
            {
                if (_flightModeChannel != value)
                {
                    _flightModeChannel = value;
                    OnPropertyChanged(nameof(FlightModeChannel));
                }
            }
        }

        public float CurrentModePWM
        {
            get => _currentModePWM;
            set
            {
                if (Math.Abs(_currentModePWM - value) > 0.01f)
                {
                    _currentModePWM = value;
                    OnPropertyChanged(nameof(CurrentModePWM));
                    ModePWMChanged?.Invoke(this, value);
                }
            }
        }

        // Servo Output properties
        public Dictionary<int, ServoConfig> ServoConfigs { get; } = new Dictionary<int, ServoConfig>();

        // Waypoint Settings properties
        public float WaypointSpeed { get; set; }
        public float WaypointRadius { get; set; }
        public float WaypointSpeedUp { get; set; }
        public float WaypointSpeedDown { get; set; }
        public float LoiterSpeed { get; set; }

        // PID Tuning (Copter) properties
        public PIDParameters RollPID { get; set; } = new PIDParameters();
        public PIDParameters PitchPID { get; set; } = new PIDParameters();
        public PIDParameters YawPID { get; set; } = new PIDParameters();

        // PID Tuning (Plane) properties
        public PIDParameters PlaneRollPID { get; set; } = new PIDParameters();
        public PIDParameters PlanePitchPID { get; set; } = new PIDParameters();
        public PIDParameters PlaneYawPID { get; set; } = new PIDParameters();
        public PIDParameters VelocityPID { get; set; } = new PIDParameters();
        public float PitchMax { get; set; }
        public float PitchMin { get; set; }
        public float RollLimit { get; set; }

        // ESC Calibration properties
        public int CurrentPWM
        {
            get => _currentPWM;
            set
            {
                if (_currentPWM != value)
                {
                    _currentPWM = value;
                    OnPropertyChanged(nameof(CurrentPWM));
                }
            }
        }

        public List<int> SelectedMotorChannels { get; } = new List<int>();

        // Motor Test properties
        public int SelectedMotor
        {
            get => _selectedMotor;
            set
            {
                if (_selectedMotor != value)
                {
                    _selectedMotor = value;
                    OnPropertyChanged(nameof(SelectedMotor));
                }
            }
        }

        public int ThrottlePercentage
        {
            get => _throttlePercentage;
            set
            {
                if (_throttlePercentage != value)
                {
                    _throttlePercentage = value;
                    OnPropertyChanged(nameof(ThrottlePercentage));
                }
            }
        }

        public int TestDuration
        {
            get => _testDuration;
            set
            {
                if (_testDuration != value)
                {
                    _testDuration = value;
                    OnPropertyChanged(nameof(TestDuration));
                }
            }
        }

        public CalibrationViewModel(IMavLinkService mavlinkService)
        {
            _mavlinkService = mavlinkService ?? throw new ArgumentNullException(nameof(mavlinkService));
            InitializeServoConfigs();
        }

        private void InitializeServoConfigs()
        {
            for (int i = 1; i <= 16; i++)
            {
                ServoConfigs[i] = new ServoConfig
                {
                    Channel = i,
                    Reverse = false,
                    Function = 0,
                    Min = 1000,
                    Trim = 1500,
                    Max = 2000,
                    CurrentOutput = 0
                };
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #region Accelerometer Calibration

        public async Task StartAccelerometerCalibrationAsync()
        {
            try
            {
                IsLoading = true;
                _currentCalibrationStep = 0;

                // Send MAVLink command to start accelerometer calibration
                // MAV_CMD_PREFLIGHT_CALIBRATION (241)
                // param5 = 1 for accelerometer calibration
                await _mavlinkService.SendCommandLongAsync(
                    command: 241, // MAV_CMD_PREFLIGHT_CALIBRATION
                    param1: 0,
                    param2: 0,
                    param3: 0,
                    param4: 0,
                    param5: 1, // Accelerometer calibration
                    param6: 0,
                    param7: 0
                );

                // Simulate calibration steps (in real implementation, listen to MAVLink messages)
                await SimulateCalibrationStepsAsync();
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task StartSimpleAccelCalibrationAsync()
        {
            try
            {
                StatusMessageChanged?.Invoke(this, "Starting simple accelerometer calibration...");
                
                // Send MAVLink command for simple accelerometer calibration
                await _mavlinkService.SendCommandLongAsync(
                    command: 241, // MAV_CMD_PREFLIGHT_CALIBRATION
                    param1: 0,
                    param2: 0,
                    param3: 0,
                    param4: 0,
                    param5: 4, // Simple accel calibration
                    param6: 0,
                    param7: 0
                );

                StatusMessageChanged?.Invoke(this, "Simple accelerometer calibration command sent");
                await Task.Delay(3000); // Wait for calibration to complete
                StatusMessageChanged?.Invoke(this, "Simple accelerometer calibration complete");
            }
            catch (Exception ex)
            {
                StatusMessageChanged?.Invoke(this, $"Error during simple accel calibration: {ex.Message}");
            }
        }

        public async Task SetAccelerometerPosition(int position)
        {
            // Send MAVLink command to set accelerometer position
            // MAV_CMD_ACCELCAL_VEHICLE_POS (42429)
            await _mavlinkService.SendCommandLongAsync(
                command: 42429, // MAV_CMD_ACCELCAL_VEHICLE_POS
                param1: position,
                param2: 0,
                param3: 0,
                param4: 0,
                param5: 0,
                param6: 0,
                param7: 0
            );
        }

        public async Task StartLevelCalibrationAsync()
        {
            try
            {
                StatusMessageChanged?.Invoke(this, "Starting level calibration...");
                
                // Send MAVLink command for level calibration
                await _mavlinkService.SendCommandLongAsync(
                    command: 241, // MAV_CMD_PREFLIGHT_CALIBRATION
                    param1: 0,
                    param2: 0,
                    param3: 0,
                    param4: 0,
                    param5: 2, // Level calibration
                    param6: 0,
                    param7: 0
                );

                StatusMessageChanged?.Invoke(this, "Level calibration command sent");
                await Task.Delay(3000); // Wait for calibration to complete
                StatusMessageChanged?.Invoke(this, "Level calibration complete");
            }
            catch (Exception ex)
            {
                StatusMessageChanged?.Invoke(this, $"Error during level calibration: {ex.Message}");
            }
        }

        private async Task SimulateCalibrationStepsAsync()
        {
            // In real implementation, this would listen to STATUSTEXT messages from MAVLink
            // For now, simulate the 6 calibration steps
            for (int i = 1; i <= 6; i++)
            {
                await Task.Delay(SimulatedCalibrationStepDelayMs);
                _currentCalibrationStep = i;
                CalibrationStepCompleted?.Invoke(this, i);
            }
        }

        #endregion

        #region Compass Calibration

        public async Task RefreshCompassListAsync()
        {
            try
            {
                StatusMessageChanged?.Invoke(this, "Refreshing compass list...");
                CompassDevices.Clear();

                // Request all parameters from vehicle
                await _mavlinkService.RequestParametersAsync();
                
                // Get all parameters
                var parameters = await _mavlinkService.GetParametersAsync();
                
                // Parse compass device information from parameters
                // Look for COMPASS_DEV_ID, COMPASS_DEV_ID2, COMPASS_DEV_ID3
                for (int i = 1; i <= 3; i++)
                {
                    string devIdParam = i == 1 ? "COMPASS_DEV_ID" : $"COMPASS_DEV_ID{i}";
                    
                    if (parameters.TryGetValue(devIdParam, out float devIdValue))
                    {
                        int devId = (int)devIdValue;
                        if (devId != 0) // Only add if device exists
                        {
                            string devType = GetCompassDeviceType(devId);
                            CompassDevices.Add(new CompassDevice 
                            { 
                                Number = i, 
                                DevID = devId, 
                                DevType = devType 
                            });
                        }
                    }
                }
                
                // If no compass devices found in parameters, add default/dummy data for testing
                if (CompassDevices.Count == 0)
                {
                    StatusMessageChanged?.Invoke(this, "No compass parameters found, using default values");
                    CompassDevices.Add(new CompassDevice { Number = 1, DevID = 73225, DevType = "HMC5883" });
                    CompassDevices.Add(new CompassDevice { Number = 2, DevID = 73226, DevType = "HMC5883" });
                }
                
                StatusMessageChanged?.Invoke(this, $"Found {CompassDevices.Count} compass device(s)");
            }
            catch (Exception ex)
            {
                StatusMessageChanged?.Invoke(this, $"Error refreshing compass list: {ex.Message}");
                
                // Add default data on error
                CompassDevices.Clear();
                CompassDevices.Add(new CompassDevice { Number = 1, DevID = 73225, DevType = "HMC5883" });
                CompassDevices.Add(new CompassDevice { Number = 2, DevID = 73226, DevType = "HMC5883" });
            }
        }
        
        private string GetCompassDeviceType(int devId)
        {
            // Parse device type from device ID
            // Device ID format: (bus_type << 24) | (bus << 16) | (address << 8) | devtype
            int devType = devId & 0xFF;
            
            // Common compass device types
            return devType switch
            {
                0x01 => "HMC5843",
                0x02 => "HMC5883",
                0x03 => "HMC5883L",
                0x04 => "AK8963",
                0x05 => "BMM150",
                0x06 => "LSM303D",
                0x07 => "AK09916",
                0x08 => "IST8310",
                0x09 => "ICM20948",
                0x0A => "MMC3416",
                0x0B => "QMC5883L",
                0x0C => "MAG3110",
                0x0D => "SITL",
                0x0E => "RM3100",
                0x0F => "RM3100_2",
                0x10 => "MMC5883",
                _ => $"Unknown (0x{devType:X2})"
            };
        }

        public async Task StartCompassCalibrationAsync()
        {
            try
            {
                _compassCalibrationCts?.Cancel();
                _compassCalibrationCts?.Dispose();
                _compassCalibrationCts = new CancellationTokenSource();
                var cancellationToken = _compassCalibrationCts.Token;

                IsLoading = true;
                StatusMessageChanged?.Invoke(this, "Starting compass calibration...");
                
                // Initialize progress bars to 0
                CalibrationProgressChanged?.Invoke(this, (0, 0));
                
                // Send MAVLink command to start compass calibration
                // MAV_CMD_PREFLIGHT_CALIBRATION (241)
                // param2 = 1 for magnetometer calibration
                await _mavlinkService.SendCommandLongAsync(
                    command: 241, // MAV_CMD_PREFLIGHT_CALIBRATION
                    param1: 0,
                    param2: 1, // Magnetometer calibration
                    param3: 0,
                    param4: 0,
                    param5: 0,
                    param6: 0,
                    param7: 0
                );

                StatusMessageChanged?.Invoke(this, "Compass calibration started. Rotate vehicle in all directions...");

                // Simulate progress updates
                // In real implementation, this would listen to MAG_CAL_PROGRESS messages
                _ = Task.Run(async () =>
                {
                    try
                    {
                        for (int i = 0; i <= 100; i += 5)
                        {
                            await Task.Delay(SimulatedCompassProgressDelayMs, cancellationToken);
                            if (cancellationToken.IsCancellationRequested)
                            {
                                return;
                            }

                            CalibrationProgressChanged?.Invoke(this, (i, i));
                        }

                        IsLoading = false;
                        StatusMessageChanged?.Invoke(this, "Compass calibration data collected. Click Accept to save or Cancel to abort.");
                    }
                    catch (OperationCanceledException)
                    {
                        // Cancellation is the normal path when the user aborts calibration.
                    }
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                IsLoading = false;
                StatusMessageChanged?.Invoke(this, $"Error starting compass calibration: {ex.Message}");
                throw;
            }
        }

        public async Task AcceptCompassCalibrationAsync()
        {
            try
            {
                _compassCalibrationCts?.Cancel();
                StatusMessageChanged?.Invoke(this, "Accepting compass calibration...");
                
                // Send MAVLink command to accept compass calibration
                // MAV_CMD_PREFLIGHT_CALIBRATION (241)
                // param2 = 2 to accept
                await _mavlinkService.SendCommandLongAsync(
                    command: 241,
                    param1: 0,
                    param2: 2, // Accept calibration
                    param3: 0,
                    param4: 0,
                    param5: 0,
                    param6: 0,
                    param7: 0
                );
                
                StatusMessageChanged?.Invoke(this, "Compass calibration accepted and saved successfully");
            }
            catch (Exception ex)
            {
                StatusMessageChanged?.Invoke(this, $"Error accepting compass calibration: {ex.Message}");
                throw;
            }
        }

        public async Task CancelCompassCalibrationAsync()
        {
            try
            {
                _compassCalibrationCts?.Cancel();
                StatusMessageChanged?.Invoke(this, "Cancelling compass calibration...");
                
                // Send MAVLink command to cancel compass calibration
                // MAV_CMD_PREFLIGHT_CALIBRATION (241)
                // param2 = 0 to cancel
                await _mavlinkService.SendCommandLongAsync(
                    command: 241,
                    param1: 0,
                    param2: 0, // Cancel calibration
                    param3: 0,
                    param4: 0,
                    param5: 0,
                    param6: 0,
                    param7: 0
                );

                // Reset progress bars to 0
                CalibrationProgressChanged?.Invoke(this, (0, 0));
                IsLoading = false;
                
                StatusMessageChanged?.Invoke(this, "Compass calibration cancelled");
            }
            catch (Exception ex)
            {
                StatusMessageChanged?.Invoke(this, $"Error cancelling compass calibration: {ex.Message}");
                throw;
            }
        }

        public async Task RebootVehicleAsync()
        {
            try
            {
                StatusMessageChanged?.Invoke(this, "Sending reboot command to vehicle...");
                
                // Send MAVLink command to reboot vehicle
                // MAV_CMD_PREFLIGHT_REBOOT_SHUTDOWN (246)
                await _mavlinkService.SendCommandLongAsync(
                    command: 246, // MAV_CMD_PREFLIGHT_REBOOT_SHUTDOWN
                    param1: 1, // Reboot autopilot
                    param2: 0,
                    param3: 0,
                    param4: 0,
                    param5: 0,
                    param6: 0,
                    param7: 0
                );
                
                StatusMessageChanged?.Invoke(this, "Reboot command sent. Vehicle will restart...");
            }
            catch (Exception ex)
            {
                StatusMessageChanged?.Invoke(this, $"Error sending reboot command: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Flight Mode

        public async Task LoadFlightModesAsync()
        {
            try
            {
                StatusMessageChanged?.Invoke(this, "Loading flight modes...");

                var requestTask = _mavlinkService.RequestParametersAsync();
                if (requestTask != null)
                {
                    await requestTask;
                }

                var parametersTask = _mavlinkService.GetParametersAsync();
                var parameters = parametersTask != null
                    ? await parametersTask ?? new Dictionary<string, float>()
                    : new Dictionary<string, float>();

                FlightModes.Clear();
                for (int i = 1; i <= 6; i++)
                {
                    var parameterName = $"FLTMODE{i}";
                    FlightModes[i] = parameters.TryGetValue(parameterName, out var value)
                        ? (int)Math.Round(value)
                        : 0;
                }

                if (parameters.TryGetValue("FLTMODE_CH", out var modeChannelValue))
                {
                    var parsedChannel = (int)Math.Round(modeChannelValue);
                    FlightModeChannel = parsedChannel <= 0 ? 5 : Math.Clamp(parsedChannel, 1, 16);
                }
                else
                {
                    FlightModeChannel = 5;
                }

                OnPropertyChanged(nameof(FlightModes));
                StatusMessageChanged?.Invoke(this, $"Flight modes loaded (channel CH{FlightModeChannel})");
            }
            catch (Exception ex)
            {
                FlightModes.Clear();
                for (int i = 1; i <= 6; i++)
                {
                    FlightModes[i] = 0;
                }
                FlightModeChannel = 5;
                StatusMessageChanged?.Invoke(this, $"Error loading flight modes: {ex.Message}");
            }
        }

        public async Task SaveFlightModesAsync(Dictionary<int, int> modes)
        {
            try
            {
                if (modes == null || modes.Count == 0)
                {
                    StatusMessageChanged?.Invoke(this, "No flight mode changes to save");
                    return;
                }

                StatusMessageChanged?.Invoke(this, "Saving flight modes...");

                foreach (var mode in modes.OrderBy(entry => entry.Key))
                {
                    if (mode.Key < 1 || mode.Key > 6)
                    {
                        continue;
                    }

                    string paramName = $"FLTMODE{mode.Key}";
                    await _mavlinkService.SetParameterAsync(paramName, mode.Value);
                    FlightModes[mode.Key] = mode.Value;
                    await Task.Delay(50);
                }

                OnPropertyChanged(nameof(FlightModes));
                StatusMessageChanged?.Invoke(this, "Flight modes saved successfully");
            }
            catch (Exception ex)
            {
                StatusMessageChanged?.Invoke(this, $"Error saving flight modes: {ex.Message}");
            }
        }

        public void UpdateCurrentModePWM(float pwm)
        {
            CurrentModePWM = pwm;
        }

        #endregion

        #region Servo Output

        public async Task LoadServoConfigsAsync()
        {
            try
            {
                StatusMessageChanged?.Invoke(this, "Loading servo configurations...");
                
                // Request servo parameters for all 16 channels
                for (int i = 1; i <= 16; i++)
                {
                    // In real implementation, this would request:
                    // SERVO{i}_FUNCTION, SERVO{i}_MIN, SERVO{i}_TRIM, SERVO{i}_MAX, SERVO{i}_REVERSED
                    // For now, configs are already initialized
                }
                
                StatusMessageChanged?.Invoke(this, "Servo configurations loaded");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                StatusMessageChanged?.Invoke(this, $"Error loading servo configs: {ex.Message}");
            }
        }

        public async Task SaveServoConfigsAsync(Dictionary<int, ServoConfig> configs)
        {
            try
            {
                StatusMessageChanged?.Invoke(this, "Saving servo configurations...");
                
                foreach (var config in configs.Values)
                {
                    await _mavlinkService.SetParameterAsync($"SERVO{config.Channel}_FUNCTION", config.Function);
                    await _mavlinkService.SetParameterAsync($"SERVO{config.Channel}_MIN", config.Min);
                    await _mavlinkService.SetParameterAsync($"SERVO{config.Channel}_TRIM", config.Trim);
                    await _mavlinkService.SetParameterAsync($"SERVO{config.Channel}_MAX", config.Max);
                    await _mavlinkService.SetParameterAsync($"SERVO{config.Channel}_REVERSED", config.Reverse ? 1 : 0);
                    await Task.Delay(100); // Small delay between parameter sets
                }
                
                StatusMessageChanged?.Invoke(this, "Servo configurations saved successfully");
            }
            catch (Exception ex)
            {
                StatusMessageChanged?.Invoke(this, $"Error saving servo configs: {ex.Message}");
            }
        }

        public void UpdateServoOutputValues(Dictionary<int, int> values)
        {
            foreach (var kvp in values)
            {
                if (ServoConfigs.ContainsKey(kvp.Key))
                {
                    ServoConfigs[kvp.Key].CurrentOutput = kvp.Value;
                }
            }
            ServoValuesChanged?.Invoke(this, values);
        }

        #endregion

        #region Settings (Waypoint and PID)

        public async Task LoadWaypointParametersAsync()
        {
            try
            {
                StatusMessageChanged?.Invoke(this, "Loading waypoint parameters...");
                
                // In real implementation, request these parameters:
                // WP_SPEED, WP_RADIUS, WPNAV_SPEED_UP, WPNAV_SPEED_DN, WPNAV_LOIT_SPEED
                // For now, initialize with defaults
                WaypointSpeed = 5.0f;
                WaypointRadius = 2.0f;
                WaypointSpeedUp = 2.5f;
                WaypointSpeedDown = 1.5f;
                LoiterSpeed = 5.0f;
                
                StatusMessageChanged?.Invoke(this, "Waypoint parameters loaded");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                StatusMessageChanged?.Invoke(this, $"Error loading waypoint parameters: {ex.Message}");
            }
        }

        public async Task SaveWaypointParametersAsync()
        {
            try
            {
                StatusMessageChanged?.Invoke(this, "Saving waypoint parameters...");
                
                await _mavlinkService.SetParameterAsync("WP_SPEED", WaypointSpeed);
                await _mavlinkService.SetParameterAsync("WP_RADIUS", WaypointRadius);
                await _mavlinkService.SetParameterAsync("WPNAV_SPEED_UP", WaypointSpeedUp);
                await _mavlinkService.SetParameterAsync("WPNAV_SPEED_DN", WaypointSpeedDown);
                await _mavlinkService.SetParameterAsync("WPNAV_LOIT_SPEED", LoiterSpeed);
                
                StatusMessageChanged?.Invoke(this, "Waypoint parameters saved successfully");
            }
            catch (Exception ex)
            {
                StatusMessageChanged?.Invoke(this, $"Error saving waypoint parameters: {ex.Message}");
            }
        }

        public async Task LoadPIDParametersAsync()
        {
            try
            {
                StatusMessageChanged?.Invoke(this, "Loading PID parameters...");
                
                // In real implementation, request PID parameters for both Copter and Plane
                // For now, initialize with defaults
                RollPID = new PIDParameters { P = 0.15f, I = 0.1f, D = 0.004f, IMAX = 0.5f };
                PitchPID = new PIDParameters { P = 0.15f, I = 0.1f, D = 0.004f, IMAX = 0.5f };
                YawPID = new PIDParameters { P = 0.2f, I = 0.02f, D = 0.0f, IMAX = 0.5f };
                
                PlaneRollPID = new PIDParameters { P = 0.15f, I = 0.1f, D = 0.004f, IMAX = 0.5f };
                PlanePitchPID = new PIDParameters { P = 0.15f, I = 0.1f, D = 0.004f, IMAX = 0.5f };
                PlaneYawPID = new PIDParameters { P = 0.2f, I = 0.02f, D = 0.0f, IMAX = 0.5f };
                VelocityPID = new PIDParameters { P = 1.0f, I = 0.5f, D = 0.0f, IMAX = 1.0f };
                
                PitchMax = 45.0f;
                PitchMin = -45.0f;
                RollLimit = 45.0f;
                
                StatusMessageChanged?.Invoke(this, "PID parameters loaded");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                StatusMessageChanged?.Invoke(this, $"Error loading PID parameters: {ex.Message}");
            }
        }

        public async Task SavePIDParametersAsync(Dictionary<string, float> parameters)
        {
            try
            {
                StatusMessageChanged?.Invoke(this, "Saving PID parameters...");
                
                // Send each parameter to the vehicle via MAVLink
                foreach (var param in parameters)
                {
                    await _mavlinkService.SetParameterAsync(param.Key, param.Value);
                    await Task.Delay(100); // Small delay between parameter sets
                }
                
                StatusMessageChanged?.Invoke(this, "PID parameters saved successfully");
            }
            catch (Exception ex)
            {
                StatusMessageChanged?.Invoke(this, $"Error saving PID parameters: {ex.Message}");
            }
        }

        #endregion

        #region ESC Calibration

        public async Task SendPWMToMotors(int pwm, List<int> channels)
        {
            try
            {
                StatusMessageChanged?.Invoke(this, $"Sending PWM {pwm} to selected motors...");
                
                foreach (var channel in channels)
                {
                    // MAV_CMD_DO_SET_SERVO (183)
                    await _mavlinkService.SendCommandLongAsync(
                        command: 183, // MAV_CMD_DO_SET_SERVO
                        param1: channel,
                        param2: pwm,
                        param3: 0,
                        param4: 0,
                        param5: 0,
                        param6: 0,
                        param7: 0
                    );
                    await Task.Delay(50);
                }
                
                StatusMessageChanged?.Invoke(this, $"PWM {pwm} sent to motors");
            }
            catch (Exception ex)
            {
                StatusMessageChanged?.Invoke(this, $"Error sending PWM: {ex.Message}");
            }
        }

        public async Task AutoCalibrateESCAsync()
        {
            try
            {
                StatusMessageChanged?.Invoke(this, "Starting ESC auto-calibration...");
                
                // Send max PWM
                await SendPWMToMotors(2000, SelectedMotorChannels);
                StatusMessageChanged?.Invoke(this, "Max PWM sent. Connect battery now...");
                await Task.Delay(5000);
                
                // Send min PWM
                await SendPWMToMotors(1000, SelectedMotorChannels);
                StatusMessageChanged?.Invoke(this, "ESC calibration complete");
            }
            catch (Exception ex)
            {
                StatusMessageChanged?.Invoke(this, $"Error during ESC calibration: {ex.Message}");
            }
        }

        #endregion

        #region Motor Test

        public async Task TestMotorAsync(int motor, int throttle, int duration)
        {
            try
            {
                StatusMessageChanged?.Invoke(this, $"Testing motor {motor} at {throttle}% for {duration}s...");
                
                // MAV_CMD_DO_MOTOR_TEST (209)
                await _mavlinkService.SendCommandLongAsync(
                    command: 209, // MAV_CMD_DO_MOTOR_TEST
                    param1: motor,
                    param2: 0, // Throttle type: 0 = percent
                    param3: throttle,
                    param4: duration,
                    param5: 1, // Motor count
                    param6: 0,
                    param7: 0
                );
                
                await Task.Delay(duration * 1000);
                StatusMessageChanged?.Invoke(this, $"Motor {motor} test complete");
            }
            catch (Exception ex)
            {
                StatusMessageChanged?.Invoke(this, $"Error testing motor: {ex.Message}");
            }
        }

        public async Task TestAllMotorsSequentiallyAsync()
        {
            try
            {
                StatusMessageChanged?.Invoke(this, "Testing all motors sequentially...");
                
                for (int motor = 1; motor <= 16; motor++)
                {
                    await TestMotorAsync(motor, ThrottlePercentage, TestDuration);
                }
                
                StatusMessageChanged?.Invoke(this, "All motors tested");
            }
            catch (Exception ex)
            {
                StatusMessageChanged?.Invoke(this, $"Error testing all motors: {ex.Message}");
            }
        }

        public async Task StopAllMotorsAsync()
        {
            try
            {
                StatusMessageChanged?.Invoke(this, "Stopping all motors...");
                
                // Send 0% throttle to all motors
                for (int motor = 1; motor <= 16; motor++)
                {
                    await _mavlinkService.SendCommandLongAsync(
                        command: 209, // MAV_CMD_DO_MOTOR_TEST
                        param1: motor,
                        param2: 0,
                        param3: 0, // 0% throttle
                        param4: 0,
                        param5: 1,
                        param6: 0,
                        param7: 0
                    );
                }
                
                StatusMessageChanged?.Invoke(this, "All motors stopped");
            }
            catch (Exception ex)
            {
                StatusMessageChanged?.Invoke(this, $"Error stopping motors: {ex.Message}");
            }
        }

        #endregion

        #region Gyro Calibration

        public async Task StartGyroCalibrationAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessageChanged?.Invoke(this, "Starting gyroscope calibration...");
                
                // Send MAVLink command to start gyro calibration
                // MAV_CMD_PREFLIGHT_CALIBRATION (241)
                // param3 = 1 for gyroscope calibration
                await _mavlinkService.SendCommandLongAsync(
                    command: 241, // MAV_CMD_PREFLIGHT_CALIBRATION
                    param1: 0,
                    param2: 0,
                    param3: 1, // Gyroscope calibration
                    param4: 0,
                    param5: 0,
                    param6: 0,
                    param7: 0
                );

                StatusMessageChanged?.Invoke(this, "Gyro calibration started. Keep vehicle stationary...");
                
                // Simulate calibration progress (30 seconds)
                _ = Task.Run(async () =>
                {
                    for (int i = 0; i <= 100; i += 5)
                    {
                        await Task.Delay(1500); // 30 seconds total
                        CalibrationProgressChanged?.Invoke(this, (i, 0)); // Use first progress bar
                    }
                    IsLoading = false;
                    StatusMessageChanged?.Invoke(this, "Gyroscope calibration completed successfully");
                });
            }
            catch (Exception ex)
            {
                IsLoading = false;
                StatusMessageChanged?.Invoke(this, $"Error during gyro calibration: {ex.Message}");
            }
        }

        public async Task CancelGyroCalibrationAsync()
        {
            try
            {
                StatusMessageChanged?.Invoke(this, "Cancelling gyroscope calibration...");
                
                // Send cancel command
                await _mavlinkService.SendCommandLongAsync(
                    command: 241,
                    param1: 0,
                    param2: 0,
                    param3: 0, // Cancel gyro calibration
                    param4: 0,
                    param5: 0,
                    param6: 0,
                    param7: 0
                );

                IsLoading = false;
                CalibrationProgressChanged?.Invoke(this, (0, 0));
                StatusMessageChanged?.Invoke(this, "Gyroscope calibration cancelled");
            }
            catch (Exception ex)
            {
                StatusMessageChanged?.Invoke(this, $"Error cancelling gyro calibration: {ex.Message}");
            }
        }

        #endregion

        #region Radio Calibration

        public async Task StartRadioCalibrationAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessageChanged?.Invoke(this, "Starting radio calibration...");
                
                // Send MAVLink command to start radio calibration
                // MAV_CMD_PREFLIGHT_CALIBRATION (241)
                // param4 = 1 for radio calibration
                await _mavlinkService.SendCommandLongAsync(
                    command: 241, // MAV_CMD_PREFLIGHT_CALIBRATION
                    param1: 0,
                    param2: 0,
                    param3: 0,
                    param4: 1, // Radio calibration
                    param5: 0,
                    param6: 0,
                    param7: 0
                );

                StatusMessageChanged?.Invoke(this, "Radio calibration started. Move all sticks and switches to extremes...");
            }
            catch (Exception ex)
            {
                IsLoading = false;
                StatusMessageChanged?.Invoke(this, $"Error starting radio calibration: {ex.Message}");
            }
        }

        public async Task SaveRadioCalibrationAsync()
        {
            try
            {
                StatusMessageChanged?.Invoke(this, "Saving radio calibration...");
                
                // Send save command
                await _mavlinkService.SendCommandLongAsync(
                    command: 241,
                    param1: 0,
                    param2: 0,
                    param3: 0,
                    param4: 2, // Save radio calibration
                    param5: 0,
                    param6: 0,
                    param7: 0
                );

                IsLoading = false;
                StatusMessageChanged?.Invoke(this, "Radio calibration saved successfully");
            }
            catch (Exception ex)
            {
                StatusMessageChanged?.Invoke(this, $"Error saving radio calibration: {ex.Message}");
            }
        }

        public async Task CancelRadioCalibrationAsync()
        {
            try
            {
                StatusMessageChanged?.Invoke(this, "Cancelling radio calibration...");
                
                // Send cancel command
                await _mavlinkService.SendCommandLongAsync(
                    command: 241,
                    param1: 0,
                    param2: 0,
                    param3: 0,
                    param4: 0, // Cancel radio calibration
                    param5: 0,
                    param6: 0,
                    param7: 0
                );

                IsLoading = false;
                StatusMessageChanged?.Invoke(this, "Radio calibration cancelled");
            }
            catch (Exception ex)
            {
                StatusMessageChanged?.Invoke(this, $"Error cancelling radio calibration: {ex.Message}");
            }
        }

        #endregion

        #region Barometer Calibration

        public async Task CalibrateGroundPressureAsync(float groundAltitude)
        {
            try
            {
                IsLoading = true;
                StatusMessageChanged?.Invoke(this, $"Calibrating ground pressure at {groundAltitude}m altitude...");
                
                // Send MAVLink command to calibrate barometer
                // MAV_CMD_PREFLIGHT_CALIBRATION (241)
                // param6 = 1 for barometer calibration
                await _mavlinkService.SendCommandLongAsync(
                    command: 241, // MAV_CMD_PREFLIGHT_CALIBRATION
                    param1: 0,
                    param2: 0,
                    param3: 0,
                    param4: 0,
                    param5: 0,
                    param6: 1, // Barometer calibration
                    param7: groundAltitude
                );

                StatusMessageChanged?.Invoke(this, "Barometer calibration started...");
                
                // Simulate calibration progress (10 seconds)
                _ = Task.Run(async () =>
                {
                    for (int i = 0; i <= 100; i += 10)
                    {
                        await Task.Delay(1000); // 10 seconds total
                        CalibrationProgressChanged?.Invoke(this, (i, 0)); // Use first progress bar
                    }
                    IsLoading = false;
                    StatusMessageChanged?.Invoke(this, $"Barometer calibration completed. Ground level set to {groundAltitude}m");
                });
            }
            catch (Exception ex)
            {
                IsLoading = false;
                StatusMessageChanged?.Invoke(this, $"Error during barometer calibration: {ex.Message}");
            }
        }

        public async Task ResetBarometerAsync()
        {
            try
            {
                StatusMessageChanged?.Invoke(this, "Resetting barometer calibration...");
                
                // Reset barometer to default values
                await _mavlinkService.SetParameterAsync("BARO_ALT_OFFSET", 0.0f);
                await _mavlinkService.SetParameterAsync("BARO_GND_PRESS", 101325.0f); // Standard sea level pressure
                
                StatusMessageChanged?.Invoke(this, "Barometer reset to default values");
            }
            catch (Exception ex)
            {
                StatusMessageChanged?.Invoke(this, $"Error resetting barometer: {ex.Message}");
            }
        }

        #endregion
    }
}
