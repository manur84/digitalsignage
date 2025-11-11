using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DigitalSignage.Server.Converters;

/// <summary>
/// Converts test result strings to appropriate colors
/// </summary>
public class TestResultToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string result)
        {
            if (result.Contains("successful", StringComparison.OrdinalIgnoreCase) ||
                result.Contains("valid", StringComparison.OrdinalIgnoreCase) ||
                result.Contains("saved", StringComparison.OrdinalIgnoreCase))
            {
                return new SolidColorBrush(Colors.Green);
            }
            else if (result.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                     result.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                     result.Contains("invalid", StringComparison.OrdinalIgnoreCase))
            {
                return new SolidColorBrush(Colors.Red);
            }
            else if (result.Contains("testing", StringComparison.OrdinalIgnoreCase))
            {
                return new SolidColorBrush(Colors.Orange);
            }
        }
        return new SolidColorBrush(Colors.Black);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
