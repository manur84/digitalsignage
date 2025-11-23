using System.Net.Http.Json;
using System.Text.Json;

namespace DigitalSignage.App.Mobile.Services;

/// <summary>
/// Implementation of authentication service.
/// Uses REST API for registration with polling for approval status.
/// </summary>
public class AuthenticationService : IAuthenticationService
{
	private readonly IApiService _apiService;
	private readonly ISecureStorageService _secureStorage;

	public AuthenticationService(IApiService apiService, ISecureStorageService secureStorage)
	{
		_apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
		_secureStorage = secureStorage ?? throw new ArgumentNullException(nameof(secureStorage));
	}

	/// <inheritdoc/>
	public async Task<Guid> RegisterAppAsync(string serverUrl, string? registrationToken = null, Action<string>? progressCallback = null)
	{
		if (string.IsNullOrWhiteSpace(serverUrl))
			throw new ArgumentException("Server URL cannot be null or empty", nameof(serverUrl));

		try
		{
			// Set server URL for API service
			_apiService.SetServerUrl(serverUrl);

			// Get device information
			var deviceInfo = await GetDeviceInfoAsync();

			Console.WriteLine($"Registering mobile app: {deviceInfo.Name} ({deviceInfo.Platform})");
			progressCallback?.Invoke("Sending registration request...");

			// Send registration request via REST API
			var registrationResponse = await _apiService.RegisterAsync(
				deviceInfo.Name,
				deviceInfo.Platform,
				deviceInfo.AppVersion,
				deviceInfo.Identifier,
				deviceInfo.OSVersion);

			if (!registrationResponse.Success)
			{
				throw new InvalidOperationException(
					registrationResponse.Message ?? "Registration failed");
			}

			var requestId = registrationResponse.RequestId;
			Console.WriteLine($"Registration request sent. RequestId: {requestId}");
			Console.WriteLine($"Message: {registrationResponse.Message}");
			progressCallback?.Invoke("Waiting for admin approval...");

			// Poll for approval status (every 5 seconds for up to 5 minutes)
			const int maxAttempts = 60; // 5 minutes with 5-second intervals
			const int pollingIntervalMs = 5000;

			for (int attempt = 1; attempt <= maxAttempts; attempt++)
			{
				Console.WriteLine($"Checking registration status (attempt {attempt}/{maxAttempts})...");

				// Update progress every 10 attempts (every 50 seconds)
				if (attempt % 10 == 0)
				{
					var elapsed = TimeSpan.FromSeconds(attempt * 5);
					progressCallback?.Invoke($"Still waiting for approval... ({elapsed.Minutes}m {elapsed.Seconds}s)");
				}

				var statusResponse = await _apiService.CheckRegistrationStatusAsync(requestId);

				if (statusResponse.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase))
				{
					// Registration approved - save token and mobile app ID
					if (string.IsNullOrWhiteSpace(statusResponse.Token))
						throw new InvalidOperationException("Approved but no token received");

					if (!statusResponse.MobileAppId.HasValue || statusResponse.MobileAppId.Value == Guid.Empty)
						throw new InvalidOperationException("Approved but no mobile app ID received");

					Console.WriteLine($"Registration approved! MobileAppId: {statusResponse.MobileAppId.Value}");
					progressCallback?.Invoke("Registration approved!");

					// Set authentication token for future API calls
					_apiService.SetAuthenticationToken(statusResponse.Token);

					// Save token to secure storage
					await _secureStorage.SaveAsync("AuthToken", statusResponse.Token);
					await _secureStorage.SaveAsync("MobileAppId", statusResponse.MobileAppId.Value.ToString());

					return statusResponse.MobileAppId.Value;
				}
				else if (statusResponse.Status.Equals("Denied", StringComparison.OrdinalIgnoreCase))
				{
					throw new InvalidOperationException(
						$"Registration was denied: {statusResponse.Message}");
				}
				else if (statusResponse.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
				{
					// Still pending - wait and try again
					Console.WriteLine($"Status: Pending - {statusResponse.Message}");

					if (attempt < maxAttempts)
						await Task.Delay(pollingIntervalMs);
				}
				else
				{
					Console.WriteLine($"Unknown status: {statusResponse.Status}");
				}
			}

			throw new InvalidOperationException(
				"Registration request timed out. The request is still pending approval. Please try again later.");
		}
		catch (InvalidOperationException)
		{
			throw; // Re-throw our own exceptions
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
#if IOS
		// Use iOS-specific LocalAuthentication
		return await Task.Run(() =>
		{
			var context = new LocalAuthentication.LAContext();
			return context.CanEvaluatePolicy(
				LocalAuthentication.LAPolicy.DeviceOwnerAuthenticationWithBiometrics,
				out _);
		});
#else
		// For other platforms, return false for now
		await Task.CompletedTask;
		return false;
#endif
	}

	/// <inheritdoc/>
	public async Task<bool> AuthenticateWithBiometricsAsync()
	{
#if IOS
		try
		{
			var context = new LocalAuthentication.LAContext();
			var reason = "Authenticate to access Digital Signage";

			// Check if biometric is available
			if (!context.CanEvaluatePolicy(
				LocalAuthentication.LAPolicy.DeviceOwnerAuthenticationWithBiometrics,
				out var authError))
			{
				Console.WriteLine($"Biometric authentication not available: {authError?.LocalizedDescription}");
				return false;
			}

			// Perform biometric authentication
			var (success, error) = await context.EvaluatePolicyAsync(
				LocalAuthentication.LAPolicy.DeviceOwnerAuthenticationWithBiometrics,
				reason);

			if (error != null)
			{
				Console.WriteLine($"Biometric authentication error: {error.LocalizedDescription}");
			}

			return success;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Exception during biometric authentication: {ex.Message}");
			return false;
		}
#else
		// For other platforms, return false for now
		await Task.CompletedTask;
		return false;
#endif
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
}
