using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Input;
using WPF_LoginForm.Models;
using WPF_LoginForm.Repositories;
using WPF_LoginForm.Services;

namespace WPF_LoginForm.ViewModels
{
    public class ConfigurationViewModel : ViewModelBase
    {
        private List<string> _tables;
        public List<string> Tables
        {
            get { return _tables; }
            set
            {
                _tables = value;
                OnPropertyChanged(nameof(Tables));
            }
        }

        private List<string> _columns;
        public List<string> Columns
        {
            get { return _columns; }
            set
            {
                _columns = value;
                OnPropertyChanged(nameof(Columns));
            }
        }

        private List<SelectableColumn> _selectableColumns;
        public List<SelectableColumn> SelectableColumns
        {
            get { return _selectableColumns; }
            set
            {
                _selectableColumns = value;
                OnPropertyChanged(nameof(SelectableColumns));
            }
        }

        private string _selectedXAxisColumn;
        public string SelectedXAxisColumn
        {
            get { return _selectedXAxisColumn; }
            set
            {
                _selectedXAxisColumn = value;
                OnPropertyChanged(nameof(SelectedXAxisColumn));
            }
        }

        private List<string> _chartTypes;
        public List<string> ChartTypes
        {
            get { return _chartTypes; }
            set
            {
                _chartTypes = value;
                OnPropertyChanged(nameof(ChartTypes));
            }
        }

        private string _selectedChartType;
        public string SelectedChartType
        {
            get { return _selectedChartType; }
            set
            {
                _selectedChartType = value;
                OnPropertyChanged(nameof(SelectedChartType));
            }
        }

        private string _selectedTable;
        public string SelectedTable
        {
            get => _selectedTable;
            set
            {
                _selectedTable = value;
                OnPropertyChanged(nameof(SelectedTable));
                LoadColumns(value);
            }
        }

        public ICommand OkCommand { get; }
        public ICommand CancelCommand { get; }
        public Action CloseAction { get; set; }

        private readonly IDataRepository _dataRepository;

        public ConfigurationViewModel()
        {
            _dataRepository = new DataRepository(new FileLogger());
            OkCommand = new ViewModelCommand(p =>
            {
                // Here you can add any validation logic before closing
                CloseAction?.Invoke();
            });
            CancelCommand = new ViewModelCommand(p => CloseAction?.Invoke());

            LoadTables();
            ChartTypes = new List<string> { "Bar", "Line", "Pie" };
        }

        private async void LoadTables()
        {
            Tables = await _dataRepository.GetTableNamesAsync();
        }

        private async void LoadColumns(string tableName)
        {
            if (string.IsNullOrEmpty(tableName)) return;
            var dataTable = await _dataRepository.GetTableDataAsync(tableName);
            Columns = dataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
            SelectableColumns = Columns.Select(c => new SelectableColumn { Name = c }).ToList();
        }
    }
}
