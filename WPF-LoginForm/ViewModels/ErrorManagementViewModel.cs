using LiveCharts;
using LiveCharts.Wpf;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WPF_LoginForm.Models;
using WPF_LoginForm.Properties;
using WPF_LoginForm.Repositories;
using WPF_LoginForm.Services;
using WPF_LoginForm.Views;

namespace WPF_LoginForm.ViewModels
{
    public class ErrorManagementViewModel : ViewModelBase
    {
        private readonly IDataRepository _repository;
        private readonly CategoryMappingService _mappingService;
        private const string StateFileName = "analytics_state.json";

        public event Action<string, DateTime, DateTime, string> DrillDownRequested;

        private List<ErrorEventModel> _cachedRawData = new List<ErrorEventModel>();
        private List<string> _fullReasonNames = new List<string>();
        private List<CategoryRule> _activeRules;

        // --- 1. Selection & Config ---
        public ObservableCollection<string> TableNames { get; set; } = new ObservableCollection<string>();

        private string _selectedTable;

        public string SelectedTable
        {
            get => _selectedTable;
            set
            {
                if (_selectedTable != value)
                {
                    _selectedTable = value;
                    OnPropertyChanged();
                    SaveState();

                    // Trigger data load immediately when table changes
                    if (!string.IsNullOrEmpty(_selectedTable))
                        _ = ExecuteLoadDataAsync();
                }
            }
        }

        // --- 2. Error Category ---
        public ObservableCollection<string> ErrorCategories { get; set; } = new ObservableCollection<string>();

        private string _selectedErrorCategory;

        public string SelectedErrorCategory
        {
            get => _selectedErrorCategory;
            set
            {
                if (SetProperty(ref _selectedErrorCategory, value))
                {
                    SaveState();
                    UpdateCategoryMachineChart();
                }
            }
        }

        private bool _isMachine00Excluded = false;

        public bool IsMachine00Excluded
        {
            get => _isMachine00Excluded;
            set
            {
                if (SetProperty(ref _isMachine00Excluded, value))
                {
                    SaveState();
                    _ = ExecuteLoadDataAsync();
                }
            }
        }

        // --- 3. Date & Slider ---
        private DateTime _startDate = DateTime.Today.AddDays(-7);

        private DateTime _endDate = DateTime.Today;
        private bool _isUpdatingFromSlider = false;

        public DateTime StartDate
        {
            get => _startDate;
            set { if (_startDate != value) { _startDate = value; OnPropertyChanged(); if (!_isUpdatingFromSlider) RecalculateSliderValues(); SaveState(); } }
        }

        public DateTime EndDate
        {
            get => _endDate;
            set { if (_endDate != value) { _endDate = value; OnPropertyChanged(); if (!_isUpdatingFromSlider) RecalculateSliderValues(); SaveState(); } }
        }

        private double _sliderMin = 0;
        private double _sliderMax = 100;
        private double _sliderLow;
        private double _sliderHigh;
        private DateTime _absoluteMinDate = DateTime.Today.AddYears(-1);

        public double SliderMinimum
        { get => _sliderMin; set { _sliderMin = value; OnPropertyChanged(); } }

        public double SliderMaximum
        { get => _sliderMax; set { _sliderMax = value; OnPropertyChanged(); } }

        public double SliderLowValue
        {
            get => _sliderLow;
            set { _sliderLow = value; OnPropertyChanged(); if (!_isUpdatingFromSlider) UpdateDatesFromSlider(); }
        }

        public double SliderHighValue
        {
            get => _sliderHigh;
            set { _sliderHigh = value; OnPropertyChanged(); if (!_isUpdatingFromSlider) UpdateDatesFromSlider(); }
        }

        // --- 4. Chart Collections ---
        private SeriesCollection _reasonSeries;

        public SeriesCollection ReasonSeries
        { get => _reasonSeries; set { _reasonSeries = value; OnPropertyChanged(); } }

        public string[] ReasonLabels { get; set; }

        private SeriesCollection _machineSeries;

