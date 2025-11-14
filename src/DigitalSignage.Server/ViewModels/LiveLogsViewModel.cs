using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// ViewModel for the Live Logs tab - displays real-time debug logs from the server
/// </summary>
public partial class LiveLogsViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<LiveLogsViewModel> _logger;
    private readonly IDialogService _dialogService;
    private bool _disposed = false;

    /// <summary>
    /// Observable collection of log messages for real-time display.
    /// This is populated by the UISink custom Serilog sink.
    /// </summary>
    public ObservableCollection<string> LogMessages { get; }

    [ObservableProperty]
    private bool _autoScroll = true;

    [ObservableProperty]
    private int _logCount = 0;

    [ObservableProperty]
    private string _statusText = "Live logs - ready";

    public LiveLogsViewModel(ILogger<LiveLogsViewModel> logger, IDialogService dialogService) : this(logger, dialogService, new ObservableCollection<string>())
    {
    }

    public LiveLogsViewModel(ILogger<LiveLogsViewModel> logger, IDialogService dialogService, ObservableCollection<string> sharedLogCollection)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        LogMessages = sharedLogCollection ?? throw new ArgumentNullException(nameof(sharedLogCollection));

        // Subscribe to collection changes to update count
        LogMessages.CollectionChanged += OnLogMessagesCollectionChanged;

        _logger.LogInformation("Live Logs viewer initialized with {Count} existing logs", LogMessages.Count);
    }

    private void OnLogMessagesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        LogCount = LogMessages.Count;
        StatusText = $"{LogCount} log entries";
    }

    [RelayCommand]
    private async Task ClearLogs()
    {
        try
        {
            var count = LogMessages.Count;
            LogMessages.Clear();
            LogCount = 0;
            StatusText = $"Cleared {count} log entries";
            _logger.LogInformation("Live logs cleared by user");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear live logs");
            await _dialogService.ShowErrorAsync($"Failed to clear logs: {ex.Message}", "Error");
        }
    }

    [RelayCommand]
    private void ToggleAutoScroll()
    {
        AutoScroll = !AutoScroll;
        StatusText = AutoScroll ? "Auto-scroll enabled" : "Auto-scroll disabled";
    }

    /// <summary>
    /// Get the log messages collection for binding to Serilog UISink
    /// </summary>
    public static ObservableCollection<string> GetSharedLogCollection()
    {
        // Return the singleton instance's collection
        if (Application.Current is App app)
        {
            try
            {
                var vm = app._host?.Services.GetService(typeof(LiveLogsViewModel)) as LiveLogsViewModel;
                return vm?.LogMessages ?? new ObservableCollection<string>();
            }
            catch
            {
                return new ObservableCollection<string>();
            }
        }
        return new ObservableCollection<string>();
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
            LogMessages.CollectionChanged -= OnLogMessagesCollectionChanged;
        }

        _disposed = true;
    }
}
