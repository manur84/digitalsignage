using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using DigitalSignage.Core.Exceptions;
using DigitalSignage.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace DigitalSignage.Server.Services;

public class ClientService : IClientService, IDisposable
{
    private readonly ConcurrentDictionary<string, RaspberryPiClient> _clients = new();
    private readonly ICommunicationService _communicationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ClientService> _logger;
    private readonly ClientRegistrationHandler _registrationHandler;
    private readonly ClientLayoutDistributor _layoutDistributor;
    private bool _isInitialized = false;
    private readonly SemaphoreSlim _initSemaphore = new(1, 1);
    private Task? _initializationTask;
    private bool _disposed = false;

    // Cleanup timer for removing old/stale clients from memory cache
    private readonly System.Threading.Timer? _cleanupTimer;
    private const int CleanupIntervalHours = 1; // Run cleanup every hour
    private const int StaleClientDays = 30; // Remove clients not seen in 30 days

    /// <summary>
    /// Event raised when a client connects
    /// </summary>
    public event EventHandler<string>? ClientConnected;

    /// <summary>
    /// Event raised when a client disconnects
    /// </summary>
    public event EventHandler<string>? ClientDisconnected;

    /// <summary>
    /// Event raised when a client status changes
    /// </summary>
    public event EventHandler<string>? ClientStatusChanged;

    public ClientService(
        ICommunicationService communicationService,
        ILayoutService layoutService,
        ISqlDataService dataService,
        IScribanService scribanService,
        AsyncLockService lockService,
        IServiceProvider serviceProvider,
        ILogger<ClientService> logger)
    {
        _communicationService = communicationService ?? throw new ArgumentNullException(nameof(communicationService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // CRITICAL FIX: Create loggers for internal helper classes using ILoggerFactory
        // This avoids exposing internal types in the public API
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var registrationLogger = loggerFactory.CreateLogger<ClientRegistrationHandler>();
        var distributorLogger = loggerFactory.CreateLogger<ClientLayoutDistributor>();

        // Initialize helper components
        _registrationHandler = new ClientRegistrationHandler(
            serviceProvider,
            registrationLogger,
            communicationService,
            layoutService,
            lockService);

        _layoutDistributor = new ClientLayoutDistributor(
            serviceProvider,
            distributorLogger,
            communicationService,
            layoutService,
            dataService,
            scribanService);

        // Track the initialization task instead of fire-and-forget
        _initializationTask = InitializeClientsWithRetryAsync();

        // Initialize cleanup timer (runs every hour to remove stale clients from cache)
        _cleanupTimer = new System.Threading.Timer(
            callback: _ => CleanupStaleClientsAsync().ConfigureAwait(false),
            state: null,
            dueTime: TimeSpan.FromHours(CleanupIntervalHours),
            period: TimeSpan.FromHours(CleanupIntervalHours)
        );
        _logger.LogDebug("Client cleanup timer initialized (interval: {Hours}h, stale threshold: {Days} days)",
            CleanupIntervalHours, StaleClientDays);
    }

    private async Task InitializeClientsWithRetryAsync()
    {
        // Retry initialization with exponential backoff to wait for database initialization
        var maxRetries = 10;
        var delayMs = 500;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await InitializeClientsAsync();
                return; // Success
            }
            catch (Exception ex)
            {
                if (attempt < maxRetries)
                {
                    _logger.LogWarning("Failed to load clients from database (attempt {Attempt}/{MaxRetries}): {Message}. Retrying in {DelayMs}ms...",
                        attempt, maxRetries, ex.Message, delayMs);
                    await Task.Delay(delayMs);
                    delayMs = Math.Min(delayMs * 2, 5000); // Exponential backoff, max 5s
                }
                else
                {
                    _logger.LogError(ex, "Failed to load clients from database after {MaxRetries} attempts. Service will continue without pre-loaded clients.", maxRetries);
                }
            }
        }
    }

