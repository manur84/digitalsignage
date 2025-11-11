namespace DigitalSignage.Server.Configuration;

/// <summary>
/// Configuration settings for SQL connection pooling
/// </summary>
public class ConnectionPoolSettings
{
    /// <summary>
    /// Minimum pool size
    /// </summary>
    public int MinPoolSize { get; set; } = 5;

    /// <summary>
    /// Maximum pool size
    /// </summary>
    public int MaxPoolSize { get; set; } = 100;

    /// <summary>
    /// Connection timeout in seconds
    /// </summary>
    public int ConnectionTimeout { get; set; } = 30;

    /// <summary>
    /// Command timeout in seconds
    /// </summary>
    public int CommandTimeout { get; set; } = 30;

    /// <summary>
    /// Whether connection pooling is enabled
    /// </summary>
    public bool Pooling { get; set; } = true;
}
