using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Server.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.IO;
using System.Windows;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// ViewModel for System Diagnostics window
/// </summary>
public partial class SystemDiagnosticsViewModel : ObservableObject
{
    private readonly SystemDiagnosticsService _diagnosticsService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<SystemDiagnosticsViewModel> _logger;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private SystemDiagnosticsReport? _currentReport;

    [ObservableProperty]
    private string _overallStatus = "Unknown";

    [ObservableProperty]
    private string _overallStatusColor = "#808080";

    // Database Health Properties
    [ObservableProperty]
    private string _databaseStatus = "Unknown";

    [ObservableProperty]
    private string _databaseStatusColor = "#808080";

    [ObservableProperty]
    private bool _databaseCanConnect;

    [ObservableProperty]
    private string _databaseProvider = "N/A";

    [ObservableProperty]
    private string _databasePath = "N/A";

    [ObservableProperty]
    private string _databaseSize = "N/A";

    [ObservableProperty]
    private string _databaseTableCounts = string.Empty;

    [ObservableProperty]
    private string _lastBackupDate = "Never";

    // WebSocket Health Properties
    [ObservableProperty]
    private string _webSocketStatus = "Unknown";

    [ObservableProperty]
    private string _webSocketStatusColor = "#808080";

    [ObservableProperty]
    private bool _webSocketIsRunning;

    [ObservableProperty]
    private string _webSocketUrl = "N/A";

    [ObservableProperty]
    private string _webSocketSslStatus = "Disabled";

    [ObservableProperty]
    private int _activeConnections;

    [ObservableProperty]
    private string _serverUptime = "N/A";

    // Port Availability Properties
    [ObservableProperty]
    private string _portStatus = "Unknown";

    [ObservableProperty]
    private string _portStatusColor = "#808080";

    [ObservableProperty]
    private int _configuredPort;

    [ObservableProperty]
    private bool _portAvailable;

    [ObservableProperty]
    private string _alternativePorts = string.Empty;

    // Certificate Properties
    [ObservableProperty]
    private string _certificateStatus = "Unknown";

    [ObservableProperty]
    private string _certificateStatusColor = "#808080";

    [ObservableProperty]
    private bool _sslEnabled;

    [ObservableProperty]
    private string _certificatePath = "N/A";

    [ObservableProperty]
    private string _certificateSubject = "N/A";

    [ObservableProperty]
    private string _certificateExpiration = "N/A";

    [ObservableProperty]
    private bool _certificateValid;

    // Client Statistics Properties
    [ObservableProperty]
    private string _clientStatsStatus = "Unknown";

    [ObservableProperty]
    private string _clientStatsStatusColor = "#808080";

    [ObservableProperty]
    private int _totalClients;

    [ObservableProperty]
    private int _onlineClients;

    [ObservableProperty]
    private int _offlineClients;

    [ObservableProperty]
    private int _disconnectedClients;

    // Performance Metrics Properties
    [ObservableProperty]
    private string _performanceStatus = "Unknown";

    [ObservableProperty]
    private string _performanceStatusColor = "#808080";

    [ObservableProperty]
    private string _cpuUsage = "N/A";

    [ObservableProperty]
    private string _memoryUsage = "N/A";

    [ObservableProperty]
    private int _threadCount;

    [ObservableProperty]
    private string _diskUsage = "N/A";

    // Log Analysis Properties
    [ObservableProperty]
    private string _logStatus = "Unknown";

    [ObservableProperty]
    private string _logStatusColor = "#808080";

    [ObservableProperty]
    private int _logFilesCount;

    [ObservableProperty]
    private string _logTotalSize = "N/A";

    [ObservableProperty]
    private int _errorsLastHour;

    [ObservableProperty]
    private int _warningsLastHour;

    [ObservableProperty]
    private int _errorsToday;

    [ObservableProperty]
    private int _warningsToday;

    [ObservableProperty]
    private string _lastCriticalError = "None";

    // System Info Properties
    [ObservableProperty]
    private string _machineName = "N/A";

    [ObservableProperty]
    private string _operatingSystem = "N/A";

    [ObservableProperty]
    private string _processorInfo = "N/A";

    [ObservableProperty]
    private string _dotNetVersion = "N/A";

    [ObservableProperty]
    private string _appVersion = "N/A";

