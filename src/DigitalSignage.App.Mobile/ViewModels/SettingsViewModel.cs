using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.App.Mobile.Services;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.App.Mobile.ViewModels;

/// <summary>
/// ViewModel for Settings page
/// </summary>
public partial class SettingsViewModel : BaseViewModel
{
    private readonly ISecureStorageService _secureStorageService;
    private readonly IAuthenticationService _authenticationService;
    private readonly ILogger<SettingsViewModel> _logger;

    [ObservableProperty]
    private bool _isDarkModeEnabled;

    [ObservableProperty]
    private bool _isBiometricAuthEnabled;

    [ObservableProperty]
    private bool _isPushNotificationsEnabled;

    [ObservableProperty]
    private bool _isAutoConnectEnabled = true;

    [ObservableProperty]
    private string _appVersion;

    [ObservableProperty]
    private string _serverUrl;

    [ObservableProperty]
    private bool _isBiometricAvailable;

    public SettingsViewModel(
        ISecureStorageService secureStorageService,
        IAuthenticationService authenticationService,
        ILogger<SettingsViewModel> logger)
    {
        _secureStorageService = secureStorageService;
        _authenticationService = authenticationService;
        _logger = logger;

        Title = "Settings";
        _appVersion = GetAppVersion();

        // Check if biometric authentication is available
        CheckBiometricAvailability();
    }

