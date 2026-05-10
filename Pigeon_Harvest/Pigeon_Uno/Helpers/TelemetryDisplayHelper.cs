using System;
using System.Globalization;
using Pigeon_Uno.Core.Models;

namespace Pigeon_Uno.Helpers
{
    /// <summary>
    /// Helper class for formatting telemetry data for display
    /// </summary>
    public static class TelemetryDisplayHelper
    {
        /// <summary>
        /// Formats altitude with unit
        /// </summary>
        public static string FormatAltitude(double altitudeMeters, bool useImperial = false)
        {
            if (useImperial)
            {
                var feet = altitudeMeters * 3.28084;
                return $"{feet:F0} ft";
            }
            return $"{altitudeMeters:F1} m";
        }

        /// <summary>
        /// Formats speed with unit
        /// </summary>
        public static string FormatSpeed(double speedMps, bool useImperial = false)
        {
            if (useImperial)
            {
                var mph = speedMps * 2.23694;
                return $"{mph:F1} mph";
            }
            return $"{speedMps:F1} m/s";
        }

        /// <summary>
        /// Formats distance with appropriate unit
        /// </summary>
        public static string FormatDistance(double distanceMeters, bool useImperial = false)
        {
            if (useImperial)
            {
                if (distanceMeters >= 1609.34)
                {
                    var miles = distanceMeters / 1609.34;
                    return $"{miles:F2} mi";
                }
                var feet = distanceMeters * 3.28084;
                return $"{feet:F0} ft";
            }

            if (distanceMeters >= 1000)
            {
                return $"{distanceMeters / 1000:F2} km";
            }
            return $"{distanceMeters:F0} m";
        }

        /// <summary>
        /// Formats coordinate in degrees minutes seconds
        /// </summary>
        public static string FormatCoordinateDMS(double coordinate, bool isLatitude)
        {
            var direction = isLatitude 
                ? (coordinate >= 0 ? "N" : "S")
                : (coordinate >= 0 ? "E" : "W");
            
            coordinate = Math.Abs(coordinate);
            var degrees = (int)coordinate;
            var minutes = (int)((coordinate - degrees) * 60);
            var seconds = (coordinate - degrees - minutes / 60.0) * 3600;

            return $"{degrees}° {minutes}' {seconds:F2}\" {direction}";
        }

        /// <summary>
        /// Formats coordinate in decimal degrees
        /// </summary>
        public static string FormatCoordinateDD(double coordinate, int decimals = 6)
        {
            return coordinate.ToString($"F{decimals}", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Formats battery percentage with color indicator
        /// </summary>
        public static (string Text, string Color) FormatBattery(double percentage)
        {
            var text = $"{percentage:F0}%";
            var color = percentage switch
            {
                > 50 => "Green",
                > 25 => "Yellow",
                _ => "Red"
            };
            return (text, color);
        }

        /// <summary>
        /// Formats heading in degrees with cardinal direction
        /// </summary>
        public static string FormatHeading(double headingDegrees)
        {
            var cardinal = GetCardinalDirection(headingDegrees);
            return $"{headingDegrees:F0}° {cardinal}";
        }

        private static string GetCardinalDirection(double degrees)
        {
            var directions = new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW", "N" };
            var index = (int)((degrees + 22.5) / 45.0) % 8;
            return directions[index];
        }

        /// <summary>
        /// Formats flight time in hours:minutes:seconds
        /// </summary>
        public static string FormatFlightTime(TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
            {
                return $"{duration.Hours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
            }
            return $"{duration.Minutes:D2}:{duration.Seconds:D2}";
        }

        /// <summary>
        /// Formats GPS fix type
        /// </summary>
        public static string FormatGpsFixType(int fixType)
        {
            return fixType switch
            {
                0 => "No GPS",
                1 => "No Fix",
                2 => "2D Fix",
                3 => "3D Fix",
                4 => "DGPS",
                5 => "RTK Float",
                6 => "RTK Fixed",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Formats number of satellites
        /// </summary>
        public static string FormatSatellites(int count)
        {
            return $"{count} sats";
        }

        /// <summary>
        /// Formats signal strength as percentage
        /// </summary>
        public static string FormatSignalStrength(int rssi)
        {
            // RSSI typically -120 to 0 dBm
            var percentage = Math.Max(0, Math.Min(100, (rssi + 120) * 100 / 120));
            return $"{percentage}%";
        }
    }
}
