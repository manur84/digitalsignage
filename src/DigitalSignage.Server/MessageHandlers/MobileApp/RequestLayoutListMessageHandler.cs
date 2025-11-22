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
/// Handles RequestLayoutList messages from mobile apps
/// Moved from WebSocketCommunicationService.HandleRequestLayoutListAsync
/// </summary>
public class RequestLayoutListMessageHandler : MessageHandlerBase
{
    private readonly ILogger<RequestLayoutListMessageHandler> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly MobileAppConnectionManager _connectionManager;

    public override string MessageType => MobileAppMessageTypes.RequestLayoutList;

    public RequestLayoutListMessageHandler(
        ILogger<RequestLayoutListMessageHandler> logger,
        IServiceProvider serviceProvider,
        MobileAppConnectionManager connectionManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
    }

    public override async Task HandleAsync(Message message, string connectionId, CancellationToken cancellationToken = default)
    {
        var requestMsg = message as RequestLayoutListMessage;
        if (requestMsg == null)
        {
            await _connectionManager.SendErrorAsync(connectionId, "Invalid layout list request", cancellationToken);
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
            var layoutService = scope.ServiceProvider.GetRequiredService<ILayoutService>();

            // Validate token and check View permission
            var registration = await mobileAppService.ValidateTokenAsync(token);
            if (registration == null || !await mobileAppService.HasPermissionAsync(token, AppPermission.View))
            {
                await _connectionManager.SendErrorAsync(connectionId, "Unauthorized", cancellationToken);
                return;
            }

            // Get all layouts
            var layoutsResult = await layoutService.GetAllLayoutsAsync();
            if (layoutsResult == null || !layoutsResult.IsSuccess || layoutsResult.Value == null || !layoutsResult.Value.Any())
            {
                await _connectionManager.SendErrorAsync(connectionId, "No layouts found", cancellationToken);
                return;
            }

            // Map to LayoutInfo DTOs
            var layoutInfos = layoutsResult.Value.Select(l => new LayoutInfo
            {
                Id = l.Id ?? string.Empty,
                Name = l.Name ?? "Unnamed Layout",
                Description = l.Description,
                Category = l.Category,
                Created = l.Created,
                Modified = l.Modified,
                Width = l.Resolution?.Width ?? 1920,
                Height = l.Resolution?.Height ?? 1080
            }).ToList();

            await _connectionManager.SendMessageAsync(connectionId, new LayoutListResponseMessage
            {
                Layouts = layoutInfos
            }, cancellationToken);

            _logger.LogDebug("Sent layout list to mobile app ({Count} layouts)", layoutInfos.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling layout list request");
            await _connectionManager.SendErrorAsync(connectionId, "Failed to retrieve layout list", cancellationToken);
        }
    }
}
