using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Prepares remote devices for installation or updates
/// </summary>
internal class RemoteInstallationPreparer
{
    private readonly ILogger<RemoteInstallationPreparer> _logger;
    private readonly string _remoteInstallDir;
    private readonly string _remoteServiceFile;
    private readonly string _remoteServiceName;

    public RemoteInstallationPreparer(
        ILogger<RemoteInstallationPreparer> logger,
        string remoteInstallDir,
        string remoteServiceFile,
        string remoteServiceName)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _remoteInstallDir = remoteInstallDir;
        _remoteServiceFile = remoteServiceFile;
        _remoteServiceName = remoteServiceName;
    }

    /// <summary>
    /// Detects if the Digital Signage client is already installed on the target device
    /// </summary>
    public async Task<bool> DetectExistingInstallationAsync(SshClient ssh, CancellationToken cancellationToken)
    {
        try
        {
            var checkCmd = ssh.CreateCommand($"test -d '{_remoteInstallDir}' && test -f '{_remoteServiceFile}' && echo 'INSTALLED' || echo 'NOT_INSTALLED'");
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
    /// Prepares for UPDATE mode: stops service and backs up config (preserves installation)
    /// </summary>
    public async Task PrepareUpdateAsync(
        SshClient ssh,
        string username,
        string password,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var isRoot = string.Equals(username, "root", StringComparison.OrdinalIgnoreCase);
        var escapedPassword = password.Replace("'", "'\"'\"'");

        // UPDATE MODE: Only stop service and backup config (do NOT remove files!)
        var updateScript = $@"
set -e
# Stop service if running
if systemctl is-active --quiet {_remoteServiceName} 2>/dev/null; then
  systemctl stop {_remoteServiceName}
  echo 'Service stopped'
fi

# Backup config.py if exists
if [ -f '{_remoteInstallDir}/config.py' ]; then
  cp '{_remoteInstallDir}/config.py' '{_remoteInstallDir}/config.py.backup'
  echo 'Config backed up'
fi
".Replace("\r\n", "\n").Replace("\r", "\n");

        var escapedScript = updateScript.Replace("'", "'\"'\"'");
        // CRITICAL FIX: Use 'bash -c' instead of 'bash -lc'
        // Login shells (-l) load profile files which can interfere with command execution
        // Non-login shells (-c) are more reliable for scripted operations
        var commandText = isRoot
            ? $"bash -c '{escapedScript}'"
            : $"printf '%s\\n' '{escapedPassword}' | sudo -S bash -c '{escapedScript}'";

        progress?.Report("Stopping service and backing up config...");
        var cmd = ssh.CreateCommand(commandText);
        cmd.CommandTimeout = TimeSpan.FromSeconds(30);

        string output;
        try
        {
            output = await Task.Run(() => cmd.Execute(), cancellationToken);
        }
        catch (Renci.SshNet.Common.SshConnectionException ex)
        {
            // SSH connection dropped - might be reboot or network issue
            _logger.LogWarning(ex, "SSH connection dropped during update preparation");
            throw new InvalidOperationException("SSH connection was aborted while preparing update. The device may have rebooted.", ex);
        }

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
    /// Prepares for CLEAN INSTALL mode: stops/disables service and removes old installation
    /// </summary>
    public async Task PrepareCleanInstallAsync(
        SshClient ssh,
        string username,
        string password,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var isRoot = string.Equals(username, "root", StringComparison.OrdinalIgnoreCase);

        // Normalize to LF to avoid CR issues on target bash
        var cleanupScript = $@"
set -e
if systemctl is-active --quiet {_remoteServiceName} 2>/dev/null; then
  systemctl stop {_remoteServiceName}
fi
if systemctl is-enabled --quiet {_remoteServiceName} 2>/dev/null; then
  systemctl disable {_remoteServiceName}
fi
rm -rf '{_remoteInstallDir}'
rm -f '{_remoteServiceFile}'
".Replace("\r\n", "\n").Replace("\r", "\n");

        // Base64-encode password locally and decode remotely to avoid shell metacharacters issues
        var passwordB64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(password ?? string.Empty));
        var remotePasswordExpr = $"echo '{passwordB64}' | base64 -d";

        var escapedScript = cleanupScript.Replace("'", "'\"'\"'");
        // CRITICAL FIX: Use 'bash -c' instead of 'bash -lc'
        // Login shells (-l) load profile files which can interfere with command execution
        // Non-login shells (-c) are more reliable for scripted operations
        var commandText = isRoot
            ? $"bash -c '{escapedScript}'"
            : $"{remotePasswordExpr} | sudo -S bash -c '{escapedScript}'";

        progress?.Report("Stopping service and removing previous install (if any)...");
        var cmd = ssh.CreateCommand(commandText);
        cmd.CommandTimeout = TimeSpan.FromSeconds(30);

        string cleanupOutput;
        try
        {
            cleanupOutput = await Task.Run(() => cmd.Execute(), cancellationToken);
        }
        catch (Renci.SshNet.Common.SshConnectionException ex)
        {
            // SSH connection dropped - might be reboot or network issue
            _logger.LogWarning(ex, "SSH connection dropped during clean install preparation");
            throw new InvalidOperationException("SSH connection was aborted while preparing clean install. The device may have rebooted.", ex);
        }

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
