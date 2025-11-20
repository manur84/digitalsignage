namespace DigitalSignage.Server.Services;

/// <summary>
/// Health status enumeration
/// </summary>
public enum HealthStatus
{
    Healthy,
    Warning,
    Critical
}

/// <summary>
/// Complete diagnostics report
/// </summary>
public class SystemDiagnosticsReport
{
    public DateTime GeneratedAt { get; set; }
    public HealthStatus OverallStatus { get; set; }
    public DatabaseHealthInfo DatabaseHealth { get; set; } = new();
    public WebSocketHealthInfo WebSocketHealth { get; set; } = new();
    public PortAvailabilityInfo PortAvailability { get; set; } = new();
    public CertificateStatusInfo CertificateStatus { get; set; } = new();
    public ClientStatisticsInfo ClientStatistics { get; set; } = new();
    public PerformanceMetricsInfo PerformanceMetrics { get; set; } = new();
    public LogAnalysisInfo LogAnalysis { get; set; } = new();
    public SystemInfoModel SystemInfo { get; set; } = new();
}

public class DatabaseHealthInfo
{
    public HealthStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool CanConnect { get; set; }
    public string ConnectionString { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string? DatabasePath { get; set; }
    public long DatabaseSize { get; set; }
    public Dictionary<string, int> TableCounts { get; set; } = new();
    public DateTime? LastBackupDate { get; set; }
}

public class WebSocketHealthInfo
{
    public HealthStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRunning { get; set; }
    public string ListeningUrl { get; set; } = string.Empty;
    public int Port { get; set; }
    public bool EnableSsl { get; set; }
    public string EndpointPath { get; set; } = string.Empty;
    public int MaxMessageSize { get; set; }
    public int HeartbeatTimeout { get; set; }
    public int ActiveConnections { get; set; }
    public TimeSpan Uptime { get; set; }
}

public class PortAvailabilityInfo
{
    public HealthStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ConfiguredPort { get; set; }
    public bool IsConfiguredPortAvailable { get; set; }
    public int CurrentActivePort { get; set; }
    public List<int> AlternativePorts { get; set; } = new();
    public List<int> AvailablePorts { get; set; } = new();
}

public class CertificateStatusInfo
{
    public HealthStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool SslEnabled { get; set; }
    public string? CertificatePath { get; set; }
    public string? Subject { get; set; }
    public string? Issuer { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public bool IsValid { get; set; }
}

public class ClientStatisticsInfo
{
    public HealthStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public int TotalClients { get; set; }
    public int OnlineClients { get; set; }
    public int OfflineClients { get; set; }
    public int DisconnectedClients { get; set; }
    public Dictionary<string, DateTime> LastHeartbeats { get; set; } = new();
}

public class PerformanceMetricsInfo
{
    public HealthStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public double CpuUsage { get; set; }
    public double MemoryUsageMB { get; set; }
    public double PrivateMemoryMB { get; set; }
    public int ThreadCount { get; set; }
    public double DiskTotalGB { get; set; }
    public double DiskFreeGB { get; set; }
    public double DiskUsagePercent { get; set; }
}

public class LogAnalysisInfo
{
    public HealthStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public int LogFilesCount { get; set; }
    public double TotalLogSizeMB { get; set; }
    public int ErrorsLastHour { get; set; }
    public int WarningsLastHour { get; set; }
    public int ErrorsToday { get; set; }
    public int WarningsToday { get; set; }
    public string? LastCriticalError { get; set; }
}

public class SystemInfoModel
{
    public string MachineName { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public int ProcessorCount { get; set; }
    public string DotNetVersion { get; set; } = string.Empty;
    public bool Is64BitOperatingSystem { get; set; }
    public bool Is64BitProcess { get; set; }
    public string ApplicationVersion { get; set; } = string.Empty;
    public string BaseDirectory { get; set; } = string.Empty;
    public string CurrentDirectory { get; set; } = string.Empty;
}
