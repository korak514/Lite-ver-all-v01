// In WPF_LoginForm.Converters/IsNullOrEmptyConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;

namespace WPF_LoginForm.Converters
{
    /// <summary>
    /// Converts a string value to a boolean indicating whether the string is null or empty.
    /// Useful for enabling/disabling controls based on TextBox content.
    /// </summary>
    [ValueConversion(typeof(string), typeof(bool))]
    public class IsNullOrEmptyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrEmpty(value as string);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}