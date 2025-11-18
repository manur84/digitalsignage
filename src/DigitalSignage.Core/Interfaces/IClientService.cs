using DigitalSignage.Core.Models;

namespace DigitalSignage.Core.Interfaces;

/// <summary>
/// Interface for managing Raspberry Pi clients
/// </summary>
public interface IClientService
{
    /// <summary>
    /// Gets all registered clients
    /// </summary>
    Task<Result<List<RaspberryPiClient>>> GetAllClientsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a client by ID
    /// </summary>
    Task<Result<RaspberryPiClient>> GetClientByIdAsync(string clientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a new client
    /// </summary>
    Task<Result<RaspberryPiClient>> RegisterClientAsync(RegisterMessage registerMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a client's status
    /// </summary>
    Task<Result> UpdateClientStatusAsync(string clientId, ClientStatus status, DeviceInfo? deviceInfo = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a command to a client
    /// </summary>
    Task<Result> SendCommandAsync(string clientId, string command, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns a layout to a client
    /// </summary>
    Task<Result> AssignLayoutAsync(string clientId, string layoutId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a client from the system
    /// </summary>
    Task<Result> RemoveClientAsync(string clientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a client's configuration
    /// </summary>
    Task<Result> UpdateClientConfigAsync(string clientId, UpdateConfigMessage config, CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised when a client connects
    /// </summary>
    event EventHandler<string>? ClientConnected;

    /// <summary>
    /// Event raised when a client disconnects
    /// </summary>
    event EventHandler<string>? ClientDisconnected;

    /// <summary>
    /// Event raised when a client status changes
    /// </summary>
    event EventHandler<string>? ClientStatusChanged;
}
