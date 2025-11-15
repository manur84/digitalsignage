using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using DigitalSignage.Data;
using DigitalSignage.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// Schedule preview item showing when a schedule will be active
/// </summary>
public class SchedulePreviewItem
{
    public DateTime Date { get; set; }
    public string TimeRange { get; set; } = string.Empty;
    public string LayoutName { get; set; } = string.Empty;
    public string ScheduleName { get; set; } = string.Empty;
}

/// <summary>
/// ViewModel for managing layout schedules
/// </summary>
public partial class SchedulingViewModel : ObservableObject, IDisposable
{
    private readonly DigitalSignageDbContext _dbContext;
    private readonly ILayoutService _layoutService;
    private readonly IClientService _clientService;
    private readonly ILogger<SchedulingViewModel> _logger;
    private bool _disposed = false;

    [ObservableProperty]
    private LayoutSchedule? _selectedSchedule;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoading = false;

    // Day of Week Checkboxes
    [ObservableProperty]
    private bool _monday = false;

    [ObservableProperty]
    private bool _tuesday = false;

    [ObservableProperty]
    private bool _wednesday = false;

    [ObservableProperty]
    private bool _thursday = false;

    [ObservableProperty]
    private bool _friday = false;

    [ObservableProperty]
    private bool _saturday = false;

    [ObservableProperty]
    private bool _sunday = false;

    [ObservableProperty]
    private bool _allDays = true;

    // Date Range
    [ObservableProperty]
    private DateTime? _validFrom;

    [ObservableProperty]
    private DateTime? _validUntil;

    // Selected Devices
    [ObservableProperty]
    private RaspberryPiClient? _selectedDevice;

    public ObservableCollection<LayoutSchedule> Schedules { get; } = new();
    public ObservableCollection<DisplayLayout> AvailableLayouts { get; } = new();
    public ObservableCollection<RaspberryPiClient> AvailableDevices { get; } = new();
    public ObservableCollection<RaspberryPiClient> SelectedDevices { get; } = new();
    public ObservableCollection<SchedulePreviewItem> SchedulePreview { get; } = new();
    public ObservableCollection<LayoutSchedule> ConflictingSchedules { get; } = new();

    public SchedulingViewModel(
        DigitalSignageDbContext dbContext,
        ILayoutService layoutService,
        IClientService clientService,
        ILogger<SchedulingViewModel> logger)
    {
        _dbContext = dbContext;
        _layoutService = layoutService;
        _clientService = clientService;
        _logger = logger;

        // Load data
        _ = LoadSchedulesAsync();
        _ = LoadLayoutsAsync();
        _ = LoadDevicesAsync();

        // Subscribe to property changes to update DaysOfWeek string
        PropertyChanged += OnPropertyChanged;
    }

