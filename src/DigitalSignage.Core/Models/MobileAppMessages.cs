namespace DigitalSignage.Core.Models;

// ============================================
// MOBILE APP MESSAGE TYPES
// ============================================

/// <summary>
/// Message type constants for mobile app communication
/// </summary>
public static class MobileAppMessageTypes
{
    // Mobile App → Server
    public const string AppRegister = "APP_REGISTER";
    public const string AppHeartbeat = "APP_HEARTBEAT";
    public const string RequestClientList = "REQUEST_CLIENT_LIST";
    public const string SendCommand = "SEND_COMMAND";
    public const string AssignLayout = "ASSIGN_LAYOUT";
    public const string RequestScreenshot = "REQUEST_SCREENSHOT";
    public const string RequestLayoutList = "REQUEST_LAYOUT_LIST";

    // Server → Mobile App
    public const string AppAuthorizationRequired = "APP_AUTHORIZATION_REQUIRED";
    public const string AppAuthorized = "APP_AUTHORIZED";
    public const string AppRejected = "APP_REJECTED";
    public const string ClientListUpdate = "CLIENT_LIST_UPDATE";
    public const string ClientStatusChanged = "CLIENT_STATUS_CHANGED";
    public const string ScreenshotResponse = "SCREENSHOT_RESPONSE";
    public const string LayoutListResponse = "LAYOUT_LIST_RESPONSE";
    public const string CommandResult = "COMMAND_RESULT";
}

// ============================================
// MOBILE APP → SERVER MESSAGES
// ============================================

/// <summary>
/// Mobile app registration request
/// </summary>
public class AppRegisterMessage : Message
{
    public AppRegisterMessage()
    {
        Type = MobileAppMessageTypes.AppRegister;
    }

    public string DeviceName { get; set; } = string.Empty;
    public string DeviceIdentifier { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
}

/// <summary>
/// Mobile app heartbeat to maintain connection
/// </summary>
public class AppHeartbeatMessage : Message
{
    public AppHeartbeatMessage()
    {
        Type = MobileAppMessageTypes.AppHeartbeat;
    }

    public Guid AppId { get; set; }
    public string Token { get; set; } = string.Empty;
}

/// <summary>
/// Request list of connected clients
/// </summary>
public class RequestClientListMessage : Message
{
    public RequestClientListMessage()
    {
        Type = MobileAppMessageTypes.RequestClientList;
    }

    public string? Filter { get; set; } // "online", "offline", "all", null = all
}

/// <summary>
/// Send command to a specific device
/// </summary>
public class SendCommandMessage : Message
{
    public SendCommandMessage()
    {
        Type = MobileAppMessageTypes.SendCommand;
    }

    public Guid TargetDeviceId { get; set; }
    public string Command { get; set; } = string.Empty;
    public Dictionary<string, object>? Parameters { get; set; }
}

/// <summary>
/// Assign layout to device
/// </summary>
public class AssignLayoutMessage : Message
{
    public AssignLayoutMessage()
    {
        Type = MobileAppMessageTypes.AssignLayout;
    }

    public Guid DeviceId { get; set; }
    public string LayoutId { get; set; } = string.Empty;
}

/// <summary>
/// Request screenshot from device
/// </summary>
public class RequestScreenshotMessage : Message
{
    public RequestScreenshotMessage()
    {
        Type = MobileAppMessageTypes.RequestScreenshot;
    }

    public Guid DeviceId { get; set; }
}

/// <summary>
/// Request list of available layouts
/// </summary>
public class RequestLayoutListMessage : Message
{
    public RequestLayoutListMessage()
    {
        Type = MobileAppMessageTypes.RequestLayoutList;
    }

    public string? Category { get; set; }
}

// ============================================
// SERVER → MOBILE APP MESSAGES
// ============================================

/// <summary>
/// Server response: authorization required
/// </summary>
public class AppAuthorizationRequiredMessage : Message
{
    public AppAuthorizationRequiredMessage()
    {
        Type = MobileAppMessageTypes.AppAuthorizationRequired;
    }

    public Guid AppId { get; set; }
    public string Status { get; set; } = "pending"; // pending, approved, rejected
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Server response: app authorized
/// </summary>
public class AppAuthorizedMessage : Message
{
    public AppAuthorizedMessage()
    {
        Type = MobileAppMessageTypes.AppAuthorized;
    }

    public Guid MobileAppId { get; set; }
    public string Token { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = new();
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// Server response: app rejected
/// </summary>
public class AppRejectedMessage : Message
{
    public AppRejectedMessage()
    {
        Type = MobileAppMessageTypes.AppRejected;
    }

    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Server sends client list
/// </summary>
public class ClientListUpdateMessage : Message
{
    public ClientListUpdateMessage()
    {
        Type = MobileAppMessageTypes.ClientListUpdate;
    }

    public List<ClientInfo> Clients { get; set; } = new();
}

/// <summary>
/// Client information for mobile app
/// </summary>
public class ClientInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public DeviceStatus Status { get; set; } = DeviceStatus.Offline;
    public string? Resolution { get; set; }
    public DeviceInfoData? DeviceInfo { get; set; }
    public DateTime LastSeen { get; set; }
    public string? AssignedLayoutId { get; set; }
    public string? AssignedLayoutName { get; set; }
    public string? Location { get; set; }
    public string? Group { get; set; }
}

/// <summary>
/// Simplified device info for mobile app
/// </summary>
public class DeviceInfoData
{
    public double? CpuUsage { get; set; }
    public double? MemoryUsage { get; set; }
    public double? Temperature { get; set; }
    public double? DiskUsage { get; set; }
    public string? OsVersion { get; set; }
    public string? AppVersion { get; set; }
}

/// <summary>
/// Server notifies app of client status change
/// </summary>
public class ClientStatusChangedMessage : Message
{
    public ClientStatusChangedMessage()
    {
        Type = MobileAppMessageTypes.ClientStatusChanged;
    }

    public Guid DeviceId { get; set; }
    public DeviceStatus Status { get; set; } = DeviceStatus.Offline;
    public string? Reason { get; set; }
    public new DateTime Timestamp { get; set; }
}

/// <summary>
/// Screenshot response
/// </summary>
public class ScreenshotResponseMessage : Message
{
    public ScreenshotResponseMessage()
    {
        Type = MobileAppMessageTypes.ScreenshotResponse;
    }

    public Guid DeviceId { get; set; }
    public string ImageData { get; set; } = string.Empty; // Base64 PNG
    public DateTime CapturedAt { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Layout list response
/// </summary>
public class LayoutListResponseMessage : Message
{
    public LayoutListResponseMessage()
    {
        Type = MobileAppMessageTypes.LayoutListResponse;
    }

    public List<LayoutInfo> Layouts { get; set; } = new();
}

/// <summary>
/// Simplified layout info for mobile app
/// </summary>
public class LayoutInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? ThumbnailData { get; set; } // Base64 thumbnail
    public DateTime Created { get; set; }
    public DateTime Modified { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

/// <summary>
/// Command execution result
/// </summary>
public class CommandResultMessage : Message
{
    public CommandResultMessage()
    {
        Type = MobileAppMessageTypes.CommandResult;
    }

    public Guid DeviceId { get; set; }
    public string Command { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object>? Result { get; set; }
}