    /// <summary>
    /// Load settings when page appears
    /// </summary>
    public async Task LoadSettingsAsync()
    {
        try
        {
            IsBusy = true;

            // Load dark mode preference
            var darkModeStr = await _secureStorageService.GetAsync("DarkMode");
            IsDarkModeEnabled = darkModeStr == "true";

            // Load biometric auth preference
            var biometricStr = await _secureStorageService.GetAsync("BiometricAuth");
            IsBiometricAuthEnabled = biometricStr == "true" && IsBiometricAvailable;

            // Load push notifications preference
            var pushStr = await _secureStorageService.GetAsync("PushNotifications");
            IsPushNotificationsEnabled = pushStr != "false"; // Default enabled

            // Load auto-connect preference
            var autoConnectStr = await _secureStorageService.GetAsync("AutoConnect");
            IsAutoConnectEnabled = autoConnectStr != "false"; // Default enabled

            // Load server URL
            ServerUrl = await _secureStorageService.GetAsync("ServerUrl") ?? "Not connected";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading settings");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Toggle dark mode
    /// </summary>
    [RelayCommand]
    private async Task ToggleDarkModeAsync()
    {
        try
        {
            await _secureStorageService.SetAsync("DarkMode", IsDarkModeEnabled.ToString().ToLower());

            // Apply theme change
            Application.Current!.UserAppTheme = IsDarkModeEnabled ? AppTheme.Dark : AppTheme.Light;

            _logger.LogInformation("Dark mode {Status}", IsDarkModeEnabled ? "enabled" : "disabled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling dark mode");
        }
    }

    /// <summary>
    /// Toggle biometric authentication
    /// </summary>
    [RelayCommand]
    private async Task ToggleBiometricAuthAsync()
    {
        try
        {
            if (IsBiometricAuthEnabled && IsBiometricAvailable)
            {
                // Verify with biometric before enabling
                var result = await _authenticationService.AuthenticateWithBiometricsAsync();
                if (!result)
                {
                    IsBiometricAuthEnabled = false;
                    await Application.Current!.MainPage!.DisplayAlert(
                        "Authentication Failed",
                        "Biometric authentication failed. Please try again.",
                        "OK");
                    return;
                }
            }

            await _secureStorageService.SetAsync("BiometricAuth", IsBiometricAuthEnabled.ToString().ToLower());
            _logger.LogInformation("Biometric auth {Status}", IsBiometricAuthEnabled ? "enabled" : "disabled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling biometric auth");
            IsBiometricAuthEnabled = false;
        }
    }

    /// <summary>
    /// Toggle push notifications
    /// </summary>
    [RelayCommand]
    private async Task TogglePushNotificationsAsync()
    {
        try
        {
            await _secureStorageService.SetAsync("PushNotifications", IsPushNotificationsEnabled.ToString().ToLower());
            _logger.LogInformation("Push notifications {Status}", IsPushNotificationsEnabled ? "enabled" : "disabled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling push notifications");
        }
    }

    /// <summary>
    /// Toggle auto-connect
    /// </summary>
    [RelayCommand]
    private async Task ToggleAutoConnectAsync()
    {
        try
        {
            await _secureStorageService.SetAsync("AutoConnect", IsAutoConnectEnabled.ToString().ToLower());
            _logger.LogInformation("Auto-connect {Status}", IsAutoConnectEnabled ? "enabled" : "disabled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling auto-connect");
        }
    }

    /// <summary>
    /// Clear cache
    /// </summary>
    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        try
        {
            var confirm = await Application.Current!.MainPage!.DisplayAlert(
                "Clear Cache",
                "Are you sure you want to clear the app cache? This will remove offline data.",
                "Yes",
                "No");

            if (!confirm)
                return;

            // Clear MAUI file cache
            var cacheDir = FileSystem.CacheDirectory;
            if (Directory.Exists(cacheDir))
            {
                var files = Directory.GetFiles(cacheDir, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete cache file: {File}", file);
                    }
                }
                
                // Remove empty directories
                var directories = Directory.GetDirectories(cacheDir, "*", SearchOption.AllDirectories)
                    .OrderByDescending(d => d.Length); // Delete deepest directories first
                foreach (var dir in directories)
                {
                    try
                    {
                        if (!Directory.EnumerateFileSystemEntries(dir).Any())
                        {
                            Directory.Delete(dir);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete cache directory: {Dir}", dir);
                    }
                }
                
                _logger.LogInformation("Cache cleared: {FileCount} files deleted", files.Length);
            }
            
            await Application.Current.MainPage.DisplayAlert("Success", "Cache cleared successfully", "OK");
            _logger.LogInformation("Cache cleared");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cache");
            await Application.Current!.MainPage!.DisplayAlert("Error", "Failed to clear cache", "OK");
        }
    }

    /// <summary>
    /// Disconnect from server
    /// </summary>
    [RelayCommand]
    private async Task DisconnectAsync()
    {
        try
        {
            var confirm = await Application.Current!.MainPage!.DisplayAlert(
                "Disconnect",
                "Are you sure you want to disconnect from the server? You will need to reconnect manually.",
                "Yes",
                "No");

            if (!confirm)
                return;

            // Clear stored credentials
            await _secureStorageService.RemoveAsync("Token");
            await _secureStorageService.RemoveAsync("ServerUrl");

            ServerUrl = "Not connected";

            // Navigate to login
            await Shell.Current.GoToAsync("//login");

            _logger.LogInformation("Disconnected from server");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting");
        }
    }

    /// <summary>
    /// Show about dialog
    /// </summary>
    [RelayCommand]
    private async Task ShowAboutAsync()
    {
        await Application.Current!.MainPage!.DisplayAlert(
            "About Digital Signage",
            $"Version: {AppVersion}\n\n" +
            "Professional digital signage management app for iOS and Android.\n\n" +
            "© 2024 Digital Signage. All rights reserved.",
            "OK");
    }

    /// <summary>
    /// Check if biometric authentication is available on device
    /// </summary>
    private async void CheckBiometricAvailability()
    {
        try
        {
            IsBiometricAvailable = await _authenticationService.IsBiometricAuthAvailableAsync();
            _logger.LogInformation("Biometric availability: {Available}", IsBiometricAvailable);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking biometric availability");
            IsBiometricAvailable = false;
        }
    }

    /// <summary>
    /// Get app version from assembly
    /// </summary>
    private static string GetAppVersion()
    {
        return AppInfo.VersionString;
    }
}
