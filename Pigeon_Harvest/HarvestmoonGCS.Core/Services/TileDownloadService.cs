using HarvestmoonGCS.Core.Models;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace HarvestmoonGCS.Core.Services;

/// <summary>
/// Background tile download service with concurrent downloads and progress reporting
/// </summary>
public class TileDownloadService : ITileDownloadService, IDisposable
{
    private readonly MBTilesDatabase _database;
    private readonly HttpClient _httpClient;
    private readonly ILoggingService? _logger;
    private readonly OfflineMapSettings _settings;
    private readonly SemaphoreSlim _downloadSemaphore;
    private CancellationTokenSource? _downloadCts;
    private DownloadProgress? _currentProgress;
    private readonly ConcurrentDictionary<string, byte[]> _downloadCache;

    public event EventHandler<MapRegion>? DownloadStarted;
    public event EventHandler<MapRegion>? DownloadCompleted;
    public event EventHandler<MapRegion>? DownloadCancelled;
    public event EventHandler<(MapRegion Region, string Error)>? DownloadFailed;
    public event EventHandler<DownloadProgress>? DownloadProgressChanged;

    public bool IsDownloading { get; private set; }

    public TileDownloadService(
        MBTilesDatabase database,
        OfflineMapSettings settings,
        ILoggingService? logger = null)
    {
        _database = database;
        _settings = settings;
        _logger = logger;
        _downloadCache = new ConcurrentDictionary<string, byte[]>();
        _downloadSemaphore = new SemaphoreSlim(settings.MaxConcurrentDownloads);
        
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", settings.UserAgent);
        _httpClient.Timeout = TimeSpan.FromSeconds(settings.DownloadTimeoutSeconds);
    }

