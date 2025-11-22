namespace DigitalSignage.App.Mobile.Models;

/// <summary>
/// Represents a Digital Signage server discovered via mDNS.
/// </summary>
public class DiscoveredServer
{
	/// <summary>
	/// Gets or sets the server hostname.
	/// </summary>
	public string Hostname { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the server IP address.
	/// </summary>
	public string IPAddress { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the server port.
	/// </summary>
	public int Port { get; set; }

	/// <summary>
	/// Gets or sets whether the server uses SSL/TLS.
	/// </summary>
	public bool UseSSL { get; set; }

	/// <summary>
	/// Gets or sets the server version.
	/// </summary>
	public string? Version { get; set; }

	/// <summary>
	/// Gets or sets the number of connected clients.
	/// </summary>
	public int? ConnectedClients { get; set; }

	/// <summary>
	/// Gets or sets when the server was discovered.
	/// </summary>
	public DateTime DiscoveredAt { get; set; } = DateTime.Now;

	/// <summary>
	/// Gets the full server URL (e.g., "https://192.168.1.100:8080").
	/// </summary>
	public string Url => $"{(UseSSL ? "https" : "http")}://{IPAddress}:{Port}";

	/// <summary>
	/// Gets the WebSocket URL (e.g., "wss://192.168.1.100:8080/ws/").
	/// </summary>
	public string WebSocketUrl => $"{(UseSSL ? "wss" : "ws")}://{IPAddress}:{Port}/ws/";

	/// <summary>
	/// Gets a display name for the server.
	/// </summary>
	public string DisplayName => !string.IsNullOrEmpty(Hostname) ? Hostname : IPAddress;

	/// <summary>
	/// Gets a description of the server.
	/// </summary>
	public string Description
	{
		get
		{
			var parts = new List<string> { IPAddress };
			if (!string.IsNullOrEmpty(Version))
				parts.Add($"v{Version}");
			if (ConnectedClients.HasValue)
				parts.Add($"{ConnectedClients} clients");
			return string.Join(" • ", parts);
		}
	}
}
