using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using DigitalSignage.Data;
using DigitalSignage.Server.Services;
using DigitalSignage.Server.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Input;

namespace DigitalSignage.Server.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ILayoutService _layoutService;
    private readonly IClientService _clientService;
    private readonly ICommunicationService _communicationService;
    private readonly DigitalSignageDbContext _dbContext;
    private readonly ILogger<MainViewModel> _logger;
    private bool _disposed = false;

    [ObservableProperty]
    private DisplayLayout? _currentLayout;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private int _connectedClients = 0;

    [ObservableProperty]
    private string _serverStatus = "Stopped";

    [ObservableProperty]
    private bool _showGrid = true;

    [ObservableProperty]
    private bool _showRulers = true;

    [ObservableProperty]
    private bool _snapToGrid = true;

    [ObservableProperty]
    private bool _hasSelectedElement = false;

    [ObservableProperty]
    private RaspberryPiClient? _selectedClient;

    public ObservableCollection<RaspberryPiClient> Clients { get; } = new();

    public DesignerViewModel Designer { get; }
    public DeviceManagementViewModel DeviceManagement { get; }
    public DataSourceViewModel DataSourceViewModel { get; }
    public PreviewViewModel PreviewViewModel { get; }
    public SchedulingViewModel SchedulingViewModel { get; }
    public MediaLibraryViewModel MediaLibraryViewModel { get; }
    public LogViewerViewModel LogViewerViewModel { get; }
    public LiveLogsViewModel LiveLogsViewModel { get; }
    public AlertsViewModel Alerts { get; }

    public MainViewModel(
        ILayoutService layoutService,
        IClientService clientService,
        ICommunicationService communicationService,
        DesignerViewModel designerViewModel,
        DeviceManagementViewModel deviceManagementViewModel,
        DataSourceViewModel dataSourceViewModel,
        PreviewViewModel previewViewModel,
        SchedulingViewModel schedulingViewModel,
        MediaLibraryViewModel mediaLibraryViewModel,
        LogViewerViewModel logViewerViewModel,
        LiveLogsViewModel liveLogsViewModel,
        AlertsViewModel alertsViewModel,
        DigitalSignageDbContext dbContext,
        ILogger<MainViewModel> logger)
    {
        _layoutService = layoutService;
        _clientService = clientService;
        _communicationService = communicationService;
        _dbContext = dbContext;
        _logger = logger;
        Designer = designerViewModel ?? throw new ArgumentNullException(nameof(designerViewModel));
        DeviceManagement = deviceManagementViewModel ?? throw new ArgumentNullException(nameof(deviceManagementViewModel));
        DataSourceViewModel = dataSourceViewModel ?? throw new ArgumentNullException(nameof(dataSourceViewModel));
        PreviewViewModel = previewViewModel ?? throw new ArgumentNullException(nameof(previewViewModel));
        SchedulingViewModel = schedulingViewModel ?? throw new ArgumentNullException(nameof(schedulingViewModel));
        MediaLibraryViewModel = mediaLibraryViewModel ?? throw new ArgumentNullException(nameof(mediaLibraryViewModel));
        LogViewerViewModel = logViewerViewModel ?? throw new ArgumentNullException(nameof(logViewerViewModel));
        LiveLogsViewModel = liveLogsViewModel ?? throw new ArgumentNullException(nameof(liveLogsViewModel));
        Alerts = alertsViewModel ?? throw new ArgumentNullException(nameof(alertsViewModel));

        // Subscribe to communication events
        _communicationService.ClientConnected += OnClientConnected;
        _communicationService.ClientDisconnected += OnClientDisconnected;

        // Subscribe to layout changes to update preview
        this.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(CurrentLayout) && CurrentLayout != null)
            {
                PreviewViewModel.LoadLayout(CurrentLayout);
            }
        };

        // Subscribe to Designer.HasUnsavedChanges to update Save command
        Designer.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Designer.HasUnsavedChanges))
            {
                SaveCommand.NotifyCanExecuteChanged();
            }
        };

        // Start the communication service
        _ = StartServerAsync();
    }

    private async Task StartServerAsync()
    {
        try
        {
            await _communicationService.StartAsync();
            ServerStatus = "Running";
            StatusText = "Server started successfully";
            _logger.LogInformation("WebSocket server started successfully");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Access Denied"))
        {
            // URL ACL permission error
            ServerStatus = "Error";
            StatusText = "Server failed to start: Access Denied (URL ACL not configured)";
            _logger.LogError(ex, "Failed to start server due to URL ACL permissions");

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                System.Windows.MessageBox.Show(
                    $"Access Denied - Cannot start server\n\n" +
                    $"Windows requires URL ACL registration to bind HTTP servers.\n\n" +
                    $"SOLUTION 1 (Recommended - One-time setup):\n" +
                    $"  1. Right-click setup-urlacl.bat in the application folder\n" +
                    $"  2. Select 'Run as administrator'\n" +
                    $"  3. Restart the application normally (no admin needed)\n\n" +
                    $"SOLUTION 2 (Temporary):\n" +
                    $"  Close this application and run as Administrator\n\n" +
                    $"After running setup-urlacl.bat once, you will never need\n" +
                    $"administrator privileges again for this application.\n\n" +
                    $"Technical Details:\n" +
                    $"{ex.Message}",
                    "Permission Error - Digital Signage Server",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            });
        }
        catch (Exception ex)
        {
            ServerStatus = "Error";
            StatusText = $"Failed to start server: {ex.Message}";
            _logger.LogError(ex, "Failed to start server");

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                System.Windows.MessageBox.Show(
                    $"Failed to start Digital Signage Server\n\n" +
                    $"Error: {ex.Message}\n\n" +
                    $"Common Solutions:\n" +
                    $"- Run diagnose-server.ps1 for diagnostics\n" +
                    $"- Check that port 8080 is not in use\n" +
                    $"- Use fix-and-run.bat for automatic fix\n\n" +
                    $"Check the logs folder for detailed error information.",
                    "Startup Error - Digital Signage Server",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            });
        }
    }

    private async void OnClientConnected(object? sender, ClientConnectedEventArgs e)
    {
        ConnectedClients++;
        StatusText = $"Client connected: {e.ClientId}";
        await RefreshClientsAsync();
    }

    private async void OnClientDisconnected(object? sender, ClientDisconnectedEventArgs e)
    {
        ConnectedClients--;
        StatusText = $"Client disconnected: {e.ClientId}";
        await RefreshClientsAsync();
    }

    private async Task RefreshClientsAsync()
    {
        try
        {
            var clients = await _clientService.GetAllClientsAsync();
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Clients.Clear();
                foreach (var client in clients)
                {
                    Clients.Add(client);
                }
            });
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to refresh clients: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task NewLayout()
    {
        try
        {
            _logger.LogInformation("Opening new layout dialog");
            StatusText = "Create a new layout...";

            // Create the new layout view model
            var newLayoutViewModel = new NewLayoutViewModel(
                Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole())
                    .CreateLogger<NewLayoutViewModel>());

            // Create and show the dialog
            var dialog = new NewLayoutDialog(newLayoutViewModel);
            var result = dialog.ShowDialog();

            if (result == true && newLayoutViewModel.SelectedResolution != null)
            {
                _logger.LogInformation("Creating new layout: {LayoutName}", newLayoutViewModel.LayoutName);

                // Create new layout
                var newLayout = new DisplayLayout
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = newLayoutViewModel.LayoutName,
                    Description = newLayoutViewModel.Description,
                    Resolution = new Resolution
                    {
                        Width = newLayoutViewModel.SelectedResolution.Width,
                        Height = newLayoutViewModel.SelectedResolution.Height,
                        Orientation = newLayoutViewModel.SelectedResolution.Width > newLayoutViewModel.SelectedResolution.Height ? "landscape" : "portrait"
                    },
                    BackgroundColor = newLayoutViewModel.BackgroundColor,
                    Elements = new List<DisplayElement>(),
                    Created = DateTime.UtcNow,
                    Modified = DateTime.UtcNow
                };

                // Load the new layout into Designer
                await Designer.CreateNewLayoutAsync(newLayout);
                CurrentLayout = newLayout;

                StatusText = $"Created new layout: {newLayout.Name}";
                _logger.LogInformation("Successfully created new layout");
            }
            else
            {
                StatusText = "New layout cancelled";
                _logger.LogInformation("New layout creation cancelled by user");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create new layout");
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task NewFromTemplate()
    {
        try
        {
            _logger.LogInformation("Opening template selection dialog");
            StatusText = "Select a template...";

            // Create the template selection view model
            var templateViewModel = new TemplateSelectionViewModel(
                _dbContext,
                Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole())
                    .CreateLogger<TemplateSelectionViewModel>());

            // Create and show the dialog
            var dialog = new TemplateSelectionWindow(templateViewModel);
            var result = dialog.ShowDialog();

            if (result == true && templateViewModel.SelectedTemplate != null)
            {
                var template = templateViewModel.SelectedTemplate;
                _logger.LogInformation("Creating layout from template: {TemplateName}", template.Name);

                // Deserialize the elements from the template
                var elements = JsonSerializer.Deserialize<List<DisplayElement>>(template.ElementsJson)
                    ?? new List<DisplayElement>();

                // Create new layout from template
                var newLayout = new DisplayLayout
                {
                    Name = $"{template.Name} - Copy",
                    Resolution = template.Resolution,
                    BackgroundColor = template.BackgroundColor,
                    BackgroundImage = template.BackgroundImage,
                    Elements = elements
                };

                // Update both the main view model and designer with the new layout
                CurrentLayout = newLayout;
                Designer.CurrentLayout = newLayout;

                // Update the designer's elements collection
                Designer.Elements.Clear();
                foreach (var element in elements)
                {
                    Designer.Elements.Add(element);
                }

                StatusText = $"Layout created from template: {template.Name}";
                _logger.LogInformation("Successfully created layout from template");
            }
            else
            {
                StatusText = "Template selection cancelled";
                _logger.LogInformation("Template selection cancelled by user");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create layout from template");
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task OpenLayout()
    {
        try
        {
            _logger.LogInformation("Opening layout selection dialog");
            StatusText = "Select a layout to open...";

            // Create the layout selection view model
            var layoutSelectionViewModel = new LayoutSelectionViewModel(
                _layoutService,
                Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole())
                    .CreateLogger<LayoutSelectionViewModel>());

            // Create and show the dialog
            var dialog = new LayoutSelectionDialog(layoutSelectionViewModel);
            var result = dialog.ShowDialog();

            if (result == true && layoutSelectionViewModel.SelectedLayout != null)
            {
                var selectedLayout = layoutSelectionViewModel.SelectedLayout;
                _logger.LogInformation("Loading layout: {LayoutName}", selectedLayout.Name);

                // Load the selected layout into Designer
                await Designer.LoadLayoutAsync(selectedLayout);
                CurrentLayout = selectedLayout;

                StatusText = $"Loaded layout: {selectedLayout.Name}";
                _logger.LogInformation("Successfully loaded layout");
            }
            else
            {
                StatusText = "Layout selection cancelled";
                _logger.LogInformation("Layout selection cancelled by user");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open layout");
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task Save()
    {
        if (CurrentLayout == null)
        {
            StatusText = "No layout to save";
            return;
        }

        try
        {
            _logger.LogInformation("Saving layout: {LayoutName}", CurrentLayout.Name);

            // Update elements from Designer
            CurrentLayout.Elements = Designer.Elements.ToList();
            CurrentLayout.Modified = DateTime.UtcNow;

            if (string.IsNullOrEmpty(CurrentLayout.Id))
            {
                CurrentLayout.Id = Guid.NewGuid().ToString();
                CurrentLayout.Created = DateTime.UtcNow;
                await _layoutService.CreateLayoutAsync(CurrentLayout);
                StatusText = $"Layout created successfully: {CurrentLayout.Name}";
                _logger.LogInformation("Created new layout: {LayoutId}", CurrentLayout.Id);
            }
            else
            {
                await _layoutService.UpdateLayoutAsync(CurrentLayout);
                StatusText = $"Layout saved successfully: {CurrentLayout.Name}";
                _logger.LogInformation("Updated layout: {LayoutId}", CurrentLayout.Id);
            }

            // Reset unsaved changes flag
            Designer.HasUnsavedChanges = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save layout");
            StatusText = $"Failed to save layout: {ex.Message}";
            System.Windows.MessageBox.Show(
                $"Failed to save layout: {ex.Message}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private bool CanSave() => CurrentLayout != null && Designer.HasUnsavedChanges;

    partial void OnCurrentLayoutChanged(DisplayLayout? value)
    {
        SaveCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task SaveAs()
    {
        if (CurrentLayout == null)
        {
            StatusText = "No layout to save";
            return;
        }

        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON Layout (*.json)|*.json",
                DefaultExt = ".json",
                FileName = $"{CurrentLayout.Name}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                CurrentLayout.Elements = Designer.Elements.ToList();
                CurrentLayout.Modified = DateTime.UtcNow;

                var json = await _layoutService.ExportLayoutAsync(CurrentLayout.Id);
                await System.IO.File.WriteAllTextAsync(dialog.FileName, json);

                StatusText = $"Layout saved to: {dialog.FileName}";
                _logger.LogInformation("Layout saved as: {FileName}", dialog.FileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save layout as file");
            StatusText = $"Failed to save: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task Export()
    {
        if (CurrentLayout == null)
        {
            StatusText = "No layout to export";
            return;
        }

        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON Layout (*.json)|*.json",
                DefaultExt = ".json",
                FileName = $"{CurrentLayout.Name}_export.json"
            };

            if (dialog.ShowDialog() == true)
            {
                CurrentLayout.Elements = Designer.Elements.ToList();
                var json = await _layoutService.ExportLayoutAsync(CurrentLayout.Id);
                await System.IO.File.WriteAllTextAsync(dialog.FileName, json);

                StatusText = $"Layout exported to: {dialog.FileName}";
                _logger.LogInformation("Layout exported: {FileName}", dialog.FileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export layout");
            StatusText = $"Failed to export: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task Import()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON Layout (*.json)|*.json",
                DefaultExt = ".json"
            };

            if (dialog.ShowDialog() == true)
            {
                StatusText = "Importing layout...";
                var json = await System.IO.File.ReadAllTextAsync(dialog.FileName);
                var layout = await _layoutService.ImportLayoutAsync(json);

                // Load into designer
                await Designer.LoadLayoutAsync(layout);
                CurrentLayout = layout;

                StatusText = $"Layout imported: {layout.Name}";
                _logger.LogInformation("Layout imported from: {FileName}", dialog.FileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import layout");
            StatusText = $"Failed to import: {ex.Message}";
            System.Windows.MessageBox.Show(
                $"Failed to import layout:\n{ex.Message}",
                "Import Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

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
    private void Settings()
    {
        try
        {
            _logger.LogInformation("Opening settings dialog");
            StatusText = "Opening settings...";

            var viewModel = App.GetService<SettingsViewModel>();
            var logger = App.GetService<Microsoft.Extensions.Logging.ILogger<Views.Dialogs.SettingsDialog>>();
            var dialog = new Views.Dialogs.SettingsDialog(viewModel, logger)
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
            System.Windows.MessageBox.Show(
                $"Failed to open settings dialog:\n\n{ex.Message}",
                "Settings Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
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
    private void Documentation()
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
            System.Windows.MessageBox.Show(
                "Documentation available at:\n\n" +
                "Local: ./docs folder\n" +
                "Online: https://github.com/manur84/digitalsignage/tree/main/docs",
                "Documentation",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            StatusText = "Documentation location shown";
        }
    }

    [RelayCommand]
    private void About()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        var message = $"Digital Signage Manager\n\n" +
                     $"Version: {version}\n" +
                     $"Framework: .NET 8.0\n" +
                     $"UI: WPF with MVVM\n\n" +
                     $"Server Status: {ServerStatus}\n" +
                     $"Connected Clients: {ConnectedClients}\n\n" +
                     $"© 2024 Digital Signage Project\n" +
                     $"Built with Claude Code";

        System.Windows.MessageBox.Show(
            message,
            "About Digital Signage Manager",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);

        StatusText = $"Digital Signage Manager v{version}";
    }

    #region Advanced Tools Commands

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
    private void ServerConfiguration()
    {
        try
        {
            var config = new System.Text.StringBuilder();
            config.AppendLine("Server Configuration:");
            config.AppendLine();
            config.AppendLine($"Server Status: {ServerStatus}");
            config.AppendLine($"Connected Clients: {ConnectedClients}");
            config.AppendLine($"WebSocket Port: 8080");
            config.AppendLine($"Base Directory: {AppDomain.CurrentDomain.BaseDirectory}");
            config.AppendLine();
            config.AppendLine("Database:");
            config.AppendLine($"  Provider: {_dbContext.Database.ProviderName}");
            config.AppendLine($"  Connection: {_dbContext.Database.GetConnectionString()}");

            System.Windows.MessageBox.Show(
                config.ToString(),
                "Server Configuration",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);

            StatusText = "Server configuration displayed";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get server configuration");
            StatusText = $"Configuration error: {ex.Message}";
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

    [RelayCommand]
    private void TemplateManager()
    {
        StatusText = "Opening Template Manager...";

        System.Windows.MessageBox.Show(
            "Template Manager\n\n" +
            "Current Templates: 11 built-in templates\n\n" +
            "Features:\n" +
            "• View all available templates\n" +
            "• Create custom templates\n" +
            "• Edit template metadata\n" +
            "• Export/Import templates\n\n" +
            "Access templates via:\n" +
            "File → New from Template",
            "Template Manager",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    [RelayCommand]
    private void ClientTokens()
    {
        StatusText = "Opening Client Registration Tokens...";

        System.Windows.MessageBox.Show(
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
            "Client Registration Tokens",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

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

            var backupService = App.GetService<BackupService>();

            // Create backup
            var result = await backupService.CreateBackupAsync(saveDialog.FileName);

            if (result.IsSuccess)
            {
                StatusText = $"Backup created successfully: {saveDialog.FileName}";
                _logger.LogInformation("Database backup created successfully");

                var fileInfo = new System.IO.FileInfo(saveDialog.FileName);
                System.Windows.MessageBox.Show(
                    $"Database backup created successfully!\n\n" +
                    $"Location: {saveDialog.FileName}\n\n" +
                    $"Size: {fileInfo.Length / 1024:N0} KB\n" +
                    $"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    "Backup Success",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            else
            {
                StatusText = "Backup failed";
                _logger.LogError("Database backup failed: {Error}", result.Error);

                System.Windows.MessageBox.Show(
                    $"Database backup failed:\n\n{result.Error}\n\n" +
                    $"Please check:\n" +
                    $"- Sufficient disk space\n" +
                    $"- Write permissions to target directory\n" +
                    $"- Database is not locked by another process",
                    "Backup Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating database backup");
            StatusText = "Backup failed";
            System.Windows.MessageBox.Show(
                $"Error creating backup:\n\n{ex.Message}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task RestoreDatabase()
    {
        try
        {
            _logger.LogWarning("User initiated database restore - showing warning dialog");

            // FIRST WARNING CONFIRMATION
            var warningResult = System.Windows.MessageBox.Show(
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
                "Restore Database - WARNING",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (warningResult != System.Windows.MessageBoxResult.Yes)
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
            var finalResult = System.Windows.MessageBox.Show(
                $"⚠️ FINAL CONFIRMATION\n\n" +
                $"Are you ABSOLUTELY SURE you want to restore from:\n\n" +
                $"{openDialog.FileName}\n\n" +
                $"Current database will be REPLACED and CANNOT be recovered!\n\n" +
                $"A safety backup will be created automatically before restore.\n\n" +
                $"Continue with restore?",
                "Final Confirmation - Restore Database",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Stop);

            if (finalResult != System.Windows.MessageBoxResult.Yes)
            {
                StatusText = "Database restore cancelled";
                _logger.LogInformation("Database restore cancelled by user at final confirmation");
                return;
            }

            // Show progress
            StatusText = "Restoring database from backup...";
            _logger.LogWarning("Starting database restore from: {FilePath}", openDialog.FileName);

            var backupService = App.GetService<BackupService>();

            // Restore backup
            var result = await backupService.RestoreBackupAsync(openDialog.FileName);

            if (result.IsSuccess)
            {
                StatusText = "Database restored successfully - application will restart";
                _logger.LogInformation("Database restored successfully");

                System.Windows.MessageBox.Show(
                    "Database restored successfully!\n\n" +
                    "A safety backup of the previous database has been created.\n\n" +
                    "The application will now RESTART to apply changes.\n\n" +
                    "Please wait for the application to restart automatically.",
                    "Restore Success",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);

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

                System.Windows.MessageBox.Show(
                    $"Database restore failed:\n\n{result.Error}\n\n" +
                    $"The current database has been preserved.\n\n" +
                    $"Please check:\n" +
                    $"- Backup file is valid\n" +
                    $"- Backup file is not corrupted\n" +
                    $"- Sufficient disk space available\n" +
                    $"- Database is not locked by another process",
                    "Restore Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring database backup");
            StatusText = "Database restore failed";
            System.Windows.MessageBox.Show(
                $"Error restoring backup:\n\n{ex.Message}\n\n" +
                $"The current database has been preserved.",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
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

    #endregion

    [RelayCommand]
    private async Task RefreshClients()
    {
        StatusText = "Refreshing clients...";
        await RefreshClientsAsync();
        StatusText = $"Clients refreshed - {Clients.Count} found";
    }

    [RelayCommand]
    private void AddDevice()
    {
        // TODO: Implement add device dialog
        StatusText = "Add device...";
    }

    [RelayCommand]
    private async Task RemoveDevice()
    {
        if (SelectedClient == null) return;

        try
        {
            await _clientService.RemoveClientAsync(SelectedClient.Id);
            Clients.Remove(SelectedClient);
            StatusText = $"Removed client: {SelectedClient.Name}";
            SelectedClient = null;
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to remove client: {ex.Message}";
        }
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
            // Dispose all sub-viewmodels
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

            // Unregister event handlers
            _communicationService.ClientConnected -= OnClientConnected;
            _communicationService.ClientDisconnected -= OnClientDisconnected;
        }

        _disposed = true;
    }
}
