using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Background service that monitors client heartbeats and marks inactive clients as offline
/// </summary>
public class HeartbeatMonitoringService : BackgroundService
{
    private readonly IClientService _clientService;
    private readonly ILogger<HeartbeatMonitoringService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30); // Check every 30 seconds
    private readonly TimeSpan _heartbeatTimeout = TimeSpan.FromSeconds(120); // 2 minutes without heartbeat = offline

    public HeartbeatMonitoringService(
        IClientService clientService,
        ILogger<HeartbeatMonitoringService> logger)
    {
        _clientService = clientService ?? throw new ArgumentNullException(nameof(clientService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Heartbeat Monitoring Service starting with {Interval}s check interval and {Timeout}s timeout",
            _checkInterval.TotalSeconds, _heartbeatTimeout.TotalSeconds);

        // Wait for database initialization to complete
        _logger.LogInformation("Waiting 15 seconds for database initialization...");
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        _logger.LogInformation("Starting heartbeat monitoring");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckClientHeartbeatsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during heartbeat monitoring check");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Service is stopping
                break;
            }
        }

        _logger.LogInformation("Heartbeat Monitoring Service stopped");
    }

    private async Task CheckClientHeartbeatsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var clientsResult = await _clientService.GetAllClientsAsync(cancellationToken);
            if (clientsResult.IsFailure || clientsResult.Value == null || clientsResult.Value.Count == 0)
            {
                _logger.LogDebug("No clients registered for heartbeat monitoring: {Error}",
                    clientsResult.ErrorMessage);
                return;
            }

            var clients = clientsResult.Value;
            var now = DateTime.UtcNow;
            var offlineCount = 0;
            var onlineCount = 0;

            foreach (var client in clients)
            {
                // Skip clients that are already marked as offline
                if (client.Status == ClientStatus.Offline)
                {
                    offlineCount++;
                    continue;
                }

                // Check if client has timed out
                var timeSinceLastSeen = now - client.LastSeen;

                if (timeSinceLastSeen > _heartbeatTimeout)
                {
                    _logger.LogWarning("Client {ClientId} timed out (last seen {TimeSinceLastSeen:F1}s ago), marking as offline",
                        client.Id, timeSinceLastSeen.TotalSeconds);

                    var result = await _clientService.UpdateClientStatusAsync(
                        client.Id,
                        ClientStatus.Offline,
                        cancellationToken: cancellationToken);

                    if (!result.IsSuccess)
                    {
                        _logger.LogWarning("Failed to mark client {ClientId} offline: {Error}",
                            client.Id, result.Error);
                    }

                    offlineCount++;
                }
                else
                {
                    onlineCount++;
                }
            }

            if (clients.Count > 0)
            {
                _logger.LogDebug("Heartbeat check complete: {OnlineCount} online, {OfflineCount} offline, {TotalCount} total",
                    onlineCount, offlineCount, clients.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check client heartbeats");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Heartbeat Monitoring Service stopping...");
        await base.StopAsync(cancellationToken);
    }
}
