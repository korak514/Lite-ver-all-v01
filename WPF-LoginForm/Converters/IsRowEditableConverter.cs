using System;
using System.Collections.ObjectModel; // For ObservableCollection
using System.Data;                   // For DataRowView
using System.Globalization;
using System.Linq;                   // For Any()
using System.Windows.Data;           // For IValueConverter
using System.Diagnostics;

namespace WPF_LoginForm.Converters
{
    public class IsRowEditableConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 'value' should be the ObservableCollection<DataRowView> of editable rows from the ViewModel
            if (!(value is ObservableCollection<DataRowView> editableRowsCollection))
            {
                //Debug.WriteLine("IsRowEditableConverter (Grid IsReadOnly): Invalid binding value, defaulting to ReadOnly (true).");
                return true; // Default to DataGrid being ReadOnly if the binding is incorrect
            }

            // DataGrid is ReadOnly if the editableRowsCollection is empty.
            // If it has items, DataGrid is NOT ReadOnly, allowing individual cells to potentially be edited.
            bool gridIsReadOnly = !editableRowsCollection.Any();
            //Debug.WriteLine($"IsRowEditableConverter (Grid IsReadOnly): Grid IsReadOnly = {gridIsReadOnly}");
            return gridIsReadOnly;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Not needed for one-way IsReadOnly binding
            throw new NotSupportedException();
        }
    }
}