using System.Globalization;
using System.Windows.Data;

namespace DigitalSignage.Server.Converters;

/// <summary>
/// Converts FontStyle string to boolean for Italic toggle button
/// </summary>
public class FontStyleToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string fontStyle)
        {
            return fontStyle.Equals("Italic", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isItalic)
        {
            return isItalic ? "Italic" : "Normal";
        }
        return "Normal";
    }
}
