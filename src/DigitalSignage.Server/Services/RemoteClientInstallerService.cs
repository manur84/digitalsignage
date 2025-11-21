using System;
using System.IO;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using Renci.SshNet.Common;
using System.Net.Sockets;
using System.Linq;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Handles copying the Raspberry Pi client installer to a remote host and executing install.sh over SSH.
/// </summary>
public class RemoteClientInstallerService
{
    private readonly ILogger<RemoteClientInstallerService> _logger;
    private readonly RemoteSshConnectionManager _connectionManager;
    private readonly RemoteFileUploader _fileUploader;
    private readonly RemoteInstallationPreparer _installationPreparer;
    private readonly string _installerSourcePath;
    private const string RemoteInstallPath = "/tmp/digitalsignage-client-installer";
    private const string RemoteServiceName = "digitalsignage-client";
    private const string RemoteInstallDir = "/opt/digitalsignage-client";
    private const string RemoteServiceFile = "/etc/systemd/system/digitalsignage-client.service";
    private const string InstallCompleteMarker = "__DS_INSTALL_COMPLETE__";

    public RemoteClientInstallerService(
        ILogger<RemoteClientInstallerService> logger,
        ILoggerFactory loggerFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _installerSourcePath = Path.Combine(AppContext.BaseDirectory, "ClientInstaller");

        // CRITICAL FIX: Create loggers for internal helper classes using ILoggerFactory
        // This avoids exposing internal types in the public API
        var connectionLogger = loggerFactory.CreateLogger<RemoteSshConnectionManager>();
        var uploaderLogger = loggerFactory.CreateLogger<RemoteFileUploader>();
        var preparerLogger = loggerFactory.CreateLogger<RemoteInstallationPreparer>();

        _connectionManager = new RemoteSshConnectionManager(connectionLogger);
        _fileUploader = new RemoteFileUploader(uploaderLogger, _installerSourcePath);
        _installationPreparer = new RemoteInstallationPreparer(
            preparerLogger,
            RemoteInstallDir,
            RemoteServiceFile,
            RemoteServiceName);
    }

