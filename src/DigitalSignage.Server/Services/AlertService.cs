using DigitalSignage.Data;
using DigitalSignage.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Service for managing alerts and alert rules
/// </summary>
public class AlertService
{
    private readonly IDbContextFactory<DigitalSignageDbContext> _contextFactory;
    private readonly ILogger<AlertService> _logger;
    private readonly Dictionary<int, DateTime> _lastTriggerTimes = new();

    public AlertService(
        IDbContextFactory<DigitalSignageDbContext> contextFactory,
        ILogger<AlertService> logger)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Evaluates all enabled alert rules
    /// </summary>
    public async Task EvaluateAllRulesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var rules = await context.AlertRules
                .Where(r => r.IsEnabled)
                .ToListAsync(cancellationToken);

            foreach (var rule in rules)
            {
                await EvaluateRuleAsync(rule, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating alert rules");
        }
    }

    /// <summary>
    /// Evaluates a specific alert rule
    /// </summary>
    public async Task EvaluateRuleAsync(AlertRule rule, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check cooldown period
            if (_lastTriggerTimes.TryGetValue(rule.Id, out var lastTrigger))
            {
                var cooldownEnd = lastTrigger.AddMinutes(rule.CooldownMinutes);
                if (DateTime.UtcNow < cooldownEnd)
                {
                    _logger.LogDebug("Rule {RuleName} is in cooldown period until {CooldownEnd}", rule.Name, cooldownEnd);
                    return;
                }
            }

            bool shouldTrigger = false;
            string? entityType = null;
            string? entityId = null;
            string message = string.Empty;

            // Evaluate based on rule type
            switch (rule.RuleType)
            {
                case AlertRuleType.DeviceOffline:
                    (shouldTrigger, entityType, entityId, message) = await EvaluateDeviceOfflineRule(rule, cancellationToken);
                    break;

                case AlertRuleType.DeviceHighCpu:
                    (shouldTrigger, entityType, entityId, message) = await EvaluateDeviceHighCpuRule(rule, cancellationToken);
                    break;

                case AlertRuleType.DeviceHighMemory:
                    (shouldTrigger, entityType, entityId, message) = await EvaluateDeviceHighMemoryRule(rule, cancellationToken);
                    break;

                case AlertRuleType.DeviceLowDiskSpace:
                    (shouldTrigger, entityType, entityId, message) = await EvaluateDeviceLowDiskSpaceRule(rule, cancellationToken);
                    break;

                case AlertRuleType.DataSourceError:
                    (shouldTrigger, entityType, entityId, message) = await EvaluateDataSourceErrorRule(rule, cancellationToken);
                    break;

                case AlertRuleType.HighErrorRate:
                    (shouldTrigger, entityType, entityId, message) = await EvaluateHighErrorRateRule(rule, cancellationToken);
                    break;

                default:
                    _logger.LogWarning("Unknown rule type: {RuleType}", rule.RuleType);
                    break;
            }

            if (shouldTrigger)
            {
                await TriggerAlertAsync(rule, message, entityType, entityId, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating rule {RuleName}", rule.Name);
        }
    }

    /// <summary>
    /// Triggers an alert
    /// </summary>
    private async Task TriggerAlertAsync(
        AlertRule rule,
        string message,
        string? entityType,
        string? entityId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var alert = new Alert
            {
                AlertRuleId = rule.Id,
                Severity = rule.Severity,
                Title = rule.Name,
                Message = message,
                EntityType = entityType,
                EntityId = entityId,
                TriggeredAt = DateTime.UtcNow,
                IsAcknowledged = false,
                IsResolved = false
            };

            context.Alerts.Add(alert);

            // Update rule statistics
            rule.LastTriggeredAt = DateTime.UtcNow;
            rule.TriggerCount++;
            context.AlertRules.Update(rule);

            await context.SaveChangesAsync(cancellationToken);

            // Track last trigger time for cooldown
            _lastTriggerTimes[rule.Id] = DateTime.UtcNow;

            _logger.LogWarning("Alert triggered: {RuleName} - {Message}", rule.Name, message);

            // Send notifications (if configured)
            await SendNotificationsAsync(alert, rule, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering alert for rule {RuleName}", rule.Name);
        }
    }

    /// <summary>
    /// Sends notifications for an alert (placeholder implementation)
    /// </summary>
    private async Task SendNotificationsAsync(Alert alert, AlertRule rule, CancellationToken cancellationToken = default)
    {
        // Placeholder for notification implementation
        // In production, this would send emails, SMS, push notifications, etc.
        _logger.LogInformation("Notification sent for alert: {Title} (Channels: {Channels})",
            alert.Title, rule.NotificationChannels ?? "None");

        await Task.CompletedTask;
    }

    /// <summary>
    /// Evaluates device offline rule
    /// </summary>
    private async Task<(bool shouldTrigger, string? entityType, string? entityId, string message)> EvaluateDeviceOfflineRule(
        AlertRule rule,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var config = ParseConfiguration(rule.Configuration);
        var offlineThresholdMinutes = GetConfigValue(config, "OfflineThresholdMinutes", 5);
        var threshold = DateTime.UtcNow.AddMinutes(-offlineThresholdMinutes);

        var offlineClients = await context.Clients
            .Where(c => c.LastSeen < threshold)
            .ToListAsync(cancellationToken);

        if (offlineClients.Any())
        {
            var clientNames = string.Join(", ", offlineClients.Select(c => c.Name));
            var message = $"{offlineClients.Count} device(s) offline: {clientNames}";
            return (true, "Client", offlineClients.First().Id, message);
        }

        return (false, null, null, string.Empty);
    }

    /// <summary>
    /// Evaluates device high CPU rule
    /// </summary>
    private async Task<(bool shouldTrigger, string? entityType, string? entityId, string message)> EvaluateDeviceHighCpuRule(
        AlertRule rule,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var config = ParseConfiguration(rule.Configuration);
        var cpuThreshold = GetConfigValue(config, "CpuThreshold", 90.0);

        var clients = await context.Clients
            .Where(c => c.DeviceInfo != null)
            .ToListAsync(cancellationToken);

        foreach (var client in clients)
        {
            if (client.DeviceInfo?.CpuUsage > cpuThreshold)
            {
                var message = $"Device {client.Name} has high CPU usage: {client.DeviceInfo.CpuUsage:F1}%";
                return (true, "Client", client.Id, message);
            }
        }

        return (false, null, null, string.Empty);
    }

    /// <summary>
    /// Evaluates device high memory rule
    /// </summary>
    private async Task<(bool shouldTrigger, string? entityType, string? entityId, string message)> EvaluateDeviceHighMemoryRule(
        AlertRule rule,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var config = ParseConfiguration(rule.Configuration);
        var memoryThreshold = GetConfigValue(config, "MemoryThreshold", 90.0);

        var clients = await context.Clients
            .Where(c => c.DeviceInfo != null)
            .ToListAsync(cancellationToken);

        foreach (var client in clients)
        {
            if (client.DeviceInfo != null && client.DeviceInfo.MemoryTotal > 0)
            {
                var memoryUsagePercent = (double)client.DeviceInfo.MemoryUsed / client.DeviceInfo.MemoryTotal * 100;
                if (memoryUsagePercent > memoryThreshold)
                {
                    var message = $"Device {client.Name} has high memory usage: {memoryUsagePercent:F1}%";
                    return (true, "Client", client.Id, message);
                }
            }
        }

        return (false, null, null, string.Empty);
    }

    /// <summary>
    /// Evaluates device low disk space rule
    /// </summary>
    private async Task<(bool shouldTrigger, string? entityType, string? entityId, string message)> EvaluateDeviceLowDiskSpaceRule(
        AlertRule rule,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var config = ParseConfiguration(rule.Configuration);
        var diskThreshold = GetConfigValue(config, "DiskThreshold", 90.0);

        var clients = await context.Clients
            .Where(c => c.DeviceInfo != null)
            .ToListAsync(cancellationToken);

        foreach (var client in clients)
        {
            if (client.DeviceInfo != null && client.DeviceInfo.DiskTotal > 0)
            {
                var diskUsagePercent = (double)client.DeviceInfo.DiskUsed / client.DeviceInfo.DiskTotal * 100;
                if (diskUsagePercent > diskThreshold)
                {
                    var message = $"Device {client.Name} has low disk space: {diskUsagePercent:F1}% used";
                    return (true, "Client", client.Id, message);
                }
            }
        }

        return (false, null, null, string.Empty);
    }

    /// <summary>
    /// Evaluates data source error rule (placeholder)
    /// </summary>
    private async Task<(bool shouldTrigger, string? entityType, string? entityId, string message)> EvaluateDataSourceErrorRule(
        AlertRule rule,
        CancellationToken cancellationToken = default)
    {
        // Placeholder - would check for data source connection errors
        await Task.CompletedTask;
        return (false, null, null, string.Empty);
    }

    /// <summary>
    /// Evaluates high error rate rule (placeholder)
    /// </summary>
    private async Task<(bool shouldTrigger, string? entityType, string? entityId, string message)> EvaluateHighErrorRateRule(
        AlertRule rule,
        CancellationToken cancellationToken = default)
    {
        // Placeholder - would check application error logs
        await Task.CompletedTask;
        return (false, null, null, string.Empty);
    }

    /// <summary>
    /// Acknowledges an alert
    /// </summary>
    public async Task AcknowledgeAlertAsync(int alertId, string acknowledgedBy, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var alert = await context.Alerts.FindAsync(new object[] { alertId }, cancellationToken);
            if (alert != null)
            {
                alert.IsAcknowledged = true;
                alert.AcknowledgedAt = DateTime.UtcNow;
                alert.AcknowledgedBy = acknowledgedBy;

                await context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Alert {AlertId} acknowledged by {User}", alertId, acknowledgedBy);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging alert {AlertId}", alertId);
        }
    }

