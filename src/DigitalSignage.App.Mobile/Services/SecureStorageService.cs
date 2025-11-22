using System.Text.Json;
using DigitalSignage.App.Mobile.Models;

namespace DigitalSignage.App.Mobile.Services;

/// <summary>
/// Implementation of secure storage service using MAUI SecureStorage.
/// </summary>
public class SecureStorageService : ISecureStorageService
{
	private const string SettingsKey = "AppSettings";
	private readonly JsonSerializerOptions _jsonOptions;

	public SecureStorageService()
	{
		_jsonOptions = new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true,
			WriteIndented = false
		};
	}

	/// <inheritdoc/>
	public async Task SaveSettingsAsync(AppSettings settings)
	{
		if (settings == null)
			throw new ArgumentNullException(nameof(settings));

		try
		{
			var json = JsonSerializer.Serialize(settings, _jsonOptions);
			await SecureStorage.Default.SetAsync(SettingsKey, json);
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException("Failed to save settings to secure storage", ex);
		}
	}

	/// <inheritdoc/>
	public async Task<AppSettings?> GetSettingsAsync()
	{
		try
		{
			var json = await SecureStorage.Default.GetAsync(SettingsKey);
			if (string.IsNullOrWhiteSpace(json))
				return null;

			return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
		}
		catch (Exception ex)
		{
			// Return null if settings don't exist or are corrupted
			Console.WriteLine($"Failed to retrieve settings: {ex.Message}");
			return null;
		}
	}

	/// <inheritdoc/>
	public async Task ClearSettingsAsync()
	{
		try
		{
			SecureStorage.Default.Remove(SettingsKey);
			await Task.CompletedTask;
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException("Failed to clear settings from secure storage", ex);
		}
	}

	/// <inheritdoc/>
	public async Task SaveAsync(string key, string value)
	{
		if (string.IsNullOrWhiteSpace(key))
			throw new ArgumentException("Key cannot be null or empty", nameof(key));
		if (value == null)
			throw new ArgumentNullException(nameof(value));

		try
		{
			await SecureStorage.Default.SetAsync(key, value);
		}
		catch (Exception ex)
		{
			// Fallback to Preferences for simulators where SecureStorage may fail
			Console.WriteLine($"SecureStorage failed for key '{key}', falling back to Preferences: {ex.Message}");
			Preferences.Default.Set(key, value);
		}
	}

	/// <inheritdoc/>
	public async Task<string?> GetAsync(string key)
	{
		if (string.IsNullOrWhiteSpace(key))
			throw new ArgumentException("Key cannot be null or empty", nameof(key));

		try
		{
			return await SecureStorage.Default.GetAsync(key);
		}
		catch (Exception ex)
		{
			// Fallback to Preferences for simulators where SecureStorage may fail
			Console.WriteLine($"SecureStorage failed for key '{key}', falling back to Preferences: {ex.Message}");
			return Preferences.Default.Get(key, (string?)null);
		}
	}

	/// <inheritdoc/>
	public async Task RemoveAsync(string key)
	{
		if (string.IsNullOrWhiteSpace(key))
			throw new ArgumentException("Key cannot be null or empty", nameof(key));

		try
		{
			SecureStorage.Default.Remove(key);
			await Task.CompletedTask;
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException($"Failed to remove value for key '{key}'", ex);
		}
	}
}
