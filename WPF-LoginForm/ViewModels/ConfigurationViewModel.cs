using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Input;
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

        private string _selectedXAxis;
        public string SelectedXAxis
        {
            get { return _selectedXAxis; }
            set
            {
                _selectedXAxis = value;
                OnPropertyChanged(nameof(SelectedXAxis));
            }
        }

        private string _selectedYAxis;
        public string SelectedYAxis
        {
            get { return _selectedYAxis; }
            set
            {
                _selectedYAxis = value;
                OnPropertyChanged(nameof(SelectedYAxis));
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

        private readonly IDataRepository _dataRepository;

        public ConfigurationViewModel()
        {
            _dataRepository = new DataRepository(new FileLogger());
            OkCommand = new ViewModelCommand(p => { /* Save configuration and close dialog */ });
            CancelCommand = new ViewModelCommand(p => { /* Close dialog */ });

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
        }
    }
}
