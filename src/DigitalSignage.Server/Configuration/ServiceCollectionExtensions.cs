using System.Collections.ObjectModel;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Data;
using DigitalSignage.Data.Services;
using DigitalSignage.Server.Helpers;
using DigitalSignage.Server.Services;
using DigitalSignage.Server.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.Server.Configuration;

/// <summary>
/// Extension methods for configuring dependency injection
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers database context and factory
    /// </summary>
    public static IServiceCollection AddDigitalSignageDatabase(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        // Register DbContext for services
        services.AddDbContext<DigitalSignageDbContext>(options =>
        {
            options.UseSqlite(connectionString);
            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));

            if (isDevelopment)
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

            if (isDevelopment)
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });

        // Database initialization runs synchronously during app startup
        services.AddHostedService<DatabaseInitializationService>();

        return services;
    }

    /// <summary>
    /// Registers all ViewModels
    /// </summary>
    public static IServiceCollection AddViewModels(
        this IServiceCollection services,
        ObservableCollection<string> liveLogMessages)
    {
        // Main ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<ServerManagementViewModel>();
        services.AddSingleton<DiagnosticsViewModel>();

        // Device Management
        services.AddSingleton<DeviceManagementViewModel>();
        services.AddSingleton<DiscoveredDevicesViewModel>();

        // Mobile App Management
        services.AddSingleton<MobileAppManagementViewModel>();

        // Layout & Content
        services.AddSingleton<LayoutManagerViewModel>();
        services.AddTransient<PreviewViewModel>();
        services.AddTransient<DataMappingViewModel>();

        // Client Management
        services.AddTransient<ClientInstallerViewModel>();
        services.AddTransient<DeviceDetailViewModel>();

        // Scheduling & Monitoring
        services.AddTransient<SchedulingViewModel>();
        services.AddSingleton<AlertsViewModel>();
        services.AddTransient<AlertRuleEditorViewModel>();

        // System & Diagnostics
        services.AddTransient<LogViewerViewModel>();
        services.AddTransient<ScreenshotViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<TokenManagementViewModel>();
        services.AddTransient<SystemDiagnosticsViewModel>();

        // Register LiveLogsViewModel as singleton with shared log collection
        services.AddSingleton<LiveLogsViewModel>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<LiveLogsViewModel>>();
            var dialogService = sp.GetRequiredService<IDialogService>();
            return new LiveLogsViewModel(logger, dialogService, liveLogMessages);
        });

        return services;
    }

    /// <summary>
    /// Registers all business services
    /// </summary>
    public static IServiceCollection AddBusinessServices(this IServiceCollection services)
    {
        // Core Services
        services.AddSingleton<ILayoutService, LayoutService>();
        services.AddSingleton<IClientService, ClientService>();
        services.AddSingleton<IScribanService, ScribanService>();
        services.AddSingleton<ICommunicationService, WebSocketCommunicationService>();
        services.AddSingleton<IDialogService, DialogService>();

        // Register SqlDataService for both IDataService and ISqlDataService
        services.AddSingleton<SqlDataService>();
        services.AddSingleton<IDataService>(sp => sp.GetRequiredService<SqlDataService>());
        services.AddSingleton<ISqlDataService>(sp => sp.GetRequiredService<SqlDataService>());

        // UI Synchronization
        services.AddSingleton<ISynchronizationContext, WpfSynchronizationContextService>();

        // Media Services
        services.AddSingleton<ThumbnailService>();
        services.AddSingleton<EnhancedMediaService>();
        services.AddSingleton<IMediaService>(sp => sp.GetRequiredService<EnhancedMediaService>());

        // Infrastructure Services
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<IMobileAppService, MobileAppService>();
        services.AddSingleton<LogStorageService>();
        services.AddSingleton<QueryCacheService>();
        services.AddSingleton<AlertService>();
        services.AddSingleton<BackupService>();
        services.AddSingleton<RemoteClientInstallerService>();
        services.AddSingleton<NetworkScannerService>();
        services.AddSingleton<NetworkInterfaceService>();
        services.AddSingleton<SystemDiagnosticsService>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<RateLimitingService>();
        services.AddScoped<DataSourceRepository>();

        // Windows Service Management
        services.AddSingleton<WindowsServiceInstaller>();

        // Caching
        services.AddMemoryCache();
        services.AddSingleton<StartupCacheService>();

        return services;
    }

    /// <summary>
    /// Registers all background hosted services
    /// </summary>
    public static IServiceCollection AddBackgroundServices(this IServiceCollection services)
    {
        services.AddHostedService<DataRefreshService>();
        services.AddHostedService<HeartbeatMonitoringService>();
        services.AddHostedService<DiscoveryService>();
        services.AddHostedService<MdnsDiscoveryService>();
        services.AddHostedService<MessageHandlerService>();
        services.AddHostedService<AlertMonitoringService>();

        // Register NetworkDiscoveryService as both interface and hosted service
        services.AddSingleton<INetworkDiscoveryService, NetworkDiscoveryService>();
        services.AddHostedService(sp => (NetworkDiscoveryService)sp.GetRequiredService<INetworkDiscoveryService>());

        // Register REST API Host service
        services.AddHostedService<Api.ApiHost>();

        return services;
    }

    /// <summary>
    /// Registers configuration settings
    /// </summary>
    public static IServiceCollection AddDigitalSignageSettings(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind and register ServerSettings
        services.Configure<ServerSettings>(configuration.GetSection("ServerSettings"));

        // Also register as Singleton for backward compatibility
        var serverSettings = new ServerSettings();
        configuration.GetSection("ServerSettings").Bind(serverSettings);
        services.AddSingleton(serverSettings);

        // Register QueryCacheSettings
        services.Configure<QueryCacheSettings>(configuration.GetSection("QueryCacheSettings"));

        // Register ConnectionPoolSettings
        services.Configure<ConnectionPoolSettings>(configuration.GetSection("ConnectionPoolSettings"));

        return services;
    }
}
