using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Manages mobile app WebSocket connections and their associated state
/// Extracted from WebSocketCommunicationService for better separation of concerns
/// </summary>
public class MobileAppConnectionManager
{
    private readonly ILogger<MobileAppConnectionManager> _logger;

    // Mobile App Connections (separate from Pi clients)
    private readonly ConcurrentDictionary<string, SslWebSocketConnection> _mobileAppConnections = new();
    private readonly ConcurrentDictionary<string, Guid> _mobileAppIds = new(); // Maps connection ID to app ID
    private readonly ConcurrentDictionary<string, string> _mobileAppTokens = new(); // Maps connection ID to token

    public MobileAppConnectionManager(ILogger<MobileAppConnectionManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Track a new mobile app connection (before registration is complete)
    /// </summary>
    public void TrackConnection(string connectionId, SslWebSocketConnection connection)
    {
        _mobileAppConnections[connectionId] = connection;
        _logger.LogDebug("Tracking mobile app connection {ConnectionId}", connectionId);
    }

    /// <summary>
    /// Register a new mobile app connection with app ID
    /// </summary>
    public void RegisterConnection(string connectionId, SslWebSocketConnection connection, Guid appId)
    {
        _mobileAppConnections[connectionId] = connection;
        _mobileAppIds[connectionId] = appId;
        _logger.LogDebug("Registered mobile app connection {ConnectionId} for app {AppId}", connectionId, appId);
    }

    /// <summary>
    /// Update the app ID for an existing connection
    /// </summary>
    public void UpdateAppId(string connectionId, Guid appId)
    {
        _mobileAppIds[connectionId] = appId;
        _logger.LogDebug("Updated app ID for connection {ConnectionId} to {AppId}", connectionId, appId);
    }

    /// <summary>
    /// Set the authentication token for a mobile app connection
    /// </summary>
    public void SetToken(string connectionId, string token)
    {
        _mobileAppTokens[connectionId] = token;
        _logger.LogDebug("Set token for mobile app connection {ConnectionId}", connectionId);
    }

    /// <summary>
    /// Get the token for a mobile app connection
    /// </summary>
    public string? GetToken(string connectionId)
    {
        return _mobileAppTokens.TryGetValue(connectionId, out var token) ? token : null;
    }

    /// <summary>
    /// Get the app ID for a mobile app connection
    /// </summary>
    public Guid? GetAppId(string connectionId)
    {
        return _mobileAppIds.TryGetValue(connectionId, out var appId) ? appId : null;
    }

    /// <summary>
    /// Get the connection for a mobile app
    /// </summary>
    public SslWebSocketConnection? GetConnection(string connectionId)
    {
        return _mobileAppConnections.TryGetValue(connectionId, out var connection) ? connection : null;
    }

    /// <summary>
    /// Get all mobile app connections
    /// </summary>
    public ConcurrentDictionary<string, SslWebSocketConnection> GetAllConnections()
    {
        return _mobileAppConnections;
    }

    /// <summary>
    /// Remove a mobile app connection
    /// </summary>
    public bool RemoveConnection(string connectionId)
    {
        var removed = _mobileAppConnections.TryRemove(connectionId, out _);
        _mobileAppIds.TryRemove(connectionId, out _);
        _mobileAppTokens.TryRemove(connectionId, out _);

        if (removed)
        {
            _logger.LogInformation("Mobile app {ConnectionId} disconnected and removed", connectionId);
        }

        return removed;
    }

    /// <summary>
    /// Send a message to a specific mobile app connection
    /// </summary>
    public async Task SendMessageAsync(string connectionId, Message message, CancellationToken cancellationToken = default)
    {
        var connection = GetConnection(connectionId);
        if (connection == null)
        {
            _logger.LogWarning("Cannot send message - mobile app connection {ConnectionId} not found", connectionId);
            throw new InvalidOperationException($"Mobile app connection {connectionId} not found");
        }

        try
        {
            var settings = new Newtonsoft.Json.JsonSerializerSettings
            {
                TypeNameHandling = Newtonsoft.Json.TypeNameHandling.Auto,
                NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore
            };
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(message, settings);
            await connection.SendTextAsync(json, cancellationToken);

            _logger.LogDebug("Sent message {MessageType} to mobile app {ConnectionId}", message.Type, connectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to mobile app {ConnectionId}", connectionId);
            throw;
        }
    }

    /// <summary>
    /// Send an error message to a mobile app connection
    /// </summary>
    public async Task SendErrorAsync(string connectionId, string errorMessage, CancellationToken cancellationToken = default)
    {
        try
        {
            var errorMsg = new CommandResultMessage
            {
                Success = false,
                ErrorMessage = errorMessage
            };

            await SendMessageAsync(connectionId, errorMsg, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send error message to mobile app {ConnectionId}", connectionId);
        }
    }

    /// <summary>
    /// Notify all connected mobile apps of a client status change
    /// </summary>
    public async Task NotifyAllClientsStatusChangedAsync(Guid deviceId, DeviceStatus status, CancellationToken cancellationToken = default)
    {
        var statusMessage = new ClientStatusChangedMessage
        {
            DeviceId = deviceId,
            Status = status,
            Timestamp = DateTime.UtcNow
        };

        var tasks = _mobileAppConnections.Values.Select(async connection =>
        {
            try
            {
                var settings = new Newtonsoft.Json.JsonSerializerSettings
                {
                    TypeNameHandling = Newtonsoft.Json.TypeNameHandling.Auto,
                    NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore
                };
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(statusMessage, settings);
                await connection.SendTextAsync(json, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error notifying mobile app of client status change");
            }
        });

        try
        {
            await Task.WhenAll(tasks);
            _logger.LogDebug("Notified {Count} mobile apps of client status change", _mobileAppConnections.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error notifying mobile apps of client status change");
        }
    }

    /// <summary>
    /// Send approval notification to a specific mobile app
    /// </summary>
    public async Task SendApprovalNotificationAsync(Guid mobileAppId, string token, AppPermission permissions, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Sending approval notification to mobile app {AppId}", mobileAppId);

            // Find connection by MobileAppId
            var connectionId = _mobileAppIds
                .Where(kvp => kvp.Value == mobileAppId)
                .Select(kvp => kvp.Key)
                .FirstOrDefault();

            if (connectionId == null)
            {
                _logger.LogWarning("Mobile app {AppId} not currently connected - cannot send approval notification", mobileAppId);
                return;
            }

            var connection = GetConnection(connectionId);
            if (connection == null)
            {
                _logger.LogWarning("Connection for mobile app {AppId} not found in connections dictionary", mobileAppId);
                return;
            }

            // Store token in connection mapping for future authorization checks
            SetToken(connectionId, token);

            // Send AppAuthorized message
            var approvalMessage = new AppAuthorizedMessage
            {
                Token = token,
                Permissions = ConvertPermissionToList(permissions),
                ExpiresAt = DateTime.UtcNow.AddYears(1) // Token valid for 1 year
            };

            await SendMessageAsync(connectionId, approvalMessage, cancellationToken);

            _logger.LogInformation("✓ Approval notification sent successfully to mobile app {AppId} via WebSocket", mobileAppId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending approval notification to mobile app {AppId}", mobileAppId);
            throw;
        }
    }

    /// <summary>
    /// Convert AppPermission flags to list of permission strings
    /// </summary>
    private static List<string> ConvertPermissionToList(AppPermission permissions)
    {
        var permissionList = new List<string>();

        if (permissions.HasFlag(AppPermission.View))
            permissionList.Add("view");

        if (permissions.HasFlag(AppPermission.Control))
            permissionList.Add("control");

        if (permissions.HasFlag(AppPermission.Manage))
            permissionList.Add("manage");

        return permissionList;
    }
}
