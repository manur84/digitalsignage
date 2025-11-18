namespace DigitalSignage.Data.Entities;

/// <summary>
/// Schedule for automatic layout switching based on time and days
/// </summary>
public class LayoutSchedule
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>
    /// The layout ID to display
    /// </summary>
    public string LayoutId { get; set; } = string.Empty;

    /// <summary>
    /// Optional: Target specific client or group (null = all clients)
    /// </summary>
    public string? ClientId { get; set; }
    public string? ClientGroup { get; set; }

    /// <summary>
    /// Start time in HH:mm format (24-hour)
    /// </summary>
    public string StartTime { get; set; } = "00:00";

    /// <summary>
    /// End time in HH:mm format (24-hour)
    /// </summary>
    public string EndTime { get; set; } = "23:59";

    /// <summary>
    /// Days of week as comma-separated string: "Monday,Tuesday,Wednesday"
    /// Or "*" for all days
    /// </summary>
    public string DaysOfWeek { get; set; } = "*";

    /// <summary>
    /// Priority for overlapping schedules (higher = more important)
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Whether this schedule is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional: Start date for schedule validity
    /// </summary>
    public DateTime? ValidFrom { get; set; }

    /// <summary>
    /// Optional: End date for schedule validity
    /// </summary>
    public DateTime? ValidUntil { get; set; }

    /// <summary>
    /// Created timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last modified timestamp
    /// </summary>
    public DateTime? ModifiedAt { get; set; }

    /// <summary>
    /// Last time this schedule was used
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Check if schedule is currently valid for given date/time
    /// </summary>
    public bool IsValidAt(DateTime dateTime)
    {
        if (!IsActive)
            return false;

        // Check validity period
        if (ValidFrom.HasValue && dateTime < ValidFrom.Value)
            return false;

        if (ValidUntil.HasValue && dateTime > ValidUntil.Value)
            return false;

        // Check day of week
        if (DaysOfWeek != "*")
        {
            var dayName = dateTime.DayOfWeek.ToString();
            if (!DaysOfWeek.Contains(dayName, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // Check time range
        var currentTime = dateTime.TimeOfDay;
        var start = TimeSpan.Parse(StartTime);
        var end = TimeSpan.Parse(EndTime);

        if (start <= end)
        {
            // Same day: 09:00 - 17:00
            return currentTime >= start && currentTime <= end;
        }
        else
        {
            // Crosses midnight: 22:00 - 02:00
            return currentTime >= start || currentTime <= end;
        }
    }
}
