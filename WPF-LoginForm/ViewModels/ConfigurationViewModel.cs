using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WPF_LoginForm.Models;
using WPF_LoginForm.Repositories;

namespace WPF_LoginForm.ViewModels
{
    public class ConfigurationViewModel : ViewModelBase
    {
        private readonly IDataRepository _dataRepository;
        private DashboardConfigurationViewModel _currentConfiguration;
        private bool _isInitializing = true;

        public ObservableCollection<DashboardConfigurationViewModel> ChartConfigurations { get; }
        public List<string> AvailableTables { get; private set; }
        public List<string> AvailableColumns { get; private set; }

        public Action CloseAction { get; set; }
        public bool WasApplied { get; private set; }
        public bool IsConfigurationVisible => CurrentConfiguration != null;

        public ICommand ApplyCommand { get; }

        public DashboardConfigurationViewModel CurrentConfiguration
        {
            get => _currentConfiguration;
            set
            {
                if (SetProperty(ref _currentConfiguration, value))
                {
                    OnPropertyChanged(nameof(IsConfigurationVisible));
                    if (_currentConfiguration != null && !_isInitializing)
                    {
                        _ = LoadColumnsForTable(_currentConfiguration.TableName);
                    }
                }
            }
        }

        public ConfigurationViewModel(IDataRepository dataRepository)
        {
            _dataRepository = dataRepository;
            ChartConfigurations = new ObservableCollection<DashboardConfigurationViewModel>();
            ApplyCommand = new ViewModelCommand(ExecuteApply);
        }

        public async Task InitializeAsync()
        {
            await LoadTables();
            _isInitializing = false;
        }

        public void LoadConfigurations(List<DashboardConfiguration> configs)
        {
            ChartConfigurations.Clear();
            var existingConfigs = configs ?? new List<DashboardConfiguration>();

            for (int i = 1; i <= 5; i++)
            {
                var configForSlot = existingConfigs.FirstOrDefault(c => c.ChartPosition == i)
                                 ?? new DashboardConfiguration { ChartPosition = i, IsEnabled = false };

                var configVM = new DashboardConfigurationViewModel(configForSlot);
                configVM.PropertyChanged += OnCurrentConfigPropertyChanged;
                ChartConfigurations.Add(configVM);
            }

            var selected = ChartConfigurations.FirstOrDefault(c => c.IsSelected) ?? ChartConfigurations.First();
            selected.IsSelected = true;
            CurrentConfiguration = selected;
        }

