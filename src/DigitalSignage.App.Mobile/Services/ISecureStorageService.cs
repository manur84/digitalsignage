using DigitalSignage.App.Mobile.Models;

namespace DigitalSignage.App.Mobile.Services;

/// <summary>
/// Service for securely storing and retrieving sensitive data.
/// </summary>
public interface ISecureStorageService
{
	/// <summary>
	/// Saves the application settings to secure storage.
	/// </summary>
	Task SaveSettingsAsync(AppSettings settings);

	/// <summary>
	/// Retrieves the application settings from secure storage.
	/// </summary>
	Task<AppSettings?> GetSettingsAsync();

	/// <summary>
	/// Clears all stored settings.
	/// </summary>
	Task ClearSettingsAsync();

	/// <summary>
	/// Saves a value to secure storage.
	/// </summary>
	Task SaveAsync(string key, string value);

	/// <summary>
	/// Retrieves a value from secure storage.
	/// </summary>
	Task<string?> GetAsync(string key);

	/// <summary>
	/// Removes a value from secure storage.
	/// </summary>
	Task RemoveAsync(string key);
}
