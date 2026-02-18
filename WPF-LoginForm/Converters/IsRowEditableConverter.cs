using System;
using System.Collections;
using System.Globalization;
using System.Windows.Data;

namespace WPF_LoginForm.Converters
{
    public class IsRowEditableConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // The Binding is: IsReadOnly="{Binding EditableRows, Converter=...}"

            // value is the 'EditableRows' collection.
            if (value is ICollection collection)
            {
                // If we have items in the list, IsReadOnly should be FALSE (Editable).
                if (collection.Count > 0)
                {
                    return false;
                }
            }

            // If list is null or empty, IsReadOnly is TRUE (Locked).
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}