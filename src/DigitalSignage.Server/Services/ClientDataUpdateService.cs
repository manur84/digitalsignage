using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Background service that monitors data source updates and pushes changes to affected clients
/// </summary>
public class ClientDataUpdateService : IHostedService, IDisposable
{
    private readonly DataSourceManager _dataSourceManager;
    private readonly IClientService _clientService;
    private readonly ILayoutService _layoutService;
    private readonly ICommunicationService _communicationService;
    private readonly ILogger<ClientDataUpdateService> _logger;
    private bool _disposed;

    public ClientDataUpdateService(
        DataSourceManager dataSourceManager,
        IClientService clientService,
        ILayoutService layoutService,
        ICommunicationService communicationService,
        ILogger<ClientDataUpdateService> logger)
    {
        _dataSourceManager = dataSourceManager ?? throw new ArgumentNullException(nameof(dataSourceManager));
        _clientService = clientService ?? throw new ArgumentNullException(nameof(clientService));
        _layoutService = layoutService ?? throw new ArgumentNullException(nameof(layoutService));
        _communicationService = communicationService ?? throw new ArgumentNullException(nameof(communicationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ClientDataUpdateService starting");

        // Subscribe to data source update events
        _dataSourceManager.DataSourceUpdated += OnDataSourceUpdated;

        _logger.LogInformation("ClientDataUpdateService started - monitoring data source updates");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ClientDataUpdateService stopping");

        // Unsubscribe from events
        _dataSourceManager.DataSourceUpdated -= OnDataSourceUpdated;

        _logger.LogInformation("ClientDataUpdateService stopped");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles data source update events and pushes updates to affected clients
    /// </summary>
    private async void OnDataSourceUpdated(object? sender, DataSourceUpdatedEventArgs e)
    {
        try
        {
            _logger.LogInformation("Data source {DataSourceId} ({Name}) updated - checking affected clients",
                e.DataSourceId, e.DataSourceName);

            // Find all layouts using this data source
            var layouts = await _layoutService.GetLayoutsWithDataSourceAsync(e.DataSourceId);

            if (layouts.Count == 0)
            {
                _logger.LogDebug("No layouts using data source {DataSourceId}", e.DataSourceId);
                return;
            }

            _logger.LogInformation("Found {LayoutCount} layouts using data source {DataSourceId}",
                layouts.Count, e.DataSourceId);

            // Find all clients with these layouts
            var allClients = await _clientService.GetAllClientsAsync();
            var affectedClients = allClients.Where(client =>
                client.AssignedLayoutId != null &&
                layouts.Any(layout => layout.Id == client.AssignedLayoutId))
                .ToList();

            if (affectedClients.Count == 0)
            {
                _logger.LogDebug("No clients displaying layouts with data source {DataSourceId}", e.DataSourceId);
                return;
            }

            _logger.LogInformation("Sending data update to {ClientCount} affected clients", affectedClients.Count);

            // Create update message
            var message = new DataUpdateMessage
            {
                DataSourceId = e.DataSourceId,
                Data = e.Data,
                Timestamp = e.Timestamp
            };

            // Send update to each affected client
            var successCount = 0;
            var failureCount = 0;

            foreach (var client in affectedClients)
            {
                try
                {
                    await _communicationService.SendMessageAsync(
                        client.Id.ToString(),
                        message,
                        CancellationToken.None);

                    successCount++;
                    _logger.LogDebug("Sent data update to client {ClientId} ({ClientName})",
                        client.Id, client.Name);
                }
                catch (Exception ex)
                {
                    failureCount++;
                    _logger.LogWarning(ex, "Failed to send data update to client {ClientId} ({ClientName})",
                        client.Id, client.Name);
                }
            }

            _logger.LogInformation(
                "Data update completed for source {DataSourceId}: {SuccessCount} succeeded, {FailureCount} failed",
                e.DataSourceId, successCount, failureCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing data source update for {DataSourceId}", e.DataSourceId);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _dataSourceManager.DataSourceUpdated -= OnDataSourceUpdated;
        _disposed = true;

        _logger.LogInformation("ClientDataUpdateService disposed");
    }
}
