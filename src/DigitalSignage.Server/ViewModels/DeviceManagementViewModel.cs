using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace DigitalSignage.Server.ViewModels;

public partial class DeviceManagementViewModel : ObservableObject
{
    private readonly IClientService _clientService;
    private readonly ILayoutService _layoutService;
    private readonly ILogger<DeviceManagementViewModel> _logger;

    [ObservableProperty]
    private RaspberryPiClient? _selectedClient;

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private string _statusMessage = "Ready";

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

    public ObservableCollection<RaspberryPiClient> Clients { get; } = new();
    public ObservableCollection<DisplayLayout> AvailableLayouts { get; } = new();
    public ObservableCollection<string> LogLevels { get; } = new() { "DEBUG", "INFO", "WARNING", "ERROR", "CRITICAL" };

    public DeviceManagementViewModel(
        IClientService clientService,
        ILayoutService layoutService,
        ILogger<DeviceManagementViewModel> logger)
    {
        _clientService = clientService ?? throw new ArgumentNullException(nameof(clientService));
        _layoutService = layoutService ?? throw new ArgumentNullException(nameof(layoutService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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
            Clients.Clear();
            foreach (var client in clients)
            {
                Clients.Add(client);
            }
            StatusMessage = $"Loaded {Clients.Count} client(s)";
            _logger.LogInformation("Loaded {Count} clients", Clients.Count);
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
        try
        {
            var layouts = await _layoutService.GetAllLayoutsAsync();
            AvailableLayouts.Clear();
            foreach (var layout in layouts)
            {
                AvailableLayouts.Add(layout);
            }
            _logger.LogInformation("Loaded {Count} layouts", AvailableLayouts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load layouts");
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

    [RelayCommand]
    private async Task AssignLayout(string layoutId)
    {
        if (SelectedClient == null || string.IsNullOrEmpty(layoutId)) return;

        try
        {
            await _clientService.AssignLayoutAsync(SelectedClient.Id, layoutId);
            SelectedClient.AssignedLayoutId = layoutId;

            var layout = AvailableLayouts.FirstOrDefault(l => l.Id == layoutId);
            StatusMessage = $"Assigned layout '{layout?.Name ?? layoutId}' to {SelectedClient.Name}";
            _logger.LogInformation("Assigned layout {LayoutId} to client {ClientId}", layoutId, SelectedClient.Id);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to assign layout to {SelectedClient.Name}: {ex.Message}";
            _logger.LogError(ex, "Failed to assign layout {LayoutId} to client {ClientId}", layoutId, SelectedClient.Id);
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteClientCommand))]
    private async Task RemoveClient()
    {
        if (SelectedClient == null) return;

        var clientName = SelectedClient.Name;
        try
        {
            await _clientService.RemoveClientAsync(SelectedClient.Id);
            await LoadClientsCommand.ExecuteAsync(null);

            StatusMessage = $"Removed client {clientName}";
            _logger.LogInformation("Removed client {ClientId}", SelectedClient.Id);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to remove client {clientName}: {ex.Message}";
            _logger.LogError(ex, "Failed to remove client {ClientId}", SelectedClient.Id);
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

        if (value != null)
        {
            StatusMessage = $"Selected: {value.Name} ({value.Status})";
            _logger.LogDebug("Selected client {ClientId}", value.Id);
        }
    }
}
