using System;
using System.Globalization;
using System.Windows.Data;

namespace DigitalSignage.Server.Converters;

/// <summary>
/// Converts boolean to On/Off string
/// </summary>
public class BoolToOnOffStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? "On" : "Off";
        }
        return "Off";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
