using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DigitalSignage.Core.DTOs.Api;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.App.Mobile.Services;

/// <summary>
/// Implementation of REST API service for Digital Signage server communication.
/// Provides HTTP-based communication as an alternative/complement to WebSocket.
/// </summary>
public class ApiService : IApiService
{
	private readonly HttpClient _httpClient;
	private readonly ILogger<ApiService> _logger;
	private string _baseUrl = "http://localhost:5000";
	private string? _authToken;

	/// <summary>
	/// Initializes a new instance of the ApiService.
	/// </summary>
	public ApiService(HttpClient httpClient, ILogger<ApiService> logger)
	{
		_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		// Configure default timeout
		_httpClient.Timeout = TimeSpan.FromSeconds(30);
	}

	/// <inheritdoc/>
	public void SetAuthenticationToken(string token)
	{
		if (string.IsNullOrWhiteSpace(token))
		{
			_authToken = null;
			_httpClient.DefaultRequestHeaders.Authorization = null;
			_logger.LogDebug("Authentication token cleared");
			return;
		}

		_authToken = token;
		_httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
		_logger.LogDebug("Authentication token set");
	}

	/// <inheritdoc/>
	public void SetServerUrl(string baseUrl)
	{
		if (string.IsNullOrWhiteSpace(baseUrl))
			throw new ArgumentException("Base URL cannot be null or empty", nameof(baseUrl));

		// Remove trailing slash
		_baseUrl = baseUrl.TrimEnd('/');
		_logger.LogInformation("Server URL set to: {BaseUrl}", _baseUrl);
	}

	/// <inheritdoc/>
	public async Task<RegisterMobileAppResponse> RegisterAsync(
		string deviceName,
		string platform,
		string appVersion,
		string? deviceModel = null,
		string? osVersion = null,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(deviceName))
			throw new ArgumentException("Device name cannot be null or empty", nameof(deviceName));
		if (string.IsNullOrWhiteSpace(platform))
			throw new ArgumentException("Platform cannot be null or empty", nameof(platform));
		if (string.IsNullOrWhiteSpace(appVersion))
			throw new ArgumentException("App version cannot be null or empty", nameof(appVersion));

