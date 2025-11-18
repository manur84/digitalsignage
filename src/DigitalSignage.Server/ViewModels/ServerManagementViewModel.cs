using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using DigitalSignage.Server.Services;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// Manages server operations and client connections.
/// Extracted from MainViewModel to follow Single Responsibility Principle.
/// </summary>
public partial class ServerManagementViewModel : ObservableObject, IDisposable
{
    private readonly IClientService _clientService;
    private readonly ICommunicationService _communicationService;
    private readonly ILogger<ServerManagementViewModel> _logger;
    private readonly IDialogService _dialogService;
    private readonly ISynchronizationContext _syncContext;
    private bool _disposed = false;

    [ObservableProperty]
    private string _serverStatus = "Stopped";

    [ObservableProperty]
    private int _connectedClients = 0;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private RaspberryPiClient? _selectedClient;

    public ObservableCollection<RaspberryPiClient> Clients { get; } = new();

    public ServerManagementViewModel(
        IClientService clientService,
        ICommunicationService communicationService,
        IDialogService dialogService,
        ISynchronizationContext syncContext,
        ILogger<ServerManagementViewModel> logger)
    {
        _clientService = clientService ?? throw new ArgumentNullException(nameof(clientService));
        _communicationService = communicationService ?? throw new ArgumentNullException(nameof(communicationService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _syncContext = syncContext ?? throw new ArgumentNullException(nameof(syncContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Subscribe to communication events
        _communicationService.ClientConnected += OnClientConnected;
        _communicationService.ClientDisconnected += OnClientDisconnected;

        // Auto-start server
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

            await _dialogService.ShowWarningAsync(
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
                "Permission Error - Digital Signage Server");
        }
        catch (Exception ex)
        {
            ServerStatus = "Error";
            StatusText = $"Failed to start server: {ex.Message}";
            _logger.LogError(ex, "Failed to start server");

            await _dialogService.ShowErrorAsync(
                $"Failed to start Digital Signage Server\n\n" +
                $"Error: {ex.Message}\n\n" +
                $"Common Solutions:\n" +
                $"- Run diagnose-server.ps1 for diagnostics\n" +
                $"- Check that port 8080 is not in use\n" +
                $"- Use fix-and-run.bat for automatic fix\n\n" +
                $"Check the logs folder for detailed error information.",
                "Startup Error - Digital Signage Server");
        }
    }

    // Event handlers must be async void, but delegate to async Task for better exception handling
    private async void OnClientConnected(object? sender, ClientConnectedEventArgs e)
        => await HandleClientConnectedAsync(e);

    private async void OnClientDisconnected(object? sender, ClientDisconnectedEventArgs e)
        => await HandleClientDisconnectedAsync(e);

    private async Task HandleClientConnectedAsync(ClientConnectedEventArgs e)
    {
        try
        {
            ConnectedClients++;
            StatusText = $"Client connected: {e.ClientId}";
            await RefreshClientsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle client connected event for client {ClientId}", e.ClientId);
            StatusText = $"Error handling client connection: {ex.Message}";
        }
    }

    private async Task HandleClientDisconnectedAsync(ClientDisconnectedEventArgs e)
    {
        try
        {
            ConnectedClients--;
            StatusText = $"Client disconnected: {e.ClientId}";
            await RefreshClientsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle client disconnected event for client {ClientId}", e.ClientId);
            StatusText = $"Error handling client disconnection: {ex.Message}";
        }
    }

    private async Task RefreshClientsAsync()
    {
        try
        {
            var clientsResult = await _clientService.GetAllClientsAsync();

            if (clientsResult.IsFailure)
            {
                _logger.LogError("Failed to load clients: {ErrorMessage}", clientsResult.ErrorMessage);
                StatusText = $"Failed to refresh clients: {clientsResult.ErrorMessage}";
                return;
            }

            var clients = clientsResult.Value;

            await _syncContext.RunOnUiThreadAsync(() =>
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
            var removeResult = await _clientService.RemoveClientAsync(SelectedClient.Id);

            if (removeResult.IsFailure)
            {
                _logger.LogError("Failed to remove client: {ErrorMessage}", removeResult.ErrorMessage);
                StatusText = $"Failed to remove client: {removeResult.ErrorMessage}";
                return;
            }

            Clients.Remove(SelectedClient);
            StatusText = $"Removed client: {SelectedClient.Name}";
            SelectedClient = null;
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to remove client: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ServerConfiguration()
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

            await _dialogService.ShowInformationAsync(
                config.ToString(),
                "Server Configuration");

            StatusText = "Server configuration displayed";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get server configuration");
            StatusText = $"Configuration error: {ex.Message}";
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
            // Unregister event handlers
            _communicationService.ClientConnected -= OnClientConnected;
            _communicationService.ClientDisconnected -= OnClientDisconnected;
        }

        _disposed = true;
    }
}
