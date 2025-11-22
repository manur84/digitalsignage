using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Metrics collection service compatible with Prometheus scraping
/// Collects application metrics for monitoring and alerting
/// </summary>
public class MetricsService
{
    private readonly ILogger<MetricsService> _logger;

    // Counters (monotonically increasing)
    private long _messagesReceived;
    private long _messagesSent;
    private long _connectionsAccepted;
    private long _connectionsClosed;
    private long _errorsTotal;

    // Gauges (current value)
    private int _activeConnections;
    private readonly ConcurrentDictionary<string, int> _messageTypeCounters = new();

    // Histogram buckets (message processing time in ms)
    private readonly ConcurrentDictionary<string, long> _processingTimeHistogram = new();

    public MetricsService(ILogger<MetricsService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ====================
    // Counter Methods
    // ====================

    /// <summary>
    /// Increment messages received counter
    /// </summary>
    public void IncrementMessagesReceived(string messageType)
    {
        Interlocked.Increment(ref _messagesReceived);
        _messageTypeCounters.AddOrUpdate($"received_{messageType}", 1, (_, count) => count + 1);
    }

    /// <summary>
    /// Increment messages sent counter
    /// </summary>
    public void IncrementMessagesSent(string messageType)
    {
        Interlocked.Increment(ref _messagesSent);
        _messageTypeCounters.AddOrUpdate($"sent_{messageType}", 1, (_, count) => count + 1);
    }

    /// <summary>
    /// Increment connections accepted counter
    /// </summary>
    public void IncrementConnectionsAccepted()
    {
        Interlocked.Increment(ref _connectionsAccepted);
    }

    /// <summary>
    /// Increment connections closed counter
    /// </summary>
    public void IncrementConnectionsClosed()
    {
        Interlocked.Increment(ref _connectionsClosed);
    }

    /// <summary>
    /// Increment errors counter
    /// </summary>
    public void IncrementErrors(string errorType)
    {
        Interlocked.Increment(ref _errorsTotal);
        _messageTypeCounters.AddOrUpdate($"error_{errorType}", 1, (_, count) => count + 1);
    }

    // ====================
    // Gauge Methods
    // ====================

    /// <summary>
    /// Set active connections gauge
    /// </summary>
    public void SetActiveConnections(int count)
    {
        Interlocked.Exchange(ref _activeConnections, count);
    }

    /// <summary>
    /// Increment active connections
    /// </summary>
    public void IncrementActiveConnections()
    {
        Interlocked.Increment(ref _activeConnections);
    }

    /// <summary>
    /// Decrement active connections
    /// </summary>
    public void DecrementActiveConnections()
    {
        Interlocked.Decrement(ref _activeConnections);
    }

    // ====================
    // Histogram Methods
    // ====================

    /// <summary>
    /// Record message processing time
    /// </summary>
    public void RecordProcessingTime(string messageType, long milliseconds)
    {
        // Simple histogram: just store latest value per message type
        // For production: use proper histogram with buckets
        _processingTimeHistogram.AddOrUpdate($"processing_time_{messageType}", milliseconds, (_, _) => milliseconds);
    }

    // ====================
    // Export Methods
    // ====================

    /// <summary>
    /// Export metrics in Prometheus text format
    /// https://prometheus.io/docs/instrumenting/exposition_formats/
    /// </summary>
    public string ExportPrometheusFormat()
    {
        var sb = new StringBuilder();

        // Metadata
        sb.AppendLine("# HELP digitalsignage_messages_received_total Total number of messages received from clients");
        sb.AppendLine("# TYPE digitalsignage_messages_received_total counter");
        sb.AppendLine($"digitalsignage_messages_received_total {_messagesReceived}");
        sb.AppendLine();

        sb.AppendLine("# HELP digitalsignage_messages_sent_total Total number of messages sent to clients");
        sb.AppendLine("# TYPE digitalsignage_messages_sent_total counter");
        sb.AppendLine($"digitalsignage_messages_sent_total {_messagesSent}");
        sb.AppendLine();

        sb.AppendLine("# HELP digitalsignage_connections_accepted_total Total number of connections accepted");
        sb.AppendLine("# TYPE digitalsignage_connections_accepted_total counter");
        sb.AppendLine($"digitalsignage_connections_accepted_total {_connectionsAccepted}");
        sb.AppendLine();

        sb.AppendLine("# HELP digitalsignage_connections_closed_total Total number of connections closed");
        sb.AppendLine("# TYPE digitalsignage_connections_closed_total counter");
        sb.AppendLine($"digitalsignage_connections_closed_total {_connectionsClosed}");
        sb.AppendLine();

        sb.AppendLine("# HELP digitalsignage_active_connections Current number of active WebSocket connections");
        sb.AppendLine("# TYPE digitalsignage_active_connections gauge");
        sb.AppendLine($"digitalsignage_active_connections {_activeConnections}");
        sb.AppendLine();

        sb.AppendLine("# HELP digitalsignage_errors_total Total number of errors");
        sb.AppendLine("# TYPE digitalsignage_errors_total counter");
        sb.AppendLine($"digitalsignage_errors_total {_errorsTotal}");
        sb.AppendLine();

        // Per-message-type counters
        sb.AppendLine("# HELP digitalsignage_messages_by_type_total Messages grouped by type");
        sb.AppendLine("# TYPE digitalsignage_messages_by_type_total counter");
        foreach (var kvp in _messageTypeCounters.OrderBy(x => x.Key))
        {
            sb.AppendLine($"digitalsignage_messages_by_type_total{{type=\"{kvp.Key}\"}} {kvp.Value}");
        }
        sb.AppendLine();

        // Processing time histogram
        sb.AppendLine("# HELP digitalsignage_processing_time_ms Message processing time in milliseconds");
        sb.AppendLine("# TYPE digitalsignage_processing_time_ms gauge");
        foreach (var kvp in _processingTimeHistogram.OrderBy(x => x.Key))
        {
            sb.AppendLine($"digitalsignage_processing_time_ms{{type=\"{kvp.Key}\"}} {kvp.Value}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Export metrics in JSON format (for custom dashboards)
    /// </summary>
    public MetricsSnapshot ExportJson()
    {
        return new MetricsSnapshot
        {
            Timestamp = DateTime.UtcNow,
            Counters = new MetricsCounters
            {
                MessagesReceived = _messagesReceived,
                MessagesSent = _messagesSent,
                ConnectionsAccepted = _connectionsAccepted,
                ConnectionsClosed = _connectionsClosed,
                ErrorsTotal = _errorsTotal
            },
            Gauges = new MetricsGauges
            {
                ActiveConnections = _activeConnections
            },
            MessageTypeCounts = _messageTypeCounters.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            ProcessingTimes = _processingTimeHistogram.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };
    }

    /// <summary>
    /// Reset all metrics (for testing)
    /// </summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _messagesReceived, 0);
        Interlocked.Exchange(ref _messagesSent, 0);
        Interlocked.Exchange(ref _connectionsAccepted, 0);
        Interlocked.Exchange(ref _connectionsClosed, 0);
        Interlocked.Exchange(ref _errorsTotal, 0);
        Interlocked.Exchange(ref _activeConnections, 0);
        _messageTypeCounters.Clear();
        _processingTimeHistogram.Clear();

        _logger.LogInformation("Metrics reset");
    }
}

// ====================
// DTOs
// ====================

public class MetricsSnapshot
{
    public DateTime Timestamp { get; set; }
    public MetricsCounters Counters { get; set; } = new();
    public MetricsGauges Gauges { get; set; } = new();
    public System.Collections.Generic.Dictionary<string, int> MessageTypeCounts { get; set; } = new();
    public System.Collections.Generic.Dictionary<string, long> ProcessingTimes { get; set; } = new();
}

public class MetricsCounters
{
    public long MessagesReceived { get; set; }
    public long MessagesSent { get; set; }
    public long ConnectionsAccepted { get; set; }
    public long ConnectionsClosed { get; set; }
    public long ErrorsTotal { get; set; }
}

public class MetricsGauges
{
    public int ActiveConnections { get; set; }
}
