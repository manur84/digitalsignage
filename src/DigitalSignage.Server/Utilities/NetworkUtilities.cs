using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace DigitalSignage.Server.Utilities;

/// <summary>
/// Utility class for network-related operations
/// Consolidates duplicate IP address retrieval logic from multiple services
/// </summary>
public static class NetworkUtilities
{
    /// <summary>
    /// Checks if an IPv4 address is valid for auto-discovery.
    /// Filters out localhost, link-local, and other invalid addresses.
    /// </summary>
    /// <param name="ipAddress">IP address to validate</param>
    /// <returns>True if IP is valid for discovery, false otherwise</returns>
    private static bool IsValidDiscoveryAddress(IPAddress ipAddress)
    {
        if (ipAddress == null || ipAddress.AddressFamily != AddressFamily.InterNetwork)
            return false;

        // Exclude loopback (127.0.0.0/8)
        if (IPAddress.IsLoopback(ipAddress))
            return false;

        var bytes = ipAddress.GetAddressBytes();

        // Exclude unspecified (0.0.0.0)
        if (bytes.All(b => b == 0))
            return false;

        // Exclude link-local (169.254.0.0/16)
        if (bytes[0] == 169 && bytes[1] == 254)
            return false;

        // Exclude broadcast (255.255.255.255)
        if (bytes.All(b => b == 255))
            return false;

        return true;
    }

    /// <summary>
    /// Checks if an IPv4 address is a private network address.
    /// Private ranges: 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16
    /// </summary>
    /// <param name="ipAddress">IP address to check</param>
    /// <returns>True if IP is a private network address</returns>
    private static bool IsPrivateAddress(IPAddress ipAddress)
    {
        if (ipAddress == null || ipAddress.AddressFamily != AddressFamily.InterNetwork)
            return false;

        var bytes = ipAddress.GetAddressBytes();

        // 10.0.0.0/8
        if (bytes[0] == 10)
            return true;

        // 172.16.0.0/12 (172.16.0.0 - 172.31.255.255)
        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            return true;

        // 192.168.0.0/16
        if (bytes[0] == 192 && bytes[1] == 168)
            return true;

        return false;
    }

    /// <summary>
    /// Gets all local IPv4 addresses (excluding loopback, link-local, etc.)
    /// Uses NetworkInterface API for most comprehensive results.
    ///
    /// Filters out:
    /// - Loopback addresses (127.x.x.x)
    /// - Link-local addresses (169.254.x.x)
    /// - Unspecified address (0.0.0.0)
    /// - Broadcast address (255.255.255.255)
    ///
    /// Prioritizes:
    /// - Private network addresses (192.168.x.x, 10.x.x.x, 172.16-31.x.x) first
    /// - Public addresses second
    /// </summary>
    /// <returns>Array of local IPv4 addresses, prioritized and filtered</returns>
    public static IPAddress[] GetLocalIPv4Addresses()
    {
        try
        {
            // Get all valid IP addresses from network interfaces
            var allAddresses = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                            ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                .Select(addr => addr.Address)
                .Where(IsValidDiscoveryAddress)
                .ToList();

            // Separate into private and public addresses
            var privateAddresses = allAddresses.Where(IsPrivateAddress).ToList();
            var publicAddresses = allAddresses.Where(ip => !IsPrivateAddress(ip)).ToList();

            // Return private addresses first, then public addresses
            var result = privateAddresses.Concat(publicAddresses).ToArray();

            // If no valid addresses found, return empty array (don't fall back to loopback!)
            if (result.Length == 0)
            {
                // Log warning but return empty array - loopback is useless for discovery
                System.Diagnostics.Debug.WriteLine(
                    "WARNING: No valid network addresses found for auto-discovery. " +
                    "Discovery will not work until network is configured.");
            }

            return result;
        }
        catch (Exception ex)
        {
            // Log error but return empty array - loopback is useless for discovery
            System.Diagnostics.Debug.WriteLine(
                $"ERROR: Failed to enumerate network interfaces: {ex.Message}. " +
                "Auto-discovery will not work.");

            // Return empty array instead of loopback - better to fail explicitly
            // than to advertise an unusable address
            return Array.Empty<IPAddress>();
        }
    }

    /// <summary>
    /// Gets all local IPv4 addresses as strings (filtered and prioritized)
    /// </summary>
    /// <returns>Array of IP address strings</returns>
    public static string[] GetLocalIPv4AddressStrings()
    {
        return GetLocalIPv4Addresses()
            .Select(ip => ip.ToString())
            .ToArray();
    }
}
