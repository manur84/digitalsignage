using DigitalSignage.Core.Models;

namespace DigitalSignage.Core.Interfaces;

/// <summary>
/// Interface for managing Raspberry Pi clients
/// </summary>
public interface IClientService
{
    Task<List<RaspberryPiClient>> GetAllClientsAsync(CancellationToken cancellationToken = default);
    Task<RaspberryPiClient?> GetClientByIdAsync(string clientId, CancellationToken cancellationToken = default);
    Task<RaspberryPiClient> RegisterClientAsync(RegisterMessage registerMessage, CancellationToken cancellationToken = default);
    Task UpdateClientStatusAsync(string clientId, ClientStatus status, DeviceInfo? deviceInfo = null, CancellationToken cancellationToken = default);
    Task<bool> SendCommandAsync(string clientId, string command, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default);
    Task<bool> AssignLayoutAsync(string clientId, string layoutId, CancellationToken cancellationToken = default);
    Task<bool> RemoveClientAsync(string clientId, CancellationToken cancellationToken = default);
}
