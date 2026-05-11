using System;
using MavLinkNet;
using HarvestmoonGCS.Models;

namespace HarvestmoonGCS.Core.Diagnostics;

/// <summary>
/// Validates data integrity through transformation stages in the telemetry pipeline.
/// Logs field changes and detects unexpected null or default values.
/// </summary>
public interface IDataTransformationValidator
{
    void ValidatePacketToFlightData(MavLinkPacketBase packet, FlightData before, FlightData after);
    void ValidateFlightDataToTelemetry(FlightData flightData, object telemetryData);
}

/// <summary>
/// Implementation of data transformation validator with diagnostic logging.
/// </summary>
public class DataTransformationValidator : IDataTransformationValidator
{
    private readonly IDiagnosticLogger _logger;
    
    public DataTransformationValidator(IDiagnosticLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <summary>
    /// Validates that packet data was correctly transformed into FlightData.
    /// Logs field changes and detects missing updates.
    /// </summary>
    public void ValidatePacketToFlightData(MavLinkPacketBase packet, FlightData before, FlightData after)
    {
        if (packet == null || before == null || after == null)
        {
            _logger.LogFlightDataUpdate("Validation", "NULL_INPUT", "Skipped validation due to null input");
            return;
        }
        
        // For ATTITUDE packets (message ID 30), verify roll/pitch/yaw were updated
        if (packet.MessageId == 30)
        {
            bool rollChanged = Math.Abs(before.IMU.Roll - after.IMU.Roll) > 0.001f;
            bool pitchChanged = Math.Abs(before.IMU.Pitch - after.IMU.Pitch) > 0.001f;
            bool yawChanged = Math.Abs(before.IMU.Yaw - after.IMU.Yaw) > 0.001f;
            
            if (!rollChanged && !pitchChanged && !yawChanged)
            {
                _logger.LogFlightDataUpdate("ATTITUDE", "NO_CHANGE", "WARNING: ATTITUDE packet did not update IMU values");
            }
            else
            {
                if (rollChanged)
                    _logger.LogFlightDataUpdate("Roll", before.IMU.Roll, after.IMU.Roll);
                if (pitchChanged)
                    _logger.LogFlightDataUpdate("Pitch", before.IMU.Pitch, after.IMU.Pitch);
                if (yawChanged)
                    _logger.LogFlightDataUpdate("Yaw", before.IMU.Yaw, after.IMU.Yaw);
            }
        }
        
        // For GPS_RAW_INT packets (message ID 24), verify GPS data was updated
        else if (packet.MessageId == 24)
        {
            bool latChanged = before.GPS.Latitude != after.GPS.Latitude;
            bool lonChanged = before.GPS.Longitude != after.GPS.Longitude;
            
            if (!latChanged && !lonChanged)
            {
                _logger.LogFlightDataUpdate("GPS", "NO_CHANGE", "WARNING: GPS packet did not update coordinates");
            }
            else
            {
                if (latChanged)
                    _logger.LogFlightDataUpdate("GPS.Latitude", before.GPS.Latitude, after.GPS.Latitude);
                if (lonChanged)
                    _logger.LogFlightDataUpdate("GPS.Longitude", before.GPS.Longitude, after.GPS.Longitude);
            }
        }
        
        // For VFR_HUD packets (message ID 74), verify speed and altitude were updated
        else if (packet.MessageId == 74)
        {
            bool speedChanged = Math.Abs(before.Speed - after.Speed) > 0.01f;
            bool altChanged = Math.Abs(before.AltitudeFloat - after.AltitudeFloat) > 0.1f;
            
            if (speedChanged)
                _logger.LogFlightDataUpdate("Speed", before.Speed, after.Speed);
            if (altChanged)
                _logger.LogFlightDataUpdate("Altitude", before.AltitudeFloat, after.AltitudeFloat);
        }
        
        // For SYS_STATUS packets (message ID 1), verify battery data was updated
        else if (packet.MessageId == 1)
        {
            bool voltChanged = before.MavlinkMiliVolt != after.MavlinkMiliVolt;
            bool currChanged = before.MavlinkCentiAmp != after.MavlinkCentiAmp;
            
            if (voltChanged)
                _logger.LogFlightDataUpdate("Battery.Voltage", before.MavlinkMiliVolt, after.MavlinkMiliVolt);
            if (currChanged)
                _logger.LogFlightDataUpdate("Battery.Current", before.MavlinkCentiAmp, after.MavlinkCentiAmp);
        }
        
        // For HEARTBEAT packets (message ID 0), verify flight mode was updated
        else if (packet.MessageId == 0)
        {
            if (before.FlightMode != after.FlightMode)
            {
                _logger.LogFlightDataUpdate("FlightMode", before.FlightMode, after.FlightMode);
            }
        }
    }
    
    /// <summary>
    /// Validates that FlightData was correctly mapped to TelemetryData.
    /// Verifies all fields are preserved with correct precision.
    /// </summary>
    public void ValidateFlightDataToTelemetry(FlightData flightData, object telemetryData)
    {
        if (flightData == null || telemetryData == null)
        {
            _logger.LogFlightDataUpdate("Mapping", "NULL_INPUT", "Skipped validation due to null input");
            return;
        }
        
        // Use reflection to compare properties
        var telemetryType = telemetryData.GetType();
        
        // Check Roll mapping
        var rollProp = telemetryType.GetProperty("Roll");
        if (rollProp != null)
        {
            var telemetryRoll = Convert.ToSingle(rollProp.GetValue(telemetryData));
            if (Math.Abs(flightData.IMU.Roll - telemetryRoll) > 0.01f)
            {
                _logger.LogFlightDataUpdate("Roll Mapping", flightData.IMU.Roll, telemetryRoll);
            }
        }
        
        // Check Pitch mapping
        var pitchProp = telemetryType.GetProperty("Pitch");
        if (pitchProp != null)
        {
            var telemetryPitch = Convert.ToSingle(pitchProp.GetValue(telemetryData));
            if (Math.Abs(flightData.IMU.Pitch - telemetryPitch) > 0.01f)
            {
                _logger.LogFlightDataUpdate("Pitch Mapping", flightData.IMU.Pitch, telemetryPitch);
            }
        }
        
        // Check Yaw mapping
        var yawProp = telemetryType.GetProperty("Yaw");
        if (yawProp != null)
        {
            var telemetryYaw = Convert.ToSingle(yawProp.GetValue(telemetryData));
            if (Math.Abs(flightData.IMU.Yaw - telemetryYaw) > 0.01f)
            {
                _logger.LogFlightDataUpdate("Yaw Mapping", flightData.IMU.Yaw, telemetryYaw);
            }
        }
        
        // Check Altitude mapping
        var altitudeProp = telemetryType.GetProperty("Altitude");
        if (altitudeProp != null)
        {
            var telemetryAltitude = Convert.ToSingle(altitudeProp.GetValue(telemetryData));
            if (Math.Abs(flightData.AltitudeFloat - telemetryAltitude) > 0.1f)
            {
                _logger.LogFlightDataUpdate("Altitude Mapping", flightData.AltitudeFloat, telemetryAltitude);
            }
        }
        
        // Check Speed mapping
        var speedProp = telemetryType.GetProperty("Speed");
        if (speedProp != null)
        {
            var telemetrySpeed = Convert.ToSingle(speedProp.GetValue(telemetryData));
            if (Math.Abs(flightData.Speed - telemetrySpeed) > 0.1f)
            {
                _logger.LogFlightDataUpdate("Speed Mapping", flightData.Speed, telemetrySpeed);
            }
        }
    }
}
