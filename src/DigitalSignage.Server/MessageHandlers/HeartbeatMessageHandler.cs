using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalSignage.Server.MessageHandlers;

/// <summary>
/// Handles heartbeat messages from clients
/// Lightweight handler for frequent messages
/// </summary>
public class HeartbeatMessageHandler : MessageHandlerBase
{
    private readonly IClientService _clientService;
    private readonly ILogger<HeartbeatMessageHandler> _logger;

    public override string MessageType => MessageTypes.Heartbeat;

    public HeartbeatMessageHandler(
        IClientService clientService,
        ILogger<HeartbeatMessageHandler> logger)
    {
        _clientService = clientService ?? throw new ArgumentNullException(nameof(clientService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task HandleAsync(Message message, string connectionId, CancellationToken cancellationToken = default)
    {
        if (message is not HeartbeatMessage heartbeatMessage)
        {
            _logger.LogWarning("Invalid message type for HeartbeatMessageHandler: {Type}", message?.GetType().Name);
            return;
        }

        try
        {
            _logger.LogTrace("Processing heartbeat from connection {ConnectionId}", connectionId);

            // Update client's last seen timestamp
            // ClientService should have a lightweight UpdateLastSeen method
            await _clientService.UpdateClientLastSeenAsync(connectionId);

            _logger.LogTrace("Heartbeat processed for {ConnectionId}", connectionId);
        }
        catch (Exception ex)
        {
            // Heartbeat failures should not be critical - log and continue
            _logger.LogWarning(ex, "Error processing heartbeat from {ConnectionId}", connectionId);
        }
    }
}
