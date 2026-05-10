using Pigeon_Uno.Core.Models;
using System.Threading.Tasks;

namespace Pigeon_Uno.Core.Services;

/// <summary>
/// Interface untuk service yang mengelola geofence (batas geografis virtual)
/// </summary>
public interface IGeofenceService
{
    /// <summary>
    /// Data geofence saat ini
    /// </summary>
    GeofenceData CurrentGeofence { get; }

    /// <summary>
    /// Mengaktifkan atau menonaktifkan geofence
    /// </summary>
    /// <param name="isActive">True untuk mengaktifkan, false untuk menonaktifkan</param>
    void SetGeofenceActive(bool isActive);

    /// <summary>
    /// Mengatur pusat geofence circular
    /// </summary>
    /// <param name="latitude">Latitude pusat</param>
    /// <param name="longitude">Longitude pusat</param>
    void SetGeofenceCenter(double latitude, double longitude);

    /// <summary>
    /// Mengatur radius geofence circular
    /// </summary>
    /// <param name="radius">Radius dalam meter</param>
    void SetGeofenceRadius(double radius);

    /// <summary>
    /// Mengatur altitude maksimum geofence
    /// </summary>
    /// <param name="maxAltitude">Altitude maksimum dalam meter</param>
    void SetMaxAltitude(double maxAltitude);

    /// <summary>
    /// Mengatur tipe geofence
    /// </summary>
    /// <param name="type">Tipe geofence (Circular atau Polygon)</param>
    void SetGeofenceType(GeofenceType type);

    /// <summary>
    /// Menambahkan vertex ke geofence polygon
    /// </summary>
    /// <param name="latitude">Latitude vertex</param>
    /// <param name="longitude">Longitude vertex</param>
    void AddPolygonVertex(double latitude, double longitude);

    /// <summary>
    /// Menghapus semua vertex geofence polygon
    /// </summary>
    void ClearPolygonVertices();

    /// <summary>
    /// Menyelesaikan drawing geofence polygon
    /// </summary>
    void CompletePolygon();

    /// <summary>
    /// Menghitung jarak dari posisi ke batas geofence terdekat
    /// </summary>
    /// <param name="latitude">Latitude posisi</param>
    /// <param name="longitude">Longitude posisi</param>
    /// <param name="altitude">Altitude posisi dalam meter</param>
    /// <returns>Jarak ke batas geofence dalam meter (negatif jika di luar geofence)</returns>
    double CalculateDistanceToBoundary(double latitude, double longitude, double altitude);

    /// <summary>
    /// Mengecek apakah posisi berada di dalam geofence
    /// </summary>
    /// <param name="latitude">Latitude posisi</param>
    /// <param name="longitude">Longitude posisi</param>
    /// <param name="altitude">Altitude posisi dalam meter</param>
    /// <returns>True jika di dalam geofence, false jika di luar</returns>
    bool IsInsideGeofence(double latitude, double longitude, double altitude);

    /// <summary>
    /// Menyimpan parameter geofence ke settings
    /// </summary>
    Task SaveGeofenceParametersAsync();

    /// <summary>
    /// Memuat parameter geofence dari settings
    /// </summary>
    Task LoadGeofenceParametersAsync();

    /// <summary>
    /// Mengirim geofence ke wahana via MAVLink
    /// </summary>
    Task SendGeofenceToVehicleAsync();

    /// <summary>
    /// Mendapatkan daftar geofence yang aktif
    /// </summary>
    /// <returns>List of active geofences</returns>
    Task<List<GeofenceData>> GetActiveGeofencesAsync();
}
