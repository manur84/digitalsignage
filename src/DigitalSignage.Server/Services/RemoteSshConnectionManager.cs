using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Manages SSH and SFTP connections for remote installation
/// </summary>
internal class RemoteSshConnectionManager
{
    private readonly ILogger<RemoteSshConnectionManager> _logger;

    public RemoteSshConnectionManager(ILogger<RemoteSshConnectionManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates an SSH client with standard configuration
    /// </summary>
    public SshClient CreateSshClient(string host, int port, string username, string password)
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

    /// <summary>
    /// Creates an SFTP client with standard configuration
    /// </summary>
    public SftpClient CreateSftpClient(string host, int port, string username, string password)
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

    /// <summary>
    /// Ensures the specified port is reachable before attempting SSH connection
    /// </summary>
    public async Task EnsurePortReachableAsync(string host, int port, CancellationToken cancellationToken)
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

    /// <summary>
    /// Connects an SSH or SFTP client with timeout handling
    /// </summary>
    public async Task ConnectWithTimeoutAsync(BaseClient client, CancellationToken cancellationToken)
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

    /// <summary>
    /// Safely disposes an SSH/SFTP client without throwing exceptions
    /// </summary>
    public void SafeDispose(IDisposable? disposable, string name)
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

    /// <summary>
    /// Checks if a TCP port is reachable
    /// </summary>
    public async Task<bool> IsTcpReachableAsync(string host, int port, CancellationToken cancellationToken)
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
    /// Checks if the Digital Signage service is active on the remote device
    /// Returns true if service is active, false if inactive, null if SSH not ready yet
    /// </summary>
    public async Task<bool?> IsServiceActiveSafeAsync(
        string host,
        int port,
        string username,
        string password,
        string serviceName,
        CancellationToken cancellationToken)
    {
        try
        {
            using var ssh = CreateSshClient(host, port, username, password);
            await ConnectWithTimeoutAsync(ssh, cancellationToken);

            var isRoot = string.Equals(username, "root", StringComparison.OrdinalIgnoreCase);
            var passwordB64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(password ?? string.Empty));
            var remotePasswordExpr = $"echo '{passwordB64}' | base64 -d";
            var sudoPrefix = isRoot ? string.Empty : $"{remotePasswordExpr} | sudo -S ";

            var checkCmd = ssh.CreateCommand($"{sudoPrefix}systemctl is-active --quiet {serviceName}");
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
