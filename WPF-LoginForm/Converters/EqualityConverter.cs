// In WPF_LoginForm.Converters/EqualityConverter.cs
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace WPF_LoginForm.Converters
{
    // This converter now implements both IValueConverter and IMultiValueConverter
    public class EqualityConverter : IValueConverter, IMultiValueConverter
    {
        // --- Single Value Conversion ---
        // Used for single bindings, like checking if ChartType equals "Bar"
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString()?.Equals(parameter?.ToString(), StringComparison.OrdinalIgnoreCase) ?? false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
            {
                return parameter;
            }
            return DependencyProperty.UnsetValue;
        }

        // --- NEW: Multi-Value Conversion ---
        // Used for checking if a value matches ANY of several parameters.
        // E.g., Is ChartType equal to "Line" OR "Bar"?
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
                return false;

            var inputValue = values[0]?.ToString();
            // Check if the inputValue matches any of the other values in the array
            return values.Skip(1).Any(v => v?.ToString()?.Equals(inputValue, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}