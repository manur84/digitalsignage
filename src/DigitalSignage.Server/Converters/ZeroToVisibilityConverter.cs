using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DigitalSignage.Server.Converters;

/// <summary>
/// Converts a count to visibility (Visible if count = 0, Collapsed if count > 0)
/// Opposite of CountToVisibilityConverter - useful for "empty state" messages
/// </summary>
public class ZeroToVisibilityConverter : IValueConverter
{
    public static ZeroToVisibilityConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
