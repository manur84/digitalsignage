using DigitalSignage.Core.Models;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Service for broadcasting server availability via mDNS/Bonjour
/// </summary>
public interface INetworkDiscoveryService
{
    /// <summary>
    /// Start broadcasting server availability via mDNS
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stop broadcasting
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Get current server info
    /// </summary>
    ServerInfo GetServerInfo();

    /// <summary>
    /// Update service advertisement (e.g., when connected clients count changes)
    /// </summary>
    void UpdateAdvertisement();
}
