using System;
using System.Globalization;
using System.Windows.Data; // Required for IValueConverter

// Ensure this namespace matches your folder structure
namespace WPF_LoginForm.Converters
{
    /// <summary>
    /// Converts a boolean value to its inverse (true to false, false to true).
    /// Useful for binding IsReadOnly property to an IsEditable property.
    /// </summary>
    [ValueConversion(typeof(bool), typeof(bool))]
    public class BooleanInverterConverter : IValueConverter
    {
        /// <summary>
        /// Converts true to false and false to true.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            // Default value if conversion fails or input is not bool
            return true; // Defaulting to true usually means "read-only" in this context
        }

        /// <summary>
        /// Converts false to true and true to false (inverse of Convert).
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            // Default value if conversion fails or input is not bool
            return false;
        }
    }
}