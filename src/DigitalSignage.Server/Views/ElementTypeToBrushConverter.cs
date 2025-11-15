using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DigitalSignage.Server.Views;

/// <summary>
/// Maps element types to friendly background brushes so items are visible on the designer canvas.
/// </summary>
public class ElementTypeToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var type = value?.ToString()?.ToLowerInvariant() ?? string.Empty;
        return type switch
        {
            "text" => new SolidColorBrush(Color.FromRgb(249, 235, 234)),
            "rectangle" or "shape" => new SolidColorBrush(Color.FromRgb(235, 245, 251)),
            "circle" => new SolidColorBrush(Color.FromRgb(234, 247, 239)),
            "image" => new SolidColorBrush(Color.FromRgb(252, 243, 207)),
            "qrcode" => new SolidColorBrush(Color.FromRgb(242, 244, 247)),
            _ => new SolidColorBrush(Color.FromRgb(246, 246, 246))
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
