using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalSignage.Server.MessageHandlers;

/// <summary>
/// Handles AppRegister messages from mobile apps
/// </summary>
public class AppRegisterMessageHandler : MessageHandlerBase
{
    private readonly ILogger<AppRegisterMessageHandler> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, SslWebSocketConnection> _mobileAppConnections;
    private readonly ConcurrentDictionary<string, Guid> _mobileAppIds;
    private readonly ConcurrentDictionary<string, string> _mobileAppTokens;

    public override string MessageType => MobileAppMessageTypes.AppRegister;

    public AppRegisterMessageHandler(
        ILogger<AppRegisterMessageHandler> logger,
        IServiceProvider serviceProvider,
        ConcurrentDictionary<string, SslWebSocketConnection> mobileAppConnections,
        ConcurrentDictionary<string, Guid> mobileAppIds,
        ConcurrentDictionary<string, string> mobileAppTokens)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _mobileAppConnections = mobileAppConnections ?? throw new ArgumentNullException(nameof(mobileAppConnections));
        _mobileAppIds = mobileAppIds ?? throw new ArgumentNullException(nameof(mobileAppIds));
        _mobileAppTokens = mobileAppTokens ?? throw new ArgumentNullException(nameof(mobileAppTokens));
    }

    public override async Task HandleAsync(Message message, string connectionId, CancellationToken cancellationToken = default)
    {
        if (message is not AppRegisterMessage appRegisterMsg)
        {
            _logger.LogWarning("Invalid message type for AppRegisterMessageHandler");
            return;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var mobileAppService = scope.ServiceProvider.GetRequiredService<IMobileAppService>();

            var result = await mobileAppService.RegisterAppAsync(
                appRegisterMsg.DeviceName,
                appRegisterMsg.DeviceIdentifier,
                appRegisterMsg.AppVersion,
                appRegisterMsg.Platform);

            if (result.IsSuccess)
            {
                var registration = result.Value;

                // Track mobile app connection
                _mobileAppIds[connectionId] = registration.Id;

                _logger.LogInformation("Mobile app registered: {DeviceName} ({Platform}), ID={AppId}",
                    appRegisterMsg.DeviceName, appRegisterMsg.Platform, registration.Id);

                // Send appropriate response based on status
                // Note: Sending is handled by the calling service since this handler doesn't have direct access to connection
            }
            else
            {
                _logger.LogWarning("Mobile app registration failed: {Error}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during mobile app registration for connection {ConnectionId}", connectionId);
        }
    }
}
