using System.ComponentModel.DataAnnotations;

namespace DigitalSignage.Data.Entities;

/// <summary>
/// Represents a rule that triggers alerts
/// </summary>
public class AlertRule
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Rule name
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Rule description
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Rule type (DeviceOffline, HighErrorRate, etc.)
    /// </summary>
    public AlertRuleType RuleType { get; set; }

    /// <summary>
    /// Alert severity when triggered
    /// </summary>
    public AlertSeverity Severity { get; set; }

    /// <summary>
    /// Whether the rule is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Rule configuration as JSON (thresholds, conditions, etc.)
    /// </summary>
    public string? Configuration { get; set; }

    /// <summary>
    /// Notification channels (Email, Push, SMS)
    /// </summary>
    public string? NotificationChannels { get; set; }

    /// <summary>
    /// Cooldown period in minutes to prevent duplicate alerts
    /// </summary>
    public int CooldownMinutes { get; set; } = 15;

    /// <summary>
    /// When the rule was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the rule was last modified
    /// </summary>
    public DateTime? ModifiedAt { get; set; }

    /// <summary>
    /// When the rule was last triggered
    /// </summary>
    public DateTime? LastTriggeredAt { get; set; }

    /// <summary>
    /// Number of times the rule has been triggered
    /// </summary>
    public int TriggerCount { get; set; }

    /// <summary>
    /// Collection of alerts triggered by this rule
    /// </summary>
    public ICollection<Alert> Alerts { get; set; } = new List<Alert>();
}

/// <summary>
/// Types of alert rules
/// </summary>
public enum AlertRuleType
{
    DeviceOffline = 0,
    DeviceHighCpu = 1,
    DeviceHighMemory = 2,
    DeviceLowDiskSpace = 3,
    DataSourceError = 4,
    HighErrorRate = 5,
    LayoutUpdateFailed = 6,
    Custom = 99
}
