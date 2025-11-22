using DigitalSignage.App.Mobile.Models;

namespace DigitalSignage.App.Mobile.Services;

/// <summary>
/// Service for discovering Digital Signage servers via mDNS/Bonjour.
/// </summary>
public interface IServerDiscoveryService
{
	/// <summary>
	/// Occurs when a server is discovered.
	/// </summary>
	event EventHandler<DiscoveredServer>? ServerDiscovered;

	/// <summary>
	/// Occurs when a server is lost (no longer responding).
	/// </summary>
	event EventHandler<DiscoveredServer>? ServerLost;

	/// <summary>
	/// Gets whether the service is currently scanning for servers.
	/// </summary>
	bool IsScanning { get; }

	/// <summary>
	/// Starts scanning for Digital Signage servers on the local network.
	/// </summary>
	Task StartScanningAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Stops scanning for servers.
	/// </summary>
	Task StopScanningAsync();

	/// <summary>
	/// Gets all currently discovered servers.
	/// </summary>
	IReadOnlyList<DiscoveredServer> GetDiscoveredServers();
}
