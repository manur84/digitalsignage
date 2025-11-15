using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Data;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// ViewModel for the Log Viewer tab
/// </summary>
public partial class LogViewerViewModel : ObservableObject, IDisposable
{
    private readonly LogStorageService _logStorageService;
    private readonly IClientService _clientService;
    private readonly ILogger<LogViewerViewModel> _logger;
    private bool _disposed = false;
    private System.Timers.Timer? _searchDebounceTimer;

    [ObservableProperty]
    private string? _selectedClientId;

    [ObservableProperty]
    private bool _filterDebug = true;

    [ObservableProperty]
    private bool _filterInfo = true;

    [ObservableProperty]
    private bool _filterWarning = true;

    [ObservableProperty]
    private bool _filterError = true;

    [ObservableProperty]
    private bool _filterCritical = true;

    [ObservableProperty]
    private bool _autoScroll = true;

    [ObservableProperty]
    private int _logCount = 0;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private DateTime? _dateFrom;

    [ObservableProperty]
    private DateTime? _dateTo;

    [ObservableProperty]
    private string _sourceFilter = string.Empty;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _caseSensitiveSearch = false;

    [ObservableProperty]
    private int _searchResultCount = 0;

    [ObservableProperty]
    private string _selectedLogLevel = "All";

    [ObservableProperty]
    private bool _isExporting = false;

    public ObservableCollection<LogEntry> Logs { get; } = new();
    public ObservableCollection<string> AvailableClients { get; } = new();
    public ObservableCollection<string> AvailableSources { get; } = new();
    public ObservableCollection<string> LogLevels { get; } = new() { "All", "Debug", "Info", "Warning", "Error", "Critical" };
    public ICollectionView FilteredLogs { get; }

    public LogViewerViewModel(
        LogStorageService logStorageService,
        IClientService clientService,
        ILogger<LogViewerViewModel> logger)
    {
        _logStorageService = logStorageService ?? throw new ArgumentNullException(nameof(logStorageService));
        _clientService = clientService ?? throw new ArgumentNullException(nameof(clientService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Setup filtered collection view
        FilteredLogs = CollectionViewSource.GetDefaultView(Logs);
        FilteredLogs.Filter = FilterLogEntry;

        // Subscribe to log events
        _logStorageService.LogReceived += OnLogReceived;

        // Initial load
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            // Load existing logs
            await RefreshLogsAsync();

            // Load available clients
            await RefreshClientsAsync();

            StatusText = "Log viewer initialized";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize log viewer");
            StatusText = $"Error: {ex.Message}";
        }
    }

    private void OnLogReceived(object? sender, LogEntry logEntry)
    {
        // Add to collection on UI thread - check if already on UI thread first
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null) return;

        if (dispatcher.CheckAccess())
        {
            Logs.Add(logEntry);
            LogCount = Logs.Count;

            // Update available clients if needed
            if (!AvailableClients.Contains(logEntry.ClientId))
            {
                AvailableClients.Add(logEntry.ClientId);
            }

            // Refresh filter
            FilteredLogs.Refresh();
        }
        else
        {
            dispatcher.InvokeAsync(() =>
            {
                Logs.Add(logEntry);
                LogCount = Logs.Count;

                // Update available clients if needed
                if (!AvailableClients.Contains(logEntry.ClientId))
                {
                    AvailableClients.Add(logEntry.ClientId);
                }

                // Refresh filter
                FilteredLogs.Refresh();
            });
        }
    }