		try
		{
			var request = new RegisterMobileAppRequest
			{
				DeviceName = deviceName,
				Platform = platform,
				AppVersion = appVersion,
				DeviceModel = deviceModel,
				OsVersion = osVersion
			};

			_logger.LogInformation("Registering mobile app: {DeviceName} ({Platform})", deviceName, platform);

			var response = await _httpClient.PostAsJsonAsync(
				$"{_baseUrl}/api/mobile/register",
				request,
				cancellationToken);

			if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
			{
				_logger.LogWarning("Registration requires authorization");
				// Server requires admin approval - this is expected
			}

			response.EnsureSuccessStatusCode();

			var result = await response.Content.ReadFromJsonAsync<RegisterMobileAppResponse>(cancellationToken: cancellationToken);
			if (result == null)
				throw new InvalidOperationException("Invalid registration response from server");

			_logger.LogInformation("Registration successful. RequestId: {RequestId}", result.RequestId);
			return result;
		}
		catch (HttpRequestException ex)
		{
			_logger.LogError(ex, "HTTP error during registration");
			throw new InvalidOperationException("Failed to connect to server. Please check the server URL and network connection.", ex);
		}
		catch (TaskCanceledException ex)
		{
			_logger.LogError(ex, "Registration request timed out");
			throw new InvalidOperationException("Request timed out. Please check your network connection.", ex);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error during registration");
			throw new InvalidOperationException("Failed to register with server", ex);
		}
	}

	/// <inheritdoc/>
	public async Task<CheckRegistrationStatusResponse> CheckRegistrationStatusAsync(
		Guid requestId,
		CancellationToken cancellationToken = default)
	{
		if (requestId == Guid.Empty)
			throw new ArgumentException("Request ID cannot be empty", nameof(requestId));

		try
		{
			_logger.LogDebug("Checking registration status for request: {RequestId}", requestId);

			var response = await _httpClient.GetAsync(
				$"{_baseUrl}/api/mobile/register/{requestId}/status",
				cancellationToken);

			response.EnsureSuccessStatusCode();

			var result = await response.Content.ReadFromJsonAsync<CheckRegistrationStatusResponse>(cancellationToken: cancellationToken);
			if (result == null)
				throw new InvalidOperationException("Invalid status response from server");

			_logger.LogDebug("Registration status: {Status}", result.Status);
			return result;
		}
		catch (HttpRequestException ex)
		{
			_logger.LogError(ex, "HTTP error checking registration status");
			throw new InvalidOperationException("Failed to check registration status", ex);
		}
		catch (TaskCanceledException ex)
		{
			_logger.LogError(ex, "Status check request timed out");
			throw new InvalidOperationException("Request timed out", ex);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error checking registration status");
			throw;
		}
	}

	/// <inheritdoc/>
	public async Task<List<DeviceDto>> GetDevicesAsync(
		string? status = null,
		CancellationToken cancellationToken = default)
	{
		try
		{
			EnsureAuthenticated();

			var url = $"{_baseUrl}/api/devices";
			if (!string.IsNullOrWhiteSpace(status))
				url += $"?status={Uri.EscapeDataString(status)}";

			_logger.LogDebug("Fetching devices (status filter: {Status})", status ?? "all");

			var response = await _httpClient.GetAsync(url, cancellationToken);
			response.EnsureSuccessStatusCode();

			var devices = await response.Content.ReadFromJsonAsync<List<DeviceDto>>(cancellationToken: cancellationToken);
			_logger.LogInformation("Retrieved {Count} devices", devices?.Count ?? 0);

			return devices ?? new List<DeviceDto>();
		}
		catch (HttpRequestException ex)
		{
			_logger.LogError(ex, "HTTP error fetching devices");
			throw new InvalidOperationException("Failed to fetch devices from server", ex);
		}
		catch (TaskCanceledException ex)
		{
			_logger.LogError(ex, "Device fetch request timed out");
			throw new InvalidOperationException("Request timed out", ex);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error fetching devices");
			throw;
		}
	}

	/// <inheritdoc/>
	public async Task<DeviceDetailDto> GetDeviceByIdAsync(
		Guid deviceId,
		CancellationToken cancellationToken = default)
	{
		if (deviceId == Guid.Empty)
			throw new ArgumentException("Device ID cannot be empty", nameof(deviceId));

		try
		{
			EnsureAuthenticated();

			_logger.LogDebug("Fetching device details: {DeviceId}", deviceId);

			var response = await _httpClient.GetAsync(
				$"{_baseUrl}/api/devices/{deviceId}",
				cancellationToken);

			if (response.StatusCode == HttpStatusCode.NotFound)
				throw new InvalidOperationException($"Device {deviceId} not found");

			response.EnsureSuccessStatusCode();

			var device = await response.Content.ReadFromJsonAsync<DeviceDetailDto>(cancellationToken: cancellationToken);
			if (device == null)
				throw new InvalidOperationException("Invalid device response from server");

			_logger.LogInformation("Retrieved device details: {DeviceName}", device.Name);
			return device;
		}
		catch (HttpRequestException ex)
		{
			_logger.LogError(ex, "HTTP error fetching device details");
			throw new InvalidOperationException("Failed to fetch device details", ex);
		}
		catch (TaskCanceledException ex)
		{
			_logger.LogError(ex, "Device fetch request timed out");
			throw new InvalidOperationException("Request timed out", ex);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error fetching device details");
			throw;
		}
	}

	/// <inheritdoc/>
	public async Task<DeviceCommandResponse> SendCommandAsync(
		Guid deviceId,
		string command,
		Dictionary<string, string>? parameters = null,
		CancellationToken cancellationToken = default)
	{
		if (deviceId == Guid.Empty)
			throw new ArgumentException("Device ID cannot be empty", nameof(deviceId));
		if (string.IsNullOrWhiteSpace(command))
			throw new ArgumentException("Command cannot be null or empty", nameof(command));

		try
		{
			EnsureAuthenticated();

			var request = new DeviceCommandRequest
			{
				Command = command,
				Parameters = parameters
			};

			_logger.LogInformation("Sending command to device {DeviceId}: {Command}", deviceId, command);

			var response = await _httpClient.PostAsJsonAsync(
				$"{_baseUrl}/api/devices/{deviceId}/commands",
				request,
				cancellationToken);

			if (response.StatusCode == HttpStatusCode.NotFound)
				throw new InvalidOperationException($"Device {deviceId} not found");

			response.EnsureSuccessStatusCode();

			var result = await response.Content.ReadFromJsonAsync<DeviceCommandResponse>(cancellationToken: cancellationToken);
			if (result == null)
				throw new InvalidOperationException("Invalid command response from server");

			_logger.LogInformation("Command sent successfully: {Success}", result.Success);
			return result;
		}
		catch (HttpRequestException ex)
		{
			_logger.LogError(ex, "HTTP error sending command");
			throw new InvalidOperationException("Failed to send command to device", ex);
		}
		catch (TaskCanceledException ex)
		{
			_logger.LogError(ex, "Command request timed out");
			throw new InvalidOperationException("Request timed out", ex);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error sending command");
			throw;
		}
	}

	/// <inheritdoc/>
	public async Task<ScreenshotResponse> RequestScreenshotAsync(
		Guid deviceId,
		int quality = 85,
		int timeoutSeconds = 30,
		CancellationToken cancellationToken = default)
	{
		if (deviceId == Guid.Empty)
			throw new ArgumentException("Device ID cannot be empty", nameof(deviceId));
		if (quality < 1 || quality > 100)
			throw new ArgumentOutOfRangeException(nameof(quality), "Quality must be between 1 and 100");

		try
		{
			EnsureAuthenticated();

			var request = new ScreenshotRequest
			{
				Quality = quality,
				TimeoutSeconds = timeoutSeconds
			};

			_logger.LogInformation("Requesting screenshot from device {DeviceId} (quality: {Quality})", deviceId, quality);

			// Use longer timeout for screenshot requests
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds + 5));

			var response = await _httpClient.PostAsJsonAsync(
				$"{_baseUrl}/api/devices/{deviceId}/screenshot",
				request,
				cts.Token);

			if (response.StatusCode == HttpStatusCode.NotFound)
				throw new InvalidOperationException($"Device {deviceId} not found");

			response.EnsureSuccessStatusCode();

			var result = await response.Content.ReadFromJsonAsync<ScreenshotResponse>(cancellationToken: cts.Token);
			if (result == null)
				throw new InvalidOperationException("Invalid screenshot response from server");

			_logger.LogInformation("Screenshot received: {Width}x{Height}, {Size} bytes", result.Width, result.Height, result.FileSizeBytes);
			return result;
		}
		catch (HttpRequestException ex)
		{
			_logger.LogError(ex, "HTTP error requesting screenshot");
			throw new InvalidOperationException("Failed to request screenshot", ex);
		}
		catch (TaskCanceledException ex)
		{
			_logger.LogError(ex, "Screenshot request timed out");
			throw new InvalidOperationException("Screenshot request timed out", ex);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error requesting screenshot");
			throw;
		}
	}

	/// <inheritdoc/>
	public async Task<List<LayoutDto>> GetLayoutsAsync(
		CancellationToken cancellationToken = default)
	{
		try
		{
			EnsureAuthenticated();

			_logger.LogDebug("Fetching layouts");

			var response = await _httpClient.GetAsync(
				$"{_baseUrl}/api/layouts",
				cancellationToken);

			response.EnsureSuccessStatusCode();

			var layouts = await response.Content.ReadFromJsonAsync<List<LayoutDto>>(cancellationToken: cancellationToken);
			_logger.LogInformation("Retrieved {Count} layouts", layouts?.Count ?? 0);

			return layouts ?? new List<LayoutDto>();
		}
		catch (HttpRequestException ex)
		{
			_logger.LogError(ex, "HTTP error fetching layouts");
			throw new InvalidOperationException("Failed to fetch layouts from server", ex);
		}
		catch (TaskCanceledException ex)
		{
			_logger.LogError(ex, "Layout fetch request timed out");
			throw new InvalidOperationException("Request timed out", ex);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error fetching layouts");
			throw;
		}
	}

	/// <inheritdoc/>
	public async Task<LayoutDetailDto> GetLayoutByIdAsync(
		int layoutId,
		bool includeElements = true,
		CancellationToken cancellationToken = default)
	{
		if (layoutId <= 0)
			throw new ArgumentException("Layout ID must be greater than 0", nameof(layoutId));

		try
		{
			EnsureAuthenticated();

			var url = $"{_baseUrl}/api/layouts/{layoutId}";
			if (includeElements)
				url += "?includeElements=true";

			_logger.LogDebug("Fetching layout details: {LayoutId}", layoutId);

			var response = await _httpClient.GetAsync(url, cancellationToken);

			if (response.StatusCode == HttpStatusCode.NotFound)
				throw new InvalidOperationException($"Layout {layoutId} not found");

			response.EnsureSuccessStatusCode();

			var layout = await response.Content.ReadFromJsonAsync<LayoutDetailDto>(cancellationToken: cancellationToken);
			if (layout == null)
				throw new InvalidOperationException("Invalid layout response from server");

			_logger.LogInformation("Retrieved layout details: {LayoutName}", layout.Name);
			return layout;
		}
		catch (HttpRequestException ex)
		{
			_logger.LogError(ex, "HTTP error fetching layout details");
			throw new InvalidOperationException("Failed to fetch layout details", ex);
		}
		catch (TaskCanceledException ex)
		{
			_logger.LogError(ex, "Layout fetch request timed out");
			throw new InvalidOperationException("Request timed out", ex);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error fetching layout details");
			throw;
		}
	}

	/// <inheritdoc/>
	public async Task<bool> AssignLayoutToDeviceAsync(
		Guid deviceId,
		int layoutId,
		DateTime? startTime = null,
		DateTime? endTime = null,
		CancellationToken cancellationToken = default)
	{
		if (deviceId == Guid.Empty)
			throw new ArgumentException("Device ID cannot be empty", nameof(deviceId));
		if (layoutId <= 0)
			throw new ArgumentException("Layout ID must be greater than 0", nameof(layoutId));

		try
		{
			EnsureAuthenticated();

			var request = new AssignLayoutRequest
			{
				LayoutId = layoutId,
				StartTime = startTime,
				EndTime = endTime
			};

			_logger.LogInformation("Assigning layout {LayoutId} to device {DeviceId}", layoutId, deviceId);

			var response = await _httpClient.PostAsJsonAsync(
				$"{_baseUrl}/api/devices/{deviceId}/layout",
				request,
				cancellationToken);

			if (response.StatusCode == HttpStatusCode.NotFound)
				throw new InvalidOperationException($"Device {deviceId} or Layout {layoutId} not found");

			response.EnsureSuccessStatusCode();

			_logger.LogInformation("Layout assigned successfully");
			return true;
		}
		catch (HttpRequestException ex)
		{
			_logger.LogError(ex, "HTTP error assigning layout");
			throw new InvalidOperationException("Failed to assign layout to device", ex);
		}
		catch (TaskCanceledException ex)
		{
			_logger.LogError(ex, "Layout assignment request timed out");
			throw new InvalidOperationException("Request timed out", ex);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error assigning layout");
			throw;
		}
	}

	/// <inheritdoc/>
	public async Task<ServerInfoResponse> GetServerInfoAsync(
		CancellationToken cancellationToken = default)
	{
		try
		{
			_logger.LogDebug("Fetching server info");

			// Server info endpoint typically doesn't require authentication
			var response = await _httpClient.GetAsync(
				$"{_baseUrl}/api/server/info",
				cancellationToken);

			response.EnsureSuccessStatusCode();

			var serverInfo = await response.Content.ReadFromJsonAsync<ServerInfoResponse>(cancellationToken: cancellationToken);
			if (serverInfo == null)
				throw new InvalidOperationException("Invalid server info response");

			_logger.LogInformation("Server info: {ServerName} v{Version}, {Status}", serverInfo.ServerName, serverInfo.Version, serverInfo.Status);
			return serverInfo;
		}
		catch (HttpRequestException ex)
		{
			_logger.LogError(ex, "HTTP error fetching server info");
			throw new InvalidOperationException("Failed to fetch server info", ex);
		}
		catch (TaskCanceledException ex)
		{
			_logger.LogError(ex, "Server info request timed out");
			throw new InvalidOperationException("Request timed out", ex);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error fetching server info");
			throw;
		}
	}

	/// <summary>
	/// Ensures that the service is authenticated before making API calls.
	/// </summary>
	private void EnsureAuthenticated()
	{
		if (string.IsNullOrWhiteSpace(_authToken))
			throw new InvalidOperationException("Not authenticated. Please set authentication token first.");
	}
}
