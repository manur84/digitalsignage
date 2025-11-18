using DigitalSignage.Data.Entities;
using System;
using System.Globalization;
using System.Windows.Data;

namespace DigitalSignage.Server.Converters;

/// <summary>
/// Converts AlertRuleType to a user-friendly string
/// </summary>
public class AlertRuleTypeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is AlertRuleType ruleType)
        {
            return ruleType switch
            {
                AlertRuleType.DeviceOffline => "Device Offline",
                AlertRuleType.DeviceHighCpu => "High CPU Usage",
                AlertRuleType.DeviceHighMemory => "High Memory Usage",
                AlertRuleType.DeviceLowDiskSpace => "Low Disk Space",
                AlertRuleType.DataSourceError => "Data Source Error",
                AlertRuleType.HighErrorRate => "High Error Rate",
                AlertRuleType.LayoutUpdateFailed => "Layout Update Failed",
                AlertRuleType.Custom => "Custom Rule",
                _ => ruleType.ToString()
            };
        }

        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
