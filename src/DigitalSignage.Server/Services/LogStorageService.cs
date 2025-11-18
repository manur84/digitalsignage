using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Service for storing and managing client logs in memory
/// </summary>
public class LogStorageService
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<LogEntry>> _clientLogs = new();
    private readonly ConcurrentQueue<LogEntry> _allLogs = new();
    private readonly ILogger<LogStorageService> _logger;
    private const int MaxLogsPerClient = 1000;
    private const int MaxTotalLogs = 10000;

    public event EventHandler<LogEntry>? LogReceived;

    public LogStorageService(ILogger<LogStorageService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Add a log entry to storage
    /// </summary>
    public void AddLog(LogEntry logEntry)
    {
        if (logEntry == null)
        {
            throw new ArgumentNullException(nameof(logEntry));
        }

        // Add to all logs
        _allLogs.Enqueue(logEntry);
        TrimQueue(_allLogs, MaxTotalLogs);

        // Add to client-specific logs
        var clientQueue = _clientLogs.GetOrAdd(logEntry.ClientId, _ => new ConcurrentQueue<LogEntry>());
        clientQueue.Enqueue(logEntry);
        TrimQueue(clientQueue, MaxLogsPerClient);

        // Notify subscribers
        LogReceived?.Invoke(this, logEntry);

        _logger.LogDebug("Stored log from client {ClientId}: {Level} - {Message}",
            logEntry.ClientId, logEntry.Level, logEntry.Message);
    }

    /// <summary>
    /// Get all logs across all clients
    /// </summary>
    public IEnumerable<LogEntry> GetAllLogs()
    {
        return _allLogs.ToArray();
    }

    /// <summary>
    /// Get logs for a specific client
    /// </summary>
    public IEnumerable<LogEntry> GetLogsForClient(string clientId)
    {
        if (_clientLogs.TryGetValue(clientId, out var clientQueue))
        {
            return clientQueue.ToArray();
        }
        return Enumerable.Empty<LogEntry>();
    }

    /// <summary>
    /// Get logs filtered by level
    /// </summary>
    public IEnumerable<LogEntry> GetLogsByLevel(Core.Models.LogLevel level)
    {
        return _allLogs.Where(log => log.Level == level).ToArray();
    }

    /// <summary>
    /// Get logs filtered by client and level
    /// </summary>
    public IEnumerable<LogEntry> GetLogs(string? clientId = null, Core.Models.LogLevel? level = null, DateTime? since = null)
    {
        IEnumerable<LogEntry> logs = clientId != null
            ? GetLogsForClient(clientId)
            : GetAllLogs();

        if (level.HasValue)
        {
            logs = logs.Where(log => log.Level == level.Value);
        }

        if (since.HasValue)
        {
            logs = logs.Where(log => log.Timestamp >= since.Value);
        }

        return logs.OrderByDescending(log => log.Timestamp).ToArray();
    }

    /// <summary>
    /// Clear all logs
    /// </summary>
    public void ClearAllLogs()
    {
        _allLogs.Clear();
        _clientLogs.Clear();
        _logger.LogInformation("All logs cleared");
    }

    /// <summary>
    /// Clear logs for a specific client
    /// </summary>
    public void ClearClientLogs(string clientId)
    {
        if (_clientLogs.TryRemove(clientId, out _))
        {
            _logger.LogInformation("Cleared logs for client {ClientId}", clientId);
        }
    }

    /// <summary>
    /// Get count of logs for a client
    /// </summary>
    public int GetLogCount(string? clientId = null)
    {
        if (clientId != null)
        {
            return _clientLogs.TryGetValue(clientId, out var queue) ? queue.Count : 0;
        }
        return _allLogs.Count;
    }

    /// <summary>
    /// Get list of clients that have logs
    /// </summary>
    public IEnumerable<string> GetClientsWithLogs()
    {
        return _clientLogs.Keys.ToArray();
    }

    /// <summary>
    /// Trim a queue to a maximum size
    /// </summary>
    private void TrimQueue(ConcurrentQueue<LogEntry> queue, int maxSize)
    {
        while (queue.Count > maxSize)
        {
            queue.TryDequeue(out _);
        }
    }

    /// <summary>
    /// Export logs to a formatted string
    /// </summary>
    public string ExportLogs(IEnumerable<LogEntry> logs)
    {
        var lines = logs
            .OrderBy(log => log.Timestamp)
            .Select(log => $"{log.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{log.Level,-8}] [{log.ClientName,-20}] {log.Message}" +
                          (string.IsNullOrEmpty(log.Exception) ? "" : $"\n    Exception: {log.Exception}"));

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Get statistics about stored logs
    /// </summary>
    public LogStatistics GetStatistics()
    {
        var allLogs = _allLogs.ToArray();

        // Use GroupBy for single-pass counting instead of multiple Count() calls
        var levelCounts = allLogs.GroupBy(l => l.Level).ToDictionary(g => g.Key, g => g.Count());

        return new LogStatistics
        {
            TotalLogs = allLogs.Length,
            DebugCount = levelCounts.GetValueOrDefault(Core.Models.LogLevel.Debug, 0),
            InfoCount = levelCounts.GetValueOrDefault(Core.Models.LogLevel.Info, 0),
            WarningCount = levelCounts.GetValueOrDefault(Core.Models.LogLevel.Warning, 0),
            ErrorCount = levelCounts.GetValueOrDefault(Core.Models.LogLevel.Error, 0),
            CriticalCount = levelCounts.GetValueOrDefault(Core.Models.LogLevel.Critical, 0),
            ClientCount = _clientLogs.Count,
            OldestLog = allLogs.Any() ? allLogs.Min(l => l.Timestamp) : (DateTime?)null,
            NewestLog = allLogs.Any() ? allLogs.Max(l => l.Timestamp) : (DateTime?)null
        };
    }
}

/// <summary>
/// Statistics about stored logs
/// </summary>
public class LogStatistics
{
    public int TotalLogs { get; set; }
    public int DebugCount { get; set; }
    public int InfoCount { get; set; }
    public int WarningCount { get; set; }
    public int ErrorCount { get; set; }
    public int CriticalCount { get; set; }
    public int ClientCount { get; set; }
    public DateTime? OldestLog { get; set; }
    public DateTime? NewestLog { get; set; }
}
