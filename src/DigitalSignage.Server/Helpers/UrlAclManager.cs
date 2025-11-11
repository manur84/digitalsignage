using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using Serilog;

namespace DigitalSignage.Server.Helpers;

/// <summary>
/// Manages URL ACL configuration for HTTP.sys listener
/// Provides automatic elevation and configuration for first-run setup
/// </summary>
public static class UrlAclManager
{
    /// <summary>
    /// Checks if URL ACL is configured for the specified port
    /// </summary>
    /// <param name="port">Port number to check</param>
    /// <returns>True if URL ACL is configured, false otherwise</returns>
    public static bool IsUrlAclConfigured(int port)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "http show urlacl",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            // Check if our port is registered
            return output.Contains($":{port}/ws/") || output.Contains($":{port}/");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to check URL ACL configuration");
            return false;
        }
    }

    /// <summary>
    /// Checks if current process is running with administrator privileges
    /// </summary>
    /// <returns>True if running as administrator, false otherwise</returns>
    public static bool IsRunningAsAdministrator()
    {
        try
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Configures URL ACL for the specified port (requires admin privileges)
    /// </summary>
    /// <param name="port">Port number to configure</param>
    /// <returns>True if configuration was successful, false otherwise</returns>
    public static bool ConfigureUrlAcl(int port)
    {
        if (!IsRunningAsAdministrator())
        {
            Log.Error("Cannot configure URL ACL - not running as administrator");
            return false;
        }

        try
        {
            var urls = new[]
            {
                $"http://+:{port}/ws/",
                $"http://+:{port}/"
            };

            foreach (var url in urls)
            {
                Log.Information($"Configuring URL ACL for: {url}");

                // Try to delete existing (ignore errors)
                RunNetshCommand($"http delete urlacl url={url}");

                // Add new URL ACL
                var result = RunNetshCommand($"http add urlacl url={url} user=Everyone");

                if (!result)
                {
                    Log.Error($"Failed to configure URL ACL for {url}");
                    return false;
                }

                Log.Information($"Successfully configured URL ACL for {url}");
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to configure URL ACL");
            return false;
        }
    }

    /// <summary>
    /// Restarts the application with administrator privileges
    /// </summary>
    /// <param name="arguments">Command line arguments to pass to elevated instance</param>
    /// <returns>True if restart was initiated, false if it failed</returns>
    public static bool RestartAsAdministrator(string arguments = "")
    {
        try
        {
            var exePath = Environment.ProcessPath ??
                         Process.GetCurrentProcess().MainModule?.FileName;

            if (string.IsNullOrEmpty(exePath))
            {
                Log.Error("Cannot determine executable path for elevation");
                return false;
            }

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas" // This triggers UAC prompt
            };

            Log.Information("Restarting application with administrator privileges...");
            Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to restart as administrator");
            return false;
        }
    }

    /// <summary>
    /// Executes a netsh command and returns success status
    /// </summary>
    private static bool RunNetshCommand(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