    public SystemDiagnosticsViewModel(
        SystemDiagnosticsService diagnosticsService,
        IDialogService dialogService,
        ILogger<SystemDiagnosticsViewModel> logger)
    {
        _diagnosticsService = diagnosticsService;
        _dialogService = dialogService;
        _logger = logger;

        // Run diagnostics on initialization
        _ = RefreshDiagnosticsAsync();
    }

    [RelayCommand]
    private async Task RefreshDiagnosticsAsync()
    {
        IsLoading = true;
        StatusMessage = "Running diagnostics...";

        try
        {
            _logger.LogInformation("Refreshing system diagnostics");

            var report = await _diagnosticsService.GetDiagnosticsAsync();
            CurrentReport = report;

            // Update all properties from report
            UpdateFromReport(report);

            StatusMessage = $"Diagnostics completed - Overall status: {report.OverallStatus}";
            _logger.LogInformation("Diagnostics refresh completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh diagnostics");
            StatusMessage = $"Error: {ex.Message}";
            await _dialogService.ShowErrorAsync($"Failed to refresh diagnostics:\n\n{ex.Message}", "Diagnostics Error");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CopyToClipboardAsync()
    {
        if (CurrentReport == null)
        {
            await _dialogService.ShowWarningAsync("No diagnostics data available. Please refresh first.", "Copy to Clipboard");
            return;
        }

        try
        {
            var textReport = _diagnosticsService.ExportToText(CurrentReport);
            Clipboard.SetText(textReport);

            StatusMessage = "Diagnostics copied to clipboard";
            await _dialogService.ShowInformationAsync("Diagnostics report copied to clipboard successfully.", "Copy to Clipboard");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy diagnostics to clipboard");
            StatusMessage = $"Failed to copy: {ex.Message}";
            await _dialogService.ShowErrorAsync($"Failed to copy to clipboard:\n\n{ex.Message}", "Copy Error");
        }
    }

    [RelayCommand]
    private async Task ExportToFileAsync()
    {
        if (CurrentReport == null)
        {
            await _dialogService.ShowWarningAsync("No diagnostics data available. Please refresh first.", "Export Diagnostics");
            return;
        }

        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                DefaultExt = ".txt",
                FileName = $"diagnostics_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (dialog.ShowDialog() == true)
            {
                var extension = Path.GetExtension(dialog.FileName).ToLowerInvariant();

                if (extension == ".json")
                {
                    // Export as JSON
                    var json = JsonConvert.SerializeObject(CurrentReport, Formatting.Indented);
                    await File.WriteAllTextAsync(dialog.FileName, json);
                }
                else
                {
                    // Export as text
                    var textReport = _diagnosticsService.ExportToText(CurrentReport);
                    await File.WriteAllTextAsync(dialog.FileName, textReport);
                }

                StatusMessage = $"Diagnostics exported to {Path.GetFileName(dialog.FileName)}";
                await _dialogService.ShowInformationAsync($"Diagnostics report exported successfully to:\n\n{dialog.FileName}", "Export Successful");

                _logger.LogInformation("Diagnostics exported to {FilePath}", dialog.FileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export diagnostics");
            StatusMessage = $"Export failed: {ex.Message}";
            await _dialogService.ShowErrorAsync($"Failed to export diagnostics:\n\n{ex.Message}", "Export Error");
        }
    }

    private void UpdateFromReport(SystemDiagnosticsReport report)
    {
        // Overall Status
        OverallStatus = report.OverallStatus.ToString();
        OverallStatusColor = GetStatusColor(report.OverallStatus);

        // Database Health
        DatabaseStatus = report.DatabaseHealth.Status.ToString();
        DatabaseStatusColor = GetStatusColor(report.DatabaseHealth.Status);
        DatabaseCanConnect = report.DatabaseHealth.CanConnect;
        DatabaseProvider = report.DatabaseHealth.ProviderName;
        DatabasePath = report.DatabaseHealth.DatabasePath ?? "N/A";
        DatabaseSize = $"{report.DatabaseHealth.DatabaseSize / 1024.0:F2} KB";
        DatabaseTableCounts = string.Join("\n", report.DatabaseHealth.TableCounts.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
        LastBackupDate = report.DatabaseHealth.LastBackupDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never";

        // WebSocket Health
        WebSocketStatus = report.WebSocketHealth.Status.ToString();
        WebSocketStatusColor = GetStatusColor(report.WebSocketHealth.Status);
        WebSocketIsRunning = report.WebSocketHealth.IsRunning;
        WebSocketUrl = report.WebSocketHealth.ListeningUrl;
        WebSocketSslStatus = report.WebSocketHealth.EnableSsl ? "Enabled" : "Disabled";
        ActiveConnections = report.WebSocketHealth.ActiveConnections;
        ServerUptime = $"{report.WebSocketHealth.Uptime.Days}d {report.WebSocketHealth.Uptime.Hours}h {report.WebSocketHealth.Uptime.Minutes}m";

        // Port Availability
        PortStatus = report.PortAvailability.Status.ToString();
        PortStatusColor = GetStatusColor(report.PortAvailability.Status);
        ConfiguredPort = report.PortAvailability.ConfiguredPort;
        PortAvailable = report.PortAvailability.IsConfiguredPortAvailable;
        AlternativePorts = report.PortAvailability.AvailablePorts.Any()
            ? string.Join(", ", report.PortAvailability.AvailablePorts)
            : "None available";

        // Certificate Status
        CertificateStatus = report.CertificateStatus.Status.ToString();
        CertificateStatusColor = GetStatusColor(report.CertificateStatus.Status);
        SslEnabled = report.CertificateStatus.SslEnabled;
        CertificatePath = report.CertificateStatus.CertificatePath ?? "N/A";
        CertificateSubject = report.CertificateStatus.Subject ?? "N/A";
        CertificateExpiration = report.CertificateStatus.ExpirationDate?.ToString("yyyy-MM-dd") ?? "N/A";
        CertificateValid = report.CertificateStatus.IsValid;

        // Client Statistics
        ClientStatsStatus = report.ClientStatistics.Status.ToString();
        ClientStatsStatusColor = GetStatusColor(report.ClientStatistics.Status);
        TotalClients = report.ClientStatistics.TotalClients;
        OnlineClients = report.ClientStatistics.OnlineClients;
        OfflineClients = report.ClientStatistics.OfflineClients;
        DisconnectedClients = report.ClientStatistics.DisconnectedClients;

        // Performance Metrics
        PerformanceStatus = report.PerformanceMetrics.Status.ToString();
        PerformanceStatusColor = GetStatusColor(report.PerformanceMetrics.Status);
        CpuUsage = $"{report.PerformanceMetrics.CpuUsage:F2}%";
        MemoryUsage = $"{report.PerformanceMetrics.MemoryUsageMB:F2} MB";
        ThreadCount = report.PerformanceMetrics.ThreadCount;
        DiskUsage = $"{report.PerformanceMetrics.DiskUsagePercent:F2}% ({report.PerformanceMetrics.DiskFreeGB:F2} GB free)";

        // Log Analysis
        LogStatus = report.LogAnalysis.Status.ToString();
        LogStatusColor = GetStatusColor(report.LogAnalysis.Status);
        LogFilesCount = report.LogAnalysis.LogFilesCount;
        LogTotalSize = $"{report.LogAnalysis.TotalLogSizeMB:F2} MB";
        ErrorsLastHour = report.LogAnalysis.ErrorsLastHour;
        WarningsLastHour = report.LogAnalysis.WarningsLastHour;
        ErrorsToday = report.LogAnalysis.ErrorsToday;
        WarningsToday = report.LogAnalysis.WarningsToday;
        LastCriticalError = string.IsNullOrWhiteSpace(report.LogAnalysis.LastCriticalError) ? "None" : report.LogAnalysis.LastCriticalError;

        // System Info
        MachineName = report.SystemInfo.MachineName;
        OperatingSystem = report.SystemInfo.OperatingSystem;
        ProcessorInfo = $"{report.SystemInfo.ProcessorCount} cores";
        DotNetVersion = report.SystemInfo.DotNetVersion;
        AppVersion = report.SystemInfo.ApplicationVersion;
    }

    private string GetStatusColor(HealthStatus status)
    {
        return status switch
        {
            HealthStatus.Healthy => "#4CAF50",   // Green
            HealthStatus.Warning => "#FF9800",   // Orange
            HealthStatus.Critical => "#F44336",  // Red
            _ => "#808080"                       // Gray
        };
    }
}
