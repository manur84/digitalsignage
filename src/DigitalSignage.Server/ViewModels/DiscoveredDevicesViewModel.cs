using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Windows;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// ViewModel for managing discovered devices on the network
/// </summary>
public partial class DiscoveredDevicesViewModel : ObservableObject, IDisposable
{
    private readonly NetworkScannerService _scannerService;
    private readonly IClientService _clientService;
    private readonly ILogger<DiscoveredDevicesViewModel> _logger;
    private readonly ISynchronizationContext _syncContext;
    private System.Timers.Timer? _autoRefreshTimer;
    private bool _disposed = false;

    [ObservableProperty]
    private DiscoveredDevice? _selectedDiscoveredDevice;

    [ObservableProperty]
    private bool _isScanning = false;

    [ObservableProperty]
    private string _scanStatusMessage = "Ready to scan";

    [ObservableProperty]
    private int _discoveredDeviceCount = 0;

    [ObservableProperty]
    private bool _autoRefreshEnabled = false;

    [ObservableProperty]
    private int _autoRefreshInterval = 60; // seconds

    [ObservableProperty]
    private int _staleDeviceThresholdMinutes = 30;

    [ObservableProperty]
    private bool _useDeepScan;

    public ObservableCollection<DiscoveredDevice> DiscoveredDevices { get; } = new();

