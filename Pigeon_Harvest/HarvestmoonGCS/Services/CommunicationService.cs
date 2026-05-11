using System;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Models;

namespace HarvestmoonGCS.Services
{
    /// <summary>
    /// High-level communication service that manages external system integration
    /// Provides unified API for telemetry streaming and command relay
    /// </summary>
    public class CommunicationService
    {
        private readonly SocketIOManager _socketManager;
        private readonly IMavLinkService _mavLinkService;
        private readonly ILoggingService _logger;
        private bool _isRelayEnabled;
        private readonly object _relayLock = new object();
        private TelemetryData? _pendingRelayTelemetry;
        private bool _relayScheduled;
        private DateTime _lastRelayTime = DateTime.MinValue;
        private const int RelayIntervalMs = 100;

        /// <summary>
        /// Event fired when telemetry is relayed to external system
        /// </summary>
        public event EventHandler<TelemetryData>? TelemetryRelayed;

        /// <summary>
        /// Event fired when command is received from external system
        /// </summary>
        public event EventHandler<string>? CommandReceived;

        /// <summary>
        /// Gets whether communication service is active
        /// </summary>
        public bool IsActive => _socketManager.IsConnected;

        /// <summary>
        /// Gets or sets whether telemetry relay is enabled
        /// </summary>
        public bool IsRelayEnabled
        {
            get => _isRelayEnabled;
            set
            {
                _isRelayEnabled = value;
                _logger.LogInfo($"Telemetry relay {(value ? "enabled" : "disabled")}", nameof(CommunicationService));
            }
        }

        public CommunicationService(
            SocketIOManager socketManager,
            IMavLinkService mavLinkService,
            ILoggingService logger)
        {
            _socketManager = socketManager;
            _mavLinkService = mavLinkService;
            _logger = logger;

            // Subscribe to MAVLink telemetry updates
            _mavLinkService.TelemetryReceived += OnTelemetryReceivedFromMavLink;
            
            // Subscribe to Socket.IO events
            _socketManager.Connected += OnSocketConnected;
            _socketManager.Disconnected += OnSocketDisconnected;
            _socketManager.ConnectionError += OnSocketError;
        }

        /// <summary>
        /// Starts communication service with specified server
        /// </summary>
        public async Task<bool> StartAsync(string serverUrl)
        {
            _logger.LogInfo($"Starting communication service: {serverUrl}", nameof(CommunicationService));
            
            var connected = await _socketManager.ConnectAsync(serverUrl);
            if (connected)
            {
                IsRelayEnabled = true;
                _logger.LogInfo("Communication service started successfully", nameof(CommunicationService));
            }
            
            return connected;
        }

        /// <summary>
        /// Stops communication service
        /// </summary>
        public async Task StopAsync()
        {
            _logger.LogInfo("Stopping communication service", nameof(CommunicationService));
            
            IsRelayEnabled = false;
            await _socketManager.DisconnectAsync();
            
            _logger.LogInfo("Communication service stopped", nameof(CommunicationService));
        }

        /// <summary>
        /// Sends command to external system
        /// </summary>
        public async Task<bool> SendCommandAsync(string command, object? payload = null)
        {
            if (!_socketManager.IsConnected)
            {
                _logger.LogWarning("Cannot send command - not connected", nameof(CommunicationService));
                return false;
            }

            try
            {
                _logger.LogInfo($"Sending command: {command}", nameof(CommunicationService));
                // Actual command sending implementation would go here
                await Task.Delay(10);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to send command: {ex.Message}", nameof(CommunicationService));
                return false;
            }
        }

        /// <summary>
        /// Handles telemetry updates from MAVLink
        /// </summary>
        private async void OnTelemetryReceivedFromMavLink(object? sender, FlightData flightData)
        {
            if (!IsRelayEnabled || !_socketManager.IsConnected) return;

            try
            {
                var telemetry = ConvertFlightDataToTelemetryData(flightData);

                TelemetryData? telemetryToSend = null;
                int delayMs = 0;
                bool shouldSchedule = false;

                lock (_relayLock)
                {
                    _pendingRelayTelemetry = telemetry;
                    var now = DateTime.UtcNow;
                    var elapsedMs = (now - _lastRelayTime).TotalMilliseconds;

                    if (elapsedMs >= RelayIntervalMs)
                    {
                        telemetryToSend = _pendingRelayTelemetry;
                        _pendingRelayTelemetry = null;
                        _lastRelayTime = now;
                        _relayScheduled = false;
                    }
                    else if (!_relayScheduled)
                    {
                        _relayScheduled = true;
                        shouldSchedule = true;
                        delayMs = Math.Max(1, (int)(RelayIntervalMs - elapsedMs));
                    }
                }

                if (telemetryToSend != null)
                {
                    await RelayTelemetryAsync(telemetryToSend);
                }

                if (shouldSchedule)
                {
                    _ = FlushRelayAfterDelayAsync(delayMs);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to relay telemetry: {ex.Message}", nameof(CommunicationService));
            }
        }

        private async Task FlushRelayAfterDelayAsync(int delayMs)
        {
            await Task.Delay(delayMs);

            TelemetryData? delayedTelemetry = null;
            lock (_relayLock)
            {
                delayedTelemetry = _pendingRelayTelemetry;
                _pendingRelayTelemetry = null;
                _lastRelayTime = DateTime.UtcNow;
                _relayScheduled = false;
            }

            if (delayedTelemetry != null)
            {
                await RelayTelemetryAsync(delayedTelemetry);
            }
        }

        private async Task RelayTelemetryAsync(TelemetryData telemetry)
        {
            var sent = await _socketManager.SendTelemetryAsync(telemetry);
            if (sent)
            {
                TelemetryRelayed?.Invoke(this, telemetry);
            }
        }

        /// <summary>
        /// Convert FlightData to TelemetryData
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
                Roll = flightData.IMU.Roll,
                Pitch = flightData.IMU.Pitch,
                Yaw = flightData.IMU.Yaw,
                Heading = flightData.IMU.Yaw,
                GroundSpeed = flightData.Speed,
                AirSpeed = flightData.Speed,
                BatteryVoltage = flightData.BatteryVolt,
                BatteryCurrent = flightData.BatteryCurr,
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
        /// Calculate battery percentage from voltage (assuming 4S LiPo)
        /// </summary>
        private double CalculateBatteryPercentage(ushort milliVolts)
        {
            if (milliVolts == 0) return 0;
            float voltage = milliVolts / 1000.0f;
            // 4S LiPo: 16.8V full, 14.0V empty
            double percent = ((voltage - 14.0) / (16.8 - 14.0)) * 100;
            return Math.Clamp(percent, 0, 100);
        }

        private void OnSocketConnected(object? sender, EventArgs e)
        {
            _logger.LogInfo("Communication service connected to server", nameof(CommunicationService));
        }

        private void OnSocketDisconnected(object? sender, EventArgs e)
        {
            _logger.LogWarning("Communication service disconnected from server", nameof(CommunicationService));
        }

        private void OnSocketError(object? sender, string error)
        {
            _logger.LogError($"Communication service error: {error}", nameof(CommunicationService));
        }
    }
}
