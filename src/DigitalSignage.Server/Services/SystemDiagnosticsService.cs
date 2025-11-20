using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using DigitalSignage.Data;
using DigitalSignage.Server.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Service for system diagnostics and health checks
/// </summary>
public class SystemDiagnosticsService
{
    private readonly ILogger<SystemDiagnosticsService> _logger;
    private readonly IDbContextFactory<DigitalSignageDbContext> _contextFactory;
    private readonly IClientService _clientService;
    private readonly ICommunicationService _communicationService;
    private readonly BackupService _backupService;
    private readonly ServerSettings _serverSettings;
    private readonly DiagnosticsReportExporter _exporter;

    public SystemDiagnosticsService(
        ILogger<SystemDiagnosticsService> logger,
        IDbContextFactory<DigitalSignageDbContext> contextFactory,
        IClientService clientService,
        ICommunicationService communicationService,
        BackupService backupService,
        ServerSettings serverSettings)
    {
        _logger = logger;
        _contextFactory = contextFactory;
        _clientService = clientService;
        _communicationService = communicationService;
        _backupService = backupService;
        _serverSettings = serverSettings;
        _exporter = new DiagnosticsReportExporter();
    }

    /// <summary>
    /// Get comprehensive system diagnostics
    /// </summary>
    public async Task<SystemDiagnosticsReport> GetDiagnosticsAsync()
    {
        _logger.LogInformation("Running system diagnostics...");

        var report = new SystemDiagnosticsReport
        {
            // ✅ CODE SMELL FIX: Use DateTime.UtcNow consistently throughout the application
            GeneratedAt = DateTime.UtcNow,
            DatabaseHealth = await GetDatabaseHealthAsync(),
            WebSocketHealth = await GetWebSocketHealthAsync(),
            PortAvailability = GetPortAvailability(),
            CertificateStatus = GetCertificateStatus(),
            ClientStatistics = await GetClientStatisticsAsync(),
            PerformanceMetrics = await GetPerformanceMetricsAsync(),
            LogAnalysis = GetLogAnalysis(),
            SystemInfo = GetSystemInfo()
        };

        report.OverallStatus = CalculateOverallStatus(report);

        _logger.LogInformation("System diagnostics completed. Overall status: {Status}", report.OverallStatus);

        return report;
    }

    private async Task<DatabaseHealthInfo> GetDatabaseHealthAsync()
    {
        var info = new DatabaseHealthInfo();

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Connection test
            info.CanConnect = await context.Database.CanConnectAsync();
            info.ConnectionString = context.Database.GetConnectionString() ?? "N/A";
            info.ProviderName = context.Database.ProviderName ?? "Unknown";

            if (info.CanConnect)
            {
                info.Status = HealthStatus.Healthy;

                // Get database file info
                var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "digitalsignage.db");
                if (File.Exists(dbPath))
                {
                    var fileInfo = new FileInfo(dbPath);
                    info.DatabasePath = dbPath;
                    info.DatabaseSize = fileInfo.Length;
                }

                // Count tables and rows (in parallel)
                var deviceCountTask = context.Clients.CountAsync();
                var layoutCountTask = context.DisplayLayouts.CountAsync();
                var mediaCountTask = context.MediaFiles.CountAsync();
                var scheduleCountTask = context.LayoutSchedules.CountAsync();

                // ✅ FIX: Await all tasks properly without using .Result to avoid potential deadlocks
                var counts = await Task.WhenAll(deviceCountTask, layoutCountTask, mediaCountTask, scheduleCountTask);

                info.TableCounts = new Dictionary<string, int>
                {
                    { "Devices", counts[0] },
                    { "Layouts", counts[1] },
                    { "Media", counts[2] },
                    { "Schedules", counts[3] }
                };

                // Get last backup info
                var backups = await _backupService.GetAvailableBackupsAsync();
                if (backups.Any())
                {
                    var lastBackup = backups.First();
                    info.LastBackupDate = lastBackup.CreatedDate;
                }
            }
            else
            {
                info.Status = HealthStatus.Critical;
                info.Message = "Cannot connect to database";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            info.Status = HealthStatus.Critical;
            info.Message = $"Error: {ex.Message}";
        }

