using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    public List<AlertRuleType> RuleTypes { get; } = Enum.GetValues<AlertRuleType>().ToList();
    public List<AlertSeverity> SeverityLevels { get; } = Enum.GetValues<AlertSeverity>().ToList();

    public AlertRuleEditorViewModel(
        IDbContextFactory<DigitalSignageDbContext> contextFactory,
        ILogger<AlertRuleEditorViewModel> logger,
        AlertRule? existingRule = null)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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
                var config = JsonDocument.Parse(rule.Configuration);

                // Load type-specific configuration
                switch (rule.RuleType)
                {
                    case AlertRuleType.DeviceOffline:
                        if (config.RootElement.TryGetProperty("OfflineThresholdMinutes", out var offlineThreshold))
                            OfflineThresholdMinutes = offlineThreshold.GetInt32();
                        break;

                    case AlertRuleType.DeviceHighCpu:
                        if (config.RootElement.TryGetProperty("CpuThreshold", out var cpuThreshold))
                            CpuThreshold = cpuThreshold.GetDouble();
                        break;

                    case AlertRuleType.DeviceHighMemory:
                        if (config.RootElement.TryGetProperty("MemoryThreshold", out var memoryThreshold))
                            MemoryThreshold = memoryThreshold.GetDouble();
                        break;

                    case AlertRuleType.DeviceLowDiskSpace:
                        if (config.RootElement.TryGetProperty("DiskThreshold", out var diskThreshold))
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
    private void Save()
    {
        if (!ValidateRule())
        {
            return;
        }

        try
        {
            // Create or update rule
            if (CurrentRule == null)
            {
                CurrentRule = new AlertRule
                {
                    CreatedAt = DateTime.UtcNow
                };
            }

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
            MessageBox.Show($"Failed to save alert rule: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
    private bool ValidateRule()
    {
        if (string.IsNullOrWhiteSpace(RuleName))
        {
            MessageBox.Show("Please enter a rule name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (CooldownMinutes < 0 || CooldownMinutes > 1440)
        {
            MessageBox.Show("Cooldown minutes must be between 0 and 1440 (24 hours).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        // Type-specific validation
        switch (SelectedRuleType)
        {
            case AlertRuleType.DeviceOffline:
                if (OfflineThresholdMinutes < 1 || OfflineThresholdMinutes > 1440)
                {
                    MessageBox.Show("Offline threshold must be between 1 and 1440 minutes.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                break;

            case AlertRuleType.DeviceHighCpu:
                if (CpuThreshold < 1 || CpuThreshold > 100)
                {
                    MessageBox.Show("CPU threshold must be between 1 and 100%.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                break;

            case AlertRuleType.DeviceHighMemory:
                if (MemoryThreshold < 1 || MemoryThreshold > 100)
                {
                    MessageBox.Show("Memory threshold must be between 1 and 100%.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                break;

            case AlertRuleType.DeviceLowDiskSpace:
                if (DiskThreshold < 1 || DiskThreshold > 100)
                {
                    MessageBox.Show("Disk threshold must be between 1 and 100%.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
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
