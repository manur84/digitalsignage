using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DigitalSignage.Core.Models;

namespace DigitalSignage.Server.Converters;

/// <summary>
/// Converts LogLevel to a color for UI display
/// </summary>
public class LogLevelToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is LogLevel level)
        {
            return level switch
            {
                LogLevel.Debug => new SolidColorBrush(Color.FromRgb(128, 128, 128)), // Gray
                LogLevel.Info => new SolidColorBrush(Color.FromRgb(0, 122, 204)), // Blue
                LogLevel.Warning => new SolidColorBrush(Color.FromRgb(255, 140, 0)), // Orange
                LogLevel.Error => new SolidColorBrush(Color.FromRgb(220, 20, 60)), // Red
                LogLevel.Critical => new SolidColorBrush(Color.FromRgb(139, 0, 0)), // Dark Red
                _ => new SolidColorBrush(Colors.Black)
            };
        }

        return new SolidColorBrush(Colors.Black);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts LogLevel to a background color for row highlighting
/// </summary>
public class LogLevelToBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is LogLevel level)
        {
            return level switch
            {
                LogLevel.Debug => new SolidColorBrush(Color.FromArgb(20, 128, 128, 128)), // Light Gray
                LogLevel.Info => new SolidColorBrush(Colors.Transparent),
                LogLevel.Warning => new SolidColorBrush(Color.FromArgb(30, 255, 140, 0)), // Light Orange
                LogLevel.Error => new SolidColorBrush(Color.FromArgb(30, 220, 20, 60)), // Light Red
                LogLevel.Critical => new SolidColorBrush(Color.FromArgb(50, 139, 0, 0)), // Light Dark Red
                _ => new SolidColorBrush(Colors.Transparent)
            };
        }

        return new SolidColorBrush(Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts LogLevel to a string for display
/// </summary>
public class LogLevelToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is LogLevel level)
        {
            return level.ToString().ToUpper();
        }

        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
