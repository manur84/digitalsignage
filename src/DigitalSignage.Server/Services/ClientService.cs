using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using System.Collections.Concurrent;

namespace DigitalSignage.Server.Services;

public class ClientService : IClientService
{
    private readonly ConcurrentDictionary<string, RaspberryPiClient> _clients = new();
    private readonly ICommunicationService _communicationService;

    public ClientService(ICommunicationService communicationService)
    {
        _communicationService = communicationService;
    }

    public Task<List<RaspberryPiClient>> GetAllClientsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_clients.Values.ToList());
    }

    public Task<RaspberryPiClient?> GetClientByIdAsync(string clientId, CancellationToken cancellationToken = default)
    {
        _clients.TryGetValue(clientId, out var client);
        return Task.FromResult(client);
    }

    public Task<RaspberryPiClient> RegisterClientAsync(
        RegisterMessage registerMessage,
        CancellationToken cancellationToken = default)
    {
        var client = new RaspberryPiClient
        {
            Id = registerMessage.ClientId,
            IpAddress = registerMessage.IpAddress,
            MacAddress = registerMessage.MacAddress,
            RegisteredAt = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow,
            Status = ClientStatus.Online,
            DeviceInfo = registerMessage.DeviceInfo
        };

        _clients[client.Id] = client;
        return Task.FromResult(client);
    }

    public Task UpdateClientStatusAsync(
        string clientId,
        ClientStatus status,
        DeviceInfo? deviceInfo = null,
        CancellationToken cancellationToken = default)
    {
        if (_clients.TryGetValue(clientId, out var client))
        {
            client.Status = status;
            client.LastSeen = DateTime.UtcNow;
            if (deviceInfo != null)
            {
                client.DeviceInfo = deviceInfo;
            }
        }

        return Task.CompletedTask;
    }

    public async Task<bool> SendCommandAsync(
        string clientId,
        string command,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var commandMessage = new CommandMessage
        {
            Command = command,
            Parameters = parameters
        };

        try
        {
            await _communicationService.SendMessageAsync(clientId, commandMessage, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public Task<bool> AssignLayoutAsync(
        string clientId,
        string layoutId,
        CancellationToken cancellationToken = default)
    {
        if (_clients.TryGetValue(clientId, out var client))
        {
            client.AssignedLayoutId = layoutId;
            // TODO: Send layout update to client
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<bool> RemoveClientAsync(string clientId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_clients.TryRemove(clientId, out _));
    }
}
