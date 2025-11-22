using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalSignage.Server.MessageHandlers;

/// <summary>
/// Handles UpdateConfigResponse messages from Pi clients
/// Moved from MessageHandlerService.HandleUpdateConfigResponseAsync
/// </summary>
public class UpdateConfigResponseMessageHandler : MessageHandlerBase
{
    private readonly ILogger<UpdateConfigResponseMessageHandler> _logger;

    public override string MessageType => MessageTypes.UpdateConfigResponse;

    public UpdateConfigResponseMessageHandler(ILogger<UpdateConfigResponseMessageHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override Task HandleAsync(Message message, string connectionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var responseMessage = message as UpdateConfigResponseMessage;
            if (responseMessage != null)
            {
                if (responseMessage.Success)
                {
                    _logger.LogInformation("Client {ClientId} successfully updated configuration", connectionId);
                }
                else
                {
                    _logger.LogWarning("Client {ClientId} failed to update configuration: {Error}",
                        connectionId,
                        responseMessage.ErrorMessage ?? "Unknown error");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling UPDATE_CONFIG_RESPONSE message from client {ClientId}", connectionId);
        }

        return Task.CompletedTask;
    }
}
