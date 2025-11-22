namespace DigitalSignage.App.Mobile.Models;

/// <summary>
/// Application settings stored in secure storage.
/// </summary>
public class AppSettings
{
	/// <summary>
	/// Gets or sets the server URL (e.g., "https://192.168.1.100:8080").
	/// </summary>
	public string? ServerUrl { get; set; }

	/// <summary>
	/// Gets or sets the registration token.
	/// </summary>
	public string? RegistrationToken { get; set; }

	/// <summary>
	/// Gets or sets the mobile app ID assigned by the server.
	/// </summary>
	public Guid? MobileAppId { get; set; }

	/// <summary>
	/// Gets or sets whether biometric authentication is enabled.
	/// </summary>
	public bool BiometricAuthEnabled { get; set; }

	/// <summary>
	/// Gets or sets whether to auto-connect to the last used server.
	/// </summary>
	public bool AutoConnect { get; set; }

	/// <summary>
	/// Gets or sets whether to accept self-signed certificates.
	/// </summary>
	public bool AcceptSelfSignedCertificates { get; set; } = true;

	/// <summary>
	/// Gets or sets the last successful connection timestamp.
	/// </summary>
	public DateTime? LastConnected { get; set; }

	/// <summary>
	/// Gets or sets the device name registered with the server.
	/// </summary>
	public string? DeviceName { get; set; }
}
