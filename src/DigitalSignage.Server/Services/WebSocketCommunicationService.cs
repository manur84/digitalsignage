using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.Configuration;
using DigitalSignage.Server.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.WebSockets;

namespace DigitalSignage.Server.Services;

public class WebSocketCommunicationService : ICommunicationService, IDisposable
{
    private readonly ConcurrentDictionary<string, WebSocket> _clients = new();
    private readonly ConcurrentDictionary<string, Task> _clientHandlerTasks = new();

    // Mobile App Connections (separate from Pi clients)
    private readonly ConcurrentDictionary<string, WebSocket> _mobileAppConnections = new();
    private readonly ConcurrentDictionary<string, Guid> _mobileAppIds = new(); // Maps connection ID to app ID
    private readonly ConcurrentDictionary<string, string> _mobileAppTokens = new(); // Maps connection ID to token

    private readonly ILogger<WebSocketCommunicationService> _logger;
    private readonly WebSocketMessageSerializer _messageSerializer;
    private readonly ServerSettings _settings;
    private readonly IServiceProvider _serviceProvider; // For scoped service access
    private readonly ICertificateService _certificateService;
    private readonly ISslBindingService _sslBindingService;
    private HttpListener? _httpListener;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _acceptClientsTask;
    private bool _disposed = false;

