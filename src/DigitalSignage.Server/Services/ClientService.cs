using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using DigitalSignage.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace DigitalSignage.Server.Services;

public class ClientService : IClientService
{
    private readonly ConcurrentDictionary<string, RaspberryPiClient> _clients = new();
    private readonly ICommunicationService _communicationService;
    private readonly ILayoutService _layoutService;
    private readonly ISqlDataService _dataService;
    private readonly ITemplateService _templateService;
    private readonly IServiceProvider _serviceProvider;
    private readonly DataSourceManager _dataSourceManager;
    private readonly ILogger<ClientService> _logger;
    private bool _isInitialized = false;
    private readonly SemaphoreSlim _initSemaphore = new(1, 1);

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
        ITemplateService templateService,
        IServiceProvider serviceProvider,
        DataSourceManager dataSourceManager,
        ILogger<ClientService> logger)
    {
        _communicationService = communicationService ?? throw new ArgumentNullException(nameof(communicationService));
        _layoutService = layoutService ?? throw new ArgumentNullException(nameof(layoutService));
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _dataSourceManager = dataSourceManager ?? throw new ArgumentNullException(nameof(dataSourceManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Load clients from database on startup with retry logic
        _ = InitializeClientsWithRetryAsync();
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

    public Task<List<RaspberryPiClient>> GetAllClientsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_clients.Values.ToList());
    }

    public Task<RaspberryPiClient?> GetClientByIdAsync(string clientId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            _logger.LogWarning("GetClientByIdAsync called with null or empty clientId");
            return Task.FromResult<RaspberryPiClient?>(null);
        }

        _clients.TryGetValue(clientId, out var client);
        return Task.FromResult(client);
    }

    public async Task<RaspberryPiClient> RegisterClientAsync(
        RegisterMessage registerMessage,
        CancellationToken cancellationToken = default)
    {
        if (registerMessage == null)
        {
            throw new ArgumentNullException(nameof(registerMessage));
        }

        if (string.IsNullOrWhiteSpace(registerMessage.MacAddress))
        {
            throw new ArgumentException("MAC address is required for registration", nameof(registerMessage));
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DigitalSignageDbContext>();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();

            // Validate registration token if provided
            string? assignedGroup = null;
            string? assignedLocation = null;

            if (!string.IsNullOrWhiteSpace(registerMessage.RegistrationToken))
            {
                _logger.LogInformation("Validating registration token for MAC {MacAddress}", registerMessage.MacAddress);

                var validationResult = await authService.ValidateRegistrationTokenAsync(
                    registerMessage.RegistrationToken,
                    registerMessage.MacAddress,
                    cancellationToken);

                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("Registration failed for MAC {MacAddress}: {Error}",
                        registerMessage.MacAddress, validationResult.ErrorMessage);
                    throw new UnauthorizedAccessException(validationResult.ErrorMessage ?? "Invalid registration token");
                }

                assignedGroup = validationResult.AutoAssignGroup;
                assignedLocation = validationResult.AutoAssignLocation;

                // Consume the token
                await authService.ConsumeRegistrationTokenAsync(
                    registerMessage.RegistrationToken,
                    registerMessage.ClientId,
                    cancellationToken);

                _logger.LogInformation("Registration token validated and consumed for MAC {MacAddress}", registerMessage.MacAddress);
            }
            else
            {
                _logger.LogWarning("Client registration without token from MAC {MacAddress} - checking if already registered",
                    registerMessage.MacAddress);
            }

            // Check if client already exists by MAC address
            var existingClient = await dbContext.Clients
                .FirstOrDefaultAsync(c => c.MacAddress == registerMessage.MacAddress, cancellationToken);

            RaspberryPiClient client;

            if (existingClient != null)
            {
                // Update existing client
                client = existingClient;

                // Check if client ID needs to be changed
                if (!string.IsNullOrWhiteSpace(registerMessage.ClientId) && registerMessage.ClientId != client.Id)
                {
                    // Client wants to change ID - this requires deleting the old entity and creating a new one
                    // because EF Core doesn't allow modifying primary keys on tracked entities
                    _logger.LogInformation("Client MAC {MacAddress} changing ID from {OldId} to {NewId}",
                        client.MacAddress, client.Id, registerMessage.ClientId);

                    // Remove old entity from database
                    dbContext.Clients.Remove(existingClient);
                    await dbContext.SaveChangesAsync(cancellationToken);

                    // Remove old ID from cache
                    _clients.TryRemove(client.Id, out _);

                    // Merge DeviceInfo from registration message with existing data
                    var mergedDeviceInfo = existingClient.DeviceInfo ?? new DeviceInfo();
                    if (registerMessage.DeviceInfo != null)
                    {
                        // Update all fields from registration message
                        mergedDeviceInfo.Hostname = registerMessage.DeviceInfo.Hostname ?? mergedDeviceInfo.Hostname;
                        mergedDeviceInfo.Model = registerMessage.DeviceInfo.Model ?? mergedDeviceInfo.Model;
                        mergedDeviceInfo.OsVersion = registerMessage.DeviceInfo.OsVersion ?? mergedDeviceInfo.OsVersion;
                        mergedDeviceInfo.ClientVersion = registerMessage.DeviceInfo.ClientVersion ?? mergedDeviceInfo.ClientVersion;
                        mergedDeviceInfo.ScreenWidth = registerMessage.DeviceInfo.ScreenWidth;
                        mergedDeviceInfo.ScreenHeight = registerMessage.DeviceInfo.ScreenHeight;
                        mergedDeviceInfo.CpuTemperature = registerMessage.DeviceInfo.CpuTemperature;
                        mergedDeviceInfo.CpuUsage = registerMessage.DeviceInfo.CpuUsage;
                        mergedDeviceInfo.MemoryTotal = registerMessage.DeviceInfo.MemoryTotal;
                        mergedDeviceInfo.MemoryUsed = registerMessage.DeviceInfo.MemoryUsed;
                        mergedDeviceInfo.DiskTotal = registerMessage.DeviceInfo.DiskTotal;
                        mergedDeviceInfo.DiskUsed = registerMessage.DeviceInfo.DiskUsed;
                        mergedDeviceInfo.Uptime = registerMessage.DeviceInfo.Uptime;
                    }

                    // Create new entity with new ID but same data
                    client = new RaspberryPiClient
                    {
                        Id = registerMessage.ClientId,
                        MacAddress = existingClient.MacAddress,
                        IpAddress = registerMessage.IpAddress ?? existingClient.IpAddress,
                        Group = existingClient.Group,
                        Location = existingClient.Location,
                        AssignedLayoutId = existingClient.AssignedLayoutId,
                        RegisteredAt = existingClient.RegisteredAt,
                        LastSeen = DateTime.UtcNow,
                        Status = ClientStatus.Online,
                        DeviceInfo = mergedDeviceInfo
                    };

                    dbContext.Clients.Add(client);
                    _logger.LogInformation("Re-registered existing client with new ID {ClientId} (MAC: {MacAddress})", client.Id, client.MacAddress);
                }
                else
                {
                    // Same ID, just update the properties
                    client.IpAddress = registerMessage.IpAddress ?? client.IpAddress;
                    client.LastSeen = DateTime.UtcNow;
                    client.Status = ClientStatus.Online;

                    // Merge DeviceInfo - update from registration but preserve existing values if not provided
                    if (registerMessage.DeviceInfo != null)
                    {
                        var deviceInfo = client.DeviceInfo ?? new DeviceInfo();
                        deviceInfo.Hostname = registerMessage.DeviceInfo.Hostname ?? deviceInfo.Hostname;
                        deviceInfo.Model = registerMessage.DeviceInfo.Model ?? deviceInfo.Model;
                        deviceInfo.OsVersion = registerMessage.DeviceInfo.OsVersion ?? deviceInfo.OsVersion;
                        deviceInfo.ClientVersion = registerMessage.DeviceInfo.ClientVersion ?? deviceInfo.ClientVersion;
                        deviceInfo.ScreenWidth = registerMessage.DeviceInfo.ScreenWidth;
                        deviceInfo.ScreenHeight = registerMessage.DeviceInfo.ScreenHeight;
                        deviceInfo.CpuTemperature = registerMessage.DeviceInfo.CpuTemperature;
                        deviceInfo.CpuUsage = registerMessage.DeviceInfo.CpuUsage;
                        deviceInfo.MemoryTotal = registerMessage.DeviceInfo.MemoryTotal;
                        deviceInfo.MemoryUsed = registerMessage.DeviceInfo.MemoryUsed;
                        deviceInfo.DiskTotal = registerMessage.DeviceInfo.DiskTotal;
                        deviceInfo.DiskUsed = registerMessage.DeviceInfo.DiskUsed;
                        deviceInfo.Uptime = registerMessage.DeviceInfo.Uptime;
                        client.DeviceInfo = deviceInfo;
                    }

                    _logger.LogInformation("Re-registered existing client {ClientId} (MAC: {MacAddress})", client.Id, client.MacAddress);
                }
            }
            else
            {
                // Create new client - ensure DeviceInfo is properly populated
                var deviceInfo = registerMessage.DeviceInfo ?? new DeviceInfo();

                client = new RaspberryPiClient
                {
                    Id = string.IsNullOrWhiteSpace(registerMessage.ClientId) ? Guid.NewGuid().ToString() : registerMessage.ClientId,
                    IpAddress = registerMessage.IpAddress ?? "unknown",
                    MacAddress = registerMessage.MacAddress,
                    Group = assignedGroup,
                    Location = assignedLocation,
                    RegisteredAt = DateTime.UtcNow,
                    LastSeen = DateTime.UtcNow,
                    Status = ClientStatus.Online,
                    DeviceInfo = deviceInfo
                };

                dbContext.Clients.Add(client);
                _logger.LogInformation("Registered new client {ClientId} (MAC: {MacAddress}) from {IpAddress} - Hostname: {Hostname}, Resolution: {Width}x{Height}",
                    client.Id, client.MacAddress, client.IpAddress, deviceInfo.Hostname, deviceInfo.ScreenWidth, deviceInfo.ScreenHeight);
            }

            // Save to database
            await dbContext.SaveChangesAsync(cancellationToken);

            // Update in-memory cache
            _clients[client.Id] = client;

            // Send registration response
            var responseMessage = new RegistrationResponseMessage
            {
                Success = true,
                AssignedClientId = client.Id,
                AssignedGroup = client.Group,
                AssignedLocation = client.Location
            };

            try
            {
                await _communicationService.SendMessageAsync(client.Id, responseMessage, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send registration response to client {ClientId}", client.Id);
            }

            // If client has an assigned layout, send it immediately after registration
            if (!string.IsNullOrEmpty(client.AssignedLayoutId))
            {
                _logger.LogInformation("Client {ClientId} has assigned layout {LayoutId}, sending DISPLAY_UPDATE",
                    client.Id, client.AssignedLayoutId);

                try
                {
                    var layout = await _layoutService.GetLayoutByIdAsync(client.AssignedLayoutId, cancellationToken);
                    if (layout != null)
                    {
                        // Fetch data for data-driven elements
                        // TODO: Implement data source fetching when data-driven elements are supported
                        Dictionary<string, object>? layoutData = null;

                        // Send DISPLAY_UPDATE message
                        var displayUpdate = new DisplayUpdateMessage
                        {
                            Layout = layout,
                            Data = layoutData
                        };

                        await _communicationService.SendMessageAsync(client.Id, displayUpdate, cancellationToken);
                        _logger.LogInformation("Successfully sent assigned layout {LayoutId} to reconnected client {ClientId}",
                            layout.Id, client.Id);
                    }
                    else
                    {
                        _logger.LogWarning("Client {ClientId} has assigned layout {LayoutId} but layout not found in database",
                            client.Id, client.AssignedLayoutId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send assigned layout to client {ClientId} after registration", client.Id);
                }
            }
            else
            {
                _logger.LogInformation("Client {ClientId} has no assigned layout", client.Id);
            }

            // Raise ClientConnected event
            ClientConnected?.Invoke(this, client.Id);
            _logger.LogDebug("Raised ClientConnected event for {ClientId}", client.Id);

            return client;
        }
        catch (UnauthorizedAccessException)
        {
            // Re-throw authentication errors
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register client from MAC {MacAddress}", registerMessage.MacAddress);
            throw;
        }
    }

    public async Task UpdateClientStatusAsync(
        string clientId,
        ClientStatus status,
        DeviceInfo? deviceInfo = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            _logger.LogWarning("UpdateClientStatusAsync called with null or empty clientId");
            return;
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
            }
        }
        else
        {
            _logger.LogWarning("Client {ClientId} not found for status update", clientId);
        }
    }

    public async Task<bool> SendCommandAsync(
        string clientId,
        string command,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            _logger.LogWarning("SendCommandAsync called with null or empty clientId");
            return false;
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            _logger.LogWarning("SendCommandAsync called with null or empty command");
            return false;
        }

        if (!_clients.ContainsKey(clientId))
        {
            _logger.LogWarning("Client {ClientId} not found for command {Command}", clientId, command);
            return false;
        }

        var commandMessage = new CommandMessage
        {
            Command = command,
            Parameters = parameters
        };

        try
        {
            await _communicationService.SendMessageAsync(clientId, commandMessage, cancellationToken);
            _logger.LogInformation("Sent command {Command} to client {ClientId}", command, clientId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send command {Command} to client {ClientId}", command, clientId);
            return false;
        }
    }

    public async Task<bool> AssignLayoutAsync(
        string clientId,
        string layoutId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            _logger.LogWarning("AssignLayoutAsync called with null or empty clientId");
            return false;
        }

        if (string.IsNullOrWhiteSpace(layoutId))
        {
            _logger.LogWarning("AssignLayoutAsync called with null or empty layoutId");
            return false;
        }

        if (_clients.TryGetValue(clientId, out var client))
        {
            client.AssignedLayoutId = layoutId;
            _logger.LogInformation("Assigned layout {LayoutId} to client {ClientId}", layoutId, clientId);

            // Update in database
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<DigitalSignageDbContext>();

                var dbClient = await dbContext.Clients.FindAsync(new object[] { clientId }, cancellationToken);
                if (dbClient != null)
                {
                    dbClient.AssignedLayoutId = layoutId;
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update client {ClientId} layout assignment in database", clientId);
            }

            // Send layout update to client
            return await SendLayoutToClientAsync(clientId, layoutId, cancellationToken);
        }

        _logger.LogWarning("Client {ClientId} not found for layout assignment", clientId);
        return false;
    }

    private async Task<bool> SendLayoutToClientAsync(
        string clientId,
        string layoutId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Load layout
            var layout = await _layoutService.GetLayoutByIdAsync(layoutId, cancellationToken);
            if (layout == null)
            {
                _logger.LogError("Layout {LayoutId} not found", layoutId);
                return false;
            }

            // Fetch data from all data sources
            var layoutData = new Dictionary<string, object>();
            if (layout.DataSources != null && layout.DataSources.Count > 0)
            {
                foreach (var dataSource in layout.DataSources)
                {
                    try
                    {
                        var data = await _dataService.GetDataAsync(dataSource, cancellationToken);
                        layoutData[dataSource.Id] = data;
                        _logger.LogDebug("Loaded data for source {DataSourceId}", dataSource.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load data for source {DataSourceId}, using empty data", dataSource.Id);
                        layoutData[dataSource.Id] = new Dictionary<string, object>();
                    }
                }
            }

            // Process templates in layout elements
            // Flatten all data into a single dictionary for template processing
            var templateData = new Dictionary<string, object>();
            foreach (var kvp in layoutData)
            {
                if (kvp.Value is Dictionary<string, object> dict)
                {
                    foreach (var dataKvp in dict)
                    {
                        templateData[dataKvp.Key] = dataKvp.Value;
                    }
                }
            }

            // Process text elements with templates
            if (layout.Elements != null && layout.Elements.Count > 0)
            {
                foreach (var element in layout.Elements)
                {
                    if (element.Type == "text")
                    {
                        try
                        {
                            var content = element["Content"]?.ToString();
                            if (!string.IsNullOrWhiteSpace(content))
                            {
                                element["Content"] = await _templateService.ProcessTemplateAsync(
                                    content,
                                    templateData,
                                    cancellationToken);
                                _logger.LogDebug("Processed template for element {ElementId}", element.Id);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to process template for element {ElementId}, using original content", element.Id);
                        }
                    }
                }
            }

            // Embed media files (images) as Base64 for client transfer
            await EmbedMediaFilesInLayoutAsync(layout, cancellationToken);

            // Check if layout has linked SQL data sources
            var linkedDataSources = new List<LayoutDataSourceInfo>();
            if (layout.LinkedDataSourceIds != null && layout.LinkedDataSourceIds.Count > 0)
            {
                _logger.LogInformation("Layout {LayoutId} has {Count} linked SQL data sources",
                    layoutId, layout.LinkedDataSourceIds.Count);

                foreach (var dsId in layout.LinkedDataSourceIds)
                {
                    var dataSource = _dataSourceManager.GetDataSource(dsId);
                    if (dataSource != null && dataSource.IsActive)
                    {
                        var cachedData = _dataSourceManager.GetCachedData(dsId) ?? new List<Dictionary<string, object>>();

                        linkedDataSources.Add(new LayoutDataSourceInfo
                        {
                            DataSourceId = dataSource.Id,
                            Name = dataSource.Name,
                            Columns = dataSource.SelectedColumns,
                            InitialData = cachedData
                        });

                        _logger.LogDebug("Included data source {Name} with {RowCount} rows",
                            dataSource.Name, cachedData.Count);
                    }
                    else
                    {
                        _logger.LogWarning("Data source {DataSourceId} not found or inactive", dsId);
                    }
                }
            }

            // If we have linked data sources, send enhanced LAYOUT_ASSIGNED message
            // Otherwise, send standard DISPLAY_UPDATE message
            if (linkedDataSources.Count > 0)
            {
                var layoutAssignmentMessage = new LayoutAssignmentMessage
                {
                    LayoutId = layoutId,
                    Layout = layout,
                    LinkedDataSources = linkedDataSources
                };

                await _communicationService.SendMessageAsync(clientId, layoutAssignmentMessage, cancellationToken);
                _logger.LogInformation("Sent LAYOUT_ASSIGNED with {Count} SQL data sources to client {ClientId}",
                    linkedDataSources.Count, clientId);
            }
            else
            {
                // Standard message (backward compatibility)
                var displayUpdateMessage = new DisplayUpdateMessage
                {
                    Layout = layout,
                    Data = layoutData
                };

                await _communicationService.SendMessageAsync(clientId, displayUpdateMessage, cancellationToken);
                _logger.LogInformation("Sent DISPLAY_UPDATE with full layout {LayoutId} and {DataSourceCount} data sources to client {ClientId}",
                    layoutId, layout.DataSources?.Count ?? 0, clientId);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send layout update to client {ClientId}", clientId);
            return false;
        }
    }

    public Task<bool> RemoveClientAsync(string clientId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            _logger.LogWarning("RemoveClientAsync called with null or empty clientId");
            return Task.FromResult(false);
        }

        if (_clients.TryRemove(clientId, out _))
        {
            _logger.LogInformation("Removed client {ClientId}", clientId);
            return Task.FromResult(true);
        }

        _logger.LogWarning("Client {ClientId} not found for removal", clientId);
        return Task.FromResult(false);
    }

    public async Task<bool> UpdateClientConfigAsync(
        string clientId,
        UpdateConfigMessage config,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            _logger.LogWarning("UpdateClientConfigAsync called with null or empty clientId");
            return false;
        }

        if (config == null)
        {
            _logger.LogWarning("UpdateClientConfigAsync called with null config");
            return false;
        }

        if (!_clients.ContainsKey(clientId))
        {
            _logger.LogWarning("Client {ClientId} not found for config update", clientId);
            return false;
        }

        try
        {
            await _communicationService.SendMessageAsync(clientId, config, cancellationToken);
            _logger.LogInformation("Sent UPDATE_CONFIG to client {ClientId} (Host: {Host}, Port: {Port})",
                clientId, config.ServerHost, config.ServerPort);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send UPDATE_CONFIG to client {ClientId}", clientId);
            return false;
        }
    }

    /// <summary>
    /// Embed media files (images) as Base64 in layout elements for client transfer
    /// </summary>
    private async Task EmbedMediaFilesInLayoutAsync(DisplayLayout layout, CancellationToken cancellationToken)
    {
        if (layout?.Elements == null || layout.Elements.Count == 0)
        {
            _logger.LogDebug("No elements to process for media embedding in layout {LayoutId}", layout.Id);
            return;
        }

        // Get MediaService from DI container
        var mediaService = _serviceProvider.GetService<IMediaService>();
        if (mediaService == null)
        {
            _logger.LogWarning("MediaService not available, cannot embed media files in layout {LayoutId}", layout.Id);
            return;
        }

        int embedCount = 0;
        int errorCount = 0;

        // Process all image elements
        foreach (var element in layout.Elements.Where(e => e.Type?.ToLower() == "image"))
        {
            try
            {
                // Get the Source property (filename, path, or media ID)
                var source = element.GetProperty<string>("Source", "");
                if (string.IsNullOrEmpty(source))
                {
                    _logger.LogDebug("Image element {ElementId} has no Source property, skipping", element.Id);
                    continue;
                }

                // Extract filename (might be full path or just filename)
                var fileName = System.IO.Path.GetFileName(source);

                _logger.LogDebug("Attempting to load media file: {FileName} for element {ElementId}", fileName, element.Id);

                // Try to load media file from disk
                var imageData = await mediaService.GetMediaAsync(fileName);

                if (imageData != null && imageData.Length > 0)
                {
                    // Embed as Base64 (use "MediaData" to match client expectations)
                    var base64 = Convert.ToBase64String(imageData);
                    element.SetProperty("MediaData", base64);

                    embedCount++;
                    _logger.LogInformation("✓ Embedded media file {FileName} ({Size} KB) as Base64 for element {ElementId}",
                        fileName, imageData.Length / 1024, element.Id);
                }
                else
                {
                    errorCount++;
                    _logger.LogWarning("✗ Media file not found: {FileName} for element {ElementId} (Source: {Source})",
                        fileName, element.Id, source);
                }
            }
            catch (Exception ex)
            {
                errorCount++;
                _logger.LogError(ex, "Failed to embed media file for image element {ElementId}", element.Id);
            }
        }

        if (embedCount > 0 || errorCount > 0)
        {
            _logger.LogInformation("Media embedding complete for layout {LayoutId}: {EmbedCount} embedded, {ErrorCount} errors",
                layout.Id, embedCount, errorCount);
        }
        else
        {
            _logger.LogDebug("No image elements found in layout {LayoutId}", layout.Id);
        }
    }
}
