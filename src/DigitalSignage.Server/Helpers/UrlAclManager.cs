using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.ServiceProcess;
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
        Console.WriteLine("====================================");
        Console.WriteLine("URL ACL Configuration Starting");
        Console.WriteLine("====================================");
        Console.WriteLine($"Port: {port}");
        Console.WriteLine($"Running as admin: {IsRunningAsAdministrator()}");
        Console.WriteLine($"User: {Environment.UserName}");
        Console.WriteLine($"Machine: {Environment.MachineName}");
        Console.WriteLine($"OS: {Environment.OSVersion}");
        Console.WriteLine($".NET Version: {Environment.Version}");
        Console.WriteLine("====================================");

        if (!IsRunningAsAdministrator())
        {
            Console.WriteLine("[ERROR] Not running as administrator!");
            Log.Error("Cannot configure URL ACL - not running as administrator");
            return false;
        }

        try
        {
            // Check HTTP.sys service
            Console.WriteLine("\nStep 0: Checking HTTP.sys service...");
            Console.WriteLine("------------------------------------");
            EnsureHttpSysRunning();
            Console.WriteLine();

            var urls = new[]
            {
                $"http://+:{port}/ws/",
                $"http://+:{port}/"
            };

            foreach (var url in urls)
            {
                Console.WriteLine($"Configuring: {url}");
                Console.WriteLine("------------------------------------");

                // Step 1: Check if already exists
                Console.WriteLine("Step 1: Checking existing configuration...");
                var checkResult = RunNetshCommand($"http show urlacl url={url}");

                // Step 2: Try to delete (ignore errors if not exists)
                Console.WriteLine("Step 2: Removing old configuration (if exists)...");
                RunNetshCommand($"http delete urlacl url={url}");

                // Step 3: Add new URL ACL
                Console.WriteLine("Step 3: Adding new URL ACL...");
                // Use SID S-1-1-0 which is "Everyone" on all Windows language versions (Jeder, Tout le monde, etc.)
                var addResult = RunNetshCommand($"http add urlacl url={url} sddl=D:(A;;GX;;;S-1-1-0)");

                if (!addResult)
                {
                    Console.WriteLine($"[FAILED] Could not configure {url}");
                    Console.WriteLine("\nTroubleshooting:");
                    Console.WriteLine("1. Are you REALLY running as Administrator?");
                    Console.WriteLine($"2. Try running manually: netsh http add urlacl url={url} sddl=D:(A;;GX;;;S-1-1-0)");
                    Console.WriteLine("3. Check Windows Event Viewer for errors");
                    Console.WriteLine("4. Run setup-urlacl.bat manually");
                    Console.WriteLine("5. Check if HTTP.sys service is running: sc query HTTP");
                    Console.WriteLine("6. Try rebooting Windows and run as admin again");
                    Log.Error($"Failed to configure URL ACL for {url}");
                    return false;
                }

                Console.WriteLine($"[SUCCESS] {url} configured");
                Console.WriteLine();
                Log.Information($"Successfully configured URL ACL for {url}");
            }

            Console.WriteLine("====================================");
            Console.WriteLine("URL ACL Configuration COMPLETED");
            Console.WriteLine("====================================");

            // Verify it worked
            Console.WriteLine("\nVerification:");
            Console.WriteLine("------------------------------------");
            RunNetshCommand("http show urlacl");
            Console.WriteLine();

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[FATAL ERROR] {ex.Message}");
            Console.WriteLine($"Exception Type: {ex.GetType().Name}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            Log.Error(ex, "Fatal error during URL ACL configuration");
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
    /// Checks if HTTP.sys service is running
    /// </summary>
    /// <returns>True if HTTP.sys is running, false otherwise</returns>
    public static bool IsHttpSysRunning()
    {
        try
        {
            using var sc = new ServiceController("HTTP");
            return sc.Status == ServiceControllerStatus.Running;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Attempts to start HTTP.sys service if not running
    /// </summary>
    /// <returns>True if HTTP.sys is running after this call, false otherwise</returns>
    public static bool EnsureHttpSysRunning()
    {
        try
        {
            using var sc = new ServiceController("HTTP");

            if (sc.Status == ServiceControllerStatus.Running)
            {
                Console.WriteLine("[OK] HTTP.sys service is already running");
                return true;
            }

            Console.WriteLine($"[WARNING] HTTP.sys service status: {sc.Status}");
            Console.WriteLine("[INFO] Attempting to start HTTP.sys service...");

            sc.Start();
            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));

            Console.WriteLine("[SUCCESS] HTTP.sys service started");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Could not start HTTP.sys service: {ex.Message}");
            Console.WriteLine("[INFO] URL ACL configuration may fail without HTTP.sys running");
            return false;
        }
    }

    /// <summary>
    /// Executes a netsh command and returns success status with detailed logging
    /// </summary>
    private static bool RunNetshCommand(string arguments)
    {
        try
        {
            Console.WriteLine($"[DEBUG] Executing: netsh {arguments}");

            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Verb = string.Empty // Important: no elevation verb here
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                Console.WriteLine("[ERROR] Failed to start netsh process");
                Log.Error("Failed to start netsh process");
                return false;
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Console.WriteLine($"[DEBUG] Exit code: {process.ExitCode}");

            if (!string.IsNullOrWhiteSpace(output))
            {
                Console.WriteLine($"[DEBUG] Output: {output.Trim()}");
                Log.Debug($"netsh output: {output.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                Console.WriteLine($"[DEBUG] Error: {error.Trim()}");
                Log.Warning($"netsh error: {error.Trim()}");
            }

            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Exception running netsh: {ex.Message}");
            Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
            Log.Error(ex, "Exception running netsh command");
            return false;
        }
    }
}
