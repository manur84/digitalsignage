using System.Diagnostics;
using System.IO;
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
    public async Task<bool> EnsureSslBindingAsync(X509Certificate2 certificate, int port, string pfxPath, string password)
    {
        if (certificate == null)
            throw new ArgumentNullException(nameof(certificate));

        if (string.IsNullOrWhiteSpace(pfxPath))
            throw new ArgumentException("PFX path is required for PowerShell fallback", nameof(pfxPath));

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

            // CRITICAL FIX: Import certificate to Windows Certificate Store BEFORE netsh binding
            // Windows Error 1312 means certificate is not found in LocalMachine\My store
            // netsh can only bind certificates that exist in the certificate store
            _logger.LogInformation("Importing certificate to Windows Certificate Store (LocalMachine\\My)...");
            if (!await ImportCertificateToStoreAsync(certificate, pfxPath, password))
            {
                _logger.LogError("Failed to import certificate to Windows store - cannot proceed with SSL binding");
                _logger.LogError("Manual fix required:");
                _logger.LogError("  1. Open certlm.msc (Certificate Manager - Local Computer)");
                _logger.LogError("  2. Navigate to Personal > Certificates");
                _logger.LogError("  3. Import the certificate file from: {0}", pfxPath);
                _logger.LogError("  4. Restart the server application");
                return false;
            }

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
                // Check for specific error codes
                if (addResult.Output?.Contains("1312") == true || addResult.Error?.Contains("1312") == true)
                {
                    _logger.LogError("SSL Binding Error 1312 - Certificate not found in Windows Certificate Store");
                    _logger.LogError("The certificate was imported but may not be accessible to netsh");
                    _logger.LogError("This can happen if:");
                    _logger.LogError("  1. Certificate import failed silently");
                    _logger.LogError("  2. Certificate is in wrong store location");
                    _logger.LogError("  3. Certificate lacks private key");
                    _logger.LogError("");
                    _logger.LogError("Manual troubleshooting steps:");
                    _logger.LogError("  1. Open PowerShell as Administrator");
                    _logger.LogError("  2. Run: Get-ChildItem -Path Cert:\\LocalMachine\\My");
                    _logger.LogError("  3. Verify certificate with thumbprint {0} exists", certificate.Thumbprint);
                    _logger.LogError("  4. If missing, import manually:");
                    _logger.LogError("     Import-PfxCertificate -FilePath \"certs\\digitalsignage.pfx\" -CertStoreLocation Cert:\\LocalMachine\\My -Exportable");
                    _logger.LogError("  5. Then bind manually:");
                    _logger.LogError("     netsh http add sslcert ipport=0.0.0.0:{0} certhash={1} appid={{{2}}}", port, certificate.Thumbprint, _settings.SslAppId);
                }
                else
                {
                    _logger.LogError("Failed to add SSL binding: {Error}", addResult.Error);
                    _logger.LogError("netsh output: {Output}", addResult.Output);
                }
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
    /// Import certificate to Windows Certificate Store (LocalMachine\My)
    /// Required before netsh can bind the certificate to a port
    /// </summary>
    private async Task<bool> ImportCertificateToStoreAsync(X509Certificate2 certificate, string pfxPath, string password)
    {
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogWarning("Certificate store import is only supported on Windows");
            return false;
        }

        try
        {
            // 1. Validate certificate has private key
            if (!certificate.HasPrivateKey)
            {
                _logger.LogError("Certificate does not have a private key - cannot be used for SSL binding");
                _logger.LogError("The certificate must be created with a private key (e.g., from PFX file)");
                return false;
            }

            _logger.LogInformation(
                "Importing certificate to LocalMachine\\My store (Thumbprint: {Thumbprint})",
                certificate.Thumbprint);

            // 2. Try standard .NET X509Store import first
            using (var store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
            {
                store.Open(OpenFlags.ReadWrite);

                try
                {
                    // Check if certificate already exists
                    var existing = store.Certificates.Find(
                        X509FindType.FindByThumbprint,
                        certificate.Thumbprint,
                        validOnly: false
                    );

                    if (existing.Count > 0)
                    {
                        _logger.LogInformation("Certificate already exists in store");

                        // Verify private key is accessible
                        var existingCert = existing[0];
                        if (existingCert.HasPrivateKey)
                        {
                            _logger.LogInformation("Existing certificate has private key - OK");
                            return true;
                        }
                        else
                        {
                            _logger.LogWarning("Existing certificate MISSING private key - removing and re-importing");
                            store.Remove(existingCert);
                        }
                    }

                    // Import certificate
                    store.Add(certificate);
                    _logger.LogInformation("Certificate added to store via X509Store API");
                }
                finally
                {
                    store.Close();
                }
            }

            // 3. Verify import with delay for Windows Store sync
            await Task.Delay(500);

            using (var store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
            {
                store.Open(OpenFlags.ReadOnly);
                var verification = store.Certificates.Find(
                    X509FindType.FindByThumbprint,
                    certificate.Thumbprint,
                    validOnly: false
                );
                store.Close();

                if (verification.Count == 0)
                {
                    _logger.LogError("Certificate not found after .NET import - trying PowerShell fallback");
                    return await ImportCertificateViaPowerShellAsync(pfxPath, password, certificate.Thumbprint);
                }

                if (!verification[0].HasPrivateKey)
                {
                    _logger.LogError("Certificate imported but private key missing - trying PowerShell fallback");
                    return await ImportCertificateViaPowerShellAsync(pfxPath, password, certificate.Thumbprint);
                }

                _logger.LogInformation("Certificate successfully imported with private key");
                _logger.LogDebug("  Subject: {Subject}", verification[0].Subject);
                _logger.LogDebug("  Issuer: {Issuer}", verification[0].Issuer);
                _logger.LogDebug("  Has Private Key: {HasPrivateKey}", verification[0].HasPrivateKey);
                return true;
            }
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            _logger.LogError(ex, "Cryptographic error importing certificate - trying PowerShell fallback");
            return await ImportCertificateViaPowerShellAsync(pfxPath, password, certificate.Thumbprint);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied importing certificate to Windows store");
            _logger.LogError("This requires Administrator privileges");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error importing certificate to Windows store");
            return false;
        }
    }

    /// <summary>
    /// Import certificate via PowerShell Import-PfxCertificate cmdlet
    /// This is a fallback method when X509Store.Add() fails to properly persist the private key
    /// </summary>
    private async Task<bool> ImportCertificateViaPowerShellAsync(string pfxPath, string password, string thumbprint)
    {
        try
        {
            _logger.LogInformation("Attempting certificate import via PowerShell...");

            if (!File.Exists(pfxPath))
            {
                _logger.LogError("PFX file not found: {Path}", pfxPath);
                return false;
            }

            // Escape single quotes in password and path for PowerShell
            var escapedPassword = password.Replace("'", "''");
            var escapedPath = pfxPath.Replace("'", "''");

            var psCommand = $@"
                $ErrorActionPreference = 'Stop'
                try {{
                    $pwd = ConvertTo-SecureString -String '{escapedPassword}' -Force -AsPlainText
                    $cert = Import-PfxCertificate -FilePath '{escapedPath}' -CertStoreLocation Cert:\LocalMachine\My -Password $pwd -Exportable
                    if ($cert) {{
                        Write-Output ""Certificate imported: $($cert.Thumbprint)""
                        $verifyThumbprint = '{thumbprint}'
                        if ($cert.Thumbprint -eq $verifyThumbprint) {{
                            Write-Output 'SUCCESS'
                        }} else {{
                            Write-Output ""ERROR: Thumbprint mismatch. Expected: $verifyThumbprint, Got: $($cert.Thumbprint)""
                        }}
                    }} else {{
                        Write-Output 'ERROR: Import-PfxCertificate returned null'
                    }}
                }}
                catch {{
                    Write-Output ""ERROR: $_""
                }}
            ";

            var processInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCommand}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                _logger.LogError("Failed to start PowerShell process");
                return false;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            _logger.LogDebug("PowerShell output: {Output}", output);
            if (!string.IsNullOrWhiteSpace(error))
            {
                _logger.LogDebug("PowerShell stderr: {Error}", error);
            }

            if (output.Contains("SUCCESS"))
            {
                _logger.LogInformation("Certificate imported successfully via PowerShell");
                return true;
            }

            _logger.LogError("PowerShell import failed");
            _logger.LogError("Output: {Output}", output);
            if (!string.IsNullOrWhiteSpace(error))
            {
                _logger.LogError("Error: {Error}", error);
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during PowerShell certificate import");
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
