using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

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
        ILogger<ServerManagementViewModel> logger)
    {
        _clientService = clientService ?? throw new ArgumentNullException(nameof(clientService));
        _communicationService = communicationService ?? throw new ArgumentNullException(nameof(communicationService));
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
