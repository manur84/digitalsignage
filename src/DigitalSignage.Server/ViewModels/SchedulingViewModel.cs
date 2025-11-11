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
/// ViewModel for managing layout schedules
/// </summary>
public partial class SchedulingViewModel : ObservableObject
{
    private readonly DigitalSignageDbContext _dbContext;
    private readonly ILayoutService _layoutService;
    private readonly ILogger<SchedulingViewModel> _logger;

    [ObservableProperty]
    private LayoutSchedule? _selectedSchedule;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoading = false;

    public ObservableCollection<LayoutSchedule> Schedules { get; } = new();
    public ObservableCollection<DisplayLayout> AvailableLayouts { get; } = new();

    public SchedulingViewModel(
        DigitalSignageDbContext dbContext,
        ILayoutService layoutService,
        ILogger<SchedulingViewModel> logger)
    {
        _dbContext = dbContext;
        _layoutService = layoutService;
        _logger = logger;

        // Load data
        _ = LoadSchedulesAsync();
        _ = LoadLayoutsAsync();
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
            var layouts = await _layoutService.GetAllLayoutsAsync();

            AvailableLayouts.Clear();
            foreach (var layout in layouts)
            {
                AvailableLayouts.Add(layout);
            }

            _logger.LogInformation("Loaded {Count} layouts", layouts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load layouts");
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

        try
        {
            _logger.LogInformation("Saving schedule: {ScheduleName}", SelectedSchedule.Name);

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
}
