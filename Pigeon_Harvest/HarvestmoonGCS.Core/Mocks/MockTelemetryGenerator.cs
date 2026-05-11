using System;
using HarvestmoonGCS.Core.Models;

namespace HarvestmoonGCS.Mocks
{
    /// <summary>
    /// Generates realistic mock telemetry data for testing avionics controls
    /// </summary>
    public class MockTelemetryGenerator
    {
        private readonly Random _random = new Random();
        private double _time = 0;
        private double _altitude = 100;
        private double _heading = 0;
        private double _roll = 0;
        private double _pitch = 0;
        private bool _ascending = true;

        public TelemetryData GenerateRealisticData()
        {
            _time += 0.1; // 100ms increment

            // Simulate realistic flight patterns
            // Altitude: slowly climb/descend
            if (_ascending)
            {
                _altitude += 0.5;
                if (_altitude > 500) _ascending = false;
            }
            else
            {
                _altitude -= 0.3;
                if (_altitude < 50) _ascending = true;
            }

            // Heading: slowly rotate
            _heading += 0.5;
            if (_heading >= 360) _heading -= 360;

            // Roll: oscillate between -30 and +30 degrees
            _roll = 25 * Math.Sin(_time * 0.5);

            // Pitch: oscillate between -15 and +15 degrees
            _pitch = 12 * Math.Sin(_time * 0.3);

            // Vertical speed based on altitude change
            var verticalSpeed = _ascending ? 2.5 : -1.5;

            // Airspeed: vary between 15-25 m/s
            var airspeed = 20 + 5 * Math.Sin(_time * 0.2);

            return new TelemetryData
            {
                Timestamp = DateTime.Now,
                Latitude = -7.2754 + (_time * 0.0001),
                Longitude = 112.7947 + (_time * 0.0001),
                Altitude = _altitude,
                RelativeAltitude = _altitude,
                Barometers = _altitude + _random.NextDouble() * 2 - 1, // Add small noise
                Roll = _roll,
                Pitch = _pitch,
                Yaw = _heading,
                Heading = _heading,
                GroundSpeed = airspeed * 0.9, // Ground speed slightly less than airspeed
                AirSpeed = airspeed,
                VerticalSpeed = verticalSpeed,
                BatteryVoltage = 12.6 - (_time * 0.001), // Slowly decrease
                BatteryCurrent = 15.0 + _random.NextDouble() * 5,
                BatteryRemaining = Math.Max(0, (int)(100 - (_time * 0.1))),
                SatelliteCount = 12 + _random.Next(-2, 3),
                HDOP = 0.8 + _random.NextDouble() * 0.4,
                GPSFixType = 3, // 3D Fix
                FlightMode = FlightMode.MANUAL, // Changed from GUIDED to MANUAL
                IsArmed = true,
                SignalStrength = 85 + _random.Next(-10, 10),
                ThrottlePercent = 50 + (int)(20 * Math.Sin(_time * 0.4))
            };
        }

        public void Reset()
        {
            _time = 0;
            _altitude = 100;
            _heading = 0;
            _roll = 0;
            _pitch = 0;
            _ascending = true;
        }
    }
}
