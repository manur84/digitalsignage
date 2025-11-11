using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace DigitalSignage.Server.Services;

public class ClientService : IClientService
{
    private readonly ConcurrentDictionary<string, RaspberryPiClient> _clients = new();
    private readonly ICommunicationService _communicationService;
    private readonly ILayoutService _layoutService;
    private readonly ISqlDataService _dataService;
    private readonly ITemplateService _templateService;
    private readonly ILogger<ClientService> _logger;

    public ClientService(
        ICommunicationService communicationService,
        ILayoutService layoutService,
        ISqlDataService dataService,
        ITemplateService templateService,
        ILogger<ClientService> logger)
    {
        _communicationService = communicationService ?? throw new ArgumentNullException(nameof(communicationService));
        _layoutService = layoutService ?? throw new ArgumentNullException(nameof(layoutService));
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<List<RaspberryPiClient>> GetAllClientsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_clients.Values.ToList());
    }

    public Task<RaspberryPiClient?> GetClientByIdAsync(string clientId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            _logger.LogWarning("GetClientByIdAsync called with null or empty clientId");
            return Task.FromResult<RaspberryPiClient?>(null);
        }

        _clients.TryGetValue(clientId, out var client);
        return Task.FromResult(client);
    }

    public Task<RaspberryPiClient> RegisterClientAsync(
        RegisterMessage registerMessage,
        CancellationToken cancellationToken = default)
    {
        if (registerMessage == null)
        {
            throw new ArgumentNullException(nameof(registerMessage));
        }

        if (string.IsNullOrWhiteSpace(registerMessage.ClientId))
        {
            throw new ArgumentException("Client ID cannot be empty", nameof(registerMessage));
        }

        try
        {
            var client = new RaspberryPiClient
            {
                Id = registerMessage.ClientId,
                IpAddress = registerMessage.IpAddress ?? "unknown",
                MacAddress = registerMessage.MacAddress ?? "unknown",
                RegisteredAt = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow,
                Status = ClientStatus.Online,
                DeviceInfo = registerMessage.DeviceInfo ?? new DeviceInfo()
            };

            _clients[client.Id] = client;
            _logger.LogInformation("Registered client {ClientId} from {IpAddress}", client.Id, client.IpAddress);
            return Task.FromResult(client);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register client {ClientId}", registerMessage.ClientId);
            throw;
        }
    }

    public Task UpdateClientStatusAsync(
        string clientId,
        ClientStatus status,
        DeviceInfo? deviceInfo = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            _logger.LogWarning("UpdateClientStatusAsync called with null or empty clientId");
            return Task.CompletedTask;
        }

        if (_clients.TryGetValue(clientId, out var client))
        {
            client.Status = status;
            client.LastSeen = DateTime.UtcNow;
            if (deviceInfo != null)
            {
                client.DeviceInfo = deviceInfo;
            }

            _logger.LogDebug("Updated client {ClientId} status to {Status}", clientId, status);
        }
        else
        {
            _logger.LogWarning("Client {ClientId} not found for status update", clientId);
        }

        return Task.CompletedTask;
    }

    public async Task<bool> SendCommandAsync(
        string clientId,
        string command,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            _logger.LogWarning("SendCommandAsync called with null or empty clientId");
            return false;
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            _logger.LogWarning("SendCommandAsync called with null or empty command");
            return false;
        }

        if (!_clients.ContainsKey(clientId))
        {
            _logger.LogWarning("Client {ClientId} not found for command {Command}", clientId, command);
            return false;
        }

        var commandMessage = new CommandMessage
        {
            Command = command,
            Parameters = parameters
        };

        try
        {
            await _communicationService.SendMessageAsync(clientId, commandMessage, cancellationToken);
            _logger.LogInformation("Sent command {Command} to client {ClientId}", command, clientId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send command {Command} to client {ClientId}", command, clientId);
            return false;
        }
    }

    public async Task<bool> AssignLayoutAsync(
        string clientId,
        string layoutId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            _logger.LogWarning("AssignLayoutAsync called with null or empty clientId");
            return false;
        }

        if (string.IsNullOrWhiteSpace(layoutId))
        {
            _logger.LogWarning("AssignLayoutAsync called with null or empty layoutId");
            return false;
        }

        if (_clients.TryGetValue(clientId, out var client))
        {
            client.AssignedLayoutId = layoutId;
            _logger.LogInformation("Assigned layout {LayoutId} to client {ClientId}", layoutId, clientId);

            // Send layout update to client
            return await SendLayoutToClientAsync(clientId, layoutId, cancellationToken);
        }

        _logger.LogWarning("Client {ClientId} not found for layout assignment", clientId);
        return false;
    }

    private async Task<bool> SendLayoutToClientAsync(
        string clientId,
        string layoutId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Load layout
            var layout = await _layoutService.GetLayoutByIdAsync(layoutId, cancellationToken);
            if (layout == null)
            {
                _logger.LogError("Layout {LayoutId} not found", layoutId);
                return false;
            }

            // Fetch data from all data sources
            var layoutData = new Dictionary<string, object>();
            if (layout.DataSources != null && layout.DataSources.Count > 0)
            {
                foreach (var dataSource in layout.DataSources)
                {
                    try
                    {
                        var data = await _dataService.GetDataAsync(dataSource, cancellationToken);
                        layoutData[dataSource.Id] = data;
                        _logger.LogDebug("Loaded data for source {DataSourceId}", dataSource.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load data for source {DataSourceId}, using empty data", dataSource.Id);
                        layoutData[dataSource.Id] = new Dictionary<string, object>();
                    }
                }
            }

            // Process templates in layout elements
            // Flatten all data into a single dictionary for template processing
            var templateData = new Dictionary<string, object>();
            foreach (var kvp in layoutData)
            {
                if (kvp.Value is Dictionary<string, object> dict)
                {
                    foreach (var dataKvp in dict)
                    {
                        templateData[dataKvp.Key] = dataKvp.Value;
                    }
                }
            }

            // Process text elements with templates
            if (layout.Elements != null && layout.Elements.Count > 0)
            {
                foreach (var element in layout.Elements)
                {
                    if (element.Type == "text" && !string.IsNullOrWhiteSpace(element.Content))
                    {
                        try
                        {
                            element.Content = await _templateService.ProcessTemplateAsync(
                                element.Content,
                                templateData,
                                cancellationToken);
                            _logger.LogDebug("Processed template for element {ElementId}", element.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to process template for element {ElementId}, using original content", element.Id);
                        }
                    }
                }
            }

            // Create and send display update message
            var displayUpdateMessage = new DisplayUpdateMessage
            {
                LayoutId = layoutId,
                Layout = layout,
                Data = layoutData
            };

            await _communicationService.SendMessageAsync(clientId, displayUpdateMessage, cancellationToken);
            _logger.LogInformation("Sent DISPLAY_UPDATE with full layout {LayoutId} and {DataSourceCount} data sources to client {ClientId}",
                layoutId, layout.DataSources?.Count ?? 0, clientId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send layout update to client {ClientId}", clientId);
            return false;
        }
    }

    public Task<bool> RemoveClientAsync(string clientId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            _logger.LogWarning("RemoveClientAsync called with null or empty clientId");
            return Task.FromResult(false);
        }

        if (_clients.TryRemove(clientId, out _))
        {
            _logger.LogInformation("Removed client {ClientId}", clientId);
            return Task.FromResult(true);
        }

        _logger.LogWarning("Client {ClientId} not found for removal", clientId);
        return Task.FromResult(false);
    }
}
