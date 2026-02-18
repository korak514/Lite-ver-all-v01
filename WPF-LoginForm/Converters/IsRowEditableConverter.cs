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
            // Value is the 'EditableRows' collection from the ViewModel.
            // We are binding to 'DataGrid.IsReadOnly'.

            // Logic:
            // If the collection has items, IsReadOnly = false (so we can edit).
            // If the collection is empty, IsReadOnly = true (locked).

            if (value is ICollection collection && collection.Count > 0)
            {
                return false;
            }

            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}