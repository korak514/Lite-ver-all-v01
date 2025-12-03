using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data; // Required for DataTable
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

        // --- NEW: Date Logic ---
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
        private readonly bool _autoCalculateId;
        private readonly DataTable _sourceTable;

        // Designer Constructor
        public AddRowViewModel()
        {
            WindowTitle = "Add New Row (Design)";
            InitializeColumnEntries(new List<string> { "Col1", "Col2" }, null, false);
        }

        // Runtime Constructor
        public AddRowViewModel(IEnumerable<string> columnNames, string tableName, Dictionary<string, object> initialValues, DataTable sourceTable, bool hideId)
        {
            WindowTitle = $"Add New Row to '{tableName}'";
            _sourceTable = sourceTable;
            _autoCalculateId = hideId;

            // Filter out ID column if we are hiding/auto-calculating it
            var filteredColumns = hideId
                ? columnNames.Where(c => !c.Equals(_idColumnName, StringComparison.OrdinalIgnoreCase))
                : columnNames;

            // Check for Date Column to bind to the DatePicker
            var dateCol = columnNames.FirstOrDefault(c =>
                c.Equals("EntryDate", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("Date", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("Tarih", StringComparison.OrdinalIgnoreCase));

            // If we found a date column, remove it from the list (so it doesn't show as a text box)
            // We will add it back manually when saving.
            string dateColName = null;
            if (dateCol != null)
            {
                dateColName = dateCol;
                filteredColumns = filteredColumns.Where(c => c != dateColName);
            }

            InitializeColumnEntries(filteredColumns, initialValues, hideId);

            // Store the name of the date column for later retrieval
            _dateTargetColumn = dateColName;
        }

        private string _dateTargetColumn;

        private void InitializeColumnEntries(IEnumerable<string> columnNames, Dictionary<string, object> initialValues, bool hideId)
        {
            ColumnEntries = new ObservableCollection<ColumnEntry>();
            if (columnNames == null) return;

            foreach (var name in columnNames)
            {
                var entry = new ColumnEntry(name);
                if (initialValues != null && initialValues.TryGetValue(name, out object initialValue))
                {
                    entry.Value = initialValue;
                }
                ColumnEntries.Add(entry);
            }
        }

        public NewRowData GetEnteredData()
        {
            var newRow = new NewRowData();

            // 1. Add Manual Entries
            foreach (var entry in ColumnEntries)
            {
                newRow.Values[entry.ColumnName] = entry.Value;
            }

            // 2. Add Date (if applicable)
            if (!string.IsNullOrEmpty(_dateTargetColumn))
            {
                newRow.Values[_dateTargetColumn] = EntryDate;
            }

            // 3. Auto Calculate ID (Max + 1)
            if (_autoCalculateId && _sourceTable != null)
            {
                try
                {
                    // Find Max ID in the source table
                    // We assume the column is named "ID" and is Integer
                    int maxId = 0;
                    if (_sourceTable.Columns.Contains(_idColumnName))
                    {
                        var ids = _sourceTable.AsEnumerable()
                            .Select(r => r[_idColumnName])
                            .Where(v => v != null && v != DBNull.Value)
                            .Select(v => Convert.ToInt32(v))
                            .ToList();

                        if (ids.Any()) maxId = ids.Max();
                    }
                    newRow.Values[_idColumnName] = maxId + 1;
                }
                catch
                {
                    // If calculation fails (e.g. ID is not int), ignore or handle error
                }
            }

            return newRow;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}