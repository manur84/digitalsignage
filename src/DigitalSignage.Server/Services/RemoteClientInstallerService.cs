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
    private readonly string _installerSourcePath;
    private const string RemoteInstallPath = "/tmp/digitalsignage-client-installer";
    private const string RemoteServiceName = "digitalsignage-client";
    private const string RemoteInstallDir = "/opt/digitalsignage-client";
    private const string RemoteServiceFile = "/etc/systemd/system/digitalsignage-client.service";
    private const string InstallCompleteMarker = "__DS_INSTALL_COMPLETE__";

    public RemoteClientInstallerService(ILogger<RemoteClientInstallerService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _installerSourcePath = Path.Combine(AppContext.BaseDirectory, "ClientInstaller");
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
            await EnsurePortReachableAsync(host, port, cancellationToken);

            try
            {
                ssh = CreateSshClient(host, port, username, password);
        sftp = CreateSftpClient(host, port, username, password);

        await ConnectWithTimeoutAsync(ssh, cancellationToken);
        await ConnectWithTimeoutAsync(sftp, cancellationToken);
    }
            catch (SshAuthenticationException ex)
            {
                _logger.LogWarning(ex, "SSH authentication failed for {Host}", host);
                return Result.Failure("SSH authentication failed. Please verify username/password.", ex);
            }
            catch (SshConnectionException ex)
            {
                _logger.LogWarning(ex, "SSH connection aborted while connecting to {Host}", host);
                return Result.Failure("SSH connection was aborted by the remote host. Check network/firewall and that SSH is running.", ex);
            }
            catch (SocketException ex)
            {
                _logger.LogWarning(ex, "SSH TCP connection failed for {Host}:{Port}", host, port);
                return Result.Failure($"Unable to reach {host}:{port} over SSH (TCP connection failed).", ex);
            }

            if (ssh == null || sftp == null)
            {
                return Result.Failure("Failed to initialize SSH/SFTP clients.");
            }

            // Detect if client is already installed (UPDATE mode) or fresh install needed
            progress?.Report("Checking for existing installation...");
            isUpdateMode = await DetectExistingInstallationAsync(ssh, cancellationToken);

            if (isUpdateMode)
            {
                progress?.Report("✓ Existing installation detected - UPDATE MODE");
                progress?.Report("Stopping service and backing up configuration...");
                await PrepareUpdateAsync(ssh, username, password, cancellationToken, progress);
            }
            else
            {
                progress?.Report("No existing installation found - INSTALL MODE");
                progress?.Report("Preparing target for fresh install...");
                await PrepareCleanInstallAsync(ssh, username, password, cancellationToken, progress);
            }

            progress?.Report("Preparing remote staging folder...");
            await Task.Run(() => ssh.RunCommand($"rm -rf '{RemoteInstallPath}' && mkdir -p '{RemoteInstallPath}'"), cancellationToken);

            progress?.Report("Uploading installer files (can take a moment)...");
            await UploadInstallerAsync(sftp, cancellationToken, progress);

            var installScriptPath = $"{RemoteInstallPath}/install.sh";

            progress?.Report($"Gefundenes install.sh: {installScriptPath}");

            progress?.Report("Making install.sh executable...");
            await Task.Run(() => ssh.RunCommand($"chmod +x '{installScriptPath}'"), cancellationToken);

            var installCommand = BuildInstallCommand(username, password, installScriptPath, isUpdateMode);
            progress?.Report(isUpdateMode ? "Running install.sh in UPDATE mode..." : "Running install.sh in INSTALL mode...");

            var sshCommand = ssh.CreateCommand(installCommand);
            sshCommand.CommandTimeout = TimeSpan.FromMinutes(10);
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
            SafeDispose(sftp, nameof(sftp));
            SafeDispose(ssh, nameof(ssh));
        }
    }

    private static SshClient CreateSshClient(string host, int port, string username, string password)
    {
        var connectionInfo = new ConnectionInfo(
            host,
            port,
            username,
            new PasswordAuthenticationMethod(username, password))
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        return new SshClient(connectionInfo)
        {
            KeepAliveInterval = TimeSpan.FromSeconds(30)
        };
    }

    private static SftpClient CreateSftpClient(string host, int port, string username, string password)
    {
        var connectionInfo = new ConnectionInfo(
            host,
            port,
            username,
            new PasswordAuthenticationMethod(username, password))
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        return new SftpClient(connectionInfo);
    }

    private async Task UploadInstallerAsync(SftpClient sftp, CancellationToken cancellationToken, IProgress<string>? progress)
    {
        await Task.Run(() =>
        {
            EnsureRemoteDirectory(sftp, RemoteInstallPath);

            var files = Directory.GetFiles(_installerSourcePath, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relative = Path.GetRelativePath(_installerSourcePath, file).Replace("\\", "/");
                var remoteFile = $"{RemoteInstallPath}/{relative}";
                var remoteDir = Path.GetDirectoryName(remoteFile)?.Replace("\\", "/");

                if (!string.IsNullOrWhiteSpace(remoteDir))
                {
                    EnsureRemoteDirectory(sftp, remoteDir);
                }

                using var fs = File.OpenRead(file);
                sftp.UploadFile(fs, remoteFile, true);

                progress?.Report($"Uploaded {relative}");
            }
        }, cancellationToken);
    }

    private static void EnsureRemoteDirectory(SftpClient sftp, string remotePath)
    {
        var parts = remotePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = string.Empty;

        foreach (var part in parts)
        {
            current += "/" + part;
            if (!sftp.Exists(current))
            {
                sftp.CreateDirectory(current);
            }
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
            return $"{envVars} /bin/bash '{normalizedPath}'";
        }

        return $"sudo {envVars} /bin/bash '{normalizedPath}'";
    }

    /// <summary>
    /// Configures boot splash screen with the DigitalSignage logo after installation.
    /// </summary>
    private async Task SetupSplashScreenAsync(SshClient ssh, string username, string password, CancellationToken cancellationToken, IProgress<string>? progress)
    {
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
            var checkCommand = isRoot
                ? $"bash -lc '{escapedCheck}'"
                : $"printf '%s\\n' '{escapedPassword}' | sudo -S bash -lc '{escapedCheck}'";

            var checkCmd = ssh.CreateCommand(checkCommand);
            checkCmd.CommandTimeout = TimeSpan.FromSeconds(10);
            var checkResult = await Task.Run(() => checkCmd.Execute(), cancellationToken);

            if (checkResult?.Contains("SPLASH_NOT_AVAILABLE", StringComparison.OrdinalIgnoreCase) == true)
            {
                progress?.Report("Splash-Screen-Setup übersprungen (Logo oder Skript nicht gefunden)");
                return;
            }

            progress?.Report("Logo und Skript gefunden. Richte Splash-Screen ein...");

            // Run splash screen setup
            var splashScript = $@"
set -e
cd '{RemoteInstallDir}'
if [ -f ./digisign-logo.png ] && [ -f ./setup-splash-screen.sh ]; then
    chmod +x ./setup-splash-screen.sh
    ./setup-splash-screen.sh ./digisign-logo.png
    echo 'SPLASH_SETUP_SUCCESS'
else
    echo 'SPLASH_SETUP_FAILED'
fi
".Replace("\r\n", "\n").Replace("\r", "\n");

            var escapedSplash = splashScript.Replace("'", "'\"'\"'");
            var splashCommand = isRoot
                ? $"bash -lc '{escapedSplash}'"
                : $"printf '%s\\n' '{escapedPassword}' | sudo -S bash -lc '{escapedSplash}'";

            var splashCmd = ssh.CreateCommand(splashCommand);
            splashCmd.CommandTimeout = TimeSpan.FromSeconds(60);
            var splashOutput = await Task.Run(() => splashCmd.Execute(), cancellationToken);

            if (splashOutput?.Contains("SPLASH_SETUP_SUCCESS", StringComparison.OrdinalIgnoreCase) == true)
            {
                progress?.Report("✓ Splash-Screen erfolgreich eingerichtet");
            }
            else
            {
                progress?.Report("⚠ Splash-Screen-Setup fehlgeschlagen (nicht kritisch)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Splash screen setup failed (non-critical)");
            progress?.Report("⚠ Splash-Screen-Setup übersprungen (Fehler, nicht kritisch)");
        }
    }

    /// <summary>
    /// Detects if the Digital Signage client is already installed on the target device.
    /// </summary>
    private async Task<bool> DetectExistingInstallationAsync(SshClient ssh, CancellationToken cancellationToken)
    {
        try
        {
            var checkCmd = ssh.CreateCommand($"test -d '{RemoteInstallDir}' && test -f '{RemoteServiceFile}' && echo 'INSTALLED' || echo 'NOT_INSTALLED'");
            checkCmd.CommandTimeout = TimeSpan.FromSeconds(10);
            var result = await Task.Run(() => checkCmd.Execute(), cancellationToken);
            return result?.Contains("INSTALLED", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch
        {
            return false; // If check fails, assume no installation
        }
    }

    /// <summary>
    /// Prepares for UPDATE mode: stops service and backs up config (preserves installation).
    /// </summary>
    private async Task PrepareUpdateAsync(SshClient ssh, string username, string password, CancellationToken cancellationToken, IProgress<string>? progress)
    {
        var isRoot = string.Equals(username, "root", StringComparison.OrdinalIgnoreCase);
        var escapedPassword = password.Replace("'", "'\"'\"'");

        // UPDATE MODE: Only stop service and backup config (do NOT remove files!)
        var updateScript = $@"
set -e
# Stop service if running
if systemctl is-active --quiet {RemoteServiceName} 2>/dev/null; then
  systemctl stop {RemoteServiceName}
  echo 'Service stopped'
fi

# Backup config.py if exists
if [ -f '{RemoteInstallDir}/config.py' ]; then
  cp '{RemoteInstallDir}/config.py' '{RemoteInstallDir}/config.py.backup'
  echo 'Config backed up'
fi
".Replace("\r\n", "\n").Replace("\r", "\n");

        var escapedScript = updateScript.Replace("'", "'\"'\"'");
        var commandText = isRoot
            ? $"bash -lc '{escapedScript}'"
            : $"printf '%s\\n' '{escapedPassword}' | sudo -S bash -lc '{escapedScript}'";

        progress?.Report("Stopping service and backing up config...");
        var cmd = ssh.CreateCommand(commandText);
        cmd.CommandTimeout = TimeSpan.FromSeconds(30);
        var output = await Task.Run(() => cmd.Execute(), cancellationToken);
        var combined = (cmd.Error ?? output ?? string.Empty).Trim();

        if (!string.IsNullOrWhiteSpace(combined))
        {
            foreach (var line in combined.Split('\n'))
            {
                var trimmed = line.TrimEnd();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    progress?.Report(trimmed);
                }
            }
        }

        if (cmd.ExitStatus != 0)
        {
            throw new InvalidOperationException($"Failed to prepare update (exit {cmd.ExitStatus}): {combined}");
        }
    }

    /// <summary>
    /// Prepares for CLEAN INSTALL mode: stops/disables service and removes old installation.
    /// </summary>
    private async Task PrepareCleanInstallAsync(SshClient ssh, string username, string password, CancellationToken cancellationToken, IProgress<string>? progress)
    {
        var isRoot = string.Equals(username, "root", StringComparison.OrdinalIgnoreCase);
        var escapedPassword = password.Replace("'", "'\"'\"'");

        // Normalize to LF to avoid CR issues on target bash
        var cleanupScript = $@"
set -e
if systemctl is-active --quiet {RemoteServiceName} 2>/dev/null; then
  systemctl stop {RemoteServiceName}
fi
if systemctl is-enabled --quiet {RemoteServiceName} 2>/dev/null; then
  systemctl disable {RemoteServiceName}
fi
rm -rf '{RemoteInstallDir}'
rm -f '{RemoteServiceFile}'
".Replace("\r\n", "\n").Replace("\r", "\n");

        var escapedScript = cleanupScript.Replace("'", "'\"'\"'");
        var commandText = isRoot
            ? $"bash -lc '{escapedScript}'"
            : $"printf '%s\\n' '{escapedPassword}' | sudo -S bash -lc '{escapedScript}'";

        progress?.Report("Stopping service and removing previous install (if any)...");
        var cmd = ssh.CreateCommand(commandText);
        cmd.CommandTimeout = TimeSpan.FromSeconds(30);
        var cleanupOutput = await Task.Run(() => cmd.Execute(), cancellationToken);
        var combined = (cmd.Error ?? cleanupOutput ?? string.Empty).Trim();

        if (!string.IsNullOrWhiteSpace(combined))
        {
            foreach (var line in combined.Split('\n'))
            {
                var trimmed = line.TrimEnd();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    progress?.Report(trimmed);
                }
            }
        }

        if (cmd.ExitStatus != 0)
        {
            throw new InvalidOperationException($"Failed to prepare fresh install (exit {cmd.ExitStatus}): {combined}");
        }
    }

    private static async Task EnsurePortReachableAsync(string host, int port, CancellationToken cancellationToken)
    {
        using var tcpClient = new TcpClient();
        var connectTask = tcpClient.ConnectAsync(host, port);
        var completed = await Task.WhenAny(connectTask, Task.Delay(TimeSpan.FromSeconds(10), cancellationToken));

        cancellationToken.ThrowIfCancellationRequested();

        if (completed != connectTask)
        {
            throw new TimeoutException($"SSH connection to {host}:{port} timed out.");
        }

        // Surface any socket exceptions
        await connectTask;
    }

    private static async Task ConnectWithTimeoutAsync(BaseClient client, CancellationToken cancellationToken)
    {
        // Run connect on TP so we can cancel/timeout gracefully
        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                client.Connect();
            }
            catch (SocketException)
            {
                throw new SshConnectionException("SSH socket connection failed");
            }
            catch (SshConnectionException)
            {
                throw;
            }
        }, cancellationToken);
    }

    private void SafeDispose(IDisposable? disposable, string name)
    {
        if (disposable == null) return;

        try
        {
            disposable.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ignoring {Resource} dispose failure during cleanup", name);
        }
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

    private static bool TryUnwrapSshConnection(AggregateException aggregate, out SshConnectionException sshEx)
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

            if (await IsTcpReachableAsync(host, port, cancellationToken))
            {
                progress?.Report("SSH reachable again - verifying client service status...");
                var serviceActive = await IsServiceActiveSafeAsync(host, port, username, password, cancellationToken);
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

    private static async Task<bool> IsTcpReachableAsync(string host, int port, CancellationToken cancellationToken)
    {
        try
        {
            using var tcpClient = new TcpClient();
            var connectTask = tcpClient.ConnectAsync(host, port);
            var completed = await Task.WhenAny(connectTask, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken));
            cancellationToken.ThrowIfCancellationRequested();
            if (completed != connectTask)
            {
                return false;
            }

            await connectTask;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Returns true if service is active, false if inactive, null if SSH not ready yet.
    /// Does not throw for connection/auth errors so callers can retry during reboot.
    /// </summary>
    private async Task<bool?> IsServiceActiveSafeAsync(
        string host,
        int port,
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        try
        {
            using var ssh = CreateSshClient(host, port, username, password);
            await ConnectWithTimeoutAsync(ssh, cancellationToken);

            var escapedPassword = password.Replace("'", "'\"'\"'");
            var sudoPrefix = string.Equals(username, "root", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : $"printf '%s\\n' '{escapedPassword}' | sudo -S ";

            var checkCmd = ssh.CreateCommand($"{sudoPrefix}systemctl is-active --quiet {RemoteServiceName}");
            checkCmd.CommandTimeout = TimeSpan.FromSeconds(10);

            await Task.Run(() => checkCmd.Execute(), cancellationToken);
            return checkCmd.ExitStatus == 0;
        }
        catch (SshConnectionException)
        {
            return null; // still rebooting or SSH not ready
        }
        catch (SshAuthenticationException)
        {
            return null; // auth not ready yet after reboot (e.g., sshd startup delay)
        }
        catch (SocketException)
        {
            return null; // network/sshd not ready
        }
    }
}
