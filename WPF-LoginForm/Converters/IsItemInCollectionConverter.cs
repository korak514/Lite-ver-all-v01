using System;
using System.Collections;
using System.Globalization;
using System.Windows.Data;

namespace WPF_LoginForm.Converters
{
    public class IsItemInCollectionConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // Value[0] = The current Row (DataRowView)
            // Value[1] = The EditableRows collection from ViewModel

            if (values.Length < 2 || values[0] == null || values[1] == null)
                return false;

            var item = values[0];
            var collection = values[1] as IList;

            if (collection != null && collection.Contains(item))
            {
                return true; // This triggers the DataTrigger in XAML to change background color
            }

            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}