using System.IO;
using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Handles file uploads to remote devices via SFTP
/// </summary>
internal class RemoteFileUploader
{
    private readonly ILogger<RemoteFileUploader> _logger;
    private readonly string _installerSourcePath;

    public RemoteFileUploader(ILogger<RemoteFileUploader> logger, string installerSourcePath)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _installerSourcePath = installerSourcePath ?? throw new ArgumentNullException(nameof(installerSourcePath));
    }

    /// <summary>
    /// Uploads installer files to the remote device
    /// </summary>
    public async Task UploadInstallerAsync(
        SftpClient sftp,
        string remoteInstallPath,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            EnsureRemoteDirectory(sftp, remoteInstallPath);

            var files = Directory.GetFiles(_installerSourcePath, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relative = Path.GetRelativePath(_installerSourcePath, file).Replace("\\", "/");
                var remoteFile = $"{remoteInstallPath}/{relative}";
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

    /// <summary>
    /// Ensures a remote directory exists (creates it if necessary)
    /// </summary>
    private void EnsureRemoteDirectory(SftpClient sftp, string remotePath)
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
}