    private bool FilterLogEntry(object obj)
    {
        if (obj is not LogEntry logEntry)
            return false;

        // Filter by client
        if (!string.IsNullOrEmpty(SelectedClientId) &&
            SelectedClientId != "All Clients" &&
            logEntry.ClientId != SelectedClientId)
        {
            return false;
        }

        // Filter by log level (single selection dropdown)
        if (SelectedLogLevel != "All")
        {
            var levelMatch = SelectedLogLevel.ToLower() switch
            {
                "debug" => logEntry.Level == Core.Models.LogLevel.Debug,
                "info" => logEntry.Level == Core.Models.LogLevel.Info,
                "warning" => logEntry.Level == Core.Models.LogLevel.Warning,
                "error" => logEntry.Level == Core.Models.LogLevel.Error,
                "critical" => logEntry.Level == Core.Models.LogLevel.Critical,
                _ => true
            };

            if (!levelMatch)
                return false;
        }
        else
        {
            // Filter by individual checkboxes (backwards compatibility)
            var levelPass = logEntry.Level switch
            {
                Core.Models.LogLevel.Debug => FilterDebug,
                Core.Models.LogLevel.Info => FilterInfo,
                Core.Models.LogLevel.Warning => FilterWarning,
                Core.Models.LogLevel.Error => FilterError,
                Core.Models.LogLevel.Critical => FilterCritical,
                _ => true
            };

            if (!levelPass)
                return false;
        }

        // Filter by date range
        if (DateFrom.HasValue && logEntry.Timestamp < DateFrom.Value)
            return false;

        if (DateTo.HasValue && logEntry.Timestamp > DateTo.Value.AddDays(1).AddSeconds(-1))
            return false;

        // Filter by source
        if (!string.IsNullOrWhiteSpace(SourceFilter) &&
            !logEntry.Source.Contains(SourceFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Filter by search text
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var comparison = CaseSensitiveSearch ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var matches = logEntry.Message.Contains(SearchText, comparison) ||
                         logEntry.Source.Contains(SearchText, comparison) ||
                         logEntry.ClientName.Contains(SearchText, comparison) ||
                         (!string.IsNullOrEmpty(logEntry.Exception) && logEntry.Exception.Contains(SearchText, comparison));

            if (!matches)
                return false;
        }

        return true;
    }

    partial void OnSelectedClientIdChanged(string? value)
    {
        FilteredLogs.Refresh();
        UpdateStatusText();
    }

    partial void OnFilterDebugChanged(bool value)
    {
        FilteredLogs.Refresh();
        UpdateStatusText();
    }

    partial void OnFilterInfoChanged(bool value)
    {
        FilteredLogs.Refresh();
        UpdateStatusText();
    }

    partial void OnFilterWarningChanged(bool value)
    {
        FilteredLogs.Refresh();
        UpdateStatusText();
    }

    partial void OnFilterErrorChanged(bool value)
    {
        FilteredLogs.Refresh();
        UpdateStatusText();
    }

    partial void OnFilterCriticalChanged(bool value)
    {
        FilteredLogs.Refresh();
        UpdateStatusText();
    }

    partial void OnDateFromChanged(DateTime? value)
    {
        FilteredLogs.Refresh();
        UpdateStatusText();
    }

    partial void OnDateToChanged(DateTime? value)
    {
        FilteredLogs.Refresh();
        UpdateStatusText();
    }

    partial void OnSourceFilterChanged(string value)
    {
        FilteredLogs.Refresh();
        UpdateStatusText();
    }

    partial void OnSearchTextChanged(string value)
    {
        // Debounce search to avoid excessive filtering
        _searchDebounceTimer?.Stop();
        _searchDebounceTimer = new System.Timers.Timer(300);
        _searchDebounceTimer.Elapsed += (s, e) =>
        {
            _searchDebounceTimer?.Stop();
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            dispatcher?.InvokeAsync(() =>
            {
                FilteredLogs.Refresh();
                UpdateStatusText();
            });
        };
        _searchDebounceTimer.Start();
    }

    partial void OnCaseSensitiveSearchChanged(bool value)
    {
        FilteredLogs.Refresh();
        UpdateStatusText();
    }

    partial void OnSelectedLogLevelChanged(string value)
    {
        FilteredLogs.Refresh();
        UpdateStatusText();
    }

    private void UpdateStatusText()
    {
        var filteredCount = FilteredLogs.Cast<LogEntry>().Count();
        var totalCount = Logs.Count;
        SearchResultCount = filteredCount;

        var activeFilters = new List<string>();

        if (!string.IsNullOrWhiteSpace(SearchText))
            activeFilters.Add("search");

        if (DateFrom.HasValue || DateTo.HasValue)
            activeFilters.Add("date range");

        if (!string.IsNullOrWhiteSpace(SourceFilter))
            activeFilters.Add("source");

        if (SelectedLogLevel != "All")
            activeFilters.Add("level");

        if (!string.IsNullOrEmpty(SelectedClientId) && SelectedClientId != "All Clients")
            activeFilters.Add("client");

        var filterText = activeFilters.Any() ? $" (filtered by: {string.Join(", ", activeFilters)})" : "";

        if (filteredCount != totalCount)
        {
            StatusText = $"Showing {filteredCount} of {totalCount} logs{filterText}";
        }
        else
        {
            StatusText = $"{totalCount} log{(totalCount != 1 ? "s" : "")}{filterText}";
        }
    }

    [RelayCommand]
    private async Task RefreshLogs()
    {
        try
        {
            StatusText = "Refreshing logs...";
            await RefreshLogsAsync();
            UpdateStatusText();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh logs");
            StatusText = $"Error: {ex.Message}";
        }
    }

    private async Task RefreshLogsAsync()
    {
        await Task.Run(() =>
        {
            var logs = _logStorageService.GetAllLogs()
                .OrderBy(log => log.Timestamp)
                .ToList();

            // Extract unique sources
            var sources = logs
                .Where(l => !string.IsNullOrWhiteSpace(l.Source))
                .Select(l => l.Source)
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            // Check if already on UI thread to avoid unnecessary context switch
            if (dispatcher.CheckAccess())
            {
                Logs.Clear();
                foreach (var log in logs)
                {
                    Logs.Add(log);
                }
                LogCount = Logs.Count;

                // Update available sources
                AvailableSources.Clear();
                AvailableSources.Add(string.Empty); // "All Sources"
                foreach (var source in sources)
                {
                    AvailableSources.Add(source);
                }
            }
            else
            {
                dispatcher.InvokeAsync(() =>
                {
                    Logs.Clear();
                    foreach (var log in logs)
                    {
                        Logs.Add(log);
                    }
                    LogCount = Logs.Count;

                    // Update available sources
                    AvailableSources.Clear();
                    AvailableSources.Add(string.Empty); // "All Sources"
                    foreach (var source in sources)
                    {
                        AvailableSources.Add(source);
                    }
                });
            }
        });
    }

    private async Task RefreshClientsAsync()
    {
        try
        {
            var clientsResult = await _clientService.GetAllClientsAsync();

            if (clientsResult.IsFailure)
            {
                _logger.LogError("Failed to load clients: {ErrorMessage}", clientsResult.ErrorMessage);
                return;
            }

            var clients = clientsResult.Value;

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            // Check if already on UI thread to avoid unnecessary context switch
            if (dispatcher.CheckAccess())
            {
                AvailableClients.Clear();
                AvailableClients.Add("All Clients");

                foreach (var client in clients.OrderBy(c => c.Name))
                {
                    AvailableClients.Add(client.Id);
                }

                // Set default selection
                if (SelectedClientId == null && AvailableClients.Any())
                {
                    SelectedClientId = "All Clients";
                }
            }
            else
            {
                await dispatcher.InvokeAsync(() =>
                {
                    AvailableClients.Clear();
                    AvailableClients.Add("All Clients");

                    foreach (var client in clients.OrderBy(c => c.Name))
                    {
                        AvailableClients.Add(client.Id);
                    }

                    // Set default selection
                    if (SelectedClientId == null && AvailableClients.Any())
                    {
                        SelectedClientId = "All Clients";
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh clients");
        }
    }

    [RelayCommand]
    private void ClearLogs()
    {
        try
        {
            if (!string.IsNullOrEmpty(SelectedClientId) && SelectedClientId != "All Clients")
            {
                _logStorageService.ClearClientLogs(SelectedClientId);
                StatusText = $"Cleared logs for {SelectedClientId}";
            }
            else
            {
                _logStorageService.ClearAllLogs();
                StatusText = "All logs cleared";
            }

            Logs.Clear();
            LogCount = 0;
            FilteredLogs.Refresh();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear logs");
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ExportToCsv()
    {
        try
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = "csv",
                FileName = $"digitalsignage-logs-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                IsExporting = true;
                StatusText = "Exporting to CSV...";

                await Task.Run(async () =>
                {
                    var logsToExport = FilteredLogs.Cast<LogEntry>().ToList();
                    var csv = new StringBuilder();

                    // Add header with filter information
                    csv.AppendLine($"# Digital Signage Logs Export");
                    csv.AppendLine($"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    csv.AppendLine($"# Total Logs: {logsToExport.Count}");

                    if (DateFrom.HasValue || DateTo.HasValue)
                    {
                        var dateRange = $"{DateFrom?.ToString("yyyy-MM-dd") ?? "Any"} to {DateTo?.ToString("yyyy-MM-dd") ?? "Any"}";
                        csv.AppendLine($"# Date Range: {dateRange}");
                    }

                    if (SelectedLogLevel != "All")
                        csv.AppendLine($"# Log Level: {SelectedLogLevel}");

                    csv.AppendLine();

                    // CSV header
                    csv.AppendLine("Timestamp,Level,Client,Source,Message,Exception");

                    // CSV data
                    foreach (var log in logsToExport.OrderBy(l => l.Timestamp))
                    {
                        csv.AppendLine($"\"{log.Timestamp:yyyy-MM-dd HH:mm:ss.fff}\"," +
                                     $"\"{log.Level}\"," +
                                     $"\"{EscapeCsv(log.ClientName)}\"," +
                                     $"\"{EscapeCsv(log.Source)}\"," +
                                     $"\"{EscapeCsv(log.Message)}\"," +
                                     $"\"{EscapeCsv(log.Exception ?? string.Empty)}\"");
                    }

                    await File.WriteAllTextAsync(saveFileDialog.FileName, csv.ToString());

                    var dispatcher = System.Windows.Application.Current?.Dispatcher;
                    dispatcher?.InvokeAsync(() =>
                    {
                        StatusText = $"Exported {logsToExport.Count} logs to {Path.GetFileName(saveFileDialog.FileName)}";
                        _logger.LogInformation("Exported {Count} logs to CSV: {FileName}", logsToExport.Count, saveFileDialog.FileName);
                    });
                });

                IsExporting = false;
            }
        }
        catch (Exception ex)
        {
            IsExporting = false;
            _logger.LogError(ex, "Failed to export logs to CSV");
            StatusText = $"Export error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ExportToText()
    {
        try
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|Log files (*.log)|*.log|All files (*.*)|*.*",
                DefaultExt = "txt",
                FileName = $"digitalsignage-logs-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                IsExporting = true;
                StatusText = "Exporting to text file...";

                await Task.Run(async () =>
                {
                    var logsToExport = FilteredLogs.Cast<LogEntry>().ToList();
                    var exportText = _logStorageService.ExportLogs(logsToExport);

                    // Add header with filter information
                    var header = new StringBuilder();
                    header.AppendLine("=".PadRight(80, '='));
                    header.AppendLine(" Digital Signage Logs Export");
                    header.AppendLine("=".PadRight(80, '='));
                    header.AppendLine($" Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    header.AppendLine($" Total Logs: {logsToExport.Count}");

                    if (DateFrom.HasValue || DateTo.HasValue)
                    {
                        var dateRange = $"{DateFrom?.ToString("yyyy-MM-dd") ?? "Any"} to {DateTo?.ToString("yyyy-MM-dd") ?? "Any"}";
                        header.AppendLine($" Date Range: {dateRange}");
                    }

                    if (SelectedLogLevel != "All")
                        header.AppendLine($" Log Level: {SelectedLogLevel}");

                    header.AppendLine("=".PadRight(80, '='));
                    header.AppendLine();

                    var fullExport = header.ToString() + exportText;
                    await File.WriteAllTextAsync(saveFileDialog.FileName, fullExport);

                    var dispatcher = System.Windows.Application.Current?.Dispatcher;
                    dispatcher?.InvokeAsync(() =>
                    {
                        StatusText = $"Exported {logsToExport.Count} logs to {Path.GetFileName(saveFileDialog.FileName)}";
                        _logger.LogInformation("Exported {Count} logs to text: {FileName}", logsToExport.Count, saveFileDialog.FileName);
                    });
                });

                IsExporting = false;
            }
        }
        catch (Exception ex)
        {
            IsExporting = false;
            _logger.LogError(ex, "Failed to export logs to text");
            StatusText = $"Export error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ExportToJson()
    {
        try
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = "json",
                FileName = $"digitalsignage-logs-{DateTime.Now:yyyyMMdd-HHmmss}.json"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                IsExporting = true;
                StatusText = "Exporting to JSON...";

                await Task.Run(async () =>
                {
                    var logsToExport = FilteredLogs.Cast<LogEntry>().OrderBy(l => l.Timestamp).ToList();

                    var exportData = new
                    {
                        ExportInfo = new
                        {
                            Generated = DateTime.Now,
                            TotalLogs = logsToExport.Count,
                            DateFrom = DateFrom?.ToString("yyyy-MM-dd"),
                            DateTo = DateTo?.ToString("yyyy-MM-dd"),
                            LogLevel = SelectedLogLevel,
                            Client = SelectedClientId,
                            SearchText = SearchText
                        },
                        Logs = logsToExport.Select(log => new
                        {
                            log.Timestamp,
                            Level = log.Level.ToString(),
                            log.ClientId,
                            log.ClientName,
                            log.Source,
                            log.Message,
                            log.Exception
                        })
                    };

                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true
                    };

                    var json = JsonSerializer.Serialize(exportData, options);
                    await File.WriteAllTextAsync(saveFileDialog.FileName, json);

                    var dispatcher = System.Windows.Application.Current?.Dispatcher;
                    dispatcher?.InvokeAsync(() =>
                    {
                        StatusText = $"Exported {logsToExport.Count} logs to {Path.GetFileName(saveFileDialog.FileName)}";
                        _logger.LogInformation("Exported {Count} logs to JSON: {FileName}", logsToExport.Count, saveFileDialog.FileName);
                    });
                });

                IsExporting = false;
            }
        }
        catch (Exception ex)
        {
            IsExporting = false;
            _logger.LogError(ex, "Failed to export logs to JSON");
            StatusText = $"Export error: {ex.Message}";
        }
    }

    private string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        // Escape double quotes and remove newlines
        return value.Replace("\"", "\"\"").Replace("\r\n", " ").Replace("\n", " ");
    }

    [RelayCommand]
    private void SelectAllFilters()
    {
        FilterDebug = true;
        FilterInfo = true;
        FilterWarning = true;
        FilterError = true;
        FilterCritical = true;
        StatusText = "All log levels enabled";
    }

    [RelayCommand]
    private void DeselectAllFilters()
    {
        FilterDebug = false;
        FilterInfo = false;
        FilterWarning = false;
        FilterError = false;
        FilterCritical = false;
        StatusText = "All log levels disabled";
    }

    [RelayCommand]
    private void ShowErrorsOnly()
    {
        FilterDebug = false;
        FilterInfo = false;
        FilterWarning = false;
        FilterError = true;
        FilterCritical = true;
        StatusText = "Showing errors and critical only";
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SelectedClientId = "All Clients";
        SelectedLogLevel = "All";
        DateFrom = null;
        DateTo = null;
        SourceFilter = string.Empty;
        SearchText = string.Empty;
        CaseSensitiveSearch = false;

        // Reset level checkboxes to all enabled
        FilterDebug = true;
        FilterInfo = true;
        FilterWarning = true;
        FilterError = true;
        FilterCritical = true;

        StatusText = "All filters cleared";
    }

    /// <summary>
    /// Get statistics about current logs
    /// </summary>
    public LogStatistics GetStatistics()
    {
        return _logStorageService.GetStatistics();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Unregister event handler
            _logStorageService.LogReceived -= OnLogReceived;

            // Dispose timer
            _searchDebounceTimer?.Stop();
            _searchDebounceTimer?.Dispose();
        }

        _disposed = true;
    }
}
