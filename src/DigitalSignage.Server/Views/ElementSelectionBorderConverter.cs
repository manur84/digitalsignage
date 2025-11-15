using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DigitalSignage.Server.Views;

public class ElementSelectionBorderConverter : IMultiValueConverter
{
    private static readonly SolidColorBrush SelectedBrush = new(Color.FromRgb(22, 119, 255));
    private static readonly SolidColorBrush UnselectedBrush = new(Color.FromArgb(100, 80, 80, 80));

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return UnselectedBrush;

        return ReferenceEquals(values[0], values[1]) ? SelectedBrush : UnselectedBrush;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
