using System;
using Pigeon_Uno.Core.Helpers;

namespace Pigeon_Uno.Core.Models
{
    public class GeoCoordinate
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }

        public GeoCoordinate()
        {
            Latitude = 0;
            Longitude = 0;
            Altitude = 0;
        }

        public GeoCoordinate(double latitude, double longitude)
        {
            Latitude = latitude;
            Longitude = longitude;
            Altitude = 0;
        }

        public GeoCoordinate(double latitude, double longitude, double altitude)
        {
            Latitude = latitude;
            Longitude = longitude;
            Altitude = altitude;
        }

        public double DistanceTo(GeoCoordinate other)
        {
            return GeometryHelper.CalculateDistance(this, other);
        }

        public double BearingTo(GeoCoordinate other)
        {
            return GeometryHelper.CalculateBearing(this, other);
        }

        public bool IsValid()
        {
            return Latitude >= -90 && Latitude <= 90 &&
                   Longitude >= -180 && Longitude <= 180;
        }

        public override string ToString()
        {
            return $"Lat: {Latitude:F6}, Lon: {Longitude:F6}, Alt: {Altitude:F2}m";
        }

        public override bool Equals(object? obj)
        {
            if (obj is GeoCoordinate other)
            {
                return Math.Abs(Latitude - other.Latitude) < 0.000001 &&
                       Math.Abs(Longitude - other.Longitude) < 0.000001 &&
                       Math.Abs(Altitude - other.Altitude) < 0.01;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Latitude.GetHashCode() ^ Longitude.GetHashCode() ^ Altitude.GetHashCode();
        }
    }
}
