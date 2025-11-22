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

	private bool CanConnectManually() => !string.IsNullOrWhiteSpace(ManualServerUrl) && !IsBusy;

	private async Task ConnectToServerAsync(string serverUrl, string webSocketUrl)
	{
		await ExecuteAsync(async () =>
		{
			Guid mobileAppId;
			string? authToken;

			// Get existing credentials (if any)
			var existingToken = await _secureStorage.GetAsync("AuthToken");
			var existingAppIdStr = await _secureStorage.GetAsync("MobileAppId");
			var existingAppId = Guid.TryParse(existingAppIdStr, out var parsedAppId) ? parsedAppId : (Guid?)null;

			// Step 1: ALWAYS try WebSocket connection FIRST
			try
			{
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

					mobileAppId = existingAppId.Value;
					authToken = existingToken;

					// Save settings
					var settings = new AppSettings
					{
						ServerUrl = serverUrl,
						MobileAppId = mobileAppId,
						AutoConnect = true,
						LastConnected = DateTime.Now
					};
					await _secureStorage.SaveSettingsAsync(settings);

					// Navigate to main page
					await Shell.Current.GoToAsync("//devices");
					return;
				}
				else
				{
					// No credentials - close WebSocket and fall through to HTTPS registration
					Console.WriteLine("No existing credentials found. Switching to HTTPS registration...");
					StatusMessage = "No credentials, trying HTTPS registration...";
					// WebSocket will be closed and reconnected after registration
				}
			}
			catch (Exception ex)
			{
				// WebSocket connection failed, fall back to HTTPS registration
				Console.WriteLine($"WebSocket connection failed: {ex.Message}. Falling back to HTTPS registration...");
				StatusMessage = "WebSocket failed, trying HTTPS registration...";
			}

			// Step 2: Fallback - Register via HTTPS REST API and poll for approval
			StatusMessage = "Registering with server via HTTPS...";

			mobileAppId = await _authService.RegisterAppAsync(
				serverUrl,
				RegistrationToken,
				progressCallback: (status) =>
				{
					// Update UI from main thread
					MainThread.BeginInvokeOnMainThread(() =>
					{
						StatusMessage = status;
					});
				});

			StatusMessage = "Registration approved! Connecting to server...";

			// Step 3: Get auth token from secure storage (saved by AuthenticationService)
			authToken = await _secureStorage.GetAsync("AuthToken");
			if (string.IsNullOrEmpty(authToken))
			{
				throw new InvalidOperationException("Authentication token not found after registration");
			}

			// Step 4: Connect WebSocket
			await _webSocketService.ConnectAsync(webSocketUrl);

			// Step 5: Authenticate the WebSocket connection with heartbeat message
			StatusMessage = "Authenticating WebSocket connection...";

			var heartbeat = new
			{
				type = "APP_HEARTBEAT",
				appId = mobileAppId,
				token = authToken
			};
			await _webSocketService.SendJsonAsync(heartbeat);

			StatusMessage = "Connected successfully!";

			// Save settings
			var appSettings = new AppSettings
			{
				ServerUrl = serverUrl,
				MobileAppId = mobileAppId,
				AutoConnect = true,
				LastConnected = DateTime.Now
			};
			await _secureStorage.SaveSettingsAsync(appSettings);

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

	partial void OnManualServerUrlChanged(string value)
	{
		ConnectManuallyCommand.NotifyCanExecuteChanged();
	}
}
