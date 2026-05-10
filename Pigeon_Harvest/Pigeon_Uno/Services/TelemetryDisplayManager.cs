using System;
using System.Collections.Generic;
using Pigeon_Uno.Core.Models;
using Pigeon_Uno.Core.Services;
using Pigeon_Uno.Models;

namespace Pigeon_Uno.Services
{
    /// <summary>
    /// Manages telemetry display updates and data aggregation
    /// Handles battery, GPS, RC channels, and system status displays
    /// </summary>
    public class TelemetryDisplayManager
    {
        private readonly IMavLinkService _mavLinkService;
        private readonly ILoggingService _logger;
        private readonly BatteryWarningSystem _batteryWarning;
        private readonly Dictionary<string, object> _latestValues;
        private readonly object _telemetryUpdateLock = new object();
        private TelemetryData? _pendingTelemetry;
        private bool _telemetryDispatchScheduled;
        private DateTime _lastTelemetryDispatchTime = DateTime.MinValue;
        private const int TelemetryUpdateIntervalMs = 100;

        /// <summary>
        /// Event fired when telemetry display is updated
        /// </summary>
        public event EventHandler<TelemetryData>? TelemetryUpdated;

        /// <summary>
        /// Event fired when battery status changes
        /// </summary>
        public event EventHandler<BatteryStatus>? BatteryStatusChanged;

        /// <summary>
        /// Gets the latest telemetry data
        /// </summary>
        public TelemetryData? LatestTelemetry { get; private set; }

        public TelemetryDisplayManager(
            IMavLinkService mavLinkService,
            ILoggingService logger,
            BatteryWarningSystem batteryWarning)
        {
            _mavLinkService = mavLinkService;
            _logger = logger;
            _batteryWarning = batteryWarning;
            _latestValues = new Dictionary<string, object>();

            // Subscribe to MAVLink telemetry updates
            // _mavLinkService.TelemetryUpdated += OnTelemetryReceived;
            // Note: TelemetryUpdated event not available, using TelemetryReceived instead
            _mavLinkService.TelemetryReceived += OnTelemetryReceivedFromMavLink;
        }

        /// <summary>
        /// Handles incoming telemetry data from MAVLink
        /// </summary>
        private void OnTelemetryReceivedFromMavLink(object? sender, FlightData flightData)
        {
            if (flightData == null) return;

            // Convert FlightData to TelemetryData
            var telemetry = ConvertFlightDataToTelemetryData(flightData);
            
            // Process the converted telemetry
            OnTelemetryReceived(this, telemetry);
        }

        /// <summary>
        /// Converts FlightData from MAVLink to TelemetryData for display
        /// </summary>
        private TelemetryData ConvertFlightDataToTelemetryData(FlightData flightData)
        {
            return new TelemetryData
            {
                Timestamp = DateTime.Now,
                Latitude = flightData.GPS.Latitude / 1e7,
                Longitude = flightData.GPS.Longitude / 1e7,
                Altitude = flightData.AltitudeFloat,
                RelativeAltitude = flightData.AltitudeFloat,
                Barometers = flightData.Barometers > 0 ? flightData.Barometers : flightData.AltitudeFloat,
                Roll = flightData.IMU.Roll,
                Pitch = flightData.IMU.Pitch,
                Yaw = flightData.IMU.Yaw,
                Heading = flightData.IMU.Yaw,
                GroundSpeed = flightData.Speed,
                AirSpeed = flightData.Speed,
                VerticalSpeed = 0,
                Speed = flightData.Speed,
                BatteryVoltage = flightData.BatteryVolt,
                BatteryCurrent = flightData.BatteryCurr,
                BatteryRemaining = 0,
                BatteryPercentage = CalculateBatteryPercentage(flightData.BatteryVolt),
                FlightMode = flightData.FlightMode,
                IsArmed = flightData.FlightMode != FlightMode.DISARMED,
                SatelliteCount = flightData.Sats,
                HDOP = flightData.Hdop / 100.0,
                SignalStrength = flightData.Signal,
                ThrottlePercent = flightData.ThrottlePercent
            };
        }

        /// <summary>
        /// Calculates battery percentage from voltage (assuming 4S LiPo: 16.8V full, 14.0V empty)
        /// </summary>
        private double CalculateBatteryPercentage(double voltage)
        {
            const double fullVoltage = 16.8;
            const double emptyVoltage = 14.0;
            
            if (voltage <= 0) return 0;
            
            var percentage = ((voltage - emptyVoltage) / (fullVoltage - emptyVoltage)) * 100.0;
            return Math.Clamp(percentage, 0, 100);
        }

