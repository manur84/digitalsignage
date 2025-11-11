namespace DigitalSignage.Core.Models;

/// <summary>
/// Represents a log entry from a client
/// </summary>
public class LogEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public LogLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Returns a formatted string representation of the log entry
    /// </summary>
    public override string ToString()
    {
        return $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level}] [{ClientName}] {Message}";
    }
}
