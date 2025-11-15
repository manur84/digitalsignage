using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.Server.Services.FileStorage;

/// <summary>
/// File-based storage service for layout scheduling
/// </summary>
public class ScheduleFileService : FileStorageService<LayoutScheduleInfo>
{
    private readonly ConcurrentDictionary<Guid, LayoutScheduleInfo> _schedulesCache = new();
    private const string SCHEDULES_FILE = "schedules.json";

    public ScheduleFileService(ILogger<ScheduleFileService> logger) : base(logger)
    {
        _ = Task.Run(async () => await LoadSchedulesAsync());
    }

    protected override string GetSubDirectory() => "Settings";

    /// <summary>
    /// Load schedules into cache
    /// </summary>
    private async Task LoadSchedulesAsync()
    {
        try
        {
            var schedules = await LoadListFromFileAsync(SCHEDULES_FILE);
            _schedulesCache.Clear();

            foreach (var schedule in schedules)
            {
                _schedulesCache[schedule.Id] = schedule;
            }

            _logger.LogInformation("Loaded {Count} schedules into cache", schedules.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load schedules");
        }
    }

    /// <summary>
    /// Save schedules to file
    /// </summary>
    private async Task SaveSchedulesAsync()
    {
        try
        {
            var schedules = _schedulesCache.Values.ToList();
            await SaveListToFileAsync(SCHEDULES_FILE, schedules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save schedules");
        }
    }

    /// <summary>
    /// Create a new schedule
    /// </summary>
    public async Task<LayoutScheduleInfo> CreateScheduleAsync(LayoutScheduleInfo schedule)
    {
        try
        {
            if (schedule.Id == Guid.Empty)
            {
                schedule.Id = Guid.NewGuid();
            }

            schedule.CreatedAt = DateTime.UtcNow;
            schedule.ModifiedAt = DateTime.UtcNow;

            _schedulesCache[schedule.Id] = schedule;
            await SaveSchedulesAsync();

            _logger.LogInformation("Created schedule {ScheduleName} ({ScheduleId})", schedule.Name, schedule.Id);
            return schedule;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create schedule {ScheduleName}", schedule.Name);
            throw;
        }
    }

    /// <summary>
    /// Get all schedules
    /// </summary>
    public Task<List<LayoutScheduleInfo>> GetAllSchedulesAsync()
    {
        return Task.FromResult(_schedulesCache.Values
            .OrderBy(s => s.Priority)
            .ThenBy(s => s.Name)
            .ToList());
    }

    /// <summary>
    /// Get active schedules
    /// </summary>
    public Task<List<LayoutScheduleInfo>> GetActiveSchedulesAsync()
    {
        var now = DateTime.UtcNow;
        var activeSchedules = _schedulesCache.Values
            .Where(s => s.IsActive &&
                       (!s.ValidFrom.HasValue || s.ValidFrom.Value <= now) &&
                       (!s.ValidUntil.HasValue || s.ValidUntil.Value >= now))
            .OrderBy(s => s.Priority)
            .ToList();

        return Task.FromResult(activeSchedules);
    }

    /// <summary>
    /// Get schedule by ID
    /// </summary>
    public Task<LayoutScheduleInfo?> GetScheduleByIdAsync(Guid scheduleId)
    {
        _schedulesCache.TryGetValue(scheduleId, out var schedule);
        return Task.FromResult(schedule);
    }

    /// <summary>
    /// Get schedules for a specific layout
    /// </summary>
    public Task<List<LayoutScheduleInfo>> GetSchedulesForLayoutAsync(Guid layoutId)
    {
        var schedules = _schedulesCache.Values
            .Where(s => s.LayoutId == layoutId)
            .OrderBy(s => s.Priority)
            .ToList();

        return Task.FromResult(schedules);
    }

    /// <summary>
    /// Get schedules for a specific client
    /// </summary>
    public Task<List<LayoutScheduleInfo>> GetSchedulesForClientAsync(Guid clientId)
    {
        var schedules = _schedulesCache.Values
            .Where(s => s.ClientId == clientId)
            .OrderBy(s => s.Priority)
            .ToList();

        return Task.FromResult(schedules);
    }

    /// <summary>
    /// Get schedules for a specific group
    /// </summary>
    public Task<List<LayoutScheduleInfo>> GetSchedulesForGroupAsync(string group)
    {
        var schedules = _schedulesCache.Values
            .Where(s => string.Equals(s.ClientGroup, group, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Priority)
            .ToList();

        return Task.FromResult(schedules);
    }

    /// <summary>
    /// Update a schedule
    /// </summary>
    public async Task<LayoutScheduleInfo?> UpdateScheduleAsync(Guid scheduleId, LayoutScheduleInfo updatedSchedule)
    {
        try
        {
            if (_schedulesCache.TryGetValue(scheduleId, out var existingSchedule))
            {
                // Preserve ID and creation date
                updatedSchedule.Id = scheduleId;
                updatedSchedule.CreatedAt = existingSchedule.CreatedAt;
                updatedSchedule.ModifiedAt = DateTime.UtcNow;

                _schedulesCache[scheduleId] = updatedSchedule;
                await SaveSchedulesAsync();

                _logger.LogInformation("Updated schedule {ScheduleName} ({ScheduleId})", updatedSchedule.Name, scheduleId);
                return updatedSchedule;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update schedule {ScheduleId}", scheduleId);
            return null;
        }
    }

    /// <summary>
    /// Delete a schedule
    /// </summary>
    public async Task<bool> DeleteScheduleAsync(Guid scheduleId)
    {
        try
        {
            if (_schedulesCache.TryRemove(scheduleId, out var schedule))
            {
                await SaveSchedulesAsync();
                _logger.LogInformation("Deleted schedule {ScheduleName} ({ScheduleId})", schedule.Name, scheduleId);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete schedule {ScheduleId}", scheduleId);
            return false;
        }
    }

    /// <summary>
    /// Get the current active layout for a client based on schedules
    /// </summary>
    public async Task<Guid?> GetActiveLayoutForClientAsync(Guid clientId, string? clientGroup = null)
    {
        try
        {
            var now = DateTime.UtcNow;
            var currentTime = now.ToString("HH:mm");
            var currentDay = now.DayOfWeek;

            // Get all applicable schedules
            var applicableSchedules = _schedulesCache.Values
                .Where(s => s.IsActive &&
                           (s.ClientId == clientId ||
                            (!string.IsNullOrEmpty(clientGroup) && string.Equals(s.ClientGroup, clientGroup, StringComparison.OrdinalIgnoreCase))) &&
                           (!s.ValidFrom.HasValue || s.ValidFrom.Value <= now) &&
                           (!s.ValidUntil.HasValue || s.ValidUntil.Value >= now))
                .ToList();

            // Filter by day of week and time
            var activeSchedules = new List<LayoutScheduleInfo>();

            foreach (var schedule in applicableSchedules)
            {
                if (IsScheduleActiveNow(schedule, currentDay, currentTime))
                {
                    activeSchedules.Add(schedule);
                }
            }

            // Get the schedule with highest priority
            var activeSchedule = activeSchedules
                .OrderBy(s => s.Priority)
                .FirstOrDefault();

            return activeSchedule?.LayoutId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get active layout for client {ClientId}", clientId);
            return null;
        }
    }

    /// <summary>
    /// Check if a schedule is active at the current time
    /// </summary>
    private bool IsScheduleActiveNow(LayoutScheduleInfo schedule, DayOfWeek currentDay, string currentTime)
    {
        // Check if current day is in schedule
        if (!string.IsNullOrEmpty(schedule.DaysOfWeek))
        {
            var days = schedule.DaysOfWeek.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(d => d.Trim())
                .ToList();

            var dayName = currentDay.ToString();
            if (!days.Any(d => string.Equals(d, dayName, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        // Check time range
        if (!string.IsNullOrEmpty(schedule.StartTime) && !string.IsNullOrEmpty(schedule.EndTime))
        {
            var startTime = TimeSpan.Parse(schedule.StartTime);
            var endTime = TimeSpan.Parse(schedule.EndTime);
            var current = TimeSpan.Parse(currentTime);

            if (endTime < startTime)
            {
                // Schedule spans midnight
                return current >= startTime || current <= endTime;
            }
            else
            {
                // Normal schedule
                return current >= startTime && current <= endTime;
            }
        }

        return true;
    }

    /// <summary>
    /// Activate or deactivate a schedule
    /// </summary>
    public async Task<bool> SetScheduleActiveStatusAsync(Guid scheduleId, bool isActive)
    {
        try
        {
            if (_schedulesCache.TryGetValue(scheduleId, out var schedule))
            {
                schedule.IsActive = isActive;
                schedule.ModifiedAt = DateTime.UtcNow;

                await SaveSchedulesAsync();
                _logger.LogInformation("{Action} schedule {ScheduleName} ({ScheduleId})",
                    isActive ? "Activated" : "Deactivated", schedule.Name, scheduleId);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update active status for schedule {ScheduleId}", scheduleId);
            return false;
        }
    }
}

/// <summary>
/// Layout schedule information
/// </summary>
public class LayoutScheduleInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid LayoutId { get; set; }
    public Guid? ClientId { get; set; }
    public string? ClientGroup { get; set; }
    public string StartTime { get; set; } = "00:00";
    public string EndTime { get; set; } = "23:59";
    public string? DaysOfWeek { get; set; } // Comma-separated: Monday,Tuesday,Wednesday...
    public int Priority { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidUntil { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}