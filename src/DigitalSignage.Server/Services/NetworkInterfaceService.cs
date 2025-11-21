using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Service for managing network interface selection and information
/// </summary>
public class NetworkInterfaceService
{
    private readonly ILogger<NetworkInterfaceService> _logger;

    public NetworkInterfaceService(ILogger<NetworkInterfaceService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get all available network interfaces with IPv4 addresses
    /// </summary>
    /// <returns>List of network interfaces</returns>
    public List<NetworkInterfaceInfo> GetAllNetworkInterfaces()
    {
        var result = new List<NetworkInterfaceInfo>();

        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (var networkInterface in interfaces)
            {
                try
                {
                    // Skip non-operational interfaces
                    if (networkInterface.OperationalStatus != OperationalStatus.Up)
                        continue;

                    var ipProperties = networkInterface.GetIPProperties();
                    var unicastAddresses = ipProperties.UnicastAddresses;

                    // Find IPv4 address
                    var ipv4Address = unicastAddresses
                        .FirstOrDefault(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork);

                    if (ipv4Address == null)
                        continue;

                    var ipAddress = ipv4Address.Address.ToString();

                    // Get MAC address
                    var macAddress = string.Join(":", networkInterface.GetPhysicalAddress()
                        .GetAddressBytes()
                        .Select(b => b.ToString("X2")));

                    var interfaceInfo = new NetworkInterfaceInfo
                    {
                        Name = networkInterface.Name,
                        Description = networkInterface.Description,
                        IpAddress = ipAddress,
                        MacAddress = macAddress,
                        InterfaceType = networkInterface.NetworkInterfaceType,
                        IsOperational = networkInterface.OperationalStatus == OperationalStatus.Up,
                        Speed = networkInterface.Speed,
                        IsLoopback = networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                                     IPAddress.IsLoopback(ipv4Address.Address)
                    };

                    result.Add(interfaceInfo);
                    _logger.LogDebug("Found network interface: {Name} ({IpAddress})",
                        interfaceInfo.Name, interfaceInfo.IpAddress);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error processing network interface {Name}",
                        networkInterface.Name);
                    continue;
                }
            }

            _logger.LogInformation("Found {Count} operational network interfaces", result.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate network interfaces");
        }

        return result;
    }

    /// <summary>
    /// Get preferred IP address based on configuration
    /// </summary>
    /// <param name="preferredInterface">Preferred interface name or IP address (null for auto-select)</param>
    /// <returns>Selected IP address or null if not found</returns>
    public string? GetPreferredIPAddress(string? preferredInterface)
    {
        var interfaces = GetAllNetworkInterfaces();

        // Remove loopback interfaces from consideration
        var nonLoopbackInterfaces = interfaces.Where(i => !i.IsLoopback).ToList();

        if (nonLoopbackInterfaces.Count == 0)
        {
            _logger.LogWarning("No non-loopback network interfaces found");
            return null;
        }

        // If no preference specified, return first non-loopback interface
        if (string.IsNullOrWhiteSpace(preferredInterface))
        {
            var firstInterface = nonLoopbackInterfaces.First();
            _logger.LogInformation("No preferred interface specified, using first available: {Name} ({IpAddress})",
                firstInterface.Name, firstInterface.IpAddress);
            return firstInterface.IpAddress;
        }

        // Try to match by interface name (case-insensitive)
        var matchByName = nonLoopbackInterfaces
            .FirstOrDefault(i => i.Name.Equals(preferredInterface, StringComparison.OrdinalIgnoreCase));

        if (matchByName != null)
        {
            _logger.LogInformation("Using preferred interface by name: {Name} ({IpAddress})",
                matchByName.Name, matchByName.IpAddress);
            return matchByName.IpAddress;
        }

        // Try to match by IP address
        var matchByIp = nonLoopbackInterfaces
            .FirstOrDefault(i => i.IpAddress.Equals(preferredInterface, StringComparison.Ordinal));

        if (matchByIp != null)
        {
            _logger.LogInformation("Using preferred interface by IP: {Name} ({IpAddress})",
                matchByIp.Name, matchByIp.IpAddress);
            return matchByIp.IpAddress;
        }

        // Try partial match by name (e.g., "Ethernet" matches "Ethernet 2")
        var partialMatch = nonLoopbackInterfaces
            .FirstOrDefault(i => i.Name.Contains(preferredInterface, StringComparison.OrdinalIgnoreCase) ||
                                i.Description.Contains(preferredInterface, StringComparison.OrdinalIgnoreCase));

        if (partialMatch != null)
        {
            _logger.LogInformation("Using interface with partial name match: {Name} ({IpAddress})",
                partialMatch.Name, partialMatch.IpAddress);
            return partialMatch.IpAddress;
        }

        // Preferred interface not found, fallback to first available
        var fallbackInterface = nonLoopbackInterfaces.First();
        _logger.LogWarning("Preferred interface '{Preferred}' not found, falling back to: {Name} ({IpAddress})",
            preferredInterface, fallbackInterface.Name, fallbackInterface.IpAddress);
        return fallbackInterface.IpAddress;
    }

    /// <summary>
    /// Get all IPv4 addresses from all non-loopback interfaces
    /// </summary>
    /// <returns>Array of IP addresses</returns>
    public string[] GetAllIPv4Addresses()
    {
        var interfaces = GetAllNetworkInterfaces();
        return interfaces
            .Where(i => !i.IsLoopback)
            .Select(i => i.IpAddress)
            .Distinct()
            .ToArray();
    }
}
