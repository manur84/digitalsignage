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

	[RelayCommand]
	private async Task ConnectToSelectedServerAsync(DiscoveredServer? server)
	{
		if (server == null)
			return;

		await ConnectToServerAsync(server.Url, server.WebSocketUrl);
	}

	[RelayCommand(CanExecute = nameof(CanConnectManually))]
	private async Task ConnectManuallyAsync()
	{
		if (string.IsNullOrWhiteSpace(ManualServerUrl))
			return;

		var url = ManualServerUrl.Trim();

		// Remove any protocol prefix
		url = url.Replace("http://", "").Replace("https://", "").Replace("ws://", "").Replace("wss://", "");

		// Force WSS-only (server only accepts WSS)
		var wsUrl = "wss://" + url + "/ws";
		await ConnectToServerAsync("https://" + url, wsUrl);
	}

	[RelayCommand]
	private void ToggleManualEntry()
	{
		ShowManualEntry = !ShowManualEntry;
	}

	private bool CanConnectManually() => !string.IsNullOrWhiteSpace(ManualServerUrl) && !IsBusy;

	private async Task ConnectToServerAsync(string serverUrl, string webSocketUrl)
	{
		await ExecuteAsync(async () =>
		{
			// Get existing credentials (if any)
			var existingToken = await _secureStorage.GetAsync("AuthToken");
			var existingAppIdStr = await _secureStorage.GetAsync("MobileAppId");
			var existingAppId = Guid.TryParse(existingAppIdStr, out var parsedAppId) ? parsedAppId : (Guid?)null;

			// Get device info for registration
			var deviceInfo = await _authService.GetDeviceInfoAsync();

			// Connect to server via WebSocket
			StatusMessage = "Connecting to server via WebSocket...";
			await _webSocketService.ConnectAsync(webSocketUrl);

			// If we have existing credentials, try to authenticate
			if (!string.IsNullOrEmpty(existingToken) && existingAppId.HasValue)
			{
				StatusMessage = "Authenticating with existing credentials...";

				var heartbeatMessage = new
				{
					type = "APP_HEARTBEAT",
					appId = existingAppId.Value,
					token = existingToken
				};
				await _webSocketService.SendJsonAsync(heartbeatMessage);

				StatusMessage = "Connected successfully!";

				// Save settings
				var settings = new AppSettings
				{
					ServerUrl = serverUrl,
					MobileAppId = existingAppId.Value,
					AutoConnect = true,
					LastConnected = DateTime.Now
				};
				await _secureStorage.SaveSettingsAsync(settings);

				// Navigate to main page
				await Shell.Current.GoToAsync("//devices");
				return;
			}

			// No existing credentials - register new app via WebSocket
			StatusMessage = "Registering new mobile app...";

			var registrationMessage = new
			{
				type = "REGISTER_MOBILE_APP",
				deviceName = deviceInfo.Name,
				platform = deviceInfo.Platform,
				appVersion = deviceInfo.AppVersion,
				deviceModel = deviceInfo.Identifier,
				osVersion = deviceInfo.OSVersion,
				token = !string.IsNullOrWhiteSpace(RegistrationToken) ? RegistrationToken : (string?)null
			};

			await _webSocketService.SendJsonAsync(registrationMessage);

			StatusMessage = "Waiting for server approval...";

			// TODO: Wait for registration response via WebSocket
			// For now, show message that admin needs to approve
			StatusMessage = "Please wait for admin to approve this device on the server.";

			// Note: The actual approval handling will be done via WebSocket message handler
			// When approved, the server will send a response with appId and token

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

	partial void OnManualServerUrlChanged(string value)
	{
		ConnectManuallyCommand.NotifyCanExecuteChanged();
	}
}