        public SeriesCollection MachineSeries
        { get => _machineSeries; set { _machineSeries = value; OnPropertyChanged(); } }

        private SeriesCollection _shiftSeries;

        public SeriesCollection ShiftSeries
        { get => _shiftSeries; set { _shiftSeries = value; OnPropertyChanged(); } }

        public Func<double, string> NumberFormatter { get; set; }

        // --- 5. Commands ---
        public ICommand LoadDataCommand { get; set; }

        public ICommand ChartClickCommand { get; set; }
        public ICommand MoveDateCommand { get; set; }
        public ICommand ConfigureCategoriesCommand { get; }

        public ErrorManagementViewModel(IDataRepository repository)
        {
            _repository = repository;
            _mappingService = new CategoryMappingService();
            _activeRules = _mappingService.LoadRules();

            // Initialize Collections Empty to prevent binding errors on load
            ReasonSeries = new SeriesCollection();
            MachineSeries = new SeriesCollection();
            ShiftSeries = new SeriesCollection();

            LoadDataCommand = new ViewModelCommand(p => ExecuteLoadDataAsync());
            ChartClickCommand = new ViewModelCommand(ExecuteChartClick);
            MoveDateCommand = new ViewModelCommand(ExecuteMoveDate);
            ConfigureCategoriesCommand = new ViewModelCommand(ExecuteConfigureCategories);

            NumberFormatter = value => $"{value:N0} {Resources.Unit_Minutes}";

            // Start Initialization
            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            // 1. Get Tables
            var tables = await _repository.GetTableNamesAsync();
            TableNames.Clear();
            foreach (var t in tables) TableNames.Add(t);

            // 2. Load Settings (Selected Table, Dates)
            LoadState();

            // 3. Set Default if nothing selected
            if (string.IsNullOrEmpty(SelectedTable) && TableNames.Count > 0)
            {
                SelectedTable = TableNames[0];
            }
            // 4. If table is selected (from state or default), Load Data
            else if (!string.IsNullOrEmpty(SelectedTable))
            {
                // We must manually trigger this because PropertyChanged in Constructor might not fire logic if value didn't change
                await OnTableChangedAsync();
            }
        }

        private void ExecuteConfigureCategories(object obj)
        {
            var win = new CategoryConfigWindow();
            if (Application.Current.MainWindow != null) win.Owner = Application.Current.MainWindow;

            if (win.ShowDialog() == true)
            {
                _activeRules = _mappingService.LoadRules();
                _ = ExecuteLoadDataAsync();
            }
        }

        private async Task OnTableChangedAsync()
        {
            await UpdateDateRangeFromDbAsync();
            await ExecuteLoadDataAsync();
        }

        private async Task UpdateDateRangeFromDbAsync()
        {
            try
            {
                var result = await _repository.GetTableDataAsync(SelectedTable, 1);
                if (result.Data == null || result.Data.Columns.Count == 0) return;

                string dateColName = result.Data.Columns.Cast<DataColumn>().FirstOrDefault(c =>
                    c.ColumnName.ToLower().Contains("date") || c.ColumnName.ToLower().Contains("tarih"))?.ColumnName;

                if (string.IsNullOrEmpty(dateColName)) return;

                var (min, max) = await _repository.GetDateRangeAsync(SelectedTable, dateColName);
                if (min == DateTime.MinValue || max == DateTime.MinValue) { min = DateTime.Today.AddMonths(-1); max = DateTime.Today; }

                _absoluteMinDate = min;
                SliderMinimum = 0;
                SliderMaximum = (max - min).TotalDays;
                if (SliderMaximum < 1) SliderMaximum = 1;

                // Only override if current dates are wildly out of range (fresh start)
                if (StartDate < min) StartDate = min;
                if (EndDate > max) EndDate = max;

                RecalculateSliderValues();
                OnPropertyChanged(nameof(SliderMinimum));
                OnPropertyChanged(nameof(SliderMaximum));
            }
            catch { }
        }

