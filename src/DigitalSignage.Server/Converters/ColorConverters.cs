using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DigitalSignage.Server.Converters;

public class HexToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrEmpty(hex))
        {
            try
            {
                return (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;
            }
            catch
            {
                return Brushes.Black;
            }
        }
        return Brushes.Black;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is SolidColorBrush brush)
        {
            return brush.Color.ToString();
        }
        return "#000000";
    }
}

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            return status.ToLower() switch
            {
                "online" => Brushes.LimeGreen,
                "offline" => Brushes.Red,
                "error" => Brushes.OrangeRed,
                "updating" => Brushes.Orange,
                _ => Brushes.Gray
            };
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
