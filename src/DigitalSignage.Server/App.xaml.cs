using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using DigitalSignage.Server.ViewModels;
using DigitalSignage.Server.Services;
using DigitalSignage.Server.Configuration;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Data.Services;
using Serilog;

namespace DigitalSignage.Server;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .WriteTo.File("logs/digitalsignage-.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

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

                // Register ViewModels
                services.AddSingleton<MainViewModel>();
                services.AddTransient<DesignerViewModel>();
                services.AddTransient<DeviceManagementViewModel>();
                services.AddTransient<DataSourceViewModel>();

                // Register Services
                services.AddSingleton<ILayoutService, LayoutService>();
                services.AddSingleton<IClientService, ClientService>();
                services.AddSingleton<ISqlDataService, SqlDataService>();
                services.AddSingleton<ICommunicationService, WebSocketCommunicationService>();
                services.AddSingleton<IMediaService, MediaService>();

                // Register Background Services
                services.AddHostedService<DataRefreshService>();

                // Register Windows
                services.AddTransient<Views.MainWindow>();
            })
            .UseSerilog()
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();

        var mainWindow = _host.Services.GetRequiredService<Views.MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
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
