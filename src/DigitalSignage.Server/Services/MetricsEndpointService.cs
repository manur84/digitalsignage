using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalSignage.Server.Services;

/// <summary>
/// HTTP endpoint for exposing metrics in Prometheus format
/// Can be scraped by Prometheus, Grafana, or any monitoring tool
/// </summary>
public class MetricsEndpointService : BackgroundService
{
    private readonly ILogger<MetricsEndpointService> _logger;
    private readonly MetricsService _metricsService;
    private HttpListener? _httpListener;
    private const int MetricsPort = 8091;

    public MetricsEndpointService(
        ILogger<MetricsEndpointService> logger,
        MetricsService metricsService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://localhost:{MetricsPort}/metrics/");
            _httpListener.Prefixes.Add($"http://127.0.0.1:{MetricsPort}/metrics/");
            _httpListener.Start();

            _logger.LogInformation("Metrics endpoint started on http://localhost:{Port}/metrics/", MetricsPort);
            _logger.LogInformation("Prometheus scrape config: scrape_interval: 15s, metrics_path: /metrics/, targets: ['localhost:{Port}']", MetricsPort);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var context = await _httpListener.GetContextAsync();
                    _ = Task.Run(() => HandleMetricsRequestAsync(context), stoppingToken);
                }
                catch (HttpListenerException) when (stoppingToken.IsCancellationRequested)
                {
                    // Expected when stopping
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in metrics endpoint listener");
                    await Task.Delay(1000, stoppingToken); // Prevent tight loop on errors
                }
            }
        }
        catch (HttpListenerException ex) when (ex.ErrorCode == 5) // Access denied
        {
            _logger.LogWarning("Metrics endpoint requires admin privileges or URL reservation. " +
                "Run: netsh http add urlacl url=http://+:{Port}/metrics/ user=Everyone", MetricsPort);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start metrics endpoint");
        }
    }

    private async Task HandleMetricsRequestAsync(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            var response = context.Response;

            // Support both /metrics and /metrics/ paths
            var path = request.Url?.AbsolutePath?.TrimEnd('/') ?? string.Empty;

            // Check for format query parameter (?format=json or ?format=prometheus)
            var format = request.QueryString["format"] ?? "prometheus";

            response.Headers.Add("Access-Control-Allow-Origin", "*");

            string content;
            string contentType;

            if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                // JSON format for custom dashboards
                var metricsSnapshot = _metricsService.ExportJson();
                content = JsonSerializer.Serialize(metricsSnapshot, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                contentType = "application/json";
            }
            else
            {
                // Prometheus format (default)
                content = _metricsService.ExportPrometheusFormat();
                contentType = "text/plain; version=0.0.4"; // Prometheus exposition format version
            }

            var buffer = Encoding.UTF8.GetBytes(content);

            response.StatusCode = 200;
            response.ContentType = contentType;
            response.ContentLength64 = buffer.Length;

            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();

            _logger.LogTrace("Metrics request served: {Path}, format: {Format}, size: {Size} bytes",
                path, format, buffer.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling metrics request");

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

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping metrics endpoint...");

        if (_httpListener != null)
        {
            try
            {
                _httpListener.Stop();
                _httpListener.Close();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping metrics endpoint");
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
