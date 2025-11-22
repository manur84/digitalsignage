using System.Globalization;

namespace DigitalSignage.App.Mobile.Converters;

/// <summary>
/// Converts a value to true if it is not null, false otherwise.
/// Useful for visibility bindings.
/// </summary>
public class IsNotNullConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return value != null;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
