using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.Configuration;
using DigitalSignage.Server.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using JsonException = Newtonsoft.Json.JsonException;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace DigitalSignage.Server.Services;

public class WebSocketCommunicationService : ICommunicationService, IDisposable
{
    private readonly ConcurrentDictionary<string, SslWebSocketConnection> _clients = new();
    private readonly ConcurrentDictionary<string, Task> _clientHandlerTasks = new();

    // Mobile App Connections (separate from Pi clients)
    private readonly ConcurrentDictionary<string, SslWebSocketConnection> _mobileAppConnections = new();
    private readonly ConcurrentDictionary<string, Guid> _mobileAppIds = new(); // Maps connection ID to app ID
    private readonly ConcurrentDictionary<string, string> _mobileAppTokens = new(); // Maps connection ID to token

    private readonly ILogger<WebSocketCommunicationService> _logger;
    private readonly WebSocketMessageSerializer _messageSerializer;
    private readonly ServerSettings _settings;
    private readonly IServiceProvider _serviceProvider; // For scoped service access
    private readonly ICertificateService _certificateService;
    private TcpListener? _tcpListener;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _acceptClientsTask;
    private bool _disposed = false;
    private int _currentPort;

    public WebSocketCommunicationService(
        ILogger<WebSocketCommunicationService> logger,
        ILogger<WebSocketMessageSerializer> serializerLogger,
        ServerSettings settings,
        IServiceProvider serviceProvider,
        ICertificateService certificateService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _certificateService = certificateService ?? throw new ArgumentNullException(nameof(certificateService));
        _messageSerializer = new WebSocketMessageSerializer(serializerLogger, enableCompression: true);
    }

    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
    public event EventHandler<ClientConnectedEventArgs>? ClientConnected;
    public event EventHandler<ClientDisconnectedEventArgs>? ClientDisconnected;
    public event EventHandler<MobileAppRegistration>? OnNewAppRegistration;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            _cancellationTokenSource = new CancellationTokenSource();

            // WSS-ONLY: SSL MUST be enabled
            if (!_settings.EnableSsl)
            {
                _logger.LogError("===================================================================");
                _logger.LogError("SSL MUST BE ENABLED FOR WSS-ONLY MODE");
                _logger.LogError("===================================================================");
                _logger.LogError("");
                _logger.LogError("This server now ONLY supports WSS (WebSocket Secure).");
                _logger.LogError("Unencrypted WS connections are no longer supported.");
                _logger.LogError("");
                _logger.LogError("SOLUTION:");
                _logger.LogError("  - Set EnableSsl=true in appsettings.json");
                _logger.LogError("  - A self-signed certificate will be generated automatically");
                _logger.LogError("");
                _logger.LogError("===================================================================");

                throw new InvalidOperationException("WSS-ONLY mode requires SSL to be enabled. Set EnableSsl=true in appsettings.json");
            }

            // Load or generate SSL certificate
            var certificate = _certificateService.GetOrCreateServerCertificate();
            if (certificate == null)
            {
                _logger.LogError("===================================================================");
                _logger.LogError("FAILED TO LOAD SSL CERTIFICATE");
                _logger.LogError("===================================================================");
                _logger.LogError("");
                _logger.LogError("Could not load or generate SSL certificate.");
                _logger.LogError("");
                _logger.LogError("Check:");
                _logger.LogError("  - Certificate path: {CertPath}", _settings.CertificatePath);
                _logger.LogError("  - Certificate password is correct");
                _logger.LogError("  - Write permissions to certs/ directory");
                _logger.LogError("");
                _logger.LogError("===================================================================");

                throw new InvalidOperationException("SSL certificate required for WSS server");
            }

            // Get available port (with fallback support)
            _currentPort = _settings.GetAvailablePort();
            if (_currentPort != _settings.Port)
            {
                _logger.LogInformation("Port fallback: Configured port {ConfiguredPort} in use, using {ActualPort} instead",
                    _settings.Port, _currentPort);
            }

            _logger.LogInformation("===================================================================");
            _logger.LogInformation("STARTING WSS (WEBSOCKET SECURE) SERVER");
            _logger.LogInformation("===================================================================");
            _logger.LogInformation("");
            _logger.LogInformation("Transport: TcpListener + SslStream + WebSocket Protocol");
            _logger.LogInformation("Port: {Port}", _currentPort);
            _logger.LogInformation("Certificate: {Subject}", certificate.Subject);
            _logger.LogInformation("Certificate Thumbprint: {Thumbprint}", certificate.Thumbprint);
            _logger.LogInformation("Valid Until: {NotAfter}", certificate.NotAfter);
            _logger.LogInformation("");
            _logger.LogInformation("Client Configuration:");
            _logger.LogInformation("  - Python clients: use_ssl=True, verify_ssl=False (self-signed)");
            _logger.LogInformation("  - Mobile apps: Accept self-signed certificates");
            _logger.LogInformation("");
            _logger.LogInformation("===================================================================");

            // Start TcpListener on all interfaces
            _tcpListener = new TcpListener(IPAddress.Any, _currentPort);
            _tcpListener.Start();

            _logger.LogInformation("WSS server listening on wss://0.0.0.0:{Port}/ws", _currentPort);

