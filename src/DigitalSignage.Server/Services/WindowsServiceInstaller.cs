using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.ServiceProcess;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Service for managing Windows Service installation and control
/// </summary>
public class WindowsServiceInstaller
{
    private readonly ILogger<WindowsServiceInstaller> _logger;
    private const string ServiceName = "DigitalSignageServer";
    private const string ServiceDisplayName = "Digital Signage Server";
    private const string ServiceDescription = "Digital Signage Server - WebSocket communication and client management";

    public WindowsServiceInstaller(ILogger<WindowsServiceInstaller> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Check if the application is running as Administrator
    /// </summary>
    public bool IsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check administrator privileges");
            return false;
        }
    }

    /// <summary>
    /// Check if the service is installed
    /// </summary>
    public bool IsServiceInstalled()
    {
        try
        {
            using var controller = new ServiceController(ServiceName);
            // Try to access a property - if service doesn't exist, this will throw
            _ = controller.Status;
            return true;
        }
        catch (InvalidOperationException)
        {
            // Service doesn't exist
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if service is installed");
            return false;
        }
    }

    /// <summary>
    /// Get the current service status
    /// </summary>
    public ServiceControllerStatus? GetServiceStatus()
    {
        try
        {
            if (!IsServiceInstalled())
                return null;

            using var controller = new ServiceController(ServiceName);
            return controller.Status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get service status");
            return null;
        }
    }

    /// <summary>
    /// Install the Windows Service
    /// </summary>
    public (bool Success, string Message) InstallService()
    {
        try
        {
            if (!IsAdministrator())
            {
                return (false, "Administrator privileges required to install Windows Service.");
            }

            if (IsServiceInstalled())
            {
                return (false, "Service is already installed.");
            }

            _logger.LogInformation("Installing Windows Service...");

            // Get current executable path
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath))
            {
                return (false, "Failed to determine executable path.");
            }

            // Use sc.exe to create the service
            var startInfo = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"create {ServiceName} binPath= \"\\\"{exePath}\\\" --service\" " +
                           $"DisplayName= \"{ServiceDisplayName}\" " +
                           $"start= auto",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return (false, "Failed to start sc.exe process.");
            }

            process.WaitForExit();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            if (process.ExitCode != 0)
            {
                _logger.LogError("Failed to create service. Output: {Output}, Error: {Error}", output, error);
                return (false, $"Failed to create service: {error}");
            }

            // Set service description
            var descStartInfo = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"description {ServiceName} \"{ServiceDescription}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var descProcess = Process.Start(descStartInfo);
            descProcess?.WaitForExit();

            _logger.LogInformation("Windows Service installed successfully");
            return (true, "Windows Service installed successfully. The service is configured to start automatically on boot.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install Windows Service");
            return (false, $"Failed to install service: {ex.Message}");
        }
    }

    /// <summary>
    /// Uninstall the Windows Service
    /// </summary>
    public (bool Success, string Message) UninstallService()
    {
        try
        {
            if (!IsAdministrator())
            {
                return (false, "Administrator privileges required to uninstall Windows Service.");
            }

            if (!IsServiceInstalled())
            {
                return (false, "Service is not installed.");
            }

            _logger.LogInformation("Uninstalling Windows Service...");

            // Stop service first if running
            var status = GetServiceStatus();
            if (status == ServiceControllerStatus.Running || status == ServiceControllerStatus.StartPending)
            {
                var stopResult = StopService();
                if (!stopResult.Success)
                {
                    _logger.LogWarning("Failed to stop service before uninstall: {Message}", stopResult.Message);
                }
            }

            // Use sc.exe to delete the service
            var startInfo = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"delete {ServiceName}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return (false, "Failed to start sc.exe process.");
            }

            process.WaitForExit();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            if (process.ExitCode != 0)
            {
                _logger.LogError("Failed to delete service. Output: {Output}, Error: {Error}", output, error);
                return (false, $"Failed to delete service: {error}");
            }

            _logger.LogInformation("Windows Service uninstalled successfully");
            return (true, "Windows Service uninstalled successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to uninstall Windows Service");
            return (false, $"Failed to uninstall service: {ex.Message}");
        }
    }

    /// <summary>
    /// Start the Windows Service
    /// </summary>
    public (bool Success, string Message) StartService()
    {
        try
        {
            if (!IsAdministrator())
            {
                return (false, "Administrator privileges required to start Windows Service.");
            }

            if (!IsServiceInstalled())
            {
                return (false, "Service is not installed.");
            }

            _logger.LogInformation("Starting Windows Service...");

            using var controller = new ServiceController(ServiceName);

            if (controller.Status == ServiceControllerStatus.Running)
            {
                return (true, "Service is already running.");
            }

            if (controller.Status == ServiceControllerStatus.StartPending)
            {
                return (true, "Service is already starting.");
            }

            controller.Start();
            controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));

            _logger.LogInformation("Windows Service started successfully");
            return (true, "Windows Service started successfully.");
        }
        catch (System.ServiceProcess.TimeoutException)
        {
            _logger.LogError("Service start timed out after 30 seconds");
            return (false, "Service start timed out. Check Windows Event Log for details.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Windows Service");
            return (false, $"Failed to start service: {ex.Message}");
        }
    }

    /// <summary>
    /// Stop the Windows Service
    /// </summary>
    public (bool Success, string Message) StopService()
    {
        try
        {
            if (!IsAdministrator())
            {
                return (false, "Administrator privileges required to stop Windows Service.");
            }

            if (!IsServiceInstalled())
            {
                return (false, "Service is not installed.");
            }

            _logger.LogInformation("Stopping Windows Service...");

            using var controller = new ServiceController(ServiceName);

            if (controller.Status == ServiceControllerStatus.Stopped)
            {
                return (true, "Service is already stopped.");
            }

            if (controller.Status == ServiceControllerStatus.StopPending)
            {
                return (true, "Service is already stopping.");
            }

            controller.Stop();
            controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));

            _logger.LogInformation("Windows Service stopped successfully");
            return (true, "Windows Service stopped successfully.");
        }
        catch (System.ServiceProcess.TimeoutException)
        {
            _logger.LogError("Service stop timed out after 30 seconds");
            return (false, "Service stop timed out. Check Windows Event Log for details.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop Windows Service");
            return (false, $"Failed to stop service: {ex.Message}");
        }
    }

    /// <summary>
    /// Get a user-friendly status string
    /// </summary>
    public string GetServiceStatusString()
    {
        var status = GetServiceStatus();
        if (status == null)
        {
            return "Not Installed";
        }

        return status.Value switch
        {
            ServiceControllerStatus.Running => "Running",
            ServiceControllerStatus.Stopped => "Stopped",
            ServiceControllerStatus.Paused => "Paused",
            ServiceControllerStatus.StartPending => "Starting...",
            ServiceControllerStatus.StopPending => "Stopping...",
            ServiceControllerStatus.ContinuePending => "Resuming...",
            ServiceControllerStatus.PausePending => "Pausing...",
            _ => "Unknown"
        };
    }
}
