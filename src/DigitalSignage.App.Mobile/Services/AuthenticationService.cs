using System.Net.Http.Json;
using System.Text.Json;

namespace DigitalSignage.App.Mobile.Services;

/// <summary>
/// Implementation of authentication service.
/// </summary>
public class AuthenticationService : IAuthenticationService
{
	private readonly HttpClient _httpClient;
	private readonly ISecureStorageService _secureStorage;

	public AuthenticationService(ISecureStorageService secureStorage)
	{
		_secureStorage = secureStorage ?? throw new ArgumentNullException(nameof(secureStorage));
		_httpClient = new HttpClient();

		// Allow self-signed certificates for development
		// TODO: Make this configurable in production
		var handler = new HttpClientHandler
		{
			ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
		};
		_httpClient = new HttpClient(handler);
	}

	/// <inheritdoc/>
	public async Task<Guid> RegisterAppAsync(string serverUrl, string? registrationToken = null)
	{
		if (string.IsNullOrWhiteSpace(serverUrl))
			throw new ArgumentException("Server URL cannot be null or empty", nameof(serverUrl));

		try
		{
			var deviceInfo = await GetDeviceInfoAsync();

			var requestData = new
			{
				DeviceName = deviceInfo.Name,
				DeviceIdentifier = deviceInfo.Identifier,
				Platform = deviceInfo.Platform,
				OSVersion = deviceInfo.OSVersion,
				AppVersion = deviceInfo.AppVersion,
				RegistrationToken = registrationToken
			};

			var response = await _httpClient.PostAsJsonAsync(
				$"{serverUrl}/api/mobile/register",
				requestData);

			response.EnsureSuccessStatusCode();

			var result = await response.Content.ReadFromJsonAsync<RegistrationResponse>();
			if (result == null || result.MobileAppId == Guid.Empty)
				throw new InvalidOperationException("Invalid registration response from server");

			Console.WriteLine($"Successfully registered with server. MobileAppId: {result.MobileAppId}");
			return result.MobileAppId;
		}
		catch (HttpRequestException ex)
		{
			Console.WriteLine($"HTTP error during registration: {ex.Message}");
			throw new InvalidOperationException("Failed to connect to server. Please check the server URL and network connection.", ex);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error during registration: {ex.Message}");
			throw new InvalidOperationException("Failed to register with server", ex);
		}
	}

	/// <inheritdoc/>
	public async Task<DeviceInfo> GetDeviceInfoAsync()
	{
		try
		{
			var deviceInfo = new DeviceInfo
			{
				Name = Microsoft.Maui.Devices.DeviceInfo.Name,
				Identifier = await GetDeviceIdentifierAsync(),
				Platform = Microsoft.Maui.Devices.DeviceInfo.Platform.ToString(),
				OSVersion = $"{Microsoft.Maui.Devices.DeviceInfo.VersionString}",
				AppVersion = AppInfo.Current.VersionString
			};

			return deviceInfo;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error getting device info: {ex.Message}");
			throw;
		}
	}

	/// <inheritdoc/>
	public async Task<bool> IsBiometricAuthAvailableAsync()
	{
		// Platform-specific implementation would go here
		// For now, return false (not implemented)
		await Task.CompletedTask;
		return false;
	}

	/// <inheritdoc/>
	public async Task<bool> AuthenticateWithBiometricsAsync()
	{
		// Platform-specific implementation would go here
		// For now, return false (not implemented)
		await Task.CompletedTask;
		return false;
	}

	private async Task<string> GetDeviceIdentifierAsync()
	{
		// Try to get a persistent device identifier from secure storage
		const string DeviceIdKey = "DeviceIdentifier";

		var deviceId = await _secureStorage.GetAsync(DeviceIdKey);
		if (string.IsNullOrEmpty(deviceId))
		{
			// Generate a new GUID as device identifier
			deviceId = Guid.NewGuid().ToString();
			await _secureStorage.SaveAsync(DeviceIdKey, deviceId);
			Console.WriteLine($"Generated new device identifier: {deviceId}");
		}

		return deviceId;
	}

	private class RegistrationResponse
	{
		public Guid MobileAppId { get; set; }
		public string? Message { get; set; }
	}
}
