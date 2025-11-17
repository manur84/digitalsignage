using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Server.Services;
using DigitalSignage.Server.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// Main application ViewModel - orchestrates sub-ViewModels.
/// Refactored to follow Single Responsibility Principle.
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<MainViewModel> _logger;
    private readonly BackupService _backupService;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly ILogger<Views.Dialogs.SettingsDialog> _settingsDialogLogger;
    private readonly IDialogService _dialogService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ThemeService _themeService;
    private bool _disposed = false;

    // Sub-ViewModels (Responsibility Segregation)
    public ServerManagementViewModel ServerManagement { get; }
    public DiagnosticsViewModel Diagnostics { get; }

    // Existing Sub-ViewModels
    public DeviceManagementViewModel DeviceManagement { get; }
    public PreviewViewModel PreviewViewModel { get; }
    public SchedulingViewModel SchedulingViewModel { get; }
    public LogViewerViewModel LogViewerViewModel { get; }
    public LiveLogsViewModel LiveLogsViewModel { get; }
    public AlertsViewModel Alerts { get; }
    public LayoutManagerViewModel LayoutManager { get; }

    // Unified Status Text (aggregated from sub-ViewModels)
    [ObservableProperty]
    private string _statusText = "Ready";

    public MainViewModel(
        ServerManagementViewModel serverManagementViewModel,
        DiagnosticsViewModel diagnosticsViewModel,
        DeviceManagementViewModel deviceManagementViewModel,
        LayoutManagerViewModel layoutManagerViewModel,
        PreviewViewModel previewViewModel,
        SchedulingViewModel schedulingViewModel,
        LogViewerViewModel logViewerViewModel,
        LiveLogsViewModel liveLogsViewModel,
        AlertsViewModel alertsViewModel,
        SettingsViewModel settingsViewModel,
        BackupService backupService,
        ThemeService themeService,
        IDialogService dialogService,
        IServiceProvider serviceProvider,
        ILogger<MainViewModel> logger,
        ILogger<Views.Dialogs.SettingsDialog> settingsDialogLogger)
    {
        // New Sub-ViewModels
        ServerManagement = serverManagementViewModel ?? throw new ArgumentNullException(nameof(serverManagementViewModel));
        Diagnostics = diagnosticsViewModel ?? throw new ArgumentNullException(nameof(diagnosticsViewModel));

        // Existing Sub-ViewModels
        DeviceManagement = deviceManagementViewModel ?? throw new ArgumentNullException(nameof(deviceManagementViewModel));
        LayoutManager = layoutManagerViewModel ?? throw new ArgumentNullException(nameof(layoutManagerViewModel));
        PreviewViewModel = previewViewModel ?? throw new ArgumentNullException(nameof(previewViewModel));
        SchedulingViewModel = schedulingViewModel ?? throw new ArgumentNullException(nameof(schedulingViewModel));
        LogViewerViewModel = logViewerViewModel ?? throw new ArgumentNullException(nameof(logViewerViewModel));
        LiveLogsViewModel = liveLogsViewModel ?? throw new ArgumentNullException(nameof(liveLogsViewModel));
        Alerts = alertsViewModel ?? throw new ArgumentNullException(nameof(alertsViewModel));

        _settingsViewModel = settingsViewModel ?? throw new ArgumentNullException(nameof(settingsViewModel));
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settingsDialogLogger = settingsDialogLogger ?? throw new ArgumentNullException(nameof(settingsDialogLogger));

        ServerManagement.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ServerManagement.StatusText))
                StatusText = ServerManagement.StatusText;

            // Sync server status to Diagnostics
            if (e.PropertyName == nameof(ServerManagement.ServerStatus) ||
                e.PropertyName == nameof(ServerManagement.ConnectedClients))
            {
                Diagnostics.UpdateServerStatus(
                    ServerManagement.ServerStatus,
                    ServerManagement.ConnectedClients);
            }
        };

        Diagnostics.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Diagnostics.StatusText))
                StatusText = Diagnostics.StatusText;
        };
    }

    #region Menu Commands

    [RelayCommand]
    private async Task Settings()
    {
        try
        {
            _logger.LogInformation("Opening settings dialog");
            StatusText = "Opening settings...";

            // Use injected dependencies instead of service locator
            var dialog = new Views.Dialogs.SettingsDialog(_settingsViewModel, _dialogService, _settingsDialogLogger)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                _logger.LogInformation("Settings saved successfully");
                StatusText = "Settings saved. Restart required for changes to take effect.";
            }
            else
            {
                _logger.LogInformation("Settings dialog cancelled");
                StatusText = "Settings not saved";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening settings dialog");
            StatusText = $"Error opening settings: {ex.Message}";
            await _dialogService.ShowErrorAsync(
                $"Failed to open settings dialog:\n\n{ex.Message}",
                "Settings Error");
        }
    }

    [RelayCommand]
    private async Task ClientTokens()
    {
        try
        {
            _logger.LogInformation("Opening Client Registration Tokens window");
            StatusText = "Opening token management...";

            var viewModel = _serviceProvider.GetRequiredService<TokenManagementViewModel>();
            var window = new Views.TokenManagementWindow(viewModel)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            window.ShowDialog();
            StatusText = "Token management window closed";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening token management window");
            StatusText = $"Error opening token management: {ex.Message}";
            await _dialogService.ShowErrorAsync(
                $"Failed to open token management window:\n\n{ex.Message}",
                "Token Management Error");
        }
    }

    [RelayCommand]
    private void ClientInstaller()
    {
        ClientInstallerViewModel? installerViewModel = null;
        try
        {
            _logger.LogInformation("Opening client installer dialog");
            StatusText = "Opening client installer...";

            installerViewModel = _serviceProvider.GetRequiredService<ClientInstallerViewModel>();
            installerViewModel.Initialize(DeviceManagement.DiscoveredDevices);

            var dialog = new Views.Dialogs.ClientInstallerDialog(installerViewModel)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            dialog.ShowDialog();
            StatusText = "Client installer closed";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening client installer dialog");
            StatusText = $"Error opening client installer: {ex.Message}";
        }
        finally
        {
            installerViewModel?.Dispose();
        }
    }

    [RelayCommand]
    private async Task SystemDiagnostics()
    {
        try
        {
            _logger.LogInformation("Opening System Diagnostics window");
            StatusText = "Opening system diagnostics...";

            var viewModel = _serviceProvider.GetRequiredService<SystemDiagnosticsViewModel>();
            var window = new Views.Dialogs.SystemDiagnosticsWindow(viewModel)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            window.ShowDialog();
            StatusText = "System diagnostics window closed";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening system diagnostics window");
            StatusText = $"Error opening diagnostics: {ex.Message}";
            await _dialogService.ShowErrorAsync(
                $"Failed to open system diagnostics window:\n\n{ex.Message}",
                "Diagnostics Error");
        }
    }

    [RelayCommand]
    private void Logs()
    {
        // Switch to Logs tab - assuming TabControl can be accessed
        StatusText = "Check the 'Logs' tab for application logs";
        _logger.LogInformation("User requested logs view");
    }

    /// <summary>
    /// Toggles between Light and Dark theme
    /// </summary>
    [RelayCommand]
    private void ToggleTheme()
    {
        try
        {
            _themeService.ToggleTheme();
            var currentTheme = _themeService.CurrentTheme;
            StatusText = $"Switched to {currentTheme} theme";
            _logger.LogInformation("Theme toggled to: {Theme}", currentTheme);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling theme");
            StatusText = $"Error toggling theme: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task Documentation()
    {
        try
        {
            var docsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "docs");

            // Try to open docs folder or README
            if (System.IO.Directory.Exists(docsPath))
            {
                System.Diagnostics.Process.Start("explorer.exe", docsPath);
                StatusText = "Opened documentation folder";
            }
            else
            {
                // Open GitHub docs
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/manur84/digitalsignage/tree/main/docs",
                    UseShellExecute = true
                });
                StatusText = "Opened online documentation";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open documentation");
            await _dialogService.ShowInformationAsync(
                "Documentation available at:\n\n" +
                "Local: ./docs folder\n" +
                "Online: https://github.com/manur84/digitalsignage/tree/main/docs",
                "Documentation");
            StatusText = "Documentation location shown";
        }
    }

    [RelayCommand]
    private async Task About()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        var message = $"Digital Signage Manager\n\n" +
                     $"Version: {version}\n" +
                     $"Framework: .NET 8.0\n" +
                     $"UI: WPF with MVVM\n\n" +
                     $"Server Status: {ServerManagement.ServerStatus}\n" +
                     $"Connected Clients: {ServerManagement.ConnectedClients}\n\n" +
                     $"© 2024 Digital Signage Project\n" +
                     $"Built with Claude Code";

        await _dialogService.ShowInformationAsync(
            message,
            "About Digital Signage Manager");

        StatusText = $"Digital Signage Manager v{version}";
    }


    #endregion

    #region Backup/Restore (Orchestrate Dialogs)

    [RelayCommand]
    private async Task BackupDatabase()
    {
        try
        {
            _logger.LogInformation("User initiated database backup");

            // File Save Dialog
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Backup Database",
                Filter = "SQLite Database (*.db)|*.db|All Files (*.*)|*.*",
                FileName = $"digitalsignage-backup-{DateTime.Now:yyyyMMdd-HHmmss}.db",
                DefaultExt = ".db"
            };

            if (saveDialog.ShowDialog() != true)
            {
                StatusText = "Backup cancelled";
                _logger.LogInformation("Database backup cancelled by user");
                return;
            }

            // Show progress
            StatusText = "Creating database backup...";
            _logger.LogInformation("Starting backup to: {FilePath}", saveDialog.FileName);

            // Create backup
            var result = await _backupService.CreateBackupAsync(saveDialog.FileName);

            if (result.IsSuccess)
            {
                StatusText = $"Backup created successfully: {saveDialog.FileName}";
                _logger.LogInformation("Database backup created successfully");

                var fileInfo = new System.IO.FileInfo(saveDialog.FileName);
                await _dialogService.ShowInformationAsync(
                    $"Database backup created successfully!\n\n" +
                    $"Location: {saveDialog.FileName}\n\n" +
                    $"Size: {fileInfo.Length / 1024:N0} KB\n" +
                    $"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    "Backup Success");
            }
            else
            {
                StatusText = "Backup failed";
                _logger.LogError("Database backup failed: {Error}", result.Error);

                await _dialogService.ShowErrorAsync(
                    $"Database backup failed:\n\n{result.Error}\n\n" +
                    $"Please check:\n" +
                    $"- Sufficient disk space\n" +
                    $"- Write permissions to target directory\n" +
                    $"- Database is not locked by another process",
                    "Backup Error");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating database backup");
            StatusText = "Backup failed";
            await _dialogService.ShowErrorAsync(
                $"Error creating backup:\n\n{ex.Message}",
                "Error");
        }
    }

    [RelayCommand]
    private async Task RestoreDatabase()
    {
        try
        {
            _logger.LogWarning("User initiated database restore - showing warning dialog");

            // FIRST WARNING CONFIRMATION
            var warningResult = await _dialogService.ShowConfirmationAsync(
                "⚠️ WARNING: Restoring a backup will REPLACE the current database!\n\n" +
                "All current data will be LOST. This action CANNOT be undone.\n\n" +
                "This includes:\n" +
                "• All layouts\n" +
                "• All client registrations\n" +
                "• All media library files\n" +
                "• All settings and configurations\n" +
                "• All logs and history\n\n" +
                "It is STRONGLY recommended to create a backup of the current database first.\n\n" +
                "Do you want to continue?",
                "Restore Database - WARNING");

            if (!warningResult)
            {
                StatusText = "Database restore cancelled";
                _logger.LogInformation("Database restore cancelled by user at first warning");
                return;
            }

            // File Open Dialog
            var openDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Database Backup File",
                Filter = "SQLite Database (*.db)|*.db|All Files (*.*)|*.*",
                DefaultExt = ".db"
            };

            if (openDialog.ShowDialog() != true)
            {
                StatusText = "Database restore cancelled";
                _logger.LogInformation("Database restore cancelled by user at file selection");
                return;
            }

            // FINAL CONFIRMATION
            var finalResult = await _dialogService.ShowConfirmationAsync(
                $"⚠️ FINAL CONFIRMATION\n\n" +
                $"Are you ABSOLUTELY SURE you want to restore from:\n\n" +
                $"{openDialog.FileName}\n\n" +
                $"Current database will be REPLACED and CANNOT be recovered!\n\n" +
                $"A safety backup will be created automatically before restore.\n\n" +
                $"Continue with restore?",
                "Final Confirmation - Restore Database");

            if (!finalResult)
            {
                StatusText = "Database restore cancelled";
                _logger.LogInformation("Database restore cancelled by user at final confirmation");
                return;
            }

            // Show progress
            StatusText = "Restoring database from backup...";
            _logger.LogWarning("Starting database restore from: {FilePath}", openDialog.FileName);

            // Restore backup
            var result = await _backupService.RestoreBackupAsync(openDialog.FileName);

            if (result.IsSuccess)
            {
                StatusText = "Database restored successfully - application will restart";
                _logger.LogInformation("Database restored successfully");

                await _dialogService.ShowInformationAsync(
                    "Database restored successfully!\n\n" +
                    "A safety backup of the previous database has been created.\n\n" +
                    "The application will now RESTART to apply changes.\n\n" +
                    "Please wait for the application to restart automatically.",
                    "Restore Success");

                _logger.LogInformation("Restarting application after successful restore");

                // RESTART APPLICATION
                try
                {
                    var currentExecutable = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(currentExecutable))
                    {
                        System.Diagnostics.Process.Start(currentExecutable);
                        _logger.LogInformation("Started new application instance");
                    }
                }
                catch (Exception restartEx)
                {
                    _logger.LogError(restartEx, "Failed to restart application automatically");
                }

                // Shutdown current instance
                System.Windows.Application.Current.Shutdown();
            }
            else
            {
                StatusText = "Database restore failed";
                _logger.LogError("Database restore failed: {Error}", result.Error);

                await _dialogService.ShowErrorAsync(
                    $"Database restore failed:\n\n{result.Error}\n\n" +
                    $"The current database has been preserved.\n\n" +
                    $"Please check:\n" +
                    $"- Backup file is valid\n" +
                    $"- Backup file is not corrupted\n" +
                    $"- Sufficient disk space available\n" +
                    $"- Database is not locked by another process",
                    "Restore Error");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring database backup");
            StatusText = "Database restore failed";
            await _dialogService.ShowErrorAsync(
                $"Error restoring backup:\n\n{ex.Message}\n\n" +
                $"The current database has been preserved.",
                "Error");
        }
    }

    #endregion

    #region Placeholder Commands (Future Implementation)
    // ClientTokens command moved to implemented section above
    #endregion

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
            // Dispose new sub-viewmodels
            ServerManagement?.Dispose();
            Diagnostics?.Dispose();

            // Dispose existing sub-viewmodels
            if (DeviceManagement is IDisposable deviceManagementDisposable)
                deviceManagementDisposable.Dispose();

            if (PreviewViewModel is IDisposable previewDisposable)
                previewDisposable.Dispose();

            if (SchedulingViewModel is IDisposable schedulingDisposable)
                schedulingDisposable.Dispose();

            if (LogViewerViewModel is IDisposable logViewerDisposable)
                logViewerDisposable.Dispose();

            if (LiveLogsViewModel is IDisposable liveLogsDisposable)
                liveLogsDisposable.Dispose();

            if (Alerts is IDisposable alertsDisposable)
                alertsDisposable.Dispose();
        }

        _disposed = true;
    }
}
