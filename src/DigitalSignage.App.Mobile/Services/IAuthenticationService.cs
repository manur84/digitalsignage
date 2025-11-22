namespace DigitalSignage.App.Mobile.Services;

/// <summary>
/// Service for authenticating the mobile app with the Digital Signage server.
/// </summary>
public interface IAuthenticationService
{
	/// <summary>
	/// Registers the mobile app with the server.
	/// </summary>
	/// <param name="serverUrl">The server URL.</param>
	/// <param name="registrationToken">The registration token (optional).</param>
	/// <returns>The assigned mobile app ID.</returns>
	Task<Guid> RegisterAppAsync(string serverUrl, string? registrationToken = null);

	/// <summary>
	/// Gets the current device information.
	/// </summary>
	Task<DeviceInfo> GetDeviceInfoAsync();

	/// <summary>
	/// Checks if biometric authentication is available on this device.
	/// </summary>
	Task<bool> IsBiometricAuthAvailableAsync();

	/// <summary>
	/// Authenticates using biometric authentication (Face ID/Touch ID).
	/// </summary>
	/// <returns>True if authentication succeeded.</returns>
	Task<bool> AuthenticateWithBiometricsAsync();
}

/// <summary>
/// Device information for registration.
/// </summary>
public class DeviceInfo
{
	/// <summary>
	/// Gets or sets the device name.
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the device identifier (unique ID).
	/// </summary>
	public string Identifier { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the device platform (iOS, Android, etc.).
	/// </summary>
	public string Platform { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the OS version.
	/// </summary>
	public string OSVersion { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the app version.
	/// </summary>
	public string AppVersion { get; set; } = string.Empty;
}
