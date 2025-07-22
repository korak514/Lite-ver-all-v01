using System.Windows.Input;
using WPF_LoginForm.Repositories;
using WPF_LoginForm.Services;

namespace WPF_LoginForm.ViewModels
{
    public class ConfigurationViewModel : ViewModelBase
    {
        public ICommand OkCommand { get; }
        public ICommand CancelCommand { get; }

        private readonly IDataRepository _dataRepository;

        private System.Collections.Generic.List<string> _tables;
        public System.Collections.Generic.List<string> Tables
        {
            get { return _tables; }
            set
            {
                _tables = value;
                OnPropertyChanged(nameof(Tables));
            }
        }

        public ConfigurationViewModel()
        {
            _dataRepository = new DataRepository(new FileLogger());
            OkCommand = new ViewModelCommand(p => { /* Save configuration and close dialog */ });
            CancelCommand = new ViewModelCommand(p => { /* Close dialog */ });
            LoadTables();
        }

        private System.Collections.Generic.List<string> _columns;
        public System.Collections.Generic.List<string> Columns
        {
            get { return _columns; }
            set
            {
                _columns = value;
                OnPropertyChanged(nameof(Columns));
            }
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

        private async void LoadTables()
        {
            Tables = await _dataRepository.GetTableNamesAsync();
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

        private System.Collections.Generic.List<string> _chartTypes;
        public System.Collections.Generic.List<string> ChartTypes
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

        private async void LoadColumns(string tableName)
        {
            var dataTable = await _dataRepository.GetTableDataAsync(tableName);
            Columns = dataTable.Columns.Cast<System.Data.DataColumn>().Select(c => c.ColumnName).ToList();
        }
    }
}