        private void OnCurrentConfigPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DashboardConfigurationViewModel.IsSelected) && sender is DashboardConfigurationViewModel selectedVM && selectedVM.IsSelected)
            {
                CurrentConfiguration = selectedVM;
                foreach (var otherVM in ChartConfigurations.Where(vm => vm != selectedVM))
                {
                    otherVM.IsSelected = false;
                }
            }
            else if (e.PropertyName == nameof(DashboardConfigurationViewModel.TableName))
            {
                _ = LoadColumnsForTable((sender as DashboardConfigurationViewModel)?.TableName);
            }
        }

        private async Task LoadTables()
        {
            try
            {
                AvailableTables = await _dataRepository.GetTableNamesAsync();
                OnPropertyChanged(nameof(AvailableTables));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load table list: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                AvailableTables = new List<string>();
            }
        }

        private async Task LoadColumnsForTable(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                AvailableColumns = new List<string>();
            }
            else
            {
                try
                {
                    DataTable dataTable = await _dataRepository.GetTableDataAsync(tableName);

                    // --- MODIFIED: Filter out 'ID' column so users don't try to graph it ---
                    AvailableColumns = dataTable.Columns
                        .Cast<DataColumn>()
                        .Select(c => c.ColumnName)
                        .Where(name => !name.Equals("ID", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to load columns for table '{tableName}': {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    AvailableColumns = new List<string>();
                }
            }
            OnPropertyChanged(nameof(AvailableColumns));
        }

        private void ExecuteApply(object obj)
        {
            WasApplied = true;
            CloseAction?.Invoke();
        }

        public List<DashboardConfiguration> GetFinalConfigurations()
        {
            return ChartConfigurations.Select(vm => vm.GetModel()).ToList();
        }
    }

    public class DashboardConfigurationViewModel : ViewModelBase
    {
        private readonly DashboardConfiguration _model;

        public DashboardConfigurationViewModel(DashboardConfiguration model)
        {
            _model = model;
            if (_model.ChartPosition == 4)
            {
                _model.ChartType = "Pie";
            }

            Series = new ObservableCollection<SeriesConfiguration>(_model.Series);
            AddSeriesCommand = new ViewModelCommand(ExecuteAddSeries, CanExecuteAddSeries);
            RemoveSeriesCommand = new ViewModelCommand(ExecuteRemoveSeries);
        }

        public DashboardConfiguration GetModel()
        {
            _model.Series = Series.ToList();
            return _model;
        }

        public ICommand AddSeriesCommand { get; }
        public ICommand RemoveSeriesCommand { get; }

        public List<string> ChartTypes { get; } = new List<string> { "Line", "Bar" };
        public List<string> AggregationOptions { get; } = new List<string> { "Daily", "Weekly", "Monthly" };
        public List<string> AvailableDataStructures { get; } = new List<string> { "Daily Date", "Monthly Date", "ID", "General" };
        public List<int> AvailableIgnoreCounts { get; } = Enumerable.Range(0, 10).ToList();

        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
        public int ChartPosition => _model.ChartPosition;
        public bool IsEnabled
        { get => _model.IsEnabled; set { if (_model.IsEnabled != value) { _model.IsEnabled = value; OnPropertyChanged(); } } }
        public string TableName
        { get => _model.TableName; set { if (_model.TableName != value) { _model.TableName = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsTableSelected)); } } }
        public string DateColumn
        { get => _model.DateColumn; set { if (_model.DateColumn != value) { _model.DateColumn = value; OnPropertyChanged(); } } }
        public ObservableCollection<SeriesConfiguration> Series { get; }
        public string AggregationType
        { get => _model.AggregationType; set { if (_model.AggregationType != value) { _model.AggregationType = value; OnPropertyChanged(); } } }

        public string DataStructureType
        {
            get => _model.DataStructureType;
            set
            {
                if (_model.DataStructureType != value)
                {
                    _model.DataStructureType = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsDailyDateStructure));
                    OnPropertyChanged(nameof(IsCategoricalStructure));
                }
            }
        }

        public int RowsToIgnore
        {
            get => _model.RowsToIgnore;
            set
            {
                if (_model.RowsToIgnore != value)
                {
                    _model.RowsToIgnore = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool UseInvariantCultureForNumbers
        {
            get => _model.UseInvariantCultureForNumbers;
            set
            {
                if (_model.UseInvariantCultureForNumbers != value)
                {
                    _model.UseInvariantCultureForNumbers = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsDailyDateStructure => DataStructureType == "Daily Date";
        public bool IsCategoricalStructure => !IsDailyDateStructure;

        public string ChartType
        {
            get => _model.ChartType;
            set
            {
                if (_model.ChartPosition == 4 || _model.ChartType == value) return;

                _model.ChartType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsPieChart));
                OnPropertyChanged(nameof(IsCartesianChart));
                (AddSeriesCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
            }
        }

        public bool IsPieChart => string.Equals(_model.ChartType, "Pie", StringComparison.OrdinalIgnoreCase);
        public bool IsCartesianChart => !IsPieChart;
        public bool CanChangeChartType => _model.ChartPosition != 4;
        public bool IsTableSelected => !string.IsNullOrEmpty(TableName);

        private bool CanExecuteAddSeries(object obj)
        {
            if (IsPieChart) return Series.Count < 6;
            if (string.Equals(ChartType, "Line", StringComparison.OrdinalIgnoreCase)) return Series.Count < 3;
            if (string.Equals(ChartType, "Bar", StringComparison.OrdinalIgnoreCase)) return Series.Count < 2;
            return false;
        }

        private void ExecuteAddSeries(object obj)
        {
            if (CanExecuteAddSeries(null))
            {
                Series.Add(new SeriesConfiguration());
            }
        }

        private void ExecuteRemoveSeries(object obj)
        {
            if (obj is SeriesConfiguration seriesToRemove)
            {
                Series.Remove(seriesToRemove);
            }
        }
    }
}