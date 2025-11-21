namespace DigitalSignage.Core.Models;

/// <summary>
/// Server information broadcast via mDNS for auto-discovery
/// </summary>
public class ServerInfo
{
    /// <summary>
    /// Server name (e.g., "DigitalSignage-DESKTOP-ABC")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Server hostname
    /// </summary>
    public string Hostname { get; set; } = string.Empty;

    /// <summary>
    /// WebSocket URL (e.g., "wss://192.168.1.100:8080")
    /// </summary>
    public string WebSocketUrl { get; set; } = string.Empty;

    /// <summary>
    /// Server version
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Number of connected clients
    /// </summary>
    public int ConnectedClients { get; set; }

    /// <summary>
    /// Server IP addresses
    /// </summary>
    public List<string> IpAddresses { get; set; } = new();

    /// <summary>
    /// Server port
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Requires SSL/TLS
    /// </summary>
    public bool UsesSsl { get; set; }
}
