using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.Server.Services.FileStorage;

/// <summary>
/// File-based storage service for device/client management
/// </summary>
public class DeviceFileService : FileStorageService<RaspberryPiClient>
{
    private readonly ConcurrentDictionary<Guid, RaspberryPiClient> _clientsCache = new();
    private readonly ConcurrentDictionary<Guid, DateTime> _lastSeenCache = new();
    private const string DEVICES_FILE = "devices.json";

    public DeviceFileService(ILogger<DeviceFileService> logger) : base(logger)
    {
        // Load devices into cache on startup
        _ = Task.Run(async () => await LoadDevicesToCacheAsync());
    }

    protected override string GetSubDirectory() => "Settings";

    /// <summary>
    /// Load all devices into cache
    /// </summary>
    private async Task LoadDevicesToCacheAsync()
    {
        try
        {
            var devices = await LoadListFromFileAsync(DEVICES_FILE);
            _clientsCache.Clear();
            foreach (var device in devices)
            {
                _clientsCache[device.Id] = device;
                if (device.LastSeen.HasValue)
                {
                    _lastSeenCache[device.Id] = device.LastSeen.Value;
                }
            }
            _logger.LogInformation("Loaded {Count} devices into cache", devices.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load devices into cache");
        }
    }

    /// <summary>
    /// Save cache to file
    /// </summary>
    private async Task SaveCacheToFileAsync()
    {
        try
        {
            var devices = _clientsCache.Values.ToList();
            await SaveListToFileAsync(DEVICES_FILE, devices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save devices cache to file");
        }
    }

    /// <summary>
    /// Register or update a client
    /// </summary>
    public async Task<RaspberryPiClient> RegisterOrUpdateClientAsync(string hostname, string ipAddress, string? macAddress = null)
    {
        try
        {
            // Check if client exists by hostname or MAC address
            var existingClient = _clientsCache.Values.FirstOrDefault(c =>
                string.Equals(c.Name, hostname, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(macAddress) && string.Equals(c.MacAddress, macAddress, StringComparison.OrdinalIgnoreCase)));

            if (existingClient != null)
            {
                // Update existing client
                existingClient.IpAddress = ipAddress;
                existingClient.LastSeen = DateTime.UtcNow;
                existingClient.Status = ClientStatus.Online;
                existingClient.IsOnline = true;

                if (!string.IsNullOrEmpty(macAddress))
                    existingClient.MacAddress = macAddress;

                _lastSeenCache[existingClient.Id] = DateTime.UtcNow;
            }
            else
            {
                // Create new client
                existingClient = new RaspberryPiClient
                {
                    Id = Guid.NewGuid(),
                    Name = hostname,
                    IpAddress = ipAddress,
                    MacAddress = macAddress,
                    Status = ClientStatus.Online,
                    IsOnline = true,
                    RegisteredAt = DateTime.UtcNow,
                    LastSeen = DateTime.UtcNow,
                    DeviceInfo = new DeviceInfo
                    {
                        Hostname = hostname,
                        Resolution = "1920x1080"
                    }
                };

                _clientsCache[existingClient.Id] = existingClient;
                _lastSeenCache[existingClient.Id] = DateTime.UtcNow;
            }

            await SaveCacheToFileAsync();
            _logger.LogInformation("Registered/Updated client {ClientName} ({ClientId})", hostname, existingClient.Id);
            return existingClient;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register/update client {Hostname}", hostname);
            throw;
        }
    }

    /// <summary>
    /// Get all clients
    /// </summary>
    public Task<List<RaspberryPiClient>> GetAllClientsAsync()
    {
        return Task.FromResult(_clientsCache.Values.ToList());
    }

    /// <summary>
    /// Get online clients
    /// </summary>
    public Task<List<RaspberryPiClient>> GetOnlineClientsAsync()
    {
        var onlineClients = _clientsCache.Values
            .Where(c => c.IsOnline && c.Status == ClientStatus.Online)
            .ToList();
        return Task.FromResult(onlineClients);
    }

    /// <summary>
    /// Get client by ID
    /// </summary>
    public Task<RaspberryPiClient?> GetClientByIdAsync(Guid clientId)
    {
        _clientsCache.TryGetValue(clientId, out var client);
        return Task.FromResult(client);
    }

    /// <summary>
    /// Get client by hostname
    /// </summary>
    public Task<RaspberryPiClient?> GetClientByHostnameAsync(string hostname)
    {
        var client = _clientsCache.Values
            .FirstOrDefault(c => string.Equals(c.Name, hostname, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(client);
    }

    /// <summary>
    /// Update client status
    /// </summary>
    public async Task<bool> UpdateClientStatusAsync(Guid clientId, ClientStatus status, bool isOnline)
    {
        try
        {
            if (_clientsCache.TryGetValue(clientId, out var client))
            {
                client.Status = status;
                client.IsOnline = isOnline;

                if (isOnline)
                {
                    client.LastSeen = DateTime.UtcNow;
                    _lastSeenCache[clientId] = DateTime.UtcNow;
                }

                await SaveCacheToFileAsync();
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update client status for {ClientId}", clientId);
            return false;
        }
    }

    /// <summary>
    /// Update client device info
    /// </summary>
    public async Task<bool> UpdateClientDeviceInfoAsync(Guid clientId, DeviceInfo deviceInfo)
    {
        try
        {
            if (_clientsCache.TryGetValue(clientId, out var client))
            {
                client.DeviceInfo = deviceInfo;
                client.LastSeen = DateTime.UtcNow;
                _lastSeenCache[clientId] = DateTime.UtcNow;

                await SaveCacheToFileAsync();
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update device info for {ClientId}", clientId);
            return false;
        }
    }

    /// <summary>
    /// Update client heartbeat
    /// </summary>
    public async Task UpdateHeartbeatAsync(Guid clientId)
    {
        try
        {
            if (_clientsCache.TryGetValue(clientId, out var client))
            {
                client.LastSeen = DateTime.UtcNow;
                client.IsOnline = true;
                client.Status = ClientStatus.Online;
                _lastSeenCache[clientId] = DateTime.UtcNow;

                // Periodically save to file
                if (DateTime.UtcNow.Subtract(client.LastSeen ?? DateTime.UtcNow).TotalMinutes > 5)
                {
                    await SaveCacheToFileAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update heartbeat for {ClientId}", clientId);
        }
    }

    /// <summary>
    /// Mark clients as offline if they haven't been seen recently
    /// </summary>
    public async Task MarkInactiveClientsOfflineAsync(TimeSpan timeout)
    {
        try
        {
            var cutoff = DateTime.UtcNow.Subtract(timeout);
            var hasChanges = false;

            foreach (var kvp in _lastSeenCache)
            {
                if (kvp.Value < cutoff && _clientsCache.TryGetValue(kvp.Key, out var client))
                {
                    if (client.IsOnline)
                    {
                        client.IsOnline = false;
                        client.Status = ClientStatus.Offline;
                        hasChanges = true;
                        _logger.LogInformation("Marked client {ClientName} as offline (last seen: {LastSeen})",
                            client.Name, kvp.Value);
                    }
                }
            }

            if (hasChanges)
            {
                await SaveCacheToFileAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark inactive clients as offline");
        }
    }

    /// <summary>
    /// Delete a client
    /// </summary>
    public async Task<bool> DeleteClientAsync(Guid clientId)
    {
        try
        {
            if (_clientsCache.TryRemove(clientId, out var client))
            {
                _lastSeenCache.TryRemove(clientId, out _);
                await SaveCacheToFileAsync();
                _logger.LogInformation("Deleted client {ClientName} ({ClientId})", client.Name, clientId);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete client {ClientId}", clientId);
            return false;
        }
    }

    /// <summary>
    /// Update client configuration
    /// </summary>
    public async Task<bool> UpdateClientConfigurationAsync(Guid clientId, string? location, string? group, Guid? assignedLayoutId)
    {
        try
        {
            if (_clientsCache.TryGetValue(clientId, out var client))
            {
                client.Location = location;
                client.Group = group;
                client.AssignedLayoutId = assignedLayoutId;

                await SaveCacheToFileAsync();
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update configuration for client {ClientId}", clientId);
            return false;
        }
    }

    /// <summary>
    /// Get clients by group
    /// </summary>
    public Task<List<RaspberryPiClient>> GetClientsByGroupAsync(string group)
    {
        var clients = _clientsCache.Values
            .Where(c => string.Equals(c.Group, group, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult(clients);
    }

    /// <summary>
    /// Get client statistics
    /// </summary>
    public Task<Dictionary<string, object>> GetClientStatisticsAsync()
    {
        var stats = new Dictionary<string, object>
        {
            ["TotalClients"] = _clientsCache.Count,
            ["OnlineClients"] = _clientsCache.Values.Count(c => c.IsOnline),
            ["OfflineClients"] = _clientsCache.Values.Count(c => !c.IsOnline),
            ["Groups"] = _clientsCache.Values
                .Where(c => !string.IsNullOrEmpty(c.Group))
                .Select(c => c.Group)
                .Distinct()
                .Count()
        };

        return Task.FromResult(stats);
    }
}