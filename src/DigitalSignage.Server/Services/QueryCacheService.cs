using DigitalSignage.Server.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Service for caching SQL query results
/// </summary>
public class QueryCacheService
{
    private readonly QueryCacheSettings _settings;
    private readonly ILogger<QueryCacheService> _logger;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly ConcurrentDictionary<string, CacheStatistics> _statistics = new();

    public QueryCacheService(
        IOptions<QueryCacheSettings> settings,
        ILogger<QueryCacheService> logger)
    {
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets cached data if available and not expired
    /// </summary>
    public bool TryGet(string query, Dictionary<string, object>? parameters, out Dictionary<string, object>? data)
    {
        data = null;

        if (!_settings.EnableCaching)
        {
            return false;
        }

        var cacheKey = GenerateCacheKey(query, parameters);

        if (_cache.TryGetValue(cacheKey, out var entry))
        {
            if (entry.ExpiresAt > DateTime.UtcNow)
            {
                data = entry.Data;

                if (_settings.EnableStatistics)
                {
                    IncrementHits(cacheKey);
                }

                _logger.LogDebug("Cache HIT for query: {CacheKey}", cacheKey);
                return true;
            }
            else
            {
                // Remove expired entry
                _cache.TryRemove(cacheKey, out _);
                _logger.LogDebug("Cache entry expired for query: {CacheKey}", cacheKey);
            }
        }

        if (_settings.EnableStatistics)
        {
            IncrementMisses(cacheKey);
        }

        _logger.LogDebug("Cache MISS for query: {CacheKey}", cacheKey);
        return false;
    }

    /// <summary>
    /// Stores data in cache
    /// </summary>
    public void Set(string query, Dictionary<string, object>? parameters, Dictionary<string, object> data, int? cacheDurationSeconds = null)
    {
        if (!_settings.EnableCaching)
        {
            return;
        }

        var cacheKey = GenerateCacheKey(query, parameters);
        var duration = cacheDurationSeconds ?? _settings.DefaultCacheDuration;

        // Check if we need to evict entries
        if (_cache.Count >= _settings.MaxCacheEntries)
        {
            EvictOldestEntries();
        }

        var entry = new CacheEntry
        {
            Data = data,
            CachedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddSeconds(duration),
            CacheKey = cacheKey
        };

        _cache[cacheKey] = entry;
        _logger.LogDebug("Cached query result: {CacheKey} (expires in {Duration}s)", cacheKey, duration);
    }

    /// <summary>
    /// Invalidates cache entries matching a pattern
    /// </summary>
    public void Invalidate(string pattern)
    {
        var keysToRemove = _cache.Keys.Where(k => k.Contains(pattern, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var key in keysToRemove)
        {
            _cache.TryRemove(key, out _);
            _logger.LogDebug("Invalidated cache entry: {CacheKey}", key);
        }

        _logger.LogInformation("Invalidated {Count} cache entries matching pattern: {Pattern}", keysToRemove.Count, pattern);
    }

    /// <summary>
    /// Clears all cache entries
    /// </summary>
    public void Clear()
    {
        var count = _cache.Count;
        _cache.Clear();
        _logger.LogInformation("Cleared {Count} cache entries", count);
    }

    /// <summary>
    /// Gets cache statistics
    /// </summary>
    public CacheStats GetStatistics()
    {
        // ✅ PERFORMANCE FIX: Use single-pass aggregation instead of multiple iterations
        // Old code: Iterated twice (Sum for Hits, Sum for Misses)
        // New code: Single iteration with Aggregate
        var (totalHits, totalMisses) = _statistics.Values.Aggregate(
            (Hits: 0L, Misses: 0L),
            (acc, stats) => (acc.Hits + stats.Hits, acc.Misses + stats.Misses));

        var totalRequests = totalHits + totalMisses;
        var hitRate = totalRequests > 0 ? (double)totalHits / totalRequests * 100 : 0;

        return new CacheStats
        {
            EntryCount = _cache.Count,
            TotalHits = totalHits,
            TotalMisses = totalMisses,
            HitRate = hitRate,
            MaxEntries = _settings.MaxCacheEntries,
            CachingEnabled = _settings.EnableCaching
        };
    }

    /// <summary>
    /// Generates a cache key from query and parameters
    /// </summary>
    private string GenerateCacheKey(string query, Dictionary<string, object>? parameters)
    {
        var sb = new StringBuilder();
        sb.Append(query.Trim().ToLower());

        if (parameters != null && parameters.Any())
        {
            sb.Append('|');
            foreach (var param in parameters.OrderBy(p => p.Key))
            {
                sb.Append($"{param.Key}={param.Value}");
                sb.Append('|');
            }
        }

        // Generate SHA256 hash for compact key
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Evicts oldest cache entries when limit is reached
    /// </summary>
    private void EvictOldestEntries()
    {
        var entriesToRemove = _settings.MaxCacheEntries / 10; // Remove 10% of entries
        var oldestEntries = _cache
            .OrderBy(e => e.Value.CachedAt)
            .Take(entriesToRemove)
            .Select(e => e.Key)
            .ToList();

        foreach (var key in oldestEntries)
        {
            _cache.TryRemove(key, out _);
        }

        _logger.LogInformation("Evicted {Count} oldest cache entries", entriesToRemove);
    }

    private void IncrementHits(string cacheKey)
    {
        _statistics.AddOrUpdate(
            cacheKey,
            new CacheStatistics { Hits = 1 },
            (_, stats) =>
            {
                Interlocked.Increment(ref stats.Hits);
                return stats;
            });
    }

    private void IncrementMisses(string cacheKey)
    {
        _statistics.AddOrUpdate(
            cacheKey,
            new CacheStatistics { Misses = 1 },
            (_, stats) =>
            {
                Interlocked.Increment(ref stats.Misses);
                return stats;
            });
    }

    private class CacheEntry
    {
        public Dictionary<string, object> Data { get; set; } = new();
        public DateTime CachedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string CacheKey { get; set; } = string.Empty;
    }

    private class CacheStatistics
    {
        public long Hits;
        public long Misses;
    }
}

/// <summary>
/// Cache statistics model
/// </summary>
public class CacheStats
{
    public int EntryCount { get; set; }
    public long TotalHits { get; set; }
    public long TotalMisses { get; set; }
    public double HitRate { get; set; }
    public int MaxEntries { get; set; }
    public bool CachingEnabled { get; set; }
}
