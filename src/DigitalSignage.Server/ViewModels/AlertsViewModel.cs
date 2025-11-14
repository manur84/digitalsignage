using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Data;
using DigitalSignage.Data.Entities;
using DigitalSignage.Server.Services;
using DigitalSignage.Server.Views.Dialogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// ViewModel for the Alerts management panel
/// </summary>
public partial class AlertsViewModel : ObservableObject, IDisposable
{
    private readonly AlertService _alertService;
    private readonly IDbContextFactory<DigitalSignageDbContext> _contextFactory;
    private readonly ILogger<AlertsViewModel> _logger;
    private CancellationTokenSource? _pollingCts;
    private bool _disposed = false;

    [ObservableProperty]
    private ObservableCollection<Alert> _alerts = new();

    [ObservableProperty]
    private ObservableCollection<AlertRule> _alertRules = new();

    [ObservableProperty]
    private Alert? _selectedAlert;

    [ObservableProperty]
    private AlertRule? _selectedAlertRule;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private AlertFilterType _selectedFilter = AlertFilterType.All;

    [ObservableProperty]
    private int _unreadAlertCount;

    [ObservableProperty]
    private int _criticalAlertCount;

    public AlertsViewModel(
        AlertService alertService,
        IDbContextFactory<DigitalSignageDbContext> contextFactory,
        ILogger<AlertsViewModel> logger)
    {
        _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize
        _ = LoadDataAsync();
        StartPolling();
    }

    /// <summary>
    /// Starts polling for new alerts every 5 seconds
    /// </summary>
    private void StartPolling()
    {
        _pollingCts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            while (!_pollingCts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(5000, _pollingCts.Token);

                    var dispatcher = Application.Current.Dispatcher;
                    // Check if already on UI thread to avoid unnecessary context switch
                    if (dispatcher.CheckAccess())
                    {
                        await LoadAlertsAsync();
                    }
                    else
                    {
                        await dispatcher.InvokeAsync(async () =>
                        {
                            await LoadAlertsAsync();
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error polling alerts");
                }
            }
        }, _pollingCts.Token);
    }

    /// <summary>
    /// Stops polling
    /// </summary>
    public void StopPolling()
    {
        _pollingCts?.Cancel();
        _pollingCts?.Dispose();
    }

    /// <summary>
    /// Loads all data
    /// </summary>
    [RelayCommand]
    private async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            await LoadAlertRulesAsync();
            await LoadAlertsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading alert data");
            MessageBox.Show($"Failed to load alert data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Loads alert rules from database
    /// </summary>
    private async Task LoadAlertRulesAsync()
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var rules = await context.AlertRules
                .OrderByDescending(r => r.IsEnabled)
                .ThenBy(r => r.Name)
                .ToListAsync();

            AlertRules.Clear();
            foreach (var rule in rules)
            {
                AlertRules.Add(rule);
            }

            _logger.LogInformation("Loaded {Count} alert rules", rules.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading alert rules");
            throw;
        }
    }

    /// <summary>
    /// Loads alerts from database
    /// </summary>
    private async Task LoadAlertsAsync()
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Get alerts based on filter
            IQueryable<Alert> query = context.Alerts.Include(a => a.AlertRule);

            query = SelectedFilter switch
            {
                AlertFilterType.Unread => query.Where(a => !a.IsAcknowledged),
                AlertFilterType.Critical => query.Where(a => a.Severity == AlertSeverity.Critical),
                AlertFilterType.Error => query.Where(a => a.Severity == AlertSeverity.Error),
                AlertFilterType.Warning => query.Where(a => a.Severity == AlertSeverity.Warning),
                AlertFilterType.Info => query.Where(a => a.Severity == AlertSeverity.Info),
                AlertFilterType.Resolved => query.Where(a => a.IsResolved),
                AlertFilterType.Unresolved => query.Where(a => !a.IsResolved),
                _ => query
            };

            // Apply text filter
            if (!string.IsNullOrWhiteSpace(FilterText))
            {
                query = query.Where(a =>
                    a.Title.Contains(FilterText) ||
                    a.Message.Contains(FilterText) ||
                    (a.EntityId != null && a.EntityId.Contains(FilterText)));
            }

            var alerts = await query
                .OrderByDescending(a => a.TriggeredAt)
                .Take(500) // Limit to last 500 alerts
                .ToListAsync();

            Alerts.Clear();
            foreach (var alert in alerts)
            {
                Alerts.Add(alert);
            }

            // Update counts
            UnreadAlertCount = await context.Alerts.CountAsync(a => !a.IsAcknowledged && !a.IsResolved);
            CriticalAlertCount = await context.Alerts.CountAsync(a => a.Severity == AlertSeverity.Critical && !a.IsResolved);

