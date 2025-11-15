using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DigitalSignage.Server.Views;

/// <summary>
/// Shows content only when the bound element represents text-like content.
/// </summary>
public class TextTypeToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var type = value?.ToString()?.ToLowerInvariant() ?? string.Empty;
        return type == "text" ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
