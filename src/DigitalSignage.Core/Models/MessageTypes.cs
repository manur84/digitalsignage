namespace DigitalSignage.Core.Models;

/// <summary>
/// Constants for WebSocket message types
/// Eliminates magic strings throughout the codebase
/// </summary>
public static class MessageTypes
{
    // ============================================
    // CLIENT → SERVER MESSAGES
    // ============================================

    /// <summary>
    /// Client registration request with device information
    /// </summary>
    public const string Register = "REGISTER";

    /// <summary>
    /// Periodic heartbeat to indicate client is alive
    /// </summary>
    public const string Heartbeat = "HEARTBEAT";

    /// <summary>
    /// Client status report (online, offline, error state)
    /// </summary>
    public const string StatusReport = "STATUS_REPORT";

    /// <summary>
    /// Log message from client to server
    /// </summary>
    public const string Log = "LOG";

    /// <summary>
    /// Screenshot data from client
    /// </summary>
    public const string Screenshot = "SCREENSHOT";

    /// <summary>
    /// Response to configuration update command
    /// </summary>
    public const string UpdateConfigResponse = "UPDATE_CONFIG_RESPONSE";

    // ============================================
    // SERVER → CLIENT MESSAGES
    // ============================================

    /// <summary>
    /// Server response to registration request
    /// </summary>
    public const string RegistrationResponse = "REGISTRATION_RESPONSE";

    /// <summary>
    /// Display/layout update notification
    /// </summary>
    public const string DisplayUpdate = "DISPLAY_UPDATE";

    /// <summary>
    /// Command to execute on client (restart, screenshot, etc.)
    /// </summary>
    public const string Command = "COMMAND";

    /// <summary>
    /// Configuration update command
    /// </summary>
    public const string UpdateConfig = "UPDATE_CONFIG";

    /// <summary>
    /// Layout assignment with linked data sources
    /// </summary>
    public const string LayoutAssigned = "LAYOUT_ASSIGNED";

    /// <summary>
    /// Data source update pushed to clients
    /// </summary>
    public const string DataUpdate = "DATA_UPDATE";

    // ============================================
    // MOBILE APP MESSAGES
    // ============================================

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
