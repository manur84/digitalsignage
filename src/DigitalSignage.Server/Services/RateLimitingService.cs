using Serilog;
using System.Collections.Concurrent;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Service for rate limiting to prevent brute-force attacks and API abuse
/// </summary>
public class RateLimitingService
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, RequestTracker> _trackers = new();
    private readonly Timer _cleanupTimer;

    // Configuration
    private readonly int _maxRequestsPerMinute;
    private readonly int _maxRequestsPerHour;
    private readonly TimeSpan _blockDuration;
    private readonly TimeSpan _cleanupInterval;

    public RateLimitingService(
        int maxRequestsPerMinute = 60,
        int maxRequestsPerHour = 1000,
        int blockDurationSeconds = 300,
        int cleanupIntervalSeconds = 300)
    {
        _logger = Log.ForContext<RateLimitingService>();
        _maxRequestsPerMinute = maxRequestsPerMinute;
        _maxRequestsPerHour = maxRequestsPerHour;
        _blockDuration = TimeSpan.FromSeconds(blockDurationSeconds);
        _cleanupInterval = TimeSpan.FromSeconds(cleanupIntervalSeconds);

        // Start cleanup timer
        _cleanupTimer = new Timer(CleanupExpiredTrackers, null, _cleanupInterval, _cleanupInterval);

        _logger.Information(
            "Rate limiting service initialized (MaxPerMin: {MaxPerMin}, MaxPerHour: {MaxPerHour}, BlockDuration: {BlockDuration}s)",
            _maxRequestsPerMinute, _maxRequestsPerHour, blockDurationSeconds);
    }

    /// <summary>
    /// Check if a request from the specified identifier should be allowed
    /// </summary>
    /// <param name="identifier">Unique identifier (IP address, username, API key hash, etc.)</param>
    /// <returns>True if request is allowed, false if rate limit exceeded</returns>
    public bool IsRequestAllowed(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            _logger.Warning("Rate limiting check called with empty identifier");
            return true; // Allow if no identifier
        }

        var tracker = _trackers.GetOrAdd(identifier, _ => new RequestTracker());

        lock (tracker.Lock)
        {
            var now = DateTime.UtcNow;

            // Check if currently blocked
            if (tracker.BlockedUntil.HasValue && now < tracker.BlockedUntil.Value)
            {
                _logger.Warning(
                    "Request blocked due to rate limiting (Identifier: {Identifier}, BlockedUntil: {BlockedUntil})",
                    MaskIdentifier(identifier), tracker.BlockedUntil.Value);
                return false;
            }

            // Clear block if expired
            if (tracker.BlockedUntil.HasValue && now >= tracker.BlockedUntil.Value)
            {
                tracker.BlockedUntil = null;
                tracker.RequestTimestamps.Clear();
                _logger.Information("Rate limit block expired for {Identifier}", MaskIdentifier(identifier));
            }

            // Remove timestamps older than 1 hour
            tracker.RequestTimestamps.RemoveAll(t => now - t > TimeSpan.FromHours(1));

            // Add current timestamp
            tracker.RequestTimestamps.Add(now);

            // Check minute limit
            var requestsInLastMinute = tracker.RequestTimestamps.Count(t => now - t <= TimeSpan.FromMinutes(1));
            if (requestsInLastMinute > _maxRequestsPerMinute)
            {
                tracker.BlockedUntil = now.Add(_blockDuration);
                _logger.Warning(
                    "Rate limit exceeded: {Count} requests in last minute (Identifier: {Identifier}, Blocked for {Duration}s)",
                    requestsInLastMinute, MaskIdentifier(identifier), _blockDuration.TotalSeconds);
                return false;
            }

            // Check hour limit
            var requestsInLastHour = tracker.RequestTimestamps.Count;
            if (requestsInLastHour > _maxRequestsPerHour)
            {
                tracker.BlockedUntil = now.Add(_blockDuration);
                _logger.Warning(
                    "Rate limit exceeded: {Count} requests in last hour (Identifier: {Identifier}, Blocked for {Duration}s)",
                    requestsInLastHour, MaskIdentifier(identifier), _blockDuration.TotalSeconds);
                return false;
            }

            // Update last access
            tracker.LastAccessTime = now;

            // Log verbose info for debugging (only every 10th request to avoid spam)
            if (requestsInLastMinute % 10 == 0)
            {
                _logger.Debug(
                    "Rate limit check passed (Identifier: {Identifier}, ReqsPerMin: {PerMin}, ReqsPerHour: {PerHour})",
                    MaskIdentifier(identifier), requestsInLastMinute, requestsInLastHour);
            }

            return true;
        }
    }

    /// <summary>
    /// Manually block an identifier for a specified duration
    /// </summary>
    public void BlockIdentifier(string identifier, TimeSpan? duration = null)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return;

        var blockDuration = duration ?? _blockDuration;
        var tracker = _trackers.GetOrAdd(identifier, _ => new RequestTracker());

        lock (tracker.Lock)
        {
            tracker.BlockedUntil = DateTime.UtcNow.Add(blockDuration);
            _logger.Warning(
                "Identifier manually blocked (Identifier: {Identifier}, Duration: {Duration}s)",
                MaskIdentifier(identifier), blockDuration.TotalSeconds);
        }
    }

    /// <summary>
    /// Unblock an identifier
    /// </summary>
    public void UnblockIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return;

        if (_trackers.TryGetValue(identifier, out var tracker))
        {
            lock (tracker.Lock)
            {
                tracker.BlockedUntil = null;
                tracker.RequestTimestamps.Clear();
                _logger.Information("Identifier manually unblocked: {Identifier}", MaskIdentifier(identifier));
            }
        }
    }

    /// <summary>
    /// Get statistics for an identifier
    /// </summary>
    public RateLimitStats? GetStats(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return null;

        if (!_trackers.TryGetValue(identifier, out var tracker))
            return null;

        lock (tracker.Lock)
        {
            var now = DateTime.UtcNow;
            var requestsInLastMinute = tracker.RequestTimestamps.Count(t => now - t <= TimeSpan.FromMinutes(1));
            var requestsInLastHour = tracker.RequestTimestamps.Count(t => now - t <= TimeSpan.FromHours(1));

            return new RateLimitStats
            {
                Identifier = identifier,
                RequestsInLastMinute = requestsInLastMinute,
                RequestsInLastHour = requestsInLastHour,
                IsBlocked = tracker.BlockedUntil.HasValue && now < tracker.BlockedUntil.Value,
                BlockedUntil = tracker.BlockedUntil,
                LastAccessTime = tracker.LastAccessTime
            };
        }
    }

    /// <summary>
    /// Get all current statistics
    /// </summary>
    public IEnumerable<RateLimitStats> GetAllStats()
    {
        var stats = new List<RateLimitStats>();
        var now = DateTime.UtcNow;

        foreach (var kvp in _trackers)
        {
            lock (kvp.Value.Lock)
            {
                var requestsInLastMinute = kvp.Value.RequestTimestamps.Count(t => now - t <= TimeSpan.FromMinutes(1));
                var requestsInLastHour = kvp.Value.RequestTimestamps.Count(t => now - t <= TimeSpan.FromHours(1));

                stats.Add(new RateLimitStats
                {
                    Identifier = kvp.Key,
                    RequestsInLastMinute = requestsInLastMinute,
                    RequestsInLastHour = requestsInLastHour,
                    IsBlocked = kvp.Value.BlockedUntil.HasValue && now < kvp.Value.BlockedUntil.Value,
                    BlockedUntil = kvp.Value.BlockedUntil,
                    LastAccessTime = kvp.Value.LastAccessTime
                });
            }
        }

        return stats.OrderByDescending(s => s.LastAccessTime);
    }

    /// <summary>
    /// Reset statistics for an identifier
    /// </summary>
    public void ResetStats(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return;

        _trackers.TryRemove(identifier, out _);
        _logger.Information("Rate limit stats reset for {Identifier}", MaskIdentifier(identifier));
    }

    /// <summary>
    /// Reset all statistics
    /// </summary>
    public void ResetAllStats()
    {
        var count = _trackers.Count;
        _trackers.Clear();
        _logger.Information("All rate limit stats reset ({Count} trackers removed)", count);
    }

    private void CleanupExpiredTrackers(object? state)
    {
        try
        {
            var now = DateTime.UtcNow;
            var expiredThreshold = TimeSpan.FromHours(2);
            var expiredKeys = new List<string>();

            foreach (var kvp in _trackers)
            {
                lock (kvp.Value.Lock)
                {
                    // Remove if no recent activity and not blocked
                    if (now - kvp.Value.LastAccessTime > expiredThreshold &&
                        (!kvp.Value.BlockedUntil.HasValue || now >= kvp.Value.BlockedUntil.Value))
                    {
                        expiredKeys.Add(kvp.Key);
                    }
                }
            }

            foreach (var key in expiredKeys)
            {
                _trackers.TryRemove(key, out _);
            }

            if (expiredKeys.Count > 0)
            {
                _logger.Debug("Cleaned up {Count} expired rate limit trackers", expiredKeys.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during rate limit tracker cleanup");
        }
    }

    /// <summary>
    /// Mask identifier for logging (show only first and last 3 chars)
    /// </summary>
    private static string MaskIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier) || identifier.Length <= 8)
            return identifier;

        return $"{identifier[..3]}...{identifier[^3..]}";
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
    }

    private class RequestTracker
    {
        public readonly object Lock = new();
        public List<DateTime> RequestTimestamps { get; } = new();
        public DateTime? BlockedUntil { get; set; }
        public DateTime LastAccessTime { get; set; } = DateTime.UtcNow;
    }
}

/// <summary>
/// Rate limit statistics for an identifier
/// </summary>
public class RateLimitStats
{
    public string Identifier { get; set; } = string.Empty;
    public int RequestsInLastMinute { get; set; }
    public int RequestsInLastHour { get; set; }
    public bool IsBlocked { get; set; }
    public DateTime? BlockedUntil { get; set; }
    public DateTime LastAccessTime { get; set; }
}
