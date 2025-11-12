using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using DigitalSignage.Data;
using DigitalSignage.Server.Views;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Input;

namespace DigitalSignage.Server.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ILayoutService _layoutService;
    private readonly IClientService _clientService;
    private readonly ICommunicationService _communicationService;
    private readonly DigitalSignageDbContext _dbContext;
    private readonly ILogger<MainViewModel> _logger;

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

    private bool CanSave() => CurrentLayout != null;

    partial void OnCurrentLayoutChanged(DisplayLayout? value)
    {
        SaveCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task SaveAs()
    {
        // TODO: Implement save as dialog
        StatusText = "Save as...";
    }

    [RelayCommand]
    private async Task Export()
    {
        if (CurrentLayout == null) return;

        try
        {
            var json = await _layoutService.ExportLayoutAsync(CurrentLayout.Id);
            // TODO: Save to file
            StatusText = "Layout exported";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to export: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task Import()
    {
        // TODO: Implement import dialog
        StatusText = "Importing layout...";
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
        StatusText = "Opening database connection...";
    }

    [RelayCommand]
    private void Settings()
    {
        StatusText = "Opening settings...";
    }

    [RelayCommand]
    private void Logs()
    {
        StatusText = "Opening logs...";
    }

    [RelayCommand]
    private void Documentation()
    {
        StatusText = "Opening documentation...";
    }

    [RelayCommand]
    private void About()
    {
        StatusText = "Digital Signage Manager v1.0";
    }

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
}