    private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectedSchedule) && SelectedSchedule != null)
        {
            LoadScheduleToUI();
        }

        // Update DaysOfWeek string when checkboxes change
        if (e.PropertyName is nameof(Monday) or nameof(Tuesday) or nameof(Wednesday) or
            nameof(Thursday) or nameof(Friday) or nameof(Saturday) or nameof(Sunday) or nameof(AllDays))
        {
            UpdateDaysOfWeekString();
        }

        // Update ValidFrom/ValidUntil when dates change
        if (e.PropertyName == nameof(ValidFrom) && SelectedSchedule != null)
        {
            SelectedSchedule.ValidFrom = ValidFrom;
        }

        if (e.PropertyName == nameof(ValidUntil) && SelectedSchedule != null)
        {
            SelectedSchedule.ValidUntil = ValidUntil;
        }
    }

    /// <summary>
    /// Load schedule data to UI controls
    /// </summary>
    private void LoadScheduleToUI()
    {
        if (SelectedSchedule == null) return;

        // Load days of week
        if (SelectedSchedule.DaysOfWeek == "*")
        {
            AllDays = true;
            Monday = Tuesday = Wednesday = Thursday = Friday = Saturday = Sunday = false;
        }
        else
        {
            AllDays = false;
            Monday = SelectedSchedule.DaysOfWeek.Contains("Monday", StringComparison.OrdinalIgnoreCase);
            Tuesday = SelectedSchedule.DaysOfWeek.Contains("Tuesday", StringComparison.OrdinalIgnoreCase);
            Wednesday = SelectedSchedule.DaysOfWeek.Contains("Wednesday", StringComparison.OrdinalIgnoreCase);
            Thursday = SelectedSchedule.DaysOfWeek.Contains("Thursday", StringComparison.OrdinalIgnoreCase);
            Friday = SelectedSchedule.DaysOfWeek.Contains("Friday", StringComparison.OrdinalIgnoreCase);
            Saturday = SelectedSchedule.DaysOfWeek.Contains("Saturday", StringComparison.OrdinalIgnoreCase);
            Sunday = SelectedSchedule.DaysOfWeek.Contains("Sunday", StringComparison.OrdinalIgnoreCase);
        }

        // Load date range
        ValidFrom = SelectedSchedule.ValidFrom;
        ValidUntil = SelectedSchedule.ValidUntil;

        // Load devices
        SelectedDevices.Clear();
        if (!string.IsNullOrEmpty(SelectedSchedule.ClientId))
        {
            var device = AvailableDevices.FirstOrDefault(d => d.Id.ToString() == SelectedSchedule.ClientId);
            if (device != null)
            {
                SelectedDevices.Add(device);
            }
        }

        // Generate preview
        GenerateSchedulePreview();

        // Check for conflicts
        _ = CheckConflicts();
    }

    /// <summary>
    /// Update DaysOfWeek string from checkbox states
    /// </summary>
    private void UpdateDaysOfWeekString()
    {
        if (SelectedSchedule == null) return;

        if (AllDays)
        {
            SelectedSchedule.DaysOfWeek = "*";
        }
        else
        {
            var days = new List<string>();
            if (Monday) days.Add("Monday");
            if (Tuesday) days.Add("Tuesday");
            if (Wednesday) days.Add("Wednesday");
            if (Thursday) days.Add("Thursday");
            if (Friday) days.Add("Friday");
            if (Saturday) days.Add("Saturday");
            if (Sunday) days.Add("Sunday");

            SelectedSchedule.DaysOfWeek = days.Count > 0 ? string.Join(",", days) : "*";
        }

        // Regenerate preview when days change
        GenerateSchedulePreview();
    }

    /// <summary>
    /// Load all schedules from database
    /// </summary>
    private async Task LoadSchedulesAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading schedules...";

        try
        {
            _logger.LogInformation("Loading layout schedules from database");

            var schedules = await _dbContext.LayoutSchedules
                .OrderBy(s => s.Priority)
                .ThenBy(s => s.StartTime)
                .ToListAsync();

            Schedules.Clear();
            foreach (var schedule in schedules)
            {
                Schedules.Add(schedule);
            }

            StatusMessage = $"Loaded {schedules.Count} schedules";
            _logger.LogInformation("Loaded {Count} schedules", schedules.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load schedules");
            StatusMessage = $"Error loading schedules: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Load available layouts
    /// </summary>
    private async Task LoadLayoutsAsync()
    {
        try
        {
            var layoutsResult = await _layoutService.GetAllLayoutsAsync();
            if (layoutsResult.IsFailure)
            {
                _logger.LogError("Failed to load layouts: {ErrorMessage}", layoutsResult.ErrorMessage);
                return;
            }

            AvailableLayouts.Clear();
            foreach (var layout in layoutsResult.Value)
            {
                AvailableLayouts.Add(layout);
            }

            _logger.LogInformation("Loaded {Count} layouts", layoutsResult.Value.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load layouts");
        }
    }

    /// <summary>
    /// Load available devices
    /// </summary>
    private async Task LoadDevicesAsync()
    {
        try
        {
            var devices = await _clientService.GetAllClientsAsync();

            AvailableDevices.Clear();
            foreach (var device in devices)
            {
                AvailableDevices.Add(device);
            }

            _logger.LogInformation("Loaded {Count} devices for scheduling", devices.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load devices");
        }
    }

    /// <summary>
    /// Create a new schedule
    /// </summary>
    [RelayCommand]
    private void AddSchedule()
    {
        var newSchedule = new LayoutSchedule
        {
            Name = "New Schedule",
            StartTime = "09:00",
            EndTime = "17:00",
            DaysOfWeek = "Monday,Tuesday,Wednesday,Thursday,Friday",
            Priority = 0,
            IsActive = true
        };

        Schedules.Add(newSchedule);
        SelectedSchedule = newSchedule;
        StatusMessage = "New schedule created (not saved yet)";
    }

    /// <summary>
    /// Save the selected schedule
    /// </summary>
    [RelayCommand]
    private async Task SaveSchedule()
    {
        if (SelectedSchedule == null)
        {
            StatusMessage = "No schedule selected";
            return;
        }

        // Validate before saving
        var validationErrors = ValidateSchedule();
        if (validationErrors.Any())
        {
            StatusMessage = $"Validation errors: {string.Join(", ", validationErrors)}";
            _logger.LogWarning("Schedule validation failed: {Errors}", string.Join(", ", validationErrors));
            return;
        }

        try
        {
            _logger.LogInformation("Saving schedule: {ScheduleName}", SelectedSchedule.Name);

            // Update ClientId from SelectedDevices - use FirstOrDefault for safety
            var firstDevice = SelectedDevices.FirstOrDefault();
            SelectedSchedule.ClientId = firstDevice?.Id.ToString();

            // Check if it's a new schedule or existing
            if (SelectedSchedule.Id == 0)
            {
                // New schedule
                _dbContext.LayoutSchedules.Add(SelectedSchedule);
                StatusMessage = "Adding new schedule...";
            }
            else
            {
                // Existing schedule
                _dbContext.LayoutSchedules.Update(SelectedSchedule);
                StatusMessage = "Updating schedule...";
            }

            SelectedSchedule.ModifiedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            StatusMessage = $"Schedule '{SelectedSchedule.Name}' saved successfully";
            _logger.LogInformation("Schedule saved: {ScheduleName} (ID: {ScheduleId})",
                SelectedSchedule.Name, SelectedSchedule.Id);

            // Reload schedules
            await LoadSchedulesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save schedule");
            StatusMessage = $"Error saving schedule: {ex.Message}";
        }
    }

    /// <summary>
    /// Delete the selected schedule
    /// </summary>
    [RelayCommand]
    private async Task DeleteSchedule()
    {
        if (SelectedSchedule == null)
        {
            StatusMessage = "No schedule selected";
            return;
        }

        try
        {
            _logger.LogInformation("Deleting schedule: {ScheduleName}", SelectedSchedule.Name);

            if (SelectedSchedule.Id > 0)
            {
                _dbContext.LayoutSchedules.Remove(SelectedSchedule);
                await _dbContext.SaveChangesAsync();
            }

            Schedules.Remove(SelectedSchedule);
            SelectedSchedule = null;

            StatusMessage = "Schedule deleted successfully";
            _logger.LogInformation("Schedule deleted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete schedule");
            StatusMessage = $"Error deleting schedule: {ex.Message}";
        }
    }

    /// <summary>
    /// Refresh schedules list
    /// </summary>
    [RelayCommand]
    private async Task RefreshSchedules()
    {
        await LoadSchedulesAsync();
    }

    /// <summary>
    /// Test if schedule is valid for current time
    /// </summary>
    [RelayCommand]
    private void TestSchedule()
    {
        if (SelectedSchedule == null)
        {
            StatusMessage = "No schedule selected";
            return;
        }

        var now = DateTime.Now;
        var isValid = SelectedSchedule.IsValidAt(now);

        StatusMessage = isValid
            ? $"✓ Schedule is ACTIVE now ({now:HH:mm})"
            : $"✗ Schedule is NOT ACTIVE now ({now:HH:mm})";

        _logger.LogInformation("Schedule test: {ScheduleName} is {Status} at {Time}",
            SelectedSchedule.Name, isValid ? "active" : "inactive", now);
    }

    /// <summary>
    /// Add selected device to schedule
    /// </summary>
    [RelayCommand]
    private void AddDevice()
    {
        if (SelectedDevice == null || SelectedDevices.Contains(SelectedDevice))
            return;

        SelectedDevices.Add(SelectedDevice);
        StatusMessage = $"Device '{SelectedDevice.Name}' added";
    }

    /// <summary>
    /// Remove device from schedule
    /// </summary>
    [RelayCommand]
    private void RemoveDevice(RaspberryPiClient? device)
    {
        if (device == null) return;

        SelectedDevices.Remove(device);
        StatusMessage = $"Device '{device.Name}' removed";
    }

    /// <summary>
    /// Validate schedule data
    /// </summary>
    private List<string> ValidateSchedule()
    {
        var errors = new List<string>();

        if (SelectedSchedule == null)
        {
            errors.Add("No schedule selected");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(SelectedSchedule.Name))
            errors.Add("Name is required");

        if (string.IsNullOrWhiteSpace(SelectedSchedule.LayoutId))
            errors.Add("Layout must be selected");

        // Validate time format
        if (!TimeSpan.TryParse(SelectedSchedule.StartTime, out var startTime))
            errors.Add("Invalid start time format (use HH:mm)");

        if (!TimeSpan.TryParse(SelectedSchedule.EndTime, out var endTime))
            errors.Add("Invalid end time format (use HH:mm)");

        // Validate date range
        if (ValidFrom.HasValue && ValidUntil.HasValue && ValidFrom.Value > ValidUntil.Value)
            errors.Add("Valid From date must be before Valid Until date");

        // Validate days of week
        if (!AllDays && !Monday && !Tuesday && !Wednesday && !Thursday && !Friday && !Saturday && !Sunday)
            errors.Add("At least one day must be selected");

        return errors;
    }

    /// <summary>
    /// Check for conflicting schedules
    /// </summary>
    [RelayCommand]
    private async Task CheckConflicts()
    {
        if (SelectedSchedule == null)
        {
            ConflictingSchedules.Clear();
            return;
        }

        try
        {
            var conflicts = await _dbContext.LayoutSchedules
                .Where(s => s.Id != SelectedSchedule.Id && s.IsActive)
                .ToListAsync();

            ConflictingSchedules.Clear();

            foreach (var schedule in conflicts)
            {
                // Check if schedules overlap
                if (SchedulesOverlap(SelectedSchedule, schedule))
                {
                    ConflictingSchedules.Add(schedule);
                }
            }

            if (ConflictingSchedules.Any())
            {
                StatusMessage = $"⚠ Warning: {ConflictingSchedules.Count} conflicting schedule(s) found (priority will determine which schedule is active)";
                _logger.LogWarning("Schedule conflicts detected: {Count}", ConflictingSchedules.Count);
            }
            else
            {
                StatusMessage = "✓ No conflicts detected";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check conflicts");
        }
    }

    /// <summary>
    /// Check if two schedules overlap
    /// </summary>
    private bool SchedulesOverlap(LayoutSchedule schedule1, LayoutSchedule schedule2)
    {
        // Check device targeting
        if (!string.IsNullOrEmpty(schedule1.ClientId) && !string.IsNullOrEmpty(schedule2.ClientId))
        {
            if (schedule1.ClientId != schedule2.ClientId)
                return false; // Different devices, no conflict
        }

        // Check date range overlap
        if (schedule1.ValidFrom.HasValue && schedule2.ValidUntil.HasValue && schedule1.ValidFrom.Value > schedule2.ValidUntil.Value)
            return false;

        if (schedule1.ValidUntil.HasValue && schedule2.ValidFrom.HasValue && schedule1.ValidUntil.Value < schedule2.ValidFrom.Value)
            return false;

        // Check days of week overlap
        if (schedule1.DaysOfWeek != "*" && schedule2.DaysOfWeek != "*")
        {
            var days1 = schedule1.DaysOfWeek.Split(',').Select(d => d.Trim()).ToHashSet();
            var days2 = schedule2.DaysOfWeek.Split(',').Select(d => d.Trim()).ToHashSet();

            if (!days1.Overlaps(days2))
                return false; // No common days
        }

        // Check time overlap
        if (!TimeSpan.TryParse(schedule1.StartTime, out var start1)) return false;
        if (!TimeSpan.TryParse(schedule1.EndTime, out var end1)) return false;
        if (!TimeSpan.TryParse(schedule2.StartTime, out var start2)) return false;
        if (!TimeSpan.TryParse(schedule2.EndTime, out var end2)) return false;

        // Check if time ranges overlap
        return !(end1 <= start2 || end2 <= start1);
    }

    /// <summary>
    /// Generate preview of when schedule will be active in next 7 days
    /// </summary>
    [RelayCommand]
    private void GenerateSchedulePreview()
    {
        SchedulePreview.Clear();

        if (SelectedSchedule == null) return;

        try
        {
            var layout = AvailableLayouts.FirstOrDefault(l => l.Id == SelectedSchedule.LayoutId);
            var layoutName = layout?.Name ?? "Unknown Layout";

            var today = DateTime.Today;
            for (int i = 0; i < 7; i++)
            {
                var date = today.AddDays(i);

                // Check if schedule is valid for this date
                if (SelectedSchedule.IsValidAt(date.Add(TimeSpan.Parse(SelectedSchedule.StartTime))))
                {
                    SchedulePreview.Add(new SchedulePreviewItem
                    {
                        Date = date,
                        TimeRange = $"{SelectedSchedule.StartTime} - {SelectedSchedule.EndTime}",
                        LayoutName = layoutName,
                        ScheduleName = SelectedSchedule.Name
                    });
                }
            }

            StatusMessage = $"Preview generated for next 7 days: {SchedulePreview.Count} activation(s)";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate schedule preview");
            StatusMessage = "Error generating preview";
        }
    }

    /// <summary>
    /// Get schedule status for display
    /// </summary>
    public string GetScheduleStatus(LayoutSchedule schedule)
    {
        if (!schedule.IsActive)
            return "⚪ Inactive";

        var now = DateTime.Now;

        // Check if expired
        if (schedule.ValidUntil.HasValue && now > schedule.ValidUntil.Value)
            return "🔴 Expired";

        // Check if currently active
        if (schedule.IsValidAt(now))
            return "🟢 Active Now";

        // Check if scheduled for future
        if (!schedule.ValidFrom.HasValue || now >= schedule.ValidFrom.Value)
            return "🔵 Scheduled";

        return "⏳ Future";
    }

    /// <summary>
    /// Toggle schedule active state
    /// </summary>
    [RelayCommand]
    private async Task ToggleScheduleActive()
    {
        if (SelectedSchedule == null) return;

        SelectedSchedule.IsActive = !SelectedSchedule.IsActive;
        await SaveSchedule();
        StatusMessage = SelectedSchedule.IsActive ? "Schedule activated" : "Schedule deactivated";
    }

    /// <summary>
    /// Duplicate selected schedule
    /// </summary>
    [RelayCommand]
    private void DuplicateSchedule()
    {
        if (SelectedSchedule == null) return;

        var duplicate = new LayoutSchedule
        {
            Name = $"{SelectedSchedule.Name} (Copy)",
            Description = SelectedSchedule.Description,
            LayoutId = SelectedSchedule.LayoutId,
            ClientId = SelectedSchedule.ClientId,
            ClientGroup = SelectedSchedule.ClientGroup,
            StartTime = SelectedSchedule.StartTime,
            EndTime = SelectedSchedule.EndTime,
            DaysOfWeek = SelectedSchedule.DaysOfWeek,
            Priority = SelectedSchedule.Priority,
            IsActive = false, // Inactive by default
            ValidFrom = SelectedSchedule.ValidFrom,
            ValidUntil = SelectedSchedule.ValidUntil
        };

        Schedules.Add(duplicate);
        SelectedSchedule = duplicate;
        StatusMessage = "Schedule duplicated (not saved yet)";
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Unregister PropertyChanged event handler
            PropertyChanged -= OnPropertyChanged;
        }

        _disposed = true;
    }
}