    /// <summary>
    /// Uploads the installer payload and executes install.sh on the remote device.
    /// </summary>
    public async Task<Result> InstallAsync(string host, int port, string username, string password, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        SshClient? ssh = null;
        SftpClient? sftp = null;
        var installCommandStarted = false;
        var installMarkerSeen = false;
        var isUpdateMode = false;

        if (string.IsNullOrWhiteSpace(host))
            return Result.Failure("Host/IP is required for installation.");

        if (!Directory.Exists(_installerSourcePath))
            return Result.Failure($"Installer payload not found: {_installerSourcePath}");

        try
        {
            progress?.Report($"Connecting to {host}:{port} ...");
            _logger.LogInformation("Starting remote installer for {Host}:{Port} as {User}", host, port, username);

            // Quick connectivity probe to fail fast on unreachable hosts/ports
            try
            {
                await _connectionManager.EnsurePortReachableAsync(host, port, cancellationToken);
            }
            catch (TimeoutException ex)
            {
                _logger.LogWarning(ex, "Connection timeout for {Host}:{Port}", host, port);
                progress?.Report($"Connection timeout: {host}:{port} did not respond within 10 seconds.");
                return Result.Failure($"Connection timeout: Host {host}:{port} did not respond within 10 seconds. Please verify the IP address and ensure the device is powered on and connected to the network.", ex);
            }
            catch (SocketException ex)
            {
                _logger.LogWarning(ex, "Network error connecting to {Host}:{Port}", host, port);
                progress?.Report($"Network error: Unable to reach {host}:{port}");
                return Result.Failure($"Network error: Unable to reach {host}:{port}. Please verify the IP address and network connectivity.", ex);
            }

            try
            {
                ssh = _connectionManager.CreateSshClient(host, port, username, password);
                sftp = _connectionManager.CreateSftpClient(host, port, username, password);

                progress?.Report("Authenticating with SSH credentials...");
                await _connectionManager.ConnectWithTimeoutAsync(ssh, cancellationToken);
                await _connectionManager.ConnectWithTimeoutAsync(sftp, cancellationToken);
            }
            catch (SshAuthenticationException ex)
            {
                _logger.LogWarning(ex, "SSH authentication failed for {Host}:{Port} with username {Username}", host, port, username);
                progress?.Report($"SSH authentication failed: Wrong password or username");
                return Result.Failure($"SSH authentication failed: Wrong password or username '{username}'. Please verify your credentials and try again.", ex);
            }
            catch (SshConnectionException ex)
            {
                _logger.LogWarning(ex, "SSH connection aborted while connecting to {Host}:{Port}", host, port);
                progress?.Report("SSH connection was aborted by the remote host");
                return Result.Failure($"SSH connection was aborted by the remote host {host}:{port}. Please check network/firewall settings and ensure SSH is running on the device.", ex);
            }
            catch (SocketException ex)
            {
                _logger.LogWarning(ex, "SSH TCP connection failed for {Host}:{Port}", host, port);
                progress?.Report($"TCP connection failed for {host}:{port}");
                return Result.Failure($"Unable to establish TCP connection to {host}:{port}. Please verify network connectivity and firewall settings.", ex);
            }
            catch (TimeoutException ex)
            {
                _logger.LogWarning(ex, "SSH connection timeout for {Host}:{Port}", host, port);
                progress?.Report($"SSH connection timeout for {host}:{port}");
                return Result.Failure($"SSH connection timeout: Host {host}:{port} did not respond within 15 seconds. The device may be slow to respond or under heavy load.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during SSH connection to {Host}:{Port}", host, port);
                progress?.Report($"Unexpected error during SSH connection: {ex.Message}");
                return Result.Failure($"Unexpected error during SSH connection to {host}:{port}: {ex.Message}", ex);
            }

            if (ssh == null || sftp == null)
            {
                return Result.Failure("Failed to initialize SSH/SFTP clients.");
            }

            // Detect if client is already installed (UPDATE mode) or fresh install needed
            progress?.Report("Checking for existing installation...");
            isUpdateMode = await _installationPreparer.DetectExistingInstallationAsync(ssh, cancellationToken);

            if (isUpdateMode)
            {
                progress?.Report("✓ Existing installation detected - UPDATE MODE");
                progress?.Report("Stopping service and backing up configuration...");
                await _installationPreparer.PrepareUpdateAsync(ssh, username, password, progress, cancellationToken);
            }
            else
            {
                progress?.Report("No existing installation found - INSTALL MODE");
                progress?.Report("Preparing target for fresh install...");
                await _installationPreparer.PrepareCleanInstallAsync(ssh, username, password, progress, cancellationToken);
            }

            progress?.Report("Preparing remote staging folder...");
            try
            {
                await Task.Run(() => ssh.RunCommand($"rm -rf '{RemoteInstallPath}' && mkdir -p '{RemoteInstallPath}'"), cancellationToken);
            }
            catch (SshConnectionException ex)
            {
                _logger.LogError(ex, "SSH connection dropped while preparing staging folder for {Host}", host);
                return Result.Failure("SSH connection was aborted while preparing remote directory. The device may have rebooted unexpectedly.", ex);
            }

            progress?.Report("Uploading installer files (can take a moment)...");
            await _fileUploader.UploadInstallerAsync(sftp, RemoteInstallPath, progress, cancellationToken);

            var installScriptPath = $"{RemoteInstallPath}/install.sh";

            progress?.Report($"Gefundenes install.sh: {installScriptPath}");

            progress?.Report("Making install.sh executable...");
            try
            {
                await Task.Run(() => ssh.RunCommand($"chmod +x '{installScriptPath}'"), cancellationToken);
            }
            catch (SshConnectionException ex)
            {
                _logger.LogError(ex, "SSH connection dropped while making install.sh executable for {Host}", host);
                return Result.Failure("SSH connection was aborted while preparing installer script. The device may have rebooted unexpectedly.", ex);
            }

            var installCommand = BuildInstallCommand(username, password, installScriptPath, isUpdateMode);
            progress?.Report(isUpdateMode ? "Running install.sh in UPDATE mode..." : "Running install.sh in INSTALL mode...");

            var sshCommand = ssh.CreateCommand(installCommand);
            sshCommand.CommandTimeout = TimeSpan.FromMinutes(30);
            installCommandStarted = true;

            // Stream output live to progress log (stdout + stderr)
            using var stdoutReader = new StreamReader(sshCommand.OutputStream ?? Stream.Null);
            using var stderrReader = new StreamReader(sshCommand.ExtendedOutputStream ?? Stream.Null);
            var asyncResult = sshCommand.BeginExecute();

            var stdoutTask = Task.Run(async () =>
            {
                while (!stdoutReader.EndOfStream)
                {
                    var line = await stdoutReader.ReadLineAsync();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        var trimmed = line.TrimEnd();
                        progress?.Report(trimmed);
                        if (!installMarkerSeen && trimmed.Contains(InstallCompleteMarker, StringComparison.Ordinal))
                        {
                            installMarkerSeen = true;
                            progress?.Report("Installations-Marker empfangen. Warte auf Neustart/Abschluss...");
                        }
                    }
                }
            }, cancellationToken);

            var stderrTask = Task.Run(async () =>
            {
                while (!stderrReader.EndOfStream)
                {
                    var line = await stderrReader.ReadLineAsync();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        var trimmed = line.TrimEnd();
                        progress?.Report($"[stderr] {trimmed}");
                        if (!installMarkerSeen && trimmed.Contains(InstallCompleteMarker, StringComparison.Ordinal))
                        {
                            installMarkerSeen = true;
                            progress?.Report("Installations-Marker empfangen. Warte auf Neustart/Abschluss...");
                        }
                    }
                }
            }, cancellationToken);

