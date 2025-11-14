using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// Manages diagnostic and maintenance operations.
/// Extracted from MainViewModel to follow Single Responsibility Principle.
/// </summary>
public partial class DiagnosticsViewModel : ObservableObject, IDisposable
{
    private readonly IClientService _clientService;
    private readonly ILayoutService _layoutService;
    private readonly DigitalSignageDbContext _dbContext;
    private readonly ILogger<DiagnosticsViewModel> _logger;
    private bool _disposed = false;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _serverStatus = "Unknown";

    [ObservableProperty]
    private int _connectedClients = 0;

    public DiagnosticsViewModel(
        IClientService clientService,
        ILayoutService layoutService,
        DigitalSignageDbContext dbContext,
        ILogger<DiagnosticsViewModel> logger)
    {
        _clientService = clientService ?? throw new ArgumentNullException(nameof(clientService));
        _layoutService = layoutService ?? throw new ArgumentNullException(nameof(layoutService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [RelayCommand]
    private async Task TestDatabase()
    {
        StatusText = "Testing database connection...";
        try
        {
            var canConnect = await Task.Run(() => _dbContext.Database.CanConnect());
            var connectionString = _dbContext.Database.GetConnectionString();

            if (canConnect)
            {
                // Test a simple query
                var clients = await _clientService.GetAllClientsAsync();
                var layouts = await _layoutService.GetAllLayoutsAsync();

                var message = $"✅ Database Connection Successful!\n\n" +
                             $"Connection String:\n{connectionString}\n\n" +
                             $"Statistics:\n" +
                             $"• Clients: {clients.Count()}\n" +
                             $"• Layouts: {layouts.Count()}\n" +
                             $"• Provider: {_dbContext.Database.ProviderName}";

                System.Windows.MessageBox.Show(
                    message,
                    "Database Test Result",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);

                StatusText = "Database test successful";
                _logger.LogInformation("Database test successful: {ClientCount} clients, {LayoutCount} layouts", clients.Count(), layouts.Count());
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "❌ Database connection failed!\n\n" +
                    "Please check:\n" +
                    "• SQL Server is running\n" +
                    "• Connection string in appsettings.json\n" +
                    "• Network connectivity\n" +
                    "• Firewall settings",
                    "Database Test Failed",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);

                StatusText = "Database test failed";
                _logger.LogError("Database test failed: Cannot connect");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database test error");
            StatusText = $"Database test error: {ex.Message}";

            System.Windows.MessageBox.Show(
                $"Database Test Error:\n\n{ex.Message}",
                "Database Test Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void DatabaseConnection()
    {
        try
        {
            var connectionString = _dbContext.Database.GetConnectionString();
            var message = $"Current Database Connection:\n\n{connectionString}\n\n" +
                         $"Server: {_dbContext.Database.CanConnect()}\n" +
                         $"Provider: {_dbContext.Database.ProviderName}";

            System.Windows.MessageBox.Show(
                message,
                "Database Connection",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);

            StatusText = "Database connection info displayed";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get database connection info");
            StatusText = $"Database connection error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SystemDiagnostics()
    {
        try
        {
            var diagnostics = new System.Text.StringBuilder();
            diagnostics.AppendLine("System Diagnostics");
            diagnostics.AppendLine("═══════════════════");
            diagnostics.AppendLine();

            diagnostics.AppendLine("Application:");
            diagnostics.AppendLine($"  Version: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}");
            diagnostics.AppendLine($"  Framework: .NET 8.0");
            diagnostics.AppendLine($"  Base Directory: {AppDomain.CurrentDomain.BaseDirectory}");
            diagnostics.AppendLine();

            diagnostics.AppendLine("Server:");
            diagnostics.AppendLine($"  Status: {ServerStatus}");
            diagnostics.AppendLine($"  Connected Clients: {ConnectedClients}");
            diagnostics.AppendLine($"  WebSocket Port: 8080");
            diagnostics.AppendLine();

            diagnostics.AppendLine("Database:");
            diagnostics.AppendLine($"  Provider: {_dbContext.Database.ProviderName}");
            diagnostics.AppendLine($"  Can Connect: {_dbContext.Database.CanConnect()}");
            diagnostics.AppendLine();

            diagnostics.AppendLine("System:");
            diagnostics.AppendLine($"  OS: {Environment.OSVersion}");
            diagnostics.AppendLine($"  Machine Name: {Environment.MachineName}");
            diagnostics.AppendLine($"  Processor Count: {Environment.ProcessorCount}");
            diagnostics.AppendLine($"  Working Set: {Environment.WorkingSet / 1024 / 1024} MB");
            diagnostics.AppendLine();

            diagnostics.AppendLine("Paths:");
            diagnostics.AppendLine($"  Current Directory: {Environment.CurrentDirectory}");
            diagnostics.AppendLine($"  Logs: {System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs")}");
            diagnostics.AppendLine($"  Temp: {System.IO.Path.GetTempPath()}");

            System.Windows.MessageBox.Show(
                diagnostics.ToString(),
                "System Diagnostics",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);

            StatusText = "System diagnostics displayed";
            _logger.LogInformation("System diagnostics viewed by user");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get system diagnostics");
            StatusText = $"Diagnostics error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ClearLogs()
    {
        var result = System.Windows.MessageBox.Show(
            "Are you sure you want to clear all application logs?\n\n" +
            "This will delete all log files in the logs directory.\n" +
            "This action cannot be undone.",
            "Clear Logs",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            try
            {
                var logsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                if (System.IO.Directory.Exists(logsPath))
                {
                    var files = System.IO.Directory.GetFiles(logsPath, "*.log");
                    foreach (var file in files)
                    {
                        try
                        {
                            System.IO.File.Delete(file);
                        }
                        catch
                        {
                            // Skip files that are in use
                        }
                    }

                    StatusText = $"Cleared {files.Length} log files";
                    _logger.LogInformation("Log files cleared by user");
                }
                else
                {
                    StatusText = "No logs directory found";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear logs");
                StatusText = $"Failed to clear logs: {ex.Message}";
            }
        }
    }

    /// <summary>
    /// Updates server status from external source (e.g., ServerManagementViewModel).
    /// </summary>
    public void UpdateServerStatus(string status, int connectedClients)
    {
        ServerStatus = status;
        ConnectedClients = connectedClients;
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
            // Cleanup if needed
        }

        _disposed = true;
    }
}
