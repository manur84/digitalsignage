using DigitalSignage.Server.Utilities;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DigitalSignage.Server.Configuration;
using DigitalSignage.Core.Utilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Service for automatic network discovery of Digital Signage server via UDP broadcast.
/// Clients can send discovery requests to port 5555 and receive server connection information.
/// </summary>
public class DiscoveryService : BackgroundService
{
    private readonly ILogger<DiscoveryService> _logger;
    private readonly ServerSettings _serverSettings;
    private UdpClient? _udpListener;
    private const int DiscoveryPort = 5555;
    private const string DiscoveryRequest = "DIGITALSIGNAGE_DISCOVER";
    private const string DiscoveryResponsePrefix = "DIGITALSIGNAGE_SERVER";

    public DiscoveryService(
        ILogger<DiscoveryService> logger,
        IOptions<ServerSettings> serverSettings)
    {
        _logger = logger;
        _serverSettings = serverSettings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("=" + new string('=', 69));
        _logger.LogInformation("UDP DISCOVERY SERVICE STARTING");
        _logger.LogInformation("=" + new string('=', 69));
        _logger.LogInformation("UDP Port: {Port}", DiscoveryPort);
        _logger.LogInformation("Discovery Request String: {Request}", DiscoveryRequest);
        _logger.LogInformation("Response Prefix: {Prefix}", DiscoveryResponsePrefix);
        _logger.LogInformation("=" + new string('=', 69));

        try
        {
            _udpListener = new UdpClient(DiscoveryPort);
            _udpListener.EnableBroadcast = true;
            _logger.LogInformation("UDP listener created and bound to port {Port}", DiscoveryPort);
            _logger.LogInformation("Broadcast enabled: True");
            _logger.LogInformation("Waiting for discovery requests from clients...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _udpListener.ReceiveAsync(stoppingToken);
                    var message = Encoding.UTF8.GetString(result.Buffer);

                    _logger.LogDebug("Received UDP message: '{Message}' from {RemoteEndPoint}", message, result.RemoteEndPoint);

                    if (message.StartsWith(DiscoveryRequest))
                    {
                        _logger.LogInformation("✓ Valid discovery request received from {RemoteEndPoint}", result.RemoteEndPoint);
                        await SendDiscoveryResponseAsync(result.RemoteEndPoint, stoppingToken);
                    }
                    else
                    {
                        _logger.LogWarning("✗ Invalid discovery message received from {RemoteEndPoint}: '{Message}'", result.RemoteEndPoint, message);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Normal shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing discovery request");
                    // Continue listening for next request
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Discovery Service");
            throw;
        }
        finally
        {
            _udpListener?.Close();
            _udpListener?.Dispose();
            _logger.LogInformation("Discovery Service stopped");
        }
    }

    private async Task SendDiscoveryResponseAsync(IPEndPoint remoteEndPoint, CancellationToken cancellationToken)
    {
        try
        {
            // Get local IP addresses
            var localIPs = GetLocalIPAddresses();
            var protocol = _serverSettings.EnableSsl ? "wss" : "ws";
            var port = _serverSettings.Port;
            var endpointPath = _serverSettings.EndpointPath?.TrimStart('/') ?? "ws";

            var response = new DiscoveryResponse
            {
                Type = DiscoveryResponsePrefix,
                ServerName = Environment.MachineName,
                LocalIPs = localIPs,
                Port = port,
                Protocol = protocol,
                EndpointPath = endpointPath,
                SslEnabled = _serverSettings.EnableSsl,
                Timestamp = DateTime.UtcNow
            };

            var json = JsonConvert.SerializeObject(response, Formatting.Indented);
            var bytes = Encoding.UTF8.GetBytes(json);

            _logger.LogDebug("Preparing discovery response: {JsonSize} bytes", bytes.Length);
            _logger.LogDebug("Response content: {JsonContent}", json);

            // Send response back to the requesting client
            using var responseClient = new UdpClient();
            await responseClient.SendAsync(bytes, bytes.Length, remoteEndPoint);

            var serverUrls = localIPs.Select(ip => $"{protocol}://{ip}:{port}/{endpointPath}").ToArray();
            _logger.LogInformation("✓ Discovery response sent to {RemoteEndPoint}", remoteEndPoint);
            _logger.LogInformation("  Server: {ServerName}", Environment.MachineName);
            _logger.LogInformation("  Protocol: {Protocol}, Port: {Port}, SSL: {SslEnabled}", protocol.ToUpper(), port, _serverSettings.EnableSsl);
            _logger.LogInformation("  Available URLs ({Count}): {ServerUrls}", serverUrls.Length, string.Join(", ", serverUrls));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending discovery response to {RemoteEndPoint}", remoteEndPoint);
        }
    }

    private static string[] GetLocalIPAddresses()
    {
        return NetworkUtilities.GetLocalIPv4AddressStrings();
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Discovery Service stopping...");
        _udpListener?.Close();
        _udpListener?.Dispose();
        return base.StopAsync(cancellationToken);
    }
}

/// <summary>
/// Response message sent to clients during discovery
/// </summary>
public class DiscoveryResponse
{
    public string Type { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string[] LocalIPs { get; set; } = Array.Empty<string>();
    public int Port { get; set; }
    public string Protocol { get; set; } = string.Empty;
    public string EndpointPath { get; set; } = string.Empty;
    public bool SslEnabled { get; set; }
    public DateTime Timestamp { get; set; }
}
