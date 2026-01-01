using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WPF_LoginForm.Converters
{
    public class StringToVisibilityConverter : IValueConverter
    {
        public Visibility EmptyValue { get; set; } = Visibility.Collapsed;
        public Visibility NotEmptyValue { get; set; } = Visibility.Visible;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrEmpty(value?.ToString()) ? EmptyValue : NotEmptyValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}