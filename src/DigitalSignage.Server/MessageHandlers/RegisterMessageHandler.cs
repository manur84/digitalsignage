using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalSignage.Server.MessageHandlers;

/// <summary>
/// Handles device registration messages from Raspberry Pi clients
/// </summary>
public class RegisterMessageHandler : MessageHandlerBase
{
    private readonly IClientService _clientService;
    private readonly ICommunicationService _communicationService;
    private readonly ILogger<RegisterMessageHandler> _logger;

    public override string MessageType => MessageTypes.Register;

    public RegisterMessageHandler(
        IClientService clientService,
        ICommunicationService communicationService,
        ILogger<RegisterMessageHandler> logger)
    {
        _clientService = clientService ?? throw new ArgumentNullException(nameof(clientService));
        _communicationService = communicationService ?? throw new ArgumentNullException(nameof(communicationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task HandleAsync(Message message, string connectionId, CancellationToken cancellationToken = default)
    {
        if (message is not RegisterMessage registerMessage)
        {
            _logger.LogWarning("Invalid message type for RegisterMessageHandler: {Type}", message?.GetType().Name);
            return;
        }

        try
        {
            _logger.LogInformation("Processing registration for client {ClientId} from connection {ConnectionId}",
                registerMessage.ClientId, connectionId);

            // Validate required fields
            if (string.IsNullOrWhiteSpace(registerMessage.ClientId))
            {
                _logger.LogWarning("Registration message missing ClientId from {ConnectionId}", connectionId);
                await SendRegistrationResponse(connectionId, false, "ClientId is required", cancellationToken);
                return;
            }

            if (string.IsNullOrWhiteSpace(registerMessage.Token))
            {
                _logger.LogWarning("Registration message missing Token from {ConnectionId}", connectionId);
                await SendRegistrationResponse(connectionId, false, "Token is required", cancellationToken);
                return;
            }

            // Register client via ClientService
            var result = await _clientService.RegisterClientAsync(
                registerMessage.ClientId,
                registerMessage.Token,
                registerMessage.DeviceInfo,
                registerMessage.IpAddress ?? "unknown");

            if (result.IsSuccess)
            {
                _logger.LogInformation("Successfully registered client {ClientId}", registerMessage.ClientId);

                // Send success response
                await SendRegistrationResponse(connectionId, true, "Registration successful", cancellationToken);

                // Send assigned layout if any
                var client = result.Value;
                if (client?.AssignedLayoutId != null)
                {
                    _logger.LogDebug("Client {ClientId} has assigned layout {LayoutId}", client.Id, client.AssignedLayoutId);
                    // Layout assignment will be handled by LayoutAssignmentHandler
                }
            }
            else
            {
                _logger.LogError("Failed to register client {ClientId}: {Error}", registerMessage.ClientId, result.ErrorMessage);
                await SendRegistrationResponse(connectionId, false, result.ErrorMessage ?? "Registration failed", cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing registration for connection {ConnectionId}", connectionId);
            await SendRegistrationResponse(connectionId, false, "Internal server error", cancellationToken);
        }
    }

    private async Task SendRegistrationResponse(string connectionId, bool success, string message, CancellationToken cancellationToken)
    {
        try
        {
            var response = new RegistrationResponseMessage
            {
                Success = success,
                Message = message,
                Timestamp = DateTime.UtcNow
            };

            await _communicationService.SendMessageAsync(connectionId, response, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send registration response to {ConnectionId}", connectionId);
        }
    }
}
