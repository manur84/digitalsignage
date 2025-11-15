using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DigitalSignage.Server.Views;

public class ElementSelectionThicknessConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return new Thickness(1);

        return ReferenceEquals(values[0], values[1]) ? new Thickness(2) : new Thickness(1);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
