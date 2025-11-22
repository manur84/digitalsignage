using System.Globalization;

namespace DigitalSignage.App.Mobile.Converters;

/// <summary>
/// Converts a value to true if it is null, false otherwise.
/// Useful for visibility bindings.
/// </summary>
public class IsNullConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return value == null;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
