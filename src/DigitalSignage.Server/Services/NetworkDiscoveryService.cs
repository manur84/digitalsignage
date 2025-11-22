using Makaretu.Dns;
using Microsoft.Extensions.Hosting;
using DigitalSignage.Core.Models;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Server.Configuration;
using Serilog;
using System.Net;
using System.Net.Sockets;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Broadcasts server availability via mDNS/Bonjour for auto-discovery
/// </summary>
public class NetworkDiscoveryService : BackgroundService, INetworkDiscoveryService
{
    private readonly ILogger _logger;
    private readonly ServerSettings _settings;
    private readonly IClientService? _clientService;
    private readonly ServiceDiscovery _serviceDiscovery;
    private ServiceProfile? _serviceProfile;

    // Service type for mDNS (follows DNS-SD naming convention)
    private const string ServiceType = "_digitalsignage._tcp";
    private const string ServiceDomain = "local";

    public NetworkDiscoveryService(
        ServerSettings settings,
        IClientService? clientService = null)
    {
        _logger = Log.ForContext<NetworkDiscoveryService>();
        _settings = settings;
        _clientService = clientService;
        _serviceDiscovery = new ServiceDiscovery();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await StartAsync(stoppingToken);

        // Keep running until stopped
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    public new async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var serverInfo = await GetServerInfoAsync();

            // Create service profile
            _serviceProfile = new ServiceProfile(
                instanceName: serverInfo.Name,
                serviceName: ServiceType,
                port: (ushort)serverInfo.Port,
                addresses: serverInfo.IpAddresses.Select(ip => IPAddress.Parse(ip))
            );

            // Add TXT records with metadata
            _serviceProfile.Resources.Add(new TXTRecord
            {
                Name = _serviceProfile.FullyQualifiedName,
                Strings =
                {
                    $"version={serverInfo.Version}",
                    $"ssl={serverInfo.UsesSsl}",
                    $"clients={serverInfo.ConnectedClients}",
                    $"url={serverInfo.WebSocketUrl}"
                }
            });

            // Advertise service
            _serviceDiscovery.Advertise(_serviceProfile);

            _logger.Information(
                "mDNS service started: {ServiceName} on port {Port}",
                _serviceProfile.FullyQualifiedName,
                serverInfo.Port);

            _logger.Information("Server discoverable at: {Url}", serverInfo.WebSocketUrl);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to start mDNS service");
        }

        await Task.CompletedTask;
    }

    public new async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_serviceProfile != null)
            {
                _serviceDiscovery.Unadvertise(_serviceProfile);
                _logger.Information("mDNS service stopped");
            }

            _serviceDiscovery.Dispose();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error stopping mDNS service");
        }

        await Task.CompletedTask;
    }

    public async Task UpdateAdvertisementAsync()
    {
        try
        {
            if (_serviceProfile != null)
            {
                var serverInfo = await GetServerInfoAsync();

                // Update TXT records
                _serviceProfile.Resources.Clear();
                _serviceProfile.Resources.Add(new TXTRecord
                {
                    Name = _serviceProfile.FullyQualifiedName,
                    Strings =
                    {
                        $"version={serverInfo.Version}",
                        $"ssl={serverInfo.UsesSsl}",
                        $"clients={serverInfo.ConnectedClients}",
                        $"url={serverInfo.WebSocketUrl}"
                    }
                });

                // Re-advertise with updated info
                _serviceDiscovery.Announce(_serviceProfile);

                _logger.Debug("Updated mDNS advertisement with {Clients} clients", serverInfo.ConnectedClients);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error updating mDNS advertisement");
        }
    }

    public async Task<ServerInfo> GetServerInfoAsync()
    {
        // Get server hostname
        var hostname = Dns.GetHostName();

        // Get local IP addresses (IPv4 only for simplicity)
        var ipAddresses = Dns.GetHostEntry(hostname)
            .AddressList
            .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
            .Select(ip => ip.ToString())
            .ToList();

        // Fallback to localhost if no network interfaces found
        if (ipAddresses.Count == 0)
        {
            ipAddresses.Add("127.0.0.1");
            _logger.Warning("No network interfaces found, using localhost");
        }

        // Get WebSocket port from configuration
        var port = _settings.Port;
        var usesSsl = _settings.EnableSsl;

        // Get connected clients count
        var connectedClients = 0;
        if (_clientService != null)
        {
            try
            {
                var result = await _clientService.GetAllClientsAsync();
                if (result.IsSuccess)
                {
                    connectedClients = result.Value?.Count(c => c.Status == ClientStatus.Online) ?? 0;
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to get connected clients count for mDNS advertisement");
            }
        }

        // Build WebSocket URL (use first IP address)
        var protocol = usesSsl ? "wss" : "ws";
        var primaryIp = ipAddresses.FirstOrDefault() ?? "localhost";
        var webSocketUrl = $"{protocol}://{primaryIp}:{port}{_settings.EndpointPath}";

        return new ServerInfo
        {
            Name = $"DigitalSignage-{hostname}",
            Hostname = hostname,
            WebSocketUrl = webSocketUrl,
            Version = GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0",
            ConnectedClients = connectedClients,
            IpAddresses = ipAddresses,
            Port = port,
            UsesSsl = usesSsl
        };
    }

    public override void Dispose()
    {
        try
        {
            if (_serviceProfile != null)
            {
                _serviceDiscovery.Unadvertise(_serviceProfile);
            }
            _serviceDiscovery?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error disposing NetworkDiscoveryService");
        }

        base.Dispose();
    }
}
