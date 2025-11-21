using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Windows Service implementation for Digital Signage Server
/// Hosts all background services when running as a Windows Service
/// </summary>
public class DigitalSignageWindowsService : IHostedService, IDisposable
{
    private readonly ILogger<DigitalSignageWindowsService> _logger;
    private readonly IHostApplicationLifetime _appLifetime;

    public DigitalSignageWindowsService(
        ILogger<DigitalSignageWindowsService> logger,
        IHostApplicationLifetime appLifetime)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _appLifetime = appLifetime ?? throw new ArgumentNullException(nameof(appLifetime));
    }

    /// <summary>
    /// Called when the service starts
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Digital Signage Windows Service starting...");

        _appLifetime.ApplicationStarted.Register(() =>
        {
            _logger.LogInformation("Digital Signage Windows Service started successfully");
            _logger.LogInformation("WebSocket server is now accepting client connections");
        });

        _appLifetime.ApplicationStopping.Register(() =>
        {
            _logger.LogInformation("Digital Signage Windows Service stopping...");
        });

        _appLifetime.ApplicationStopped.Register(() =>
        {
            _logger.LogInformation("Digital Signage Windows Service stopped");
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the service stops
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Digital Signage Windows Service stop requested");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        // No resources to dispose
        GC.SuppressFinalize(this);
    }
}
