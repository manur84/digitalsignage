using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalSignage.Server.MessageHandlers;

/// <summary>
/// Handles StatusReport messages from Pi clients
/// Moved from MessageHandlerService.HandleStatusReportMessageAsync
/// </summary>
public class StatusReportMessageHandler : MessageHandlerBase
{
    private readonly ILogger<StatusReportMessageHandler> _logger;
    private readonly IClientService _clientService;

    public override string MessageType => MessageTypes.StatusReport;

    public StatusReportMessageHandler(
        ILogger<StatusReportMessageHandler> logger,
        IClientService clientService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clientService = clientService ?? throw new ArgumentNullException(nameof(clientService));
    }

    public override async Task HandleAsync(Message message, string connectionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var statusMessage = message as StatusReportMessage;
            if (statusMessage != null)
            {
                await _clientService.UpdateClientStatusAsync(
                    statusMessage.ClientId,
                    statusMessage.Status,
                    statusMessage.DeviceInfo);

                if (!string.IsNullOrEmpty(statusMessage.ErrorMessage))
                {
                    _logger.LogWarning("Client {ClientId} reported error: {Error}",
                        statusMessage.ClientId, statusMessage.ErrorMessage);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling STATUS_REPORT message from client {ClientId}", connectionId);
        }
    }
}
