using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text.RegularExpressions;
using DigitalSignage.Server.Configuration;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Service for managing Windows SSL/TLS port bindings via netsh
/// WINDOWS-ONLY: Uses netsh http commands to configure SSL certificate bindings
/// </summary>
public class SslBindingService : ISslBindingService
{
    private readonly ILogger<SslBindingService> _logger;
    private readonly ServerSettings _settings;

    public SslBindingService(
        ILogger<SslBindingService> logger,
        ServerSettings settings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Ensure SSL binding exists for the certificate and port
    /// </summary>
    public async Task<bool> EnsureSslBindingAsync(X509Certificate2 certificate, int port)
    {
        if (certificate == null)
            throw new ArgumentNullException(nameof(certificate));

        if (!OperatingSystem.IsWindows())
        {
            _logger.LogWarning("SSL binding configuration is only supported on Windows");
            return false;
        }

        if (!IsRunningAsAdministrator())
        {
            _logger.LogWarning("Cannot configure SSL binding - Administrator privileges required");
            _logger.LogWarning("To configure SSL binding manually, run as Administrator:");
            _logger.LogWarning($"  netsh http add sslcert ipport=0.0.0.0:{port} certhash={certificate.Thumbprint} appid={{{_settings.SslAppId}}}");
            return false;
        }

        try
        {
            _logger.LogInformation("Configuring SSL binding for port {Port}...", port);
            _logger.LogDebug("Certificate Thumbprint: {Thumbprint}", certificate.Thumbprint);
            _logger.LogDebug("App ID: {{{AppId}}}", _settings.SslAppId);

            // Check if binding already exists
            var existingThumbprint = await GetBoundCertificateThumbprintAsync(port);

            if (existingThumbprint != null)
            {
                if (existingThumbprint.Equals(certificate.Thumbprint, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("SSL binding already exists with correct certificate");
                    return true;
                }
                else
                {
                    _logger.LogInformation("SSL binding exists with different certificate, updating...");
                    _logger.LogDebug("Old thumbprint: {OldThumbprint}", existingThumbprint);
                    _logger.LogDebug("New thumbprint: {NewThumbprint}", certificate.Thumbprint);

                    // Remove old binding
                    if (!await RemoveSslBindingAsync(port))
                    {
                        _logger.LogError("Failed to remove old SSL binding");
                        return false;
                    }
                }
            }

            // Add new SSL binding
            var addResult = await RunNetshCommandAsync(
                $"http add sslcert ipport=0.0.0.0:{port} certhash={certificate.Thumbprint} appid={{{_settings.SslAppId}}}");

            if (!addResult.Success)
            {
                _logger.LogError("Failed to add SSL binding: {Error}", addResult.Error);
                _logger.LogError("netsh output: {Output}", addResult.Output);
                return false;
            }

            _logger.LogInformation("SSL binding configured successfully for port {Port}", port);
            _logger.LogDebug("netsh output: {Output}", addResult.Output);

            // Optionally add URL ACL if configured
            if (_settings.AutoConfigureUrlAcl)
            {
                var protocol = _settings.EnableSsl ? "https" : "http";
                var url = $"{protocol}://+:{port}{_settings.EndpointPath}";
                await AddUrlAclAsync(url);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error configuring SSL binding for port {Port}", port);
            return false;
        }
    }

    /// <summary>
    /// Remove SSL binding from a port
    /// </summary>
    public async Task<bool> RemoveSslBindingAsync(int port)
    {
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogWarning("SSL binding configuration is only supported on Windows");
            return false;
        }

        if (!IsRunningAsAdministrator())
        {
            _logger.LogWarning("Cannot remove SSL binding - Administrator privileges required");
            return false;
        }

        try
        {
            _logger.LogInformation("Removing SSL binding from port {Port}...", port);

            var result = await RunNetshCommandAsync($"http delete sslcert ipport=0.0.0.0:{port}");

            if (!result.Success)
            {
                // Check if error is "The system cannot find the file specified" (binding doesn't exist)
                if (result.Error?.Contains("cannot find") == true || result.Output?.Contains("cannot find") == true)
                {
                    _logger.LogDebug("SSL binding does not exist on port {Port}", port);
                    return true; // Not an error if binding doesn't exist
                }

                _logger.LogWarning("Failed to remove SSL binding: {Error}", result.Error);
                return false;
            }

            _logger.LogInformation("SSL binding removed successfully from port {Port}", port);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing SSL binding from port {Port}", port);
            return false;
        }
    }

    /// <summary>
    /// Check if running as administrator
    /// </summary>
    public bool IsRunningAsAdministrator()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check administrator status");
            return false;
        }
    }

