using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace DigitalSignage.Server.Converters;

/// <summary>
/// Converts enum value to visibility based on parameter match
/// </summary>
public class EnumVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return Visibility.Collapsed;

        var enumValue = value.ToString();
        var targetValues = parameter.ToString()?.Split(',').Select(s => s.Trim()).ToList();

        if (targetValues != null && targetValues.Contains(enumValue))
            return Visibility.Visible;

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