            // Show all available connection URLs
            var localIPs = System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName())
                .Where(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            _logger.LogInformation("Clients can connect using:");
            foreach (var ip in localIPs)
            {
                _logger.LogInformation("  - wss://{IP}:{Port}/ws", ip, _currentPort);
            }

            // Start accepting clients
            _acceptClientsTask = Task.Run(() => AcceptClientsAsync(_cancellationTokenSource.Token, certificate));

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start WSS server");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        _logger.LogInformation("Stopping WSS communication service...");

        // 1. Cancel accept loop
        _cancellationTokenSource?.Cancel();

        // 2. Stop TcpListener
        if (_tcpListener != null)
        {
            try
            {
                _tcpListener.Stop();
                _logger.LogDebug("TcpListener stopped");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping TcpListener");
            }
        }

        // 3. Close all client connections gracefully
        var connectionsSnapshot = _clients.Values.ToList();
        var closeTasks = connectionsSnapshot.Select(async connection =>
        {
            try
            {
                await connection.CloseAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing client connection during shutdown");
            }
        });

        try
        {
            await Task.WhenAll(closeTasks);
            _logger.LogInformation("All {Count} client connections closed", connectionsSnapshot.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error waiting for client connections to close");
        }

        // 4. Dispose all connections and clear dictionary
        foreach (var connection in connectionsSnapshot)
        {
            try { connection.Dispose(); } catch { }
        }

        // 5. Wait for accept clients task to complete (with 10s timeout)
        if (_acceptClientsTask != null)
        {
            try
            {
                await _acceptClientsTask.WaitAsync(TimeSpan.FromSeconds(10));
                _logger.LogDebug("Accept clients task stopped gracefully");
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Accept clients task did not stop within timeout");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error waiting for accept clients task to complete");
            }
        }

        // 6. Wait for all client handler tasks to complete (with 10s timeout)
        var handlerTasks = _clientHandlerTasks.Values.ToArray();
        if (handlerTasks.Length > 0)
        {
            try
            {
                _logger.LogDebug("Waiting for {Count} client handler tasks to complete", handlerTasks.Length);
                await Task.WhenAll(handlerTasks).WaitAsync(TimeSpan.FromSeconds(10));
                _logger.LogDebug("All client handler tasks stopped gracefully");
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("{Count} client handler tasks did not stop within timeout", handlerTasks.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error waiting for client handler tasks to complete");
            }
        }

        // 7. Clear clients dictionary and handler tasks
        _clients.Clear();
        _clientHandlerTasks.Clear();

        // 8. Dispose cancellation token source
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _acceptClientsTask = null;

        _logger.LogInformation("WSS communication service stopped successfully");
    }

    public async Task SendMessageAsync(
        string clientId,
        Message message,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        _logger.LogDebug("SendMessageAsync called: clientId={ClientId}, messageType={MessageType}",
            clientId, message?.Type ?? "null");

        if (_clients.TryGetValue(clientId, out var connection))
        {
            try
            {
                if (!connection.IsConnected)
                {
                    _logger.LogWarning("Cannot send message to client {ClientId}: connection not open", clientId);
                    return;
                }

                _logger.LogDebug("Serializing message type {MessageType} for client {ClientId}", message.Type, clientId);

                var settings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    NullValueHandling = NullValueHandling.Ignore,
                    DefaultValueHandling = DefaultValueHandling.Include,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    MaxDepth = 32
                };

                var json = JsonConvert.SerializeObject(message, settings);

                _logger.LogDebug("Serialized message {MessageType}: {Length} bytes", message.Type, json.Length);

                await connection.SendTextAsync(json, cancellationToken);

                _logger.LogDebug("Successfully sent message {MessageType} to client {ClientId}", message.Type, clientId);
            }
            catch (JsonSerializationException jsonEx)
            {
                _logger.LogError(jsonEx, "JSON serialization error for message type {MessageType} to client {ClientId}",
                    message?.Type, clientId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message to client {ClientId}", clientId);
                throw;
            }
        }
        else
        {
            _logger.LogWarning("Client {ClientId} not found in connections dictionary (have {Count} clients)",
                clientId, _clients.Count);
        }
    }

    public async Task BroadcastMessageAsync(Message message, CancellationToken cancellationToken = default)
    {
        var settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Include
        };
        var json = JsonConvert.SerializeObject(message, settings);

        // Send to all connected clients
        var tasks = _clients.Values.Select(async connection =>
        {
            try
            {
                if (connection.IsConnected)
                {
                    await connection.SendTextAsync(json, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast to connection {ConnectionId}", connection.ConnectionId);
            }
        });

        await Task.WhenAll(tasks);
    }

    public void UpdateClientId(string oldClientId, string newClientId)
    {
        if (_clients.TryRemove(oldClientId, out var connection))
        {
            _clients[newClientId] = connection;
            _logger.LogInformation("Updated WebSocket client ID mapping from {OldId} to {NewId}", oldClientId, newClientId);
        }
        else
        {
            _logger.LogWarning("Cannot update client ID: old client ID {OldId} not found in WebSocket connections", oldClientId);
        }
    }

    private async Task AcceptClientsAsync(CancellationToken cancellationToken, X509Certificate2 certificate)
    {
        _logger.LogInformation("WSS accept loop started - waiting for client connections...");

        while (!cancellationToken.IsCancellationRequested && _tcpListener != null)
        {
            try
            {
                var tcpClient = await _tcpListener.AcceptTcpClientAsync(cancellationToken);
                _ = Task.Run(() => HandleTcpClientAsync(tcpClient, certificate, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Accept clients task cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting TCP client");
            }
        }

        _logger.LogInformation("WSS accept loop stopped");
    }

    private async Task HandleTcpClientAsync(TcpClient tcpClient, X509Certificate2 certificate, CancellationToken cancellationToken)
    {
        SslWebSocketConnection? connection = null;

        try
        {
            var remoteEndPoint = tcpClient.Client.RemoteEndPoint?.ToString();
            _logger.LogInformation("TCP client connecting from {RemoteEndPoint}", remoteEndPoint);

            // SSL Handshake
            var sslStream = new SslStream(tcpClient.GetStream(), false);
            await sslStream.AuthenticateAsServerAsync(
                certificate,
                clientCertificateRequired: false,
                enabledSslProtocols: SslProtocols.Tls12 | SslProtocols.Tls13,
                checkCertificateRevocation: false
            );

            _logger.LogInformation("SSL handshake completed with {RemoteEndPoint} (Protocol: {Protocol})",
                remoteEndPoint, sslStream.SslProtocol);

            // WebSocket Handshake
            connection = new SslWebSocketConnection(tcpClient, sslStream, _logger);

            if (!await connection.PerformWebSocketHandshakeAsync(cancellationToken))
            {
                _logger.LogWarning("WebSocket handshake failed for {RemoteEndPoint}", remoteEndPoint);
                return;
            }

            var clientId = connection.ConnectionId;
            _clients[clientId] = connection;

            _logger.LogInformation("WSS connection established: {ConnectionId} from {RemoteEndPoint}", clientId, remoteEndPoint);

            ClientConnected?.Invoke(this, new ClientConnectedEventArgs
            {
                ClientId = clientId,
                IpAddress = connection.ClientIpAddress ?? "unknown"
            });

            // Handle WebSocket messages
            await HandleWebSocketConnectionAsync(connection, cancellationToken);
        }
        catch (AuthenticationException ex)
        {
            var remoteEndPoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "Unknown";

            // Check for specific SSL error: UnknownCA means client rejected our self-signed certificate
            if (ex.Message.Contains("UnknownCA", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("===================================================================");
                _logger.LogError("SSL AUTHENTICATION FAILED: Client rejected self-signed certificate");
                _logger.LogError("===================================================================");
                _logger.LogError("Client: {RemoteEndPoint}", remoteEndPoint);
                _logger.LogError("Error: {Message}", ex.Message);
                _logger.LogError("");
                _logger.LogError("SOLUTION:");
                _logger.LogError("The client must be configured to accept self-signed certificates.");
                _logger.LogError("");
                _logger.LogError("For Python Client (Raspberry Pi):");
                _logger.LogError("  - In client.py, ensure SSL context is configured:");
                _logger.LogError("    sslopt = {{");
                _logger.LogError("        \"cert_reqs\": ssl.CERT_NONE,");
                _logger.LogError("        \"check_hostname\": False");
                _logger.LogError("    }}");
                _logger.LogError("");
                _logger.LogError("For iOS App:");
                _logger.LogError("  - In WebSocketService.cs, ensure callback is set:");
                _logger.LogError("    _webSocket.Options.RemoteCertificateValidationCallback =");
                _logger.LogError("        (sender, cert, chain, errors) => true;");
                _logger.LogError("===================================================================");
            }
            else
            {
                _logger.LogError(ex, "SSL authentication failed for {RemoteEndPoint}", remoteEndPoint);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling TCP/SSL client");
        }
        finally
        {
            connection?.Dispose();
        }
    }

    private async Task HandleWebSocketConnectionAsync(SslWebSocketConnection connection, CancellationToken cancellationToken)
    {
        var clientId = connection.ConnectionId;

        try
        {
            while (connection.IsConnected && !cancellationToken.IsCancellationRequested)
            {
                var frame = await connection.ReceiveFrameAsync(cancellationToken);

                if (frame == null)
                {
                    _logger.LogInformation("Connection closed: {ConnectionId}", clientId);
                    break;
                }

                await HandleWebSocketFrameAsync(connection, frame, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling WebSocket connection {ConnectionId}", clientId);
        }
        finally
        {
            _clients.TryRemove(clientId, out _);
            _clientHandlerTasks.TryRemove(clientId, out _);

            // Clean up mobile app connection if applicable
            if (_mobileAppConnections.TryRemove(clientId, out _))
            {
                _mobileAppIds.TryRemove(clientId, out _);
                _mobileAppTokens.TryRemove(clientId, out _);
                _logger.LogInformation("Mobile app {ClientId} disconnected", clientId);
            }
            else
            {
                _logger.LogInformation("Client {ClientId} disconnected", clientId);
            }

            ClientDisconnected?.Invoke(this, new ClientDisconnectedEventArgs
            {
                ClientId = clientId,
                Reason = "Connection closed"
            });
        }
    }

    private async Task HandleWebSocketFrameAsync(SslWebSocketConnection connection, WebSocketFrame frame, CancellationToken cancellationToken)
    {
        try
        {
            switch (frame.Opcode)
            {
                case WebSocketOpcode.Text:
                    var message = Encoding.UTF8.GetString(frame.Payload);
                    _logger.LogDebug("Received text message from {ConnectionId}: {Length} bytes", connection.ConnectionId, message.Length);

                    await ProcessClientMessageAsync(connection, message, cancellationToken);
                    break;

                case WebSocketOpcode.Binary:
                    _logger.LogDebug("Received binary message from {ConnectionId}: {Length} bytes", connection.ConnectionId, frame.Payload.Length);
                    // Handle binary messages if needed
                    break;

                case WebSocketOpcode.Close:
                    _logger.LogInformation("Client requested close: {ConnectionId}", connection.ConnectionId);
                    await connection.CloseAsync(cancellationToken);
                    break;

                case WebSocketOpcode.Ping:
                    _logger.LogDebug("Received ping from {ConnectionId}", connection.ConnectionId);
                    await connection.SendFrameAsync(WebSocketOpcode.Pong, frame.Payload, cancellationToken);
                    break;

                case WebSocketOpcode.Pong:
                    _logger.LogDebug("Received pong from {ConnectionId}", connection.ConnectionId);
                    break;

                default:
                    _logger.LogWarning("Unsupported opcode: {Opcode} from {ConnectionId}", frame.Opcode, connection.ConnectionId);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling WebSocket frame from {ConnectionId}", connection.ConnectionId);
        }
    }

    private async Task ProcessClientMessageAsync(SslWebSocketConnection connection, string messageJson, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Received message from {ConnectionId}: {Length} bytes", connection.ConnectionId, messageJson.Length);

            // Parse JSON to check if it has $type field (sent by server with TypeNameHandling.Auto)
            // or just Type field (sent by clients without TypeNameHandling)
            var jsonObject = JObject.Parse(messageJson);
            var hasTypeHint = jsonObject["$type"] != null;
            var messageType = jsonObject["type"]?.ToString() ?? jsonObject["Type"]?.ToString();

            if (string.IsNullOrWhiteSpace(messageType))
            {
                _logger.LogWarning("Message from {ConnectionId} missing 'type' field", connection.ConnectionId);
                return;
            }

            _logger.LogDebug("Processing message type: {MessageType} from {ConnectionId} (has $type: {HasTypeHint})",
                messageType, connection.ConnectionId, hasTypeHint);

            Message? message;

            // If message has $type field, use TypeNameHandling.Auto to deserialize polymorphically
            // This handles messages sent by the server itself (e.g., echoed back)
            if (hasTypeHint)
            {
                var settings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    NullValueHandling = NullValueHandling.Ignore,
                    DefaultValueHandling = DefaultValueHandling.Include
                };

                message = JsonConvert.DeserializeObject<Message>(messageJson, settings);
            }
            else
            {
                // No $type field - deserialize based on Type field value
                // This handles messages from Raspberry Pi clients and mobile apps
                var settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    DefaultValueHandling = DefaultValueHandling.Include
                };

                message = DeserializeMessageByType(messageType, messageJson, settings);
            }

            if (message == null)
            {
                _logger.LogWarning("Failed to deserialize message type {MessageType} from {ConnectionId}", messageType, connection.ConnectionId);
                return;
            }

            _logger.LogDebug("Successfully deserialized message type: {MessageType} from {ConnectionId}", message.Type, connection.ConnectionId);

            // Check if this is a mobile app message
            if (IsMobileAppMessage(message))
            {
                await HandleMobileAppMessageAsync(connection.ConnectionId, connection, message, cancellationToken);
            }
            else
            {
                // For REGISTER messages, use the ClientId from the message for the event
                var eventClientId = connection.ConnectionId;
                if (message is RegisterMessage registerMsg && !string.IsNullOrWhiteSpace(registerMsg.ClientId))
                {
                    eventClientId = registerMsg.ClientId;

                    // Update the WebSocket client mapping if the client ID is different
                    if (eventClientId != connection.ConnectionId)
                    {
                        _logger.LogInformation("Client registering with ID {RegisteredId} (WebSocket connection ID: {ConnectionId})",
                            eventClientId, connection.ConnectionId);
                        UpdateClientId(connection.ConnectionId, eventClientId);
                    }
                }

                MessageReceived?.Invoke(this, new MessageReceivedEventArgs
                {
                    ClientId = eventClientId,
                    Message = message
                });
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize message from {ConnectionId}", connection.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing client message from {ConnectionId}", connection.ConnectionId);
        }
    }


    // ============================================
    // MESSAGE DESERIALIZATION
    // ============================================

    /// <summary>
    /// Deserialize a JSON message to the correct concrete Message type based on the message type field
    /// </summary>
    private Message? DeserializeMessageByType(string messageType, string messageJson, JsonSerializerSettings settings)
    {
        try
        {
            return messageType switch
            {
                // ============================================
                // MOBILE APP ↔ SERVER MESSAGES
                // ============================================

                // Mobile App → Server
                MobileAppMessageTypes.AppRegister => JsonConvert.DeserializeObject<AppRegisterMessage>(messageJson, settings),
                MobileAppMessageTypes.AppHeartbeat => JsonConvert.DeserializeObject<AppHeartbeatMessage>(messageJson, settings),
                MobileAppMessageTypes.RequestClientList => JsonConvert.DeserializeObject<RequestClientListMessage>(messageJson, settings),
                MobileAppMessageTypes.SendCommand => JsonConvert.DeserializeObject<SendCommandMessage>(messageJson, settings),
                MobileAppMessageTypes.AssignLayout => JsonConvert.DeserializeObject<AssignLayoutMessage>(messageJson, settings),
                MobileAppMessageTypes.RequestScreenshot => JsonConvert.DeserializeObject<RequestScreenshotMessage>(messageJson, settings),
                MobileAppMessageTypes.RequestLayoutList => JsonConvert.DeserializeObject<RequestLayoutListMessage>(messageJson, settings),

                // Server → Mobile App
                MobileAppMessageTypes.AppAuthorizationRequired => JsonConvert.DeserializeObject<AppAuthorizationRequiredMessage>(messageJson, settings),
                MobileAppMessageTypes.AppAuthorized => JsonConvert.DeserializeObject<AppAuthorizedMessage>(messageJson, settings),
                MobileAppMessageTypes.AppRejected => JsonConvert.DeserializeObject<AppRejectedMessage>(messageJson, settings),
                MobileAppMessageTypes.ClientListUpdate => JsonConvert.DeserializeObject<ClientListUpdateMessage>(messageJson, settings),
                MobileAppMessageTypes.ClientStatusChanged => JsonConvert.DeserializeObject<ClientStatusChangedMessage>(messageJson, settings),
                MobileAppMessageTypes.ScreenshotResponse => JsonConvert.DeserializeObject<ScreenshotResponseMessage>(messageJson, settings),
                MobileAppMessageTypes.LayoutListResponse => JsonConvert.DeserializeObject<LayoutListResponseMessage>(messageJson, settings),
                MobileAppMessageTypes.CommandResult => JsonConvert.DeserializeObject<CommandResultMessage>(messageJson, settings),

                // ============================================
                // DEVICE (RASPBERRY PI) ↔ SERVER MESSAGES
                // ============================================

                // Device → Server
                MessageTypes.Register => JsonConvert.DeserializeObject<RegisterMessage>(messageJson, settings),
                MessageTypes.Heartbeat => JsonConvert.DeserializeObject<HeartbeatMessage>(messageJson, settings),
                MessageTypes.StatusReport => JsonConvert.DeserializeObject<StatusReportMessage>(messageJson, settings),
                MessageTypes.Screenshot => JsonConvert.DeserializeObject<ScreenshotMessage>(messageJson, settings),
                MessageTypes.Log => JsonConvert.DeserializeObject<LogMessage>(messageJson, settings),
                MessageTypes.UpdateConfigResponse => JsonConvert.DeserializeObject<UpdateConfigResponseMessage>(messageJson, settings),

                // Server → Device
                MessageTypes.RegistrationResponse => JsonConvert.DeserializeObject<RegistrationResponseMessage>(messageJson, settings),
                MessageTypes.DisplayUpdate => JsonConvert.DeserializeObject<DisplayUpdateMessage>(messageJson, settings),
                MessageTypes.Command => JsonConvert.DeserializeObject<CommandMessage>(messageJson, settings),
                MessageTypes.UpdateConfig => JsonConvert.DeserializeObject<UpdateConfigMessage>(messageJson, settings),
                MessageTypes.LayoutAssigned => JsonConvert.DeserializeObject<LayoutAssignmentMessage>(messageJson, settings),
                MessageTypes.DataUpdate => JsonConvert.DeserializeObject<DataUpdateMessage>(messageJson, settings),

                _ => null
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize message type {MessageType}", messageType);
            return null;
        }
    }

    // ============================================
    // MOBILE APP MESSAGE HANDLING
    // ============================================

    /// <summary>
    /// Check if a message is from a mobile app (based on message type)
    /// </summary>
    private static bool IsMobileAppMessage(Message message)
    {
        return message.Type switch
        {
            MobileAppMessageTypes.AppRegister => true,
            MobileAppMessageTypes.AppHeartbeat => true,
            MobileAppMessageTypes.RequestClientList => true,
            MobileAppMessageTypes.SendCommand => true,
            MobileAppMessageTypes.AssignLayout => true,
            MobileAppMessageTypes.RequestScreenshot => true,
            MobileAppMessageTypes.RequestLayoutList => true,
            _ => false
        };
    }

    /// <summary>
    /// Route mobile app messages to appropriate handler
    /// </summary>
    private async Task HandleMobileAppMessageAsync(
        string connectionId,
        SslWebSocketConnection connection,
        Message message,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Handling mobile app message {MessageType} from connection {ConnectionId}",
                message.Type, connectionId);

            switch (message.Type)
            {
                case MobileAppMessageTypes.AppRegister:
                    await HandleAppRegisterAsync(connectionId, connection, message as AppRegisterMessage);
                    break;

                case MobileAppMessageTypes.AppHeartbeat:
                    await HandleAppHeartbeatAsync(connectionId, connection, message as AppHeartbeatMessage);
                    break;

                case MobileAppMessageTypes.RequestClientList:
                    await HandleRequestClientListAsync(connectionId, connection, message as RequestClientListMessage);
                    break;

                case MobileAppMessageTypes.SendCommand:
                    await HandleSendCommandAsync(connectionId, connection, message as SendCommandMessage);
                    break;

                case MobileAppMessageTypes.AssignLayout:
                    await HandleAssignLayoutAsync(connectionId, connection, message as AssignLayoutMessage);
                    break;

                case MobileAppMessageTypes.RequestScreenshot:
                    await HandleRequestScreenshotAsync(connectionId, connection, message as RequestScreenshotMessage);
                    break;

                case MobileAppMessageTypes.RequestLayoutList:
                    await HandleRequestLayoutListAsync(connectionId, connection, message as RequestLayoutListMessage);
                    break;

                default:
                    _logger.LogWarning("Unknown mobile app message type: {MessageType}", message.Type);
                    await SendErrorAsync(connection, $"Unknown message type: {message.Type}");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling mobile app message {MessageType}", message.Type);
            await SendErrorAsync(connection, "Internal server error");
        }
    }

    /// <summary>
    /// Handle mobile app registration
    /// </summary>
    private async Task HandleAppRegisterAsync(string connectionId, SslWebSocketConnection connection, AppRegisterMessage? message)
    {
        if (message == null)
        {
            await SendErrorAsync(connection, "Invalid registration message");
            return;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var mobileAppService = scope.ServiceProvider.GetRequiredService<IMobileAppService>();

            var result = await mobileAppService.RegisterAppAsync(
                message.DeviceName,
                message.DeviceIdentifier,
                message.AppVersion,
                message.Platform);

            if (result.IsSuccess)
            {
                var registration = result.Value;

                // Track mobile app connection
                _mobileAppConnections[connectionId] = connection;
                _mobileAppIds[connectionId] = registration.Id;

                // Fire event for new registration
                OnNewAppRegistration?.Invoke(this, registration);

                // If already approved, send token immediately
                if (registration.Status == AppRegistrationStatus.Approved && !string.IsNullOrEmpty(registration.Token))
                {
                    _mobileAppTokens[connectionId] = registration.Token;

                    await SendMessageAsync(connection, new AppAuthorizedMessage
                    {
                        Token = registration.Token,
                        Permissions = registration.Permissions.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                        ExpiresAt = DateTime.UtcNow.AddYears(1) // Token valid for 1 year
                    });

                    _logger.LogInformation("Mobile app {DeviceName} reconnected with existing authorization", message.DeviceName);
                }
                else
                {
                    // Send authorization required message
                    await SendMessageAsync(connection, new AppAuthorizationRequiredMessage
                    {
                        AppId = registration.Id,
                        Status = "pending",
                        Message = "Registration pending. Waiting for admin approval."
                    });

                    _logger.LogInformation("New mobile app registration: {DeviceName} ({Platform})", message.DeviceName, message.Platform);
                }
            }
            else
            {
                await SendErrorAsync(connection, result.ErrorMessage ?? "Registration failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during mobile app registration");
            await SendErrorAsync(connection, "Registration failed");
        }
    }

    /// <summary>
    /// Handle mobile app heartbeat
    /// </summary>
    private async Task HandleAppHeartbeatAsync(string connectionId, SslWebSocketConnection connection, AppHeartbeatMessage? message)
    {
        if (message == null) return;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var mobileAppService = scope.ServiceProvider.GetRequiredService<IMobileAppService>();

            await mobileAppService.UpdateLastSeenAsync(message.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error handling app heartbeat");
        }
    }

    /// <summary>
    /// Handle request for client list
    /// </summary>
    private async Task HandleRequestClientListAsync(string connectionId, SslWebSocketConnection connection, RequestClientListMessage? message)
    {
        if (message == null)
        {
            await SendErrorAsync(connection, "Invalid request");
            return;
        }

        // Validate token
        if (!_mobileAppTokens.TryGetValue(connectionId, out var token))
        {
            await SendErrorAsync(connection, "Not authenticated");
            return;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var mobileAppService = scope.ServiceProvider.GetRequiredService<IMobileAppService>();
            var clientService = scope.ServiceProvider.GetRequiredService<IClientService>();

            // Validate token and check permissions
            var registration = await mobileAppService.ValidateTokenAsync(token);
            if (registration == null)
            {
                await SendErrorAsync(connection, "Unauthorized");
                return;
            }

            if (!await mobileAppService.HasPermissionAsync(token, AppPermission.View))
            {
                await SendErrorAsync(connection, "Insufficient permissions");
                return;
            }

            // Get all clients
            var clientsResult = await clientService.GetAllClientsAsync();
            if (!clientsResult.IsSuccess)
            {
                await SendErrorAsync(connection, "Failed to retrieve client list");
                return;
            }

            var clients = clientsResult.Value;

            // Filter by status if requested
            if (!string.IsNullOrEmpty(message.Filter))
            {
                clients = message.Filter.ToLowerInvariant() switch
                {
                    "online" => clients.Where(c => c.Status == ClientStatus.Online).ToList(),
                    "offline" => clients.Where(c => c.Status == ClientStatus.Offline).ToList(),
                    _ => clients
                };
            }

            // Map to ClientInfo DTOs
            var clientInfos = clients.Select(c => new ClientInfo
            {
                Id = Guid.TryParse(c.Id, out var guid) ? guid : Guid.Empty,
                Name = c.Name ?? c.IpAddress ?? "Unknown",
                IpAddress = c.IpAddress,
                Status = ConvertToDeviceStatus(c.Status),
                Resolution = c.DeviceInfo != null ? $"{c.DeviceInfo.ScreenWidth}x{c.DeviceInfo.ScreenHeight}" : null,
                DeviceInfo = c.DeviceInfo != null ? new DeviceInfoData
                {
                    CpuUsage = c.DeviceInfo.CpuUsage,
                    MemoryUsage = c.DeviceInfo.MemoryUsed,
                    Temperature = c.DeviceInfo.CpuTemperature,
                    DiskUsage = c.DeviceInfo.DiskUsed,
                    OsVersion = c.DeviceInfo.OsVersion,
                    AppVersion = c.DeviceInfo.ClientVersion
                } : null,
                LastSeen = c.LastSeen,
                AssignedLayoutId = c.AssignedLayoutId,
                Location = c.Location,
                Group = c.Group
            }).ToList();

            await SendMessageAsync(connection, new ClientListUpdateMessage
            {
                Clients = clientInfos
            });

            _logger.LogDebug("Sent client list to mobile app ({Count} clients)", clientInfos.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling client list request");
            await SendErrorAsync(connection, "Failed to retrieve client list");
        }
    }

    /// <summary>
    /// Handle send command to device
    /// </summary>
    private async Task HandleSendCommandAsync(string connectionId, SslWebSocketConnection connection, SendCommandMessage? message)
    {
        if (message == null)
        {
            await SendErrorAsync(connection, "Invalid command message");
            return;
        }

        // Validate token
        if (!_mobileAppTokens.TryGetValue(connectionId, out var token))
        {
            await SendErrorAsync(connection, "Not authenticated");
            return;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var mobileAppService = scope.ServiceProvider.GetRequiredService<IMobileAppService>();

            // Validate token and check Control permission
            var registration = await mobileAppService.ValidateTokenAsync(token);
            if (registration == null || !await mobileAppService.HasPermissionAsync(token, AppPermission.Control))
            {
                await SendErrorAsync(connection, "Unauthorized");
                return;
            }

            // Find target device WebSocket
            var targetClientId = message.TargetDeviceId.ToString();
            if (!_clients.TryGetValue(targetClientId, out var targetSocket))
            {
                await SendErrorAsync(connection, "Device not connected");
                return;
            }

            // Forward command to Pi client
            var commandMessage = new CommandMessage
            {
                Command = message.Command,
                Parameters = message.Parameters
            };

            await SendMessageAsync(targetSocket, commandMessage);

            // Acknowledge to mobile app
            await SendMessageAsync(connection, new CommandResultMessage
            {
                DeviceId = message.TargetDeviceId,
                Command = message.Command,
                Success = true
            });

            _logger.LogInformation("Mobile app sent command {Command} to device {DeviceId}",
                message.Command, message.TargetDeviceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling send command");
            await SendErrorAsync(connection, "Failed to send command");
        }
    }

    /// <summary>
    /// Handle assign layout to device
    /// </summary>
    private async Task HandleAssignLayoutAsync(string connectionId, SslWebSocketConnection connection, AssignLayoutMessage? message)
    {
        if (message == null)
        {
            await SendErrorAsync(connection, "Invalid assign layout message");
            return;
        }

        // Validate token
        if (!_mobileAppTokens.TryGetValue(connectionId, out var token))
        {
            await SendErrorAsync(connection, "Not authenticated");
            return;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var mobileAppService = scope.ServiceProvider.GetRequiredService<IMobileAppService>();
            var clientService = scope.ServiceProvider.GetRequiredService<IClientService>();

            // Validate token and check Manage permission
            var registration = await mobileAppService.ValidateTokenAsync(token);
            if (registration == null || !await mobileAppService.HasPermissionAsync(token, AppPermission.Manage))
            {
                await SendErrorAsync(connection, "Unauthorized");
                return;
            }

            // Assign layout to device
            var result = await clientService.AssignLayoutAsync(message.DeviceId.ToString(), message.LayoutId);
            if (result.IsSuccess)
            {
                await SendMessageAsync(connection, new CommandResultMessage
                {
                    DeviceId = message.DeviceId,
                    Command = "AssignLayout",
                    Success = true
                });

                _logger.LogInformation("Mobile app assigned layout {LayoutId} to device {DeviceId}",
                    message.LayoutId, message.DeviceId);
            }
            else
            {
                await SendErrorAsync(connection, result.ErrorMessage ?? "Failed to assign layout");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling assign layout");
            await SendErrorAsync(connection, "Failed to assign layout");
        }
    }

    /// <summary>
    /// Handle request screenshot from device
    /// </summary>
    private async Task HandleRequestScreenshotAsync(string connectionId, SslWebSocketConnection connection, RequestScreenshotMessage? message)
    {
        if (message == null)
        {
            await SendErrorAsync(connection, "Invalid screenshot request");
            return;
        }

        // Validate token
        if (!_mobileAppTokens.TryGetValue(connectionId, out var token))
        {
            await SendErrorAsync(connection, "Not authenticated");
            return;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var mobileAppService = scope.ServiceProvider.GetRequiredService<IMobileAppService>();

            // Validate token and check View permission
            var registration = await mobileAppService.ValidateTokenAsync(token);
            if (registration == null || !await mobileAppService.HasPermissionAsync(token, AppPermission.View))
            {
                await SendErrorAsync(connection, "Unauthorized");
                return;
            }

            // Find target device WebSocket
            var targetClientId = message.DeviceId.ToString();
            if (!_clients.TryGetValue(targetClientId, out var targetSocket))
            {
                await SendErrorAsync(connection, "Device not connected");
                return;
            }

            // Request screenshot from Pi client
            // Create a simple command message to request screenshot
            var screenshotMessage = new CommandMessage
            {
                Command = "Screenshot"
            };

            await SendMessageAsync(targetSocket, screenshotMessage);

            _logger.LogInformation("Mobile app requested screenshot from device {DeviceId}", message.DeviceId);

            // Note: The screenshot response will be received separately and needs to be
            // forwarded to the mobile app - this would require maintaining a mapping
            // of screenshot requests to mobile app connections
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling screenshot request");
            await SendErrorAsync(connection, "Failed to request screenshot");
        }
    }

    /// <summary>
    /// Handle request for layout list
    /// </summary>
    private async Task HandleRequestLayoutListAsync(string connectionId, SslWebSocketConnection connection, RequestLayoutListMessage? message)
    {
        if (message == null)
        {
            await SendErrorAsync(connection, "Invalid layout list request");
            return;
        }

        // Validate token
        if (!_mobileAppTokens.TryGetValue(connectionId, out var token))
        {
            await SendErrorAsync(connection, "Not authenticated");
            return;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var mobileAppService = scope.ServiceProvider.GetRequiredService<IMobileAppService>();
            var layoutService = scope.ServiceProvider.GetRequiredService<ILayoutService>();

            // Validate token and check View permission
            var registration = await mobileAppService.ValidateTokenAsync(token);
            if (registration == null || !await mobileAppService.HasPermissionAsync(token, AppPermission.View))
            {
                await SendErrorAsync(connection, "Unauthorized");
                return;
            }

            // Get all layouts
            var layoutsResult = await layoutService.GetAllLayoutsAsync();
            if (layoutsResult == null || !layoutsResult.IsSuccess || layoutsResult.Value == null || !layoutsResult.Value.Any())
            {
                await SendErrorAsync(connection, "No layouts found");
                return;
            }

            // Map to LayoutInfo DTOs
            var layoutInfos = layoutsResult.Value.Select(l => new LayoutInfo
            {
                Id = l.Id ?? string.Empty,
                Name = l.Name ?? "Unnamed Layout",
                Description = l.Description,
                Category = l.Category,
                Created = l.Created,
                Modified = l.Modified,
                Width = l.Resolution?.Width ?? 1920,
                Height = l.Resolution?.Height ?? 1080
            }).ToList();

            await SendMessageAsync(connection, new LayoutListResponseMessage
            {
                Layouts = layoutInfos
            });

            _logger.LogDebug("Sent layout list to mobile app ({Count} layouts)", layoutInfos.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling layout list request");
            await SendErrorAsync(connection, "Failed to retrieve layout list");
        }
    }

    /// <summary>
    /// Send error message to WebSocket connection
    /// </summary>
    private async Task SendErrorAsync(SslWebSocketConnection connection, string errorMessage)
    {
        try
        {
            // Create a CommandResultMessage to send error
            var errorMsg = new CommandResultMessage
            {
                Success = false,
                ErrorMessage = errorMessage
            };

            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                NullValueHandling = NullValueHandling.Ignore
            };
            var json = JsonConvert.SerializeObject(errorMsg, settings);
            await connection.SendTextAsync(json, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send error message to WebSocket");
        }
    }

    /// <summary>
    /// Send message directly to a WebSocket connection
    /// </summary>
    private async Task SendMessageAsync(SslWebSocketConnection connection, Message message)
    {
        try
        {
            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                NullValueHandling = NullValueHandling.Ignore
            };
            var json = JsonConvert.SerializeObject(message, settings);
            await connection.SendTextAsync(json, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to WebSocket");
            throw;
        }
    }

    /// <summary>
    /// Notify all connected mobile apps of client status change
    /// </summary>
    public async Task NotifyMobileAppsClientStatusChangedAsync(Guid deviceId, DeviceStatus status)
    {
        var statusMessage = new ClientStatusChangedMessage
        {
            DeviceId = deviceId,
            Status = status,
            Timestamp = DateTime.UtcNow
        };

        var tasks = _mobileAppConnections.Values.Select(socket =>
            SendMessageAsync(socket, statusMessage));

        try
        {
            await Task.WhenAll(tasks);
            _logger.LogDebug("Notified {Count} mobile apps of client status change", _mobileAppConnections.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error notifying mobile apps of client status change");
        }
    }

    /// <summary>
    /// Converts ClientStatus to DeviceStatus for mobile app compatibility
    /// </summary>
    private static DeviceStatus ConvertToDeviceStatus(ClientStatus clientStatus)
    {
        return clientStatus switch
        {
            ClientStatus.Online => DeviceStatus.Online,
            ClientStatus.Offline => DeviceStatus.Offline,
            ClientStatus.Disconnected => DeviceStatus.Offline,
            ClientStatus.Error => DeviceStatus.Error,
            ClientStatus.Updating => DeviceStatus.Warning,
            ClientStatus.Connecting => DeviceStatus.Warning,
            ClientStatus.OfflineRecovery => DeviceStatus.Warning,
            _ => DeviceStatus.Offline
        };
    }

    /// <summary>
    /// Throws ObjectDisposedException if service has been disposed
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WebSocketCommunicationService));
        }
    }

    /// <summary>
    /// Disposes managed resources
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes managed and unmanaged resources
    /// </summary>
    /// <param name="disposing">True if disposing managed resources</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // Dispose managed resources
            try
            {
                // Stop async should be called before dispose
                // But if not, clean up what we can
                _tcpListener?.Stop();
                _cancellationTokenSource?.Dispose();

                // Dispose any remaining connections
                foreach (var connection in _clients.Values.ToList())
                {
                    try { connection.Dispose(); } catch { }
                }

                _logger.LogInformation("WebSocketCommunicationService disposed");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during WebSocketCommunicationService disposal");
            }
        }

        _disposed = true;
    }
}
