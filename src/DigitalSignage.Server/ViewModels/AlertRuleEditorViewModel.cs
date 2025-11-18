using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Data;
using DigitalSignage.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// ViewModel for editing alert rules
/// </summary>
public partial class AlertRuleEditorViewModel : ObservableObject
{
    private readonly IDbContextFactory<DigitalSignageDbContext> _contextFactory;
    private readonly ILogger _logger;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private AlertRule? _currentRule;

    [ObservableProperty]
    private string _ruleName = string.Empty;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private AlertRuleType _selectedRuleType = AlertRuleType.DeviceOffline;

    [ObservableProperty]
    private AlertSeverity _selectedSeverity = AlertSeverity.Warning;

    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    private int _cooldownMinutes = 15;

    [ObservableProperty]
    private string _notificationChannels = "UI";

    // Device Offline Configuration
    [ObservableProperty]
    private int _offlineThresholdMinutes = 5;

    // CPU Configuration
    [ObservableProperty]
    private double _cpuThreshold = 90.0;

    // Memory Configuration
    [ObservableProperty]
    private double _memoryThreshold = 90.0;

    // Disk Configuration
    [ObservableProperty]
    private double _diskThreshold = 90.0;

    [ObservableProperty]
    private bool _isEditMode;

    public List<AlertRuleType> RuleTypes { get; } = [.. Enum.GetValues<AlertRuleType>()];
    public List<AlertSeverity> SeverityLevels { get; } = [.. Enum.GetValues<AlertSeverity>()];

    public AlertRuleEditorViewModel(
        IDbContextFactory<DigitalSignageDbContext> contextFactory,
        ILogger<AlertRuleEditorViewModel> logger,
        IDialogService dialogService,
        AlertRule? existingRule = null)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

        if (existingRule != null)
        {
            IsEditMode = true;
            LoadFromRule(existingRule);
        }
    }

    /// <summary>
    /// Loads data from an existing rule
    /// </summary>
    private void LoadFromRule(AlertRule rule)
    {
        CurrentRule = rule;
        RuleName = rule.Name;
        Description = rule.Description;
        SelectedRuleType = rule.RuleType;
        SelectedSeverity = rule.Severity;
        IsEnabled = rule.IsEnabled;
        CooldownMinutes = rule.CooldownMinutes;
        NotificationChannels = rule.NotificationChannels ?? "UI";

        // Parse configuration
        if (!string.IsNullOrWhiteSpace(rule.Configuration))
        {
            try
            {
                using var config = JsonDocument.Parse(rule.Configuration);

                // Load type-specific configuration
                switch (rule.RuleType)
                {
                    case AlertRuleType.DeviceOffline:
                        if (config.RootElement.TryGetProperty(nameof(OfflineThresholdMinutes), out var offlineThreshold))
                            OfflineThresholdMinutes = offlineThreshold.GetInt32();
                        break;

                    case AlertRuleType.DeviceHighCpu:
                        if (config.RootElement.TryGetProperty(nameof(CpuThreshold), out var cpuThreshold))
                            CpuThreshold = cpuThreshold.GetDouble();
                        break;

                    case AlertRuleType.DeviceHighMemory:
                        if (config.RootElement.TryGetProperty(nameof(MemoryThreshold), out var memoryThreshold))
                            MemoryThreshold = memoryThreshold.GetDouble();
                        break;

                    case AlertRuleType.DeviceLowDiskSpace:
                        if (config.RootElement.TryGetProperty(nameof(DiskThreshold), out var diskThreshold))
                            DiskThreshold = diskThreshold.GetDouble();
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing rule configuration");
            }
        }
    }

    /// <summary>
    /// Saves the alert rule
    /// </summary>
    [RelayCommand]
    private async Task Save()
    {
        if (!await ValidateRule())
        {
            return;
        }

        try
        {
            // Create or update rule
            CurrentRule ??= new AlertRule
            {
                CreatedAt = DateTime.UtcNow
            };

            CurrentRule.Name = RuleName;
            CurrentRule.Description = Description;
            CurrentRule.RuleType = SelectedRuleType;
            CurrentRule.Severity = SelectedSeverity;
            CurrentRule.IsEnabled = IsEnabled;
            CurrentRule.CooldownMinutes = CooldownMinutes;
            CurrentRule.NotificationChannels = NotificationChannels;
            CurrentRule.ModifiedAt = DateTime.UtcNow;

            // Build configuration JSON
            CurrentRule.Configuration = BuildConfiguration();

            // Close dialog with success
            CloseDialog(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving alert rule");
            await _dialogService.ShowErrorAsync($"Failed to save alert rule: {ex.Message}", "Error");
        }
    }

    /// <summary>
    /// Cancels editing
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        CloseDialog(false);
    }

    /// <summary>
    /// Validates the rule
    /// </summary>
    private async Task<bool> ValidateRule()
    {
        if (string.IsNullOrWhiteSpace(RuleName))
        {
            await _dialogService.ShowValidationErrorAsync("Please enter a rule name.");
            return false;
        }

        if (CooldownMinutes < 0 || CooldownMinutes > 1440)
        {
            await _dialogService.ShowValidationErrorAsync("Cooldown minutes must be between 0 and 1440 (24 hours).");
            return false;
        }

        // Type-specific validation
        switch (SelectedRuleType)
        {
            case AlertRuleType.DeviceOffline:
                if (OfflineThresholdMinutes < 1 || OfflineThresholdMinutes > 1440)
                {
                    await _dialogService.ShowValidationErrorAsync("Offline threshold must be between 1 and 1440 minutes.");
                    return false;
                }
                break;

            case AlertRuleType.DeviceHighCpu:
                if (CpuThreshold < 1 || CpuThreshold > 100)
                {
                    await _dialogService.ShowValidationErrorAsync("CPU threshold must be between 1 and 100%.");
                    return false;
                }
                break;

            case AlertRuleType.DeviceHighMemory:
                if (MemoryThreshold < 1 || MemoryThreshold > 100)
                {
                    await _dialogService.ShowValidationErrorAsync("Memory threshold must be between 1 and 100%.");
                    return false;
                }
                break;

            case AlertRuleType.DeviceLowDiskSpace:
                if (DiskThreshold < 1 || DiskThreshold > 100)
                {
                    await _dialogService.ShowValidationErrorAsync("Disk threshold must be between 1 and 100%.");
                    return false;
                }
                break;
        }

        return true;
    }

    /// <summary>
    /// Builds the configuration JSON
    /// </summary>
    private string BuildConfiguration()
    {
        var config = new Dictionary<string, object>();

        switch (SelectedRuleType)
        {
            case AlertRuleType.DeviceOffline:
                config["OfflineThresholdMinutes"] = OfflineThresholdMinutes;
                break;

            case AlertRuleType.DeviceHighCpu:
                config["CpuThreshold"] = CpuThreshold;
                break;

            case AlertRuleType.DeviceHighMemory:
                config["MemoryThreshold"] = MemoryThreshold;
                break;

            case AlertRuleType.DeviceLowDiskSpace:
                config["DiskThreshold"] = DiskThreshold;
                break;
        }

        return JsonSerializer.Serialize(config);
    }

    /// <summary>
    /// Closes the dialog
    /// </summary>
    private void CloseDialog(bool dialogResult)
    {
        // Find the dialog window
        foreach (Window window in Application.Current.Windows)
        {
            if (window.DataContext == this)
            {
                window.DialogResult = dialogResult;
                window.Close();
                break;
            }
        }
    }
}
