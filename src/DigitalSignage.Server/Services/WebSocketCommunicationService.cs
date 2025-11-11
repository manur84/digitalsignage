using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;

namespace DigitalSignage.Server.Services;

public class WebSocketCommunicationService : ICommunicationService
{
    private readonly ConcurrentDictionary<string, WebSocket> _clients = new();
    private readonly ILogger<WebSocketCommunicationService> _logger;
    private HttpListener? _httpListener;
    private CancellationTokenSource? _cancellationTokenSource;

    public WebSocketCommunicationService(ILogger<WebSocketCommunicationService> logger)
    {
        _logger = logger;
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
            _httpListener.Prefixes.Add("http://+:8080/ws/");
            _httpListener.Start();

            _logger.LogInformation("WebSocket server started on port 8080");

            _ = Task.Run(() => AcceptClientsAsync(_cancellationTokenSource.Token));

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
                cancellationToken).AsTask());

        await Task.WhenAll(tasks);
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
        var buffer = new byte[8192];

        try
        {
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var message = JsonConvert.DeserializeObject<Message>(json);

                if (message != null)
                {
                    MessageReceived?.Invoke(this, new MessageReceivedEventArgs
                    {
                        ClientId = clientId,
                        Message = message
                    });
                }
            }
        }
        catch (WebSocketException ex)
        {
            _logger.LogWarning(ex, "WebSocket error for client {ClientId}", clientId);
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
