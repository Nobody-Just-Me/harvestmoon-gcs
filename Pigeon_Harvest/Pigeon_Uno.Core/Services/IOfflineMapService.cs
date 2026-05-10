namespace Pigeon_Uno.Core.Services;

/// <summary>
/// Service for managing offline map tiles and mode switching
/// Provides tile retrieval from local storage with fallback to online
/// </summary>
public interface IOfflineMapService
{
    /// <summary>
    /// Gets whether offline mode is currently enabled
    /// When true, map will prioritize offline tiles; when false, uses online
    /// </summary>
    bool IsOfflineMode { get; }

    /// <summary>
    /// Toggles between offline and online mode
    /// </summary>
    void ToggleOfflineMode();

    /// <summary>
    /// Sets offline mode explicitly
    /// </summary>
    /// <param name="isOffline">true for offline mode, false for online</param>
    void SetOfflineMode(bool isOffline);

    /// <summary>
    /// Retrieves a tile from offline storage
    /// </summary>
    /// <param name="z">Zoom level (0-18+)</param>
    /// <param name="x">Tile X coordinate</param>
    /// <param name="y">Tile Y coordinate</param>
    /// <returns>Tile image data or null if not found</returns>
    Task<byte[]?> GetTileAsync(int z, int x, int y);

    /// <summary>
    /// Gets the total storage size of offline tiles in bytes
    /// </summary>
    /// <returns>Size in bytes</returns>
    Task<long> GetStorageSizeAsync();

    /// <summary>
    /// Gets the formatted storage size string (e.g., "512 MB")
    /// </summary>
    Task<string> GetStorageSizeFormattedAsync();

    /// <summary>
    /// Clears all offline tiles from storage
    /// </summary>
    /// <returns>Number of tiles removed</returns>
    Task<int> ClearCacheAsync();

    /// <summary>
    /// Checks if a specific tile exists in offline storage
    /// </summary>
    Task<bool> HasTileAsync(int z, int x, int y);

    /// <summary>
    /// Gets count of available offline tiles
    /// </summary>
    Task<int> GetTileCountAsync();

    /// <summary>
    /// Event raised when offline mode changes
    /// </summary>
    event EventHandler<bool> OfflineModeChanged;
}
