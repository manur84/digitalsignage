using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using DigitalSignage.Core.Interfaces;
using Serilog;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Service for warming up caches during application startup to improve initial performance
/// </summary>
public class StartupCacheService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMemoryCache _cache;
    private readonly ILogger _logger;

    public StartupCacheService(IServiceProvider serviceProvider, IMemoryCache cache)
    {
        _serviceProvider = serviceProvider;
        _cache = cache;
        _logger = Log.ForContext<StartupCacheService>();
    }

    /// <summary>
    /// Warm up all caches during startup
    /// </summary>
    public async Task WarmupCachesAsync(CancellationToken cancellationToken = default)
    {
        _logger.Information("Starting cache warmup...");
        var startTime = DateTime.UtcNow;

        try
        {
            // Run warmup tasks in parallel for faster startup
            await Task.WhenAll(
                WarmupLayoutCacheAsync(cancellationToken),
                WarmupClientCacheAsync(cancellationToken)
            );

            var duration = DateTime.UtcNow - startTime;
            _logger.Information("Cache warmup completed in {Duration}ms", duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Cache warmup failed, but application will continue");
        }
    }

    /// <summary>
    /// Warm up layout cache by loading recent layouts
    /// </summary>
    private async Task WarmupLayoutCacheAsync(CancellationToken cancellationToken)
    {
        try
        {
            var layoutService = _serviceProvider.GetService<ILayoutService>();
            if (layoutService != null)
            {
                _logger.Debug("Warming up layout cache...");
                var result = await layoutService.GetAllLayoutsAsync();

                if (result.IsSuccess && result.Value != null)
                {
                    // Cache layout count
                    _cache.Set("layout_count", result.Value.Count, TimeSpan.FromMinutes(5));
                    _logger.Debug("Layout cache warmed up: {Count} layouts", result.Value.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to warm up layout cache");
        }
    }

    /// <summary>
    /// Warm up client cache by loading registered clients
    /// </summary>
    private async Task WarmupClientCacheAsync(CancellationToken cancellationToken)
    {
        try
        {
            var clientService = _serviceProvider.GetService<IClientService>();
            if (clientService != null)
            {
                _logger.Debug("Warming up client cache...");
                var result = await clientService.GetAllClientsAsync();

                if (result.IsSuccess && result.Value != null)
                {
                    // Cache client count and online count
                    var onlineCount = result.Value.Count(c => c.Status == Core.Models.ClientStatus.Online);
                    _cache.Set("client_count", result.Value.Count, TimeSpan.FromMinutes(1));
                    _cache.Set("client_online_count", onlineCount, TimeSpan.FromMinutes(1));

                    _logger.Debug("Client cache warmed up: {Total} clients ({Online} online)",
                        result.Value.Count, onlineCount);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to warm up client cache");
        }
    }

    // Media cache warmup removed - MediaService no longer exists

    /// <summary>
    /// Clear all startup caches
    /// </summary>
    public void ClearCaches()
    {
        _logger.Information("Clearing startup caches...");
        _cache.Remove("layout_count");
        _cache.Remove("client_count");
        _cache.Remove("client_online_count");
    }
}
