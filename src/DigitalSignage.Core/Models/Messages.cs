namespace DigitalSignage.Core.Models;

/// <summary>
/// Base message for client-server communication
/// </summary>
public abstract class Message
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string SenderId { get; set; } = string.Empty;
}

/// <summary>
/// Client registration message
/// </summary>
public class RegisterMessage : Message
{
    public RegisterMessage() { Type = "REGISTER"; }

    public string ClientId { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public DeviceInfo DeviceInfo { get; set; } = new();
    public string? RegistrationToken { get; set; } // Token for authenticated registration
}

/// <summary>
/// Registration response message (server -> client)
/// </summary>
public class RegistrationResponseMessage : Message
{
    public RegistrationResponseMessage() { Type = "REGISTRATION_RESPONSE"; }

    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? AssignedClientId { get; set; }
    public string? AssignedGroup { get; set; }
    public string? AssignedLocation { get; set; }
}

/// <summary>
/// Heartbeat/ping message
/// </summary>
public class HeartbeatMessage : Message
{
    public HeartbeatMessage() { Type = "HEARTBEAT"; }

    public string ClientId { get; set; } = string.Empty;
    public ClientStatus Status { get; set; }
    public DeviceInfo? DeviceInfo { get; set; }
}

/// <summary>
/// Display update message (server -> client)
/// </summary>
public class DisplayUpdateMessage : Message
{
    public DisplayUpdateMessage() { Type = "DISPLAY_UPDATE"; }

    public DisplayLayout Layout { get; set; } = new();
    public Dictionary<string, object>? Data { get; set; }
    public bool ForceRefresh { get; set; } = false;
}

/// <summary>
/// Status report message (client -> server)
/// </summary>
public class StatusReportMessage : Message
{
    public StatusReportMessage() { Type = "STATUS_REPORT"; }

    public string ClientId { get; set; } = string.Empty;
    public ClientStatus Status { get; set; }
    public DeviceInfo DeviceInfo { get; set; } = new();
    public string? CurrentLayoutId { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Command message (server -> client)
/// </summary>
public class CommandMessage : Message
{
    public CommandMessage() { Type = "COMMAND"; }

    public string Command { get; set; } = string.Empty;
    public Dictionary<string, object>? Parameters { get; set; }
}

/// <summary>
/// Screenshot request/response
/// </summary>
public class ScreenshotMessage : Message
{
    public ScreenshotMessage() { Type = "SCREENSHOT"; }

    public string ClientId { get; set; } = string.Empty;
    public string? ImageData { get; set; } // Base64 encoded
    public string Format { get; set; } = "png";
}

/// <summary>
/// Log message (client -> server)
/// </summary>
public class LogMessage : Message
{
    public LogMessage() { Type = "LOG"; }

    public string ClientId { get; set; } = string.Empty;
    public LogLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
}

/// <summary>
/// Update configuration message (server -> client)
/// </summary>
public class UpdateConfigMessage : Message
{
    public UpdateConfigMessage() { Type = "UPDATE_CONFIG"; }

    public string ServerHost { get; set; } = string.Empty;
    public int ServerPort { get; set; }
    public bool UseSSL { get; set; }
    public bool VerifySSL { get; set; } = true;
    public bool FullScreen { get; set; } = true;
    public string LogLevel { get; set; } = "INFO";
    public string? RegistrationToken { get; set; }
}

/// <summary>
/// Update configuration response (client -> server)
/// </summary>
public class UpdateConfigResponseMessage : Message
{
    public UpdateConfigResponseMessage() { Type = "UPDATE_CONFIG_RESPONSE"; }

    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Available client commands
/// </summary>
public static class ClientCommands
{
    public const string Restart = "RESTART";
    public const string RestartApp = "RESTART_APP";
    public const string Screenshot = "SCREENSHOT";
    public const string Update = "UPDATE";
    public const string ScreenOn = "SCREEN_ON";
    public const string ScreenOff = "SCREEN_OFF";
    public const string SetVolume = "SET_VOLUME";
    public const string GetLogs = "GET_LOGS";
    public const string ClearCache = "CLEAR_CACHE";
}
