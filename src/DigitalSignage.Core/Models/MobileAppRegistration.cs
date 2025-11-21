namespace DigitalSignage.Core.Models;

/// <summary>
/// Represents a mobile app registration awaiting or granted authorization
/// </summary>
public class MobileAppRegistration
{
    /// <summary>
    /// Unique identifier for this registration
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Device name from mobile app (e.g., "iPhone 15 Pro")
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Device identifier (OS-generated unique ID)
    /// </summary>
    public string DeviceIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// App version (e.g., "1.0.5")
    /// </summary>
    public string AppVersion { get; set; } = string.Empty;

    /// <summary>
    /// Platform: iOS, Android, etc.
    /// </summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// Registration status: Pending, Approved, Rejected, Revoked
    /// </summary>
    public AppRegistrationStatus Status { get; set; } = AppRegistrationStatus.Pending;

    /// <summary>
    /// Authentication token (null until approved)
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Granted permissions (comma-separated: "view,control,manage")
    /// </summary>
    public string Permissions { get; set; } = string.Empty;

    /// <summary>
    /// When the app first registered
    /// </summary>
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the registration was approved/rejected
    /// </summary>
    public DateTime? AuthorizedAt { get; set; }

    /// <summary>
    /// Admin who authorized the app
    /// </summary>
    public string? AuthorizedBy { get; set; }

    /// <summary>
    /// Last time the app connected
    /// </summary>
    public DateTime? LastSeenAt { get; set; }

    /// <summary>
    /// Optional notes from admin
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Status of a mobile app registration
/// </summary>
public enum AppRegistrationStatus
{
    /// <summary>
    /// Awaiting administrator approval
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Approved and authorized to connect
    /// </summary>
    Approved = 1,

    /// <summary>
    /// Rejected by administrator
    /// </summary>
    Rejected = 2,

    /// <summary>
    /// Previously approved but access revoked
    /// </summary>
    Revoked = 3
}

/// <summary>
/// Permissions that can be granted to mobile apps
/// </summary>
[Flags]
public enum AppPermission
{
    /// <summary>
    /// No permissions
    /// </summary>
    None = 0,

    /// <summary>
    /// View-only access (device list, screenshots)
    /// </summary>
    View = 1,

    /// <summary>
    /// Execute commands (restart, screenshot, volume)
    /// </summary>
    Control = 2,

    /// <summary>
    /// Assign layouts and schedules
    /// </summary>
    Manage = 4,

    /// <summary>
    /// All permissions
    /// </summary>
    All = View | Control | Manage
}
