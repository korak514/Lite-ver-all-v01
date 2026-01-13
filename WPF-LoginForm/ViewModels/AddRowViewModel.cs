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
                // 2. If it's the ID column, determine default value based on Type
                else if (name.Equals(_idColumnName, StringComparison.OrdinalIgnoreCase) && _sourceTable != null)
                {
                    entry.Value = CalculateDefaultIdValue(name);
                }

                ColumnEntries.Add(entry);
            }
        }

        // FIX: Replaced CalculateNextId with type-aware logic
        private object CalculateDefaultIdValue(string colName)
        {
            try
            {
                if (!_sourceTable.Columns.Contains(colName)) return null;

                Type type = _sourceTable.Columns[colName].DataType;

                // Case A: GUID
                if (type == typeof(Guid))
                {
                    return Guid.NewGuid();
                }

                // Case B: Integers (Auto-Increment logic)
                if (type == typeof(int) || type == typeof(long) || type == typeof(short))
                {
                    var ids = _sourceTable.AsEnumerable()
                        .Select(r => r[colName])
                        .Where(val => val != null && val != DBNull.Value)
                        .Select(val =>
                        {
                            if (long.TryParse(val.ToString(), out long id)) return id;
                            return 0L;
                        })
                        .ToList();

                    if (ids.Any())
                    {
                        return ids.Max() + 1;
                    }
                    return 1;
                }
            }
            catch { }

            // Case C: Strings or others -> Leave blank for user input
            return null;
        }

        public bool ValidateData(out string errorMsg)
        {
            errorMsg = "";
            if (_sourceTable == null) return true;

            foreach (var entry in ColumnEntries)
            {
                if (!_sourceTable.Columns.Contains(entry.ColumnName)) continue;
                DataColumn col = _sourceTable.Columns[entry.ColumnName];

                string strVal = entry.Value?.ToString();

                // Check for Nulls on Required Columns
                if (string.IsNullOrWhiteSpace(strVal))
                {
                    if (!col.AllowDBNull && !col.AutoIncrement)
                    {
                        errorMsg = $"Column '{entry.ColumnName}' is required.";
                        return false;
                    }
                    continue;
                }

                // Check Type Compatibility
                try
                {
                    // Special handling for GUID strings
                    if (col.DataType == typeof(Guid))
                    {
                        Guid.Parse(strVal);
                    }
                    else
                    {
                        Convert.ChangeType(entry.Value, col.DataType);
                    }
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

            foreach (var entry in ColumnEntries)
            {
                newRowData.Values[entry.ColumnName] = entry.Value;
            }

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