using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalSignage.Server.MessageHandlers.MobileApp;

/// <summary>
/// Handles SendCommand messages from mobile apps
/// Moved from WebSocketCommunicationService.HandleSendCommandAsync
/// </summary>
public class SendCommandMessageHandler : MessageHandlerBase
{
    private readonly ILogger<SendCommandMessageHandler> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly MobileAppConnectionManager _connectionManager;
    private readonly ICommunicationService _communicationService;

    public override string MessageType => MobileAppMessageTypes.SendCommand;

    public SendCommandMessageHandler(
        ILogger<SendCommandMessageHandler> logger,
        IServiceProvider serviceProvider,
        MobileAppConnectionManager connectionManager,
        ICommunicationService communicationService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        _communicationService = communicationService ?? throw new ArgumentNullException(nameof(communicationService));
    }

    public override async Task HandleAsync(Message message, string connectionId, CancellationToken cancellationToken = default)
    {
        var commandMsg = message as SendCommandMessage;
        if (commandMsg == null)
        {
            await _connectionManager.SendErrorAsync(connectionId, "Invalid command message", cancellationToken);
            return;
        }

        // Validate token
        var token = _connectionManager.GetToken(connectionId);
        if (string.IsNullOrEmpty(token))
        {
            await _connectionManager.SendErrorAsync(connectionId, "Not authenticated", cancellationToken);
            return;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var mobileAppService = scope.ServiceProvider.GetRequiredService<IMobileAppService>();

            // Validate token and check Control permission
            var registration = await mobileAppService.ValidateTokenAsync(token);
            if (registration == null || !await mobileAppService.HasPermissionAsync(token, AppPermission.Control))
            {
                await _connectionManager.SendErrorAsync(connectionId, "Unauthorized", cancellationToken);
                return;
            }

            // Check if device is connected
            var targetClientId = commandMsg.TargetDeviceId.ToString();
            if (!_communicationService.IsClientConnected(targetClientId))
            {
                await _connectionManager.SendErrorAsync(connectionId, "Device not connected", cancellationToken);
                return;
            }

            // Forward command to Pi client
            var piCommandMessage = new CommandMessage
            {
                Command = commandMsg.Command,
                Parameters = commandMsg.Parameters
            };

            // Send command to Pi client
            await _communicationService.SendMessageAsync(targetClientId, piCommandMessage, cancellationToken);

            // Acknowledge to mobile app
            await _connectionManager.SendMessageAsync(connectionId, new CommandResultMessage
            {
                DeviceId = commandMsg.TargetDeviceId,
                Command = commandMsg.Command,
                Success = true
            }, cancellationToken);

            _logger.LogInformation("Mobile app sent command {Command} to device {DeviceId}",
                commandMsg.Command, commandMsg.TargetDeviceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling send command");
            await _connectionManager.SendErrorAsync(connectionId, "Failed to send command", cancellationToken);
        }
    }
}