            _logger.LogDebug("Loaded {Count} alerts (Unread: {Unread}, Critical: {Critical})",
                alerts.Count, UnreadAlertCount, CriticalAlertCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading alerts");
            throw;
        }
    }

    /// <summary>
    /// Creates a new alert rule
    /// </summary>
    [RelayCommand]
    private async Task CreateAlertRuleAsync()
    {
        try
        {
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetRequiredService<ILogger<AlertRuleEditorViewModel>>(App.GetServiceProvider());
            var viewModel = new AlertRuleEditorViewModel(_contextFactory, logger);
            var dialog = new AlertRuleEditorDialog
            {
                DataContext = viewModel,
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true && viewModel.CurrentRule != null)
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                context.AlertRules.Add(viewModel.CurrentRule);
                await context.SaveChangesAsync();

                AlertRules.Add(viewModel.CurrentRule);
                _logger.LogInformation("Created alert rule: {RuleName}", viewModel.CurrentRule.Name);

                MessageBox.Show($"Alert rule '{viewModel.CurrentRule.Name}' created successfully.",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating alert rule");
            MessageBox.Show($"Failed to create alert rule: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Edits the selected alert rule
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanEditAlertRule))]
    private async Task EditAlertRuleAsync()
    {
        if (SelectedAlertRule == null) return;

        try
        {
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetRequiredService<ILogger<AlertRuleEditorViewModel>>(App.GetServiceProvider());
            var viewModel = new AlertRuleEditorViewModel(_contextFactory, logger, SelectedAlertRule);
            var dialog = new AlertRuleEditorDialog
            {
                DataContext = viewModel,
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true && viewModel.CurrentRule != null)
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                context.AlertRules.Update(viewModel.CurrentRule);
                await context.SaveChangesAsync();

                await LoadAlertRulesAsync();
                _logger.LogInformation("Updated alert rule: {RuleName}", viewModel.CurrentRule.Name);

                MessageBox.Show($"Alert rule '{viewModel.CurrentRule.Name}' updated successfully.",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing alert rule");
            MessageBox.Show($"Failed to edit alert rule: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanEditAlertRule() => SelectedAlertRule != null;

    /// <summary>
    /// Deletes the selected alert rule
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDeleteAlertRule))]
    private async Task DeleteAlertRuleAsync()
    {
        if (SelectedAlertRule == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete the alert rule '{SelectedAlertRule.Name}'?\n\nThis will also delete all alerts triggered by this rule.",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var rule = await context.AlertRules.FindAsync(SelectedAlertRule.Id);
            if (rule != null)
            {
                context.AlertRules.Remove(rule);
                await context.SaveChangesAsync();

                AlertRules.Remove(SelectedAlertRule);
                _logger.LogInformation("Deleted alert rule: {RuleName}", rule.Name);

                MessageBox.Show($"Alert rule '{rule.Name}' deleted successfully.",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                await LoadAlertsAsync(); // Refresh alerts
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting alert rule");
            MessageBox.Show($"Failed to delete alert rule: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanDeleteAlertRule() => SelectedAlertRule != null;

    /// <summary>
    /// Tests the selected alert rule
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanTestAlertRule))]
    private async Task TestAlertRuleAsync()
    {
        if (SelectedAlertRule == null) return;

        try
        {
            IsLoading = true;
            await _alertService.EvaluateRuleAsync(SelectedAlertRule);
            await LoadAlertsAsync();

            MessageBox.Show($"Alert rule '{SelectedAlertRule.Name}' evaluated.\n\nCheck the alerts list to see if any alerts were triggered.",
                "Test Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing alert rule");
            MessageBox.Show($"Failed to test alert rule: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanTestAlertRule() => SelectedAlertRule != null;

    /// <summary>
    /// Toggles the enabled state of the selected alert rule
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanToggleAlertRule))]
    private async Task ToggleAlertRuleAsync()
    {
        if (SelectedAlertRule == null) return;

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var rule = await context.AlertRules.FindAsync(SelectedAlertRule.Id);
            if (rule != null)
            {
                rule.IsEnabled = !rule.IsEnabled;
                rule.ModifiedAt = DateTime.UtcNow;
                await context.SaveChangesAsync();

                SelectedAlertRule.IsEnabled = rule.IsEnabled;
                SelectedAlertRule.ModifiedAt = rule.ModifiedAt;

                _logger.LogInformation("Alert rule '{RuleName}' {Status}",
                    rule.Name, rule.IsEnabled ? "enabled" : "disabled");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling alert rule");
            MessageBox.Show($"Failed to toggle alert rule: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanToggleAlertRule() => SelectedAlertRule != null;

    /// <summary>
    /// Acknowledges the selected alert
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAcknowledgeAlert))]
    private async Task AcknowledgeAlertAsync()
    {
        if (SelectedAlert == null) return;

        try
        {
            await _alertService.AcknowledgeAlertAsync(SelectedAlert.Id, Environment.UserName);
            SelectedAlert.IsAcknowledged = true;
            SelectedAlert.AcknowledgedAt = DateTime.UtcNow;
            SelectedAlert.AcknowledgedBy = Environment.UserName;

            await LoadAlertsAsync();
            _logger.LogInformation("Acknowledged alert: {AlertId}", SelectedAlert.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging alert");
            MessageBox.Show($"Failed to acknowledge alert: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanAcknowledgeAlert() => SelectedAlert != null && !SelectedAlert.IsAcknowledged;

    /// <summary>
    /// Acknowledges all unread alerts
    /// </summary>
    [RelayCommand]
    private async Task AcknowledgeAllAlertsAsync()
    {
        if (UnreadAlertCount == 0)
        {
            MessageBox.Show("No unread alerts to acknowledge.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"Are you sure you want to acknowledge all {UnreadAlertCount} unread alerts?",
            "Confirm Acknowledge All",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            IsLoading = true;
            await using var context = await _contextFactory.CreateDbContextAsync();
            var unreadAlerts = await context.Alerts
                .Where(a => !a.IsAcknowledged)
                .ToListAsync();

            foreach (var alert in unreadAlerts)
            {
                alert.IsAcknowledged = true;
                alert.AcknowledgedAt = DateTime.UtcNow;
                alert.AcknowledgedBy = Environment.UserName;
            }

            await context.SaveChangesAsync();
            await LoadAlertsAsync();

            _logger.LogInformation("Acknowledged all {Count} unread alerts", unreadAlerts.Count);
            MessageBox.Show($"Acknowledged {unreadAlerts.Count} alerts.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging all alerts");
            MessageBox.Show($"Failed to acknowledge alerts: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Resolves the selected alert
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanResolveAlert))]
    private async Task ResolveAlertAsync()
    {
        if (SelectedAlert == null) return;

        try
        {
            await _alertService.ResolveAlertAsync(SelectedAlert.Id);
            SelectedAlert.IsResolved = true;
            SelectedAlert.ResolvedAt = DateTime.UtcNow;

            await LoadAlertsAsync();
            _logger.LogInformation("Resolved alert: {AlertId}", SelectedAlert.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving alert");
            MessageBox.Show($"Failed to resolve alert: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanResolveAlert() => SelectedAlert != null && !SelectedAlert.IsResolved;

    /// <summary>
    /// Deletes the selected alert
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDeleteAlert))]
    private async Task DeleteAlertAsync()
    {
        if (SelectedAlert == null) return;

        var result = MessageBox.Show(
            "Are you sure you want to delete this alert?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var alert = await context.Alerts.FindAsync(SelectedAlert.Id);
            if (alert != null)
            {
                context.Alerts.Remove(alert);
                await context.SaveChangesAsync();

                Alerts.Remove(SelectedAlert);
                _logger.LogInformation("Deleted alert: {AlertId}", alert.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting alert");
            MessageBox.Show($"Failed to delete alert: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanDeleteAlert() => SelectedAlert != null;

    /// <summary>
    /// Clears all resolved alerts
    /// </summary>
    [RelayCommand]
    private async Task ClearResolvedAlertsAsync()
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var resolvedCount = await context.Alerts.CountAsync(a => a.IsResolved);

            if (resolvedCount == 0)
            {
                MessageBox.Show("No resolved alerts to clear.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Are you sure you want to delete all {resolvedCount} resolved alerts?",
                "Confirm Clear",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            IsLoading = true;
            var resolvedAlerts = await context.Alerts.Where(a => a.IsResolved).ToListAsync();
            context.Alerts.RemoveRange(resolvedAlerts);
            await context.SaveChangesAsync();

            await LoadAlertsAsync();
            _logger.LogInformation("Cleared {Count} resolved alerts", resolvedCount);

            MessageBox.Show($"Cleared {resolvedCount} resolved alerts.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing resolved alerts");
            MessageBox.Show($"Failed to clear alerts: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Refreshes the alerts and rules
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadDataAsync();
    }

    /// <summary>
    /// Called when filter changes
    /// </summary>
    partial void OnSelectedFilterChanged(AlertFilterType value)
    {
        _ = LoadAlertsAsync();
    }

    /// <summary>
    /// Called when filter text changes
    /// </summary>
    partial void OnFilterTextChanged(string value)
    {
        _ = LoadAlertsAsync();
    }

    /// <summary>
    /// Disposes resources used by this ViewModel
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes managed and unmanaged resources
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Stop polling task
            _pollingCts?.Cancel();
            _pollingCts?.Dispose();
            _pollingCts = null;

            _logger.LogInformation("AlertsViewModel disposed");
        }

        _disposed = true;
    }
}

/// <summary>
/// Alert filter types
/// </summary>
public enum AlertFilterType
{
    All,
    Unread,
    Critical,
    Error,
    Warning,
    Info,
    Resolved,
    Unresolved
}