        /// <summary>
        /// Handles incoming telemetry data from MAVLink
        /// </summary>
        private void OnTelemetryReceived(object? sender, TelemetryData telemetry)
        {
            if (telemetry == null) return;

            TelemetryData? telemetryToDispatch = null;
            int scheduleDelayMs = 0;
            bool shouldSchedule = false;

            lock (_telemetryUpdateLock)
            {
                _pendingTelemetry = telemetry;
                var now = DateTime.UtcNow;
                var elapsedMs = (now - _lastTelemetryDispatchTime).TotalMilliseconds;

                if (elapsedMs >= TelemetryUpdateIntervalMs)
                {
                    telemetryToDispatch = _pendingTelemetry;
                    _pendingTelemetry = null;
                    _lastTelemetryDispatchTime = now;
                    _telemetryDispatchScheduled = false;
                }
                else if (!_telemetryDispatchScheduled)
                {
                    _telemetryDispatchScheduled = true;
                    shouldSchedule = true;
                    scheduleDelayMs = Math.Max(1, (int)(TelemetryUpdateIntervalMs - elapsedMs));
                }
            }

            if (telemetryToDispatch != null)
            {
                PublishTelemetryUpdate(telemetryToDispatch);
            }

            if (shouldSchedule)
            {
                _ = System.Threading.Tasks.Task.Delay(scheduleDelayMs).ContinueWith(_ =>
                {
                    TelemetryData? delayedTelemetry = null;
                    lock (_telemetryUpdateLock)
                    {
                        delayedTelemetry = _pendingTelemetry;
                        _pendingTelemetry = null;
                        _lastTelemetryDispatchTime = DateTime.UtcNow;
                        _telemetryDispatchScheduled = false;
                    }

                    if (delayedTelemetry != null)
                    {
                        PublishTelemetryUpdate(delayedTelemetry);
                    }
                });
            }
        }

        private void PublishTelemetryUpdate(TelemetryData telemetry)
        {
            if (telemetry == null) return;

            LatestTelemetry = telemetry;

            // Update battery status
            UpdateBatteryStatus(telemetry);

            // Store latest values
            _latestValues["altitude"] = telemetry.Altitude;
            _latestValues["speed"] = telemetry.Speed;
            _latestValues["heading"] = telemetry.Heading;
            _latestValues["battery"] = telemetry.BatteryPercentage;

            // Notify subscribers
            TelemetryUpdated?.Invoke(this, telemetry);

            _logger.LogDebug($"Telemetry updated - Alt: {telemetry.Altitude:F1}m, Speed: {telemetry.Speed:F1}m/s", nameof(TelemetryDisplayManager));
        }

        /// <summary>
        /// Updates battery status and triggers warnings if needed
        /// </summary>
        private void UpdateBatteryStatus(TelemetryData telemetry)
        {
            var batteryStatus = new BatteryStatus
            {
                Percentage = telemetry.BatteryPercentage,
                Voltage = telemetry.BatteryVoltage,
                Current = telemetry.BatteryCurrent,
                Remaining = telemetry.BatteryRemaining
            };

            // Check battery warnings
            _batteryWarning.CheckBatteryLevel(batteryStatus);

            // Notify subscribers
            BatteryStatusChanged?.Invoke(this, batteryStatus);
        }

        /// <summary>
        /// Gets the latest value for a specific telemetry parameter
        /// </summary>
        public T? GetLatestValue<T>(string key)
        {
            if (_latestValues.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return default;
        }

        /// <summary>
        /// Gets all latest telemetry values
        /// </summary>
        public IReadOnlyDictionary<string, object> GetAllLatestValues()
        {
            return _latestValues;
        }

        /// <summary>
        /// Clears all stored telemetry data
        /// </summary>
        public void ClearData()
        {
            _latestValues.Clear();
            LatestTelemetry = null;
            _logger.LogInfo("Telemetry data cleared", nameof(TelemetryDisplayManager));
        }
    }

    /// <summary>
    /// Represents battery status information
    /// </summary>
    public class BatteryStatus
    {
        public double Percentage { get; set; }
        public double Voltage { get; set; }
        public double Current { get; set; }
        public double Remaining { get; set; }
    }
}
