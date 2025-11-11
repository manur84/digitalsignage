using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using DigitalSignage.Server.ViewModels;
using DigitalSignage.Server.Services;
using DigitalSignage.Server.Configuration;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Data;
using DigitalSignage.Data.Services;
using Serilog;

namespace DigitalSignage.Server;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        // Create configuration first to load Serilog settings
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .Build();

        // Configure Serilog from configuration
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.WithProperty("Application", "DigitalSignage.Server")
            .CreateLogger();

        Log.Information("Digital Signage Server starting...");

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                // Load configuration from appsettings.json
                config.SetBasePath(AppDomain.CurrentDomain.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                // Bind and register ServerSettings from configuration
                var serverSettings = new ServerSettings();
                context.Configuration.GetSection("ServerSettings").Bind(serverSettings);
                services.AddSingleton(serverSettings);

                // Register Database Context
                var connectionString = context.Configuration.GetConnectionString("DefaultConnection")
                    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

                // Register DbContext for services
                services.AddDbContext<DigitalSignageDbContext>(options =>
                {
                    // Use SQLite for cross-platform compatibility
                    options.UseSqlite(connectionString);

                    // Enable sensitive data logging in development
                    if (context.HostingEnvironment.IsDevelopment())
                    {
                        options.EnableSensitiveDataLogging();
                        options.EnableDetailedErrors();
                    }
                });

                // Register DbContextFactory for ViewModels (allows transient/singleton usage)
                services.AddDbContextFactory<DigitalSignageDbContext>(options =>
                {
                    options.UseSqlite(connectionString);

                    if (context.HostingEnvironment.IsDevelopment())
                    {
                        options.EnableSensitiveDataLogging();
                        options.EnableDetailedErrors();
                    }
                });

                // Register Database Initialization Service
                services.AddHostedService<DatabaseInitializationService>();

                // Register ViewModels
                services.AddSingleton<MainViewModel>();
                services.AddTransient<DesignerViewModel>();
                services.AddTransient<DeviceManagementViewModel>();
                services.AddTransient<DataSourceViewModel>();
                services.AddTransient<PreviewViewModel>();

                // Register Services
                services.AddSingleton<ILayoutService, LayoutService>();
                services.AddSingleton<IClientService, ClientService>();

                // Register SqlDataService for both IDataService and ISqlDataService
                services.AddSingleton<SqlDataService>();
                services.AddSingleton<IDataService>(sp => sp.GetRequiredService<SqlDataService>());
                services.AddSingleton<ISqlDataService>(sp => sp.GetRequiredService<SqlDataService>());

                services.AddSingleton<ITemplateService, TemplateService>();
                services.AddSingleton<ICommunicationService, WebSocketCommunicationService>();
                services.AddSingleton<IMediaService, EnhancedMediaService>();
                services.AddScoped<IAuthenticationService, AuthenticationService>();

                // Register Repositories
                services.AddSingleton<DataSourceRepository>();

                // Register Background Services
                services.AddHostedService<DataRefreshService>();
                services.AddHostedService<HeartbeatMonitoringService>();

                // Register Windows
                services.AddTransient<Views.MainWindow>();
            })
            .UseSerilog()
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        await _host.StartAsync();

        var mainWindow = _host.Services.GetRequiredService<Views.MainWindow>();
        mainWindow.WindowState = WindowState.Maximized;
        mainWindow.Show();
        mainWindow.Activate();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    public static T GetService<T>() where T : class
    {
        if (Current is App app)
        {
            return app._host.Services.GetRequiredService<T>();
        }
        throw new InvalidOperationException("Application is not initialized");
    }
}
