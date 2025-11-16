using System.Windows;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using DigitalSignage.Server.ViewModels;
using DigitalSignage.Server.Services;
using DigitalSignage.Server.Configuration;
using DigitalSignage.Server.Helpers;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Data;
using DigitalSignage.Data.Services;
using Serilog;
using static DigitalSignage.Server.Services.MessageHandlerService;

namespace DigitalSignage.Server;

public partial class App : Application
{
    public readonly IHost? _host;
    private bool _initializationFailed = false;

    // Shared log collection for UISink
    private static readonly ObservableCollection<string> _liveLogMessages = new();

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

            // Configure Serilog from configuration AND add UISink for live logs in UI
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .Enrich.WithProperty("Application", "DigitalSignage.Server")
                .WriteTo.UISink(_liveLogMessages, maxMessages: 2000)  // Add UI sink for live logging
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
                    options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));

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
                    options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));

                    if (context.HostingEnvironment.IsDevelopment())
                    {
                        options.EnableSensitiveDataLogging();
                        options.EnableDetailedErrors();
                    }
                });

                // CRITICAL: Database initialization MUST complete BEFORE other services start
                // Run database initialization synchronously during app startup
                Log.Information("==========================================================");
                Log.Information("INITIALIZING DATABASE (SYNCHRONOUS - BLOCKING STARTUP)");
                Log.Information("==========================================================");

                try
                {
                    using (var scope = services.BuildServiceProvider().CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<DigitalSignageDbContext>();

                        Log.Information("Working directory: {WorkingDirectory}", Directory.GetCurrentDirectory());
                        var dbConnectionString = dbContext.Database.GetConnectionString();
                        Log.Information("Database connection string: {ConnectionString}", dbConnectionString);

                        // Extract database file path for logging
                        string? dbPath = null;
                        if (dbConnectionString?.Contains("Data Source=") == true)
                        {
                            try
                            {
                                var startIndex = dbConnectionString.IndexOf("Data Source=") + "Data Source=".Length;
                                var remainingString = dbConnectionString.Substring(startIndex);
                                var dbFile = remainingString.Split(';')[0].Trim();
                                dbPath = Path.IsPathRooted(dbFile) ? dbFile : Path.Combine(Directory.GetCurrentDirectory(), dbFile);
                                Log.Information("Database file path: {DatabasePath}", dbPath);
                                Log.Information("Database file exists before migration: {Exists}", File.Exists(dbPath));
                            }
                            catch (Exception ex)
                            {
                                Log.Warning(ex, "Could not parse database path from connection string");
                            }
                        }

                        // Check for pending migrations
                        Log.Information("Checking for pending database migrations...");
                        var allMigrations = dbContext.Database.GetMigrations();
                        var appliedMigrations = dbContext.Database.GetAppliedMigrations();
                        var pendingMigrations = dbContext.Database.GetPendingMigrations();

                        Log.Information("Total migrations: {Total}", allMigrations.Count());
                        Log.Information("Applied migrations: {Applied}", appliedMigrations.Count());
                        Log.Information("Pending migrations: {Pending}", pendingMigrations.Count());

                        if (!appliedMigrations.Any())
                        {
                            Log.Information("No migrations applied yet - database will be created from scratch");
                        }

                        if (pendingMigrations.Any())
                        {
                            Log.Information("Applying {Count} pending migrations:", pendingMigrations.Count());
                            foreach (var migration in pendingMigrations)
                            {
                                Log.Information("  - {Migration}", migration);
                            }

                            // SYNCHRONOUS migration - blocks until complete
                            dbContext.Database.Migrate();
                            Log.Information("Database migrations applied successfully");
                        }
                        else
                        {
                            Log.Information("Database is up to date - no pending migrations");
                        }

                        // Verify database connection
                        var canConnect = dbContext.Database.CanConnect();
                        Log.Information("Database connection verification: {Status}", canConnect ? "SUCCESS" : "FAILED");

                        if (!canConnect)
                        {
                            var errorMsg = "CRITICAL: Database exists but cannot connect! Check connection string and permissions.";
                            Log.Error(errorMsg);
                            throw new InvalidOperationException(errorMsg);
                        }

                        if (dbPath != null && File.Exists(dbPath))
                        {
                            Log.Information("Database file exists after migration: {Exists}", File.Exists(dbPath));
                            Log.Information("Database size: {Size} bytes", new FileInfo(dbPath).Length);
                        }

                        Log.Information("==========================================================");
                        Log.Information("DATABASE INITIALIZATION COMPLETED SUCCESSFULLY");
                        Log.Information("==========================================================");
                    }
                }
                catch (Exception ex)
                {
                    Log.Fatal(ex, "CRITICAL: Database initialization FAILED during app startup!");
                    Log.Fatal("Error type: {ErrorType}", ex.GetType().FullName);
                    Log.Fatal("Error message: {Message}", ex.Message);

                    if (ex.InnerException != null)
                    {
                        Log.Fatal("Inner exception: {InnerMessage}", ex.InnerException.Message);
                    }

                    Log.Fatal("==========================================================");
                    Log.Fatal("DATABASE INITIALIZATION FAILED - APPLICATION CANNOT START");
                    Log.Fatal("Common solutions:");
                    Log.Fatal("1. Check database file permissions");
                    Log.Fatal("2. Ensure no other process has the database file locked");
                    Log.Fatal("3. Verify the connection string in appsettings.json");
                    Log.Fatal("4. Run: dotnet ef database update");
                    Log.Fatal("==========================================================");

                    throw; // Re-throw to prevent app startup
                }

                // Keep the hosted service for seed data (runs asynchronously after startup)
                services.AddHostedService<DatabaseInitializationService>();

                // Register ViewModels
                services.AddSingleton<MainViewModel>();

                // Register new Sub-ViewModels (Refactored from MainViewModel)
                services.AddSingleton<ServerManagementViewModel>();
                services.AddSingleton<DiagnosticsViewModel>();

                // Existing ViewModels
                services.AddTransient<DeviceManagementViewModel>();
                services.AddTransient<DiscoveredDevicesViewModel>();
                services.AddTransient<PreviewViewModel>();
                services.AddTransient<SchedulingViewModel>();
                services.AddTransient<MediaLibraryViewModel>();
                services.AddTransient<LogViewerViewModel>();
                services.AddTransient<ScreenshotViewModel>();
                services.AddTransient<DeviceDetailViewModel>();
                services.AddTransient<MediaBrowserViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddSingleton<AlertsViewModel>();
                services.AddTransient<AlertRuleEditorViewModel>();
                services.AddTransient<TokenManagementViewModel>();
                services.AddTransient<SystemDiagnosticsViewModel>();
                services.AddTransient<DataMappingViewModel>();
                services.AddTransient<SqlDataSourcesViewModel>();

                // Register LiveLogsViewModel as singleton with shared log collection
                services.AddSingleton<LiveLogsViewModel>(sp =>
                {
                    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LiveLogsViewModel>>();
                    var dialogService = sp.GetRequiredService<IDialogService>();
                    return new LiveLogsViewModel(logger, dialogService, _liveLogMessages);
                });

                // Register Services
                services.AddSingleton<ILayoutService, LayoutService>();
                services.AddSingleton<IClientService, ClientService>();

                // Register SqlDataService for both IDataService and ISqlDataService
                services.AddSingleton<SqlDataService>();
                services.AddSingleton<IDataService>(sp => sp.GetRequiredService<SqlDataService>());
                services.AddSingleton<ISqlDataService>(sp => sp.GetRequiredService<SqlDataService>());

                services.AddSingleton<IScribanService, ScribanService>();
                services.AddSingleton<ICommunicationService, WebSocketCommunicationService>();
                services.AddSingleton<IDialogService, DialogService>();

                // Register ThumbnailService and EnhancedMediaService for both interface and concrete type
                services.AddSingleton<ThumbnailService>();
                services.AddSingleton<EnhancedMediaService>();
                services.AddSingleton<IMediaService>(sp => sp.GetRequiredService<EnhancedMediaService>());

                services.AddScoped<IAuthenticationService, AuthenticationService>();
                services.AddSingleton<LogStorageService>();
                services.AddSingleton<QueryCacheService>();
                services.AddSingleton<AlertService>();
                services.AddSingleton<BackupService>();
                services.AddSingleton<NetworkScannerService>();
                services.AddSingleton<SystemDiagnosticsService>();
                services.AddSingleton<ThemeService>();
                services.AddSingleton<RateLimitingService>();

                // SQL Data Source Services
                services.AddSingleton<ISqlDataSourceService, SqlDataSourceService>();
                services.AddSingleton<DataSourceManager>();
                services.AddScoped<DataSourceRepository>();

                // Register Background Services
                services.AddHostedService<DataRefreshService>();
                services.AddHostedService<HeartbeatMonitoringService>();
                services.AddHostedService<DiscoveryService>();
                services.AddHostedService<MdnsDiscoveryService>();
                services.AddHostedService<MessageHandlerService>();
                services.AddHostedService<AlertMonitoringService>();
                services.AddHostedService<ClientDataUpdateService>();

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

            // Set as application main window
            MainWindow = mainWindow;

            mainWindow.WindowState = WindowState.Maximized;
            mainWindow.Show();
            mainWindow.Activate();
            Log.Information("Main window displayed successfully");

            // Subscribe to screenshot events
            MessageHandlerService.ScreenshotReceived += OnScreenshotReceived;
            Log.Information("Screenshot event handler registered");
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
            catch (Exception logEx)
            {
                // Unable to write emergency log - best effort only, don't crash on logging failure
                System.Diagnostics.Debug.WriteLine($"Failed to write emergency log: {logEx.Message}");
            }

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
        // Unsubscribe from screenshot events
        MessageHandlerService.ScreenshotReceived -= OnScreenshotReceived;

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

    /// <summary>
    /// Handle screenshot received event and show screenshot window
    /// </summary>
    private void OnScreenshotReceived(object? sender, ScreenshotReceivedEventArgs e)
    {
        try
        {
            Log.Information("Screenshot received from client {ClientName}, showing window...", e.ClientName);

            // Get a new instance of ScreenshotViewModel from DI
            var viewModel = _host?.Services.GetRequiredService<ScreenshotViewModel>();

            if (viewModel != null)
            {
                // Show screenshot window on UI thread
                Views.ScreenshotWindow.ShowScreenshot(e.ClientName, e.ImageData, viewModel);
                Log.Information("Screenshot window opened for client {ClientName}", e.ClientName);
            }
            else
            {
                Log.Error("Failed to get ScreenshotViewModel from DI container");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error showing screenshot window for client {ClientName}", e.ClientName);
            MessageBox.Show(
                $"Failed to show screenshot from {e.ClientName}:\n\n{ex.Message}",
                "Screenshot Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public static T GetService<T>() where T : class
    {
        if (Current is App app)
        {
            return app._host!.Services.GetRequiredService<T>();
        }
        throw new InvalidOperationException("Application is not initialized");
    }

    public static IServiceProvider GetServiceProvider()
    {
        if (Current is App app)
        {
            return app._host!.Services;
        }
        throw new InvalidOperationException("Application is not initialized");
    }
}
