using DigitalSignage.Data.Entities;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DigitalSignage.Server.Converters;

/// <summary>
/// Converts AlertSeverity to a color for UI display
/// </summary>
public class AlertSeverityToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is AlertSeverity severity)
        {
            return severity switch
            {
                AlertSeverity.Info => new SolidColorBrush(Color.FromRgb(33, 150, 243)), // Blue
                AlertSeverity.Warning => new SolidColorBrush(Color.FromRgb(255, 193, 7)), // Amber/Yellow
                AlertSeverity.Error => new SolidColorBrush(Color.FromRgb(255, 87, 34)), // Deep Orange
                AlertSeverity.Critical => new SolidColorBrush(Color.FromRgb(244, 67, 54)), // Red
                _ => new SolidColorBrush(Colors.Gray)
            };
        }

        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
