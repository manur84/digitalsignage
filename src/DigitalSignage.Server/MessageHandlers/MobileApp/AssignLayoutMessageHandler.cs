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
/// Handles AssignLayout messages from mobile apps
/// Moved from WebSocketCommunicationService.HandleAssignLayoutAsync
/// </summary>
public class AssignLayoutMessageHandler : MessageHandlerBase
{
    private readonly ILogger<AssignLayoutMessageHandler> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly MobileAppConnectionManager _connectionManager;

    public override string MessageType => MobileAppMessageTypes.AssignLayout;

    public AssignLayoutMessageHandler(
        ILogger<AssignLayoutMessageHandler> logger,
        IServiceProvider serviceProvider,
        MobileAppConnectionManager connectionManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
    }

    public override async Task HandleAsync(Message message, string connectionId, CancellationToken cancellationToken = default)
    {
        var assignMsg = message as AssignLayoutMessage;
        if (assignMsg == null)
        {
            await _connectionManager.SendErrorAsync(connectionId, "Invalid assign layout message", cancellationToken);
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
            var clientService = scope.ServiceProvider.GetRequiredService<IClientService>();

            // Validate token and check Manage permission
            var registration = await mobileAppService.ValidateTokenAsync(token);
            if (registration == null || !await mobileAppService.HasPermissionAsync(token, AppPermission.Manage))
            {
                await _connectionManager.SendErrorAsync(connectionId, "Unauthorized", cancellationToken);
                return;
            }

            // Assign layout to device
            var result = await clientService.AssignLayoutAsync(assignMsg.DeviceId.ToString(), assignMsg.LayoutId);
            if (result.IsSuccess)
            {
                await _connectionManager.SendMessageAsync(connectionId, new CommandResultMessage
                {
                    DeviceId = assignMsg.DeviceId,
                    Command = "AssignLayout",
                    Success = true
                }, cancellationToken);

                _logger.LogInformation("Mobile app assigned layout {LayoutId} to device {DeviceId}",
                    assignMsg.LayoutId, assignMsg.DeviceId);
            }
            else
            {
                await _connectionManager.SendErrorAsync(connectionId, result.ErrorMessage ?? "Failed to assign layout", cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling assign layout");
            await _connectionManager.SendErrorAsync(connectionId, "Failed to assign layout", cancellationToken);
        }
    }
}
