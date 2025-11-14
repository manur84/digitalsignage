using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Server.Services;
using DigitalSignage.Server.Views;
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
    private bool _disposed = false;

    // View Options
    [ObservableProperty]
    private bool _showGrid = true;

    [ObservableProperty]
    private bool _showRulers = true;

    [ObservableProperty]
    private bool _snapToGrid = true;

    [ObservableProperty]
    private bool _hasSelectedElement = false;

    // Sub-ViewModels (Responsibility Segregation)
    public LayoutManagementViewModel LayoutManagement { get; }
    public ServerManagementViewModel ServerManagement { get; }
    public DiagnosticsViewModel Diagnostics { get; }

    // Existing Sub-ViewModels
    public DesignerViewModel Designer { get; }
    public DeviceManagementViewModel DeviceManagement { get; }
    public DataSourceViewModel DataSourceViewModel { get; }
    public PreviewViewModel PreviewViewModel { get; }
    public SchedulingViewModel SchedulingViewModel { get; }
    public MediaLibraryViewModel MediaLibraryViewModel { get; }
    public LogViewerViewModel LogViewerViewModel { get; }
    public LiveLogsViewModel LiveLogsViewModel { get; }
    public AlertsViewModel Alerts { get; }

    // Unified Status Text (aggregated from sub-ViewModels)
    [ObservableProperty]
    private string _statusText = "Ready";

    public MainViewModel(
        LayoutManagementViewModel layoutManagementViewModel,
        ServerManagementViewModel serverManagementViewModel,
        DiagnosticsViewModel diagnosticsViewModel,
        DesignerViewModel designerViewModel,
        DeviceManagementViewModel deviceManagementViewModel,
        DataSourceViewModel dataSourceViewModel,
        PreviewViewModel previewViewModel,
        SchedulingViewModel schedulingViewModel,
        MediaLibraryViewModel mediaLibraryViewModel,
        LogViewerViewModel logViewerViewModel,
        LiveLogsViewModel liveLogsViewModel,
        AlertsViewModel alertsViewModel,
        SettingsViewModel settingsViewModel,
        BackupService backupService,
        IDialogService dialogService,
        ILogger<MainViewModel> logger,
        ILogger<Views.Dialogs.SettingsDialog> settingsDialogLogger)
    {
        // New Sub-ViewModels
        LayoutManagement = layoutManagementViewModel ?? throw new ArgumentNullException(nameof(layoutManagementViewModel));
        ServerManagement = serverManagementViewModel ?? throw new ArgumentNullException(nameof(serverManagementViewModel));
        Diagnostics = diagnosticsViewModel ?? throw new ArgumentNullException(nameof(diagnosticsViewModel));

        // Existing Sub-ViewModels
        Designer = designerViewModel ?? throw new ArgumentNullException(nameof(designerViewModel));
        DeviceManagement = deviceManagementViewModel ?? throw new ArgumentNullException(nameof(deviceManagementViewModel));
        DataSourceViewModel = dataSourceViewModel ?? throw new ArgumentNullException(nameof(dataSourceViewModel));
        PreviewViewModel = previewViewModel ?? throw new ArgumentNullException(nameof(previewViewModel));
        SchedulingViewModel = schedulingViewModel ?? throw new ArgumentNullException(nameof(schedulingViewModel));
        MediaLibraryViewModel = mediaLibraryViewModel ?? throw new ArgumentNullException(nameof(mediaLibraryViewModel));
        LogViewerViewModel = logViewerViewModel ?? throw new ArgumentNullException(nameof(logViewerViewModel));
        LiveLogsViewModel = liveLogsViewModel ?? throw new ArgumentNullException(nameof(liveLogsViewModel));
        Alerts = alertsViewModel ?? throw new ArgumentNullException(nameof(alertsViewModel));

        _settingsViewModel = settingsViewModel ?? throw new ArgumentNullException(nameof(settingsViewModel));
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settingsDialogLogger = settingsDialogLogger ?? throw new ArgumentNullException(nameof(settingsDialogLogger));

        // Subscribe to sub-ViewModel status changes to update unified StatusText
        LayoutManagement.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(LayoutManagement.StatusText))
                StatusText = LayoutManagement.StatusText;
        };

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

        // Subscribe to layout changes to update preview
        LayoutManagement.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(LayoutManagement.CurrentLayout) && LayoutManagement.CurrentLayout != null)
            {
                PreviewViewModel.LoadLayout(LayoutManagement.CurrentLayout);
            }
        };
    }

    #region Pass-Through Commands (Designer)

    [RelayCommand]
    private void Undo()
    {
        Designer.UndoCommand.Execute(null);
        StatusText = $"Undo: {Designer.CommandHistory.RedoDescription ?? "Nothing to undo"}";
    }

    [RelayCommand]
    private void Redo()
    {
        Designer.RedoCommand.Execute(null);
        StatusText = $"Redo: {Designer.CommandHistory.UndoDescription ?? "Nothing to redo"}";
    }

    [RelayCommand]
    private void Cut()
    {
        StatusText = "Cut";
    }

    [RelayCommand]
    private void Copy()
    {
        StatusText = "Copy";
    }

    [RelayCommand]
    private void Paste()
    {
        StatusText = "Paste";
    }

    [RelayCommand]
    private void Delete()
    {
        StatusText = "Delete";
    }

    [RelayCommand]
    private void ZoomIn()
    {
        StatusText = "Zoom in";
    }

    [RelayCommand]
    private void ZoomOut()
    {
        StatusText = "Zoom out";
    }

    [RelayCommand]
    private void ZoomToFit()
    {
        StatusText = "Zoom to fit";
    }

    #endregion

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
    private void Logs()
    {
        // Switch to Logs tab - assuming TabControl can be accessed
        StatusText = "Check the 'Logs' tab for application logs";
        _logger.LogInformation("User requested logs view");
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
                "• All layouts and templates\n" +
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

    [RelayCommand]
    private async Task TemplateManager()
    {
        StatusText = "Opening Template Manager...";

        await _dialogService.ShowInformationAsync(
            "Template Manager\n\n" +
            "Current Templates: 11 built-in templates\n\n" +
            "Features:\n" +
            "• View all available templates\n" +
            "• Create custom templates\n" +
            "• Edit template metadata\n" +
            "• Export/Import templates\n\n" +
            "Access templates via:\n" +
            "File → New from Template",
            "Template Manager");
    }

    [RelayCommand]
    private async Task ClientTokens()
    {
        StatusText = "Opening Client Registration Tokens...";

        await _dialogService.ShowInformationAsync(
            "Client Registration Tokens\n\n" +
            "Manage tokens for client device registration.\n\n" +
            "Features:\n" +
            "• Generate new registration tokens\n" +
            "• Set token expiration\n" +
            "• Limit token usage count\n" +
            "• Assign groups and locations\n" +
            "• Restrict by MAC address\n\n" +
            "Token-based registration ensures secure\n" +
            "client onboarding.",
            "Client Registration Tokens");
    }

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
            LayoutManagement?.Dispose();
            ServerManagement?.Dispose();
            Diagnostics?.Dispose();

            // Dispose existing sub-viewmodels
            if (Designer is IDisposable designerDisposable)
                designerDisposable.Dispose();

            if (DeviceManagement is IDisposable deviceManagementDisposable)
                deviceManagementDisposable.Dispose();

            if (DataSourceViewModel is IDisposable dataSourceDisposable)
                dataSourceDisposable.Dispose();

            if (PreviewViewModel is IDisposable previewDisposable)
                previewDisposable.Dispose();

            if (SchedulingViewModel is IDisposable schedulingDisposable)
                schedulingDisposable.Dispose();

            if (MediaLibraryViewModel is IDisposable mediaLibraryDisposable)
                mediaLibraryDisposable.Dispose();

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
