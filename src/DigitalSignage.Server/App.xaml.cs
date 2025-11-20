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
using DigitalSignage.Server.Views;
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
                // Configure settings from appsettings.json
                services.AddDigitalSignageSettings(context.Configuration);

                // Register and initialize database (SYNCHRONOUS - blocks startup)
                services.AddDigitalSignageDatabase(context.Configuration, context.HostingEnvironment.IsDevelopment());
                DatabaseInitializer.InitializeDatabase(services);

                // Register ViewModels
                services.AddViewModels(_liveLogMessages);

                // Register Business Services
                services.AddBusinessServices();

                // Register Background Services
                services.AddBackgroundServices();

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

        var originalShutdownMode = ShutdownMode;
        SplashScreenWindow? splash = null;

        try
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown; // Prevent the splash from becoming MainWindow
            splash = SplashScreenWindow.ShowSplash("Digital Signage Server wird gestartet...");

            // Create progress manager for detailed startup tracking
            var progressManager = new StartupProgressManager(splash);

            // Define startup steps with weights
            progressManager.DefineSteps(
                new StartupStep("Starte Hosting-Dienste...", weight: 2.0, "Initialisiere IHost Container"),
                new StartupStep("Lade Datenquellen...", weight: 1.0, "Initialisiere Data Sources"),
                new StartupStep("Starte WebSocket Server...", weight: 1.5, "Port 8080-8083"),
                new StartupStep("Initialisiere Background Services...", weight: 2.0, "DataRefresh, Heartbeat, Discovery..."),
                new StartupStep("Lade Media-Cache...", weight: 1.0, "Thumbnails und Previews"),
                new StartupStep("Erstelle Hauptfenster...", weight: 1.5, "UI Initialisierung"),
                new StartupStep("Registriere Event Handler...", weight: 0.5, "Screenshot Events"),
                new StartupStep("Finalisiere Startup...", weight: 0.5, "Letzte Prüfungen")
            );

            // Step 1: Start application host
            await progressManager.ExecuteStepAsync(async () =>
            {
                Log.Information("Starting application host...");
                await _host.StartAsync();
                Log.Information("Application host started successfully");
            });

            // Step 2: Load data sources (simulated cache warming)
            await progressManager.ExecuteStepAsync(async () =>
            {
                // Give background services time to initialize
                await Task.Delay(100);
            });

            // Step 3: WebSocket server startup (already started by host)
            await progressManager.ExecuteStepAsync(async () =>
            {
                await Task.Delay(100);
            });

            // Step 4: Background services (already started by host)
            await progressManager.ExecuteStepAsync(async () =>
            {
                await Task.Delay(150);
            });

            // Step 5: Media and data cache warming
            await progressManager.ExecuteStepAsync(async () =>
            {
                try
                {
                    var cacheService = _host.Services.GetService<StartupCacheService>();
                    if (cacheService != null)
                    {
                        await cacheService.WarmupCachesAsync();
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Cache warming skipped");
                }
            });

            // Step 6: Create main window
            Views.MainWindow? mainWindow = null;
            await progressManager.ExecuteStepAsync(() =>
            {
                Log.Information("Creating main window...");
                mainWindow = _host.Services.GetRequiredService<Views.MainWindow>();
                MainWindow = mainWindow;
                ShutdownMode = originalShutdownMode;
            });

            // Step 7: Register event handlers
            await progressManager.ExecuteStepAsync(() =>
            {
                MessageHandlerService.ScreenshotReceived += OnScreenshotReceived;
                Log.Information("Screenshot event handler registered");
            });

            // Step 8: Finalize
            await progressManager.ExecuteStepAsync(async () =>
            {
                await Task.Delay(100);
            });

            // Complete startup
            await progressManager.CompleteAsync();

            // Close splash and show main window
            splash?.CloseSafely();

            if (mainWindow != null)
            {
                mainWindow.WindowState = WindowState.Maximized;
                mainWindow.Show();
                mainWindow.Activate();
                Log.Information("Main window displayed successfully");
            }
        }
        catch (Exception ex)
        {
            splash?.CloseSafely();
            ShutdownMode = originalShutdownMode;

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
