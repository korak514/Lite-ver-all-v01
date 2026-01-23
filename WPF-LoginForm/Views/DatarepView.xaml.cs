using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Data; // Required for DataTable/DataColumn
using System.ComponentModel;
using WPF_LoginForm.ViewModels;
using WPF_LoginForm.Converters; // Ensure this matches your namespace

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
            string lowerName = columnName.ToLower();

            // 1. Get Access to the Real Data Schema
            // The DataGrid often guesses "object", so we check the ViewModel's Table directly.
            Type realColumnType = typeof(string); // Default
            bool isReadOnlyInDb = false;

            if (this.DataContext is DatarepViewModel vm && vm.DataTableView != null)
            {
                if (vm.DataTableView.Table.Columns.Contains(columnName))
                {
                    var dtCol = vm.DataTableView.Table.Columns[columnName];
                    realColumnType = dtCol.DataType;
                    isReadOnlyInDb = dtCol.ReadOnly || dtCol.AutoIncrement;
                }
            }

            // 2. ID Column Logic (Lock it down)
            if (columnName.Equals("ID", StringComparison.OrdinalIgnoreCase))
            {
                e.Column.DisplayIndex = 0;

                // Visibility
                Binding visBinding = new Binding("IsIdVisible")
                {
                    Source = this.DataContext,
                    Converter = new Converters.BooleanToVisibilityConverter()
                };
                BindingOperations.SetBinding(e.Column, DataGridColumn.VisibilityProperty, visBinding);

                // ReadOnly (Force True)
                e.Column.IsReadOnly = true;
            }
            // 3. Generic Column Logic
            else if (e.Column is DataGridTextColumn textColumn)
            {
                // Apply ReadOnly if DB says so (e.g. Calculated columns)
                if (isReadOnlyInDb)
                {
                    textColumn.IsReadOnly = true;
                    // Switch to OneWay to prevent UI from trying to push updates
                    if (textColumn.Binding is Binding b) b.Mode = BindingMode.OneWay;
                }

                // --- TIME & DATE FORMATTING ---
                // We check the REAL type from the DataTable, not the 'e.PropertyType'
                if (realColumnType == typeof(DateTime))
                {
                    // Detect Time-Only columns
                    if (lowerName.Contains("saat") ||
                        lowerName.Contains("time") ||
                        lowerName.Contains("süre") ||
                        lowerName.Contains("duration") ||
                        lowerName.Contains("bitiş") ||
                        lowerName.Contains("başlangıç") ||
                        lowerName.Contains("start") ||
                        lowerName.Contains("end") ||
                        lowerName.Contains("duraklama"))
                    {
                        // FORCE Time Format
                        textColumn.Binding.StringFormat = "HH:mm";
                    }
                    else
                    {
                        // Standard Date Format
                        textColumn.Binding.StringFormat = "dd.MM.yyyy HH:mm";
                    }
                }
                // Handle TimeSpan (Postgres Intervals)
                else if (realColumnType == typeof(TimeSpan))
                {
                    textColumn.Binding.StringFormat = @"hh\:mm";
                }
            }
        }

        private void DataDisplayGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            if (this.DataContext is DatarepViewModel viewModel)
            {
                // 1. Check Column ReadOnly state
                if (e.Column.IsReadOnly)
                {
                    e.Cancel = true;
                    return;
                }

                // 2. Check "Edit Mode" (User must click 'Edit' button first)
                if (e.Row != null && e.Row.Item is DataRowView drv)
                {
                    if (viewModel.EditableRows == null || !viewModel.EditableRows.Contains(drv))
                    {
                        e.Cancel = true;
                    }
                }
            }
        }

        private void DataDisplayGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            if (DataContext is DatarepViewModel viewModel && viewModel.DataTableView != null)
            {
                var column = e.Column;
                string sortBy = column.SortMemberPath;
                if (string.IsNullOrEmpty(sortBy)) return;

                ListSortDirection direction = (column.SortDirection != ListSortDirection.Ascending)
                                               ? ListSortDirection.Ascending
                                               : ListSortDirection.Descending;
                try
                {
                    // Handle spaces/brackets in column names for sorting
                    string sortExpression = sortBy;
                    if (sortBy.Any(c => char.IsWhiteSpace(c) || "()[]{}%#&+-*/\\".Contains(c)))
                    {
                        sortExpression = $"[{sortBy.Replace("]", "]]")}]";
                    }

                    viewModel.DataTableView.Sort = $"{sortExpression} {(direction == ListSortDirection.Ascending ? "ASC" : "DESC")}";
                    column.SortDirection = direction;
                }
                catch { }
                finally { e.Handled = true; }
            }
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataDisplayGrid.Items.Count > 0)
            {
                if (DataDisplayGrid.SelectedItems.Count == DataDisplayGrid.Items.Count)
                    DataDisplayGrid.UnselectAll();
                else
                    DataDisplayGrid.SelectAll();
            }
            DataDisplayGrid.Focus();
        }
    }
}