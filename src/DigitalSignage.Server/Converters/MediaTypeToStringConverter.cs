using System;
using System.Globalization;
using System.Windows.Data;
using DigitalSignage.Data.Entities;

namespace DigitalSignage.Server.Converters;

/// <summary>
/// Converts MediaType enum (nullable) to display string
/// </summary>
public class MediaTypeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
        {
            return "All Types";
        }

        if (value is MediaType mediaType)
        {
            return mediaType.ToString();
        }

        return value.ToString() ?? "All Types";
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            if (str == "All Types")
            {
                return null;
            }

            if (Enum.TryParse<MediaType>(str, out var result))
            {
                return result;
            }
        }
        return null;
    }
}
