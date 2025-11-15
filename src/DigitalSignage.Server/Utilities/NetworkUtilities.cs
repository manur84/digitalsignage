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
    /// Gets all local IPv4 addresses (excluding loopback)
    /// Uses NetworkInterface API for most comprehensive results
    /// </summary>
    /// <returns>Array of local IPv4 addresses</returns>
    public static IPAddress[] GetLocalIPv4Addresses()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                            ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                .Where(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork &&
                              !IPAddress.IsLoopback(addr.Address))
                .Select(addr => addr.Address)
                .ToArray();
        }
        catch
        {
            // Fallback to loopback if network interfaces cannot be enumerated
            return new[] { IPAddress.Loopback };
        }
    }

    /// <summary>
    /// Gets all local IPv4 addresses as strings
    /// </summary>
    /// <returns>Array of IP address strings</returns>
    public static string[] GetLocalIPv4AddressStrings()
    {
        return GetLocalIPv4Addresses()
            .Select(ip => ip.ToString())
            .ToArray();
    }
}
