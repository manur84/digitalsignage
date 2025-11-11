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

    public MainViewModel(
        ILayoutService layoutService,
        IClientService clientService,
        ICommunicationService communicationService,
        DesignerViewModel designerViewModel,
        DeviceManagementViewModel deviceManagementViewModel,
        DataSourceViewModel dataSourceViewModel,
        PreviewViewModel previewViewModel,
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
        }
        catch (Exception ex)
        {
            ServerStatus = "Error";
            StatusText = $"Failed to start server: {ex.Message}";
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
        CurrentLayout = new DisplayLayout
        {
            Name = "New Layout",
            Resolution = new Resolution { Width = 1920, Height = 1080 }
        };
        StatusText = "New layout created";
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
        // TODO: Implement layout selection dialog
        StatusText = "Opening layout...";
    }

    [RelayCommand]
    private async Task Save()
    {
        if (CurrentLayout == null)
        {
            StatusText = "No layout to save";
            return;
        }

        try
        {
            if (string.IsNullOrEmpty(CurrentLayout.Id))
            {
                await _layoutService.CreateLayoutAsync(CurrentLayout);
            }
            else
            {
                await _layoutService.UpdateLayoutAsync(CurrentLayout);
            }
            StatusText = "Layout saved successfully";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to save layout: {ex.Message}";
        }
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
        // TODO: Implement undo
        StatusText = "Undo";
    }

    [RelayCommand]
    private void Redo()
    {
        // TODO: Implement redo
        StatusText = "Redo";
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
