using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace HarvestmoonGCS.Services;

/// <summary>
/// Manages page caching to improve navigation performance
/// Keeps frequently accessed pages in memory to avoid recreation overhead
/// </summary>
public class PageCacheManager
{
    private readonly Dictionary<Type, Page> _pageCache = new();
    private readonly Queue<Type> _cacheOrder = new();
    private readonly int _maxCacheSize;
    private readonly IServiceProvider _serviceProvider;

    public PageCacheManager(IServiceProvider serviceProvider, int maxCacheSize = 10)
    {
        _serviceProvider = serviceProvider;
        _maxCacheSize = maxCacheSize;
    }

    /// <summary>
    /// Get a page from cache or create new instance
    /// </summary>
    public Page GetOrCreatePage(Type pageType)
    {
        if (_pageCache.TryGetValue(pageType, out var cachedPage))
        {
            MarkAsRecentlyUsed(pageType);
            return cachedPage;
        }

        try
        {
            // Try to get from DI container first (for pages with dependencies)
            Page newPage;
            try
            {
                newPage = (Page)ActivatorUtilities.CreateInstance(_serviceProvider, pageType);
            }
            catch
            {
                // Fallback to Activator.CreateInstance for pages without dependencies
                newPage = (Page)Activator.CreateInstance(pageType)!;
            }
            
            EvictOldestPagesIfNeeded();

            if (!_pageCache.ContainsKey(pageType))
            {
                _pageCache[pageType] = newPage;
                _cacheOrder.Enqueue(pageType);
            }

            return newPage;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, $"[PageCacheManager] Failed to create {pageType.Name}");
            throw;
        }
    }

    public bool IsCached(Type pageType)
    {
        return _pageCache.ContainsKey(pageType);
    }

    private void MarkAsRecentlyUsed(Type pageType)
    {
        var items = _cacheOrder.ToArray();
        _cacheOrder.Clear();
        foreach (var item in items)
        {
            if (item != pageType && _pageCache.ContainsKey(item))
            {
                _cacheOrder.Enqueue(item);
            }
        }

        _cacheOrder.Enqueue(pageType);
    }

    private void EvictOldestPagesIfNeeded()
    {
        while (_pageCache.Count >= _maxCacheSize && _cacheOrder.Count > 0)
        {
            var oldestType = _cacheOrder.Dequeue();
            if (_pageCache.Remove(oldestType))
            {
                break;
            }
        }
    }

    /// <summary>
    /// Clear specific page from cache
    /// </summary>
    public void ClearPage(Type pageType)
    {
        if (_pageCache.Remove(pageType))
        {
            var items = _cacheOrder.ToArray();
            _cacheOrder.Clear();
            foreach (var item in items)
            {
                if (item != pageType)
                {
                    _cacheOrder.Enqueue(item);
                }
            }

            Serilog.Log.Debug($"[PageCacheManager] Cleared {pageType.Name} from cache");
        }
    }

    /// <summary>
    /// Clear all cached pages
    /// </summary>
    public void ClearAll()
    {
        _pageCache.Clear();
        _cacheOrder.Clear();
        Serilog.Log.Debug("[PageCacheManager] Cleared all cached pages");
    }

    /// <summary>
    /// Get current cache size
    /// </summary>
    public int CacheSize => _pageCache.Count;
}
