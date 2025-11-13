using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.Configuration;
using DigitalSignage.Server.Helpers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;

namespace DigitalSignage.Server.Services;

public class WebSocketCommunicationService : ICommunicationService
{
    private readonly ConcurrentDictionary<string, WebSocket> _clients = new();
    private readonly ILogger<WebSocketCommunicationService> _logger;
    private readonly ServerSettings _settings;
    private HttpListener? _httpListener;
    private CancellationTokenSource? _cancellationTokenSource;

    public WebSocketCommunicationService(
        ILogger<WebSocketCommunicationService> logger,
        ServerSettings settings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
    public event EventHandler<ClientConnectedEventArgs>? ClientConnected;
    public event EventHandler<ClientDisconnectedEventArgs>? ClientDisconnected;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _cancellationTokenSource = new CancellationTokenSource();

            _httpListener = new HttpListener();

            // Check if URL ACL is configured and use appropriate prefix
            var useWildcard = UrlAclManager.IsUrlAclConfigured(_settings.Port);
            var urlPrefix = useWildcard ? _settings.GetUrlPrefix() : _settings.GetLocalhostPrefix();

            if (!useWildcard)
            {
                _logger.LogWarning("===================================================================");
                _logger.LogWarning("URL ACL NOT CONFIGURED - Running in localhost-only mode");
                _logger.LogWarning("===================================================================");
                _logger.LogWarning("");
                _logger.LogWarning("The server is running on localhost only and will NOT be accessible");
                _logger.LogWarning("from external clients or devices.");
                _logger.LogWarning("");
                _logger.LogWarning("To enable external access:");
                _logger.LogWarning("  1. Restart the application (it will prompt for configuration)");
                _logger.LogWarning("  2. Or run setup-urlacl.bat as Administrator");
                _logger.LogWarning("");
                _logger.LogWarning("===================================================================");
            }

            _httpListener.Prefixes.Add(urlPrefix);

            // Log SSL configuration warning if SSL is enabled
            if (_settings.EnableSsl)
            {
                _logger.LogWarning("SSL/TLS is enabled. Ensure SSL certificate is properly configured.");
                _logger.LogWarning("For Windows: Use 'netsh http add sslcert' to bind certificate to port {Port}", _settings.Port);
                _logger.LogWarning("For production: Consider using a reverse proxy (nginx/IIS) for SSL termination");

                if (string.IsNullOrWhiteSpace(_settings.CertificateThumbprint) &&
                    string.IsNullOrWhiteSpace(_settings.CertificatePath))
                {
                    _logger.LogError("SSL enabled but no certificate configured. Server may fail to accept connections.");
                }
            }

            _httpListener.Start();

            var protocol = _settings.EnableSsl ? "HTTPS/WSS" : "HTTP/WS";
            var bindMode = useWildcard ? "all interfaces" : "localhost only";
            _logger.LogInformation("WebSocket server started on port {Port} using {Protocol} ({BindMode})",
                _settings.Port, protocol, bindMode);
            _logger.LogInformation("WebSocket endpoint: {Endpoint}", urlPrefix);

            _ = Task.Run(() => AcceptClientsAsync(_cancellationTokenSource.Token));

            await Task.CompletedTask;
        }
        catch (HttpListenerException ex) when (ex.ErrorCode == 5)
        {
            _logger.LogError("===================================================================");
            _logger.LogError("ACCESS DENIED - Cannot start WebSocket server on port {Port}", _settings.Port);
            _logger.LogError("===================================================================");
            _logger.LogError("");
            _logger.LogError("This error should not occur if URL ACL check is working properly.");
            _logger.LogError("");
            _logger.LogError("SOLUTION 1 (Recommended - One-time setup):");
            _logger.LogError("  1. Right-click setup-urlacl.bat");
            _logger.LogError("  2. Select 'Run as administrator'");
            _logger.LogError("  3. Restart the application normally (no admin needed)");
            _logger.LogError("");
            _logger.LogError("SOLUTION 2 (Temporary):");
            _logger.LogError("  Run this application as Administrator");
            _logger.LogError("");
            _logger.LogError("Manual setup command:");
            _logger.LogError("  netsh http add urlacl url={Prefix} user=Everyone", _settings.GetUrlPrefix());
            _logger.LogError("");
            _logger.LogError("===================================================================");

            // Try localhost fallback as last resort
            _logger.LogWarning("Attempting localhost fallback...");
            try
            {
                _httpListener?.Stop();
                _httpListener = new HttpListener();
                var localhostPrefix = _settings.GetLocalhostPrefix();
                _httpListener.Prefixes.Add(localhostPrefix);
                _httpListener.Start();

                _logger.LogWarning("Successfully started in localhost-only mode");
                _logger.LogInformation("WebSocket endpoint: {Endpoint}", localhostPrefix);

                _ = Task.Run(() => AcceptClientsAsync(_cancellationTokenSource.Token));
                return;
            }
            catch
            {
                _logger.LogError("Localhost fallback also failed");
                throw new InvalidOperationException(
                    $"Access Denied - Cannot start server on port {_settings.Port}. " +
                    $"Run setup-urlacl.bat as Administrator to fix this permanently, " +
                    $"or run this application as Administrator.",
                    ex);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start WebSocket server");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _cancellationTokenSource?.Cancel();
        _httpListener?.Stop();

        foreach (var client in _clients.Values)
        {
            await client.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Server shutting down",
                cancellationToken);
        }

        _clients.Clear();
    }