        private void RecalculateSliderValues()
        {
            _isUpdatingFromSlider = true;
            SliderLowValue = (StartDate - _absoluteMinDate).TotalDays;
            SliderHighValue = (EndDate - _absoluteMinDate).TotalDays;
            if (SliderLowValue < SliderMinimum) SliderLowValue = SliderMinimum;
            if (SliderHighValue > SliderMaximum) SliderHighValue = SliderMaximum;
            _isUpdatingFromSlider = false;
        }

        private void UpdateDatesFromSlider()
        {
            _isUpdatingFromSlider = true;
            StartDate = _absoluteMinDate.AddDays(SliderLowValue);
            EndDate = _absoluteMinDate.AddDays(SliderHighValue);
            _isUpdatingFromSlider = false;
            _ = ExecuteLoadDataAsync();
        }

        private void ExecuteMoveDate(object parameter)
        {
            if (parameter is string s)
            {
                var p = s.Split('|');
                if (p.Length == 2 && int.TryParse(p[1], out int d))
                {
                    if (p[0] == "Start") StartDate = StartDate.AddDays(d);
                    else if (p[0] == "End") EndDate = EndDate.AddDays(d);
                    if (StartDate > EndDate) { if (p[0] == "Start") EndDate = StartDate; else StartDate = EndDate; }
                }
            }
        }

        // --- MAIN DATA LOADING ---
        private async Task ExecuteLoadDataAsync()
        {
            if (string.IsNullOrEmpty(SelectedTable)) return;

            try
            {
                // Background Thread
                var rawData = await _repository.GetErrorDataAsync(StartDate, EndDate, SelectedTable);

                // UI Thread Update
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _cachedRawData = rawData;
                    RefreshCharts();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load Error: {ex.Message}");
            }
        }

        private void RefreshCharts()
        {
            if (_cachedRawData == null || !_cachedRawData.Any())
            {
                ReasonSeries.Clear();
                MachineSeries.Clear();
                ShiftSeries.Clear();
                ErrorCategories.Clear();
                return;
            }

            // Filtering
            var baseFiltered = _cachedRawData.Where(x => !string.IsNullOrWhiteSpace(x.ErrorDescription));
            if (IsMachine00Excluded)
            {
                baseFiltered = baseFiltered.Where(x => x.MachineCode != "00" && x.MachineCode != "0" && x.MachineCode != "MA-00" && !(x.SectionCode == "MA" && x.MachineCode == "00"));
            }
            var filteredList = baseFiltered.ToList();

            // Refresh Category Dropdown
            var uniqueCategories = filteredList
                .Select(x => _mappingService.GetMappedCategory(x.ErrorDescription, _activeRules))
                .Distinct().OrderBy(x => x).ToList();

            string prevCat = SelectedErrorCategory;
            ErrorCategories.Clear();
            foreach (var cat in uniqueCategories) ErrorCategories.Add(cat);

            if (!string.IsNullOrEmpty(prevCat) && ErrorCategories.Contains(prevCat))
                _selectedErrorCategory = prevCat;
            else if (ErrorCategories.Any())
                _selectedErrorCategory = ErrorCategories[0];

            OnPropertyChanged(nameof(SelectedErrorCategory));

            if (!filteredList.Any()) return;

            // 1. REASON CHART (Column)
            var reasonStats = filteredList
                .GroupBy(x => _mappingService.GetMappedCategory(x.ErrorDescription, _activeRules))
                .Select(g => new { Reason = g.Key, MaxDuration = g.Max(x => x.DurationMinutes) })
                .OrderByDescending(x => x.MaxDuration)
                .Take(5)
                .ToList();

            _fullReasonNames = reasonStats.Select(x => x.Reason).ToList();
            ReasonLabels = reasonStats.Select((x, index) => (index % 2 != 0) ? "\n" + x.Reason : x.Reason).ToArray();

            // Create NEW collection to force redraw
            ReasonSeries = new SeriesCollection
            {
                new ColumnSeries
                {
                    Title = Resources.Chart_LongestIncident,
                    Values = new ChartValues<int>(reasonStats.Select(x => x.MaxDuration)),
                    DataLabels = true,
                    Fill = Brushes.DodgerBlue
                }
            };

            // 2. MACHINE CHART (Total)
            var machineStats = filteredList.GroupBy(x => x.MachineCode)
                .Select(g => new { Machine = g.Key, TotalTime = g.Sum(x => x.DurationMinutes) })
                .OrderByDescending(x => x.TotalTime).Take(9).ToList();

            var machineColl = new SeriesCollection();
            foreach (var item in machineStats)
                machineColl.Add(new PieSeries { Title = $"MA-{item.Machine}", Values = new ChartValues<int> { item.TotalTime }, PushOut = 2 });
            MachineSeries = machineColl;

            UpdateCategoryMachineChart();
        }

