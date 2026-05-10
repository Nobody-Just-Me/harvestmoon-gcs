using Pigeon_Uno.Core.Models;
using System.Collections.Concurrent;

namespace Pigeon_Uno.Core.Services;

/// <summary>
/// Implementation of offline map service using MBTiles database
/// Provides tile retrieval with fallback to online mode
/// </summary>
public class OfflineMapService : IOfflineMapService, IDisposable
{
    private readonly MBTilesDatabase _database;
    private readonly ILoggingService? _logger;
    private bool _isOfflineMode;
    private readonly ConcurrentDictionary<string, byte[]> _memoryCache;
    private const int MaxMemoryCacheSize = 100; // Max tiles in memory cache

    /// <summary>
    /// Event raised when offline mode changes
    /// </summary>
    public event EventHandler<bool>? OfflineModeChanged;

    /// <summary>
    /// Creates a new offline map service instance
    /// </summary>
    public OfflineMapService(ILoggingService? logger = null)
    {
        _logger = logger;
        _database = new MBTilesDatabase(logger);
        _memoryCache = new ConcurrentDictionary<string, byte[]>();
        
        _logger?.LogInfo("OfflineMapService initialized", nameof(OfflineMapService));
    }

    /// <inheritdoc/>
    public bool IsOfflineMode => _isOfflineMode;

    /// <inheritdoc/>
    public void ToggleOfflineMode()
    {
        SetOfflineMode(!_isOfflineMode);
    }

    /// <inheritdoc/>
    public void SetOfflineMode(bool isOffline)
    {
        if (_isOfflineMode != isOffline)
        {
            _isOfflineMode = isOffline;
            _logger?.LogInfo($"Offline mode changed to: {isOffline}", nameof(OfflineMapService));
            OfflineModeChanged?.Invoke(this, isOffline);
        }
    }

    /// <inheritdoc/>
    public async Task<byte[]?> GetTileAsync(int z, int x, int y)
    {
        var key = $"{z}/{x}/{y}";

        // Check memory cache first (fastest)
        if (_memoryCache.TryGetValue(key, out var cachedTile))
        {
            return cachedTile;
        }

        // Check database
        var tile = await _database.GetTileAsync(z, x, y);
        
        if (tile != null)
        {
            // Add to memory cache (LRU eviction handled by ConcurrentDictionary)
            AddToMemoryCache(key, tile);
        }

        return tile;
    }

    private void AddToMemoryCache(string key, byte[] tileData)
    {
        // Simple LRU: if cache is full, clear half of it
        if (_memoryCache.Count >= MaxMemoryCacheSize)
        {
            var keysToRemove = _memoryCache.Keys.Take(MaxMemoryCacheSize / 2).ToList();
            foreach (var oldKey in keysToRemove)
            {
                _memoryCache.TryRemove(oldKey, out _);
            }
        }

        _memoryCache[key] = tileData;
    }

    /// <inheritdoc/>
    public Task<bool> HasTileAsync(int z, int x, int y)
    {
        return _database.HasTileAsync(z, x, y);
    }

    /// <inheritdoc/>
    public Task<int> GetTileCountAsync()
    {
        return _database.GetTileCountAsync();
    }

    /// <inheritdoc/>
    public Task<long> GetStorageSizeAsync()
    {
        return Task.FromResult(_database.GetStorageSize());
    }

    /// <inheritdoc/>
    public Task<string> GetStorageSizeFormattedAsync()
    {
        return Task.FromResult(_database.GetStorageSizeFormatted());
    }

    /// <inheritdoc/>
    public async Task<int> ClearCacheAsync()
    {
        _memoryCache.Clear();
        var count = await _database.ClearAllTilesAsync();
        _logger?.LogInfo($"Cleared {count} tiles from cache", nameof(OfflineMapService));
        return count;
    }

    /// <summary>
    /// Preloads tiles into memory cache for faster access
    /// </summary>
    public async Task PreloadTilesAsync(IEnumerable<(int z, int x, int y)> coordinates)
    {
        foreach (var (z, x, y) in coordinates)
        {
            var key = $"{z}/{x}/{y}";
            if (!_memoryCache.ContainsKey(key))
            {
                var tile = await _database.GetTileAsync(z, x, y);
                if (tile != null)
                {
                    AddToMemoryCache(key, tile);
                }
            }
        }
    }

    /// <summary>
    /// Gets the database instance for direct access
    /// </summary>
    public MBTilesDatabase Database => _database;

    public void Dispose()
    {
        _database.Dispose();
        _memoryCache.Clear();
    }
}