    private async Task InitializeClientsAsync()
    {
        if (_isInitialized) return;

        await _initSemaphore.WaitAsync();
        try
        {
            if (_isInitialized) return;

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DigitalSignageDbContext>();

            var dbClients = await dbContext.Clients.ToListAsync();

            foreach (var client in dbClients)
            {
                if (client == null || string.IsNullOrWhiteSpace(client.Id))
                {
                    _logger.LogWarning("Skipping client with null or empty ID during initialization");
                    continue;
                }

                // Mark all as offline on startup
                client.Status = ClientStatus.Offline;
                _clients[client.Id] = client;
            }

            _isInitialized = true;
            _logger.LogInformation("Loaded {Count} clients from database", dbClients.Count);
        }
        finally
        {
            _initSemaphore.Release();
        }
    }

    /// <summary>
    /// Ensures that client initialization has completed
    /// </summary>
    /// <returns>A task that completes when initialization is done</returns>
    public async Task EnsureInitializedAsync()
    {
        if (_initializationTask != null)
        {
            try
            {
                await _initializationTask;
                _logger.LogDebug("Client initialization completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Client initialization failed, but service will continue");
            }
        }
    }

    public async Task<Result<List<RaspberryPiClient>>> GetAllClientsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            ThrowIfDisposed();

            // Reload from database to get latest DeviceInfo including resolution
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DigitalSignageDbContext>();

            var dbClients = await dbContext.Clients.ToListAsync(cancellationToken);

            // Update in-memory cache with fresh data from database
            foreach (var dbClient in dbClients)
            {
                if (!string.IsNullOrWhiteSpace(dbClient.Id))
                {
                    // Preserve online status from in-memory cache (reflects real-time connection state)
                    if (_clients.TryGetValue(dbClient.Id, out var cachedClient))
                    {
                        dbClient.Status = cachedClient.Status;

                        // Preserve live device info (e.g. resolution) when DB entry is missing it
                        if (cachedClient.DeviceInfo != null)
                        {
                            dbClient.DeviceInfo ??= new DeviceInfo();

                            var cachedInfo = cachedClient.DeviceInfo;
                            var dbInfo = dbClient.DeviceInfo;

                            if (string.IsNullOrWhiteSpace(dbInfo.Hostname) && !string.IsNullOrWhiteSpace(cachedInfo.Hostname))
                                dbInfo.Hostname = cachedInfo.Hostname;
                            if (string.IsNullOrWhiteSpace(dbInfo.Model) && !string.IsNullOrWhiteSpace(cachedInfo.Model))
                                dbInfo.Model = cachedInfo.Model;
                            if (string.IsNullOrWhiteSpace(dbInfo.OsVersion) && !string.IsNullOrWhiteSpace(cachedInfo.OsVersion))
                                dbInfo.OsVersion = cachedInfo.OsVersion;
                            if (string.IsNullOrWhiteSpace(dbInfo.ClientVersion) && !string.IsNullOrWhiteSpace(cachedInfo.ClientVersion))
                                dbInfo.ClientVersion = cachedInfo.ClientVersion;

                            if (dbInfo.ScreenWidth <= 0 && cachedInfo.ScreenWidth > 0)
                                dbInfo.ScreenWidth = cachedInfo.ScreenWidth;
                            if (dbInfo.ScreenHeight <= 0 && cachedInfo.ScreenHeight > 0)
                                dbInfo.ScreenHeight = cachedInfo.ScreenHeight;

                            if (dbInfo.CpuTemperature <= 0 && cachedInfo.CpuTemperature > 0)
                                dbInfo.CpuTemperature = cachedInfo.CpuTemperature;
                            if (dbInfo.CpuUsage <= 0 && cachedInfo.CpuUsage > 0)
                                dbInfo.CpuUsage = cachedInfo.CpuUsage;
                            if (dbInfo.MemoryTotal <= 0 && cachedInfo.MemoryTotal > 0)
                                dbInfo.MemoryTotal = cachedInfo.MemoryTotal;
                            if (dbInfo.MemoryUsed <= 0 && cachedInfo.MemoryUsed > 0)
                                dbInfo.MemoryUsed = cachedInfo.MemoryUsed;
                            if (dbInfo.DiskTotal <= 0 && cachedInfo.DiskTotal > 0)
                                dbInfo.DiskTotal = cachedInfo.DiskTotal;
                            if (dbInfo.DiskUsed <= 0 && cachedInfo.DiskUsed > 0)
                                dbInfo.DiskUsed = cachedInfo.DiskUsed;
                            if (dbInfo.NetworkLatency <= 0 && cachedInfo.NetworkLatency > 0)
                                dbInfo.NetworkLatency = cachedInfo.NetworkLatency;
                            if (dbInfo.Uptime <= 0 && cachedInfo.Uptime > 0)
                                dbInfo.Uptime = cachedInfo.Uptime;
                        }
                    }

                    _clients[dbClient.Id] = dbClient;
                }
            }

