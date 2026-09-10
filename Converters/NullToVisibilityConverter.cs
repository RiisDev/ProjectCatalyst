using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ProjectCatalyst.Converters
{
	internal class NullToVisibilityConverter : IValueConverter
	{
		public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			bool hasValue = value switch
			{
				null => false,
				string s => !string.IsNullOrWhiteSpace(s), 
				_ => true
			};
			bool invert = string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase); 
			if (invert) 
				hasValue = !hasValue; 
			return hasValue ? Visibility.Visible : Visibility.Collapsed;
		} public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
	}
}