    /// <summary>
    /// Add URL ACL reservation
    /// </summary>
    public async Task<bool> AddUrlAclAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL cannot be null or empty", nameof(url));

        if (!OperatingSystem.IsWindows())
        {
            _logger.LogWarning("URL ACL configuration is only supported on Windows");
            return false;
        }

        if (!IsRunningAsAdministrator())
        {
            _logger.LogWarning("Cannot configure URL ACL - Administrator privileges required");
            _logger.LogWarning("To configure URL ACL manually, run as Administrator:");
            _logger.LogWarning($"  netsh http add urlacl url={url} user=Everyone");
            return false;
        }

        try
        {
            _logger.LogInformation("Configuring URL ACL for {Url}...", url);

            // Check if URL ACL already exists
            var showResult = await RunNetshCommandAsync($"http show urlacl url={url}");

            if (showResult.Success && showResult.Output?.Contains("Reserved URL") == true)
            {
                _logger.LogDebug("URL ACL already exists for {Url}", url);
                return true;
            }

            // Add URL ACL
            var addResult = await RunNetshCommandAsync($"http add urlacl url={url} user=Everyone");

            if (!addResult.Success)
            {
                _logger.LogWarning("Failed to add URL ACL: {Error}", addResult.Error);
                return false;
            }

            _logger.LogInformation("URL ACL configured successfully for {Url}", url);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error configuring URL ACL for {Url}", url);
            return false;
        }
    }

    /// <summary>
    /// Check if SSL binding exists for a port
    /// </summary>
    public async Task<bool> SslBindingExistsAsync(int port)
    {
        var thumbprint = await GetBoundCertificateThumbprintAsync(port);
        return thumbprint != null;
    }

    /// <summary>
    /// Get the certificate thumbprint for an existing SSL binding
    /// </summary>
    public async Task<string?> GetBoundCertificateThumbprintAsync(int port)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        try
        {
            var result = await RunNetshCommandAsync($"http show sslcert ipport=0.0.0.0:{port}");

            if (!result.Success || string.IsNullOrWhiteSpace(result.Output))
                return null;

            // Parse certificate hash from output
            // Expected format: "Certificate Hash             : ABCDEF123456..."
            var match = Regex.Match(result.Output, @"Certificate Hash\s*:\s*([0-9a-fA-F]+)", RegexOptions.IgnoreCase);

            if (match.Success && match.Groups.Count > 1)
            {
                var thumbprint = match.Groups[1].Value.Trim();
                _logger.LogDebug("Found SSL binding on port {Port} with thumbprint {Thumbprint}", port, thumbprint);
                return thumbprint;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking SSL binding for port {Port}", port);
            return null;
        }
    }

    /// <summary>
    /// Run netsh command and capture output
    /// </summary>
    private async Task<NetshResult> RunNetshCommandAsync(string arguments)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new NetshResult
            {
                Success = false,
                Error = "netsh is only available on Windows"
            };
        }

        try
        {
            _logger.LogDebug("Executing: netsh {Arguments}", arguments);

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "netsh.exe",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Environment.SystemDirectory
            };

            using var process = new Process { StartInfo = processStartInfo };

            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    outputBuilder.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    errorBuilder.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait for process to complete (max 30 seconds)
            await process.WaitForExitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token);

            var output = outputBuilder.ToString();
            var error = errorBuilder.ToString();

            var success = process.ExitCode == 0;

            if (!success)
            {
                _logger.LogDebug("netsh command failed with exit code {ExitCode}", process.ExitCode);
            }

            return new NetshResult
            {
                Success = success,
                Output = output,
                Error = error,
                ExitCode = process.ExitCode
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing netsh command: {Arguments}", arguments);
            return new NetshResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Result of netsh command execution
    /// </summary>
    private class NetshResult
    {
        public bool Success { get; set; }
        public string? Output { get; set; }
        public string? Error { get; set; }
        public int ExitCode { get; set; }
    }
}