        return info;
    }

    private async Task<WebSocketHealthInfo> GetWebSocketHealthAsync()
    {
        var info = new WebSocketHealthInfo();

        try
        {
            // Get server status
            info.Port = _serverSettings.Port;
            info.EnableSsl = _serverSettings.EnableSsl;
            info.EndpointPath = _serverSettings.EndpointPath;
            info.MaxMessageSize = _serverSettings.MaxMessageSize;
            info.HeartbeatTimeout = _serverSettings.ClientHeartbeatTimeout;

            // Build listening URL
            var protocol = _serverSettings.EnableSsl ? "wss" : "ws";
            info.ListeningUrl = $"{protocol}://+:{_serverSettings.Port}{_serverSettings.EndpointPath}";

            // Check if server is running (based on having active clients)
            var clientsResult = await _clientService.GetAllClientsAsync();
            if (!clientsResult.IsSuccess || clientsResult.Value == null)
            {
                info.IsRunning = false;
                info.ActiveConnections = 0;
                info.Status = HealthStatus.Warning;
                info.Message = $"Unable to determine client connections: {clientsResult.ErrorMessage}";
            }
            else
            {
                var clients = clientsResult.Value;
                info.IsRunning = clients.Any();
                info.ActiveConnections = clients.Count(c => c.Status == Core.Models.ClientStatus.Online);
                info.Status = info.IsRunning ? HealthStatus.Healthy : HealthStatus.Warning;
                info.Message = info.IsRunning ? "Server is running" : "No active connections";
            }

            // Get uptime (approximation based on process)
            var currentProcess = Process.GetCurrentProcess();
            // ✅ CODE SMELL FIX: Use DateTime.UtcNow for consistency
            info.Uptime = DateTime.UtcNow - currentProcess.StartTime;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebSocket health check failed");
            info.Status = HealthStatus.Critical;
            info.Message = $"Error: {ex.Message}";
        }

        return info;
    }

    private PortAvailabilityInfo GetPortAvailability()
    {
        var info = new PortAvailabilityInfo
        {
            ConfiguredPort = _serverSettings.Port,
            AlternativePorts = _serverSettings.AlternativePorts.ToList()
        };

        // Check configured port
        info.IsConfiguredPortAvailable = IsPortAvailable(_serverSettings.Port);
        info.CurrentActivePort = _serverSettings.Port;

        // Check alternative ports
        info.AvailablePorts = new List<int>();
        foreach (var port in _serverSettings.AlternativePorts)
        {
            if (IsPortAvailable(port))
            {
                info.AvailablePorts.Add(port);
            }
        }

        info.Status = info.IsConfiguredPortAvailable ? HealthStatus.Healthy : HealthStatus.Warning;
        info.Message = info.IsConfiguredPortAvailable
            ? "Configured port is available"
            : $"Configured port {_serverSettings.Port} is in use";

        return info;
    }

    private bool IsPortAvailable(int port)
    {
        TcpListener? listener = null;
        try
        {
            listener = new TcpListener(System.Net.IPAddress.Any, port);
            listener.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        finally
        {
            listener?.Stop();
        }
    }

    private CertificateStatusInfo GetCertificateStatus()
    {
        var info = new CertificateStatusInfo
        {
            SslEnabled = _serverSettings.EnableSsl
        };

        if (!_serverSettings.EnableSsl)
        {
            info.Status = HealthStatus.Healthy;
            info.Message = "SSL/TLS is not enabled";
            return info;
        }

        try
        {
            // Check certificate file path
            if (!string.IsNullOrWhiteSpace(_serverSettings.CertificatePath))
            {
                info.CertificatePath = _serverSettings.CertificatePath;

                if (File.Exists(_serverSettings.CertificatePath))
                {
                    try
                    {
                        // Load certificate
                        var cert = new X509Certificate2(_serverSettings.CertificatePath, _serverSettings.CertificatePassword);
                        info.Subject = cert.Subject;
                        info.Issuer = cert.Issuer;
                        info.ExpirationDate = cert.NotAfter;
                        // ✅ CODE SMELL FIX: Use DateTime.UtcNow for certificate validation
                        info.IsValid = cert.NotBefore <= DateTime.UtcNow && cert.NotAfter >= DateTime.UtcNow;

                        if (info.IsValid)
                        {
                            // ✅ CODE SMELL FIX: Use DateTime.UtcNow for consistency
                            var daysUntilExpiry = (cert.NotAfter - DateTime.UtcNow).TotalDays;
                            if (daysUntilExpiry < 30)
                            {
                                info.Status = HealthStatus.Warning;
                                info.Message = $"Certificate expires in {daysUntilExpiry:F0} days";
                            }
                            else
                            {
                                info.Status = HealthStatus.Healthy;
                                info.Message = "Certificate is valid";
                            }
                        }
                        else
                        {
                            info.Status = HealthStatus.Critical;
                            info.Message = "Certificate is expired or not yet valid";
                        }
                    }
                    catch (Exception ex)
                    {
                        info.Status = HealthStatus.Critical;
                        info.Message = $"Cannot load certificate: {ex.Message}";
                    }
                }
                else
                {
                    info.Status = HealthStatus.Critical;
                    info.Message = "Certificate file not found";
                }
            }
            else if (!string.IsNullOrWhiteSpace(_serverSettings.CertificateThumbprint))
            {
                info.CertificatePath = $"Certificate Store (Thumbprint: {_serverSettings.CertificateThumbprint})";
                info.Status = HealthStatus.Warning;
                info.Message = "Cannot validate certificate store certificates";
            }
            else
            {
                info.Status = HealthStatus.Critical;
                info.Message = "SSL enabled but no certificate configured";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Certificate validation failed");
            info.Status = HealthStatus.Critical;
            info.Message = $"Error: {ex.Message}";
        }

        return info;
    }

    private async Task<ClientStatisticsInfo> GetClientStatisticsAsync()
    {
        var info = new ClientStatisticsInfo();

        try
        {
            var clientsResult = await _clientService.GetAllClientsAsync();
            var clients = clientsResult.IsSuccess && clientsResult.Value != null
                ? clientsResult.Value
                : new List<RaspberryPiClient>();

            if (!clientsResult.IsSuccess)
            {
                _logger.LogWarning("Unable to fetch clients for statistics: {Error}", clientsResult.ErrorMessage);
            }

            info.TotalClients = clients.Count;
            info.OnlineClients = clients.Count(c => c.Status == Core.Models.ClientStatus.Online);
            info.OfflineClients = clients.Count(c => c.Status == Core.Models.ClientStatus.Offline);
            info.DisconnectedClients = clients.Count(c => c.Status == Core.Models.ClientStatus.Disconnected);

            // Get last heartbeat times (using LastSeen as RaspberryPiClient doesn't have LastHeartbeat)
            info.LastHeartbeats = clients
                .OrderByDescending(c => c.LastSeen)
                .Take(5)
                .ToDictionary(c => c.Name, c => c.LastSeen);

            info.Status = info.OnlineClients > 0 ? HealthStatus.Healthy : HealthStatus.Warning;
            info.Message = $"{info.OnlineClients} clients online, {info.OfflineClients} offline";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Client statistics failed");
            info.Status = HealthStatus.Critical;
            info.Message = $"Error: {ex.Message}";
        }

        return info;
    }

    private async Task<PerformanceMetricsInfo> GetPerformanceMetricsAsync()
    {
        var info = new PerformanceMetricsInfo();

        try
        {
            var currentProcess = Process.GetCurrentProcess();

            // CPU usage (approximation)
            var startTime = DateTime.UtcNow;
            var startCpuUsage = currentProcess.TotalProcessorTime;
            await Task.Delay(500);
            var endTime = DateTime.UtcNow;
            var endCpuUsage = currentProcess.TotalProcessorTime;

            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);

            info.CpuUsage = cpuUsageTotal * 100;

            // Memory usage
            info.MemoryUsageMB = currentProcess.WorkingSet64 / (1024.0 * 1024.0);
            info.PrivateMemoryMB = currentProcess.PrivateMemorySize64 / (1024.0 * 1024.0);
            info.ThreadCount = currentProcess.Threads.Count;

            // Disk usage (database drive)
            var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "digitalsignage.db");
            if (File.Exists(dbPath))
            {
                var driveInfo = new DriveInfo(Path.GetPathRoot(dbPath) ?? "C:\\");
                info.DiskTotalGB = driveInfo.TotalSize / (1024.0 * 1024.0 * 1024.0);
                info.DiskFreeGB = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                info.DiskUsagePercent = ((info.DiskTotalGB - info.DiskFreeGB) / info.DiskTotalGB) * 100;
            }

            // Health status based on thresholds
            if (info.CpuUsage > 80 || info.MemoryUsageMB > 2000 || info.DiskUsagePercent > 90)
            {
                info.Status = HealthStatus.Warning;
                info.Message = "High resource usage detected";
            }
            else
            {
                info.Status = HealthStatus.Healthy;
                info.Message = "Performance is normal";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Performance metrics failed");
            info.Status = HealthStatus.Warning;
            info.Message = $"Error: {ex.Message}";
        }

        return info;
    }

    private LogAnalysisInfo GetLogAnalysis()
    {
        var info = new LogAnalysisInfo();

        try
        {
            var logsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

            if (Directory.Exists(logsPath))
            {
                var logFiles = Directory.GetFiles(logsPath, "*.txt");
                info.LogFilesCount = logFiles.Length;

                // Calculate total log size
                info.TotalLogSizeMB = logFiles.Sum(f => new FileInfo(f).Length) / (1024.0 * 1024.0);

                // Analyze recent errors (today's log) using streaming, single-pass
                // ✅ CODE SMELL FIX: Use DateTime.UtcNow for consistency
                var todayLog = logFiles.FirstOrDefault(f => f.Contains(DateTime.UtcNow.ToString("yyyyMMdd")));
                if (todayLog != null && File.Exists(todayLog))
                {
                    // ✅ CODE SMELL FIX: Use DateTime.UtcNow for consistency
                    var now = DateTime.UtcNow;
                    var oneHourAgo = now - TimeSpan.FromHours(1);
                    var todayStart = now.Date;

                    int errorsLastHour = 0, warningsLastHour = 0, errorsToday = 0, warningsToday = 0;
                    string? lastCritical = null;

                    foreach (var line in File.ReadLines(todayLog))
                    {
                        // Extract timestamp (format: 2025-11-14 12:34:56)
                        DateTime timestamp;
                        var tsMatch = System.Text.RegularExpressions.Regex.Match(line, @"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}");
                        if (tsMatch.Success && DateTime.TryParse(tsMatch.Value, out var parsed))
                        {
                            timestamp = parsed;
                        }
                        else
                        {
                            // If no timestamp, treat as today
                            timestamp = now;
                        }

                        var isError = line.Contains("[Error]", StringComparison.OrdinalIgnoreCase);
                        var isWarning = line.Contains("[Warning]", StringComparison.OrdinalIgnoreCase);
                        var isCritical = isError || line.Contains("[Fatal]", StringComparison.OrdinalIgnoreCase);

                        if (isError)
                        {
                            if (timestamp >= oneHourAgo) errorsLastHour++;
                            if (timestamp >= todayStart) errorsToday++;
                        }
                        else if (isWarning)
                        {
                            if (timestamp >= oneHourAgo) warningsLastHour++;
                            if (timestamp >= todayStart) warningsToday++;
                        }

                        if (isCritical)
                        {
                            // Track last critical line encountered
                            lastCritical = line.Length > 200 ? line.Substring(0, 200) + "..." : line;
                        }
                    }

                    info.ErrorsLastHour = errorsLastHour;
                    info.WarningsLastHour = warningsLastHour;
                    info.ErrorsToday = errorsToday;
                    info.WarningsToday = warningsToday;
                    info.LastCriticalError = lastCritical;
                }

                if         (info.ErrorsLastHour > 10)
                {
                    info.Status = HealthStatus.Warning;
                    info.Message = $"{info.ErrorsLastHour} errors in the last hour";
                }
                else if (info.ErrorsToday > 50)
                {
                    info.Status = HealthStatus.Warning;
                    info.Message = $"{info.ErrorsToday} errors today";
                }
                else
                {
                    info.Status = HealthStatus.Healthy;
                    info.Message = "No significant errors";
                }
            }
            else
            {
                info.Status = HealthStatus.Warning;
                info.Message = "Logs directory not found";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Log analysis failed");
            info.Status = HealthStatus.Warning;
            info.Message = $"Error: {ex.Message}";
        }

        return info;
    }

    private int CountLogLevel(string logContent, string level, TimeSpan timeWindow)
    {
        var lines = logContent.Split('\n');
        // ✅ CODE SMELL FIX: Use DateTime.UtcNow for consistency
        var cutoffTime = DateTime.UtcNow - timeWindow;
        var count = 0;

        foreach (var line in lines)
        {
            if (line.Contains($"[{level}]", StringComparison.OrdinalIgnoreCase))
            {
                // Try to extract timestamp (format: 2025-11-14 12:34:56)
                var timestampMatch = System.Text.RegularExpressions.Regex.Match(line, @"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}");
                if (timestampMatch.Success && DateTime.TryParse(timestampMatch.Value, out var timestamp))
                {
                    if (timestamp >= cutoffTime)
                    {
                        count++;
                    }
                }
                else
                {
                    // If no timestamp, count it (assume recent)
                    count++;
                }
            }
        }

        return count;
    }

    private string? FindLastCriticalError(string logContent)
    {
        var lines = logContent.Split('\n');
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            if (lines[i].Contains("[Error]", StringComparison.OrdinalIgnoreCase) ||
                lines[i].Contains("[Fatal]", StringComparison.OrdinalIgnoreCase))
            {
                return lines[i].Length > 200 ? lines[i].Substring(0, 200) + "..." : lines[i];
            }
        }
        return null;
    }

    private SystemInfoModel GetSystemInfo()
    {
        return new SystemInfoModel
        {
            MachineName = Environment.MachineName,
            OperatingSystem = Environment.OSVersion.ToString(),
            ProcessorCount = Environment.ProcessorCount,
            DotNetVersion = Environment.Version.ToString(),
            Is64BitOperatingSystem = Environment.Is64BitOperatingSystem,
            Is64BitProcess = Environment.Is64BitProcess,
            ApplicationVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown",
            BaseDirectory = AppDomain.CurrentDomain.BaseDirectory,
            CurrentDirectory = Environment.CurrentDirectory
        };
    }

    private HealthStatus CalculateOverallStatus(SystemDiagnosticsReport report)
    {
        var statuses = new[]
        {
            report.DatabaseHealth.Status,
            report.WebSocketHealth.Status,
            report.PortAvailability.Status,
            report.CertificateStatus.Status,
            report.ClientStatistics.Status,
            report.PerformanceMetrics.Status,
            report.LogAnalysis.Status
        };

        if (statuses.Any(s => s == HealthStatus.Critical))
            return HealthStatus.Critical;
        if (statuses.Any(s => s == HealthStatus.Warning))
            return HealthStatus.Warning;

        return HealthStatus.Healthy;
    }

    /// <summary>
    /// Export diagnostics report to formatted text
    /// </summary>
    public string ExportToText(SystemDiagnosticsReport report)
    {
        return _exporter.ExportToText(report);
    }
}
