using System;

namespace DigitalSignage.Core.DTOs.Api;

/// <summary>
/// Device command request
/// </summary>
public class DeviceCommandRequest
{
    /// <summary>
    /// Command to execute (Restart, Screenshot, VolumeUp, VolumeDown, ScreenOn, ScreenOff)
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Optional command parameters
    /// </summary>
    public Dictionary<string, string>? Parameters { get; set; }
}

/// <summary>
/// Device command response
/// </summary>
public class DeviceCommandResponse
{
    /// <summary>
    /// Indicates if command was successfully sent
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Response message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Command execution timestamp
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Optional result data
    /// </summary>
    public object? Data { get; set; }
}

/// <summary>
/// Screenshot request
/// </summary>
public class ScreenshotRequest
{
    /// <summary>
    /// Screenshot quality (1-100, default: 85)
    /// </summary>
    public int Quality { get; set; } = 85;

    /// <summary>
    /// Timeout in seconds (default: 30)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Screenshot response
/// </summary>
public class ScreenshotResponse
{
    /// <summary>
    /// Screenshot image data (base64 encoded PNG)
    /// </summary>
    public string ImageBase64 { get; set; } = string.Empty;

    /// <summary>
    /// Screenshot capture timestamp
    /// </summary>
    public DateTime CapturedAt { get; set; }

    /// <summary>
    /// Image width in pixels
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Image height in pixels
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSizeBytes { get; set; }
}
