using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Linq;
using System.Data;
using WPF_LoginForm.ViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;
using WPF_LoginForm.Converters;

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

            // --- LOGIC FOR ID COLUMN ---
            if (columnName.Equals("ID", StringComparison.OrdinalIgnoreCase))
            {
                // 1. Move ID to the far Left (Visual Fix)
                e.Column.DisplayIndex = 0;

                // 2. Bind Visibility to 'IsIdVisible'
                Binding visBinding = new Binding("IsIdVisible")
                {
                    Source = this.DataContext,
                    Converter = new WPF_LoginForm.Converters.BooleanToVisibilityConverter()
                };
                BindingOperations.SetBinding(e.Column, DataGridColumn.VisibilityProperty, visBinding);

                // 3. Bind IsReadOnly to 'IsIdEditable'
                Binding readOnlyBinding = new Binding("IsIdEditable")
                {
                    Source = this.DataContext,
                    Converter = new BooleanInverterConverter()
                };
                BindingOperations.SetBinding(e.Column, DataGridColumn.IsReadOnlyProperty, readOnlyBinding);
            }
            else if (e.Column is DataGridTextColumn textColumn)
            {
                // Default behavior for other columns
                BindingMode bindingMode = BindingMode.TwoWay;

                if (dataColumn != null && (dataColumn.AutoIncrement || dataColumn.ReadOnly))
                {
                    bindingMode = BindingMode.OneWay;
                    textColumn.IsReadOnly = true;
                }

                Binding newBinding = new Binding()
                {
                    Path = new PropertyPath($"[{columnName}]"),
                    Mode = bindingMode,
                    UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
                };
                textColumn.Binding = newBinding;
            }

            if (e.PropertyType == typeof(DateTime))
            {
                if (e.Column is DataGridTextColumn dateTextColumn)
                {
                    if (dateTextColumn.Binding is Binding dateBinding)
                    {
                        dateBinding.StringFormat = "d";
                    }
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
                    e.Handled = true;
                    return;
                }

                ListSortDirection direction = (column.SortDirection != ListSortDirection.Ascending)
                                               ? ListSortDirection.Ascending
                                               : ListSortDirection.Descending;

                try
                {
                    string sortExpression;

                    // FIX: Handle column names with spaces or special chars
                    if (sortBy.Any(c => char.IsWhiteSpace(c) || "()[]{}%#&+-*/\\".Contains(c)))
                    {
                        // Escape existing brackets and wrap in brackets
                        sortExpression = $"[{sortBy.Replace("]", "]]")}] {(direction == ListSortDirection.Ascending ? "ASC" : "DESC")}";
                    }
                    else
                    {
                        sortExpression = $"{sortBy} {(direction == ListSortDirection.Ascending ? "ASC" : "DESC")}";
                    }

                    // Apply sort to the underlying DataView
                    viewModel.DataTableView.Sort = sortExpression;

                    // Update UI arrow
                    column.SortDirection = direction;
                }
                catch (Exception ex)
                {
                    // Prevent crash if sort fails
                    System.Diagnostics.Debug.WriteLine($"Sort Error: {ex.Message}");
                }
                finally
                {
                    // Mark handled to prevent default DataGrid sort which fights with DataView sort
                    e.Handled = true;
                }
            }
            else
            {
                e.Handled = true;
            }
        }

        private void CheckBox_Checked()
        {
        }
    }
}