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
/// Handles AppHeartbeat messages from mobile apps
/// Moved from WebSocketCommunicationService.HandleAppHeartbeatAsync
/// </summary>
public class AppHeartbeatMessageHandler : MessageHandlerBase
{
    private readonly ILogger<AppHeartbeatMessageHandler> _logger;
    private readonly IServiceProvider _serviceProvider;

    public override string MessageType => MobileAppMessageTypes.AppHeartbeat;

    public AppHeartbeatMessageHandler(
        ILogger<AppHeartbeatMessageHandler> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public override async Task HandleAsync(Message message, string connectionId, CancellationToken cancellationToken = default)
    {
        var heartbeatMsg = message as AppHeartbeatMessage;
        if (heartbeatMsg == null)
        {
            return;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var mobileAppService = scope.ServiceProvider.GetRequiredService<IMobileAppService>();

            await mobileAppService.UpdateLastSeenAsync(heartbeatMsg.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error handling app heartbeat");
        }
    }
}
