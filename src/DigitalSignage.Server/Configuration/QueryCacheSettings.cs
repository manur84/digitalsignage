namespace DigitalSignage.Server.Configuration;

/// <summary>
/// Configuration settings for SQL query caching
/// </summary>
public class QueryCacheSettings
{
    /// <summary>
    /// Whether query caching is enabled
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Default cache duration in seconds (5 minutes default)
    /// </summary>
    public int DefaultCacheDuration { get; set; } = 300;

    /// <summary>
    /// Maximum number of cache entries to store
    /// </summary>
    public int MaxCacheEntries { get; set; } = 1000;

    /// <summary>
    /// Whether to track cache statistics
    /// </summary>
    public bool EnableStatistics { get; set; } = true;
}
