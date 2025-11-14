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

    public ObservableCollection<LogEntry> Logs { get; } = new();
    public ObservableCollection<string> AvailableClients { get; } = new();
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
        // Add to collection on UI thread
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
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

        // Filter by log level
        return logEntry.Level switch
        {
            Core.Models.LogLevel.Debug => FilterDebug,
            Core.Models.LogLevel.Info => FilterInfo,
            Core.Models.LogLevel.Warning => FilterWarning,
            Core.Models.LogLevel.Error => FilterError,
            Core.Models.LogLevel.Critical => FilterCritical,
            _ => true
        };
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

    private void UpdateStatusText()
    {
        var filteredCount = FilteredLogs.Cast<LogEntry>().Count();
        var totalCount = Logs.Count;

        if (filteredCount != totalCount)
        {
            StatusText = $"Showing {filteredCount} of {totalCount} logs";
        }
        else
        {
            StatusText = $"{totalCount} log{(totalCount != 1 ? "s" : "")}";
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

            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                Logs.Clear();
                foreach (var log in logs)
                {
                    Logs.Add(log);
                }
                LogCount = Logs.Count;
            });
        });
    }

    private async Task RefreshClientsAsync()
    {
        try
        {
            var clients = await _clientService.GetAllClientsAsync();

            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
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
    private async Task ExportLogs()
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
                StatusText = "Exporting logs...";

                // Get filtered logs
                var logsToExport = FilteredLogs.Cast<LogEntry>().ToList();
                var exportText = _logStorageService.ExportLogs(logsToExport);

                await File.WriteAllTextAsync(saveFileDialog.FileName, exportText);

                StatusText = $"Exported {logsToExport.Count} logs to {Path.GetFileName(saveFileDialog.FileName)}";
                _logger.LogInformation("Exported {Count} logs to {FileName}", logsToExport.Count, saveFileDialog.FileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export logs");
            StatusText = $"Export error: {ex.Message}";
        }
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
        }

        _disposed = true;
    }
}