            return Result<List<RaspberryPiClient>>.Success(dbClients);
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogError(ex, "ClientService has been disposed");
            return Result<List<RaspberryPiClient>>.Failure("Service is no longer available", ex);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Get all clients operation cancelled");
            return Result<List<RaspberryPiClient>>.Failure("Operation was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload clients from database, returning cached data");
            // Return cached data as success with warning logged
            var cachedClients = _clients.Values.ToList();
            return Result<List<RaspberryPiClient>>.Success(cachedClients);
        }
    }

    public Task<Result<RaspberryPiClient>> GetClientByIdAsync(string clientId, CancellationToken cancellationToken = default)
    {
        try
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(clientId))
            {
                _logger.LogWarning("GetClientByIdAsync called with null or empty clientId");
                return Task.FromResult(Result<RaspberryPiClient>.Failure("Client ID cannot be empty"));
            }

            if (_clients.TryGetValue(clientId, out var client))
            {
                return Task.FromResult(Result<RaspberryPiClient>.Success(client));
            }

            _logger.LogWarning("Client {ClientId} not found", clientId);
            return Task.FromResult(Result<RaspberryPiClient>.Failure($"Client '{clientId}' not found"));
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogError(ex, "ClientService has been disposed");
            return Task.FromResult(Result<RaspberryPiClient>.Failure("Service is no longer available", ex));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get client {ClientId}", clientId);
            return Task.FromResult(Result<RaspberryPiClient>.Failure($"Failed to retrieve client: {ex.Message}", ex));
        }
    }

    public async Task<Result<RaspberryPiClient>> RegisterClientAsync(
        RegisterMessage registerMessage,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ThrowIfDisposed();

            // Delegate to registration handler
            return await _registrationHandler.RegisterClientAsync(
                registerMessage,
                _clients,
                ClientConnected,
                cancellationToken);
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogError(ex, "ClientService has been disposed");
            return Result<RaspberryPiClient>.Failure("Service is no longer available", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register client from MAC {MacAddress}", registerMessage?.MacAddress);
            return Result<RaspberryPiClient>.Failure($"Failed to register client: {ex.Message}", ex);
        }
    }

    public async Task<Result> UpdateClientStatusAsync(
        string clientId,
        ClientStatus status,
        DeviceInfo? deviceInfo = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(clientId))
            {
                _logger.LogWarning("UpdateClientStatusAsync called with null or empty clientId");
                return Result.Failure("Client ID cannot be empty");
            }

        if (_clients.TryGetValue(clientId, out var client))
        {
            var oldStatus = client.Status;
            client.Status = status;
            client.LastSeen = DateTime.UtcNow;
            if (deviceInfo != null)
            {
                client.DeviceInfo = deviceInfo;
            }

            _logger.LogDebug("Updated client {ClientId} status to {Status}", clientId, status);

            // Raise events if status changed
            if (oldStatus != status)
            {
                ClientStatusChanged?.Invoke(this, clientId);
                _logger.LogDebug("Raised ClientStatusChanged event for {ClientId}: {OldStatus} -> {NewStatus}", clientId, oldStatus, status);

                // Raise specific connect/disconnect events
                if (status == ClientStatus.Online && oldStatus == ClientStatus.Offline)
                {
                    ClientConnected?.Invoke(this, clientId);
                    _logger.LogDebug("Raised ClientConnected event for {ClientId}", clientId);
                }
                else if (status == ClientStatus.Offline && oldStatus == ClientStatus.Online)
                {
                    ClientDisconnected?.Invoke(this, clientId);
                    _logger.LogDebug("Raised ClientDisconnected event for {ClientId}", clientId);
                }
            }

            // Update in database - await instead of fire-and-forget
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<DigitalSignageDbContext>();

                var dbClient = await dbContext.Clients.FindAsync(new object[] { clientId }, cancellationToken);
                if (dbClient != null)
                {
                    dbClient.Status = status;
                    dbClient.LastSeen = DateTime.UtcNow;
                    if (deviceInfo != null)
                    {
                        dbClient.DeviceInfo = deviceInfo;
                    }
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update client {ClientId} status in database", clientId);
                // Don't fail the whole operation if DB update fails - in-memory cache is updated
            }

            return Result.Success();
        }
        else
        {
            _logger.LogWarning("Client {ClientId} not found for status update", clientId);
            return Result.Failure($"Client '{clientId}' not found");
        }
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogError(ex, "ClientService has been disposed");
            return Result.Failure("Service is no longer available", ex);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Update client status operation cancelled for {ClientId}", clientId);
            return Result.Failure("Operation was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update client {ClientId} status", clientId);
            return Result.Failure($"Failed to update client status: {ex.Message}", ex);
        }
    }

    public async Task<Result> SendCommandAsync(
        string clientId,
        string command,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(clientId))
            {
                _logger.LogWarning("SendCommandAsync called with null or empty clientId");
                return Result.Failure("Client ID cannot be empty");
            }

            if (string.IsNullOrWhiteSpace(command))
            {
                _logger.LogWarning("SendCommandAsync called with null or empty command");
                return Result.Failure("Command cannot be empty");
            }

            if (!_clients.ContainsKey(clientId))
            {
                _logger.LogWarning("Client {ClientId} not found for command {Command}", clientId, command);
                return Result.Failure($"Client '{clientId}' not found");
            }

            var commandMessage = new CommandMessage
            {
                Command = command,
                Parameters = parameters
            };

            await _communicationService.SendMessageAsync(clientId, commandMessage, cancellationToken);
            _logger.LogInformation("Sent command {Command} to client {ClientId}", command, clientId);
            return Result.Success();
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogError(ex, "ClientService has been disposed");
            return Result.Failure("Service is no longer available", ex);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Send command operation cancelled for client {ClientId}", clientId);
            return Result.Failure("Operation was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send command {Command} to client {ClientId}", command, clientId);
            return Result.Failure($"Failed to send command: {ex.Message}", ex);
        }
    }

    public async Task<Result> AssignLayoutAsync(
        string clientId,
        string layoutId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ThrowIfDisposed();

            // Delegate to layout distributor
            return await _layoutDistributor.AssignLayoutAsync(
                clientId,
                layoutId,
                _clients,
                cancellationToken);
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogError(ex, "ClientService has been disposed");
            return Result.Failure("Service is no longer available", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assign layout {LayoutId} to client {ClientId}", layoutId, clientId);
            return Result.Failure($"Failed to assign layout: {ex.Message}", ex);
        }
    }


    public async Task<Result> RemoveClientAsync(string clientId, CancellationToken cancellationToken = default)
    {
        try
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(clientId))
            {
                _logger.LogWarning("RemoveClientAsync called with null or empty clientId");
                return Result.Failure("Client ID cannot be empty");
            }

            if (_clients.TryRemove(clientId, out _))
            {
                _logger.LogInformation("Removed client {ClientId}", clientId);

                // Remove from database
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<DigitalSignageDbContext>();

                    var dbClient = await dbContext.Clients.FindAsync(new object[] { clientId }, cancellationToken);
                    if (dbClient != null)
                    {
                        dbContext.Clients.Remove(dbClient);
                        await dbContext.SaveChangesAsync(cancellationToken);
                        _logger.LogInformation("Removed client {ClientId} from database", clientId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to remove client {ClientId} from database", clientId);
                }

                return Result.Success();
            }

            _logger.LogWarning("Client {ClientId} not found for removal", clientId);
            return Result.Failure($"Client '{clientId}' not found");
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogError(ex, "ClientService has been disposed");
            return Result.Failure("Service is no longer available", ex);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Client removal cancelled for {ClientId}", clientId);
            return Result.Failure("Operation was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove client {ClientId}", clientId);
            return Result.Failure($"Failed to remove client: {ex.Message}", ex);
        }
    }

    public async Task<Result> UpdateClientConfigAsync(
        string clientId,
        UpdateConfigMessage config,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(clientId))
            {
                _logger.LogWarning("UpdateClientConfigAsync called with null or empty clientId");
                return Result.Failure("Client ID cannot be empty");
            }

            if (config == null)
            {
                _logger.LogWarning("UpdateClientConfigAsync called with null config");
                return Result.Failure("Configuration cannot be null");
            }

            if (!_clients.ContainsKey(clientId))
            {
                _logger.LogWarning("Client {ClientId} not found for config update", clientId);
                return Result.Failure($"Client '{clientId}' not found");
            }

            await _communicationService.SendMessageAsync(clientId, config, cancellationToken);
            _logger.LogInformation("Sent UPDATE_CONFIG to client {ClientId} (Host: {Host}, Port: {Port})",
                clientId, config.ServerHost, config.ServerPort);
            return Result.Success();
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogError(ex, "ClientService has been disposed");
            return Result.Failure("Service is no longer available", ex);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Config update cancelled for client {ClientId}", clientId);
            return Result.Failure("Operation was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send UPDATE_CONFIG to client {ClientId}", clientId);
            return Result.Failure($"Failed to update client configuration: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Update client properties (Name, Group, Location)
    /// </summary>
    public async Task<Result> UpdateClientAsync(
        string clientId,
        string? name = null,
        string? group = null,
        string? location = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(clientId))
            {
                _logger.LogWarning("UpdateClientAsync called with null or empty clientId");
                return Result.Failure("Client ID cannot be empty");
            }

            if (!_clients.TryGetValue(clientId, out var client))
            {
                _logger.LogWarning("Client {ClientId} not found for update", clientId);
                return Result.Failure($"Client '{clientId}' not found");
            }

            // Update in-memory client
            if (name != null)
                client.Name = name;
            if (group != null)
                client.Group = string.IsNullOrWhiteSpace(group) ? null : group;
            if (location != null)
                client.Location = string.IsNullOrWhiteSpace(location) ? null : location;

            // Update in database
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DigitalSignageDbContext>();

            var dbClient = await dbContext.Clients.FindAsync(new object[] { clientId }, cancellationToken);
            if (dbClient != null)
            {
                if (name != null)
                    dbClient.Name = name;
                if (group != null)
                    dbClient.Group = string.IsNullOrWhiteSpace(group) ? null : group;
                if (location != null)
                    dbClient.Location = string.IsNullOrWhiteSpace(location) ? null : location;

                await dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Updated client {ClientId} (Name: {Name}, Group: {Group}, Location: {Location})",
                    clientId, dbClient.Name, dbClient.Group ?? "None", dbClient.Location ?? "None");
            }

            return Result.Success();
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogError(ex, "ClientService has been disposed");
            return Result.Failure("Service is no longer available", ex);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Client update cancelled for {ClientId}", clientId);
            return Result.Failure("Operation was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update client {ClientId}", clientId);
            return Result.Failure($"Failed to update client: {ex.Message}", ex);
        }
    }


    /// <summary>
    /// Throws ObjectDisposedException if service has been disposed
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ClientService));
        }
    }

    /// <summary>
    /// Removes stale clients from the in-memory cache (clients not seen in StaleClientDays)
    /// This prevents the cache from growing indefinitely with old/inactive clients
    /// Note: Only removes from cache, not from database
    /// </summary>
    private async Task CleanupStaleClientsAsync()
    {
        if (_disposed)
            return;

        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-StaleClientDays);
            var staleClients = _clients.Values
                .Where(c => c.LastSeen < cutoffDate && c.Status == ClientStatus.Offline)
                .ToList();

            if (staleClients.Count == 0)
            {
                _logger.LogDebug("Client cleanup: No stale clients found");
                return;
            }

            _logger.LogInformation("Client cleanup: Found {Count} stale clients (not seen since {Date})",
                staleClients.Count, cutoffDate);

            foreach (var client in staleClients)
            {
                if (_clients.TryRemove(client.Id, out _))
                {
                    _logger.LogDebug("Removed stale client {ClientId} ({Name}) from cache (last seen: {LastSeen})",
                        client.Id, client.DisplayName, client.LastSeen);
                }
            }

            _logger.LogInformation("Client cleanup: Removed {Count} stale clients from cache", staleClients.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during client cleanup");
        }

        await Task.CompletedTask;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes managed and unmanaged resources
    /// </summary>
    /// <param name="disposing">True if disposing managed resources</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // Dispose managed resources
            _cleanupTimer?.Dispose();
            _initSemaphore?.Dispose();
            _logger.LogInformation("ClientService disposed");
        }

        _disposed = true;
    }
}
