using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using WPF_LoginForm.Models;

namespace WPF_LoginForm.ViewModels
{
    public class AddRowViewModel : INotifyPropertyChanged
    {
        private string _windowTitle;

        public string WindowTitle
        {
            get => _windowTitle;
            set { _windowTitle = value; OnPropertyChanged(); }
        }

        // --- Date Logic ---
        private bool _isDateToday = true;

        public bool IsDateToday
        {
            get => _isDateToday;
            set
            {
                if (_isDateToday != value)
                {
                    _isDateToday = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsDatePickerEnabled));
                    if (value) EntryDate = DateTime.Today;
                }
            }
        }

        public bool IsDatePickerEnabled => !_isDateToday;

        private DateTime _entryDate = DateTime.Today;

        public DateTime EntryDate
        {
            get => _entryDate;
            set { _entryDate = value; OnPropertyChanged(); }
        }

        public ObservableCollection<ColumnEntry> ColumnEntries { get; private set; }

        private readonly string _idColumnName = "ID";
        private readonly DataTable _sourceTable;
        private string _dateTargetColumn;

        // Designer Constructor
        public AddRowViewModel()
        {
            WindowTitle = "Add New Row (Design)";
            InitializeColumnEntries(new List<string> { "Col1", "Col2" }, null);
        }

        // Runtime Constructor
        public AddRowViewModel(IEnumerable<string> columnNames, string tableName, Dictionary<string, object> initialValues, DataTable sourceTable, bool hideId)
        {
            WindowTitle = $"Add New Row to '{tableName}'";
            _sourceTable = sourceTable;

            var columnsToDisplay = columnNames.ToList();

            // Detect Date Column to bind to the DatePicker
            var dateCol = columnsToDisplay.FirstOrDefault(c =>
                c.Equals("EntryDate", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("Date", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("Tarih", StringComparison.OrdinalIgnoreCase));

            if (dateCol != null)
            {
                _dateTargetColumn = dateCol;
                columnsToDisplay.Remove(dateCol); // Remove from generic list so it doesn't show twice
            }

            InitializeColumnEntries(columnsToDisplay, initialValues);
        }

        private void InitializeColumnEntries(IEnumerable<string> columnNames, Dictionary<string, object> initialValues)
        {
            ColumnEntries = new ObservableCollection<ColumnEntry>();
            if (columnNames == null) return;

            foreach (var name in columnNames)
            {
                var entry = new ColumnEntry(name);

                // 1. Check if specific initial value provided
                if (initialValues != null && initialValues.TryGetValue(name, out object initialValue))
                {
                    entry.Value = initialValue;
                }
                // 2. If it's the ID column, calculate Max + 1 default
                else if (name.Equals(_idColumnName, StringComparison.OrdinalIgnoreCase) && _sourceTable != null)
                {
                    entry.Value = CalculateNextId();
                }

                ColumnEntries.Add(entry);
            }
        }

        private object CalculateNextId()
        {
            try
            {
                if (_sourceTable.Columns.Contains(_idColumnName))
                {
                    // Find max ID safely
                    var ids = _sourceTable.AsEnumerable()
                        .Select(r => r[_idColumnName])
                        .Where(val => val != null && val != DBNull.Value)
                        .Select(val =>
                        {
                            if (int.TryParse(val.ToString(), out int id)) return id;
                            return 0;
                        })
                        .ToList();

                    if (ids.Any())
                    {
                        return ids.Max() + 1;
                    }
                }
            }
            catch { }
            return 1; // Default if table empty
        }

        // --- NEW: Validation Method ---
        public bool ValidateData(out string errorMsg)
        {
            errorMsg = "";
            if (_sourceTable == null) return true; // Cannot validate without schema

            foreach (var entry in ColumnEntries)
            {
                // 1. Find the column definition
                if (!_sourceTable.Columns.Contains(entry.ColumnName)) continue;
                DataColumn col = _sourceTable.Columns[entry.ColumnName];

                string strVal = entry.Value?.ToString();

                // 2. Check for Nulls on Required Columns
                if (string.IsNullOrWhiteSpace(strVal))
                {
                    if (!col.AllowDBNull)
                    {
                        errorMsg = $"Column '{entry.ColumnName}' is required.";
                        return false;
                    }
                    continue; // Null is allowed, skip type check
                }

                // 3. Check Type Compatibility
                try
                {
                    // Try to convert. If it fails, it throws an exception.
                    var converted = Convert.ChangeType(entry.Value, col.DataType);
                }
                catch
                {
                    errorMsg = $"Value '{strVal}' is not valid for column '{entry.ColumnName}' (Expected: {col.DataType.Name}).";
                    return false;
                }
            }
            return true;
        }

        public NewRowData GetEnteredData()
        {
            var newRowData = new NewRowData();

            // 1. Add Manual Entries
            foreach (var entry in ColumnEntries)
            {
                newRowData.Values[entry.ColumnName] = entry.Value;
            }

            // 2. Add Date (if applicable)
            if (!string.IsNullOrEmpty(_dateTargetColumn))
            {
                newRowData.Values[_dateTargetColumn] = EntryDate;
            }

            return newRowData;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}