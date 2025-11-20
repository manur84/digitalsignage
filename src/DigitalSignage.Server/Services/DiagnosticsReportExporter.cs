using System.Text;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Exports diagnostics reports to various formats
/// </summary>
public class DiagnosticsReportExporter
{
    /// <summary>
    /// Export diagnostics report to formatted text
    /// </summary>
    public string ExportToText(SystemDiagnosticsReport report)
    {
        var sb = new StringBuilder();

        sb.AppendLine("═══════════════════════════════════════════════════════");
        sb.AppendLine("   DIGITAL SIGNAGE SERVER - SYSTEM DIAGNOSTICS REPORT");
        sb.AppendLine("═══════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine($"Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Overall Status: {report.OverallStatus}");
        sb.AppendLine();

        // Database Health
        AppendDatabaseHealth(sb, report.DatabaseHealth);

        // WebSocket Health
        AppendWebSocketHealth(sb, report.WebSocketHealth);

        // Port Availability
        AppendPortAvailability(sb, report.PortAvailability);

        // Certificate Status
        AppendCertificateStatus(sb, report.CertificateStatus);

        // Client Statistics
        AppendClientStatistics(sb, report.ClientStatistics);

        // Performance Metrics
        AppendPerformanceMetrics(sb, report.PerformanceMetrics);

        // Log Analysis
        AppendLogAnalysis(sb, report.LogAnalysis);

        // System Information
        AppendSystemInfo(sb, report.SystemInfo);

        sb.AppendLine("═══════════════════════════════════════════════════════");
        sb.AppendLine("                     END OF REPORT");
        sb.AppendLine("═══════════════════════════════════════════════════════");

        return sb.ToString();
    }

    private void AppendDatabaseHealth(StringBuilder sb, DatabaseHealthInfo health)
    {
        sb.AppendLine("───────────────────────────────────────────────────────");
        sb.AppendLine("DATABASE HEALTH");
        sb.AppendLine("───────────────────────────────────────────────────────");
        sb.AppendLine($"Status: {health.Status}");
        sb.AppendLine($"Can Connect: {health.CanConnect}");
        sb.AppendLine($"Provider: {health.ProviderName}");
        sb.AppendLine($"Path: {health.DatabasePath ?? "N/A"}");
        sb.AppendLine($"Size: {health.DatabaseSize / 1024.0:F2} KB");
        if (health.TableCounts.Any())
        {
            sb.AppendLine("Table Counts:");
            foreach (var kvp in health.TableCounts)
            {
                sb.AppendLine($"  • {kvp.Key}: {kvp.Value}");
            }
        }
        if (health.LastBackupDate.HasValue)
        {
            sb.AppendLine($"Last Backup: {health.LastBackupDate:yyyy-MM-dd HH:mm:ss}");
        }
        sb.AppendLine();
    }

    private void AppendWebSocketHealth(StringBuilder sb, WebSocketHealthInfo health)
    {
        sb.AppendLine("───────────────────────────────────────────────────────");
        sb.AppendLine("WEBSOCKET SERVER HEALTH");
        sb.AppendLine("───────────────────────────────────────────────────────");
        sb.AppendLine($"Status: {health.Status}");
        sb.AppendLine($"Running: {health.IsRunning}");
        sb.AppendLine($"Listening URL: {health.ListeningUrl}");
        sb.AppendLine($"SSL/TLS: {(health.EnableSsl ? "Enabled" : "Disabled")}");
        sb.AppendLine($"Active Connections: {health.ActiveConnections}");
        sb.AppendLine($"Uptime: {health.Uptime:dd\\.hh\\:mm\\:ss}");
        sb.AppendLine();
    }

    private void AppendPortAvailability(StringBuilder sb, PortAvailabilityInfo port)
    {
        sb.AppendLine("───────────────────────────────────────────────────────");
        sb.AppendLine("PORT AVAILABILITY");
        sb.AppendLine("───────────────────────────────────────────────────────");
        sb.AppendLine($"Status: {port.Status}");
        sb.AppendLine($"Configured Port: {port.ConfiguredPort}");
        sb.AppendLine($"Port Available: {port.IsConfiguredPortAvailable}");
        sb.AppendLine($"Current Active Port: {port.CurrentActivePort}");
        if (port.AvailablePorts.Any())
        {
            sb.AppendLine($"Available Alternative Ports: {string.Join(", ", port.AvailablePorts)}");
        }
        sb.AppendLine();
    }

    private void AppendCertificateStatus(StringBuilder sb, CertificateStatusInfo cert)
    {
        sb.AppendLine("───────────────────────────────────────────────────────");
        sb.AppendLine("CERTIFICATE STATUS");
        sb.AppendLine("───────────────────────────────────────────────────────");
        sb.AppendLine($"Status: {cert.Status}");
        sb.AppendLine($"SSL Enabled: {cert.SslEnabled}");
        if (cert.SslEnabled)
        {
            sb.AppendLine($"Path: {cert.CertificatePath ?? "N/A"}");
            sb.AppendLine($"Subject: {cert.Subject ?? "N/A"}");
            sb.AppendLine($"Issuer: {cert.Issuer ?? "N/A"}");
            if (cert.ExpirationDate.HasValue)
            {
                sb.AppendLine($"Expires: {cert.ExpirationDate:yyyy-MM-dd}");
            }
            sb.AppendLine($"Valid: {cert.IsValid}");
        }
        sb.AppendLine();
    }

    private void AppendClientStatistics(StringBuilder sb, ClientStatisticsInfo stats)
    {
        sb.AppendLine("───────────────────────────────────────────────────────");
        sb.AppendLine("CLIENT STATISTICS");
        sb.AppendLine("───────────────────────────────────────────────────────");
        sb.AppendLine($"Status: {stats.Status}");
        sb.AppendLine($"Total Clients: {stats.TotalClients}");
        sb.AppendLine($"Online: {stats.OnlineClients}");
        sb.AppendLine($"Offline: {stats.OfflineClients}");
        sb.AppendLine($"Disconnected: {stats.DisconnectedClients}");
        sb.AppendLine();
    }

    private void AppendPerformanceMetrics(StringBuilder sb, PerformanceMetricsInfo perf)
    {
        sb.AppendLine("───────────────────────────────────────────────────────");
        sb.AppendLine("PERFORMANCE METRICS");
        sb.AppendLine("───────────────────────────────────────────────────────");
        sb.AppendLine($"Status: {perf.Status}");
        sb.AppendLine($"CPU Usage: {perf.CpuUsage:F2}%");
        sb.AppendLine($"Memory Usage: {perf.MemoryUsageMB:F2} MB");
        sb.AppendLine($"Private Memory: {perf.PrivateMemoryMB:F2} MB");
        sb.AppendLine($"Thread Count: {perf.ThreadCount}");
        sb.AppendLine($"Disk Usage: {perf.DiskUsagePercent:F2}% ({perf.DiskFreeGB:F2} GB free)");
        sb.AppendLine();
    }

    private void AppendLogAnalysis(StringBuilder sb, LogAnalysisInfo log)
    {
        sb.AppendLine("───────────────────────────────────────────────────────");
        sb.AppendLine("LOG ANALYSIS");
        sb.AppendLine("───────────────────────────────────────────────────────");
        sb.AppendLine($"Status: {log.Status}");
        sb.AppendLine($"Log Files: {log.LogFilesCount}");
        sb.AppendLine($"Total Size: {log.TotalLogSizeMB:F2} MB");
        sb.AppendLine($"Errors (Last Hour): {log.ErrorsLastHour}");
        sb.AppendLine($"Warnings (Last Hour): {log.WarningsLastHour}");
        sb.AppendLine($"Errors (Today): {log.ErrorsToday}");
        sb.AppendLine($"Warnings (Today): {log.WarningsToday}");
        if (!string.IsNullOrWhiteSpace(log.LastCriticalError))
        {
            sb.AppendLine($"Last Critical Error: {log.LastCriticalError}");
        }
        sb.AppendLine();
    }

    private void AppendSystemInfo(StringBuilder sb, SystemInfoModel info)
    {
        sb.AppendLine("───────────────────────────────────────────────────────");
        sb.AppendLine("SYSTEM INFORMATION");
        sb.AppendLine("───────────────────────────────────────────────────────");
        sb.AppendLine($"Machine Name: {info.MachineName}");
        sb.AppendLine($"OS: {info.OperatingSystem}");
        sb.AppendLine($"Processors: {info.ProcessorCount}");
        sb.AppendLine($".NET Version: {info.DotNetVersion}");
        sb.AppendLine($"Application Version: {info.ApplicationVersion}");
        sb.AppendLine($"64-bit OS: {info.Is64BitOperatingSystem}");
        sb.AppendLine($"64-bit Process: {info.Is64BitProcess}");
        sb.AppendLine();
    }
}
