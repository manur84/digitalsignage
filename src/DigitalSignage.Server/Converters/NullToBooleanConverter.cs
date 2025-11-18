using System;
using System.Globalization;
using System.Windows.Data;

namespace DigitalSignage.Server.Converters;

/// <summary>
/// Converts null to false, non-null to true
/// </summary>
public class NullToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value != null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
