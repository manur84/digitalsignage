using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalSignage.Server.MessageHandlers;

/// <summary>
/// Handles device registration messages from Raspberry Pi clients
/// Moved from MessageHandlerService.HandleRegisterMessageAsync
/// </summary>
public class RegisterMessageHandler : MessageHandlerBase
{
    private readonly IClientService _clientService;
    private readonly ILogger<RegisterMessageHandler> _logger;

    public override string MessageType => MessageTypes.Register;

    public RegisterMessageHandler(
        IClientService clientService,
        ILogger<RegisterMessageHandler> logger)
    {
        _clientService = clientService ?? throw new ArgumentNullException(nameof(clientService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task HandleAsync(Message message, string connectionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var registerMessage = message as RegisterMessage;
            if (registerMessage != null)
            {
                var result = await _clientService.RegisterClientAsync(registerMessage);
                if (result.IsSuccess && result.Value != null)
                {
                    var registeredClient = result.Value;
                    _logger.LogInformation("Client registered: {ClientId} from {IpAddress}",
                        registeredClient.Id, registeredClient.IpAddress);
                }
                else
                {
                    _logger.LogWarning("Client registration failed: {Error}", result.ErrorMessage);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling REGISTER message from client {ClientId}", connectionId);
        }
    }
}
