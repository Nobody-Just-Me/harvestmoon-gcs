namespace Pigeon_Uno.Core.Models;

/// <summary>
/// Represents a single map tile with its data and coordinates
/// Used for storage and retrieval from offline database
/// </summary>
public class TileData
{
    /// <summary>
    /// Zoom level (0-18+)
    /// </summary>
    public int Z { get; set; }

    /// <summary>
    /// X coordinate (column) in tile grid
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Y coordinate (row) in tile grid
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// Raw tile image data (PNG/JPEG bytes)
    /// </summary>
    public byte[] ImageData { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Timestamp when tile was downloaded
    /// </summary>
    public DateTime DownloadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// ETag from HTTP response for cache validation
    /// </summary>
    public string? ETag { get; set; }

    /// <summary>
    /// Content type (image/png, image/jpeg)
    /// </summary>
    public string ContentType { get; set; } = "image/png";

    /// <summary>
    /// Size of tile data in bytes
    /// </summary>
    public int Size => ImageData?.Length ?? 0;

    /// <summary>
    /// Creates a unique key for this tile
    /// Format: {z}/{x}/{y}
    /// </summary>
    public string GetKey()
    {
        return $"{Z}/{X}/{Y}";
    }

    /// <summary>
    /// Converts TMS Y coordinate to Google/OSM Y coordinate
    /// TMS uses flipped Y compared to XYZ scheme
    /// </summary>
    public int ToTmsY()
    {
        return (int)Math.Pow(2, Z) - 1 - Y;
    }

    /// <summary>
    /// Converts Google/OSM Y coordinate to TMS Y coordinate
    /// </summary>
    public static int FromTmsY(int tmsY, int zoom)
    {
        return (int)Math.Pow(2, zoom) - 1 - tmsY;
    }
}
