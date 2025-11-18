using System;
using System.Globalization;
using System.Windows.Data;

namespace DigitalSignage.Server.Converters;

/// <summary>
/// Converts progress percentage (0-100) to actual pixel width for progress bar
/// </summary>
public class ProgressWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length != 2 || values[0] is not double progress || values[1] is not double maxWidth)
        {
            return 0.0;
        }

        // Clamp progress between 0 and 100
        progress = Math.Clamp(progress, 0, 100);

        // Calculate width based on percentage
        return (maxWidth * progress) / 100.0;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
