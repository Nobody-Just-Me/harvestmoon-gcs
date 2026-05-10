namespace Pigeon_Uno.Core.Models;

/// <summary>
/// Configuration settings for offline map functionality
/// </summary>
public class OfflineMapSettings
{
    /// <summary>
    /// Minimum zoom level for offline storage (typically 0-4 for overview)
    /// Default: 12 (city level)
    /// </summary>
    public int DefaultZoomMin { get; set; } = 12;

    /// <summary>
    /// Maximum zoom level for offline storage (typically 15-18 for detail)
    /// Default: 15 (street level)
    /// </summary>
    public int DefaultZoomMax { get; set; } = 15;

    /// <summary>
    /// Maximum concurrent tile downloads (to avoid overwhelming server)
    /// Default: 4
    /// </summary>
    public int MaxConcurrentDownloads { get; set; } = 4;

    /// <summary>
    /// Number of tiles to insert in one database batch
    /// Default: 100
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Base URL for tile source
    /// Default: OpenStreetMap
    /// </summary>
    public string TileSourceUrl { get; set; } = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";

    /// <summary>
    /// User agent string for HTTP requests
    /// </summary>
    public string UserAgent { get; set; } = "Pigeon_Uno/1.0 Offline Maps";

    /// <summary>
    /// Timeout for tile download requests in seconds
    /// Default: 30 seconds
    /// </summary>
    public int DownloadTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum retry attempts for failed downloads
    /// Default: 3
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Delay between retries in milliseconds
    /// Default: 1000ms
    /// </summary>
    public int RetryDelayMilliseconds { get; set; } = 1000;

    /// <summary>
    /// Maximum storage size in MB before warning user
    /// Default: 2000 MB (2 GB)
    /// </summary>
    public long MaxStorageSizeMB { get; set; } = 2000;

    /// <summary>
    /// Whether to enable automatic tile caching while browsing
    /// Default: true
    /// </summary>
    public bool EnableAutoCache { get; set; } = true;

    /// <summary>
    /// Creates settings with default values
    /// </summary>
    public static OfflineMapSettings Default => new();
}
