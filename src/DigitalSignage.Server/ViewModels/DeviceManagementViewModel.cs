using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace DigitalSignage.Server.ViewModels;

public partial class DeviceManagementViewModel : ObservableObject, IDisposable
{
    private readonly IClientService _clientService;
    private readonly ILayoutService _layoutService;
    private readonly ILogger<DeviceManagementViewModel> _logger;
    private readonly IServiceProvider _serviceProvider;
    private bool _disposed = false;

    [ObservableProperty]
    private RaspberryPiClient? _selectedClient;

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    /// <summary>
    /// Gets the discovered devices ViewModel
    /// </summary>
    public DiscoveredDevicesViewModel DiscoveredDevices { get; }

    [ObservableProperty]
    private int _volumeLevel = 50;

    [ObservableProperty]
    private string _configServerHost = "localhost";

    [ObservableProperty]
    private int _configServerPort = 8080;

    [ObservableProperty]
    private bool _configUseSSL = false;

    [ObservableProperty]
    private bool _configVerifySSL = true;

    [ObservableProperty]
    private bool _configFullScreen = true;

    [ObservableProperty]
    private string _configLogLevel = "INFO";

    [ObservableProperty]
    private string? _selectedLayoutId;

    public ObservableCollection<RaspberryPiClient> Clients { get; } = new();
    public ObservableCollection<DisplayLayout> AvailableLayouts { get; } = new();
    public ObservableCollection<string> LogLevels { get; } = new() { "DEBUG", "INFO", "WARNING", "ERROR", "CRITICAL" };

