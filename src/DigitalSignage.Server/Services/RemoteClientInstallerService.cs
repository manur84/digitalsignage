using System;
using System.IO;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using Renci.SshNet.Sftp;

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
        if (string.IsNullOrWhiteSpace(host))
            return Result.Failure("Host/IP is required for installation.");

        if (!Directory.Exists(_installerSourcePath))
            return Result.Failure($"Installer payload not found: {_installerSourcePath}");

        try
        {
            progress?.Report($"Connecting to {host}:{port} ...");
            _logger.LogInformation("Starting remote installer for {Host}:{Port} as {User}", host, port, username);

            using var ssh = CreateSshClient(host, port, username, password);
            using var sftp = CreateSftpClient(host, port, username, password);

            await Task.Run(() => ssh.Connect(), cancellationToken);
            await Task.Run(() => sftp.Connect(), cancellationToken);

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Remote installation failed for host {Host}", host);
            return Result.Failure($"Installation failed: {ex.Message}", ex);
        }
    }

    private static SshClient CreateSshClient(string host, int port, string username, string password)
    {
        return new SshClient(host, port, username, password)
        {
            KeepAliveInterval = TimeSpan.FromSeconds(30)
        };
    }

    private static SftpClient CreateSftpClient(string host, int port, string username, string password)
    {
        return new SftpClient(host, port, username, password);
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

        // Root can execute directly without sudo.
        if (string.Equals(username, "root", StringComparison.OrdinalIgnoreCase))
        {
            // Run install.sh in non-interactive mode (script handles its own defaults)
            return $"DS_NONINTERACTIVE=1 /bin/bash '{normalizedPath}'";
        }

        // Run through bash via sudo; first line of input is the sudo password, remaining feed into install.sh.
        var escapedPassword = password.Replace("'", "'\"'\"'");
        return $"printf '%s\\n' '{escapedPassword}' | sudo -S DS_NONINTERACTIVE=1 /bin/bash '{normalizedPath}'";
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
}
