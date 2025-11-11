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
        // Handle command line arguments for auto-configuration
        var args = Environment.GetCommandLineArgs();

        // Check if we're in setup mode (restarted as admin)
        if (args.Contains("--setup-urlacl"))
        {
            HandleUrlAclSetup(args);
            Environment.Exit(0);
            return;
        }

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

            // Check URL ACL configuration
            if (!UrlAclManager.IsUrlAclConfigured(serverPort))
            {
                Log.Warning($"URL ACL not configured for port {serverPort}");

                if (!UrlAclManager.IsRunningAsAdministrator())
                {
                    // Ask user if they want to configure it now
                    var result = MessageBox.Show(
                        $"Erste Ausführung: URL ACL Konfiguration erforderlich\n\n" +
                        $"Die Digital Signage Server App benötigt eine einmalige\n" +
                        $"Windows-Konfiguration für Port {serverPort}.\n\n" +
                        $"Die App wird sich kurz mit Administrator-Rechten neu starten,\n" +
                        $"die Konfiguration vornehmen, und dann normal weiterlaufen.\n\n" +
                        $"Möchten Sie die automatische Konfiguration jetzt durchführen?",
                        "Einmalige Konfiguration erforderlich",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        Log.Information("User accepted automatic URL ACL configuration");

                        // Restart as admin with setup flag
                        if (UrlAclManager.RestartAsAdministrator($"--setup-urlacl {serverPort}"))
                        {
                            Log.Information("Application restarted with elevation for URL ACL setup");
                            // This instance will exit, elevated instance will run setup
                            Environment.Exit(0);
                            return;
                        }
                        else
                        {
                            Log.Error("Failed to restart as administrator");
                            MessageBox.Show(
                                "Fehler beim Neustart mit Administrator-Rechten.\n\n" +
                                "Bitte führen Sie setup-urlacl.bat manuell als Administrator aus.",
                                "Fehler",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                            Environment.Exit(1);
                            return;
                        }
                    }
                    else
                    {
                        Log.Warning("User declined automatic URL ACL configuration");
                        MessageBox.Show(
                            $"URL ACL nicht konfiguriert.\n\n" +
                            $"Die App läuft jetzt im localhost-Modus (nur lokal erreichbar).\n\n" +
                            $"Für externe Clients führen Sie bitte setup-urlacl.bat\n" +
                            $"als Administrator aus.",
                            "Hinweis",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        // Continue with localhost-only mode
                    }
                }
                else
                {
                    // We're already running as admin, configure now
                    Log.Information("Running as administrator, configuring URL ACL now");
                    if (UrlAclManager.ConfigureUrlAcl(serverPort))
                    {
                        Log.Information("URL ACL configured successfully");
                        MessageBox.Show(
                            "URL ACL erfolgreich konfiguriert!\n\n" +
                            "Die App startet jetzt normal.",
                            "Konfiguration abgeschlossen",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        Log.Error("Failed to configure URL ACL even with admin rights");
                    }
                }
            }
            else
            {
                Log.Information($"URL ACL already configured for port {serverPort}");
            }

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

    /// <summary>
    /// Handles URL ACL setup when running in elevated mode
    /// </summary>
    private void HandleUrlAclSetup(string[] args)
    {
        try
        {
            // Initialize Serilog for setup logging
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "setup-.txt"),
                    rollingInterval: RollingInterval.Day)
                .CreateLogger();

            // Get port from command line args
            var portArg = args.FirstOrDefault(a => int.TryParse(a, out _));
            var port = portArg != null ? int.Parse(portArg) : 8080;

            Log.Information($"Running URL ACL setup for port {port}");

            if (UrlAclManager.ConfigureUrlAcl(port))
            {
                Log.Information("URL ACL setup completed successfully");

                // Show success message
                MessageBox.Show(
                    "URL ACL Konfiguration abgeschlossen!\n\n" +
                    "Die App startet jetzt automatisch normal neu.",
                    "Setup erfolgreich",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Restart the app normally (without admin)
                var exePath = Environment.ProcessPath ??
                             Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true
                    });
                }
            }
            else
            {
                Log.Error("URL ACL setup failed");
                MessageBox.Show(
                    "URL ACL Konfiguration fehlgeschlagen.\n\n" +
                    "Bitte führen Sie setup-urlacl.bat manuell aus.",
                    "Setup fehlgeschlagen",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during URL ACL setup");
            MessageBox.Show(
                $"Fehler während der URL ACL Konfiguration:\n\n{ex.Message}",
                "Fehler",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
