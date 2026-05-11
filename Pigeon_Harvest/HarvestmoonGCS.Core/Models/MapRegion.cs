using System.ComponentModel.DataAnnotations;

namespace HarvestmoonGCS.Core.Models;

/// <summary>
/// Represents a geographic region for offline map storage
/// Contains bounding coordinates, zoom levels, and download status
/// </summary>
public class MapRegion
{
    /// <summary>
    /// Unique identifier for the region (e.g., "jawa", "kalimantan")
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Display name for UI (e.g., "Pulau Jawa", "Kalimantan")
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Description of the region
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Minimum longitude (West boundary)
    /// </summary>
    public double MinLongitude { get; set; }

    /// <summary>
    /// Minimum latitude (South boundary)
    /// </summary>
    public double MinLatitude { get; set; }

    /// <summary>
    /// Maximum longitude (East boundary)
    /// </summary>
    public double MaxLongitude { get; set; }

    /// <summary>
    /// Maximum latitude (North boundary)
    /// </summary>
    public double MaxLatitude { get; set; }

    /// <summary>
    /// Minimum zoom level for offline storage (typically 0-4 for overview)
    /// </summary>
    public int MinZoomLevel { get; set; } = 12;

    /// <summary>
    /// Maximum zoom level for offline storage (typically 15-18 for detail)
    /// </summary>
    public int MaxZoomLevel { get; set; } = 15;

    /// <summary>
    /// Whether this region has been fully downloaded
    /// </summary>
    public bool IsDownloaded { get; set; }

    /// <summary>
    /// Estimated storage size in bytes
    /// </summary>
    public long EstimatedStorageSize { get; set; }

    /// <summary>
    /// Actual storage size in bytes after download
    /// </summary>
    public long ActualStorageSize { get; set; }

    /// <summary>
    /// Number of tiles in this region
    /// </summary>
    public int TileCount { get; set; }

    /// <summary>
    /// Date when region was downloaded
    /// </summary>
    public DateTime? DownloadDate { get; set; }

    /// <summary>
    /// Returns the bounds as an array [minLon, minLat, maxLon, maxLat]
    /// </summary>
    public double[] Bounds => new[] { MinLongitude, MinLatitude, MaxLongitude, MaxLatitude };

    /// <summary>
    /// Predefined region for Jawa (Java) Island
    /// Bounds: West 105.0, South -8.5, East 115.5, North -5.0
    /// </summary>
    public static MapRegion Jawa => new()
    {
        Name = "jawa",
        DisplayName = "Pulau Jawa",
        Description = "Pulau Jawa - Cilegon sampai Banyuwangi",
        MinLongitude = 105.0,
        MinLatitude = -8.5,
        MaxLongitude = 115.5,
        MaxLatitude = -5.0,
        MinZoomLevel = 12,
        MaxZoomLevel = 15,
        EstimatedStorageSize = 400 * 1024 * 1024 // ~400 MB
    };

    /// <summary>
    /// Predefined region for Kalimantan (Indonesian Borneo)
    /// Bounds: West 108.5, South -4.5, East 119.0, North 2.0
    /// </summary>
    public static MapRegion Kalimantan => new()
    {
        Name = "kalimantan",
        DisplayName = "Kalimantan",
        Description = "Kalimantan (Borneo Indonesia)",
        MinLongitude = 108.5,
        MinLatitude = -4.5,
        MaxLongitude = 119.0,
        MaxLatitude = 2.0,
        MinZoomLevel = 12,
        MaxZoomLevel = 15,
        EstimatedStorageSize = 600 * 1024 * 1024 // ~600 MB
    };

    /// <summary>
    /// Calculates the approximate number of tiles for this region
    /// </summary>
    public int CalculateTileCount()
    {
        int count = 0;
        for (int z = MinZoomLevel; z <= MaxZoomLevel; z++)
        {
            // Calculate tile coordinates for this zoom level
            var minTile = LatLonToTile(MinLatitude, MinLongitude, z);
            var maxTile = LatLonToTile(MaxLatitude, MaxLongitude, z);
            
            // Count tiles in this zoom level
            int tilesX = Math.Abs(maxTile.X - minTile.X) + 1;
            int tilesY = Math.Abs(maxTile.Y - minTile.Y) + 1;
            count += tilesX * tilesY;
        }
        return count;
    }

    /// <summary>
    /// Converts latitude/longitude to tile coordinates
    /// </summary>
    private static (int X, int Y) LatLonToTile(double lat, double lon, int zoom)
    {
        int n = 1 << zoom;
        int x = (int)((lon + 180.0) / 360.0 * n);
        double latRad = lat * Math.PI / 180.0;
        int y = (int)((1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n);
        return (x, y);
    }
}
