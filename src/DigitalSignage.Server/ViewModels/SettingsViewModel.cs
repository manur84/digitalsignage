using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// ViewModel for the Settings Dialog
/// Manages application configuration settings
/// </summary>
public partial class SettingsViewModel : ObservableValidator
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly IDialogService _dialogService;
    private readonly NetworkInterfaceService _networkInterfaceService;
    private readonly string _appSettingsPath;

    #region Server Settings

    [ObservableProperty]
    [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
    private int _port = 8080;

    [ObservableProperty]
    private bool _autoSelectPort = true;

    [ObservableProperty]
    private string _alternativePorts = "8081, 8082, 8083, 8888, 9000";

    [ObservableProperty]
    private bool _enableSsl = false;

    [ObservableProperty]
    private string _certificateThumbprint = string.Empty;

    [ObservableProperty]
    private string _certificatePath = string.Empty;

    [ObservableProperty]
    private string _certificatePassword = string.Empty;

    [ObservableProperty]
    private string _endpointPath = "/ws/";

    [ObservableProperty]
    [Range(1024, 104857600, ErrorMessage = "Max message size must be between 1KB and 100MB")]
    private int _maxMessageSize = 1048576;

    [ObservableProperty]
    [Range(10, 600, ErrorMessage = "Timeout must be between 10 and 600 seconds")]
    private int _clientHeartbeatTimeout = 90;

    #endregion

    #region Database Settings

    [ObservableProperty]
    private string _connectionString = "Data Source=digitalsignage.db";

    [ObservableProperty]
    private bool _enableAutoBackup = false;

    [ObservableProperty]
    [Range(1, 168, ErrorMessage = "Backup interval must be between 1 and 168 hours")]
    private int _backupInterval = 24;

    [ObservableProperty]
    [Range(1, 365, ErrorMessage = "Retention days must be between 1 and 365")]
    private int _backupRetentionDays = 30;

    [ObservableProperty]
    private string _backupPath = "backups";

    #endregion

    #region Logging Settings

    [ObservableProperty]
    private string _minimumLogLevel = "Information";

    [ObservableProperty]
    private string _logFilePath = "logs/digitalsignage-.log";

    [ObservableProperty]
    [Range(1, 1000, ErrorMessage = "File size limit must be between 1 and 1000 MB")]
    private int _maxLogFileSizeMB = 100;

    [ObservableProperty]
    [Range(1, 365, ErrorMessage = "Retained file count must be between 1 and 365")]
    private int _retainedLogFileCount = 30;

    [ObservableProperty]
    private bool _enableConsoleLogging = true;

    [ObservableProperty]
    private bool _enableDebugLogging = true;

    #endregion

    #region Query Cache Settings

    [ObservableProperty]
    private bool _enableQueryCaching = true;

    [ObservableProperty]
    [Range(10, 3600, ErrorMessage = "Cache duration must be between 10 and 3600 seconds")]
    private int _defaultCacheDuration = 300;

    [ObservableProperty]
    [Range(100, 10000, ErrorMessage = "Max cache entries must be between 100 and 10000")]
    private int _maxCacheEntries = 1000;

    [ObservableProperty]
    private bool _enableCacheStatistics = true;

    #endregion

    #region Connection Pool Settings

    [ObservableProperty]
    [Range(1, 50, ErrorMessage = "Min pool size must be between 1 and 50")]
    private int _minPoolSize = 5;

    [ObservableProperty]
    [Range(10, 200, ErrorMessage = "Max pool size must be between 10 and 200")]
    private int _maxPoolSize = 100;

    [ObservableProperty]
    [Range(5, 120, ErrorMessage = "Connection timeout must be between 5 and 120 seconds")]
    private int _connectionTimeout = 30;

    [ObservableProperty]
    [Range(5, 300, ErrorMessage = "Command timeout must be between 5 and 300 seconds")]
    private int _commandTimeout = 30;

    [ObservableProperty]
    private bool _enablePooling = true;

    #endregion

    #region Discovery Settings

    [ObservableProperty]
    private bool _enableMdns = true;

    [ObservableProperty]
    private bool _enableUdpBroadcast = true;

    [ObservableProperty]
    [Range(5, 300, ErrorMessage = "Discovery interval must be between 5 and 300 seconds")]
    private int _discoveryInterval = 30;

    #endregion

    #region Network Interface Settings

    [ObservableProperty]
    private string _preferredNetworkInterface = string.Empty;

    [ObservableProperty]
    private ObservableCollection<NetworkInterfaceInfo> _availableNetworkInterfaces = new();

    [ObservableProperty]
    private NetworkInterfaceInfo? _selectedNetworkInterface;

    #endregion

    #region State Management

    [ObservableProperty]
    private bool _hasUnsavedChanges = false;

    [ObservableProperty]
    private bool _isSaving = false;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    #endregion

    public SettingsViewModel(
        IConfiguration configuration,
        IDialogService dialogService,
        ILogger<SettingsViewModel> logger,
        NetworkInterfaceService networkInterfaceService)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _networkInterfaceService = networkInterfaceService ?? throw new ArgumentNullException(nameof(networkInterfaceService));
        _appSettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

        // Subscribe to property changes to track dirty state
        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != nameof(HasUnsavedChanges) &&
                e.PropertyName != nameof(IsSaving) &&
                e.PropertyName != nameof(StatusMessage) &&
                e.PropertyName != nameof(AvailableNetworkInterfaces))
            {
                HasUnsavedChanges = true;
            }
        };

        // Subscribe to selected interface changes
        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SelectedNetworkInterface))
            {
                if (SelectedNetworkInterface != null)
                {
                    PreferredNetworkInterface = SelectedNetworkInterface.Name;
                }
            }
        };

        LoadSettings();
        LoadNetworkInterfaces();
    }

    /// <summary>
    /// Load settings from appsettings.json
    /// </summary>
    private void LoadSettings()
    {
        try
        {
            _logger.LogInformation("Loading settings from configuration");

            // Server Settings
            Port = _configuration.GetValue<int>("ServerSettings:Port", 8080);
            AutoSelectPort = _configuration.GetValue<bool>("ServerSettings:AutoSelectPort", true);

            var altPorts = _configuration.GetSection("ServerSettings:AlternativePorts").Get<int[]>() ?? new[] { 8081, 8082, 8083, 8888, 9000 };
            AlternativePorts = string.Join(", ", altPorts);

            EnableSsl = _configuration.GetValue<bool>("ServerSettings:EnableSsl", false);
            CertificateThumbprint = _configuration.GetValue<string>("ServerSettings:CertificateThumbprint") ?? string.Empty;
            CertificatePath = _configuration.GetValue<string>("ServerSettings:CertificatePath") ?? string.Empty;
            CertificatePassword = _configuration.GetValue<string>("ServerSettings:CertificatePassword") ?? string.Empty;
            EndpointPath = _configuration.GetValue<string>("ServerSettings:EndpointPath") ?? "/ws/";
            MaxMessageSize = _configuration.GetValue<int>("ServerSettings:MaxMessageSize", 1048576);
            ClientHeartbeatTimeout = _configuration.GetValue<int>("ServerSettings:ClientHeartbeatTimeout", 90);

            // Database Settings
            ConnectionString = _configuration.GetConnectionString("DefaultConnection") ?? "Data Source=digitalsignage.db";
            EnableAutoBackup = _configuration.GetValue<bool>("DatabaseSettings:EnableAutoBackup", false);
            BackupInterval = _configuration.GetValue<int>("DatabaseSettings:BackupInterval", 24);
            BackupRetentionDays = _configuration.GetValue<int>("DatabaseSettings:BackupRetentionDays", 30);
            BackupPath = _configuration.GetValue<string>("DatabaseSettings:BackupPath") ?? "backups";

            // Logging Settings
            MinimumLogLevel = _configuration.GetValue<string>("Serilog:MinimumLevel:Default") ?? "Information";
            var logFile = _configuration.GetValue<string>("Serilog:WriteTo:2:Args:path") ?? "logs/digitalsignage-.log";
            LogFilePath = logFile;

            var fileSizeBytes = _configuration.GetValue<long>("Serilog:WriteTo:2:Args:fileSizeLimitBytes", 104857600);
            MaxLogFileSizeMB = (int)(fileSizeBytes / 1024 / 1024);

            RetainedLogFileCount = _configuration.GetValue<int>("Serilog:WriteTo:2:Args:retainedFileCountLimit", 30);
            EnableConsoleLogging = true;
            EnableDebugLogging = true;

            // Query Cache Settings
            EnableQueryCaching = _configuration.GetValue<bool>("QueryCacheSettings:EnableCaching", true);
            DefaultCacheDuration = _configuration.GetValue<int>("QueryCacheSettings:DefaultCacheDuration", 300);
            MaxCacheEntries = _configuration.GetValue<int>("QueryCacheSettings:MaxCacheEntries", 1000);
            EnableCacheStatistics = _configuration.GetValue<bool>("QueryCacheSettings:EnableStatistics", true);

            // Connection Pool Settings
            MinPoolSize = _configuration.GetValue<int>("ConnectionPoolSettings:MinPoolSize", 5);
            MaxPoolSize = _configuration.GetValue<int>("ConnectionPoolSettings:MaxPoolSize", 100);
            ConnectionTimeout = _configuration.GetValue<int>("ConnectionPoolSettings:ConnectionTimeout", 30);
            CommandTimeout = _configuration.GetValue<int>("ConnectionPoolSettings:CommandTimeout", 30);
            EnablePooling = _configuration.GetValue<bool>("ConnectionPoolSettings:Pooling", true);

            // Discovery Settings
            EnableMdns = _configuration.GetValue<bool>("DiscoverySettings:EnableMdns", true);
            EnableUdpBroadcast = _configuration.GetValue<bool>("DiscoverySettings:EnableUdpBroadcast", true);
            DiscoveryInterval = _configuration.GetValue<int>("DiscoverySettings:DiscoveryInterval", 30);

            // Network Interface Settings
            PreferredNetworkInterface = _configuration.GetValue<string>("ServerSettings:PreferredNetworkInterface") ?? string.Empty;

            HasUnsavedChanges = false;
            StatusMessage = "Settings loaded successfully";
            _logger.LogInformation("Settings loaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings");
            StatusMessage = $"Error loading settings: {ex.Message}";
        }
    }

    /// <summary>
    /// Load available network interfaces
    /// </summary>
    private void LoadNetworkInterfaces()
    {
        try
        {
            _logger.LogInformation("Loading network interfaces");

            var interfaces = _networkInterfaceService.GetAllNetworkInterfaces();
            AvailableNetworkInterfaces.Clear();

            foreach (var networkInterface in interfaces)
            {
                AvailableNetworkInterfaces.Add(networkInterface);
            }

            // Select the preferred interface if set
            if (!string.IsNullOrWhiteSpace(PreferredNetworkInterface))
            {
                SelectedNetworkInterface = AvailableNetworkInterfaces
                    .FirstOrDefault(i => i.Name.Equals(PreferredNetworkInterface, StringComparison.OrdinalIgnoreCase) ||
                                        i.IpAddress.Equals(PreferredNetworkInterface, StringComparison.Ordinal));
            }

            _logger.LogInformation("Loaded {Count} network interfaces", AvailableNetworkInterfaces.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load network interfaces");
        }
    }

    /// <summary>
    /// Refresh network interfaces list
    /// </summary>
    [RelayCommand]
    private void RefreshNetworkInterfaces()
    {
        LoadNetworkInterfaces();
        StatusMessage = $"Network interfaces refreshed. Found {AvailableNetworkInterfaces.Count} interfaces.";
    }

    /// <summary>
    /// Save settings to appsettings.json
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsSaving) return;

        try
        {
            IsSaving = true;
            StatusMessage = "Saving settings...";
            _logger.LogInformation("Saving settings to {Path}", _appSettingsPath);

            // Validate all properties
            ValidateAllProperties();
            if (HasErrors)
            {
                StatusMessage = "Validation errors found. Please correct them before saving.";
                _logger.LogWarning("Settings validation failed");
                return;
            }

            // Read existing appsettings.json
            var json = await File.ReadAllTextAsync(_appSettingsPath);
            var settings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? new Dictionary<string, JsonElement>();

            // Update Server Settings
            var serverSettings = new Dictionary<string, object?>
            {
                ["Port"] = Port,
                ["AutoSelectPort"] = AutoSelectPort,
                ["AlternativePorts"] = ParseAlternativePorts(),
                ["EnableSsl"] = EnableSsl,
                ["CertificateThumbprint"] = string.IsNullOrWhiteSpace(CertificateThumbprint) ? null : CertificateThumbprint,
                ["CertificatePath"] = string.IsNullOrWhiteSpace(CertificatePath) ? null : CertificatePath,
                ["CertificatePassword"] = string.IsNullOrWhiteSpace(CertificatePassword) ? null : CertificatePassword,
                ["EndpointPath"] = EndpointPath,
                ["MaxMessageSize"] = MaxMessageSize,
                ["ClientHeartbeatTimeout"] = ClientHeartbeatTimeout,
                ["PreferredNetworkInterface"] = string.IsNullOrWhiteSpace(PreferredNetworkInterface) ? null : PreferredNetworkInterface
            };
            settings["ServerSettings"] = JsonSerializer.SerializeToElement(serverSettings);

            // Update Connection String
            var connectionStrings = new Dictionary<string, string>
            {
                ["DefaultConnection"] = ConnectionString
            };
            settings["ConnectionStrings"] = JsonSerializer.SerializeToElement(connectionStrings);

            // Update Database Settings
            var databaseSettings = new Dictionary<string, object>
            {
                ["EnableAutoBackup"] = EnableAutoBackup,
                ["BackupInterval"] = BackupInterval,
                ["BackupRetentionDays"] = BackupRetentionDays,
                ["BackupPath"] = BackupPath
            };
            settings["DatabaseSettings"] = JsonSerializer.SerializeToElement(databaseSettings);

            // Update Query Cache Settings
            var queryCacheSettings = new Dictionary<string, object>
            {
                ["EnableCaching"] = EnableQueryCaching,
                ["DefaultCacheDuration"] = DefaultCacheDuration,
                ["MaxCacheEntries"] = MaxCacheEntries,
                ["EnableStatistics"] = EnableCacheStatistics
            };
            settings["QueryCacheSettings"] = JsonSerializer.SerializeToElement(queryCacheSettings);

            // Update Connection Pool Settings
            var connectionPoolSettings = new Dictionary<string, object>
            {
                ["MinPoolSize"] = MinPoolSize,
                ["MaxPoolSize"] = MaxPoolSize,
                ["ConnectionTimeout"] = ConnectionTimeout,
                ["CommandTimeout"] = CommandTimeout,
                ["Pooling"] = EnablePooling
            };
            settings["ConnectionPoolSettings"] = JsonSerializer.SerializeToElement(connectionPoolSettings);

            // Update Discovery Settings
            var discoverySettings = new Dictionary<string, object>
            {
                ["EnableMdns"] = EnableMdns,
                ["EnableUdpBroadcast"] = EnableUdpBroadcast,
                ["DiscoveryInterval"] = DiscoveryInterval
            };
            settings["DiscoverySettings"] = JsonSerializer.SerializeToElement(discoverySettings);

            // Update Serilog settings (minimal update to preserve structure)
            if (settings.TryGetValue("Serilog", out var serilogElement))
            {
                var serilogJson = serilogElement.GetRawText();
                var serilogSettings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(serilogJson) ?? new Dictionary<string, JsonElement>();

                // Update MinimumLevel
                if (serilogSettings.TryGetValue("MinimumLevel", out var minLevelElement))
                {
                    var minLevelJson = minLevelElement.GetRawText();
                    var minLevel = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(minLevelJson) ?? new Dictionary<string, JsonElement>();
                    minLevel["Default"] = JsonSerializer.SerializeToElement(MinimumLogLevel);
                    serilogSettings["MinimumLevel"] = JsonSerializer.SerializeToElement(minLevel);
                }

                settings["Serilog"] = JsonSerializer.SerializeToElement(serilogSettings);
            }

            // Write back to file with pretty formatting
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var updatedJson = JsonSerializer.Serialize(settings, options);
            await File.WriteAllTextAsync(_appSettingsPath, updatedJson);

            HasUnsavedChanges = false;
            StatusMessage = "Settings saved successfully. Restart required for changes to take effect.";
            _logger.LogInformation("Settings saved successfully");

            await _dialogService.ShowInformationAsync(
                "Settings saved successfully!\n\nPlease restart the application for changes to take effect.",
                "Settings Saved");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            StatusMessage = $"Error saving settings: {ex.Message}";

            await _dialogService.ShowErrorAsync(
                $"Failed to save settings:\n\n{ex.Message}",
                "Save Error");
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>
    /// Reset all settings to default values
    /// </summary>
    [RelayCommand]
    private async Task ResetToDefaults()
    {
        var result = await _dialogService.ShowConfirmationAsync(
            "Are you sure you want to reset all settings to default values?\n\n" +
            "This will discard any unsaved changes.",
            "Reset to Defaults");

        if (result)
        {
            _logger.LogInformation("Resetting settings to defaults");

            // Server Settings
            Port = 8080;
            AutoSelectPort = true;
            AlternativePorts = "8081, 8082, 8083, 8888, 9000";
            EnableSsl = false;
            CertificateThumbprint = string.Empty;
            CertificatePath = string.Empty;
            CertificatePassword = string.Empty;
            EndpointPath = "/ws/";
            MaxMessageSize = 1048576;
            ClientHeartbeatTimeout = 90;

            // Database Settings
            ConnectionString = "Data Source=digitalsignage.db";
            EnableAutoBackup = false;
            BackupInterval = 24;
            BackupRetentionDays = 30;
            BackupPath = "backups";

            // Logging Settings
            MinimumLogLevel = "Information";
            LogFilePath = "logs/digitalsignage-.log";
            MaxLogFileSizeMB = 100;
            RetainedLogFileCount = 30;
            EnableConsoleLogging = true;
            EnableDebugLogging = true;

            // Query Cache Settings
            EnableQueryCaching = true;
            DefaultCacheDuration = 300;
            MaxCacheEntries = 1000;
            EnableCacheStatistics = true;

            // Connection Pool Settings
            MinPoolSize = 5;
            MaxPoolSize = 100;
            ConnectionTimeout = 30;
            CommandTimeout = 30;
            EnablePooling = true;

            // Discovery Settings
            EnableMdns = true;
            EnableUdpBroadcast = true;
            DiscoveryInterval = 30;

            HasUnsavedChanges = true;
            StatusMessage = "Settings reset to defaults";
        }
    }

    /// <summary>
    /// Browse for certificate file
    /// </summary>
    [RelayCommand]
    private void BrowseCertificate()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Certificate Files (*.pfx)|*.pfx|All Files (*.*)|*.*",
            Title = "Select SSL Certificate"
        };

        if (dialog.ShowDialog() == true)
        {
            CertificatePath = dialog.FileName;
        }
    }

    /// <summary>
    /// Browse for backup path - Not implemented, user can type path directly
    /// </summary>
    [RelayCommand]
    private async Task BrowseBackupPath()
    {
        await _dialogService.ShowInformationAsync(
            "Please enter the backup directory path directly in the text field.\n\n" +
            "Example paths:\n" +
            "• backups\n" +
            "• C:\\Backups\\DigitalSignage\n" +
            "• \\\\server\\share\\backups",
            "Backup Directory");
    }

    /// <summary>
    /// Parse alternative ports from comma-separated string
    /// </summary>
    private int[] ParseAlternativePorts()
    {
        try
        {
            return AlternativePorts
                .Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(int.Parse)
                .ToArray();
        }
        catch
        {
            _logger.LogWarning("Failed to parse alternative ports, using defaults");
            return new[] { 8081, 8082, 8083, 8888, 9000 };
        }
    }

    /// <summary>
    /// Check if there are unsaved changes before closing
    /// </summary>
    public async Task<bool> CanClose()
    {
        if (!HasUnsavedChanges)
            return true;

        var result = await _dialogService.ShowYesNoCancelAsync(
            "You have unsaved changes. Do you want to save them before closing?",
            "Unsaved Changes");

        if (result == true) // Yes
        {
            await SaveAsync();
            return !HasUnsavedChanges; // Only close if save succeeded
        }

        return result == false; // No = true, Cancel = false
    }
}