    public DeviceManagementViewModel(
        IClientService clientService,
        ILayoutService layoutService,
        DiscoveredDevicesViewModel discoveredDevicesViewModel,
        ILogger<DeviceManagementViewModel> logger,
        IServiceProvider serviceProvider)
    {
        _clientService = clientService ?? throw new ArgumentNullException(nameof(clientService));
        _layoutService = layoutService ?? throw new ArgumentNullException(nameof(layoutService));
        DiscoveredDevices = discoveredDevicesViewModel ?? throw new ArgumentNullException(nameof(discoveredDevicesViewModel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        // Subscribe to client events for auto-refresh
        _clientService.ClientConnected += OnClientConnected;
        _clientService.ClientDisconnected += OnClientDisconnected;
        _clientService.ClientStatusChanged += OnClientStatusChanged;

        _ = LoadClientsCommand.ExecuteAsync(null);
        _ = LoadLayoutsCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task LoadClients()
    {
        IsLoading = true;
        StatusMessage = "Loading clients...";
        try
        {
            var clients = await _clientService.GetAllClientsAsync();

            // Load all layouts to populate AssignedLayout navigation property
            var layouts = await _layoutService.GetAllLayoutsAsync();
            var layoutDict = layouts.ToDictionary(l => l.Id, l => l);

            Clients.Clear();
            foreach (var client in clients)
            {
                // Populate AssignedLayout navigation property for display
                if (!string.IsNullOrEmpty(client.AssignedLayoutId) && layoutDict.TryGetValue(client.AssignedLayoutId, out var layout))
                {
                    client.AssignedLayout = layout;
                }

                Clients.Add(client);
            }
            StatusMessage = $"Loaded {Clients.Count} client(s)";
            _logger.LogInformation("Loaded {Count} clients", Clients.Count);

            // Also refresh available layouts when loading clients
            await RefreshAvailableLayoutsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading clients: {ex.Message}";
            _logger.LogError(ex, "Failed to load clients");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadLayouts()
    {
        await RefreshAvailableLayoutsAsync();
    }

    /// <summary>
    /// Refreshes the available layouts list from the LayoutService
    /// </summary>
    private async Task RefreshAvailableLayoutsAsync()
    {
        try
        {
            var layouts = await _layoutService.GetAllLayoutsAsync();
            AvailableLayouts.Clear();

            // Add "No Layout" option as first item
            AvailableLayouts.Add(new DisplayLayout
            {
                Id = Guid.Empty.ToString(),
                Name = "- Nicht zugewiesen -",
                Description = "Kein Layout zugewiesen"
            });

            foreach (var layout in layouts)
            {
                AvailableLayouts.Add(layout);
            }
            _logger.LogInformation("Refreshed {Count} available layouts (plus 'No Layout' option)", layouts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh available layouts");
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteClientCommand))]
    private async Task RestartClient()
    {
        if (SelectedClient == null) return;

        try
        {
            await _clientService.SendCommandAsync(
                SelectedClient.Id,
                ClientCommands.Restart);

            StatusMessage = $"Restart command sent to {SelectedClient.Name}";
            _logger.LogInformation("Restart command sent to client {ClientId}", SelectedClient.Id);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to restart {SelectedClient.Name}: {ex.Message}";
            _logger.LogError(ex, "Failed to send restart command to client {ClientId}", SelectedClient.Id);
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteClientCommand))]
    private async Task RestartClientApp()
    {
        if (SelectedClient == null) return;

        try
        {
            await _clientService.SendCommandAsync(
                SelectedClient.Id,
                ClientCommands.RestartApp);

            StatusMessage = $"App restart command sent to {SelectedClient.Name}";
            _logger.LogInformation("App restart command sent to client {ClientId}", SelectedClient.Id);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to restart app on {SelectedClient.Name}: {ex.Message}";
            _logger.LogError(ex, "Failed to send restart app command to client {ClientId}", SelectedClient.Id);
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteClientCommand))]
    private async Task TakeScreenshot()
    {
        if (SelectedClient == null) return;

        try
        {
            await _clientService.SendCommandAsync(
                SelectedClient.Id,
                ClientCommands.Screenshot);

            StatusMessage = $"Screenshot requested from {SelectedClient.Name}";
            _logger.LogInformation("Screenshot command sent to client {ClientId}", SelectedClient.Id);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to take screenshot on {SelectedClient.Name}: {ex.Message}";
            _logger.LogError(ex, "Failed to send screenshot command to client {ClientId}", SelectedClient.Id);
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteClientCommand))]
    private async Task ScreenOn()
    {
        if (SelectedClient == null) return;

        try
        {
            await _clientService.SendCommandAsync(
                SelectedClient.Id,
                ClientCommands.ScreenOn);

            StatusMessage = $"Screen on command sent to {SelectedClient.Name}";
            _logger.LogInformation("Screen on command sent to client {ClientId}", SelectedClient.Id);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to turn screen on for {SelectedClient.Name}: {ex.Message}";
            _logger.LogError(ex, "Failed to send screen on command to client {ClientId}", SelectedClient.Id);
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteClientCommand))]
    private async Task ScreenOff()
    {
        if (SelectedClient == null) return;

        try
        {
            await _clientService.SendCommandAsync(
                SelectedClient.Id,
                ClientCommands.ScreenOff);

            StatusMessage = $"Screen off command sent to {SelectedClient.Name}";
            _logger.LogInformation("Screen off command sent to client {ClientId}", SelectedClient.Id);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to turn screen off for {SelectedClient.Name}: {ex.Message}";
            _logger.LogError(ex, "Failed to send screen off command to client {ClientId}", SelectedClient.Id);
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteClientCommand))]
    private async Task SetVolume()
    {
        if (SelectedClient == null) return;

        try
        {
            await _clientService.SendCommandAsync(
                SelectedClient.Id,
                ClientCommands.SetVolume,
                new Dictionary<string, object> { ["volume"] = VolumeLevel });

            StatusMessage = $"Volume set to {VolumeLevel}% on {SelectedClient.Name}";
            _logger.LogInformation("Set volume to {Volume}% on client {ClientId}", VolumeLevel, SelectedClient.Id);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to set volume on {SelectedClient.Name}: {ex.Message}";
            _logger.LogError(ex, "Failed to send set volume command to client {ClientId}", SelectedClient.Id);
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteClientCommand))]
    private async Task ClearCache()
    {
        if (SelectedClient == null) return;

        try
        {
            await _clientService.SendCommandAsync(
                SelectedClient.Id,
                ClientCommands.ClearCache);

            StatusMessage = $"Cache clear command sent to {SelectedClient.Name}";
            _logger.LogInformation("Clear cache command sent to client {ClientId}", SelectedClient.Id);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to clear cache on {SelectedClient.Name}: {ex.Message}";
            _logger.LogError(ex, "Failed to send clear cache command to client {ClientId}", SelectedClient.Id);
        }
    }

    [RelayCommand(CanExecute = nameof(CanAssignLayout))]
    private async Task AssignLayout()
    {
        if (SelectedClient == null || string.IsNullOrEmpty(SelectedLayoutId)) return;

        try
        {
            await _clientService.AssignLayoutAsync(SelectedClient.Id, SelectedLayoutId);
            SelectedClient.AssignedLayoutId = SelectedLayoutId;

            var layout = AvailableLayouts.FirstOrDefault(l => l.Id == SelectedLayoutId);
            StatusMessage = $"Assigned layout '{layout?.Name ?? SelectedLayoutId}' to {SelectedClient.Name}";
            _logger.LogInformation("Assigned layout {LayoutId} to client {ClientId}", SelectedLayoutId, SelectedClient.Id);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to assign layout to {SelectedClient.Name}: {ex.Message}";
            _logger.LogError(ex, "Failed to assign layout {LayoutId} to client {ClientId}", SelectedLayoutId, SelectedClient.Id);
        }
    }

    private bool CanAssignLayout()
    {
        return SelectedClient != null && !string.IsNullOrEmpty(SelectedLayoutId) && !IsLoading;
    }

    [RelayCommand(CanExecute = nameof(CanExecuteClientCommand))]
    private async Task RemoveClient()
    {
        if (SelectedClient == null) return;

        var clientId = SelectedClient.Id;
        var clientName = SelectedClient.Name;
        try
        {
            await _clientService.RemoveClientAsync(clientId);
            await LoadClientsCommand.ExecuteAsync(null);

            StatusMessage = $"Removed client {clientName}";
            _logger.LogInformation("Removed client {ClientId}", clientId);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to remove client {clientName}: {ex.Message}";
            _logger.LogError(ex, "Failed to remove client {ClientId}", clientId);
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteClientCommand))]
    private async Task UpdateClientConfig()
    {
        if (SelectedClient == null) return;

        try
        {
            var configMessage = new UpdateConfigMessage
            {
                ServerHost = ConfigServerHost,
                ServerPort = ConfigServerPort,
                UseSSL = ConfigUseSSL,
                VerifySSL = ConfigVerifySSL,
                FullScreen = ConfigFullScreen,
                LogLevel = ConfigLogLevel
            };

            await _clientService.UpdateClientConfigAsync(SelectedClient.Id, configMessage);

            StatusMessage = $"Configuration update sent to {SelectedClient.Name}";
            _logger.LogInformation("Configuration update sent to client {ClientId}", SelectedClient.Id);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to update configuration for {SelectedClient.Name}: {ex.Message}";
            _logger.LogError(ex, "Failed to update configuration for client {ClientId}", SelectedClient.Id);
        }
    }

    [RelayCommand]
    private void OpenWebInterface(RaspberryPiClient? client)
    {
        if (client == null || string.IsNullOrEmpty(client.IpAddress)) return;

        try
        {
            var url = $"http://{client.IpAddress}:5000";

            var psi = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };

            Process.Start(psi);

            var hostname = !string.IsNullOrEmpty(client.DeviceInfo.Hostname)
                ? client.DeviceInfo.Hostname
                : client.Name;
            StatusMessage = $"Opened web interface for {hostname}";
            _logger.LogInformation("Opened web interface for client {ClientId} at {Url}", client.Id, url);
        }
        catch (Exception ex)
        {
            var hostname = client.DeviceInfo?.Hostname ?? client.Name;
            StatusMessage = $"Failed to open web interface for {hostname}: {ex.Message}";
            _logger.LogError(ex, "Failed to open web interface for client {ClientId}", client.Id);
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteClientCommand))]
    private void ShowDeviceDetails()
    {
        if (SelectedClient == null) return;

        try
        {
            _logger.LogInformation("Opening device details for {ClientName}", SelectedClient.Name);
            StatusMessage = $"Opening device details for {SelectedClient.Name}...";

            var viewModel = _serviceProvider.GetRequiredService<DeviceDetailViewModel>();
            var window = new Views.DeviceDetailWindow(viewModel)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            // Load the device information into the view model
            viewModel.LoadDeviceInfo(SelectedClient);

            window.Show();
            StatusMessage = $"Device details opened for {SelectedClient.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to open device details: {ex.Message}";
            _logger.LogError(ex, "Failed to open device details for client {ClientId}", SelectedClient?.Id);
        }
    }

    private bool CanExecuteClientCommand()
    {
        return SelectedClient != null && !IsLoading;
    }

    partial void OnSelectedClientChanged(RaspberryPiClient? value)
    {
        // Update CanExecute for all commands that depend on SelectedClient
        RestartClientCommand.NotifyCanExecuteChanged();
        RestartClientAppCommand.NotifyCanExecuteChanged();
        TakeScreenshotCommand.NotifyCanExecuteChanged();
        ScreenOnCommand.NotifyCanExecuteChanged();
        ScreenOffCommand.NotifyCanExecuteChanged();
        SetVolumeCommand.NotifyCanExecuteChanged();
        ClearCacheCommand.NotifyCanExecuteChanged();
        RemoveClientCommand.NotifyCanExecuteChanged();
        UpdateClientConfigCommand.NotifyCanExecuteChanged();
        AssignLayoutCommand.NotifyCanExecuteChanged();
        ShowDeviceDetailsCommand.NotifyCanExecuteChanged();

        if (value != null)
        {
            // Pre-select the currently assigned layout in the ComboBox
            SelectedLayoutId = value.AssignedLayoutId;
            StatusMessage = $"Selected: {value.Name} ({value.Status})";
            _logger.LogDebug("Selected client {ClientId}", value.Id);
        }
        else
        {
            SelectedLayoutId = null;
        }
    }

    partial void OnSelectedLayoutIdChanged(string? value)
    {
        AssignLayoutCommand.NotifyCanExecuteChanged();
    }

    private void OnClientConnected(object? sender, string clientId)
    {
        _logger.LogInformation("Client connected event received: {ClientId}", clientId);

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null) return;

        // Check if already on UI thread to avoid unnecessary context switch
        if (dispatcher.CheckAccess())
        {
            _ = LoadClientsCommand.ExecuteAsync(null);
            StatusMessage = $"Client {clientId} connected";
        }
        else
        {
            dispatcher.InvokeAsync(async () =>
            {
                await LoadClientsCommand.ExecuteAsync(null);
                StatusMessage = $"Client {clientId} connected";
            });
        }
    }

    private void OnClientDisconnected(object? sender, string clientId)
    {
        _logger.LogInformation("Client disconnected event received: {ClientId}", clientId);

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null) return;

        // Check if already on UI thread to avoid unnecessary context switch
        if (dispatcher.CheckAccess())
        {
            var client = Clients.FirstOrDefault(c => c.Id == clientId);
            if (client != null)
            {
                client.Status = ClientStatus.Offline;
                StatusMessage = $"Client {client.Name} disconnected";
            }
        }
        else
        {
            dispatcher.InvokeAsync(() =>
            {
                var client = Clients.FirstOrDefault(c => c.Id == clientId);
                if (client != null)
                {
                    client.Status = ClientStatus.Offline;
                    StatusMessage = $"Client {client.Name} disconnected";
                }
            });
        }
    }

    private void OnClientStatusChanged(object? sender, string clientId)
    {
        _logger.LogDebug("Client status changed event received: {ClientId}", clientId);

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null) return;

        // Check if already on UI thread to avoid unnecessary context switch
        if (dispatcher.CheckAccess())
        {
            _ = LoadClientsCommand.ExecuteAsync(null);
        }
        else
        {
            dispatcher.InvokeAsync(async () =>
            {
                await LoadClientsCommand.ExecuteAsync(null);
            });
        }
    }

    /// <summary>
    /// Disposes resources used by this ViewModel
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes managed and unmanaged resources
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Unregister event handlers
            _clientService.ClientConnected -= OnClientConnected;
            _clientService.ClientDisconnected -= OnClientDisconnected;
            _clientService.ClientStatusChanged -= OnClientStatusChanged;

            // Dispose discovered devices ViewModel
            DiscoveredDevices?.Dispose();

            _logger.LogInformation("DeviceManagementViewModel disposed");
        }

        _disposed = true;
    }
}