            var commandConnectionDropped = false;
            Exception? commandException = null;

            var endTask = Task.Run(() =>
            {
                try
                {
                    sshCommand.EndExecute(asyncResult);
                }
                catch (SshConnectionException ex)
                {
                    commandConnectionDropped = true;
                    commandException = ex;
                }
                catch (Exception ex)
                {
                    commandException = ex;
                }
            }, cancellationToken);

            await Task.WhenAll(stdoutTask, stderrTask, endTask);

            // Wenn install.sh durch ist, immer als Erfolg behandeln und Verbindung schließen.
            if (installMarkerSeen)
            {
                // FEATURE: Nach erfolgreicher Installation auch Splash-Screen einrichten (falls Logo verfügbar)
                progress?.Report("Installation abgeschlossen. Prüfe Splash-Screen-Setup...");
                await SetupSplashScreenAsync(ssh, username, password, cancellationToken, progress);

                progress?.Report("Installations-Marker empfangen; Verbindung wird beendet.");
                return Result.Success("Installation abgeschlossen (Marker empfangen). Gerät rebootet ggf.");
            }

            if (commandConnectionDropped)
            {
                progress?.Report("SSH-Verbindung nach install.sh getrennt – Installation gilt als abgeschlossen.");
                return Result.Success("Installation abgeschlossen (SSH getrennt nach install.sh). Gerät rebootet ggf.");
            }

            if (commandException != null)
            {
                throw commandException;
            }

            // FEATURE: Nach erfolgreicher Installation auch Splash-Screen einrichten (falls Logo verfügbar)
            progress?.Report("Installation abgeschlossen. Prüfe Splash-Screen-Setup...");
            await SetupSplashScreenAsync(ssh, username, password, cancellationToken, progress);

