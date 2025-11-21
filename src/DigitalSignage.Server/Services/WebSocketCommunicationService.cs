using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.Configuration;
using DigitalSignage.Server.Helpers;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.WebSockets;

namespace DigitalSignage.Server.Services;

public class WebSocketCommunicationService : ICommunicationService, IDisposable
{
    private readonly ConcurrentDictionary<string, WebSocket> _clients = new();
    private readonly ConcurrentDictionary<string, Task> _clientHandlerTasks = new();
    private readonly ILogger<WebSocketCommunicationService> _logger;
    private readonly WebSocketMessageSerializer _messageSerializer;
    private readonly ServerSettings _settings;
    private HttpListener? _httpListener;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _acceptClientsTask;
    private bool _disposed = false;

    public WebSocketCommunicationService(
        ILogger<WebSocketCommunicationService> logger,
        ILogger<WebSocketMessageSerializer> serializerLogger,
        ServerSettings settings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _messageSerializer = new WebSocketMessageSerializer(serializerLogger, enableCompression: true);
    }

    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
    public event EventHandler<ClientConnectedEventArgs>? ClientConnected;
    public event EventHandler<ClientDisconnectedEventArgs>? ClientDisconnected;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            _cancellationTokenSource = new CancellationTokenSource();

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
