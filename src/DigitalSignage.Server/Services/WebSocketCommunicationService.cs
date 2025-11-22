using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.Configuration;
using DigitalSignage.Server.Helpers;
using DigitalSignage.Server.MessageHandlers;
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

    // Track all TCP client handler tasks (created before we have a client ID)
    private readonly ConcurrentBag<Task> _allHandlerTasks = new();

    // Mobile App Connections (separate from Pi clients)
    private readonly ConcurrentDictionary<string, SslWebSocketConnection> _mobileAppConnections = new();
    private readonly ConcurrentDictionary<string, Guid> _mobileAppIds = new(); // Maps connection ID to app ID
    private readonly ConcurrentDictionary<string, string> _mobileAppTokens = new(); // Maps connection ID to token

    private readonly ILogger<WebSocketCommunicationService> _logger;
    private readonly WebSocketMessageSerializer _messageSerializer;
    private readonly ServerSettings _settings;
    private readonly IServiceProvider _serviceProvider; // For scoped service access
    private readonly ICertificateService _certificateService;
    private readonly MessageHandlerFactory _messageHandlerFactory;
    private readonly MessageVersionValidator _versionValidator;
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
        ICertificateService certificateService,
        MessageHandlerFactory messageHandlerFactory,
        MessageVersionValidator versionValidator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _certificateService = certificateService ?? throw new ArgumentNullException(nameof(certificateService));
        _messageHandlerFactory = messageHandlerFactory ?? throw new ArgumentNullException(nameof(messageHandlerFactory));
        _versionValidator = versionValidator ?? throw new ArgumentNullException(nameof(versionValidator));
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

        // 7. Wait for all TCP handler tasks (created before client registration)
        var allHandlerTasksArray = _allHandlerTasks.ToArray();
        if (allHandlerTasksArray.Length > 0)
        {
            try
            {
                _logger.LogDebug("Waiting for {Count} TCP handler tasks to complete", allHandlerTasksArray.Length);
                await Task.WhenAll(allHandlerTasksArray).WaitAsync(TimeSpan.FromSeconds(10));
                _logger.LogDebug("All TCP handler tasks stopped gracefully");
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("{Count} TCP handler tasks did not stop within timeout", allHandlerTasksArray.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error waiting for TCP handler tasks to complete");
            }
        }

        // 8. Clear clients dictionary and handler tasks
        _clients.Clear();
        _clientHandlerTasks.Clear();
        _allHandlerTasks.Clear();

        // 9. Dispose cancellation token source
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

        _logger.LogWarning("CRITICAL DEBUG: SendMessageAsync called: clientId={ClientId}, messageType={MessageType}, _clients.Count={Count}",
            clientId, message?.Type ?? "null", _clients.Count);
        _logger.LogWarning("CRITICAL DEBUG: Looking for client {ClientId} in _clients dictionary: {Exists}",
            clientId, _clients.ContainsKey(clientId));

        if (_clients.TryGetValue(clientId, out var connection))
        {
            _logger.LogWarning("CRITICAL DEBUG: Found connection for {ClientId}, IsConnected={IsConnected}",
                clientId, connection.IsConnected);

            try
            {
                if (!connection.IsConnected)
                {
                    _logger.LogError("CRITICAL ERROR: Cannot send message to client {ClientId}: connection not open!", clientId);
                    throw new InvalidOperationException($"Client {clientId} connection is not open");
                }

                _logger.LogDebug("Serializing message type {MessageType} for client {ClientId}", message.Type, clientId);

                // Add protocol version to outgoing message
                if (string.IsNullOrWhiteSpace(message.Version))
                {
                    message.Version = _versionValidator.GetServerVersion().ToString();
                }

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
            _logger.LogWarning("Client {ClientId} not found in connections dictionary (have {Count} clients, first 5: {ClientIds})",
                clientId, _clients.Count, string.Join(", ", _clients.Keys.Take(5)));
            throw new InvalidOperationException($"Client {clientId} not found");
        }
    }

    public async Task BroadcastMessageAsync(Message message, CancellationToken cancellationToken = default)
    {
        // Add protocol version to outgoing message
        if (string.IsNullOrWhiteSpace(message.Version))
        {
            message.Version = _versionValidator.GetServerVersion().ToString();
        }

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

    /// <summary>
    /// Check if a Pi client is currently connected
    /// </summary>
    public bool IsClientConnected(string clientId)
    {
        return _clients.ContainsKey(clientId);
    }

    private async Task AcceptClientsAsync(CancellationToken cancellationToken, X509Certificate2 certificate)
    {
        _logger.LogInformation("WSS accept loop started - waiting for client connections...");

        while (!cancellationToken.IsCancellationRequested && _tcpListener != null)
        {
            try
            {
                var tcpClient = await _tcpListener.AcceptTcpClientAsync(cancellationToken);

                // Track handler task instead of fire-and-forget
                var handlerTask = Task.Run(() => HandleTcpClientAsync(tcpClient, certificate, cancellationToken), cancellationToken);
                _allHandlerTasks.Add(handlerTask);
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

            // Clean up version cache
            _versionValidator.RemoveClientVersion(clientId);

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

            // ============================================
            // MESSAGE VERSION VALIDATION
            // ============================================

            // Extract version from message (optional for backward compatibility)
            var versionString = jsonObject["version"]?.ToString() ?? jsonObject["Version"]?.ToString();

            // Validate client version
            var versionResult = _versionValidator.ValidateVersion(versionString, connection.ConnectionId);

            if (!versionResult.IsCompatible)
            {
                _logger.LogError("Incompatible protocol version from {ConnectionId}: {Message}",
                    connection.ConnectionId, versionResult.Message);

                // Send error message to client
                try
                {
                    var errorResponse = new
                    {
                        type = "ERROR",
                        message = versionResult.Message,
                        serverVersion = versionResult.ServerVersion?.ToString(),
                        clientVersion = versionResult.ClientVersion?.ToString()
                    };

                    var errorJson = JsonConvert.SerializeObject(errorResponse);
                    await connection.SendTextAsync(errorJson, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send version error to client {ConnectionId}", connection.ConnectionId);
                }

                // Close connection with incompatible version
                await connection.CloseAsync(cancellationToken);
                return;
            }

            // Version is compatible - log for monitoring
            _logger.LogDebug("Client {ConnectionId} version validated: {ClientVersion} (Server: {ServerVersion})",
                connection.ConnectionId, versionResult.ClientVersion, versionResult.ServerVersion);

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
                // ==================================================================
                // PI CLIENT MESSAGE HANDLING (New Handler Pattern)
                // ==================================================================

                // For REGISTER messages, update the WebSocket client mapping BEFORE calling handler
                // This ensures that handlers can send messages back to the correct client ID
                var clientId = connection.ConnectionId;
                if (message is RegisterMessage registerMsg && !string.IsNullOrWhiteSpace(registerMsg.ClientId))
                {
                    clientId = registerMsg.ClientId;

                    // Update the WebSocket client mapping if the client ID is different
                    // CRITICAL: This MUST happen BEFORE handler is called to ensure
                    // RegistrationResponse and Layout messages can be sent successfully
                    if (clientId != connection.ConnectionId)
                    {
                        _logger.LogDebug("Client registering with ID {RegisteredId} (WebSocket connection ID: {ConnectionId})",
                            clientId, connection.ConnectionId);
                        UpdateClientId(connection.ConnectionId, clientId);
                        _logger.LogDebug("Updated client ID mapping: {ConnectionId} -> {ClientId}",
                            connection.ConnectionId, clientId);
                    }
                }

                // Get handler for this message type
                var handler = _messageHandlerFactory.GetHandler(message.Type);

                if (handler != null)
                {
                    // Call handler directly (NEW: Handler Pattern - replaces MessageReceived event)
                    _logger.LogDebug("Calling handler {HandlerType} for message type {MessageType}",
                        handler.GetType().Name, message.Type);
                    await handler.HandleAsync(message, clientId, cancellationToken);
                }
                else
                {
                    // No handler found - fall back to firing MessageReceived event
                    // This allows gradual migration (some messages still use old event-based system)
                    _logger.LogDebug("No handler for message type {MessageType}, firing MessageReceived event", message.Type);
                    MessageReceived?.Invoke(this, new MessageReceivedEventArgs
                    {
                        ClientId = clientId,
                        Message = message
                    });
                }
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
            // Normalize message type to UPPERCASE for case-insensitive comparison
            // This allows clients to send "Register", "register", "REGISTER", etc.
            var normalizedType = messageType?.ToUpperInvariant() ?? string.Empty;

            return normalizedType switch
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

            // ==================================================================
            // MOBILE APP MESSAGE HANDLING (New Handler Pattern)
            // ==================================================================

            // Ensure connection is tracked in MobileAppConnectionManager
            // This must happen BEFORE handlers are called so they can access the connection
            var mobileAppManager = _serviceProvider.GetService<MobileAppConnectionManager>();
            if (mobileAppManager != null && mobileAppManager.GetConnection(connectionId) == null)
            {
                mobileAppManager.TrackConnection(connectionId, connection);
            }

            // Get handler for this message type
            var handler = _messageHandlerFactory.GetHandler(message.Type);

            if (handler != null)
            {
                // Call handler directly (NEW: Handler Pattern)
                _logger.LogDebug("Calling handler {HandlerType} for mobile app message type {MessageType}",
                    handler.GetType().Name, message.Type);
                await handler.HandleAsync(message, connectionId, cancellationToken);
            }
            else
            {
                // No handler found
                _logger.LogWarning("No handler registered for mobile app message type: {MessageType}", message.Type);
                await SendErrorAsync(connection, $"Unknown message type: {message.Type}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling mobile app message {MessageType}", message.Type);
            await SendErrorAsync(connection, "Internal server error");
        }
    }

    // ============================================
    // OLD MOBILE APP HANDLER METHODS - REMOVED
    // ============================================
    // The following methods have been migrated to individual handlers in MessageHandlers/MobileApp/:
    // - HandleAppRegisterAsync → AppRegisterMessageHandler
    // - HandleAppHeartbeatAsync → AppHeartbeatMessageHandler
    // - HandleRequestClientListAsync → RequestClientListMessageHandler
    // - HandleSendCommandAsync → SendCommandMessageHandler
    // - HandleAssignLayoutAsync → AssignLayoutMessageHandler
    // - HandleRequestScreenshotAsync → RequestScreenshotMessageHandler
    // - HandleRequestLayoutListAsync → RequestLayoutListMessageHandler
    // - SendErrorAsync / SendMessageAsync → MobileAppConnectionManager
    // - SendApprovalNotificationAsync → MobileAppConnectionManager.SendApprovalNotificationAsync
    // - NotifyMobileAppsClientStatusChangedAsync → MobileAppConnectionManager.NotifyAllClientsStatusChangedAsync
    // - ConvertPermissionToList → MobileAppConnectionManager.ConvertPermissionToList

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
