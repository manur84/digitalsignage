using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.App.Mobile.Models;
using DigitalSignage.App.Mobile.Services;

namespace DigitalSignage.App.Mobile.ViewModels;

/// <summary>
/// View model for the login page (server discovery and connection).
/// </summary>
public partial class LoginViewModel : BaseViewModel
{
	private readonly IServerDiscoveryService _discoveryService;
	private readonly IAuthenticationService _authService;
	private readonly ISecureStorageService _secureStorage;
	private readonly IWebSocketService _webSocketService;

	[ObservableProperty]
	private ObservableCollection<DiscoveredServer> _discoveredServers = new();

	[ObservableProperty]
	private DiscoveredServer? _selectedServer;

	[ObservableProperty]
	private bool _isScanning;

	[ObservableProperty]
	private bool _showManualEntry;

	[ObservableProperty]
	private string _manualServerUrl = string.Empty;

	[ObservableProperty]
	private string _registrationToken = string.Empty;

	[ObservableProperty]
	private string _statusMessage = "Tap 'Scan for Servers' to discover Digital Signage servers on your network";

	public LoginViewModel(
		IServerDiscoveryService discoveryService,
		IAuthenticationService authService,
		ISecureStorageService secureStorage,
		IWebSocketService webSocketService)
	{
		_discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
		_authService = authService ?? throw new ArgumentNullException(nameof(authService));
		_secureStorage = secureStorage ?? throw new ArgumentNullException(nameof(secureStorage));
		_webSocketService = webSocketService ?? throw new ArgumentNullException(nameof(webSocketService));

		Title = "Connect to Server";

		// Subscribe to discovery events
		_discoveryService.ServerDiscovered += OnServerDiscovered;
		_discoveryService.ServerLost += OnServerLost;
	}

	[RelayCommand]
	private async Task StartScanAsync()
	{
		await ExecuteAsync(async () =>
		{
			IsScanning = true;
			StatusMessage = "Scanning for servers...";
			DiscoveredServers.Clear();

			try
			{
				await _discoveryService.StartScanningAsync();

				// Servers are added via OnServerDiscovered event handler, no need to add them again here

				StatusMessage = DiscoveredServers.Count > 0
					? $"Found {DiscoveredServers.Count} server(s)"
					: "No servers found. Make sure the server is running and on the same network.";
			}
			finally
			{
				IsScanning = false;
			}
		}, "Failed to scan for servers");
	}

	[RelayCommand]
	private async Task StopScanAsync()
	{
		await _discoveryService.StopScanningAsync();
		IsScanning = false;
		StatusMessage = "Scan stopped";
	}

	[RelayCommand(CanExecute = nameof(CanConnectToSelectedServer))]
	private async Task ConnectToSelectedServerAsync()
	{
		if (SelectedServer == null)
			return;

		await ConnectToServerAsync(SelectedServer.Url, SelectedServer.WebSocketUrl);
	}

	[RelayCommand(CanExecute = nameof(CanConnectManually))]
	private async Task ConnectManuallyAsync()
	{
		if (string.IsNullOrWhiteSpace(ManualServerUrl))
			return;

		var url = ManualServerUrl.Trim();
		if (!url.StartsWith("http://") && !url.StartsWith("https://"))
			url = "https://" + url;

		var wsUrl = url.Replace("https://", "wss://").Replace("http://", "ws://") + "/ws";
		await ConnectToServerAsync(url, wsUrl);
	}

	[RelayCommand]
	private void ToggleManualEntry()
	{
		ShowManualEntry = !ShowManualEntry;
	}

	private bool CanConnectToSelectedServer() => SelectedServer != null && !IsBusy;

	private bool CanConnectManually() => !string.IsNullOrWhiteSpace(ManualServerUrl) && !IsBusy;

	private async Task ConnectToServerAsync(string serverUrl, string webSocketUrl)
	{
		await ExecuteAsync(async () =>
		{
			StatusMessage = $"Connecting to {webSocketUrl}...";

			// Connect WebSocket
			await _webSocketService.ConnectAsync(webSocketUrl);

			StatusMessage = "Connected to WebSocket. Registering with server...";

			// Get device info
			var deviceInfo = await _authService.GetDeviceInfoAsync();

			// Register with server via WebSocket
			var mobileAppId = await _webSocketService.RegisterAndWaitForAuthorizationAsync(
				deviceInfo.Name,
				deviceInfo.Identifier,
				deviceInfo.Platform,
				deviceInfo.AppVersion);

			StatusMessage = "Registration successful!";

			// Save settings
			var settings = new AppSettings
			{
				ServerUrl = serverUrl,
				MobileAppId = mobileAppId,
				AutoConnect = true,
				LastConnected = DateTime.Now
			};
			await _secureStorage.SaveSettingsAsync(settings);

			StatusMessage = "Connected successfully!";

			// Navigate to main page
			await Shell.Current.GoToAsync("//devices");

		}, "Failed to connect to server");
	}

	private void OnServerDiscovered(object? sender, DiscoveredServer server)
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			if (!DiscoveredServers.Any(s => s.IPAddress == server.IPAddress && s.Port == server.Port))
			{
				DiscoveredServers.Add(server);
				StatusMessage = $"Found {DiscoveredServers.Count} server(s)";
			}
		});
	}

	private void OnServerLost(object? sender, DiscoveredServer server)
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			var existingServer = DiscoveredServers.FirstOrDefault(
				s => s.IPAddress == server.IPAddress && s.Port == server.Port);

			if (existingServer != null)
			{
				DiscoveredServers.Remove(existingServer);
				StatusMessage = $"Found {DiscoveredServers.Count} server(s)";
			}
		});
	}

	partial void OnSelectedServerChanged(DiscoveredServer? value)
	{
		ConnectToSelectedServerCommand.NotifyCanExecuteChanged();
	}

	partial void OnManualServerUrlChanged(string value)
	{
		ConnectManuallyCommand.NotifyCanExecuteChanged();
	}
}
