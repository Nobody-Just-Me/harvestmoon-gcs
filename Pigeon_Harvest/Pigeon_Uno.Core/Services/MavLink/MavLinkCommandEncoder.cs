using System;
using System.Collections.Generic;
using Pigeon_Uno.Core.Models;
using MavLinkNet;

namespace Pigeon_Uno.Core.Services.MavLink;

/// <summary>
/// MAVLink command encoder
/// Encodes commands into MAVLink messages
/// </summary>
public class MavLinkCommandEncoder
{
    /// <summary>
    /// Create an ARM/DISARM command message
    /// </summary>
    /// <param name="arm">True to arm, false to disarm</param>
    /// <returns>MAVLink command message</returns>
    public static UasMessage CreateArmDisarmCommand(bool arm)
    {
        // MAV_CMD_COMPONENT_ARM_DISARM (400)
        // param1: 1 to arm, 0 to disarm
        // param2: 0 (normal arm/disarm), 21196 (force arm/disarm)
        
        return CreateCommandLong(
            command: 400, // MAV_CMD_COMPONENT_ARM_DISARM
            param1: arm ? 1.0f : 0.0f,
            param2: 0.0f,
            param3: 0.0f,
            param4: 0.0f,
            param5: 0.0f,
            param6: 0.0f,
            param7: 0.0f
        );
    }

    /// <summary>
    /// Create a SET_MODE command message
    /// </summary>
    /// <param name="customMode">Custom mode number (autopilot-specific)</param>
    /// <returns>MAVLink command message</returns>
    public static UasMessage CreateSetModeCommand(uint customMode)
    {
        // MAV_CMD_DO_SET_MODE (176)
        // param1: mode (MAV_MODE)
        // param2: custom mode
        // param3: custom sub mode
        
        return CreateCommandLong(
            command: 176, // MAV_CMD_DO_SET_MODE
            param1: 1.0f, // MAV_MODE_FLAG_CUSTOM_MODE_ENABLED
            param2: customMode,
            param3: 0.0f,
            param4: 0.0f,
            param5: 0.0f,
            param6: 0.0f,
            param7: 0.0f
        );
    }

    /// <summary>
    /// Create a TAKEOFF command message
    /// </summary>
    /// <param name="altitude">Target altitude in meters</param>
    /// <param name="pitch">Pitch angle in degrees (optional)</param>
    /// <returns>MAVLink command message</returns>
    public static UasMessage CreateTakeoffCommand(float altitude, float pitch = 0.0f)
    {
        // MAV_CMD_NAV_TAKEOFF (22)
        // param1: pitch
        // param2: empty
        // param3: empty
        // param4: yaw angle
        // param5: latitude
        // param6: longitude
        // param7: altitude
        
        return CreateCommandLong(
            command: 22, // MAV_CMD_NAV_TAKEOFF
            param1: pitch,
            param2: 0.0f,
            param3: 0.0f,
            param4: float.NaN, // Use current yaw
            param5: float.NaN, // Use current position
            param6: float.NaN, // Use current position
            param7: altitude
        );
    }

    /// <summary>
    /// Create a LAND command message
    /// </summary>
    /// <param name="abortAlt">Altitude to abort landing (0 = no abort)</param>
    /// <returns>MAVLink command message</returns>
    public static UasMessage CreateLandCommand(float abortAlt = 0.0f)
    {
        // MAV_CMD_NAV_LAND (21)
        // param1: abort altitude
        // param2: land mode
        // param3: empty
        // param4: yaw angle
        // param5: latitude
        // param6: longitude
        // param7: altitude
        
        return CreateCommandLong(
            command: 21, // MAV_CMD_NAV_LAND
            param1: abortAlt,
            param2: 0.0f, // Precision land mode
            param3: 0.0f,
            param4: float.NaN, // Use current yaw
            param5: float.NaN, // Use current position
            param6: float.NaN, // Use current position
            param7: 0.0f
        );
    }

