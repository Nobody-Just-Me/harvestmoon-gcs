using System;

namespace HarvestmoonGCS.Helpers;

/// <summary>
/// Kelas helper untuk perhitungan geografis menggunakan model bumi spherical.
/// Menyediakan fungsi untuk menghitung jarak, bearing, azimuth/elevation, dan koordinat tujuan.
/// </summary>
public static class GeoMath
{
    /// <summary>
    /// Radius bumi dalam meter (model spherical)
    /// </summary>
    private const double EarthRadius = 6371000; // meters

    /// <summary>
    /// Mengkonversi derajat ke radian
    /// </summary>
    /// <param name="degrees">Nilai dalam derajat</param>
    /// <returns>Nilai dalam radian</returns>
    public static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    /// <summary>
    /// Mengkonversi radian ke derajat
    /// </summary>
    /// <param name="radians">Nilai dalam radian</param>
    /// <returns>Nilai dalam derajat</returns>
    public static double ToDegrees(double radians) => radians * 180.0 / Math.PI;

    /// <summary>
    /// Menghitung jarak antara dua koordinat GPS menggunakan formula Haversine.
    /// Formula ini memberikan jarak great-circle antara dua titik di permukaan bumi spherical.
    /// </summary>
    /// <param name="lat1">Latitude titik pertama dalam derajat (-90 hingga 90)</param>
    /// <param name="lon1">Longitude titik pertama dalam derajat (-180 hingga 180)</param>
    /// <param name="lat2">Latitude titik kedua dalam derajat (-90 hingga 90)</param>
    /// <param name="lon2">Longitude titik kedua dalam derajat (-180 hingga 180)</param>
    /// <returns>Jarak dalam meter</returns>
    public static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = ToRadians(lat2 - lat1);
        double dLon = ToRadians(lon2 - lon1);
        
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
                   
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadius * c;
    }

    /// <summary>
    /// Menghitung bearing (arah/heading) dari titik pertama ke titik kedua.
    /// Bearing adalah sudut searah jarum jam dari utara geografis.
    /// </summary>
    /// <param name="lat1">Latitude titik pertama dalam derajat (-90 hingga 90)</param>
    /// <param name="lon1">Longitude titik pertama dalam derajat (-180 hingga 180)</param>
    /// <param name="lat2">Latitude titik kedua dalam derajat (-90 hingga 90)</param>
    /// <param name="lon2">Longitude titik kedua dalam derajat (-180 hingga 180)</param>
    /// <returns>Bearing dalam derajat (0 hingga 360), dimana 0° = Utara, 90° = Timur, 180° = Selatan, 270° = Barat</returns>
    public static double CalculateBearing(double lat1, double lon1, double lat2, double lon2)
    {
        double dLon = ToRadians(lon2 - lon1);
        double y = Math.Sin(dLon) * Math.Cos(ToRadians(lat2));
        double x = Math.Cos(ToRadians(lat1)) * Math.Sin(ToRadians(lat2)) -
                   Math.Sin(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Cos(dLon);
                   
        double brng = Math.Atan2(y, x);
        return (ToDegrees(brng) + 360) % 360;
    }

    /// <summary>
    /// Menghitung azimuth dan elevation untuk antenna tracker.
    /// Azimuth adalah sudut horizontal dari utara, elevation adalah sudut vertikal dari horizon.
    /// Digunakan untuk mengarahkan antenna tracker ke wahana udara.
    /// </summary>
    /// <param name="vehicleLat">Latitude wahana dalam derajat (-90 hingga 90)</param>
    /// <param name="vehicleLon">Longitude wahana dalam derajat (-180 hingga 180)</param>
    /// <param name="vehicleAlt">Altitude wahana dalam meter (MSL - Mean Sea Level)</param>
    /// <param name="trackerLat">Latitude tracker dalam derajat (-90 hingga 90)</param>
    /// <param name="trackerLon">Longitude tracker dalam derajat (-180 hingga 180)</param>
    /// <param name="trackerAlt">Altitude tracker dalam meter (MSL - Mean Sea Level)</param>
    /// <returns>Tuple berisi (azimuth dalam derajat 0-360, elevation dalam derajat -90 hingga 90)</returns>
    public static (double azimuth, double elevation) CalculateAzimuthElevation(
        double vehicleLat, double vehicleLon, double vehicleAlt,
        double trackerLat, double trackerLon, double trackerAlt)
    {
        // Hitung jarak horizontal antara tracker dan wahana
        double distance = CalculateDistance(trackerLat, trackerLon, vehicleLat, vehicleLon);
        
        // Hitung azimuth (bearing dari tracker ke wahana)
        double azimuth = CalculateBearing(trackerLat, trackerLon, vehicleLat, vehicleLon);
        
        // Hitung elevation (sudut vertikal)
        // Menggunakan perbedaan altitude dan jarak horizontal
        double altitudeDifference = vehicleAlt - trackerAlt;
        double elevation = ToDegrees(Math.Atan2(altitudeDifference, distance));
        
        return (azimuth, elevation);
    }

    /// <summary>
    /// Menghitung koordinat tujuan berdasarkan titik awal, bearing, dan jarak.
    /// Berguna untuk menghitung posisi waypoint atau prediksi posisi wahana.
    /// </summary>
    /// <param name="lat">Latitude titik awal dalam derajat (-90 hingga 90)</param>
    /// <param name="lon">Longitude titik awal dalam derajat (-180 hingga 180)</param>
    /// <param name="bearing">Bearing dalam derajat (0 hingga 360), dimana 0° = Utara</param>
    /// <param name="distance">Jarak dalam meter</param>
    /// <returns>Tuple berisi (latitude tujuan, longitude tujuan) dalam derajat</returns>
    public static (double lat, double lon) CalculateDestination(double lat, double lon, double bearing, double distance)
    {
        double latRad = ToRadians(lat);
        double lonRad = ToRadians(lon);
        double bearingRad = ToRadians(bearing);
        double angularDistance = distance / EarthRadius;

        double lat2 = Math.Asin(Math.Sin(latRad) * Math.Cos(angularDistance) +
                               Math.Cos(latRad) * Math.Sin(angularDistance) * Math.Cos(bearingRad));

        double lon2 = lonRad + Math.Atan2(Math.Sin(bearingRad) * Math.Sin(angularDistance) * Math.Cos(latRad),
                                         Math.Cos(angularDistance) - Math.Sin(latRad) * Math.Sin(lat2));

        return (ToDegrees(lat2), ToDegrees(lon2));
    }

    // ========== Legacy methods untuk kompatibilitas dengan kode lama ==========

    /// <summary>
    /// Method legacy untuk menghitung jarak. Gunakan CalculateDistance() untuk kode baru.
    /// </summary>
    public static double Distance(double lat1, double lon1, double lat2, double lon2) => 
        CalculateDistance(lat1, lon1, lat2, lon2);
    
    /// <summary>
    /// Method legacy untuk menghitung bearing. Gunakan CalculateBearing() untuk kode baru.
    /// </summary>
    public static double Bearing(double lat1, double lon1, double lat2, double lon2) => 
        CalculateBearing(lat1, lon1, lat2, lon2);

    /// <summary>
    /// Menghitung pitch (sudut elevasi) berdasarkan jarak horizontal dan perbedaan altitude.
    /// Method legacy untuk kompatibilitas.
    /// </summary>
    /// <param name="dist">Jarak horizontal dalam meter</param>
    /// <param name="alt1">Altitude titik pertama dalam meter</param>
    /// <param name="alt2">Altitude titik kedua dalam meter</param>
    /// <returns>Pitch dalam derajat</returns>
    public static double Pitch(double dist, double alt1, double alt2)
    {
        if (dist < 0.1) return 0;
        double dAlt = alt2 - alt1;
        return ToDegrees(Math.Atan2(dAlt, dist));
    }
}
