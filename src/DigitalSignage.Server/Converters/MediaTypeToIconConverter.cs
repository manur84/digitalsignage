using System;
using System.Globalization;
using System.Windows.Data;
using DigitalSignage.Data.Entities;

namespace DigitalSignage.Server.Converters;

/// <summary>
/// Converts MediaType enum to icon string
/// </summary>
public class MediaTypeToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MediaType mediaType)
        {
            return mediaType switch
            {
                MediaType.Image => "🖼",
                MediaType.Video => "🎥",
                MediaType.Audio => "🔊",
                MediaType.Document => "📄",
                MediaType.Other => "📎",
                _ => "📎"
            };
        }
        return "📎";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
