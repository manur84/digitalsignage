using System.Windows;
using System.IO;
using System.Linq;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using DigitalSignage.Server.ViewModels;
using DigitalSignage.Server.Services;
using DigitalSignage.Server.Configuration;
using DigitalSignage.Server.Helpers;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Data;
using DigitalSignage.Data.Services;
using Serilog;

namespace DigitalSignage.Server;

public partial class App : Application
{
    private readonly IHost? _host;
    private bool _initializationFailed = false;

    public App()
    {
        // URL ACL check is now in Program.cs - this constructor only runs if check passed

        try
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
            Log.Information("Base Directory: {BaseDirectory}", AppDomain.CurrentDomain.BaseDirectory);
            Log.Information(".NET Version: {DotNetVersion}", Environment.Version);

            // Get server port from configuration
            var serverPort = configuration.GetValue<int>("ServerSettings:Port", 8080);
            Log.Information($"URL ACL already configured for port {serverPort} (checked in Program.cs)");

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
                services.Configure<ServerSettings>(context.Configuration.GetSection("ServerSettings"));

                // Also register as Singleton for backward compatibility
                var serverSettings = new ServerSettings();
                context.Configuration.GetSection("ServerSettings").Bind(serverSettings);
                services.AddSingleton(serverSettings);

                // Register QueryCacheSettings
                services.Configure<QueryCacheSettings>(context.Configuration.GetSection("QueryCacheSettings"));

                // Register ConnectionPoolSettings
                services.Configure<ConnectionPoolSettings>(context.Configuration.GetSection("ConnectionPoolSettings"));

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
                services.AddTransient<SchedulingViewModel>();
                services.AddTransient<MediaLibraryViewModel>();
                services.AddTransient<LogViewerViewModel>();

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
                services.AddSingleton<LogStorageService>();
                services.AddSingleton<QueryCacheService>();
                services.AddSingleton<AlertService>();

                // Register Repositories
                services.AddSingleton<DataSourceRepository>();

                // Register Background Services
                services.AddHostedService<DataRefreshService>();
                services.AddHostedService<HeartbeatMonitoringService>();
                services.AddHostedService<DiscoveryService>();
                services.AddHostedService<MdnsDiscoveryService>();
                services.AddHostedService<MessageHandlerService>();
                services.AddHostedService<AlertMonitoringService>();

                // Register Windows
                services.AddTransient<Views.MainWindow>();
            })
            .UseSerilog()
            .Build();

            Log.Information("Application host built successfully");
        }
        catch (Exception ex)
        {
            _initializationFailed = true;

            // Create emergency log if Serilog not initialized
            var emergencyLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup-error.txt");
            var errorMessage = $@"FATAL ERROR during Application initialization
Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
Base Directory: {AppDomain.CurrentDomain.BaseDirectory}
.NET Version: {Environment.Version}

Error Type: {ex.GetType().FullName}
Message: {ex.Message}

Stack Trace:
{ex.StackTrace}

Inner Exception: {ex.InnerException?.Message}
Inner Exception Stack Trace:
{ex.InnerException?.StackTrace}

Common Solutions:
1. Run diagnose-server.ps1 for detailed diagnostics
2. Check that port 8080 is not in use by another application
3. Verify appsettings.json exists and is valid JSON
4. Ensure .NET 8.0 Runtime is installed
5. Run 'dotnet restore' to restore NuGet packages
6. Run 'dotnet build' to rebuild the application
7. Check that digitalsignage.db is not locked by another process

For detailed diagnostics, run:
  PowerShell: .\diagnose-server.ps1
  Or use: .\fix-and-run.bat
";

            try
            {
                File.WriteAllText(emergencyLogPath, errorMessage);
            }
            catch
            {
                // If we can't write emergency log, we're in really bad shape
            }

            // Try to log with Serilog if available
            try
            {
                Log.Fatal(ex, "Fatal error during application initialization");
                Log.CloseAndFlush();
            }
            catch
            {
                // Serilog not available
            }

            // Show user-friendly error message
            MessageBox.Show(
                $"Failed to start Digital Signage Server\n\n" +
                $"Error: {ex.Message}\n\n" +
                $"Details saved to:\n{emergencyLogPath}\n\n" +
                $"Common Solutions:\n" +
                $"- Run diagnose-server.ps1 for diagnostics\n" +
                $"- Check port 8080 is not in use\n" +
                $"- Verify appsettings.json exists\n" +
                $"- Run 'dotnet build' to rebuild\n" +
                $"- Use fix-and-run.bat for automatic fix",
                "Startup Error - Digital Signage Server",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Environment.Exit(1);
        }
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // If initialization failed, exit immediately
        if (_initializationFailed || _host == null)
        {
            Shutdown(1);
            return;
        }

        try
        {
            Log.Information("Starting application host...");
            await _host.StartAsync();
            Log.Information("Application host started successfully");

            Log.Information("Creating main window...");
            var mainWindow = _host.Services.GetRequiredService<Views.MainWindow>();
            mainWindow.WindowState = WindowState.Maximized;
            mainWindow.Show();
            mainWindow.Activate();
            Log.Information("Main window displayed successfully");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Fatal error during startup");

            var emergencyLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup-error.txt");
            var errorMessage = $@"FATAL ERROR during OnStartup
Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

Error Type: {ex.GetType().FullName}
Message: {ex.Message}

Stack Trace:
{ex.StackTrace}

Inner Exception: {ex.InnerException?.Message}

This error occurred after initialization, likely during:
- Starting background services (DatabaseInitializationService, DataRefreshService, etc.)
- Opening WebSocket server on port 8080
- Creating main window

Common Solutions:
1. Run diagnose-server.ps1 for detailed diagnostics
2. Check that port 8080 is not in use
3. Check database connection and permissions
4. Review logs in logs/ directory for more details
";

            try
            {
                File.WriteAllText(emergencyLogPath, errorMessage);
            }
            catch { }

            MessageBox.Show(
                $"Failed to start Digital Signage Server\n\n" +
                $"Error: {ex.Message}\n\n" +
                $"Details saved to:\n{emergencyLogPath}\n\n" +
                $"Run diagnose-server.ps1 for diagnostics",
                "Startup Error - Digital Signage Server",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Log.CloseAndFlush();
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null)
        {
            try
            {
                await _host.StopAsync();
                _host.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during application shutdown");
            }
        }

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
