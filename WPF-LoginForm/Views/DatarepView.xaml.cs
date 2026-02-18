using System.Windows;
using System.Windows.Controls;
using System.Data;
using WPF_LoginForm.ViewModels;

namespace WPF_LoginForm.Views
{
    /// <summary>
    /// Interaction logic for DatarepView.xaml
    /// </summary>
    public partial class DatarepView : UserControl
    {
        public DatarepView()
        {
            InitializeComponent();
        }

        // --- CRITICAL FIX: This restricts editing to ONLY the rows in the "EditableRows" list ---
        private void DataDisplayGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            // 1. Get the ViewModel to access the allowed 'EditableRows' list
            if (this.DataContext is DatarepViewModel viewModel)
            {
                // 2. Get the specific row the user is trying to edit
                var rowView = e.Row.Item as DataRowView;

                // 3. Check if this row is inside the allowed list
                // If the list is null, or the row isn't in it, we BLOCK the edit.
                if (viewModel.EditableRows == null || !viewModel.EditableRows.Contains(rowView))
                {
                    e.Cancel = true; // This stops the cell from entering edit mode
                }
            }
            else
            {
                // Safety: If no ViewModel is found, prevent editing to avoid bugs
                e.Cancel = true;
            }
        }

        // Logic for the "Select All" button in the toolbar
        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            DataDisplayGrid.SelectAll();
        }

        // Handles auto-generation of columns.
        // We leave this simple, but you can add date formatting here if needed.
        private void DataDisplayGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            // Example: If you want to format all dates nicely
            // if (e.PropertyType == typeof(System.DateTime))
            // {
            //     (e.Column as DataGridTextColumn).Binding.StringFormat = "dd.MM.yyyy HH:mm";
            // }
        }

        // Handles sorting. Default behavior is usually fine.
        private void DataDisplayGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            // Leave empty to use default DataGrid sorting,
            // or add custom logic here if you need to sort specific columns differently.
        }
    }
}