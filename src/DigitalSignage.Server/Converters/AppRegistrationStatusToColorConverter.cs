using DigitalSignage.Core.Models;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DigitalSignage.Server.Converters;

/// <summary>
/// Converts AppRegistrationStatus enum to a color brush
/// </summary>
public class AppRegistrationStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is AppRegistrationStatus status)
        {
            return status switch
            {
                AppRegistrationStatus.Pending => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9800")), // Orange
                AppRegistrationStatus.Approved => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50")), // Green
                AppRegistrationStatus.Rejected => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336")), // Red
                AppRegistrationStatus.Revoked => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9E9E9E")), // Gray
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
