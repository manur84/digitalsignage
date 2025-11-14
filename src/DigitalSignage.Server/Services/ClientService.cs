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
        ILogger<ClientService> logger)
    {
        _communicationService = communicationService ?? throw new ArgumentNullException(nameof(communicationService));
        _layoutService = layoutService ?? throw new ArgumentNullException(nameof(layoutService));
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
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
                        DeviceInfo = registerMessage.DeviceInfo ?? existingClient.DeviceInfo
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
                    client.DeviceInfo = registerMessage.DeviceInfo ?? client.DeviceInfo;

                    _logger.LogInformation("Re-registered existing client {ClientId} (MAC: {MacAddress})", client.Id, client.MacAddress);
                }
            }
            else
            {
                // Create new client
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
                    DeviceInfo = registerMessage.DeviceInfo ?? new DeviceInfo()
                };

                dbContext.Clients.Add(client);
                _logger.LogInformation("Registered new client {ClientId} (MAC: {MacAddress}) from {IpAddress}",
                    client.Id, client.MacAddress, client.IpAddress);
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

            // Update in database (async, don't block)
            _ = Task.Run(async () =>
            {
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
                    _logger.LogWarning(ex, "Failed to update client {ClientId} status in database", clientId);
                }
            }, cancellationToken);
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

            // Create and send display update message
            var displayUpdateMessage = new DisplayUpdateMessage
            {
                Layout = layout,
                Data = layoutData
            };

            await _communicationService.SendMessageAsync(clientId, displayUpdateMessage, cancellationToken);
            _logger.LogInformation("Sent DISPLAY_UPDATE with full layout {LayoutId} and {DataSourceCount} data sources to client {ClientId}",
                layoutId, layout.DataSources?.Count ?? 0, clientId);
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
    private async Task EmbedMediaFilesInLayoutAsync(Layout layout, CancellationToken cancellationToken)
    {
        if (layout == null || layout.Elements == null || layout.Elements.Count == 0)
            return;

        try
        {
            // Get MediaService from DI
            using var scope = _serviceProvider.CreateScope();
            var mediaService = scope.ServiceProvider.GetService<IMediaService>();
            if (mediaService == null)
            {
                _logger.LogWarning("MediaService not available, skipping media embedding");
                return;
            }

            foreach (var element in layout.Elements)
            {
                try
                {
                    // Process Image elements
                    if (element.Type == "image")
                    {
                        var source = element["Source"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(source))
                        {
                            // Check if it's a media library reference (GUID)
                            if (Guid.TryParse(source, out var mediaId))
                            {
                                var media = await mediaService.GetMediaByIdAsync(mediaId.ToString(), cancellationToken);
                                if (media != null && File.Exists(media.FilePath))
                                {
                                    var base64Data = await ConvertFileToBase64Async(media.FilePath, cancellationToken);
                                    element["MediaData"] = base64Data;
                                    element["MediaType"] = media.MimeType;
                                    _logger.LogDebug("Embedded media {MediaId} ({Size} bytes) in element {ElementId}",
                                        mediaId, base64Data.Length, element.Id);
                                }
                            }
                            // Check if it's a file path
                            else if (File.Exists(source))
                            {
                                var base64Data = await ConvertFileToBase64Async(source, cancellationToken);
                                element["MediaData"] = base64Data;
                                element["MediaType"] = GetMimeTypeFromExtension(source);
                                _logger.LogDebug("Embedded file {FilePath} in element {ElementId}", source, element.Id);
                            }
                        }
                    }

                    // Process BackgroundImage in layout
                    if (element.ContainsKey("BackgroundImage"))
                    {
                        var bgImage = element["BackgroundImage"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(bgImage))
                        {
                            if (Guid.TryParse(bgImage, out var mediaId))
                            {
                                var media = await mediaService.GetMediaByIdAsync(mediaId.ToString(), cancellationToken);
                                if (media != null && File.Exists(media.FilePath))
                                {
                                    var base64Data = await ConvertFileToBase64Async(media.FilePath, cancellationToken);
                                    element["BackgroundImageData"] = base64Data;
                                    element["BackgroundImageType"] = media.MimeType;
                                }
                            }
                            else if (File.Exists(bgImage))
                            {
                                var base64Data = await ConvertFileToBase64Async(bgImage, cancellationToken);
                                element["BackgroundImageData"] = base64Data;
                                element["BackgroundImageType"] = GetMimeTypeFromExtension(bgImage);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to embed media for element {ElementId}, element will use fallback", element.Id);
                }
            }

            // Also check layout-level BackgroundImage
            if (layout.BackgroundImage != null)
            {
                var bgImage = layout.BackgroundImage.ToString();
                if (!string.IsNullOrWhiteSpace(bgImage))
                {
                    try
                    {
                        if (Guid.TryParse(bgImage, out var mediaId))
                        {
                            var media = await mediaService.GetMediaByIdAsync(mediaId.ToString(), cancellationToken);
                            if (media != null && File.Exists(media.FilePath))
                            {
                                var base64Data = await ConvertFileToBase64Async(media.FilePath, cancellationToken);
                                layout["BackgroundImageData"] = base64Data;
                                layout["BackgroundImageType"] = media.MimeType;
                            }
                        }
                        else if (File.Exists(bgImage))
                        {
                            var base64Data = await ConvertFileToBase64Async(bgImage, cancellationToken);
                            layout["BackgroundImageData"] = base64Data;
                            layout["BackgroundImageType"] = GetMimeTypeFromExtension(bgImage);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to embed layout background image");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to embed media files in layout");
        }
    }

    /// <summary>
    /// Convert a file to Base64 string
    /// </summary>
    private async Task<string> ConvertFileToBase64Async(string filePath, CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Get MIME type from file extension
    /// </summary>
    private string GetMimeTypeFromExtension(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream"
        };
    }
}
