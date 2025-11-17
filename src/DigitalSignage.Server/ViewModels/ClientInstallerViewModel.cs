using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Server.Services;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Text;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// ViewModel for installing the Raspberry Pi client on a discovered device over SSH.
/// </summary>
public partial class ClientInstallerViewModel : ObservableObject, IDisposable
{
    private readonly RemoteClientInstallerService _installerService;
    private readonly ILogger<ClientInstallerViewModel> _logger;
    private bool _disposed;

    public DiscoveredDevicesViewModel? DiscoveredDevices { get; private set; }

    [ObservableProperty]
    private string _sshUsername = "pi";

    [ObservableProperty]
    private string _sshPassword = string.Empty;

    [ObservableProperty]
    private int _sshPort = 22;

    [ObservableProperty]
    private string _manualTargetIp = string.Empty;

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private string _statusMessage = "Bereit zum Installieren";

    [ObservableProperty]
    private string _logOutput = string.Empty;

    public ClientInstallerViewModel(RemoteClientInstallerService installerService, ILogger<ClientInstallerViewModel> logger)
    {
        _installerService = installerService ?? throw new ArgumentNullException(nameof(installerService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Injects the shared discovered devices ViewModel so we can reuse the scan and selection state.
    /// </summary>
    public void Initialize(DiscoveredDevicesViewModel discoveredDevicesViewModel)
    {
        DiscoveredDevices = discoveredDevicesViewModel ?? throw new ArgumentNullException(nameof(discoveredDevicesViewModel));
        DiscoveredDevices.PropertyChanged += OnDiscoveredDevicesPropertyChanged;
        InstallSelectedDeviceCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Runs install.sh on the selected discovered device.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task InstallSelectedDevice()
    {
        if (DiscoveredDevices?.SelectedDiscoveredDevice == null)
        {
            if (string.IsNullOrWhiteSpace(ManualTargetIp))
                return;
        }

        var targetDevice = DiscoveredDevices?.SelectedDiscoveredDevice;
        var targetIp = targetDevice?.IpAddress ?? ManualTargetIp.Trim();
        var targetName = targetDevice?.Hostname ?? targetIp;

        try
        {
            IsInstalling = true;
            StatusMessage = $"Installiere auf {targetIp} ...";
            AppendLog($"Verbinde mit {targetName} ({targetIp}) als {SshUsername}.");

            var progress = new Progress<string>(message => AppendLog(message));
            var result = await _installerService.InstallAsync(targetIp, SshPort, SshUsername, SshPassword, progress);

            if (result.IsSuccess)
            {
                StatusMessage = result.SuccessMessage ?? "Installation abgeschlossen";
                AppendLog(result.SuccessMessage ?? "Installation abgeschlossen.");
            }
            else
            {
                StatusMessage = result.ErrorMessage ?? "Installation fehlgeschlagen";
                AppendLog($"FEHLER: {result.ErrorMessage}");

                if (result.Exception != null)
                {
                    AppendLog(result.Exception.Message);
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Installation fehlgeschlagen";
            _logger.LogError(ex, "Installation failed for device {Ip}", targetIp);
            AppendLog($"FEHLER: {ex.Message}");
        }
        finally
        {
            IsInstalling = false;
        }
    }

    private bool CanInstall()
    {
        return !IsInstalling
            && !string.IsNullOrWhiteSpace(SshUsername)
            && !string.IsNullOrWhiteSpace(SshPassword)
            && SshPort > 0
            && (DiscoveredDevices?.SelectedDiscoveredDevice != null || !string.IsNullOrWhiteSpace(ManualTargetIp));
    }

    private void AppendLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var entry = $"[{timestamp}] {message}";

        // Newest first in the log output
        if (string.IsNullOrWhiteSpace(LogOutput))
        {
            LogOutput = entry;
        }
        else
        {
            var builder = new StringBuilder(entry.Length + LogOutput.Length + Environment.NewLine.Length);
            builder.Append(entry);
            builder.AppendLine();
            builder.Append(LogOutput);
            LogOutput = builder.ToString();
        }
    }

    partial void OnSshUsernameChanged(string value) => InstallSelectedDeviceCommand.NotifyCanExecuteChanged();
    partial void OnSshPasswordChanged(string value) => InstallSelectedDeviceCommand.NotifyCanExecuteChanged();
    partial void OnSshPortChanged(int value) => InstallSelectedDeviceCommand.NotifyCanExecuteChanged();
    partial void OnManualTargetIpChanged(string value) => InstallSelectedDeviceCommand.NotifyCanExecuteChanged();

    private void OnDiscoveredDevicesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DiscoveredDevicesViewModel.SelectedDiscoveredDevice))
        {
            InstallSelectedDeviceCommand.NotifyCanExecuteChanged();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing && DiscoveredDevices != null)
        {
            DiscoveredDevices.PropertyChanged -= OnDiscoveredDevicesPropertyChanged;
        }

        _disposed = true;
    }
}
