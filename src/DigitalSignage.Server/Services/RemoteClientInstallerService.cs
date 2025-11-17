using System;
using System.IO;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using Renci.SshNet.Common;
using System.Net.Sockets;

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

            // Always start from a clean slate on the target device
            progress?.Report("Preparing target for fresh install (stop/disable service, remove old files)...");
            await ForceFreshInstallAsync(ssh, username, password, cancellationToken, progress);

            progress?.Report("Preparing remote staging folder...");
            await Task.Run(() => ssh.RunCommand($"rm -rf '{RemoteInstallPath}' && mkdir -p '{RemoteInstallPath}'"), cancellationToken);

            progress?.Report("Uploading installer files (can take a moment)...");
            await UploadInstallerAsync(sftp, cancellationToken, progress);

            var installScriptPath = $"{RemoteInstallPath}/install.sh";

            progress?.Report($"Gefundenes install.sh: {installScriptPath}");

            progress?.Report("Making install.sh executable...");
            await Task.Run(() => ssh.RunCommand($"chmod +x '{installScriptPath}'"), cancellationToken);

            var installCommand = BuildInstallCommand(username, password, installScriptPath);
            progress?.Report("Running install.sh (sudo) ...");

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
                        progress?.Report(line.TrimEnd());
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
                        progress?.Report($"[stderr] {line.TrimEnd()}");
                    }
                }
            }, cancellationToken);

            await Task.WhenAll(stdoutTask, stderrTask, Task.Run(() => sshCommand.EndExecute(asyncResult), cancellationToken));

            if (sshCommand.ExitStatus != 0)
            {
                var combinedError = (sshCommand.Error ?? sshCommand.Result ?? string.Empty).Trim();
                return Result.Failure($"install.sh failed with exit code {sshCommand.ExitStatus}: {combinedError}");
            }

            progress?.Report("Installation completed successfully.");
            return Result.Success("Client installed successfully.");
        }
        catch (SshConnectionException ex)
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
            return Result.Failure("SSH connection was aborted by the server during installation. Verify network stability and retry.", ex);
        }
        catch (SshAuthenticationException ex)
        {
            _logger.LogError(ex, "SSH authentication failed mid-install for host {Host}", host);
            return Result.Failure("SSH authentication failed while running install.sh. Please re-enter credentials.", ex);
        }
        catch (SocketException ex)
        {
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

    private string BuildInstallCommand(string username, string password, string installScriptPath)
    {
        var normalizedPath = installScriptPath.Replace("\\", "/");
        if (!normalizedPath.StartsWith("/"))
        {
            normalizedPath = $"{RemoteInstallPath.TrimEnd('/')}/{normalizedPath.TrimStart('/')}";
        }

        // Execute install.sh exactly as provided, without extra environment flags or password piping.
        if (string.Equals(username, "root", StringComparison.OrdinalIgnoreCase))
        {
            return $"/bin/bash '{normalizedPath}'";
        }

        return $"sudo /bin/bash '{normalizedPath}'";
    }

    private async Task ForceFreshInstallAsync(SshClient ssh, string username, string password, CancellationToken cancellationToken, IProgress<string>? progress)
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
            client.Connect();
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
                if (await IsServiceActiveAsync(host, port, username, password, cancellationToken))
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

    private async Task<bool> IsServiceActiveAsync(
        string host,
        int port,
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        using var ssh = CreateSshClient(host, port, username, password);
        await ConnectWithTimeoutAsync(ssh, cancellationToken);

        var checkCmd = ssh.CreateCommand($"systemctl is-active --quiet {RemoteServiceName}");
        checkCmd.CommandTimeout = TimeSpan.FromSeconds(10);

        await Task.Run(() => checkCmd.Execute(), cancellationToken);
        return checkCmd.ExitStatus == 0;
    }
}