    public async Task<bool> DownloadRegionAsync(
        MapRegion region, 
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (IsDownloading)
        {
            throw new InvalidOperationException("Another download is already in progress");
        }

        IsDownloading = true;
        _downloadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger?.LogInfo($"Starting download for {region.Name}", nameof(TileDownloadService));
            DownloadStarted?.Invoke(this, region);

            // Calculate total tiles
            var totalTiles = CalculateTotalTiles(region);
            
            _currentProgress = new DownloadProgress
            {
                TotalTiles = totalTiles,
                CurrentRegion = region.Name,
                StartTime = DateTime.UtcNow
            };

            var downloadedCount = 0;
            var failedCount = 0;
            var tileTasks = new List<Task>();

            // Generate all tile coordinates for the region
            for (int zoom = region.MinZoomLevel; zoom <= region.MaxZoomLevel; zoom++)
            {
                var tiles = GetTilesForZoomLevel(region, zoom);
                
                foreach (var (x, y) in tiles)
                {
                    if (_downloadCts.Token.IsCancellationRequested)
                    {
                        break;
                    }

                    await _downloadSemaphore.WaitAsync(_downloadCts.Token);
                    
                    var task = DownloadSingleTileAsync(
                        region, zoom, x, y, 
                        () => Interlocked.Increment(ref downloadedCount),
                        () => Interlocked.Increment(ref failedCount),
                        _downloadCts.Token);

                    _ = task.ContinueWith(_ => _downloadSemaphore.Release());
                    tileTasks.Add(task);

                    // Update progress
                    if (downloadedCount % 10 == 0)
                    {
                        UpdateProgress(downloadedCount, failedCount, totalTiles, zoom, stopwatch.Elapsed);
                        progress?.Report(_currentProgress);
                        DownloadProgressChanged?.Invoke(this, _currentProgress);
                    }
                }
            }

            await Task.WhenAll(tileTasks);

            // Final batch insert
            await FlushDownloadCacheAsync();

            stopwatch.Stop();

            if (_downloadCts.Token.IsCancellationRequested)
            {
                _currentProgress!.IsCancelled = true;
                DownloadCancelled?.Invoke(this, region);
                _logger?.LogInfo($"Download cancelled for {region.Name}", nameof(TileDownloadService));
                return false;
            }

            _currentProgress!.IsComplete = true;
            _currentProgress.EndTime = DateTime.UtcNow;
            DownloadCompleted?.Invoke(this, region);
            _logger?.LogInfo($"Download completed for {region.Name}: {downloadedCount} tiles", nameof(TileDownloadService));
            
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Download failed for {region.Name}: {ex.Message}", nameof(TileDownloadService));
            DownloadFailed?.Invoke(this, (region, ex.Message));
            throw;
        }
        finally
        {
            IsDownloading = false;
            _downloadCts?.Dispose();
            _downloadCts = null;
        }
    }

    private async Task DownloadSingleTileAsync(
        MapRegion region,
        int zoom, int x, int y,
        Action onSuccess,
        Action onFailure,
        CancellationToken ct)
    {
        var key = $"{zoom}/{x}/{y}";

        try
        {
            // Check if already in database
            if (await _database.HasTileAsync(zoom, x, y))
            {
                onSuccess();
                return;
            }

            // Download from tile server
            var url = _settings.TileSourceUrl
                .Replace("{z}", zoom.ToString())
                .Replace("{x}", x.ToString())
                .Replace("{y}", y.ToString());

            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var tileData = await response.Content.ReadAsByteArrayAsync();

            // Add to cache for batch insert
            _downloadCache[key] = tileData;

            // Flush cache when it reaches batch size
            if (_downloadCache.Count >= _settings.BatchSize)
            {
                await FlushDownloadCacheAsync();
            }

            onSuccess();
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to download tile {key}: {ex.Message}", nameof(TileDownloadService));
            onFailure();
            
            // Retry logic
            await Task.Delay(_settings.RetryDelayMilliseconds, ct);
        }
    }

    private async Task FlushDownloadCacheAsync()
    {
        if (_downloadCache.IsEmpty) return;

        var tiles = _downloadCache.Select(kvp =>
        {
            var parts = kvp.Key.Split('/');
            return new TileData
            {
                Z = int.Parse(parts[0]),
                X = int.Parse(parts[1]),
                Y = int.Parse(parts[2]),
                ImageData = kvp.Value,
                ContentType = "image/png"
            };
        }).ToList();

        await _database.InsertTilesBatchAsync(tiles);
        _downloadCache.Clear();
        
        _logger?.LogInfo($"Flushed {tiles.Count} tiles to database", nameof(TileDownloadService));
    }

    private void UpdateProgress(int downloaded, int failed, int total, int currentZoom, TimeSpan elapsed)
    {
        if (_currentProgress == null) return;

        _currentProgress.DownloadedTiles = downloaded;
        _currentProgress.FailedTiles = failed;
        _currentProgress.CurrentZoomLevel = currentZoom;
        _currentProgress.TilesPerSecond = downloaded / elapsed.TotalSeconds;
    }

    private int CalculateTotalTiles(MapRegion region)
    {
        int total = 0;
        for (int z = region.MinZoomLevel; z <= region.MaxZoomLevel; z++)
        {
            var tiles = GetTilesForZoomLevel(region, z);
            total += tiles.Count;
        }
        return total;
    }

    private List<(int x, int y)> GetTilesForZoomLevel(MapRegion region, int zoom)
    {
        var tiles = new List<(int x, int y)>();
        
        var minTile = LatLonToTile(region.MinLatitude, region.MinLongitude, zoom);
        var maxTile = LatLonToTile(region.MaxLatitude, region.MaxLongitude, zoom);

        for (int x = minTile.x; x <= maxTile.x; x++)
        {
            for (int y = minTile.y; y <= maxTile.y; y++)
            {
                tiles.Add((x, y));
            }
        }

        return tiles;
    }

    private static (int x, int y) LatLonToTile(double lat, double lon, int zoom)
    {
        int n = 1 << zoom;
        int x = (int)((lon + 180.0) / 360.0 * n);
        double latRad = lat * Math.PI / 180.0;
        int y = (int)((1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n);
        return (x, y);
    }

    public void CancelDownload()
    {
        _downloadCts?.Cancel();
        _logger?.LogInfo("Download cancellation requested", nameof(TileDownloadService));
    }

    public DownloadProgress? GetDownloadProgress()
    {
        return _currentProgress;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _downloadSemaphore.Dispose();
        _downloadCts?.Dispose();
    }
}
