namespace HarvestmoonGCS.Core.Models;

/// <summary>
/// Tracks the progress of a tile download operation
/// Used for progress reporting to UI
/// </summary>
public class DownloadProgress
{
    /// <summary>
    /// Total number of tiles to download
    /// </summary>
    public int TotalTiles { get; set; }

    /// <summary>
    /// Number of tiles successfully downloaded
    /// </summary>
    public int DownloadedTiles { get; set; }

    /// <summary>
    /// Number of tiles that failed to download
    /// </summary>
    public int FailedTiles { get; set; }

    /// <summary>
    /// Name of the region being downloaded
    /// </summary>
    public string CurrentRegion { get; set; } = string.Empty;

    /// <summary>
    /// Current zoom level being processed
    /// </summary>
    public int CurrentZoomLevel { get; set; }

    /// <summary>
    /// Whether the download is complete
    /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    /// Whether the download was cancelled
    /// </summary>
    public bool IsCancelled { get; set; }

    /// <summary>
    /// Error message if download failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Download progress percentage (0-100)
    /// </summary>
    public double ProgressPercentage => TotalTiles > 0 
        ? (DownloadedTiles * 100.0) / TotalTiles 
        : 0;

    /// <summary>
    /// Number of tiles remaining
    /// </summary>
    public int RemainingTiles => TotalTiles - DownloadedTiles - FailedTiles;

    /// <summary>
    /// Average download speed in tiles per second
    /// </summary>
    public double TilesPerSecond { get; set; }

    /// <summary>
    /// Estimated time remaining in seconds
    /// </summary>
    public double? EstimatedTimeRemaining => TilesPerSecond > 0 
        ? RemainingTiles / TilesPerSecond 
        : null;

    /// <summary>
    /// Timestamp when download started
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when download completed or failed
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Duration of download so far
    /// </summary>
    public TimeSpan? Duration => EndTime.HasValue 
        ? EndTime.Value - StartTime 
        : DateTime.UtcNow - StartTime;

    /// <summary>
    /// Formatted progress string for display
    /// </summary>
    public string GetFormattedProgress()
    {
        if (IsComplete)
            return $"Complete: {DownloadedTiles}/{TotalTiles} tiles";
        
        if (IsCancelled)
            return $"Cancelled: {DownloadedTiles}/{TotalTiles} tiles";
        
        if (!string.IsNullOrEmpty(ErrorMessage))
            return $"Error: {ErrorMessage}";
        
        var eta = EstimatedTimeRemaining.HasValue 
            ? $", ETA: {TimeSpan.FromSeconds(EstimatedTimeRemaining.Value):hh\\:mm\\:ss}"
            : "";
        
        return $"{ProgressPercentage:F1}% ({DownloadedTiles}/{TotalTiles}){eta}";
    }
}
