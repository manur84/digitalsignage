using System;

namespace DigitalSignage.Core.DTOs.Api;

/// <summary>
/// Mobile app registration request
/// </summary>
public class RegisterMobileAppRequest
{
    /// <summary>
    /// Device name (e.g., "iPhone 15 Pro")
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Platform (iOS, Android)
    /// </summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// App version
    /// </summary>
    public string AppVersion { get; set; } = string.Empty;

    /// <summary>
    /// Device model
    /// </summary>
    public string? DeviceModel { get; set; }

    /// <summary>
    /// Operating system version
    /// </summary>
    public string? OsVersion { get; set; }
}

/// <summary>
/// Mobile app registration response
/// </summary>
public class RegisterMobileAppResponse
{
    /// <summary>
    /// Indicates if registration request was sent successfully
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Response message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Unique request ID for checking status
    /// </summary>
    public Guid RequestId { get; set; }

    /// <summary>
    /// QR code data (if applicable)
    /// </summary>
    public string? QrCodeData { get; set; }
}

/// <summary>
/// Registration status check request
/// </summary>
public class CheckRegistrationStatusRequest
{
    /// <summary>
    /// Request ID from initial registration
    /// </summary>
    public Guid RequestId { get; set; }
}

/// <summary>
/// Registration status check response
/// </summary>
public class CheckRegistrationStatusResponse
{
    /// <summary>
    /// Registration status (Pending, Approved, Denied)
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Authentication token (only when status is Approved)
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Mobile app ID (only when status is Approved)
    /// </summary>
    public Guid? MobileAppId { get; set; }

    /// <summary>
    /// Status message
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Token validation response
/// </summary>
public class ValidateTokenResponse
{
    /// <summary>
    /// Indicates if token is valid
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Mobile app ID associated with token
    /// </summary>
    public Guid? MobileAppId { get; set; }

    /// <summary>
    /// Device name
    /// </summary>
    public string? DeviceName { get; set; }

    /// <summary>
    /// Token expiration (if applicable)
    /// </summary>
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>
/// Server information response
/// </summary>
public class ServerInfoResponse
{
    /// <summary>
    /// Server version
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Server name
    /// </summary>
    public string ServerName { get; set; } = string.Empty;

    /// <summary>
    /// Server status
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Number of connected clients
    /// </summary>
    public int ConnectedClientCount { get; set; }

    /// <summary>
    /// Number of registered mobile apps
    /// </summary>
    public int RegisteredMobileAppCount { get; set; }

    /// <summary>
    /// Total number of layouts
    /// </summary>
    public int TotalLayoutCount { get; set; }

    /// <summary>
    /// WebSocket server status
    /// </summary>
    public string WebSocketStatus { get; set; } = string.Empty;

    /// <summary>
    /// WebSocket server URL
    /// </summary>
    public string? WebSocketUrl { get; set; }

    /// <summary>
    /// Server uptime in seconds
    /// </summary>
    public long UptimeSeconds { get; set; }
}
