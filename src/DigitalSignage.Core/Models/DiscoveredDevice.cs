namespace DigitalSignage.Core.Models;

/// <summary>
/// Represents a device discovered on the network but not yet registered
/// </summary>
public class DiscoveredDevice
{
    /// <summary>
    /// Gets or sets the hostname of the discovered device
    /// </summary>
    public string Hostname { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the IP address
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the MAC address (if available)
    /// </summary>
    public string? MacAddress { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the device was discovered
    /// </summary>
    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the discovery method (mDNS, UDP, ARP, etc.)
    /// </summary>
    public string DiscoveryMethod { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional information received during discovery
    /// </summary>
    public Dictionary<string, string> Properties { get; set; } = new();

    /// <summary>
    /// Gets or sets whether the device responded to ping
    /// </summary>
    public bool IsReachable { get; set; } = true;

    /// <summary>
    /// Gets or sets the operating system type (if detected)
    /// </summary>
    public string? OsType { get; set; }

    /// <summary>
    /// Gets or sets whether this device is likely a Raspberry Pi
    /// </summary>
    public bool IsLikelyRaspberryPi { get; set; }
}
