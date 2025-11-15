using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DigitalSignage.Server.Configuration;
using Makaretu.Dns;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DigitalSignage.Server.Services;

/// <summary>
/// mDNS/Zeroconf service discovery for Digital Signage server.
/// Announces the server on the local network using multicast DNS.
/// This complements UDP broadcast discovery and works better across subnets.
/// </summary>
public class MdnsDiscoveryService : BackgroundService
{
    private readonly ILogger<MdnsDiscoveryService> _logger;
    private readonly ServerSettings _serverSettings;
    private ServiceDiscovery? _serviceDiscovery;
    private ServiceProfile? _serviceProfile;

    private const string ServiceType = "_digitalsignage._tcp";

    public MdnsDiscoveryService(
        ILogger<MdnsDiscoveryService> logger,
        IOptions<ServerSettings> serverSettings)
    {
        _logger = logger;
        _serverSettings = serverSettings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("=" + new string('=', 69));
        _logger.LogInformation("MDNS DISCOVERY SERVICE STARTING");
        _logger.LogInformation("=" + new string('=', 69));

        try
        {
            // Create service discovery instance
            _serviceDiscovery = new ServiceDiscovery();
            _logger.LogInformation("ServiceDiscovery instance created");

            // Get local hostname
            var hostname = Dns.GetHostName();
            var serviceName = $"{hostname} Digital Signage";

            // Get server configuration
            var port = (ushort)_serverSettings.Port;
            var protocol = _serverSettings.GetWebSocketProtocol();
            var endpointPath = _serverSettings.EndpointPath?.TrimStart('/') ?? "ws";
            var sslEnabled = _serverSettings.EnableSsl;

            // Get local IP addresses
            var localIPs = GetLocalIPAddresses();

            _logger.LogInformation("mDNS Service Configuration:");
            _logger.LogInformation("  Service Name: {ServiceName}", serviceName);
            _logger.LogInformation("  Service Type: {ServiceType}.local.", ServiceType);
            _logger.LogInformation("  Hostname: {Hostname}", hostname);
            _logger.LogInformation("  Port: {Port}", port);
            _logger.LogInformation("  Protocol: {Protocol}", protocol.ToUpper());
            _logger.LogInformation("  Endpoint: /{EndpointPath}", endpointPath);
            _logger.LogInformation("  SSL: {SslEnabled}", sslEnabled ? "Enabled" : "Disabled");
            _logger.LogInformation("  Local IPs: {IpCount}", localIPs.Length);

            // Create service profile
            _serviceProfile = new ServiceProfile(
                serviceName,
                ServiceType,
                port
            );

            // Add TXT records with server metadata
            _serviceProfile.AddProperty("protocol", protocol);
            _serviceProfile.AddProperty("endpoint", endpointPath);
            _serviceProfile.AddProperty("ssl_enabled", sslEnabled.ToString().ToLower());
            _serviceProfile.AddProperty("version", "1.0.0");
            _serviceProfile.AddProperty("server_name", hostname);

            // Add all local IP addresses to the service profile
            _logger.LogInformation("Adding IP addresses to mDNS service:");
            foreach (var ipAddress in localIPs)
            {
                try
                {
                    _serviceProfile.Resources.Add(new ARecord
                    {
                        Name = _serviceProfile.HostName,
                        Address = ipAddress
                    });
                    _logger.LogInformation("  ✓ Added IP: {IpAddress}", ipAddress);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "  ✗ Failed to add IP address {IpAddress} to mDNS service", ipAddress);
                }
            }

            // Advertise the service
            _logger.LogInformation("Advertising mDNS service on the network...");
            _serviceDiscovery.Advertise(_serviceProfile);

            _logger.LogInformation("=" + new string('=', 69));
            _logger.LogInformation("✓ MDNS SERVICE REGISTERED SUCCESSFULLY");
            _logger.LogInformation("=" + new string('=', 69));
            _logger.LogInformation("Discoverable as: {ServiceName}.{ServiceType}.local.",
                serviceName, ServiceType);
            _logger.LogInformation("Full service name: {FullServiceName}",
                $"{serviceName}.{ServiceType}.local.");
            _logger.LogInformation("Advertised IPs: {IpAddresses}",
                string.Join(", ", localIPs.Select(ip => ip.ToString())));
            _logger.LogInformation("Clients can now discover this server via mDNS/Zeroconf");
            _logger.LogInformation("=" + new string('=', 69));

            // Keep the service running until cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
            _logger.LogInformation("mDNS Discovery Service shutting down...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in mDNS Discovery Service");
            throw;
        }
        finally
        {
            // Clean up
            if (_serviceProfile != null && _serviceDiscovery != null)
            {
                try
                {
                    _serviceDiscovery.Unadvertise(_serviceProfile);
                    _logger.LogInformation("mDNS service unregistered");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error unregistering mDNS service");
                }
            }

            _serviceDiscovery?.Dispose();
            _logger.LogInformation("mDNS Discovery Service stopped");
        }
    }

    /// <summary>
    /// Get all local IPv4 addresses
    /// </summary>
    private static IPAddress[] GetLocalIPAddresses()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            return host.AddressList
                .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork) // IPv4 only
                .ToArray();
        }
        catch
        {
            // Fallback to localhost if we can't get network interfaces
            return new[] { IPAddress.Loopback };
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("mDNS Discovery Service stopping...");

        // Unadvertise service before stopping
        if (_serviceProfile != null && _serviceDiscovery != null)
        {
            try
            {
                _serviceDiscovery.Unadvertise(_serviceProfile);
                _logger.LogInformation("mDNS service unadvertised");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error unadvertising mDNS service during shutdown");
            }
        }

        // Dispose ServiceDiscovery
        _serviceDiscovery?.Dispose();

        return base.StopAsync(cancellationToken);
    }
}
