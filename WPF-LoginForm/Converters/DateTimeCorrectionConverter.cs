using System;
using System.Globalization;
using System.Windows.Data;

namespace WPF_LoginForm.Converters
{
    public class DateTimeCorrectionConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || !(values[0] is DateTime dt) || !(values[1] is bool isEnabled))
                return values.Length > 0 ? values[0] : null;

            if (isEnabled && dt.Year == 1899 && dt.Month == 12 && dt.Day == 30)
            {
                return dt.ToString("HH:mm");
            }

            return dt.ToString("dd/MM/yyyy HH:mm");
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            // We usually don't need to convert back for display-only formatting in this way,
            // but if we do, we'd need to parse it back. For now, return the value as is.
            return new[] { value };
        }
    }
}
