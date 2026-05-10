using System;
using Pigeon_Uno.Models;
using Pigeon_Uno.Core.Models;
using MavLinkNet;

namespace Pigeon_Uno.Core.Services.MavLink;

/// <summary>
/// MAVLink message parser
/// Parses MAVLink messages and updates FlightData model
/// </summary>
public class MavLinkMessageParser
{
    /// <summary>
    /// Parse a MAVLink packet and update flight data
    /// </summary>
    /// <param name="packet">The MAVLink packet to parse</param>
    /// <param name="flightData">The FlightData object to update</param>
    /// <returns>True if the packet was successfully parsed</returns>
    public static bool ParsePacket(MavLinkPacketBase packet, FlightData flightData)
    {
        if (packet == null || flightData == null || !packet.IsValid)
            return false;

        try
        {
            // Get the message ID
            var messageId = packet.MessageId;

            switch (messageId)
            {
                case 0: // HEARTBEAT
                    ParseHeartbeat(packet, flightData);
                    return true;

                case 30: // ATTITUDE
                    ParseAttitude(packet, flightData);
                    return true;

                case 33: // GLOBAL_POSITION_INT
                    ParseGlobalPositionInt(packet, flightData);
                    return true;

                case 74: // VFR_HUD
                    ParseVfrHud(packet, flightData);
                    return true;

                case 1: // SYS_STATUS
                    ParseSysStatus(packet, flightData);
                    return true;

                case 39: // MISSION_ITEM
                    ParseMissionItem(packet, flightData);
                    return true;

                default:
                    // Unknown or unhandled message
                    return false;
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Parse HEARTBEAT message (ID: 0)
    /// </summary>
    private static void ParseHeartbeat(MavLinkPacketBase packet, FlightData flightData)
    {
        // HEARTBEAT message structure:
        // uint32_t custom_mode
        // uint8_t type
        // uint8_t autopilot
        // uint8_t base_mode
        // uint8_t system_status
        // uint8_t mavlink_version

        var payload = packet.Payload;
        if (payload.Length < 9) return;

        // Parse custom mode (first 4 bytes)
        uint customMode = BitConverter.ToUInt32(payload, 0);

        // Parse base mode (byte 5)
        byte baseMode = payload[5];

        // Check if armed (bit 7 of base_mode)
        bool isArmed = (baseMode & 0x80) != 0;

        // Update flight mode based on custom mode
        // This is ArduPilot specific - different autopilots may use different mappings
        flightData.FlightMode = MapCustomModeToFlightMode(customMode, isArmed);
    }

    /// <summary>
    /// Parse ATTITUDE message (ID: 30)
    /// </summary>
    private static void ParseAttitude(MavLinkPacketBase packet, FlightData flightData)
    {
        // ATTITUDE message structure:
        // uint32_t time_boot_ms
        // float roll
        // float pitch
        // float yaw
        // float rollspeed
        // float pitchspeed
        // float yawspeed

        var payload = packet.Payload;
        if (payload.Length < 28) return;

        // Skip time_boot_ms (first 4 bytes)
        // Parse roll, pitch, yaw (radians)
        float roll = BitConverter.ToSingle(payload, 4);
        float pitch = BitConverter.ToSingle(payload, 8);
        float yaw = BitConverter.ToSingle(payload, 12);

        // Convert from radians to degrees
        flightData.IMU.Roll = (float)(roll * 180.0 / Math.PI);
        flightData.IMU.Pitch = (float)(pitch * 180.0 / Math.PI);
        flightData.IMU.Yaw = (float)(yaw * 180.0 / Math.PI);

        // Normalize yaw to 0-360 range
        if (flightData.IMU.Yaw < 0)
            flightData.IMU.Yaw += 360;
    }

    /// <summary>
    /// Parse GLOBAL_POSITION_INT message (ID: 33)
    /// </summary>
    private static void ParseGlobalPositionInt(MavLinkPacketBase packet, FlightData flightData)
    {
        // GLOBAL_POSITION_INT message structure:
        // uint32_t time_boot_ms
        // int32_t lat (degE7)
        // int32_t lon (degE7)
        // int32_t alt (mm above MSL)
        // int32_t relative_alt (mm above ground)
        // int16_t vx (cm/s)
        // int16_t vy (cm/s)
        // int16_t vz (cm/s)
        // uint16_t hdg (cdeg)

        var payload = packet.Payload;
        if (payload.Length < 28) return;

        // Skip time_boot_ms (first 4 bytes)
        // Parse latitude and longitude (degE7 = degrees * 10^7)
        int lat = BitConverter.ToInt32(payload, 4);
        int lon = BitConverter.ToInt32(payload, 8);

        // Parse altitude (mm above MSL)
        int alt = BitConverter.ToInt32(payload, 12);

        // Parse heading (cdeg = degrees * 100)
        ushort hdg = BitConverter.ToUInt16(payload, 24);

        // Update GPS data
        flightData.GPS.Latitude = lat;
        flightData.GPS.Longitude = lon;

        // Update altitude (convert from mm to mm - already in correct unit)
        flightData.Altitude = alt;
        flightData.AltitudeFloat = alt / 1000.0f; // Convert to meters for float version

        // Update heading (convert from cdeg to degrees)
        if (hdg != 65535) // 65535 means unknown
        {
            flightData.IMU.Yaw = hdg / 100.0f;
        }
    }

    /// <summary>
    /// Parse VFR_HUD message (ID: 74)
    /// </summary>
    private static void ParseVfrHud(MavLinkPacketBase packet, FlightData flightData)
    {
        // VFR_HUD message structure:
        // float airspeed (m/s)
        // float groundspeed (m/s)
        // int16_t heading (deg)
        // uint16_t throttle (%)
        // float alt (m)
        // float climb (m/s)

        var payload = packet.Payload;
        if (payload.Length < 20) return;

        // Parse airspeed (m/s)
        float airspeed = BitConverter.ToSingle(payload, 0);

        // Parse groundspeed (m/s)
        float groundspeed = BitConverter.ToSingle(payload, 4);

        // Parse heading (degrees)
        short heading = BitConverter.ToInt16(payload, 8);

        // Parse altitude (meters)
        float alt = BitConverter.ToSingle(payload, 12);

        // Parse throttle (uint16, 0-100%)
        if (payload.Length >= 18)
        {
            ushort throttle = BitConverter.ToUInt16(payload, 16);
            flightData.ThrottlePercent = Math.Min(100, (int)throttle);
        }

        // Update flight data
        flightData.Speed = groundspeed;

        // Update heading if valid
        if (heading >= 0 && heading <= 360)
        {
            flightData.IMU.Yaw = heading;
        }

        // Update altitude (convert from meters to millimeters)
        flightData.AltitudeFloat = alt;
        flightData.Altitude = (int)(alt * 1000);
    }

    /// <summary>
    /// Parse SYS_STATUS message (ID: 1)
    /// </summary>
    private static void ParseSysStatus(MavLinkPacketBase packet, FlightData flightData)
    {
        // SYS_STATUS message structure:
        // uint32_t onboard_control_sensors_present
        // uint32_t onboard_control_sensors_enabled
        // uint32_t onboard_control_sensors_health
        // uint16_t load (d%)
        // uint16_t voltage_battery (mV)
        // int16_t current_battery (cA)
        // int8_t battery_remaining (%)
        // uint16_t drop_rate_comm (c%)
        // uint16_t errors_comm
        // uint16_t errors_count1
        // uint16_t errors_count2
        // uint16_t errors_count3
        // uint16_t errors_count4

        var payload = packet.Payload;
        if (payload.Length < 31) return;

        // Parse voltage (mV)
        ushort voltage = BitConverter.ToUInt16(payload, 12);

        // Parse current (cA = centi-amps)
        short current = BitConverter.ToInt16(payload, 14);

        // Parse battery remaining (%)
        sbyte batteryRemaining = (sbyte)payload[16];

        // Parse drop rate (c% = percent * 100)
        ushort dropRate = BitConverter.ToUInt16(payload, 17);

        // Update flight data
        flightData.MavlinkMiliVolt = voltage;
        flightData.MavlinkCentiAmp = current;

        // Convert to display units
        flightData.BatteryVolt = (ushort)(voltage / 1000); // Convert mV to V
        flightData.BatteryCurr = (ushort)Math.Abs(current / 100); // Convert cA to A

        // Calculate signal quality from drop rate
        // drop_rate is in c% (0-10000), convert to 0-255 scale
        // Lower drop rate = better signal
        if (dropRate <= 10000)
        {
            flightData.Signal = (byte)(255 - (dropRate * 255 / 10000));
        }
    }

    /// <summary>
    /// Parse MISSION_ITEM message (ID: 39)
    /// </summary>
    private static void ParseMissionItem(MavLinkPacketBase packet, FlightData flightData)
    {
        // MISSION_ITEM message structure:
        // uint8_t target_system
        // uint8_t target_component
        // uint16_t seq
        // uint8_t frame
        // uint16_t command
        // uint8_t current
        // uint8_t autocontinue
        // float param1
        // float param2
        // float param3
        // float param4
        // float x (latitude)
        // float y (longitude)
        // float z (altitude)

        var payload = packet.Payload;
        if (payload.Length < 37) return;

        // Parse sequence number
        ushort seq = BitConverter.ToUInt16(payload, 2);

        // Parse parameters
        float param1 = BitConverter.ToSingle(payload, 9);
        float param2 = BitConverter.ToSingle(payload, 13);
        float param3 = BitConverter.ToSingle(payload, 17);
        float param4 = BitConverter.ToSingle(payload, 21);

        // Parse position
        float x = BitConverter.ToSingle(payload, 25); // latitude
        float y = BitConverter.ToSingle(payload, 29); // longitude
        float z = BitConverter.ToSingle(payload, 33); // altitude

        // Update waypoint data
        flightData.Wpoint.Speed = param1;
        flightData.Wpoint.Radius = param2;
        flightData.Wpoint.LoiterSpeed = param3;
        flightData.Wpoint.TargetLongt2 = y;
    }

    /// <summary>
    /// Map ArduPilot custom mode to FlightMode enum
    /// </summary>
    private static FlightMode MapCustomModeToFlightMode(uint customMode, bool isArmed)
    {
        if (!isArmed)
            return FlightMode.DISARMED;

        // ArduPilot Plane modes
        switch (customMode)
        {
            case 0: return FlightMode.MANUAL;
            case 1: return FlightMode.MANUAL; // CIRCLE
            case 2: return FlightMode.STABILIZER;
            case 3: return FlightMode.MANUAL; // TRAINING
            case 4: return FlightMode.MANUAL; // ACRO
            case 5: return FlightMode.FBWA;
            case 6: return FlightMode.FBWA; // FBWB
            case 7: return FlightMode.MANUAL; // CRUISE
            case 8: return FlightMode.MANUAL; // AUTOTUNE
            case 10: return FlightMode.AUTO;
            case 11: return FlightMode.RTL;
            case 12: return FlightMode.LOITER;
            case 13: return FlightMode.TAKEOFF;
            case 14: return FlightMode.HOLD_ALTITUDE; // AVOID_ADSB
            case 15: return FlightMode.MANUAL; // GUIDED
            case 16: return FlightMode.MANUAL; // INITIALIZING
            case 17: return FlightMode.Q_Stabilize;
            case 18: return FlightMode.Q_Hover;
            case 19: return FlightMode.Q_Land;
            case 20: return FlightMode.RTL; // Q_RTL
            case 21: return FlightMode.AUTO; // Q_AUTOTUNE
            case 22: return FlightMode.AUTO; // Q_ACRO
            case 23: return FlightMode.AUTO; // THERMAL
            default: return isArmed ? FlightMode.ARMED : FlightMode.DISARMED;
        }
    }
}
