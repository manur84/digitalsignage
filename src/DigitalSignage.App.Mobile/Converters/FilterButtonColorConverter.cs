using System.Globalization;

namespace DigitalSignage.App.Mobile.Converters;

/// <summary>
/// Converts filter selection to button background color
/// </summary>
public class FilterButtonColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string selectedFilter || parameter is not string buttonFilter)
            return Application.Current?.Resources["Gray500"];

        // Return Primary color if selected, Gray if not
        return selectedFilter == buttonFilter
            ? Application.Current?.Resources["Primary"]
            : Application.Current?.Resources["Gray500"];
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
