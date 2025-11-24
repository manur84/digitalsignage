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
    /// WSS-ONLY MODE: This MUST be true. Server only accepts secure WebSocket connections.
    /// 
    /// IMPORTANT: While this property defaults to true and the protocol methods always return
    /// secure variants, this property MUST remain in the configuration for validation purposes.
    /// The WebSocketCommunicationService explicitly checks this at startup and throws an exception
    /// if it's false, ensuring users cannot accidentally disable SSL.
    /// </summary>
    public bool EnableSsl { get; set; } = true;

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
    /// Application ID (GUID) for SSL binding via netsh
    /// Used by Windows HTTP.SYS to identify the application that owns the SSL binding
    /// </summary>
    public string SslAppId { get; set; } = "12345678-1234-1234-1234-123456789ABC";

    /// <summary>
    /// Automatically configure SSL binding via netsh on startup
    /// Requires Administrator privileges
    /// </summary>
    public bool AutoConfigureSslBinding { get; set; } = true;

    /// <summary>
    /// Automatically configure URL ACL via netsh on startup
    /// Requires Administrator privileges
    /// </summary>
    public bool AutoConfigureUrlAcl { get; set; } = true;

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
    /// Get the server URL prefix - ALWAYS HTTPS (WSS-only mode)
    /// 
    /// NOTE: Returns HTTPS regardless of EnableSsl setting value.
    /// The EnableSsl property is still validated at startup to ensure it's true,
    /// but this method enforces secure protocol at the code level.
    /// </summary>
    public string GetUrlPrefix()
    {
        // WSS-ONLY: Always use HTTPS regardless of EnableSsl setting
        const string protocol = "https";
        return $"{protocol}://+:{Port}{EndpointPath}";
    }

    /// <summary>
    /// Get the localhost URL prefix - ALWAYS HTTPS (WSS-only mode)
    /// Used when URL ACL is not configured
    /// </summary>
    public string GetLocalhostPrefix()
    {
        // WSS-ONLY: Always use HTTPS regardless of EnableSsl setting
        const string protocol = "https";
        return $"{protocol}://localhost:{Port}{EndpointPath}";
    }

    /// <summary>
    /// Get the URL prefix for a specific IP address - ALWAYS HTTPS (WSS-only mode)
    /// </summary>
    /// <param name="ipAddress">IP address to bind to</param>
    /// <returns>URL prefix for the specific IP address</returns>
    public string GetIpSpecificPrefix(string ipAddress)
    {
        // WSS-ONLY: Always use HTTPS regardless of EnableSsl setting
        const string protocol = "https";
        return $"{protocol}://{ipAddress}:{Port}{EndpointPath}";
    }

    /// <summary>
    /// Get the WebSocket protocol - ALWAYS WSS (WebSocket Secure)
    /// WSS-ONLY MODE: Server only accepts secure WebSocket connections
    /// 
    /// NOTE: Returns "wss" regardless of EnableSsl setting value.
    /// The EnableSsl property is still validated at startup to ensure it's true,
    /// but this method enforces secure protocol at the code level.
    /// </summary>
    public string GetWebSocketProtocol()
    {
        // WSS-ONLY: Always return "wss" regardless of EnableSsl setting
        return "wss";
    }
}
