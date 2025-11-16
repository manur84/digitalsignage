using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.ViewModels;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.Server.Views;

/// <summary>
/// Main window for the Digital Signage Manager application (designer features removed).
/// </summary>
public partial class MainWindow : Window
{
    private readonly ILogger<MainWindow>? _logger;

    public MainWindow(MainViewModel viewModel, ILogger<MainWindow>? logger = null)
    {
        InitializeComponent();
        DataContext = viewModel;
        _logger = logger;
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    /// <summary>
    /// Copies selected log entries to clipboard from the Logs tab.
    /// </summary>
    private void CopyLogsToClipboard_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem { Parent: ContextMenu contextMenu } ||
                contextMenu.PlacementTarget is not DataGrid dataGrid)
            {
                return;
            }

            var selectedLogs = dataGrid.SelectedItems.Cast<LogEntry>().ToList();
            if (!selectedLogs.Any())
            {
                return;
            }

            var logText = new StringBuilder();
            foreach (var log in selectedLogs.OrderBy(l => l.Timestamp))
            {
                logText.AppendLine($"[{log.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{log.Level,-8}] [{log.ClientName,-20}] {log.Message}");
                if (!string.IsNullOrEmpty(log.Exception))
                {
                    logText.AppendLine($"    Exception: {log.Exception}");
                }
            }

            Clipboard.SetText(logText.ToString());
            _logger?.LogInformation("Copied {Count} log entries to clipboard", selectedLogs.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to copy logs to clipboard");
            MessageBox.Show($"Failed to copy logs: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Shows detailed information for a single log entry.
    /// </summary>
    private void ShowLogDetails_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem { Parent: ContextMenu contextMenu } ||
                contextMenu.PlacementTarget is not DataGrid dataGrid)
            {
                return;
            }

            if (dataGrid.SelectedItem is not LogEntry selectedLog)
            {
                return;
            }

            var details = new StringBuilder();
            details.AppendLine($"Timestamp: {selectedLog.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
            details.AppendLine($"Level: {selectedLog.Level}");
            details.AppendLine($"Client ID: {selectedLog.ClientId}");
            details.AppendLine($"Client Name: {selectedLog.ClientName}");
            details.AppendLine($"Source: {selectedLog.Source}");
            details.AppendLine($"\nMessage:");
            details.AppendLine(selectedLog.Message);

            if (!string.IsNullOrEmpty(selectedLog.Exception))
            {
                details.AppendLine($"\nException:");
                details.AppendLine(selectedLog.Exception);
            }

            MessageBox.Show(details.ToString(), "Log Entry Details", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to show log details");
            MessageBox.Show($"Failed to show log details: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
