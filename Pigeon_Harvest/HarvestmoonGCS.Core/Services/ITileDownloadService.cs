using HarvestmoonGCS.Core.Models;

namespace HarvestmoonGCS.Core.Services;

/// <summary>
/// Service for downloading map tiles for offline storage
/// Handles background downloads with progress reporting
/// </summary>
public interface ITileDownloadService
{
    /// <summary>
    /// Downloads all tiles for a geographic region
    /// </summary>
    /// <param name="region">Region to download</param>
    /// <param name="progress">Progress reporter (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>true if download completed, false if cancelled</returns>
    Task<bool> DownloadRegionAsync(
        MapRegion region, 
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels the current download operation
    /// </summary>
    void CancelDownload();

    /// <summary>
    /// Gets current download progress
    /// </summary>
    DownloadProgress? GetDownloadProgress();

    /// <summary>
    /// Checks if a download is currently in progress
    /// </summary>
    bool IsDownloading { get; }

    /// <summary>
    /// Event raised when download starts
    /// </summary>
    event EventHandler<MapRegion> DownloadStarted;

    /// <summary>
    /// Event raised when download completes successfully
    /// </summary>
    event EventHandler<MapRegion> DownloadCompleted;

    /// <summary>
    /// Event raised when download is cancelled
    /// </summary>
    event EventHandler<MapRegion> DownloadCancelled;

    /// <summary>
    /// Event raised when download fails
    /// </summary>
    event EventHandler<(MapRegion Region, string Error)> DownloadFailed;

    /// <summary>
    /// Event raised when download progress updates
    /// </summary>
    event EventHandler<DownloadProgress> DownloadProgressChanged;
}