    /// <summary>
    /// Resolves an alert
    /// </summary>
    public async Task ResolveAlertAsync(int alertId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var alert = await context.Alerts.FindAsync(new object[] { alertId }, cancellationToken);
            if (alert != null)
            {
                alert.IsResolved = true;
                alert.ResolvedAt = DateTime.UtcNow;

                await context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Alert {AlertId} resolved", alertId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving alert {AlertId}", alertId);
        }
    }

    /// <summary>
    /// Gets active alerts
    /// </summary>
    public async Task<List<Alert>> GetActiveAlertsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Alerts
            .Include(a => a.AlertRule)
            .Where(a => !a.IsResolved)
            .OrderByDescending(a => a.TriggeredAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Parses rule configuration JSON
    /// </summary>
    private Dictionary<string, JsonElement> ParseConfiguration(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            return new Dictionary<string, JsonElement>();
        }

        try
        {
            var doc = JsonDocument.Parse(configJson);
            var dict = new Dictionary<string, JsonElement>();

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                dict[prop.Name] = prop.Value;
            }

            return dict;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing rule configuration");
            return new Dictionary<string, JsonElement>();
        }
    }

    /// <summary>
    /// Gets a configuration value with fallback
    /// </summary>
    private T GetConfigValue<T>(Dictionary<string, JsonElement> config, string key, T defaultValue)
    {
        if (!config.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        try
        {
            if (typeof(T) == typeof(int))
            {
                return (T)(object)value.GetInt32();
            }
            else if (typeof(T) == typeof(double))
            {
                return (T)(object)value.GetDouble();
            }
            else if (typeof(T) == typeof(string))
            {
                return (T)(object)(value.GetString() ?? string.Empty);
            }
            else if (typeof(T) == typeof(bool))
            {
                return (T)(object)value.GetBoolean();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing config value {Key}", key);
        }

        return defaultValue;
    }
}
