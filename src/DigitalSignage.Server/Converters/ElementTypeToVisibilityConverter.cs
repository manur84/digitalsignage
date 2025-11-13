using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DigitalSignage.Server.Converters;

/// <summary>
/// Converts element type to Visibility based on comma-separated list in ConverterParameter
/// Example: ConverterParameter="rectangle,shape,circle" will show only for these types
/// </summary>
public class ElementTypeToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return Visibility.Collapsed;

        string elementType = value.ToString()?.ToLower() ?? string.Empty;
        string allowedTypes = parameter.ToString()?.ToLower() ?? string.Empty;

        // Split by comma and check if element type is in the list
        var allowedTypesList = allowedTypes.Split(',')
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrEmpty(t));

        bool isVisible = allowedTypesList.Contains(elementType);

        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
