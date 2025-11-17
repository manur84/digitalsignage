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

            progress?.Report("Preparing remote staging folder...");
            await Task.Run(() => ssh.RunCommand($"rm -rf '{RemoteInstallPath}' && mkdir -p '{RemoteInstallPath}'"), cancellationToken);

            progress?.Report("Uploading installer files (can take a moment)...");
            await UploadInstallerAsync(sftp, cancellationToken, progress);

            progress?.Report("Making install.sh executable...");
            await Task.Run(() => ssh.RunCommand($"cd '{RemoteInstallPath}' && chmod +x install.sh"), cancellationToken);

            var installCommand = BuildInstallCommand(username, password);
            progress?.Report("Running install.sh (sudo) ...");

            var sshCommand = ssh.CreateCommand(installCommand);
            sshCommand.CommandTimeout = TimeSpan.FromMinutes(10);

            var commandOutput = await Task.Run(() => sshCommand.Execute(), cancellationToken);
            var output = (sshCommand.Result ?? commandOutput ?? string.Empty).Trim();
            var error = sshCommand.Error?.Trim();

            if (!string.IsNullOrWhiteSpace(output))
            {
                _logger.LogInformation("install.sh output: {Output}", output);
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                _logger.LogWarning("install.sh stderr: {Error}", error);
            }

            if (sshCommand.ExitStatus != 0)
            {
                return Result.Failure($"install.sh failed with exit code {sshCommand.ExitStatus}: {error ?? output}");
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

    private string BuildInstallCommand(string username, string password)
    {
        // Run through bash explicitly to avoid line-ending interpreter issues on uploaded scripts
        var baseCommand = $"cd '{RemoteInstallPath}' && /bin/bash install.sh";

        // Root can execute directly without sudo.
        if (string.Equals(username, "root", StringComparison.OrdinalIgnoreCase))
        {
            return baseCommand;
        }

        var escapedPassword = password.Replace("'", "'\"'\"'");
        return $"cd '{RemoteInstallPath}' && echo '{escapedPassword}' | sudo -S ./install.sh";
    }
}
