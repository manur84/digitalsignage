using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DigitalSignage.Server.Configuration;
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
        _logger.LogInformation("Discovery Service starting on UDP port {Port}", DiscoveryPort);

        try
        {
            _udpListener = new UdpClient(DiscoveryPort);
            _udpListener.EnableBroadcast = true;
            _logger.LogInformation("Discovery Service listening for broadcast requests");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _udpListener.ReceiveAsync(stoppingToken);
                    var message = Encoding.UTF8.GetString(result.Buffer);

                    if (message.StartsWith(DiscoveryRequest))
                    {
                        _logger.LogInformation("Discovery request received from {RemoteEndPoint}", result.RemoteEndPoint);
                        await SendDiscoveryResponseAsync(result.RemoteEndPoint, stoppingToken);
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

            var json = JsonConvert.SerializeObject(response);
            var bytes = Encoding.UTF8.GetBytes(json);

            // Send response back to the requesting client
            using var responseClient = new UdpClient();
            await responseClient.SendAsync(bytes, bytes.Length, remoteEndPoint);

            _logger.LogInformation("Discovery response sent to {RemoteEndPoint}: {ServerUrls}",
                remoteEndPoint,
                string.Join(", ", localIPs.Select(ip => $"{protocol}://{ip}:{port}/{endpointPath}")));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending discovery response to {RemoteEndPoint}", remoteEndPoint);
        }
    }

    private static string[] GetLocalIPAddresses()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            return host.AddressList
                .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork) // IPv4 only
                .Select(ip => ip.ToString())
                .ToArray();
        }
        catch
        {
            return new[] { "127.0.0.1" };
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Discovery Service stopping...");
        _udpListener?.Close();
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
