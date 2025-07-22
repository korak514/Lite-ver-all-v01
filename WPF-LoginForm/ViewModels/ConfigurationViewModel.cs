using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Input;

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

        public ICommand OkCommand { get; }
        public ICommand CancelCommand { get; }

        private readonly Repositories.IDataRepository _dataRepository;

        public ConfigurationViewModel()
        {
            _dataRepository = new Repositories.DataRepository(new Services.FileLogger());
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
            var dataTable = await _dataRepository.GetTableDataAsync(tableName);
            Columns = dataTable.Columns.Cast<System.Data.DataColumn>().Select(c => c.ColumnName).ToList();
        }

        private string _selectedTable;
        public string SelectedTable
        {
            get { return _selectedTable; }
            set
            {
                _selectedTable = value;
                OnPropertyChanged(nameof(SelectedTable));
                LoadColumns(value);
            }
        }
    }
}
