using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DigitalSignage.Server.Converters;

/// <summary>
/// Converts null values to Visibility (inverted). Null = Collapsed, Not Null = Visible
/// </summary>
public class InverseNullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value == null ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
