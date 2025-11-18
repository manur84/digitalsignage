using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DigitalSignage.Server.Converters;

/// <summary>
/// Multi-value converter that shows screenshot image only when not loading and image is available
/// </summary>
public class ScreenshotVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2)
        {
            bool isLoading = values[0] is bool loading && loading;
            bool hasImage = values[1] != null;

            // Show image only if not loading and has image
            return !isLoading && hasImage ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Multi-value converter that shows "No Image" message only when not loading and no image is available
/// </summary>
public class NoImageVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2)
        {
            bool isLoading = values[0] is bool loading && loading;
            bool hasImage = values[1] != null;

            // Show "No Image" message only if not loading and no image
            return !isLoading && !hasImage ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
