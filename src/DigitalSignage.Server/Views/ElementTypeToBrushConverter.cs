using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DigitalSignage.Server.Views;

/// <summary>
/// Provides lightweight background colors for element previews.
/// </summary>
public class ElementTypeToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var type = value?.ToString()?.ToLowerInvariant() ?? string.Empty;

        return type switch
        {
            "text" => new SolidColorBrush(Color.FromRgb(255, 248, 220)),
            "rectangle" or "shape" => new SolidColorBrush(Color.FromRgb(230, 247, 255)),
            "circle" => new SolidColorBrush(Color.FromRgb(235, 251, 238)),
            "image" => new SolidColorBrush(Color.FromRgb(254, 245, 231)),
            "datetime" => new SolidColorBrush(Color.FromRgb(240, 244, 248)),
            "qrcode" => new SolidColorBrush(Color.FromRgb(245, 245, 245)),
            "table" => new SolidColorBrush(Color.FromRgb(236, 243, 239)),
            _ => new SolidColorBrush(Color.FromRgb(248, 248, 248))
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
