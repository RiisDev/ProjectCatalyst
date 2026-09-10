using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ProjectCatalyst.Converters
{
	public sealed class BoolToVisibilityConverter : IValueConverter
	{
		public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			bool flag = value is true;
			bool invert = string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase);
			if (invert) flag = !flag;
			return flag ? Visibility.Visible : Visibility.Collapsed;
		}

		public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is Visibility.Visible;
	}
}
