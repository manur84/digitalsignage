using System.Net.NetworkInformation;

namespace DigitalSignage.Core.Models;

/// <summary>
/// Information about a network interface
/// </summary>
public class NetworkInterfaceInfo
{
    /// <summary>
    /// Interface name (e.g., "Ethernet", "Wi-Fi", "eth0")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Interface description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// IPv4 address
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// MAC address
    /// </summary>
    public string MacAddress { get; set; } = string.Empty;

    /// <summary>
    /// Interface type (Ethernet, Wireless, etc.)
    /// </summary>
    public NetworkInterfaceType InterfaceType { get; set; }

    /// <summary>
    /// Whether the interface is currently operational
    /// </summary>
    public bool IsOperational { get; set; }

    /// <summary>
    /// Interface speed in bits per second (0 if unknown)
    /// </summary>
    public long Speed { get; set; }

    /// <summary>
    /// Display name combining name and IP address
    /// </summary>
    public string DisplayName => $"{Name} ({IpAddress})";

    /// <summary>
    /// Whether this is a localhost/loopback interface
    /// </summary>
    public bool IsLoopback { get; set; }
}
