using System.Net.Sockets;
using System.Net;
using Serilog;

namespace DigitalSignage.Server.Configuration;

/// <summary>
/// Server configuration settings
/// </summary>
public class ServerSettings
{
    /// <summary>
    /// WebSocket server port
    /// </summary>
    public int Port { get; set; } = 8080;

    /// <summary>
    /// Enable automatic port selection if configured port is in use
    /// </summary>
    public bool AutoSelectPort { get; set; } = true;

    /// <summary>
    /// Alternative ports to try if main port is in use
    /// </summary>
    public int[] AlternativePorts { get; set; } = new[] { 8081, 8082, 8083, 8888, 9000 };

    /// <summary>
    /// Enable HTTPS/WSS (requires SSL certificate configuration)
    /// </summary>
    public bool EnableSsl { get; set; } = false;

    /// <summary>
    /// SSL certificate thumbprint (for Windows certificate store)
    /// </summary>
    public string? CertificateThumbprint { get; set; }

    /// <summary>
    /// Path to SSL certificate file (.pfx)
    /// </summary>
    public string? CertificatePath { get; set; }

    /// <summary>
    /// SSL certificate password (if using .pfx file)
    /// </summary>
    public string? CertificatePassword { get; set; }

    /// <summary>
    /// WebSocket endpoint path
    /// </summary>
    public string EndpointPath { get; set; } = "/ws/";

    /// <summary>
    /// Maximum message size in bytes
    /// </summary>
    public int MaxMessageSize { get; set; } = 1024 * 1024; // 1 MB

    /// <summary>
    /// Client heartbeat timeout in seconds
    /// </summary>
    public int ClientHeartbeatTimeout { get; set; } = 90;

    /// <summary>
    /// Preferred network interface for WebSocket server binding and discovery
    /// Can be interface name (e.g., "Ethernet", "Wi-Fi") or IP address
    /// Empty or null = auto-select first available non-localhost interface
    /// </summary>
    public string? PreferredNetworkInterface { get; set; }

    /// <summary>
    /// Get an available port, either the configured port or an alternative
    /// </summary>
    public int GetAvailablePort()
    {
        // Try configured port first
        if (IsPortAvailable(Port))
        {
            Log.Information("Using configured port {Port}", Port);
            return Port;
        }

        if (!AutoSelectPort)
        {
            throw new InvalidOperationException(
                $"Port {Port} is in use and AutoSelectPort is disabled. " +
                $"Please free port {Port} or enable AutoSelectPort in appsettings.json");
        }

        Log.Warning("Port {Port} is in use, trying alternative ports...", Port);

        // Port in use, try alternatives
        foreach (var port in AlternativePorts)
        {
            if (IsPortAvailable(port))
            {
                Log.Warning("Using alternative port {Port} (configured port {ConfiguredPort} was in use)", port, Port);
                return port;
            }
        }

        throw new InvalidOperationException(
            $"No available ports found. Ports tried: {Port}, {string.Join(", ", AlternativePorts)}. " +
            $"Please free one of these ports or configure different ports in appsettings.json");
    }

    /// <summary>
    /// Check if a port is available for listening
    /// </summary>
    private bool IsPortAvailable(int port)
    {
        TcpListener? listener = null;
        try
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        finally
        {
            listener?.Stop();
        }
    }

    /// <summary>
    /// Get the server URL prefix based on SSL configuration
    /// </summary>
    public string GetUrlPrefix()
    {
        var protocol = EnableSsl ? "https" : "http";
        return $"{protocol}://+:{Port}{EndpointPath}";
    }

    /// <summary>
    /// Get the localhost URL prefix based on SSL configuration
    /// Used when URL ACL is not configured
    /// </summary>
    public string GetLocalhostPrefix()
    {
        var protocol = EnableSsl ? "https" : "http";
        return $"{protocol}://localhost:{Port}{EndpointPath}";
    }

    /// <summary>
    /// Get the URL prefix for a specific IP address
    /// </summary>
    /// <param name="ipAddress">IP address to bind to</param>
    /// <returns>URL prefix for the specific IP address</returns>
    public string GetIpSpecificPrefix(string ipAddress)
    {
        var protocol = EnableSsl ? "https" : "http";
        return $"{protocol}://{ipAddress}:{Port}{EndpointPath}";
    }

    /// <summary>
    /// Get the WebSocket protocol based on SSL configuration
    /// </summary>
    public string GetWebSocketProtocol()
    {
        return EnableSsl ? "wss" : "ws";
    }
}