    public async Task SendMessageAsync(
        string clientId,
        Message message,
        CancellationToken cancellationToken = default)
    {
        if (_clients.TryGetValue(clientId, out var socket))
        {
            try
            {
                if (socket.State != WebSocketState.Open)
                {
                    _logger.LogWarning("Cannot send message to client {ClientId}: connection not open", clientId);
                    return;
                }

                var json = JsonConvert.SerializeObject(message);
                var bytes = Encoding.UTF8.GetBytes(json);
                await socket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    cancellationToken);

                _logger.LogDebug("Sent {MessageType} to client {ClientId}", message.Type, clientId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message to client {ClientId}", clientId);
                throw;
            }
        }
        else
        {
            _logger.LogWarning("Client {ClientId} not found", clientId);
        }
    }

    public async Task BroadcastMessageAsync(Message message, CancellationToken cancellationToken = default)
    {
        var json = JsonConvert.SerializeObject(message);
        var bytes = Encoding.UTF8.GetBytes(json);

        var tasks = _clients.Values.Select(socket =>
            socket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                cancellationToken));

        await Task.WhenAll(tasks);
    }

    public void UpdateClientId(string oldClientId, string newClientId)
    {
        if (_clients.TryRemove(oldClientId, out var socket))
        {
            _clients[newClientId] = socket;
            _logger.LogInformation("Updated WebSocket client ID mapping from {OldId} to {NewId}", oldClientId, newClientId);
        }
        else
        {
            _logger.LogWarning("Cannot update client ID: old client ID {OldId} not found in WebSocket connections", oldClientId);
        }
    }

    private async Task AcceptClientsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _httpListener != null)
        {
            try
            {
                var context = await _httpListener.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                {
                    var wsContext = await context.AcceptWebSocketAsync(null);
                    var clientId = Guid.NewGuid().ToString();
                    _clients[clientId] = wsContext.WebSocket;

                    var ipAddress = context.Request.RemoteEndPoint?.Address.ToString() ?? "unknown";
                    _logger.LogInformation("Client {ClientId} connected from {IpAddress}", clientId, ipAddress);

                    ClientConnected?.Invoke(this, new ClientConnectedEventArgs
                    {
                        ClientId = clientId,
                        IpAddress = ipAddress
                    });

                    _ = Task.Run(() => HandleClientAsync(clientId, wsContext.WebSocket, cancellationToken));
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
            catch (HttpListenerException ex)
            {
                _logger.LogError(ex, "HTTP listener error while accepting clients");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while accepting clients");
            }
        }
    }

    private async Task HandleClientAsync(
        string clientId,
        WebSocket socket,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[8192]; // 8KB buffer per read

        try
        {
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                // Use MemoryStream to accumulate data across multiple frames
                using var messageStream = new MemoryStream();
                WebSocketReceiveResult result;

                // Keep reading frames until we get the complete message (EndOfMessage = true)
                do
                {
                    result = await socket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    // Append this frame's data to the message stream
                    messageStream.Write(buffer, 0, result.Count);

                } while (!result.EndOfMessage);

                // If client closed connection, break out of the loop
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                // Convert the complete message to string
                var json = Encoding.UTF8.GetString(messageStream.ToArray());

                _logger.LogDebug("Received complete message from client {ClientId} ({ByteCount} bytes)",
                    clientId, messageStream.Length);

                // Deserialize to JObject first to read the Type field
                var jObject = Newtonsoft.Json.Linq.JObject.Parse(json);
                var messageType = jObject["Type"]?.ToString() ?? string.Empty;

                // Deserialize to concrete type based on Type field
                Message? message = messageType switch
                {
                    "REGISTER" => JsonConvert.DeserializeObject<RegisterMessage>(json),
                    "HEARTBEAT" => JsonConvert.DeserializeObject<HeartbeatMessage>(json),
                    "STATUS_REPORT" => JsonConvert.DeserializeObject<StatusReportMessage>(json),
                    "LOG" => JsonConvert.DeserializeObject<LogMessage>(json),
                    "SCREENSHOT" => JsonConvert.DeserializeObject<ScreenshotMessage>(json),
                    "UPDATE_CONFIG_RESPONSE" => JsonConvert.DeserializeObject<UpdateConfigResponseMessage>(json),
                    _ => null
                };

                if (message != null)
                {
                    // For REGISTER messages, use the ClientId from the message for the event
                    var eventClientId = clientId;
                    if (message is RegisterMessage registerMsg && !string.IsNullOrWhiteSpace(registerMsg.ClientId))
                    {
                        eventClientId = registerMsg.ClientId;

                        // Update the WebSocket client mapping if the client ID is different
                        if (eventClientId != clientId)
                        {
                            _logger.LogInformation("Client registering with ID {RegisteredId} (WebSocket connection ID: {ConnectionId})",
                                eventClientId, clientId);
                            UpdateClientId(clientId, eventClientId);
                            clientId = eventClientId; // Use the registered ID for the rest of the connection
                        }
                    }

                    MessageReceived?.Invoke(this, new MessageReceivedEventArgs
                    {
                        ClientId = eventClientId,
                        Message = message
                    });
                }
                else
                {
                    _logger.LogWarning("Unknown message type '{MessageType}' from client {ClientId}", messageType, clientId);
                }
            }
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
            _logger.LogInformation("Client {ClientId} closed connection without completing handshake", clientId);
        }
        catch (WebSocketException ex)
        {
            _logger.LogWarning(ex, "WebSocket error for client {ClientId}: {ErrorCode}", clientId, ex.WebSocketErrorCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error handling client {ClientId}", clientId);
        }
        finally
        {
            _clients.TryRemove(clientId, out _);
            _logger.LogInformation("Client {ClientId} disconnected", clientId);

            ClientDisconnected?.Invoke(this, new ClientDisconnectedEventArgs
            {
                ClientId = clientId,
                Reason = "Connection closed"
            });

            if (socket.State == WebSocketState.Open)
            {
                await socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Closing",
                    CancellationToken.None);
            }
            socket.Dispose();
        }
    }
}
