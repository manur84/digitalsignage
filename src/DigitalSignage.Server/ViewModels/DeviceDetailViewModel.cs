using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Windows;
using DigitalSignage.Server.Services;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// ViewModel for displaying detailed device information
/// </summary>
public partial class DeviceDetailViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<DeviceDetailViewModel> _logger;
    private readonly IDialogService _dialogService;
    private readonly ISynchronizationContext _syncContext;
    private readonly IClientService _clientService;
    private readonly System.Timers.Timer _refreshTimer;
    private RaspberryPiClient? _client;
    private bool _disposed;

    #region Observable Properties

    [ObservableProperty]
    private string _windowTitle = "Device Details";

    [ObservableProperty]
    private string _deviceName = string.Empty;

    [ObservableProperty]
    private string _hostname = string.Empty;

    [ObservableProperty]
    private string _ipAddress = string.Empty;

    [ObservableProperty]
    private string _macAddress = string.Empty;

    [ObservableProperty]
    private string _status = "Offline";

    [ObservableProperty]
    private string _location = "Not set";

    [ObservableProperty]
    private string _group = "Not set";

    [ObservableProperty]
    private string _model = "Unknown";

    [ObservableProperty]
    private string _osVersion = "Unknown";

    [ObservableProperty]
    private string _clientVersion = "Unknown";

    [ObservableProperty]
    private string _resolution = "0x0";

    [ObservableProperty]
    private string _assignedLayout = "Not assigned";

    [ObservableProperty]
    private double _cpuUsage = 0;

    [ObservableProperty]
    private double _cpuTemperature = 0;

    [ObservableProperty]
    private long _memoryTotal = 0;

    [ObservableProperty]
    private long _memoryUsed = 0;

    [ObservableProperty]
    private long _diskTotal = 0;

    [ObservableProperty]
    private long _diskUsed = 0;

    [ObservableProperty]
    private int _networkLatency = 0;

    [ObservableProperty]
    private string _uptime = "Unknown";

    [ObservableProperty]
    private DateTime _registeredAt = DateTime.Now;

    [ObservableProperty]
    private DateTime _lastSeen = DateTime.Now;

    [ObservableProperty]
    private bool _isPingInProgress = false;

    [ObservableProperty]
    private string _pingResult = string.Empty;

    [ObservableProperty]
    private bool _isAutoRefreshEnabled = true;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    // Computed properties for UI display
    [ObservableProperty]
    private string _memoryUsageText = "0 MB / 0 MB";

    [ObservableProperty]
    private double _memoryUsagePercent = 0;

    [ObservableProperty]
    private string _diskUsageText = "0 GB / 0 GB";

    [ObservableProperty]
    private double _diskUsagePercent = 0;

    #endregion

    /// <summary>
    /// Event raised when the window should close
    /// </summary>
    public event EventHandler? CloseRequested;

    public DeviceDetailViewModel(ILogger<DeviceDetailViewModel> logger, IDialogService dialogService, ISynchronizationContext syncContext, IClientService clientService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _syncContext = syncContext ?? throw new ArgumentNullException(nameof(syncContext));
        _clientService = clientService ?? throw new ArgumentNullException(nameof(clientService));

        // Setup auto-refresh timer (every 5 seconds)
        _refreshTimer = new System.Timers.Timer(5000);
        _refreshTimer.Elapsed += async (s, e) =>
        {
            if (IsAutoRefreshEnabled && _client != null)
            {
                await _syncContext.RunOnUiThreadAsync(() =>
                {
                    LoadDeviceInfo(_client);
                });
            }
        };
    }

    /// <summary>
    /// Load device information
    /// </summary>
    public void LoadDeviceInfo(RaspberryPiClient client)
    {
        try
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));

            _logger.LogInformation("Loading device details for {DeviceName}", client.Name);

            // DEBUG: Log ALL DeviceInfo values to diagnose why they're not displaying
            _logger.LogInformation("DeviceInfo Debug - Hostname: '{Hostname}', Model: '{Model}', OS: '{Os}', Version: '{Version}'",
                client.DeviceInfo?.Hostname ?? "NULL",
                client.DeviceInfo?.Model ?? "NULL",
                client.DeviceInfo?.OsVersion ?? "NULL",
                client.DeviceInfo?.ClientVersion ?? "NULL");
            _logger.LogInformation("DeviceInfo Debug - Resolution: {Width}x{Height}, CPU: {Cpu}%, Temp: {Temp}C",
                client.DeviceInfo?.ScreenWidth ?? 0,
                client.DeviceInfo?.ScreenHeight ?? 0,
                client.DeviceInfo?.CpuUsage ?? 0,
                client.DeviceInfo?.CpuTemperature ?? 0);
            _logger.LogInformation("DeviceInfo Debug - Memory: {MemUsed}/{MemTotal}, Disk: {DiskUsed}/{DiskTotal}",
                client.DeviceInfo?.MemoryUsed ?? 0,
                client.DeviceInfo?.MemoryTotal ?? 0,
                client.DeviceInfo?.DiskUsed ?? 0,
                client.DeviceInfo?.DiskTotal ?? 0);

            // Ensure DeviceInfo is never null
            if (client.DeviceInfo == null)
            {
                _logger.LogWarning("Client {ClientId} has null DeviceInfo - creating default", client.Id);
                client.DeviceInfo = new DeviceInfo();
            }

            // Basic information
            DeviceName = string.IsNullOrWhiteSpace(client.Name) ? client.DeviceInfo.Hostname : client.Name;
            Hostname = client.DeviceInfo.Hostname ?? string.Empty;
            IpAddress = client.IpAddress ?? string.Empty;
            MacAddress = client.MacAddress ?? string.Empty;
            Status = client.Status.ToString();
            Location = string.IsNullOrWhiteSpace(client.Location) ? "Not set" : client.Location;
            Group = string.IsNullOrWhiteSpace(client.Group) ? "Not set" : client.Group;

            // Device information
            Model = string.IsNullOrWhiteSpace(client.DeviceInfo.Model) ? "Unknown" : client.DeviceInfo.Model;
            OsVersion = string.IsNullOrWhiteSpace(client.DeviceInfo.OsVersion) ? "Unknown" : client.DeviceInfo.OsVersion;
            ClientVersion = string.IsNullOrWhiteSpace(client.DeviceInfo.ClientVersion) ? "Unknown" : client.DeviceInfo.ClientVersion;
            Resolution = $"{client.DeviceInfo.ScreenWidth}x{client.DeviceInfo.ScreenHeight}";
            AssignedLayout = client.AssignedLayoutName ?? "Not assigned";

            // Hardware metrics
            CpuUsage = client.DeviceInfo.CpuUsage;
            CpuTemperature = client.DeviceInfo.CpuTemperature;

            // Memory
            MemoryTotal = client.DeviceInfo.MemoryTotal;
            MemoryUsed = client.DeviceInfo.MemoryUsed;
            UpdateMemoryDisplay();

            // Disk
            DiskTotal = client.DeviceInfo.DiskTotal;
            DiskUsed = client.DeviceInfo.DiskUsed;
            UpdateDiskDisplay();

            // Network
            NetworkLatency = client.DeviceInfo.NetworkLatency;

            // Uptime
            Uptime = FormatUptime(client.DeviceInfo.Uptime);

            // Timestamps
            RegisteredAt = client.RegisteredAt.ToLocalTime();
            LastSeen = client.LastSeen.ToLocalTime();

            // Window title
            WindowTitle = $"Device Details - {DeviceName}";

            StatusMessage = $"Last updated: {DateTime.Now:HH:mm:ss}";

            // Start auto-refresh if not already running
            if (!_refreshTimer.Enabled && IsAutoRefreshEnabled)
            {
                _refreshTimer.Start();
            }

            _logger.LogInformation("Device details loaded successfully for {DeviceName}", DeviceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load device information");
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Update memory display text and percentage
    /// </summary>
    private void UpdateMemoryDisplay()
    {
        if (MemoryTotal > 0)
        {
            double memoryUsedMB = MemoryUsed / 1024.0 / 1024.0;
            double memoryTotalMB = MemoryTotal / 1024.0 / 1024.0;
            MemoryUsageText = $"{memoryUsedMB:F0} MB / {memoryTotalMB:F0} MB";
            MemoryUsagePercent = (double)MemoryUsed / MemoryTotal * 100;
        }
        else
        {
            MemoryUsageText = "N/A";
            MemoryUsagePercent = 0;
        }
    }

    /// <summary>
    /// Update disk display text and percentage
    /// </summary>
    private void UpdateDiskDisplay()
    {
        if (DiskTotal > 0)
        {
            double diskUsedGB = DiskUsed / 1024.0 / 1024.0 / 1024.0;
            double diskTotalGB = DiskTotal / 1024.0 / 1024.0 / 1024.0;
            DiskUsageText = $"{diskUsedGB:F1} GB / {diskTotalGB:F1} GB";
            DiskUsagePercent = (double)DiskUsed / DiskTotal * 100;
        }
        else
        {
            DiskUsageText = "N/A";
            DiskUsagePercent = 0;
        }
    }

    /// <summary>
    /// Format uptime in seconds to readable string
    /// </summary>
    private string FormatUptime(long seconds)
    {
        if (seconds == 0)
            return "Unknown";

        var ts = TimeSpan.FromSeconds(seconds);

        if (ts.TotalDays >= 1)
            return $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m";
        else if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        else
            return $"{ts.Minutes}m {ts.Seconds}s";
    }

    /// <summary>
    /// Ping the device to test connectivity
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPing))]
    private async Task PingDevice()
    {
        if (string.IsNullOrWhiteSpace(IpAddress))
        {
            PingResult = "Error: No IP address";
            return;
        }

        IsPingInProgress = true;
        PingResult = "Pinging...";
        StatusMessage = $"Pinging {IpAddress}...";

        try
        {
            _logger.LogInformation("Pinging device at {IpAddress}", IpAddress);

            using var ping = new Ping();
            var stopwatch = Stopwatch.StartNew();
            var reply = await ping.SendPingAsync(IpAddress, 5000); // 5 second timeout
            stopwatch.Stop();

            if (reply.Status == IPStatus.Success)
            {
                PingResult = $"✓ Reply from {IpAddress} in {reply.RoundtripTime}ms (TTL={reply.Options?.Ttl})";
                StatusMessage = $"Ping successful: {reply.RoundtripTime}ms";
                _logger.LogInformation("Ping successful: {RoundtripTime}ms", reply.RoundtripTime);
            }
            else
            {
                PingResult = $"✗ Ping failed: {reply.Status}";
                StatusMessage = $"Ping failed: {reply.Status}";
                _logger.LogWarning("Ping failed with status: {Status}", reply.Status);
            }
        }
        catch (Exception ex)
        {
            PingResult = $"✗ Ping error: {ex.Message}";
            StatusMessage = $"Ping error: {ex.Message}";
            _logger.LogError(ex, "Failed to ping device at {IpAddress}", IpAddress);
        }
        finally
        {
            IsPingInProgress = false;
        }
    }

    private bool CanPing() => !IsPingInProgress && !string.IsNullOrWhiteSpace(IpAddress);

    /// <summary>
    /// Refresh device information manually
    /// </summary>
    [RelayCommand]
    private void Refresh()
    {
        if (_client != null)
        {
            LoadDeviceInfo(_client);
            StatusMessage = "Device information refreshed";
        }
    }

    /// <summary>
    /// Toggle auto-refresh
    /// </summary>
    [RelayCommand]
    private void ToggleAutoRefresh()
    {
        IsAutoRefreshEnabled = !IsAutoRefreshEnabled;

        if (IsAutoRefreshEnabled)
        {
            _refreshTimer.Start();
            StatusMessage = "Auto-refresh enabled (5 seconds)";
        }
        else
        {
            _refreshTimer.Stop();
            StatusMessage = "Auto-refresh disabled";
        }
    }

    /// <summary>
    /// Save changes to Group and Location
    /// </summary>
    [RelayCommand]
    private async Task SaveChanges()
    {
        if (_client == null)
        {
            StatusMessage = "Error: No client loaded";
            return;
        }

        try
        {
            StatusMessage = "Saving changes...";
            _logger.LogInformation("Saving changes for device {DeviceId}: Group={Group}, Location={Location}",
                _client.Id, Group, Location);

            // Update client via service
            var result = await _clientService.UpdateClientAsync(
                _client.Id,
                name: null, // Don't update name
                group: Group == "Not set" ? null : Group,
                location: Location == "Not set" ? null : Location);

            if (result.IsSuccess)
            {
                StatusMessage = "Changes saved successfully";
                _logger.LogInformation("Successfully saved changes for device {DeviceId}", _client.Id);

                // Update the client object
                _client.Group = Group == "Not set" ? null : Group;
                _client.Location = Location == "Not set" ? null : Location;
            }
            else
            {
                StatusMessage = $"Error: {result.Error}";
                _logger.LogError("Failed to save changes for device {DeviceId}: {Error}", _client.Id, result.Error);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            _logger.LogError(ex, "Exception while saving changes for device {DeviceId}", _client?.Id);
        }
    }

    /// <summary>
    /// Close the window
    /// </summary>
    [RelayCommand]
    private void Close()
    {
        _logger.LogDebug("Closing device detail window for {DeviceName}", DeviceName);
        _refreshTimer.Stop();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    partial void OnIsPingInProgressChanged(bool value)
    {
        PingDeviceCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        try
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
        }
        catch { }
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
