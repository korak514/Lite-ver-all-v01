using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using WPF_LoginForm.Models;

namespace WPF_LoginForm.ViewModels
{
    public class AddRowViewModel : INotifyPropertyChanged
    {
        // --- Make WindowTitle a Property ---
        private string _windowTitle;
        public string WindowTitle // It was likely defined only as a local variable before
        {
            get => _windowTitle;
            set { _windowTitle = value; OnPropertyChanged(); } // Add setter if needed elsewhere
        }
        // --- End Property Definition ---

        // Parameterless constructor for Designer
        public AddRowViewModel()
        {
            // *** FIX: Assign to the WindowTitle PROPERTY, not a local variable ***
            this.WindowTitle = "Add New Row (Design Time)"; // Use 'this.' for clarity if needed
            // Initialize ColumnEntries for design time
            InitializeColumnEntries(new List<string> { "SampleDateCol", "SampleTextCol" }, null);
        }

        // Modified Constructor used at runtime
        public AddRowViewModel(IEnumerable<string> columnNames, string tableName, Dictionary<string, object> initialValues)
        {
            // *** FIX: Assign to the WindowTitle PROPERTY ***
            this.WindowTitle = $"Add New Row to '{tableName}'"; // Use 'this.' for clarity if needed
            InitializeColumnEntries(columnNames, initialValues);
        }

        // Helper to create ColumnEntries and apply initial values
        private void InitializeColumnEntries(IEnumerable<string> columnNames, Dictionary<string, object> initialValues)
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

        public ObservableCollection<ColumnEntry> ColumnEntries { get; private set; }

        public NewRowData GetEnteredData()
        {
            var newRow = new NewRowData();
            foreach (var entry in ColumnEntries)
            {
                newRow.Values[entry.ColumnName] = entry.Value;
            }
            return newRow;
        }

        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}