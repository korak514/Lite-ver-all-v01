// In WPF_LoginForm.Views.DatarepView.xaml.cs

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Linq;
using System.Data;
using WPF_LoginForm.ViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace WPF_LoginForm.Views
{
    public partial class DatarepView : UserControl
    {
        public DatarepView()
        {
            InitializeComponent();
        }

        private void DataDisplayGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            string columnName = e.PropertyName;

            e.Column.SortMemberPath = columnName;

            DataColumn dataColumn = null;
            if (this.DataContext is DatarepViewModel vm && vm.DataTableView?.Table != null && vm.DataTableView.Table.Columns.Contains(columnName))
            {
                dataColumn = vm.DataTableView.Table.Columns[columnName];
            }


            if (e.Column is DataGridTextColumn textColumn)
            {
                BindingMode bindingMode = BindingMode.TwoWay; // Default to TwoWay

                // --- MODIFICATION FOR READ-ONLY/IDENTITY COLUMNS ---
                if (dataColumn != null && (dataColumn.AutoIncrement || dataColumn.ReadOnly || columnName.Equals("ID", StringComparison.OrdinalIgnoreCase)))
                {
                    bindingMode = BindingMode.OneWay; // Change to OneWay for read-only columns
                    textColumn.IsReadOnly = true;     // Also explicitly make the DataGrid column read-only
                    // Optional: Style read-only columns differently (e.g., lighter background)
                    // textColumn.CellStyle = FindResource("ReadOnlyCellStyle") as Style; 
                }
                // --- END OF MODIFICATION ---

                Binding newBinding = new Binding()
                {
                    Path = new PropertyPath($"[{columnName}]"),
                    Mode = bindingMode, // Use determined binding mode
                    UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
                };
                textColumn.Binding = newBinding;
            }

            if (e.PropertyType == typeof(DateTime)) // This uses e.PropertyType from the event args
            {
                if (e.Column is DataGridTextColumn dateTextColumn)
                {
                    if (dateTextColumn.Binding is Binding dateBinding)
                    {
                        dateBinding.StringFormat = "d";
                    }
                    // For DateTime columns, also consider making them read-only in the grid if editing is done via DatePicker
                    // or if they are not meant to be directly text-edited.
                    // textColumn.IsReadOnly = true; // If you want dates to be non-editable directly in the cell
                }
            }
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataDisplayGrid.Items.Count > 0)
            {
                if (DataDisplayGrid.SelectedItems.Count == DataDisplayGrid.Items.Count)
                {
                    DataDisplayGrid.UnselectAll();
                }
                else
                {
                    DataDisplayGrid.SelectAll();
                }
            }
            DataDisplayGrid.Focus();
        }

        private void DataDisplayGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            if (this.DataContext is DatarepViewModel viewModel)
            {
                // Explicitly check if the column itself is marked as read-only in the DataGrid
                if (e.Column.IsReadOnly)
                {
                    e.Cancel = true;
                    return;
                }

                if (e.Row != null && e.Row.Item is DataRowView drv)
                {
                    if (viewModel.EditableRows != null && viewModel.EditableRows.Contains(drv))
                    {
                        e.Cancel = false;
                    }
                    else
                    {
                        e.Cancel = true;
                    }
                }
                else
                {
                    e.Cancel = true;
                }
            }
            else
            {
                e.Cancel = true;
            }
        }

        private void DataDisplayGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            if (DataContext is DatarepViewModel viewModel && viewModel.DataTableView != null)
            {
                DataGridColumn column = e.Column;
                string sortBy = column.SortMemberPath;

                if (string.IsNullOrEmpty(sortBy))
                {
                    viewModel.SetErrorMessage($"Cannot sort: Column '{column.Header}' has no SortMemberPath defined.");
                    e.Handled = true;
                    return;
                }

                ListSortDirection direction = (column.SortDirection != ListSortDirection.Ascending)
                                               ? ListSortDirection.Ascending
                                               : ListSortDirection.Descending;
                try
                {
                    string sortExpression;
                    if (sortBy.Any(c => char.IsWhiteSpace(c) || "()[]{}%#&+-*/\\".Contains(c)))
                    {
                        sortExpression = $"[{sortBy.Replace("]", "]]")}] {(direction == ListSortDirection.Ascending ? "ASC" : "DESC")}";
                    }
                    else
                    {
                        sortExpression = $"{sortBy} {(direction == ListSortDirection.Ascending ? "ASC" : "DESC")}";
                    }

                    viewModel.DataTableView.Sort = sortExpression;
                    viewModel.SetErrorMessage(null);

                    column.SortDirection = direction;
                }
                catch (Exception ex)
                {
                    string errorMsg = $"Error sorting by column '{column.Header}': {ex.Message}";
                    viewModel.SetErrorMessage(errorMsg);
                }
                finally
                {
                    e.Handled = true;
                }
            }
            else
            {
                e.Handled = true;
            }
        }
    }
}