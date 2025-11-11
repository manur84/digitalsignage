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
    /// Get the server URL prefix based on SSL configuration
    /// </summary>
    public string GetUrlPrefix()
    {
        var protocol = EnableSsl ? "https" : "http";
        return $"{protocol}://+:{Port}{EndpointPath}";
    }

    /// <summary>
    /// Get the WebSocket protocol based on SSL configuration
    /// </summary>
    public string GetWebSocketProtocol()
    {
        return EnableSsl ? "wss" : "ws";
    }
}