    public DiscoveredDevicesViewModel(
        NetworkScannerService scannerService,
        IClientService clientService,
        ILogger<DiscoveredDevicesViewModel> logger,
        ISynchronizationContext syncContext)
    {
        _scannerService = scannerService ?? throw new ArgumentNullException(nameof(scannerService));
        _clientService = clientService ?? throw new ArgumentNullException(nameof(clientService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _syncContext = syncContext ?? throw new ArgumentNullException(nameof(syncContext));

        // Subscribe to scanner events
        _scannerService.DeviceDiscovered += OnDeviceDiscovered;
        _scannerService.ScanningStatusChanged += OnScanningStatusChanged;

        // Load existing discovered devices
        RefreshDiscoveredDevicesList();

        // Setup auto-refresh timer
        SetupAutoRefreshTimer();
    }

    /// <summary>
    /// Setup timer for auto-refresh
    /// </summary>
    private void SetupAutoRefreshTimer()
    {
        _autoRefreshTimer = new System.Timers.Timer();
        _autoRefreshTimer.Elapsed += async (s, e) =>
        {
            if (AutoRefreshEnabled && !IsScanning)
            {
                await ScanNetworkCommand.ExecuteAsync(null);
            }
        };
        UpdateAutoRefreshTimer();
    }

    /// <summary>
    /// Update auto-refresh timer interval
    /// </summary>
    private void UpdateAutoRefreshTimer()
    {
        if (_autoRefreshTimer != null)
        {
            _autoRefreshTimer.Interval = AutoRefreshInterval * 1000;
            _autoRefreshTimer.Enabled = AutoRefreshEnabled;
        }
    }

    partial void OnAutoRefreshEnabledChanged(bool value)
    {
        UpdateAutoRefreshTimer();
        _logger.LogInformation("Auto-refresh {Status}", value ? "enabled" : "disabled");
    }

    partial void OnAutoRefreshIntervalChanged(int value)
    {
        UpdateAutoRefreshTimer();
        _logger.LogInformation("Auto-refresh interval changed to {Interval}s", value);
    }

    /// <summary>
    /// Event handler for device discovered
    /// </summary>
    private void OnDeviceDiscovered(object? sender, DiscoveredDevice device)
    {
        _ = _syncContext.RunOnUiThreadAsync(() =>
        {
            // Check if device already exists
            var existing = DiscoveredDevices.FirstOrDefault(d => d.IpAddress == device.IpAddress);
            if (existing != null)
            {
                DiscoveredDevices.Remove(existing);
            }

            DiscoveredDevices.Insert(0, device);
            DiscoveredDeviceCount = DiscoveredDevices.Count;
        });
    }

    /// <summary>
    /// Event handler for scanning status changed
    /// </summary>
    private void OnScanningStatusChanged(object? sender, bool isScanning)
    {
        _ = _syncContext.RunOnUiThreadAsync(() =>
        {
            IsScanning = isScanning;
            ScanStatusMessage = isScanning ? "Scanning network..." : $"Scan complete. Found {DiscoveredDeviceCount} device(s)";
        });
    }

    /// <summary>
    /// Refresh the list of discovered devices
    /// </summary>
    private void RefreshDiscoveredDevicesList()
    {
        DiscoveredDevices.Clear();
        foreach (var device in _scannerService.GetDiscoveredDevices())
        {
            DiscoveredDevices.Add(device);
        }
        DiscoveredDeviceCount = DiscoveredDevices.Count;
    }

    /// <summary>
    /// Scan network command
    /// </summary>
    [RelayCommand]
    private async Task ScanNetwork()
    {
        try
        {
            IsScanning = true;
            ScanStatusMessage = "Starting network scan...";
            _logger.LogInformation("Starting manual network scan");

            // Remove stale devices before scanning
            _scannerService.RemoveStaleDevices(TimeSpan.FromMinutes(StaleDeviceThresholdMinutes));

            var scanMode = UseDeepScan ? NetworkScanMode.Deep : NetworkScanMode.Quick;
            var count = await _scannerService.ScanNetworkAsync(scanMode);

            RefreshDiscoveredDevicesList();
            var modeLabel = scanMode == NetworkScanMode.Deep ? "Deep" : "Quick";
            ScanStatusMessage = $"Scan complete ({modeLabel}). Discovered {count} device(s)";
            _logger.LogInformation("{Mode} network scan completed. Found {Count} devices", modeLabel, count);
        }
        catch (Exception ex)
        {
            ScanStatusMessage = $"Scan failed: {ex.Message}";
            _logger.LogError(ex, "Network scan failed");
            MessageBox.Show($"Network scan failed: {ex.Message}", "Scan Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsScanning = false;
        }
    }

    /// <summary>
    /// Clear all discovered devices
    /// </summary>
    [RelayCommand]
    private void ClearDiscoveredDevices()
    {
        try
        {
            _scannerService.ClearDiscoveredDevices();
            DiscoveredDevices.Clear();
            DiscoveredDeviceCount = 0;
            ScanStatusMessage = "Discovered devices cleared";
            _logger.LogInformation("Cleared all discovered devices");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear discovered devices");
            MessageBox.Show($"Failed to clear devices: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Remove selected discovered device
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRemoveDiscoveredDevice))]
    private void RemoveDiscoveredDevice()
    {
        if (SelectedDiscoveredDevice == null) return;

        try
        {
            var ipAddress = SelectedDiscoveredDevice.IpAddress;
            _scannerService.RemoveDiscoveredDevice(ipAddress);
            DiscoveredDevices.Remove(SelectedDiscoveredDevice);
            DiscoveredDeviceCount = DiscoveredDevices.Count;
            ScanStatusMessage = $"Removed device {ipAddress}";
            _logger.LogInformation("Removed discovered device: {IpAddress}", ipAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove discovered device");
            MessageBox.Show($"Failed to remove device: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanRemoveDiscoveredDevice() => SelectedDiscoveredDevice != null;

    /// <summary>
    /// Register selected discovered device
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRegisterDevice))]
    private async Task RegisterDevice()
    {
        if (SelectedDiscoveredDevice == null) return;

        try
        {
            _logger.LogInformation("Registering device: {Hostname} ({IpAddress})",
                SelectedDiscoveredDevice.Hostname, SelectedDiscoveredDevice.IpAddress);

            // Open registration dialog
            var dialog = new Views.Dialogs.RegisterDiscoveredDeviceDialog(SelectedDiscoveredDevice);
            if (dialog.ShowDialog() == true)
            {
                var newClient = dialog.RegisteredClient;
                if (newClient != null)
                {
                    // Register via ClientService using RegisterMessage
                    var registerMessage = new Core.Models.RegisterMessage
                    {
                        ClientId = newClient.Id,
                        MacAddress = newClient.MacAddress,
                        IpAddress = newClient.IpAddress,
                        DeviceInfo = newClient.DeviceInfo,
                        RegistrationToken = newClient.Metadata.TryGetValue("RegistrationToken", out var token)
                            ? token?.ToString()
                            : null
                    };

                    var registerResult = await _clientService.RegisterClientAsync(registerMessage);

                    if (registerResult.IsFailure || registerResult.Value == null)
                    {
                        _logger.LogError("Failed to register device: {ErrorMessage}", registerResult.ErrorMessage ?? "Null result");
                        MessageBox.Show($"Failed to register device: {registerResult.ErrorMessage ?? "Null result"}",
                            "Registration Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    var registeredClient = registerResult.Value;

                    // Update client properties that couldn't be set in RegisterMessage
                    var updateStatusResult = await _clientService.UpdateClientStatusAsync(
                        registeredClient.Id,
                        ClientStatus.Offline,
                        registeredClient.DeviceInfo);

                    if (updateStatusResult.IsFailure)
                    {
                        _logger.LogWarning("Failed to update client status: {ErrorMessage}", updateStatusResult.ErrorMessage);
                    }

                    // Remove from discovered devices
                    _scannerService.RemoveDiscoveredDevice(SelectedDiscoveredDevice.IpAddress);
                    DiscoveredDevices.Remove(SelectedDiscoveredDevice);
                    DiscoveredDeviceCount = DiscoveredDevices.Count;

                    ScanStatusMessage = $"Registered device: {newClient.Name}";
                    _logger.LogInformation("Device registered successfully: {Name} ({IpAddress})",
                        newClient.Name, newClient.IpAddress);

                    MessageBox.Show($"Device '{newClient.Name}' registered successfully!",
                        "Registration Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register device");
            MessageBox.Show($"Failed to register device: {ex.Message}", "Registration Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanRegisterDevice() => SelectedDiscoveredDevice != null;

    /// <summary>
    /// Remove stale devices
    /// </summary>
    [RelayCommand]
    private void RemoveStaleDevices()
    {
        try
        {
            var threshold = TimeSpan.FromMinutes(StaleDeviceThresholdMinutes);
            _scannerService.RemoveStaleDevices(threshold);
            RefreshDiscoveredDevicesList();
            ScanStatusMessage = $"Removed devices older than {StaleDeviceThresholdMinutes} minutes";
            _logger.LogInformation("Removed stale devices (threshold: {Threshold} minutes)", StaleDeviceThresholdMinutes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove stale devices");
            MessageBox.Show($"Failed to remove stale devices: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Refresh discovered devices list
    /// </summary>
    [RelayCommand]
    private void RefreshList()
    {
        RefreshDiscoveredDevicesList();
        ScanStatusMessage = $"List refreshed. {DiscoveredDeviceCount} device(s)";
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Unsubscribe from events
            _scannerService.DeviceDiscovered -= OnDeviceDiscovered;
            _scannerService.ScanningStatusChanged -= OnScanningStatusChanged;

            // Dispose timer
            _autoRefreshTimer?.Stop();
            _autoRefreshTimer?.Dispose();
        }

        _disposed = true;
    }
}
