using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using DigitalSignage.Server.Views.DeviceManagement; // hinzugefügt
using DigitalSignage.Server.Services;

namespace DigitalSignage.Server.ViewModels;

public partial class DeviceManagementViewModel : ObservableObject, IDisposable
{
    private readonly IClientService _clientService;
    private readonly ILayoutService _layoutService;
    private readonly ILogger<DeviceManagementViewModel> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ISynchronizationContext _syncContext;
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

    public string ClientStatusSummary => BuildClientStatusSummary();

    public DeviceManagementViewModel(
        IClientService clientService,
        ILayoutService layoutService,
        DiscoveredDevicesViewModel discoveredDevicesViewModel,
        ILogger<DeviceManagementViewModel> logger,
        IServiceProvider serviceProvider,
        ISynchronizationContext syncContext)
    {
        _clientService = clientService ?? throw new ArgumentNullException(nameof(clientService));
        _layoutService = layoutService ?? throw new ArgumentNullException(nameof(layoutService));
        DiscoveredDevices = discoveredDevicesViewModel ?? throw new ArgumentNullException(nameof(discoveredDevicesViewModel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _syncContext = syncContext ?? throw new ArgumentNullException(nameof(syncContext));

        Clients.CollectionChanged += Clients_CollectionChanged;

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
            var clientsResult = await _clientService.GetAllClientsAsync();

            if (clientsResult.IsFailure || clientsResult.Value == null)
            {
                _logger.LogError("Failed to load clients: {ErrorMessage}", clientsResult.ErrorMessage ?? "Null result");
                StatusMessage = $"Failed to load clients: {clientsResult.ErrorMessage ?? "Null result"}";
                IsLoading = false;
                return;
            }

            var clients = clientsResult.Value;

            // Load all layouts to populate AssignedLayout navigation property
            var layoutsResult = await _layoutService.GetAllLayoutsAsync();
            if (layoutsResult.IsFailure)
            {
                _logger.LogError("Failed to load layouts: {ErrorMessage}", layoutsResult.ErrorMessage);
                // Continue without layouts - clients will still be displayed
            }

            var layoutDict = layoutsResult.IsSuccess && layoutsResult.Value != null
                ? layoutsResult.Value.ToDictionary(l => l.Id, l => l)
                : new Dictionary<string, DisplayLayout>();

            // Preserve current selection (by Id) so UI selection isn't lost when we refresh the collection
            var previousSelectedId = SelectedClient?.Id;

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

            // Try to restore selection if possible
            if (!string.IsNullOrEmpty(previousSelectedId))
            {
                var restored = Clients.FirstOrDefault(c => c.Id == previousSelectedId);
                if (restored != null)
                {
                    SelectedClient = restored;
                }
            }

            StatusMessage = $"Loaded {Clients.Count} client(s)";
            _logger.LogInformation("Loaded {Count} clients", Clients.Count);
            RaiseClientStatusSummaryChanged();

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
            var layoutsResult = await _layoutService.GetAllLayoutsAsync();
            if (layoutsResult.IsFailure || layoutsResult.Value == null)
            {
                _logger.LogError("Failed to load layouts: {ErrorMessage}", layoutsResult.ErrorMessage ?? "Null result");
                return;
            }

            AvailableLayouts.Clear();

            // Add "No Layout" option as first item
            AvailableLayouts.Add(new DisplayLayout
            {
                Id = Guid.Empty.ToString(),
                Name = "- Nicht zugewiesen -",
                Description = "Kein Layout zugewiesen"
            });

            foreach (var layout in layoutsResult.Value)
            {
                AvailableLayouts.Add(layout);
            }
            _logger.LogInformation("Refreshed {Count} available layouts (plus 'No Layout' option)", layoutsResult.Value.Count);
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
            var result = await _clientService.SendCommandAsync(
                SelectedClient.Id,
                ClientCommands.Restart);

            if (result.IsFailure)
            {
                StatusMessage = $"Failed to restart {SelectedClient.Name}: {result.ErrorMessage}";
                _logger.LogError("Failed to send restart command to client {ClientId}: {ErrorMessage}", SelectedClient.Id, result.ErrorMessage);
                return;
            }

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
            var result = await _clientService.SendCommandAsync(
                SelectedClient.Id,
                ClientCommands.RestartApp);

            if (result.IsFailure)
            {
                StatusMessage = $"Failed to restart app on {SelectedClient.Name}: {result.ErrorMessage}";
                _logger.LogError("Failed to send restart app command to client {ClientId}: {ErrorMessage}", SelectedClient.Id, result.ErrorMessage);
                return;
            }

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
            var result = await _clientService.SendCommandAsync(
                SelectedClient.Id,
                ClientCommands.Screenshot);

            if (result.IsFailure)
            {
                StatusMessage = $"Failed to take screenshot on {SelectedClient.Name}: {result.ErrorMessage}";
                _logger.LogError("Failed to send screenshot command to client {ClientId}: {ErrorMessage}", SelectedClient.Id, result.ErrorMessage);
                return;
            }

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
            var result = await _clientService.SendCommandAsync(
                SelectedClient.Id,
                ClientCommands.ScreenOn);

            if (result.IsFailure)
            {
                StatusMessage = $"Failed to turn screen on for {SelectedClient.Name}: {result.ErrorMessage}";
                _logger.LogError("Failed to send screen on command to client {ClientId}: {ErrorMessage}", SelectedClient.Id, result.ErrorMessage);
                return;
            }

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
            var result = await _clientService.SendCommandAsync(
                SelectedClient.Id,
                ClientCommands.ScreenOff);

            if (result.IsFailure)
            {
                StatusMessage = $"Failed to turn screen off for {SelectedClient.Name}: {result.ErrorMessage}";
                _logger.LogError("Failed to send screen off command to client {ClientId}: {ErrorMessage}", SelectedClient.Id, result.ErrorMessage);
                return;
            }

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
            var result = await _clientService.SendCommandAsync(
                SelectedClient.Id,
                ClientCommands.SetVolume,
                new Dictionary<string, object> { ["volume"] = VolumeLevel });

            if (result.IsFailure)
            {
                StatusMessage = $"Failed to set volume on {SelectedClient.Name}: {result.ErrorMessage}";
                _logger.LogError("Failed to send set volume command to client {ClientId}: {ErrorMessage}", SelectedClient.Id, result.ErrorMessage);
                return;
            }

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
            var result = await _clientService.SendCommandAsync(
                SelectedClient.Id,
                ClientCommands.ClearCache);

            if (result.IsFailure)
            {
                StatusMessage = $"Failed to clear cache on {SelectedClient.Name}: {result.ErrorMessage}";
                _logger.LogError("Failed to send clear cache command to client {ClientId}: {ErrorMessage}", SelectedClient.Id, result.ErrorMessage);
                return;
            }

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
            var result = await _clientService.AssignLayoutAsync(SelectedClient.Id, SelectedLayoutId);

            if (result.IsFailure)
            {
                StatusMessage = $"Failed to assign layout to {SelectedClient.Name}: {result.ErrorMessage}";
                _logger.LogError("Failed to assign layout {LayoutId} to client {ClientId}: {ErrorMessage}", SelectedLayoutId, SelectedClient.Id, result.ErrorMessage);
                return;
            }

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
            var result = await _clientService.RemoveClientAsync(clientId);

            if (result.IsFailure)
            {
                StatusMessage = $"Failed to remove client {clientName}: {result.ErrorMessage}";
                _logger.LogError("Failed to remove client {ClientId}: {ErrorMessage}", clientId, result.ErrorMessage);
                return;
            }

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

            var result = await _clientService.UpdateClientConfigAsync(SelectedClient.Id, configMessage);

            if (result.IsFailure)
            {
                StatusMessage = $"Failed to update configuration for {SelectedClient.Name}: {result.ErrorMessage}";
                _logger.LogError("Failed to update configuration for client {ClientId}: {ErrorMessage}", SelectedClient.Id, result.ErrorMessage);
                return;
            }

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
        var targetClient = client ?? SelectedClient;
        if (targetClient == null || string.IsNullOrWhiteSpace(targetClient.IpAddress)) return;

        try
        {
            var url = $"http://{targetClient.IpAddress}:5000";
            var window = new DeviceWebInterfaceWindow(url) // korrigiert
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            window.Show();

            var hostname = !string.IsNullOrEmpty(targetClient.DeviceInfo.Hostname) ? targetClient.DeviceInfo.Hostname : targetClient.Name;
            StatusMessage = $"Web Interface geöffnet (Vollbild) für {hostname}";
            _logger.LogInformation("Opened fullscreen web interface for client {ClientId} at {Url}", targetClient.Id, url);
        }
        catch (Exception ex)
        {
            var hostname = targetClient.DeviceInfo?.Hostname ?? targetClient.Name;
            StatusMessage = $"Fehler beim Öffnen Web Interface für {hostname}: {ex.Message}";
            _logger.LogError(ex, "Failed to open web interface for client {ClientId}", targetClient.Id);
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteClientCommand))]
    private async Task ShowDeviceDetails()
    {
        if (SelectedClient == null) return;

        try
        {
            _logger.LogInformation("Opening device details for {ClientName}", SelectedClient.Name);
            StatusMessage = $"Opening device details for {SelectedClient.Name}...";

            // CRITICAL FIX: Reload client from service to get fresh data including DeviceInfo
            // This ensures we have the latest database values merged with in-memory cache
            var clientResult = await _clientService.GetClientByIdAsync(SelectedClient.Id);
            if (clientResult.IsFailure || clientResult.Value == null)
            {
                _logger.LogError("Failed to reload client {ClientId}: {Error}", SelectedClient.Id, clientResult.ErrorMessage);
                StatusMessage = $"Failed to load client details: {clientResult.ErrorMessage}";
                return;
            }

            var freshClient = clientResult.Value;
            _logger.LogInformation("Reloaded client {ClientId} - DeviceInfo null: {IsNull}, Model: '{Model}'",
                freshClient.Id,
                freshClient.DeviceInfo == null,
                freshClient.DeviceInfo?.Model ?? "NULL");

            var viewModel = _serviceProvider.GetRequiredService<DeviceDetailViewModel>();
            var window = new Views.DeviceDetailWindow(viewModel)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            // Load the FRESH device information into the view model
            viewModel.LoadDeviceInfo(freshClient);

            window.Show();
            StatusMessage = $"Device details opened for {SelectedClient.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to open device details: {ex.Message}";
            _logger.LogError(ex, "Failed to open device details for client {ClientId}", SelectedClient?.Id);
        }
    }

    [RelayCommand]
    private void OpenClientInstaller()
    {
        ClientInstallerViewModel? installerViewModel = null;
        try
        {
            _logger.LogInformation("Opening client installer");
            StatusMessage = "Opening client installer...";

            installerViewModel = _serviceProvider.GetRequiredService<ClientInstallerViewModel>();
            installerViewModel.Initialize(DiscoveredDevices);

            var dialog = new Views.Dialogs.ClientInstallerDialog(installerViewModel)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            dialog.ShowDialog();
            StatusMessage = "Client installer closed";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to open client installer: {ex.Message}";
            _logger.LogError(ex, "Failed to open client installer dialog");
        }
        finally
        {
            installerViewModel?.Dispose();
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
        _ = _syncContext.RunOnUiThreadAsync(async () =>
        {
            await LoadClientsCommand.ExecuteAsync(null);
            StatusMessage = $"Client {clientId} connected";
        });
    }

    private void OnClientDisconnected(object? sender, string clientId)
    {
        _logger.LogInformation("Client disconnected event received: {ClientId}", clientId);
        _ = _syncContext.RunOnUiThreadAsync(() =>
        {
            var client = Clients.FirstOrDefault(c => c.Id == clientId);
            if (client != null)
            {
                client.Status = ClientStatus.Offline;
                StatusMessage = $"Client {client.Name} disconnected";
                RaiseClientStatusSummaryChanged();
            }
        });
    }

    private void OnClientStatusChanged(object? sender, string clientId)
    {
        _logger.LogDebug("Client status changed event received: {ClientId}", clientId);
        _ = _syncContext.RunOnUiThreadAsync(async () =>
        {
            await LoadClientsCommand.ExecuteAsync(null);
        });
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
            Clients.CollectionChanged -= Clients_CollectionChanged;
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

    private void Clients_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RaiseClientStatusSummaryChanged();
    }

    private void RaiseClientStatusSummaryChanged()
    {
        OnPropertyChanged(nameof(ClientStatusSummary));
    }

    private string BuildClientStatusSummary()
    {
        var total = Clients.Count;
        if (total == 0)
        {
            return "No registered devices";
        }

        int online = 0, offline = 0, connecting = 0, errors = 0;

        foreach (var client in Clients)
        {
            switch (client.Status)
            {
                case ClientStatus.Online:
                case ClientStatus.Updating:
                    online++;
                    break;
                case ClientStatus.Offline:
                case ClientStatus.Disconnected:
                case ClientStatus.OfflineRecovery:
                    offline++;
                    break;
                case ClientStatus.Connecting:
                    connecting++;
                    break;
                case ClientStatus.Error:
                    errors++;
                    break;
            }
        }

        var segments = new List<string>
        {
            $"{total} total",
            $"{online} online",
            $"{offline} offline"
        };

        if (connecting > 0)
        {
            segments.Add($"{connecting} connecting");
        }

        if (errors > 0)
        {
            segments.Add($"{errors} error");
        }

        return string.Join(" · ", segments);
    }
}
