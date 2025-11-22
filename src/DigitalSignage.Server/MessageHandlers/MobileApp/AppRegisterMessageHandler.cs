using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalSignage.Server.MessageHandlers.MobileApp;

/// <summary>
/// Handles AppRegister messages from mobile apps
/// Moved from WebSocketCommunicationService.HandleAppRegisterAsync
/// </summary>
public class AppRegisterMessageHandler : MessageHandlerBase
{
    private readonly ILogger<AppRegisterMessageHandler> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly MobileAppConnectionManager _connectionManager;
    private readonly Action<MobileAppRegistration> _onNewAppRegistration;

    public override string MessageType => MobileAppMessageTypes.AppRegister;

    public AppRegisterMessageHandler(
        ILogger<AppRegisterMessageHandler> logger,
        IServiceProvider serviceProvider,
        MobileAppConnectionManager connectionManager,
        Action<MobileAppRegistration> onNewAppRegistration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        _onNewAppRegistration = onNewAppRegistration ?? throw new ArgumentNullException(nameof(onNewAppRegistration));
    }

    public override async Task HandleAsync(Message message, string connectionId, CancellationToken cancellationToken = default)
    {
        var appRegisterMsg = message as AppRegisterMessage;
        if (appRegisterMsg == null)
        {
            await _connectionManager.SendErrorAsync(connectionId, "Invalid registration message", cancellationToken);
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

                // Update app ID for this connection (connection is already tracked by WebSocketCommunicationService)
                _connectionManager.UpdateAppId(connectionId, registration.Id);

                // Fire event for new registration
                _onNewAppRegistration?.Invoke(registration);

                // If already approved, send token immediately
                if (registration.Status == AppRegistrationStatus.Approved && !string.IsNullOrEmpty(registration.Token))
                {
                    _connectionManager.SetToken(connectionId, registration.Token);

                    await _connectionManager.SendMessageAsync(connectionId, new AppAuthorizedMessage
                    {
                        Token = registration.Token,
                        Permissions = registration.Permissions.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                        ExpiresAt = DateTime.UtcNow.AddYears(1) // Token valid for 1 year
                    }, cancellationToken);

                    _logger.LogInformation("Mobile app {DeviceName} reconnected with existing authorization", appRegisterMsg.DeviceName);
                }
                else
                {
                    // Send authorization required message
                    await _connectionManager.SendMessageAsync(connectionId, new AppAuthorizationRequiredMessage
                    {
                        AppId = registration.Id,
                        Status = "pending",
                        Message = "Registration pending. Waiting for admin approval."
                    }, cancellationToken);

                    _logger.LogInformation("New mobile app registration: {DeviceName} ({Platform})", appRegisterMsg.DeviceName, appRegisterMsg.Platform);
                }
            }
            else
            {
                await _connectionManager.SendErrorAsync(connectionId, result.ErrorMessage ?? "Registration failed", cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during mobile app registration");
            await _connectionManager.SendErrorAsync(connectionId, "Registration failed", cancellationToken);
        }
    }
}
