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
/// Handles RequestClientList messages from mobile apps
/// Moved from WebSocketCommunicationService.HandleRequestClientListAsync
/// </summary>
public class RequestClientListMessageHandler : MessageHandlerBase
{
    private readonly ILogger<RequestClientListMessageHandler> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly MobileAppConnectionManager _connectionManager;

    public override string MessageType => MobileAppMessageTypes.RequestClientList;

    public RequestClientListMessageHandler(
        ILogger<RequestClientListMessageHandler> logger,
        IServiceProvider serviceProvider,
        MobileAppConnectionManager connectionManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
    }

    public override async Task HandleAsync(Message message, string connectionId, CancellationToken cancellationToken = default)
    {
        var requestMsg = message as RequestClientListMessage;
        if (requestMsg == null)
        {
            await _connectionManager.SendErrorAsync(connectionId, "Invalid request", cancellationToken);
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

            // Validate token and check permissions
            var registration = await mobileAppService.ValidateTokenAsync(token);
            if (registration == null)
            {
                await _connectionManager.SendErrorAsync(connectionId, "Unauthorized", cancellationToken);
                return;
            }

            if (!await mobileAppService.HasPermissionAsync(token, AppPermission.View))
            {
                await _connectionManager.SendErrorAsync(connectionId, "Insufficient permissions", cancellationToken);
                return;
            }

            // Get all clients
            var clientsResult = await clientService.GetAllClientsAsync();
            if (!clientsResult.IsSuccess)
            {
                await _connectionManager.SendErrorAsync(connectionId, "Failed to retrieve client list", cancellationToken);
                return;
            }

            var clients = clientsResult.Value;

            // Filter by status if requested
            if (!string.IsNullOrEmpty(requestMsg.Filter))
            {
                clients = requestMsg.Filter.ToLowerInvariant() switch
                {
                    "online" => clients.Where(c => c.Status == ClientStatus.Online).ToList(),
                    "offline" => clients.Where(c => c.Status == ClientStatus.Offline).ToList(),
                    _ => clients
                };
            }

            // Map to ClientInfo DTOs
            var clientInfos = clients.Select(c => new ClientInfo
            {
                Id = Guid.TryParse(c.Id, out var guid) ? guid : Guid.Empty,
                Name = c.Name ?? c.IpAddress ?? "Unknown",
                IpAddress = c.IpAddress,
                Status = ConvertToDeviceStatus(c.Status),
                Resolution = c.DeviceInfo != null ? $"{c.DeviceInfo.ScreenWidth}x{c.DeviceInfo.ScreenHeight}" : null,
                DeviceInfo = c.DeviceInfo != null ? new DeviceInfoData
                {
                    CpuUsage = c.DeviceInfo.CpuUsage,
                    MemoryUsage = c.DeviceInfo.MemoryUsed,
                    Temperature = c.DeviceInfo.CpuTemperature,
                    DiskUsage = c.DeviceInfo.DiskUsed,
                    OsVersion = c.DeviceInfo.OsVersion,
                    AppVersion = c.DeviceInfo.ClientVersion
                } : null,
                LastSeen = c.LastSeen,
                AssignedLayoutId = c.AssignedLayoutId,
                Location = c.Location,
                Group = c.Group
            }).ToList();

            await _connectionManager.SendMessageAsync(connectionId, new ClientListUpdateMessage
            {
                Clients = clientInfos
            }, cancellationToken);

            _logger.LogDebug("Sent client list to mobile app ({Count} clients)", clientInfos.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling client list request");
            await _connectionManager.SendErrorAsync(connectionId, "Failed to retrieve client list", cancellationToken);
        }
    }

    private static DeviceStatus ConvertToDeviceStatus(ClientStatus status)
    {
        return status switch
        {
            ClientStatus.Online => DeviceStatus.Online,
            ClientStatus.Offline => DeviceStatus.Offline,
            ClientStatus.Error => DeviceStatus.Error,
            _ => DeviceStatus.Offline
        };
    }
}