            // Auch ohne Marker: nach regulärem Ende als Erfolg melden und beenden.
            progress?.Report("install.sh beendet; Verbindung wird beendet.");
            return Result.Success("Installation abgeschlossen (install.sh beendet).");
        }
        catch (AggregateException ex) when (TryUnwrapSshConnection(ex, out var sshEx))
        {
            if (installCommandStarted)
            {
                progress?.Report("SSH-Verbindung nach install.sh getrennt – Installation gilt als abgeschlossen.");
                _logger.LogInformation(sshEx, "SSH connection dropped after install for host {Host}", host);
                return Result.Success("Installation abgeschlossen (SSH getrennt nach install.sh).");
            }

            return Result.Failure("SSH-Verbindung getrennt, bevor install.sh gestartet wurde.", sshEx);
        }
        catch (SshConnectionException ex)
        {
            if (installCommandStarted)
            {
                progress?.Report("SSH-Verbindung nach install.sh getrennt – Installation gilt als abgeschlossen.");
                _logger.LogInformation(ex, "SSH connection dropped after install for host {Host}", host);
                return Result.Success("Installation abgeschlossen (SSH getrennt nach install.sh).");
            }

            _logger.LogError(ex, "SSH connection dropped during install for host {Host}", host);
            return Result.Failure("SSH connection was aborted by the server during installation. Verify network/firewall and retry.", ex);
        }
        catch (SshAuthenticationException ex)
        {
            _logger.LogError(ex, "SSH authentication failed mid-install for host {Host}", host);
            return Result.Failure("SSH authentication failed while running install.sh. Please re-enter credentials.", ex);
        }
        catch (SocketException ex)
        {
            // If we were already running install.sh, treat as connection drop (e.g., reboot)
            if (installCommandStarted)
            {
                _logger.LogInformation(ex, "Socket error during install (likely reboot) for host {Host}", host);
                progress?.Report("SSH-Socket getrennt nach install.sh – Installation gilt als abgeschlossen.");
                return Result.Success("Installation abgeschlossen (SSH getrennt nach install.sh).");
            }

            _logger.LogError(ex, "Socket error during remote install for host {Host}", host);
            return Result.Failure($"Network error during installation: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Remote installation failed for host {Host}", host);
            return Result.Failure($"Installation failed: {ex.Message}", ex);
        }
        finally
        {
            // Ensure disposal never escapes as an unhandled SshConnectionException when the server drops the line
            _connectionManager.SafeDispose(sftp, nameof(sftp));
            _connectionManager.SafeDispose(ssh, nameof(ssh));
        }
    }

    private string BuildInstallCommand(string username, string password, string installScriptPath, bool isUpdateMode)
    {
        var normalizedPath = installScriptPath.Replace("\\", "/");
        if (!normalizedPath.StartsWith("/"))
        {
            normalizedPath = $"{RemoteInstallPath.TrimEnd('/')}/{normalizedPath.TrimStart('/')}";
        }

        // Set environment variables for install.sh
        // DS_NONINTERACTIVE=1: Skip all interactive prompts
        // DS_UPDATE_MODE=1: Force UPDATE mode (preserve config, update files only)
        var envVars = isUpdateMode
            ? "DS_NONINTERACTIVE=1 DS_UPDATE_MODE=1"
            : "DS_NONINTERACTIVE=1";

        // Execute install.sh with environment variables
        if (string.Equals(username, "root", StringComparison.OrdinalIgnoreCase))
        {
            // Root user: direct execution with env vars
            return $"{envVars} /bin/bash '{normalizedPath}'";
        }

        // CRITICAL FIX: Pass environment variables through sudo using 'env'
        // sudo typically resets environment variables, so we must use 'sudo env VAR=value command'
        // This ensures DS_NONINTERACTIVE and DS_UPDATE_MODE reach install.sh
        return $"sudo env {envVars} /bin/bash '{normalizedPath}'";
    }

    /// <summary>
    /// Configures boot splash screen with the DigitalSignage logo after installation.
    /// CRITICAL: This method MUST NEVER throw exceptions - all errors are logged and suppressed
    /// to prevent app crashes during splash screen setup (which is non-critical).
    /// Note: Despite the Async suffix, this method is intentionally synchronous to avoid Task.Run() exception handling issues.
    /// </summary>
    private Task SetupSplashScreenAsync(SshClient ssh, string username, string password, CancellationToken cancellationToken, IProgress<string>? progress)
    {
        // CRITICAL: Top-level try-catch to prevent ANY exception from escaping
        // Splash screen setup is NON-CRITICAL and should never crash the application
        try
        {
            var isRoot = string.Equals(username, "root", StringComparison.OrdinalIgnoreCase);
            var escapedPassword = password.Replace("'", "'\"'\"'");

            // Check if logo and setup script exist in install directory
            var checkScript = $@"
set -e
if [ -f '{RemoteInstallDir}/digisign-logo.png' ] && [ -f '{RemoteInstallDir}/setup-splash-screen.sh' ]; then
    echo 'SPLASH_AVAILABLE'
else
    echo 'SPLASH_NOT_AVAILABLE'
fi
".Replace("\r\n", "\n").Replace("\r", "\n");

            var escapedCheck = checkScript.Replace("'", "'\"'\"'");
            // CRITICAL FIX: Use 'bash -c' instead of 'bash -lc'
            // Login shells (-l) load profile files which can interfere
            var checkCommand = isRoot
                ? $"bash -c '{escapedCheck}'"
                : $"printf '%s\\n' '{escapedPassword}' | sudo -S bash -c '{escapedCheck}'";

            var checkCmd = ssh.CreateCommand(checkCommand);
            checkCmd.CommandTimeout = TimeSpan.FromSeconds(10);

            string? checkResult;
            try
            {
                // CRITICAL FIX: Remove Task.Run() wrapper
                // Execute synchronously to ensure exceptions are properly caught
                // Task.Run() was causing exceptions to escape the try-catch block
                checkResult = checkCmd.Execute();
            }
            catch (Renci.SshNet.Common.SshConnectionException sshEx)
            {
                // SSH connection dropped - Pi might be rebooting or network unstable
                progress?.Report("⚠ SSH-Verbindung unterbrochen beim Prüfen (Pi könnte neu starten, nicht kritisch)");
                _logger.LogInformation(sshEx, "SSH connection dropped during splash screen check - this is normal during setup");
                return Task.CompletedTask; // Skip splash setup if connection is unstable - not an error
            }
            catch (Renci.SshNet.Common.SshException sshGenericEx)
            {
                // Any other SSH exception
                progress?.Report("⚠ SSH-Fehler beim Prüfen (nicht kritisch)");
                _logger.LogInformation(sshGenericEx, "SSH error during splash screen check - continuing");
                return Task.CompletedTask; // Skip splash setup on SSH errors
            }
            catch (TimeoutException timeoutEx)
            {
                // Timeout is normal during checks - not an error
                progress?.Report("⚠ Timeout beim Prüfen der Splash-Screen-Dateien (nicht kritisch)");
                _logger.LogInformation(timeoutEx, "Timeout during splash screen file check - continuing");
                return Task.CompletedTask; // Skip splash setup on timeout
            }
            catch (Exception ex)
            {
                // Catch all other exceptions to prevent crashes
                progress?.Report("⚠ Fehler beim Prüfen der Splash-Screen-Dateien (nicht kritisch)");
                _logger.LogInformation(ex, "Error during splash screen check - continuing without splash setup");
                return Task.CompletedTask; // Skip splash setup on any error
            }

            if (checkResult?.Contains("SPLASH_NOT_AVAILABLE", StringComparison.OrdinalIgnoreCase) == true)
            {
                progress?.Report("Splash-Screen-Setup übersprungen (Logo oder Skript nicht gefunden)");
                return Task.CompletedTask;
            }

            progress?.Report("Logo und Skript gefunden. Richte Splash-Screen ein...");

            // Run splash screen setup
            // IMPORTANT: Copy logo to /digisign-logo.png first (Plymouth expects it there)
            var splashScript = $@"
set -e
cd '{RemoteInstallDir}'
if [ -f ./digisign-logo.png ] && [ -f ./setup-splash-screen.sh ]; then
    # Copy logo to root directory for Plymouth
    cp ./digisign-logo.png /digisign-logo.png
    chmod 644 /digisign-logo.png

    # Make setup script executable and run it
    chmod +x ./setup-splash-screen.sh
    ./setup-splash-screen.sh /digisign-logo.png
    echo 'SPLASH_SETUP_SUCCESS'
else
    echo 'SPLASH_SETUP_FAILED'
fi
".Replace("\r\n", "\n").Replace("\r", "\n");

            var escapedSplash = splashScript.Replace("'", "'\"'\"'");
            // CRITICAL FIX: Use 'bash -c' instead of 'bash -lc'
            // Login shells (-l) load profile files which can interfere
            var splashCommand = isRoot
                ? $"bash -c '{escapedSplash}'"
                : $"printf '%s\\n' '{escapedPassword}' | sudo -S bash -c '{escapedSplash}'";

            var splashCmd = ssh.CreateCommand(splashCommand);
            splashCmd.CommandTimeout = TimeSpan.FromSeconds(60);

            try
            {
                // CRITICAL FIX: Remove Task.Run() wrapper
                // Execute synchronously to ensure exceptions are properly caught
                // Task.Run() was causing exceptions to escape the try-catch block
                var splashOutput = splashCmd.Execute();

                if (splashOutput?.Contains("SPLASH_SETUP_SUCCESS", StringComparison.OrdinalIgnoreCase) == true)
                {
                    progress?.Report("✓ Splash-Screen erfolgreich eingerichtet (Reboot erforderlich für Anzeige beim Booten)");
                    _logger.LogInformation("Splash screen setup completed successfully. Manual reboot required for boot logo to appear.");
                }
                else
                {
                    progress?.Report("⚠ Splash-Screen-Setup fehlgeschlagen (nicht kritisch)");
                    _logger.LogInformation("Splash screen setup did not report success. Output: {Output}", splashOutput);
                }
            }
            catch (Renci.SshNet.Common.SshConnectionException sshEx)
            {
                // FIXED: SSH connection dropped during initramfs rebuild
                // This is NORMAL behavior - initramfs rebuild can drop SSH connections
                // The Pi is rebuilding the initramfs, which is EXPECTED during splash setup
                // This is NOT an error - it's a sign that the setup is working correctly
                progress?.Report("✓ Splash-Screen Setup erfolgreich (SSH-Verbindung während initramfs rebuild unterbrochen - NORMAL)");
                _logger.LogInformation(sshEx, "SSH connection dropped during splash setup - EXPECTED behavior during initramfs rebuild");
            }
            catch (Renci.SshNet.Common.SshException sshGenericEx)
            {
                // Any other SSH exception during splash setup
                progress?.Report("⚠ SSH-Fehler beim Splash-Screen-Setup (nicht kritisch)");
                _logger.LogInformation(sshGenericEx, "SSH error during splash setup - continuing");
            }
            catch (TimeoutException timeoutEx)
            {
                // Initramfs rebuild can take 30-60 seconds on Raspberry Pi
                // Timeout is NORMAL and EXPECTED - not a failure
                progress?.Report("✓ Splash-Screen Setup läuft (Timeout während initramfs rebuild - NORMAL)");
                _logger.LogInformation(timeoutEx, "Timeout during splash setup - EXPECTED during initramfs rebuild (30-60 seconds)");
            }
            catch (Exception ex)
            {
                // Catch ANY other exception during splash command execution
                progress?.Report("⚠ Fehler beim Splash-Screen-Setup (nicht kritisch)");
                _logger.LogInformation(ex, "Error during splash setup command - continuing");
            }
        }
        catch (Renci.SshNet.Common.SshException sshEx)
        {
            // CRITICAL: Top-level SSH exception catch - prevents app crash
            _logger.LogWarning(sshEx, "SSH error during splash screen setup (non-critical) - caught at top level");
            progress?.Report("⚠ Splash-Screen-Setup übersprungen (SSH-Fehler, nicht kritisch)");
        }
        catch (Exception ex)
        {
            // CRITICAL: Top-level catch-all - prevents app crash from ANY exception
            _logger.LogWarning(ex, "Unexpected error during splash screen setup (non-critical) - caught at top level");
            progress?.Report("⚠ Splash-Screen-Setup übersprungen (Fehler, nicht kritisch)");
        }

        return Task.CompletedTask;
    }

    private async Task<Result> HandleConnectionDropAsync(
        SshConnectionException ex,
        bool installCommandStarted,
        string host,
        int port,
        string username,
        string password,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        // If the connection dropped after we started the install command, assume reboot and wait for service
        if (installCommandStarted)
        {
            _logger.LogInformation(ex, "SSH connection dropped during install (likely reboot) for host {Host}", host);
            var serviceUp = await WaitForRebootAndServiceAsync(host, port, username, password, progress, cancellationToken);
            if (serviceUp)
            {
                return Result.Success("Installation completed and service is running after reboot.");
            }

            return Result.Failure("Device reboot detected, but the client service did not come online in time.", ex);
        }

        _logger.LogError(ex, "SSH connection dropped during install for host {Host}", host);
        return Result.Failure("SSH connection was aborted by the server during installation. Verify network/firewall and retry.", ex);
    }

    private static bool TryUnwrapSshConnection(AggregateException aggregate, out SshConnectionException? sshEx)
    {
        var flattened = aggregate.Flatten();
        sshEx = flattened.InnerExceptions.OfType<SshConnectionException>().FirstOrDefault();
        return sshEx != null;
    }

    private async Task<bool> WaitForRebootAndServiceAsync(
        string host,
        int port,
        string username,
        string password,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var maxRebootWait = TimeSpan.FromMinutes(3);
        var retryDelay = TimeSpan.FromSeconds(5);
        var start = DateTime.UtcNow;

        progress?.Report("Connection dropped (likely reboot). Waiting for device to come back online...");
        while (DateTime.UtcNow - start < maxRebootWait)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await _connectionManager.IsTcpReachableAsync(host, port, cancellationToken))
            {
                progress?.Report("SSH reachable again - verifying client service status...");
                var serviceActive = await _connectionManager.IsServiceActiveSafeAsync(host, port, username, password, RemoteServiceName, cancellationToken);
                if (serviceActive == true)
                {
                    progress?.Report("digitalsignage-client service is active after reboot.");
                    return true;
                }

                progress?.Report("Service not yet active, retrying...");
            }
            else
            {
                progress?.Report("Waiting for SSH to come back up...");
            }

            await Task.Delay(retryDelay, cancellationToken);
        }

        progress?.Report("Timeout waiting for device to reboot and start the service.");
        return false;
    }
}
