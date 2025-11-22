using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Server.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.Server.Api;

/// <summary>
/// Hosted service that runs the REST API server alongside the WPF application
/// </summary>
public class ApiHost : IHostedService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ApiHost> _logger;
    private IWebHost? _webHost;
    private int _currentPort = 5001;
    private readonly int[] _portFallbacks = { 5001, 5002, 5003, 5004, 5005, 5006 };

    public ApiHost(IServiceProvider serviceProvider, ILogger<ApiHost> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Gets the URL where the API is accessible (HTTP)
    /// </summary>
    public string? ApiUrl { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting REST API server...");

        // Try to start the web host on available ports
        foreach (var port in _portFallbacks)
        {
            try
            {
                _currentPort = port;
                _webHost = BuildWebHost(port);
                await _webHost.StartAsync(cancellationToken);

                ApiUrl = $"http://localhost:{port}";
                _logger.LogInformation("REST API server started successfully on HTTP port {Port}", port);
                _logger.LogInformation("Swagger UI available at: {SwaggerUrl}", $"{ApiUrl}/swagger");
                _logger.LogInformation("For production SSL: Use nginx reverse proxy (see README for config)");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to start API server on port {Port}, trying next port...", port);

                // Dispose failed web host
                if (_webHost != null)
                {
                    await _webHost.StopAsync(CancellationToken.None);
                    _webHost.Dispose();
                    _webHost = null;
                }
            }
        }

        _logger.LogError("Failed to start REST API server on any available port");
        throw new InvalidOperationException("Failed to start REST API server - no available ports");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping REST API server...");

        if (_webHost != null)
        {
            try
            {
                await _webHost.StopAsync(cancellationToken);
                _logger.LogInformation("REST API server stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping REST API server");
            }
        }
    }

    private IWebHost BuildWebHost(int port)
    {
        return new WebHostBuilder()
            .UseKestrel(options =>
            {
                // Listen on all network interfaces for HTTP
                // SSL/TLS termination should be handled by reverse proxy (nginx/caddy/IIS)
                options.ListenAnyIP(port, listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                    // HTTP only - no SSL configuration
                    // For production: Use nginx reverse proxy for HTTPS
                });

                // Increase request limits for screenshot uploads
                options.Limits.MaxRequestBodySize = 52428800; // 50 MB
            })
            .UseStartup<ApiStartup>()
            .ConfigureServices(services =>
            {
                // Share services from the main WPF application
                // This allows controllers to access existing services like ClientService, LayoutService, etc.

                using var scope = _serviceProvider.CreateScope();

                // Register all interface services from the main application
                var serviceDescriptors = _serviceProvider.GetType()
                    .GetProperty("Root", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.GetValue(_serviceProvider) as IServiceProvider;

                // Manually register critical services that controllers need
                RegisterSharedService<IMobileAppService>(services);
                RegisterSharedService<IClientService>(services);
                RegisterSharedService<ILayoutService>(services);
                RegisterSharedService<ICommunicationService>(services);

                // Add logging
                services.AddLogging();
            })
            .ConfigureLogging(logging =>
            {
                // Use the same logging configuration as the main application
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .UseEnvironment(Environments.Development) // Can be configured based on build configuration
            .Build();
    }

    private void RegisterSharedService<T>(IServiceCollection services) where T : class
    {
        var service = _serviceProvider.GetService<T>();
        if (service != null)
        {
            services.AddSingleton(service);
        }
        else
        {
            _logger.LogWarning("Service {ServiceType} not found in main service provider", typeof(T).Name);
        }
    }

    public void Dispose()
    {
        _webHost?.Dispose();
    }
}
