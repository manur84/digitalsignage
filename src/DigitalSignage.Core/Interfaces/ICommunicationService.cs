using DigitalSignage.Core.Models;

namespace DigitalSignage.Core.Interfaces;

/// <summary>
/// Interface for server-client communication
/// </summary>
public interface ICommunicationService
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task SendMessageAsync(string clientId, Message message, CancellationToken cancellationToken = default);
    Task BroadcastMessageAsync(Message message, CancellationToken cancellationToken = default);
    void UpdateClientId(string oldClientId, string newClientId);

    event EventHandler<MessageReceivedEventArgs>? MessageReceived;
    event EventHandler<ClientConnectedEventArgs>? ClientConnected;
    event EventHandler<ClientDisconnectedEventArgs>? ClientDisconnected;
}

public class MessageReceivedEventArgs : EventArgs
{
    public string ClientId { get; set; } = string.Empty;
    public Message Message { get; set; } = null!;
}

public class ClientConnectedEventArgs : EventArgs
{
    public string ClientId { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
}

public class ClientDisconnectedEventArgs : EventArgs
{
    public string ClientId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
