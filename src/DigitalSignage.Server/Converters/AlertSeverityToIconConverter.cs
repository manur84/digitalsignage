using DigitalSignage.Data.Entities;
using System;
using System.Globalization;
using System.Windows.Data;

namespace DigitalSignage.Server.Converters;

/// <summary>
/// Converts AlertSeverity to an icon/emoji for UI display
/// </summary>
public class AlertSeverityToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is AlertSeverity severity)
        {
            return severity switch
            {
                AlertSeverity.Info => "ℹ️",
                AlertSeverity.Warning => "⚠️",
                AlertSeverity.Error => "❌",
                AlertSeverity.Critical => "🔴",
                _ => "•"
            };
        }

        return "•";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
