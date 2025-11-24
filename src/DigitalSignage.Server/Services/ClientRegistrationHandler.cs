using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using DigitalSignage.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Handles client registration logic
/// </summary>
internal class ClientRegistrationHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ClientRegistrationHandler> _logger;
    private readonly ICommunicationService _communicationService;
    private readonly ILayoutService _layoutService;

    public ClientRegistrationHandler(
        IServiceProvider serviceProvider,
        ILogger<ClientRegistrationHandler> logger,
        ICommunicationService communicationService,
        ILayoutService layoutService)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _communicationService = communicationService ?? throw new ArgumentNullException(nameof(communicationService));
        _layoutService = layoutService ?? throw new ArgumentNullException(nameof(layoutService));
    }

    /// <summary>
    /// Registers or updates a client
    /// </summary>
    public async Task<Result<RaspberryPiClient>> RegisterClientAsync(
        RegisterMessage registerMessage,
        ConcurrentDictionary<string, RaspberryPiClient> clientsCache,
        EventHandler<string>? clientConnectedHandler,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (registerMessage == null)
            {
                return Result<RaspberryPiClient>.Failure("Registration message cannot be null");
            }

            if (string.IsNullOrWhiteSpace(registerMessage.MacAddress))
            {
                return Result<RaspberryPiClient>.Failure("MAC address is required for registration");
            }

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DigitalSignageDbContext>();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();

            // Validate registration token if provided
            string? assignedGroup = null;
            string? assignedLocation = null;

            if (!string.IsNullOrWhiteSpace(registerMessage.RegistrationToken))
            {
                var tokenResult = await ValidateAndConsumeTokenAsync(
                    authService,
                    registerMessage,
                    cancellationToken);

                if (!tokenResult.IsSuccess)
                {
                    return Result<RaspberryPiClient>.Failure(tokenResult.ErrorMessage ?? "Token validation failed");
                }

                assignedGroup = tokenResult.Value.Group;
                assignedLocation = tokenResult.Value.Location;
            }
            else
            {
                _logger.LogWarning("Client registration without token from MAC {MacAddress} - checking if already registered",
                    registerMessage.MacAddress);
            }

            // Check if client already exists by MAC address
            var existingClient = await dbContext.Clients
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.MacAddress == registerMessage.MacAddress, cancellationToken);

            RaspberryPiClient client;

            if (existingClient != null)
            {
                // CRITICAL FIX: Client exists with this MAC - update it
                _logger.LogDebug("Found existing client by MAC {MacAddress}: ID={ClientId}",
                    registerMessage.MacAddress, existingClient.Id);

                client = await UpdateExistingClientAsync(
                    dbContext,
                    existingClient,
                    registerMessage,
                    clientsCache,
                    cancellationToken);
            }
            else
            {
                // CRITICAL FIX: No client with this MAC found
                // But check if the requested Client ID is already taken by another MAC
                if (!string.IsNullOrWhiteSpace(registerMessage.ClientId))
                {
                    var clientWithSameId = await dbContext.Clients
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Id == registerMessage.ClientId, cancellationToken);

                    if (clientWithSameId != null)
                    {
                        _logger.LogWarning("Client ID {ClientId} already exists with different MAC (existing: {ExistingMac}, new: {NewMac}). " +
                            "This is a NEW device with duplicated config. Will generate unique ID.",
                            registerMessage.ClientId, clientWithSameId.MacAddress, registerMessage.MacAddress);

                        // This is a NEW device with a duplicate config file
                        // The CreateNewClient method will handle ID conflict resolution
                    }
                }

                client = CreateNewClient(
                    dbContext,
                    registerMessage,
                    assignedGroup,
                    assignedLocation,
                    cancellationToken);
            }

            // Save to database
            await dbContext.SaveChangesAsync(cancellationToken);

            // Update in-memory cache
            clientsCache[client.Id] = client;

            // Send registration response
            await SendRegistrationResponseAsync(client, cancellationToken);

            // Send assigned layout if exists
            await SendAssignedLayoutIfExistsAsync(client, cancellationToken);

            // Raise ClientConnected event
            clientConnectedHandler?.Invoke(this, client.Id);
            _logger.LogDebug("Raised ClientConnected event for {ClientId}", client.Id);

            return Result<RaspberryPiClient>.Success(client);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Client registration cancelled for MAC {MacAddress}", registerMessage?.MacAddress);
            return Result<RaspberryPiClient>.Failure("Operation was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register client from MAC {MacAddress}", registerMessage?.MacAddress);
            return Result<RaspberryPiClient>.Failure($"Failed to register client: {ex.Message}", ex);
        }
    }

    private async Task<Result<(string? Group, string? Location)>> ValidateAndConsumeTokenAsync(
        IAuthenticationService authService,
        RegisterMessage registerMessage,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Validating registration token for MAC {MacAddress}", registerMessage.MacAddress);

        var validationResult = await authService.ValidateRegistrationTokenAsync(
            registerMessage.RegistrationToken!,
            registerMessage.MacAddress,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            _logger.LogWarning("Registration failed for MAC {MacAddress}: {Error}",
                registerMessage.MacAddress, validationResult.ErrorMessage);
            return Result<(string?, string?)>.Failure(validationResult.ErrorMessage ?? "Invalid registration token");
        }

        var assignedGroup = validationResult.AutoAssignGroup;
        var assignedLocation = validationResult.AutoAssignLocation;

        // Consume the token
        await authService.ConsumeRegistrationTokenAsync(
            registerMessage.RegistrationToken!,
            registerMessage.ClientId,
            cancellationToken);

        _logger.LogInformation("Registration token validated and consumed for MAC {MacAddress}", registerMessage.MacAddress);

        return Result<(string?, string?)>.Success((assignedGroup, assignedLocation));
    }

    private async Task<RaspberryPiClient> UpdateExistingClientAsync(
        DigitalSignageDbContext dbContext,
        RaspberryPiClient existingClient,
        RegisterMessage registerMessage,
        ConcurrentDictionary<string, RaspberryPiClient> clientsCache,
        CancellationToken cancellationToken)
    {
        var client = existingClient;

        // Check if client ID needs to be changed
        if (!string.IsNullOrWhiteSpace(registerMessage.ClientId) && registerMessage.ClientId != client.Id)
        {
            _logger.LogInformation("Client MAC {MacAddress} changing ID from {OldId} to {NewId}",
                client.MacAddress, client.Id, registerMessage.ClientId);

            // CRITICAL FIX: Check if new ID is already taken by a DIFFERENT client
            var clientWithNewId = await dbContext.Clients
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == registerMessage.ClientId, cancellationToken);

            if (clientWithNewId != null && clientWithNewId.MacAddress != client.MacAddress)
            {
                // ID conflict: Another client is using this ID
                _logger.LogWarning("Client ID {NewId} is already taken by another client (MAC: {ExistingMac}). " +
                    "Will update the conflicting client's data instead of creating duplicate.",
                    registerMessage.ClientId, clientWithNewId.MacAddress);

                // Load the conflicting client for tracking
                var conflictingClient = await dbContext.Clients.FindAsync(
                    new object[] { registerMessage.ClientId }, 
                    cancellationToken);

                if (conflictingClient != null)
                {
                    // Update the conflicting client with new MAC and IP
                    conflictingClient.MacAddress = registerMessage.MacAddress;
                    conflictingClient.IpAddress = registerMessage.IpAddress ?? conflictingClient.IpAddress;
                    conflictingClient.Name = registerMessage.DeviceInfo?.Hostname ?? conflictingClient.Name ?? conflictingClient.Id; // Update name from hostname
                    conflictingClient.LastSeen = DateTime.UtcNow;
                    conflictingClient.Status = ClientStatus.Online;
                    conflictingClient.DeviceInfo = MergeDeviceInfo(conflictingClient.DeviceInfo, registerMessage.DeviceInfo);

                    // Mark as modified
                    dbContext.Entry(conflictingClient).State = EntityState.Modified;

                    // Remove old client entry from database
                    var oldClient = await dbContext.Clients.FindAsync(
                        new object[] { existingClient.Id }, 
                        cancellationToken);
                    if (oldClient != null && oldClient.Id != conflictingClient.Id)
                    {
                        _logger.LogInformation("Removing old client entry {OldId} (MAC: {OldMac})",
                            oldClient.Id, oldClient.MacAddress);
                        dbContext.Clients.Remove(oldClient);
                    }

                    // Remove from cache
                    clientsCache.TryRemove(existingClient.Id, out _);

                    _logger.LogInformation("Updated conflicting client {ClientId} with new MAC {MacAddress}",
                        conflictingClient.Id, conflictingClient.MacAddress);

                    return conflictingClient;
                }
            }

            // Load the entity for tracking before removal
            var trackedClient = await dbContext.Clients.FindAsync(new object[] { existingClient.Id }, cancellationToken);
            if (trackedClient != null)
            {
                // Remove old entity from database
                dbContext.Clients.Remove(trackedClient);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            // Remove old ID from cache
            clientsCache.TryRemove(client.Id, out _);

            // Merge DeviceInfo from registration message with existing data
            var mergedDeviceInfo = MergeDeviceInfo(existingClient.DeviceInfo, registerMessage.DeviceInfo);

            // Create new entity with new ID but same data
            client = new RaspberryPiClient
            {
                Id = registerMessage.ClientId,
                Name = mergedDeviceInfo.Hostname ?? registerMessage.ClientId, // Use hostname as name
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
            dbContext.Clients.Attach(client);

            client.IpAddress = registerMessage.IpAddress ?? client.IpAddress;
            client.Name = registerMessage.DeviceInfo?.Hostname ?? client.Name ?? client.Id; // Update name from hostname
            client.LastSeen = DateTime.UtcNow;
            client.Status = ClientStatus.Online;

            // Merge DeviceInfo - update from registration but preserve existing values if not provided
            if (registerMessage.DeviceInfo != null)
            {
                client.DeviceInfo = MergeDeviceInfo(client.DeviceInfo, registerMessage.DeviceInfo);
            }

            // Mark entity as modified
            dbContext.Entry(client).State = EntityState.Modified;

            _logger.LogInformation("Re-registered existing client {ClientId} (MAC: {MacAddress})", client.Id, client.MacAddress);
        }

        return client;
    }

    private RaspberryPiClient CreateNewClient(
        DigitalSignageDbContext dbContext,
        RegisterMessage registerMessage,
        string? assignedGroup,
        string? assignedLocation,
        CancellationToken cancellationToken)
    {
        // Create new client - ensure DeviceInfo is properly populated
        var deviceInfo = registerMessage.DeviceInfo ?? new DeviceInfo();

        // DEBUG: Log incoming DeviceInfo to verify data is being received
        _logger.LogInformation("Creating new client with DeviceInfo from registration:");
        _logger.LogInformation("  Model: {Model}", deviceInfo.Model);
        _logger.LogInformation("  OsVersion: {OsVersion}", deviceInfo.OsVersion);
        _logger.LogInformation("  ClientVersion: {ClientVersion}", deviceInfo.ClientVersion);
        _logger.LogInformation("  Hostname: {Hostname}", deviceInfo.Hostname);
        _logger.LogInformation("  MdnsName: {MdnsName}", deviceInfo.MdnsName);
        _logger.LogInformation("  Resolution: {Width}x{Height}", deviceInfo.ScreenWidth, deviceInfo.ScreenHeight);
        _logger.LogInformation("  Memory: {MemoryUsed}/{MemoryTotal}", deviceInfo.MemoryUsed, deviceInfo.MemoryTotal);
        _logger.LogInformation("  Disk: {DiskUsed}/{DiskTotal}", deviceInfo.DiskUsed, deviceInfo.DiskTotal);
        _logger.LogInformation("  Uptime: {Uptime}s", deviceInfo.Uptime);

        // CRITICAL FIX: Generate unique ID if client ID is already taken
        var clientId = string.IsNullOrWhiteSpace(registerMessage.ClientId) 
            ? Guid.NewGuid().ToString() 
            : registerMessage.ClientId;

        // Check if this ID is already taken
        var existingClientWithId = dbContext.Clients
            .AsNoTracking()
            .FirstOrDefault(c => c.Id == clientId);

        if (existingClientWithId != null)
        {
            _logger.LogWarning("Client ID {ClientId} is already taken (MAC: {ExistingMac}). " +
                "Generating new unique ID for new client (MAC: {NewMac})",
                clientId, existingClientWithId.MacAddress, registerMessage.MacAddress);

            // Generate a unique ID based on hostname + timestamp or just GUID
            if (!string.IsNullOrWhiteSpace(deviceInfo.Hostname))
            {
                // Try hostname-based ID with timestamp suffix
                var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                clientId = $"{deviceInfo.Hostname}-{timestamp}";

                // Double-check uniqueness
                if (dbContext.Clients.AsNoTracking().Any(c => c.Id == clientId))
                {
                    clientId = Guid.NewGuid().ToString();
                }
            }
            else
            {
                clientId = Guid.NewGuid().ToString();
            }

            _logger.LogInformation("Assigned new unique ID: {ClientId} for MAC {MacAddress}",
                clientId, registerMessage.MacAddress);
        }

        var client = new RaspberryPiClient
        {
            Id = clientId,
            Name = deviceInfo.Hostname ?? clientId, // Use hostname as name, fallback to client ID
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

        return client;
    }

    private DeviceInfo MergeDeviceInfo(DeviceInfo? existing, DeviceInfo? incoming)
    {
        var merged = existing ?? new DeviceInfo();

        if (incoming != null)
        {
            // CRITICAL FIX: Always update string fields from incoming data (even if empty)
            // This ensures Model, OsVersion, ClientVersion are always set from registration
            merged.Hostname = incoming.Hostname ?? merged.Hostname;
            merged.MdnsName = incoming.MdnsName ?? merged.MdnsName;  // Also update MdnsName

            // Always update these critical fields from incoming (don't preserve old values)
            merged.Model = !string.IsNullOrWhiteSpace(incoming.Model) ? incoming.Model : merged.Model;
            merged.OsVersion = !string.IsNullOrWhiteSpace(incoming.OsVersion) ? incoming.OsVersion : merged.OsVersion;
            merged.ClientVersion = !string.IsNullOrWhiteSpace(incoming.ClientVersion) ? incoming.ClientVersion : merged.ClientVersion;

            // Numeric fields: only update if incoming value is valid (> 0)
            merged.ScreenWidth = incoming.ScreenWidth > 0 ? incoming.ScreenWidth : merged.ScreenWidth;
            merged.ScreenHeight = incoming.ScreenHeight > 0 ? incoming.ScreenHeight : merged.ScreenHeight;

            // Hardware metrics: always update with current values (even if 0)
            // CPU/Memory/Disk can legitimately be 0 or low values
            merged.CpuTemperature = incoming.CpuTemperature;  // Can be 0 if sensor fails
            merged.CpuUsage = incoming.CpuUsage;  // Can be 0 if idle
            merged.MemoryTotal = incoming.MemoryTotal > 0 ? incoming.MemoryTotal : merged.MemoryTotal;
            merged.MemoryUsed = incoming.MemoryUsed;  // Can be 0
            merged.DiskTotal = incoming.DiskTotal > 0 ? incoming.DiskTotal : merged.DiskTotal;
            merged.DiskUsed = incoming.DiskUsed;  // Can be 0
            merged.Uptime = incoming.Uptime;  // Can be 0 right after boot

            _logger.LogDebug("Merged DeviceInfo: Model={Model}, OS={OsVersion}, Version={ClientVersion}, Resolution={Width}x{Height}",
                merged.Model, merged.OsVersion, merged.ClientVersion, merged.ScreenWidth, merged.ScreenHeight);
        }

        return merged;
    }

    private async Task SendRegistrationResponseAsync(RaspberryPiClient client, CancellationToken cancellationToken)
    {
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
    }

    private async Task SendAssignedLayoutIfExistsAsync(RaspberryPiClient client, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(client.AssignedLayoutId))
        {
            _logger.LogInformation("Client {ClientId} has no assigned layout", client.Id);
            return;
        }

        _logger.LogInformation("Client {ClientId} has assigned layout {LayoutId}, delegating to ClientLayoutDistributor",
            client.Id, client.AssignedLayoutId);

        try
        {
            // CRITICAL FIX: Use ClientLayoutDistributor instead of sending raw layout
            // This ensures media is embedded, data sources are fetched, and templates are processed
            using var scope = _serviceProvider.CreateScope();
            var layoutDistributor = scope.ServiceProvider.GetRequiredService<ClientLayoutDistributor>();

            var result = await layoutDistributor.SendLayoutToClientAsync(client.Id, client.AssignedLayoutId, cancellationToken);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Successfully sent assigned layout {LayoutId} to reconnected client {ClientId}",
                    client.AssignedLayoutId, client.Id);
            }
            else
            {
                _logger.LogWarning("Failed to send layout {LayoutId} to client {ClientId}: {Error}",
                    client.AssignedLayoutId, client.Id, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send assigned layout to client {ClientId} after registration", client.Id);
        }
    }
}
