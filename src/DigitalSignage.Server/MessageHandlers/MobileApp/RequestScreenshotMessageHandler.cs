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
/// Handles RequestScreenshot messages from mobile apps
/// Moved from WebSocketCommunicationService.HandleRequestScreenshotAsync
/// </summary>
public class RequestScreenshotMessageHandler : MessageHandlerBase
{
    private readonly ILogger<RequestScreenshotMessageHandler> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly MobileAppConnectionManager _connectionManager;
    private readonly ICommunicationService _communicationService;

    public override string MessageType => MobileAppMessageTypes.RequestScreenshot;

    public RequestScreenshotMessageHandler(
        ILogger<RequestScreenshotMessageHandler> logger,
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
        var screenshotMsg = message as RequestScreenshotMessage;
        if (screenshotMsg == null)
        {
            await _connectionManager.SendErrorAsync(connectionId, "Invalid screenshot request", cancellationToken);
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

            // Validate token and check View permission
            var registration = await mobileAppService.ValidateTokenAsync(token);
            if (registration == null || !await mobileAppService.HasPermissionAsync(token, AppPermission.View))
            {
                await _connectionManager.SendErrorAsync(connectionId, "Unauthorized", cancellationToken);
                return;
            }

            // Check if device is connected
            var targetClientId = screenshotMsg.DeviceId.ToString();
            if (!_communicationService.IsClientConnected(targetClientId))
            {
                await _connectionManager.SendErrorAsync(connectionId, "Device not connected", cancellationToken);
                return;
            }

            // Request screenshot from Pi client
            var commandMessage = new CommandMessage
            {
                Command = "Screenshot"
            };

            await _communicationService.SendMessageAsync(targetClientId, commandMessage, cancellationToken);

            _logger.LogInformation("Mobile app requested screenshot from device {DeviceId}", screenshotMsg.DeviceId);

            // Note: The screenshot response will be received separately and needs to be
            // forwarded to the mobile app - this would require maintaining a mapping
            // of screenshot requests to mobile app connections
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling screenshot request");
            await _connectionManager.SendErrorAsync(connectionId, "Failed to request screenshot", cancellationToken);
        }
    }
}