    public WebSocketCommunicationService(
        ILogger<WebSocketCommunicationService> logger,
        ILogger<WebSocketMessageSerializer> serializerLogger,
        ServerSettings settings,
        IServiceProvider serviceProvider,
        ICertificateService certificateService,
        ISslBindingService sslBindingService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _certificateService = certificateService ?? throw new ArgumentNullException(nameof(certificateService));
        _sslBindingService = sslBindingService ?? throw new ArgumentNullException(nameof(sslBindingService));
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

            // Load SSL certificate if enabled
            if (_settings.EnableSsl)
            {
                try
                {
                    _logger.LogInformation("SSL/TLS is enabled - loading certificate...");
                    var certificate = _certificateService.GetOrCreateServerCertificate();

                    if (certificate == null)
                    {
                        _logger.LogWarning("===================================================================");
                        _logger.LogWarning("SSL CERTIFICATE LOADING FAILED");
                        _logger.LogWarning("===================================================================");
                        _logger.LogWarning("");
                        _logger.LogWarning("SSL is enabled but no certificate could be loaded or generated.");
                        _logger.LogWarning("Falling back to HTTP/WS (unencrypted) mode.");
                        _logger.LogWarning("");
                        _logger.LogWarning("To fix SSL:");
                        _logger.LogWarning("  1. Check CertificatePath in appsettings.json");
                        _logger.LogWarning("  2. Ensure certificate file exists and is accessible");
                        _logger.LogWarning("  3. Verify CertificatePassword is correct");
                        _logger.LogWarning("  4. Or set EnableSsl=false to disable SSL");
                        _logger.LogWarning("");
                        _logger.LogWarning("===================================================================");

                        // Disable SSL and continue with HTTP
                        _settings.EnableSsl = false;
                    }
                    else
                    {
                        _logger.LogInformation("✅ SSL certificate loaded successfully");
                        _logger.LogInformation("Certificate Subject: {Subject}", certificate.Subject);
                        _logger.LogInformation("Certificate Thumbprint: {Thumbprint}", certificate.Thumbprint);
                        _logger.LogInformation("Certificate Valid Until: {NotAfter}", certificate.NotAfter);

                        // Automatically configure SSL binding if enabled and running on Windows
                        if (_settings.AutoConfigureSslBinding && OperatingSystem.IsWindows())
                        {
                            _logger.LogInformation("Attempting automatic SSL binding configuration...");

                            var currentPort = _settings.Port;
                            var pfxPath = _settings.CertificatePath ?? string.Empty;
                            var password = _settings.CertificatePassword ?? string.Empty;

                            var bindingSuccess = await _sslBindingService.EnsureSslBindingAsync(
                                certificate,
                                currentPort,
                                pfxPath,
                                password);

                            if (bindingSuccess)
                            {
                                _logger.LogInformation("✅ SSL binding configured successfully");
                                _logger.LogInformation("Clients can now connect via WSS (secure WebSocket)");
                            }
                            else
                            {
                                if (!_sslBindingService.IsRunningAsAdministrator())
                                {
                                    _logger.LogWarning("===================================================================");
                                    _logger.LogWarning("SSL BINDING NOT CONFIGURED - Administrator Rights Required");
                                    _logger.LogWarning("===================================================================");
                                    _logger.LogWarning("");
                                    _logger.LogWarning("The server is NOT running with Administrator privileges.");
                                    _logger.LogWarning("SSL/TLS cannot be configured automatically without admin rights.");
                                    _logger.LogWarning("");
                                    _logger.LogWarning("OPTIONS:");
                                    _logger.LogWarning("  1. Restart the application as Administrator (recommended)");
                                    _logger.LogWarning("  2. Manually configure SSL binding:");
                                    _logger.LogWarning($"     netsh http add sslcert ipport=0.0.0.0:{currentPort} certhash={certificate.Thumbprint} appid={{{_settings.SslAppId}}}");
                                    _logger.LogWarning("  3. Set EnableSsl=false in appsettings.json to use HTTP/WS");
                                    _logger.LogWarning("  4. Use a reverse proxy (nginx/IIS) for SSL termination");
                                    _logger.LogWarning("");
                                    _logger.LogWarning("Falling back to HTTP/WS (unencrypted) mode...");
                                    _logger.LogWarning("===================================================================");

                                    // Disable SSL and continue with HTTP
                                    _settings.EnableSsl = false;
                                }
                                else
                                {
                                    _logger.LogError("Failed to configure SSL binding despite having admin rights");
                                    _logger.LogWarning("Falling back to HTTP/WS mode");
                                    _settings.EnableSsl = false;
                                }
                            }
                        }
                        else if (!OperatingSystem.IsWindows())
                        {
                            _logger.LogWarning("⚠️  Automatic SSL binding is only supported on Windows");
                            _logger.LogWarning("On Linux/macOS, use a reverse proxy (nginx/caddy) for SSL termination");
                            _logger.LogWarning("Continuing without SSL binding configuration...");
                        }

                        // Dispose certificate as HttpListener doesn't use it directly
                        // (HttpListener reads from Windows certificate store via netsh binding)
                        certificate.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load SSL certificate - falling back to HTTP");
                    _settings.EnableSsl = false;
                }
            }

            _httpListener = new HttpListener();

            // ✅ LOGIC FIX: Use GetAvailablePort() to enable automatic port fallback
            // This ensures AlternativePorts are actually tried when the configured port is in use
            var actualPort = _settings.GetAvailablePort();
            if (actualPort != _settings.Port)
            {
                _logger.LogInformation("Port fallback: Configured port {ConfiguredPort} in use, using {ActualPort} instead",
                    _settings.Port, actualPort);
                _settings.Port = actualPort; // Update settings with selected port
            }

            // Try different binding options in order of preference
            var bindingAttempts = new List<(string prefix, string description)>();

            // 1. Try wildcard binding first (if URL ACL exists or running as admin)
            if (UrlAclManager.IsUrlAclConfigured(_settings.Port) || UrlAclManager.IsRunningAsAdministrator())
            {
                bindingAttempts.Add((_settings.GetUrlPrefix(), "all interfaces"));
            }

            // 2. Try specific IP addresses
            var localIPs = System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName())
                .Where(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .Select(ip => ip.ToString());

            foreach (var ip in localIPs)
            {
                var ipProtocol = _settings.EnableSsl ? "https" : "http";
                bindingAttempts.Add(($"{ipProtocol}://{ip}:{_settings.Port}{_settings.EndpointPath}", $"IP {ip}"));
            }

            // 3. Always add localhost and 127.0.0.1 as fallbacks
            bindingAttempts.Add((_settings.GetLocalhostPrefix(), "localhost"));
            bindingAttempts.Add(($"{(_settings.EnableSsl ? "https" : "http")}://127.0.0.1:{_settings.Port}{_settings.EndpointPath}", "127.0.0.1"));

            bool started = false;
            string successfulPrefix = "";
            string bindMode = "";

            // Try each binding option
            foreach (var (prefix, description) in bindingAttempts)
            {
                try
                {
                    _logger.LogDebug("Attempting to bind to {Prefix} ({Description})", prefix, description);

                    // Clear any previous prefixes
                    _httpListener.Prefixes.Clear();
                    _httpListener.Prefixes.Add(prefix);

                    _httpListener.Start();

                    successfulPrefix = prefix;
                    bindMode = description;
                    started = true;

                    _logger.LogInformation("Successfully bound to {Prefix} ({Description})", prefix, description);
                    break;
                }
                catch (HttpListenerException ex) when (ex.ErrorCode == 5) // Access denied
                {
                    _logger.LogDebug("Access denied for {Prefix}, trying next option...", prefix);
                    _httpListener?.Stop();
                    _httpListener = new HttpListener(); // Create fresh listener
                    continue;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to bind to {Prefix}, trying next option...", prefix);
                    _httpListener?.Stop();
                    _httpListener = new HttpListener(); // Create fresh listener
                    continue;
                }
            }

            if (!started)
            {
                _logger.LogError("===================================================================");
                _logger.LogError("FAILED TO START WEBSOCKET SERVER");
                _logger.LogError("===================================================================");
                _logger.LogError("");
                _logger.LogError("Could not bind to any network interface on port {Port}", _settings.Port);
                _logger.LogError("");
                _logger.LogError("SOLUTIONS:");
                _logger.LogError("  1. Run setup-urlacl.bat as Administrator (recommended)");
                _logger.LogError("  2. Run this application as Administrator");
                _logger.LogError("  3. Check if another application is using port {Port}", _settings.Port);
                _logger.LogError("  4. Check Windows Firewall settings");
                _logger.LogError("");
                _logger.LogError("===================================================================");

                throw new InvalidOperationException(
                    $"Cannot start WebSocket server on port {_settings.Port}. " +
                    $"No binding option succeeded. Check logs for details.");
            }

            // Warn if running in limited mode
            if (bindMode.Contains("localhost") || bindMode.Contains("127.0.0.1"))
            {
                _logger.LogWarning("===================================================================");
                _logger.LogWarning("SERVER RUNNING IN LIMITED MODE");
                _logger.LogWarning("===================================================================");
                _logger.LogWarning("");
                _logger.LogWarning("The server is only accessible from this machine.");
                _logger.LogWarning("External clients (like Raspberry Pi) CANNOT connect!");
                _logger.LogWarning("");
                _logger.LogWarning("To enable external access:");
                _logger.LogWarning("  1. Run setup-urlacl.bat as Administrator");
                _logger.LogWarning("  2. Restart the application");
                _logger.LogWarning("");
                _logger.LogWarning("===================================================================");
            }

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

            var protocol = _settings.EnableSsl ? "HTTPS/WSS" : "HTTP/WS";
            _logger.LogInformation("WebSocket server started on port {Port} using {Protocol} ({BindMode})",
                _settings.Port, protocol, bindMode);
            _logger.LogInformation("WebSocket endpoint: {Endpoint}", successfulPrefix);

            // Show all available connection URLs
            if (!bindMode.Contains("localhost") && !bindMode.Contains("127.0.0.1"))
            {
                _logger.LogInformation("Clients can connect using:");
                if (successfulPrefix.Contains("+"))
                {
                    // Wildcard binding - show all IPs
                    foreach (var ip in localIPs)
                    {
                        _logger.LogInformation("  - ws://{IP}:{Port}{Path}", ip, _settings.Port, _settings.EndpointPath);
                    }
                }
                else
                {
                    _logger.LogInformation("  - {Endpoint}", successfulPrefix.Replace("http://", "ws://").Replace("https://", "wss://"));
                }
            }

            // Track the accept clients task instead of fire-and-forget
            _acceptClientsTask = Task.Run(() => AcceptClientsAsync(_cancellationTokenSource.Token));

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start WebSocket server");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        _logger.LogInformation("Stopping WebSocket communication service...");

        // 1. Cancel accept loop
        _cancellationTokenSource?.Cancel();

        // 2. Stop and close HTTP listener
        if (_httpListener != null)
        {
            try
            {
                _httpListener.Stop();
                _httpListener.Close();
                _logger.LogDebug("HttpListener stopped and closed");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping HttpListener");
            }
        }

        // 3. Close all client connections gracefully
        var socketsSnapshot = _clients.Values.ToList();
        var closeTasks = socketsSnapshot.Select(async socket =>
        {
            try
            {
                if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
                {
                    await socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Server shutting down",
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing client WebSocket during shutdown");
            }
        });

        try
        {
            await Task.WhenAll(closeTasks);
            _logger.LogInformation("All {Count} client connections closed", socketsSnapshot.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error waiting for client connections to close");
        }

        // 4. Dispose all sockets and clear dictionary
        foreach (var socket in socketsSnapshot)
        {
            try { socket.Dispose(); } catch { }
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

        _logger.LogInformation("WebSocket communication service stopped successfully");
    }

    public async Task SendMessageAsync(
        string clientId,
        Message message,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_clients.TryGetValue(clientId, out var socket))
        {
            try
            {
                if (socket.State != WebSocketState.Open)
                {
                    _logger.LogWarning("Cannot send message to client {ClientId}: connection not open", clientId);
                    return;
                }

                // Delegate to WebSocketMessageSerializer
                await _messageSerializer.SendMessageAsync(socket, clientId, message, cancellationToken);
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
        // Send to all connected clients using WebSocketMessageSerializer
        var tasks = _clients.Select(kvp =>
            _messageSerializer.SendMessageAsync(kvp.Value, kvp.Key, message, cancellationToken));

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

                    // Track each client handler task instead of fire-and-forget
                    var handlerTask = Task.Run(() => HandleClientAsync(clientId, wsContext.WebSocket, cancellationToken));
                    _clientHandlerTasks[clientId] = handlerTask;
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
        try
        {
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Delegate to WebSocketMessageSerializer for receiving and deserializing
                    var message = await _messageSerializer.ReceiveMessageAsync(socket, clientId, cancellationToken);

                    if (message != null)
                    {
                        // Check if this is a mobile app message (has mobile app message type)
                        if (IsMobileAppMessage(message))
                        {
                            await HandleMobileAppMessageAsync(clientId, socket, message, cancellationToken);
                        }
                        else
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
                    }
                }
                catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                {
                    _logger.LogInformation("Client {ClientId} closed connection without completing handshake", clientId);
                    break;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogDebug("Client {ClientId} message receive cancelled", clientId);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message from client {ClientId}", clientId);
                    // Continue processing to maintain connection
                    continue;
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
        WebSocket socket,
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
                    await HandleAppRegisterAsync(connectionId, socket, message as AppRegisterMessage);
                    break;

                case MobileAppMessageTypes.AppHeartbeat:
                    await HandleAppHeartbeatAsync(connectionId, socket, message as AppHeartbeatMessage);
                    break;

                case MobileAppMessageTypes.RequestClientList:
                    await HandleRequestClientListAsync(connectionId, socket, message as RequestClientListMessage);
                    break;

                case MobileAppMessageTypes.SendCommand:
                    await HandleSendCommandAsync(connectionId, socket, message as SendCommandMessage);
                    break;

                case MobileAppMessageTypes.AssignLayout:
                    await HandleAssignLayoutAsync(connectionId, socket, message as AssignLayoutMessage);
                    break;

                case MobileAppMessageTypes.RequestScreenshot:
                    await HandleRequestScreenshotAsync(connectionId, socket, message as RequestScreenshotMessage);
                    break;

                case MobileAppMessageTypes.RequestLayoutList:
                    await HandleRequestLayoutListAsync(connectionId, socket, message as RequestLayoutListMessage);
                    break;

                default:
                    _logger.LogWarning("Unknown mobile app message type: {MessageType}", message.Type);
                    await SendErrorAsync(socket, $"Unknown message type: {message.Type}");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling mobile app message {MessageType}", message.Type);
            await SendErrorAsync(socket, "Internal server error");
        }
    }

    /// <summary>
    /// Handle mobile app registration
    /// </summary>
    private async Task HandleAppRegisterAsync(string connectionId, WebSocket socket, AppRegisterMessage? message)
    {
        if (message == null)
        {
            await SendErrorAsync(socket, "Invalid registration message");
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
                _mobileAppConnections[connectionId] = socket;
                _mobileAppIds[connectionId] = registration.Id;

                // Fire event for new registration
                OnNewAppRegistration?.Invoke(this, registration);

                // If already approved, send token immediately
                if (registration.Status == AppRegistrationStatus.Approved && !string.IsNullOrEmpty(registration.Token))
                {
                    _mobileAppTokens[connectionId] = registration.Token;

                    await SendMessageAsync(socket, new AppAuthorizedMessage
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
                    await SendMessageAsync(socket, new AppAuthorizationRequiredMessage
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
                await SendErrorAsync(socket, result.ErrorMessage ?? "Registration failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during mobile app registration");
            await SendErrorAsync(socket, "Registration failed");
        }
    }

    /// <summary>
    /// Handle mobile app heartbeat
    /// </summary>
    private async Task HandleAppHeartbeatAsync(string connectionId, WebSocket socket, AppHeartbeatMessage? message)
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
    private async Task HandleRequestClientListAsync(string connectionId, WebSocket socket, RequestClientListMessage? message)
    {
        if (message == null)
        {
            await SendErrorAsync(socket, "Invalid request");
            return;
        }

        // Validate token
        if (!_mobileAppTokens.TryGetValue(connectionId, out var token))
        {
            await SendErrorAsync(socket, "Not authenticated");
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
                await SendErrorAsync(socket, "Unauthorized");
                return;
            }

            if (!await mobileAppService.HasPermissionAsync(token, AppPermission.View))
            {
                await SendErrorAsync(socket, "Insufficient permissions");
                return;
            }

            // Get all clients
            var clientsResult = await clientService.GetAllClientsAsync();
            if (!clientsResult.IsSuccess)
            {
                await SendErrorAsync(socket, "Failed to retrieve client list");
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

            await SendMessageAsync(socket, new ClientListUpdateMessage
            {
                Clients = clientInfos
            });

            _logger.LogDebug("Sent client list to mobile app ({Count} clients)", clientInfos.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling client list request");
            await SendErrorAsync(socket, "Failed to retrieve client list");
        }
    }

    /// <summary>
    /// Handle send command to device
    /// </summary>
    private async Task HandleSendCommandAsync(string connectionId, WebSocket socket, SendCommandMessage? message)
    {
        if (message == null)
        {
            await SendErrorAsync(socket, "Invalid command message");
            return;
        }

        // Validate token
        if (!_mobileAppTokens.TryGetValue(connectionId, out var token))
        {
            await SendErrorAsync(socket, "Not authenticated");
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
                await SendErrorAsync(socket, "Unauthorized");
                return;
            }

            // Find target device WebSocket
            var targetClientId = message.TargetDeviceId.ToString();
            if (!_clients.TryGetValue(targetClientId, out var targetSocket))
            {
                await SendErrorAsync(socket, "Device not connected");
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
            await SendMessageAsync(socket, new CommandResultMessage
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
            await SendErrorAsync(socket, "Failed to send command");
        }
    }

    /// <summary>
    /// Handle assign layout to device
    /// </summary>
    private async Task HandleAssignLayoutAsync(string connectionId, WebSocket socket, AssignLayoutMessage? message)
    {
        if (message == null)
        {
            await SendErrorAsync(socket, "Invalid assign layout message");
            return;
        }

        // Validate token
        if (!_mobileAppTokens.TryGetValue(connectionId, out var token))
        {
            await SendErrorAsync(socket, "Not authenticated");
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
                await SendErrorAsync(socket, "Unauthorized");
                return;
            }

            // Assign layout to device
            var result = await clientService.AssignLayoutAsync(message.DeviceId.ToString(), message.LayoutId);
            if (result.IsSuccess)
            {
                await SendMessageAsync(socket, new CommandResultMessage
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
                await SendErrorAsync(socket, result.ErrorMessage ?? "Failed to assign layout");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling assign layout");
            await SendErrorAsync(socket, "Failed to assign layout");
        }
    }

    /// <summary>
    /// Handle request screenshot from device
    /// </summary>
    private async Task HandleRequestScreenshotAsync(string connectionId, WebSocket socket, RequestScreenshotMessage? message)
    {
        if (message == null)
        {
            await SendErrorAsync(socket, "Invalid screenshot request");
            return;
        }

        // Validate token
        if (!_mobileAppTokens.TryGetValue(connectionId, out var token))
        {
            await SendErrorAsync(socket, "Not authenticated");
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
                await SendErrorAsync(socket, "Unauthorized");
                return;
            }

            // Find target device WebSocket
            var targetClientId = message.DeviceId.ToString();
            if (!_clients.TryGetValue(targetClientId, out var targetSocket))
            {
                await SendErrorAsync(socket, "Device not connected");
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
            await SendErrorAsync(socket, "Failed to request screenshot");
        }
    }

    /// <summary>
    /// Handle request for layout list
    /// </summary>
    private async Task HandleRequestLayoutListAsync(string connectionId, WebSocket socket, RequestLayoutListMessage? message)
    {
        if (message == null)
        {
            await SendErrorAsync(socket, "Invalid layout list request");
            return;
        }

        // Validate token
        if (!_mobileAppTokens.TryGetValue(connectionId, out var token))
        {
            await SendErrorAsync(socket, "Not authenticated");
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
                await SendErrorAsync(socket, "Unauthorized");
                return;
            }

            // Get all layouts
            var layoutsResult = await layoutService.GetAllLayoutsAsync();
            if (layoutsResult == null || !layoutsResult.IsSuccess || layoutsResult.Value == null || !layoutsResult.Value.Any())
            {
                await SendErrorAsync(socket, "No layouts found");
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

            await SendMessageAsync(socket, new LayoutListResponseMessage
            {
                Layouts = layoutInfos
            });

            _logger.LogDebug("Sent layout list to mobile app ({Count} layouts)", layoutInfos.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling layout list request");
            await SendErrorAsync(socket, "Failed to retrieve layout list");
        }
    }

    /// <summary>
    /// Send error message to WebSocket
    /// </summary>
    private async Task SendErrorAsync(WebSocket socket, string errorMessage)
    {
        try
        {
            // Create a CommandResultMessage to send error
            var errorMsg = new CommandResultMessage
            {
                Success = false,
                ErrorMessage = errorMessage
            };

            await _messageSerializer.SendMessageAsync(socket, "error", errorMsg, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send error message to WebSocket");
        }
    }

    /// <summary>
    /// Send message directly to a WebSocket
    /// </summary>
    private async Task SendMessageAsync(WebSocket socket, Message message)
    {
        try
        {
            await _messageSerializer.SendMessageAsync(socket, "mobile-app", message, CancellationToken.None);
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
                _httpListener?.Close();
                ((IDisposable?)_httpListener)?.Dispose();
                _cancellationTokenSource?.Dispose();

                // Dispose any remaining sockets
                foreach (var socket in _clients.Values.ToList())
                {
                    try { socket.Dispose(); } catch { }
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
