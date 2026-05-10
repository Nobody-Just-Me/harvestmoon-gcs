using System;
using System.Collections.Generic;
using System.Linq;
using Pigeon_Uno.Core.Models;

namespace Pigeon_Uno.Core.Helpers
{
    public static class GeometryHelper
    {
        public static bool IsPointInPolygon(GeoCoordinate point, List<GeoCoordinate> vertices)
        {
            if (vertices == null || vertices.Count < 3)
                return false;

            bool inside = false;
            int j = vertices.Count - 1;

            for (int i = 0; i < vertices.Count; i++)
            {
                if (((vertices[i].Longitude > point.Longitude) != (vertices[j].Longitude > point.Longitude)) &&
                    (point.Latitude < (vertices[j].Latitude - vertices[i].Latitude) * 
                     (point.Longitude - vertices[i].Longitude) / 
                     (vertices[j].Longitude - vertices[i].Longitude) + vertices[i].Latitude))
                {
                    inside = !inside;
                }
                j = i;
            }

            return inside;
        }

        public static double CalculateDistance(GeoCoordinate point1, GeoCoordinate point2)
        {
            const double EarthRadiusKm = 6371.0;

            double lat1Rad = DegreesToRadians(point1.Latitude);
            double lat2Rad = DegreesToRadians(point2.Latitude);
            double deltaLat = DegreesToRadians(point2.Latitude - point1.Latitude);
            double deltaLon = DegreesToRadians(point2.Longitude - point1.Longitude);

            double a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                      Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                      Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return EarthRadiusKm * c * 1000; // Convert to meters
        }

        public static double CalculateBearing(GeoCoordinate from, GeoCoordinate to)
        {
            double lat1 = DegreesToRadians(from.Latitude);
            double lat2 = DegreesToRadians(to.Latitude);
            double deltaLon = DegreesToRadians(to.Longitude - from.Longitude);

            double y = Math.Sin(deltaLon) * Math.Cos(lat2);
            double x = Math.Cos(lat1) * Math.Sin(lat2) -
                      Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(deltaLon);

            double bearing = Math.Atan2(y, x);
            return (RadiansToDegrees(bearing) + 360) % 360;
        }

        public static double CalculatePolygonArea(List<GeoCoordinate> vertices)
        {
            if (vertices == null || vertices.Count < 3)
                return 0;

            double area = 0;
            int j = vertices.Count - 1;

            for (int i = 0; i < vertices.Count; i++)
            {
                area += (vertices[j].Longitude + vertices[i].Longitude) * 
                       (vertices[j].Latitude - vertices[i].Latitude);
                j = i;
            }

            return Math.Abs(area / 2.0);
        }

        public static GeoCoordinate CalculateCenter(List<GeoCoordinate> points)
        {
            if (points == null || points.Count == 0)
                return new GeoCoordinate(0, 0);

            double totalLat = points.Sum(p => p.Latitude);
            double totalLon = points.Sum(p => p.Longitude);

            return new GeoCoordinate(
                totalLat / points.Count,
                totalLon / points.Count
            );
        }

        /// <summary>
        /// Calculates the shortest distance from a point to a line segment
        /// </summary>
        /// <param name="point">The point</param>
        /// <param name="lineStart">Start of the line segment</param>
        /// <param name="lineEnd">End of the line segment</param>
        /// <returns>Distance in meters</returns>
        public static double DistanceToLineSegment(GeoCoordinate point, GeoCoordinate lineStart, GeoCoordinate lineEnd)
        {
            // Convert to Cartesian coordinates for easier calculation
            double x = point.Longitude;
            double y = point.Latitude;
            double x1 = lineStart.Longitude;
            double y1 = lineStart.Latitude;
            double x2 = lineEnd.Longitude;
            double y2 = lineEnd.Latitude;

            double A = x - x1;
            double B = y - y1;
            double C = x2 - x1;
            double D = y2 - y1;

            double dot = A * C + B * D;
            double lenSq = C * C + D * D;
            double param = -1;

            if (lenSq != 0) // Line segment is not a point
                param = dot / lenSq;

            double xx, yy;

            if (param < 0)
            {
                xx = x1;
                yy = y1;
            }
            else if (param > 1)
            {
                xx = x2;
                yy = y2;
            }
            else
            {
                xx = x1 + param * C;
                yy = y1 + param * D;
            }

            // Convert back to GeoCoordinate and calculate distance
            var closestPoint = new GeoCoordinate(yy, xx);
            return CalculateDistance(point, closestPoint);
        }

        private static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        private static double RadiansToDegrees(double radians)
        {
            return radians * 180.0 / Math.PI;
        }
    }
}
