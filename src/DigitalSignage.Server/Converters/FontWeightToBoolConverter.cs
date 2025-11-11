using System.Globalization;
using System.Windows.Data;

namespace DigitalSignage.Server.Converters;

/// <summary>
/// Converts FontWeight string to boolean for Bold toggle button
/// </summary>
public class FontWeightToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string fontWeight)
        {
            return fontWeight.Equals("Bold", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isBold)
        {
            return isBold ? "Bold" : "Normal";
        }
        return "Normal";
    }
}
