using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.Server.Services;

public class DataRefreshService : BackgroundService
{
    private readonly IClientService _clientService;
    private readonly ILayoutService _layoutService;
    private readonly ISqlDataService _dataService;
    private readonly IScribanService _scribanService;
    private readonly ICommunicationService _communicationService;
    private readonly ILogger<DataRefreshService> _logger;

    public DataRefreshService(
        IClientService clientService,
        ILayoutService layoutService,
        ISqlDataService dataService,
        IScribanService scribanService,
        ICommunicationService communicationService,
        ILogger<DataRefreshService> logger)
    {
        _clientService = clientService ?? throw new ArgumentNullException(nameof(clientService));
        _layoutService = layoutService ?? throw new ArgumentNullException(nameof(layoutService));
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        _scribanService = scribanService ?? throw new ArgumentNullException(nameof(scribanService));
        _communicationService = communicationService ?? throw new ArgumentNullException(nameof(communicationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("→ DataRefreshService.ExecuteAsync BEGIN");
        _logger.LogInformation("DataRefreshService started");

        // Wait for database initialization to complete
        _logger.LogInformation("Waiting 15 seconds for database initialization...");
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        _logger.LogInformation("← DataRefreshService.ExecuteAsync initialization complete, starting monitoring");

        // Main loop to check for clients that need data refresh
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshActiveClientsDataAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DataRefreshService main loop");
            }

            // Check every 30 seconds
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }

        _logger.LogInformation("DataRefreshService stopped");
    }

    private async Task RefreshActiveClientsDataAsync(CancellationToken cancellationToken)
    {
        try
        {
            var clientsResult = await _clientService.GetAllClientsAsync(cancellationToken);
            if (clientsResult.IsFailure || clientsResult.Value == null || clientsResult.Value.Count == 0)
            {
                _logger.LogDebug("No clients available for data refresh: {Error}", clientsResult.ErrorMessage);
                return;
            }

            foreach (var client in clientsResult.Value)
            {
                if (client.Status != ClientStatus.Online || string.IsNullOrWhiteSpace(client.AssignedLayoutId))
                {
                    continue;
                }

                try
                {
                    await RefreshClientDataAsync(client.Id, client.AssignedLayoutId, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to refresh data for client {ClientId}", client.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get clients for data refresh");
        }
    }

    private async Task RefreshClientDataAsync(string clientId, string layoutId, CancellationToken cancellationToken)
    {
        try
        {
            var layoutResult = await _layoutService.GetLayoutByIdAsync(layoutId, cancellationToken);
            if (layoutResult == null || layoutResult.IsFailure || layoutResult.Value == null)
            {
                _logger.LogWarning("Layout {LayoutId} not found for client {ClientId}: {Error}",
                    layoutId, clientId, layoutResult?.ErrorMessage ?? "Unknown error");
                return;
            }

            var layout = layoutResult.Value;

            if (layout.DataSources == null || layout.DataSources.Count == 0)
            {
                // No data sources to refresh
                return;
            }

            // Check if any data source needs refresh
            bool needsRefresh = false;
            foreach (var dataSource in layout.DataSources)
            {
                if (dataSource.Enabled && dataSource.RefreshInterval > 0)
                {
                    needsRefresh = true;
                    break;
                }
            }

            if (!needsRefresh)
            {
                return;
            }

            // Fetch fresh data
            var layoutData = new Dictionary<string, object>();
            foreach (var dataSource in layout.DataSources)
            {
                if (!dataSource.Enabled)
                {
                    continue;
                }

                try
                {
                    var data = await _dataService.GetDataAsync(dataSource, cancellationToken);
                    layoutData[dataSource.Id] = data;
                    _logger.LogDebug("Refreshed data for source {DataSourceId} on client {ClientId}",
                        dataSource.Id, clientId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to refresh data for source {DataSourceId}", dataSource.Id);
                    layoutData[dataSource.Id] = new Dictionary<string, object>();
                }
            }

            // Process templates in layout elements
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
                    if (element.Type == "text")
                    {
                        try
                        {
                            var content = element["Content"]?.ToString();
                            if (!string.IsNullOrWhiteSpace(content))
                            {
                                element["Content"] = await _scribanService.ProcessTemplateAsync(
                                    content,
                                    templateData,
                                    cancellationToken);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to process template for element {ElementId}", element.Id);
                        }
                    }
                }
            }

            // Send updated data to client
            var displayUpdateMessage = new DisplayUpdateMessage
            {
                Layout = layout,
                Data = layoutData
            };

            await _communicationService.SendMessageAsync(clientId, displayUpdateMessage, cancellationToken);
            _logger.LogDebug("Sent data refresh to client {ClientId} for layout {LayoutId}", clientId, layoutId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh data for client {ClientId}", clientId);
        }
    }
}
