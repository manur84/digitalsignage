using System.Globalization;
using DigitalSignage.Core.Models;

namespace DigitalSignage.App.Mobile.Converters;

/// <summary>
/// Converts a DeviceStatus enum to a color.
/// </summary>
public class DeviceStatusToColorConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is DeviceStatus status)
		{
			return status switch
			{
				DeviceStatus.Online => Colors.Green,
				DeviceStatus.Offline => Colors.Gray,
				DeviceStatus.Error => Colors.Red,
				_ => Colors.Gray
			};
		}

		return Colors.Gray;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