        private void UpdateCategoryMachineChart()
        {
            if (string.IsNullOrEmpty(SelectedErrorCategory) || _cachedRawData == null) return;

            var baseFiltered = _cachedRawData.Where(x => !string.IsNullOrWhiteSpace(x.ErrorDescription));
            if (IsMachine00Excluded) baseFiltered = baseFiltered.Where(x => x.MachineCode != "00" && x.MachineCode != "0" && x.MachineCode != "MA-00");

            var categoryStats = baseFiltered
                .Where(x => _mappingService.GetMappedCategory(x.ErrorDescription, _activeRules)
                            .Equals(SelectedErrorCategory, StringComparison.OrdinalIgnoreCase))
                .GroupBy(x => x.MachineCode)
                .Select(g => new { Machine = g.Key, TotalTime = g.Sum(x => x.DurationMinutes) })
                .OrderByDescending(x => x.TotalTime).ToList();

            var catColl = new SeriesCollection();
            foreach (var item in categoryStats.Take(9))
            {
                catColl.Add(new PieSeries
                {
                    Title = $"MA-{item.Machine}",
                    Values = new ChartValues<int> { item.TotalTime },
                    DataLabels = true,
                    LabelPoint = p => $"{p.Y} {Resources.Unit_Minutes} ({p.Participation:P0})"
                });
            }
            ShiftSeries = catColl;
        }

        private void ExecuteChartClick(object obj)
        {
            if (obj is ChartPoint point)
            {
                var seriesView = point.SeriesView;
                string filterText = seriesView.Title;

                if (filterText == "Others" || string.IsNullOrEmpty(SelectedTable)) return;

                bool isCategoryChart = ShiftSeries != null && ShiftSeries.Cast<object>().Any(s => s == seriesView);

                if (isCategoryChart && !string.IsNullOrEmpty(SelectedErrorCategory))
                {
                    filterText = $"{filterText}-{SelectedErrorCategory}";
                }
                else if (seriesView is ColumnSeries)
                {
                    int index = (int)point.X;
                    if (_fullReasonNames != null && index >= 0 && index < _fullReasonNames.Count)
                        filterText = _fullReasonNames[index];
                }

                DrillDownRequested?.Invoke(SelectedTable, StartDate, EndDate, filterText);
            }
        }

        private void SaveState()
        { try { var state = new AnalyticsState { SelectedTable = SelectedTable, StartDate = StartDate, EndDate = EndDate, ExcludeMachine00 = IsMachine00Excluded, SelectedCategory = SelectedErrorCategory }; File.WriteAllText(StateFileName, JsonConvert.SerializeObject(state)); } catch { } }

        private void LoadState()
        { try { if (File.Exists(StateFileName)) { var state = JsonConvert.DeserializeObject<AnalyticsState>(File.ReadAllText(StateFileName)); if (state != null) { _startDate = state.StartDate; _endDate = state.EndDate; _isMachine00Excluded = state.ExcludeMachine00; _selectedErrorCategory = state.SelectedCategory; _selectedTable = state.SelectedTable; OnPropertyChanged(nameof(IsMachine00Excluded)); } } } catch { } }

        private class AnalyticsState
        { public string SelectedTable { get; set; } public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } public bool ExcludeMachine00 { get; set; } public string SelectedCategory { get; set; } }
    }
}