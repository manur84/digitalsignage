using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalSignage.Server.MessageHandlers;

/// <summary>
/// Handles heartbeat messages from clients
/// Moved from MessageHandlerService.HandleHeartbeatMessageAsync
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
        try
        {
            var heartbeatMessage = message as HeartbeatMessage;
            if (heartbeatMessage != null)
            {
                await _clientService.UpdateClientStatusAsync(
                    heartbeatMessage.ClientId,
                    heartbeatMessage.Status,
                    heartbeatMessage.DeviceInfo);

                _logger.LogDebug("Heartbeat received from client {ClientId}", heartbeatMessage.ClientId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling HEARTBEAT message from client {ClientId}", connectionId);
        }
    }
}
