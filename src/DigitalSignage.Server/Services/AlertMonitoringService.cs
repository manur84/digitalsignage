using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Background service that periodically evaluates alert rules
/// </summary>
public class AlertMonitoringService : BackgroundService
{
    private readonly AlertService _alertService;
    private readonly ILogger<AlertMonitoringService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1); // Check every minute

    public AlertMonitoringService(
        AlertService alertService,
        ILogger<AlertMonitoringService> logger)
    {
        _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Alert Monitoring Service started");

        // Wait for database initialization to complete
        _logger.LogInformation("Waiting 15 seconds for database initialization...");
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        _logger.LogInformation("Starting alert monitoring");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _alertService.EvaluateAllRulesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in alert monitoring loop");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Alert Monitoring Service stopped");
    }
}