    /// <summary>
    /// Create an RTL (Return to Launch) command message
    /// </summary>
    /// <returns>MAVLink command message</returns>
    public static UasMessage CreateRtlCommand()
    {
        // MAV_CMD_NAV_RETURN_TO_LAUNCH (20)
        
        return CreateCommandLong(
            command: 20, // MAV_CMD_NAV_RETURN_TO_LAUNCH
            param1: 0.0f,
            param2: 0.0f,
            param3: 0.0f,
            param4: 0.0f,
            param5: 0.0f,
            param6: 0.0f,
            param7: 0.0f
        );
    }

    /// <summary>
    /// Create a LOITER command message
    /// </summary>
    /// <param name="radius">Loiter radius in meters</param>
    /// <param name="time">Loiter time in seconds (0 = unlimited)</param>
    /// <returns>MAVLink command message</returns>
    public static UasMessage CreateLoiterCommand(float radius = 50.0f, float time = 0.0f)
    {
        // MAV_CMD_NAV_LOITER_TIME (19)
        // param1: time
        // param2: empty
        // param3: radius
        // param4: yaw
        // param5: latitude
        // param6: longitude
        // param7: altitude
        
        return CreateCommandLong(
            command: 19, // MAV_CMD_NAV_LOITER_TIME
            param1: time,
            param2: 0.0f,
            param3: radius,
            param4: float.NaN, // Use current yaw
            param5: float.NaN, // Use current position
            param6: float.NaN, // Use current position
            param7: float.NaN // Use current altitude
        );
    }

    /// <summary>
    /// Create a START_MISSION command message
    /// </summary>
    /// <param name="firstItem">First mission item to run (0 = start from beginning)</param>
    /// <param name="lastItem">Last mission item to run (0 = run to end)</param>
    /// <returns>MAVLink command message</returns>
    public static UasMessage CreateStartMissionCommand(int firstItem = 0, int lastItem = 0)
    {
        // MAV_CMD_MISSION_START (300)
        // param1: first item
        // param2: last item
        
        return CreateCommandLong(
            command: 300, // MAV_CMD_MISSION_START
            param1: firstItem,
            param2: lastItem,
            param3: 0.0f,
            param4: 0.0f,
            param5: 0.0f,
            param6: 0.0f,
            param7: 0.0f
        );
    }

    /// <summary>
    /// Create a PAUSE_MISSION command message
    /// </summary>
    /// <returns>MAVLink command message</returns>
    public static UasMessage CreatePauseMissionCommand()
    {
        // MAV_CMD_DO_PAUSE_CONTINUE (193)
        // param1: 0 = pause, 1 = continue
        
        return CreateCommandLong(
            command: 193, // MAV_CMD_DO_PAUSE_CONTINUE
            param1: 0.0f, // Pause
            param2: 0.0f,
            param3: 0.0f,
            param4: 0.0f,
            param5: 0.0f,
            param6: 0.0f,
            param7: 0.0f
        );
    }

    /// <summary>
    /// Create a CONTINUE_MISSION command message
    /// </summary>
    /// <returns>MAVLink command message</returns>
    public static UasMessage CreateContinueMissionCommand()
    {
        // MAV_CMD_DO_PAUSE_CONTINUE (193)
        // param1: 0 = pause, 1 = continue
        
        return CreateCommandLong(
            command: 193, // MAV_CMD_DO_PAUSE_CONTINUE
            param1: 1.0f, // Continue
            param2: 0.0f,
            param3: 0.0f,
            param4: 0.0f,
            param5: 0.0f,
            param6: 0.0f,
            param7: 0.0f
        );
    }

    /// <summary>
    /// Create a SET_PARAMETER command message
    /// </summary>
    /// <param name="paramName">Parameter name (max 16 characters)</param>
    /// <param name="value">Parameter value</param>
    /// <returns>MAVLink parameter set message</returns>
    public static UasMessage CreateSetParameterCommand(string paramName, float value)
    {
        if (string.IsNullOrWhiteSpace(paramName))
        {
            throw new ArgumentException("Parameter name cannot be empty.", nameof(paramName));
        }

        return new UasParamSet
        {
            TargetSystem = 1,
            TargetComponent = 0,
            ParamId = ToMavLinkParamId(paramName),
            ParamValue = value,
            ParamType = MavParamType.Real32
        };
    }

