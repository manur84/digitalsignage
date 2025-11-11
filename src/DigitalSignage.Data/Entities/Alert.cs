using System.ComponentModel.DataAnnotations;

namespace DigitalSignage.Data.Entities;

/// <summary>
/// Represents an alert triggered by the system
/// </summary>
public class Alert
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Alert rule that triggered this alert
    /// </summary>
    public int AlertRuleId { get; set; }
    public AlertRule? AlertRule { get; set; }

    /// <summary>
    /// Alert severity level
    /// </summary>
    public AlertSeverity Severity { get; set; }

    /// <summary>
    /// Alert title
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Alert message
    /// </summary>
    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Entity type related to alert (Client, DataSource, etc.)
    /// </summary>
    [MaxLength(100)]
    public string? EntityType { get; set; }

    /// <summary>
    /// Entity ID related to alert
    /// </summary>
    public string? EntityId { get; set; }

    /// <summary>
    /// When the alert was triggered
    /// </summary>
    public DateTime TriggeredAt { get; set; }

    /// <summary>
    /// Whether the alert has been acknowledged
    /// </summary>
    public bool IsAcknowledged { get; set; }

    /// <summary>
    /// When the alert was acknowledged
    /// </summary>
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>
    /// User who acknowledged the alert
    /// </summary>
    [MaxLength(100)]
    public string? AcknowledgedBy { get; set; }

    /// <summary>
    /// Whether the alert has been resolved
    /// </summary>
    public bool IsResolved { get; set; }

    /// <summary>
    /// When the alert was resolved
    /// </summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>
    /// Additional metadata as JSON
    /// </summary>
    public string? Metadata { get; set; }
}

/// <summary>
/// Alert severity levels
/// </summary>
public enum AlertSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2,
    Critical = 3
}
