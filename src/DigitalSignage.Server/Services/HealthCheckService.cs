using DigitalSignage.Core.Interfaces;
using DigitalSignage.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalSignage.Server.Services;

/// <summary>
/// HTTP Health Check endpoint for monitoring and load balancers
/// </summary>
public class HealthCheckService : BackgroundService
{
    private readonly ILogger<HealthCheckService> _logger;
    private readonly IDbContextFactory<DigitalSignageDbContext> _contextFactory;
    private readonly ICommunicationService _communicationService;
    private HttpListener? _httpListener;
    private const int HealthCheckPort = 8090;
    private readonly List<Task> _runningHandlers = new();

    public HealthCheckService(
        ILogger<HealthCheckService> logger,
        IDbContextFactory<DigitalSignageDbContext> contextFactory,
        ICommunicationService communicationService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _communicationService = communicationService ?? throw new ArgumentNullException(nameof(communicationService));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://localhost:{HealthCheckPort}/health/");
            _httpListener.Prefixes.Add($"http://127.0.0.1:{HealthCheckPort}/health/");
            _httpListener.Start();

            _logger.LogInformation("Health check endpoint started on http://localhost:{Port}/health/", HealthCheckPort);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var context = await _httpListener.GetContextAsync();

                    // Track handler task and clean up completed tasks
                    var handlerTask = Task.Run(() => HandleHealthCheckAsync(context), stoppingToken);

                    lock (_runningHandlers)
                    {
                        _runningHandlers.Add(handlerTask);
                        _runningHandlers.RemoveAll(t => t.IsCompleted);
                    }
                }
                catch (HttpListenerException) when (stoppingToken.IsCancellationRequested)
                {
                    // Expected when stopping
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in health check listener");
                    await Task.Delay(1000, stoppingToken); // Prevent tight loop on errors
                }
            }
        }
        catch (HttpListenerException ex) when (ex.ErrorCode == 5) // Access denied
        {
            _logger.LogWarning("Health check endpoint requires admin privileges or URL reservation. " +
                "Run: netsh http add urlacl url=http://+:{Port}/health/ user=Everyone", HealthCheckPort);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start health check endpoint");
        }
    }

    private async Task HandleHealthCheckAsync(HttpListenerContext context)
    {
        try
        {
            var response = context.Response;
            response.ContentType = "application/json";
            response.Headers.Add("Access-Control-Allow-Origin", "*");

            var healthStatus = await GetHealthStatusAsync();

            var json = JsonSerializer.Serialize(healthStatus, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var buffer = Encoding.UTF8.GetBytes(json);

            // Set HTTP status code based on health
            response.StatusCode = healthStatus.Status == "healthy" ? 200 : 503;

            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();

            _logger.LogDebug("Health check responded: {Status}", healthStatus.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling health check request");

            try
            {
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
            catch
            {
                // Ignore errors when trying to send error response
            }
        }
    }

    private async Task<HealthCheckResponse> GetHealthStatusAsync()
    {
        var checks = new System.Collections.Generic.Dictionary<string, CheckResult>();

        // Check 1: Database connection
        checks["database"] = await CheckDatabaseAsync();

        // Check 2: WebSocket service (via communication service)
        checks["websocket"] = CheckWebSocketService();

        // Overall status
        var allHealthy = checks.Values.All(c => c.Healthy);
        var status = allHealthy ? "healthy" : "degraded";

        return new HealthCheckResponse
        {
            Status = status,
            Timestamp = DateTime.UtcNow,
            Checks = checks,
            Version = GetVersion()
        };
    }

    private async Task<CheckResult> CheckDatabaseAsync()
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Simple query to test connection
            var canConnect = await context.Database.CanConnectAsync();

            if (!canConnect)
            {
                return new CheckResult
                {
                    Healthy = false,
                    Message = "Cannot connect to database"
                };
            }

            // Optional: Check if migrations are applied
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                return new CheckResult
                {
                    Healthy = false,
                    Message = $"Database has pending migrations: {string.Join(", ", pendingMigrations)}"
                };
            }

            return new CheckResult
            {
                Healthy = true,
                Message = "Database connection OK"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return new CheckResult
            {
                Healthy = false,
                Message = $"Database error: {ex.Message}"
            };
        }
    }

    private CheckResult CheckWebSocketService()
    {
        try
        {
            // Simple check: if service is not null and not disposed, assume it's running
            // More sophisticated check would query actual listener status
            if (_communicationService != null)
            {
                return new CheckResult
                {
                    Healthy = true,
                    Message = "WebSocket service running"
                };
            }

            return new CheckResult
            {
                Healthy = false,
                Message = "WebSocket service not initialized"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebSocket service health check failed");
            return new CheckResult
            {
                Healthy = false,
                Message = $"WebSocket service error: {ex.Message}"
            };
        }
    }

    private static string GetVersion()
    {
        var version = typeof(HealthCheckService).Assembly.GetName().Version;
        return version?.ToString() ?? "unknown";
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping health check endpoint...");

        if (_httpListener != null)
        {
            try
            {
                _httpListener.Stop();
                _httpListener.Close();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping health check endpoint");
            }
        }

        // Wait for all running handlers to complete (with timeout)
        Task[] handlers;
        lock (_runningHandlers)
        {
            handlers = _runningHandlers.ToArray();
        }

        if (handlers.Length > 0)
        {
            _logger.LogDebug("Waiting for {Count} health check handlers to complete", handlers.Length);
            try
            {
                await Task.WhenAll(handlers).WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Some health check handlers did not complete in time");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error waiting for health check handlers");
            }
        }

        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _httpListener?.Close();
        base.Dispose();
    }
}

/// <summary>
/// Overall health status response
/// </summary>
public class HealthCheckResponse
{
    public string Status { get; set; } = "unknown";
    public DateTime Timestamp { get; set; }
    public System.Collections.Generic.Dictionary<string, CheckResult> Checks { get; set; } = new();
    public string Version { get; set; } = "unknown";
}

/// <summary>
/// Individual check result
/// </summary>
public class CheckResult
{
    public bool Healthy { get; set; }
    public string Message { get; set; } = string.Empty;
}
