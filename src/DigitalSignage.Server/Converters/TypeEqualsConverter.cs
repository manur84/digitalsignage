using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DigitalSignage.Server.Converters
{
    /// <summary>
    /// Converts element type to visibility based on expected type parameter
    /// </summary>
    public class TypeEqualsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return Visibility.Collapsed;

            string type = value.ToString()?.ToLower() ?? "";
            string expectedType = parameter.ToString()?.ToLower() ?? "";

            return type == expectedType ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts element type to visibility if it matches any of the comma-separated types
    /// </summary>
    public class TypeEqualsAnyConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2) return Visibility.Collapsed;

            string type = values[0]?.ToString()?.ToLower() ?? "";
            string allowedTypes = values[1]?.ToString()?.ToLower() ?? "";

            string[] types = allowedTypes.Split(',');
            foreach (var t in types)
            {
                if (type == t.Trim())
                    return Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