    /// <summary>
    /// Create a REQUEST_PARAMETERS command message
    /// </summary>
    /// <returns>MAVLink parameter request message</returns>
    public static UasMessage CreateRequestParametersCommand()
    {
        return new UasParamRequestList
        {
            TargetSystem = 1,
            TargetComponent = 0
        };
    }

    /// <summary>
    /// Create a waypoint/mission item message
    /// </summary>
    /// <param name="seq">Sequence number</param>
    /// <param name="latitude">Latitude in degrees</param>
    /// <param name="longitude">Longitude in degrees</param>
    /// <param name="altitude">Altitude in meters</param>
    /// <param name="radius">Acceptance radius in meters</param>
    /// <returns>MAVLink mission item message</returns>
    public static UasMessage CreateWaypointCommand(
        ushort seq,
        double latitude,
        double longitude,
        float altitude,
        float radius = 10.0f)
    {
        if (latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude must be between -90 and 90 degrees.");
        }

        if (longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude), "Longitude must be between -180 and 180 degrees.");
        }

        return new UasMissionItemInt
        {
            TargetSystem = 1,
            TargetComponent = 0,
            Seq = seq,
            Frame = MavLinkNet.MavFrame.GlobalRelativeAltInt,
            Command = MavCmd.NavWaypoint,
            Current = seq == 0 ? (byte)1 : (byte)0,
            Autocontinue = 1,
            Param1 = 0.0f,
            Param2 = radius,
            Param3 = 0.0f,
            Param4 = float.NaN,
            X = (int)Math.Round(latitude * 10_000_000),
            Y = (int)Math.Round(longitude * 10_000_000),
            Z = altitude
        };
    }

    /// <summary>
    /// Create a HEARTBEAT message
    /// </summary>
    /// <returns>MAVLink heartbeat message</returns>
    public static UasMessage CreateHeartbeatMessage()
    {
        return new UasHeartbeat
        {
            Type = MavLinkNet.MavType.Gcs,
            Autopilot = MavAutopilot.Invalid,
            BaseMode = MavModeFlag.CustomModeEnabled,
            CustomMode = 0,
            SystemStatus = MavState.Active,
            MavlinkVersion = 3
        };
    }

    /// <summary>
    /// Helper method to create a COMMAND_LONG message
    /// </summary>
    public static UasCommandLong CreateCommandLong(
        uint command,
        float param1,
        float param2,
        float param3,
        float param4,
        float param5,
        float param6,
        float param7)
    {
        return new UasCommandLong
        {
            TargetSystem = 1,
            TargetComponent = 0,
            Command = (MavCmd)command,
            Confirmation = 0,
            Param1 = param1,
            Param2 = param2,
            Param3 = param3,
            Param4 = param4,
            Param5 = param5,
            Param6 = param6,
            Param7 = param7
        };
    }

    /// <summary>
    /// Map Command enum to MAVLink command
    /// </summary>
    public static UasMessage CreateCommandFromEnum(Command command, params float[] parameters)
    {
        switch (command)
        {
            case Command.ARM:
                return CreateArmDisarmCommand(true);
            
            case Command.DISARM:
                return CreateArmDisarmCommand(false);
            
            case Command.TAKE_OFF:
                float altitude = parameters.Length > 0 ? parameters[0] : 50.0f;
                return CreateTakeoffCommand(altitude);
            
            case Command.LAND:
                return CreateLandCommand();
            
            case Command.RTL:
                return CreateRtlCommand();
            
            case Command.PAUSE:
                return CreatePauseMissionCommand();
            
            case Command.CONTINUE:
                return CreateContinueMissionCommand();
            
            case Command.BATALKAN:
                // Cancel takeoff - send land command
                return CreateLandCommand();
            
            default:
                throw new ArgumentException($"Unknown command: {command}");
        }
    }

    private static char[] ToMavLinkParamId(string value)
    {
        var paramId = new char[16];
        var source = value.ToCharArray();
        Array.Copy(source, paramId, Math.Min(source.Length, paramId.Length));
        return paramId;
    }
}
