using System;
using System.Collections; // For IList
using System.Globalization;
using System.Windows.Data;

namespace WPF_LoginForm.Converters
{
    public class IsItemInCollectionConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[0] == null || !(values[1] is IList collection))
            {
                return false;
            }

            // values[0] is the item to check
            // values[1] is the collection to check against
            return collection.Contains(values[0]);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}