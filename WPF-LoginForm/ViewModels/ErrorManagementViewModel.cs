using LiveCharts;
using LiveCharts.Wpf;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading; // Required for CancellationToken
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

        // --- Cancellation Token for Debouncing ---
        private CancellationTokenSource _cts;

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
                if (SetProperty(ref _selectedTable, value))
                {
                    SaveState();
                    if (!string.IsNullOrEmpty(_selectedTable))
                        _ = LoadDataWithDebounce(); // Use Debounce
                }
            }
        }

        // --- 2. Error Category ---
        public ObservableCollection<string> ErrorCategories { get; set; } = new ObservableCollection<string>();

        private string _selectedErrorCategory;

        public string SelectedErrorCategory
        {
            get => _selectedErrorCategory;
            set { if (SetProperty(ref _selectedErrorCategory, value)) { SaveState(); UpdateCategoryMachineChart(); } }
        }

        private bool _isMachine00Excluded = false;

        public bool IsMachine00Excluded
        {
            get => _isMachine00Excluded;
            set { if (SetProperty(ref _isMachine00Excluded, value)) { SaveState(); _ = LoadDataWithDebounce(); } }
        }

        // --- 3. Date & Slider ---
        private DateTime _startDate = DateTime.Today.AddDays(-7);

        private DateTime _endDate = DateTime.Today;
        private bool _isUpdatingFromSlider = false;

        public DateTime StartDate
        {
            get => _startDate;
            set { if (SetProperty(ref _startDate, value)) { if (!_isUpdatingFromSlider) RecalculateSliderValues(); SaveState(); } }
        }

        public DateTime EndDate
        {
            get => _endDate;
            set { if (SetProperty(ref _endDate, value)) { if (!_isUpdatingFromSlider) RecalculateSliderValues(); SaveState(); } }
        }

        private double _sliderMin = 0;
        private double _sliderMax = 100;
        private double _sliderLow;
        private double _sliderHigh;
        private DateTime _absoluteMinDate = DateTime.Today.AddYears(-1);

        public double SliderMinimum { get => _sliderMin; set => SetProperty(ref _sliderMin, value); }
        public double SliderMaximum { get => _sliderMax; set => SetProperty(ref _sliderMax, value); }

        public double SliderLowValue
        {
            get => _sliderLow;
            set { if (SetProperty(ref _sliderLow, value) && !_isUpdatingFromSlider) UpdateDatesFromSlider(); }
        }

        public double SliderHighValue
        {
            get => _sliderHigh;
            set { if (SetProperty(ref _sliderHigh, value) && !_isUpdatingFromSlider) UpdateDatesFromSlider(); }
        }

        // --- 4. Chart Collections ---
        private SeriesCollection _reasonSeries;

        public SeriesCollection ReasonSeries { get => _reasonSeries; set => SetProperty(ref _reasonSeries, value); }

        private string[] _reasonLabels;
        public string[] ReasonLabels { get => _reasonLabels; set => SetProperty(ref _reasonLabels, value); }

        private SeriesCollection _machineSeries;
        public SeriesCollection MachineSeries { get => _machineSeries; set => SetProperty(ref _machineSeries, value); }

        private SeriesCollection _shiftSeries;
        public SeriesCollection ShiftSeries { get => _shiftSeries; set => SetProperty(ref _shiftSeries, value); }

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

            // Initialize Empty
            ReasonSeries = new SeriesCollection();
            MachineSeries = new SeriesCollection();
            ShiftSeries = new SeriesCollection();

            LoadDataCommand = new ViewModelCommand(p => _ = LoadDataWithDebounce());
            ChartClickCommand = new ViewModelCommand(ExecuteChartClick);
            MoveDateCommand = new ViewModelCommand(ExecuteMoveDate);
            ConfigureCategoriesCommand = new ViewModelCommand(ExecuteConfigureCategories);

            NumberFormatter = value => $"{value:N0} min";

            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            var tables = await _repository.GetTableNamesAsync();
            TableNames.Clear();
            foreach (var t in tables) TableNames.Add(t);

            LoadState();

            if (string.IsNullOrEmpty(SelectedTable) && TableNames.Count > 0)
                SelectedTable = TableNames[0]; // Triggers LoadDataWithDebounce via setter
            else if (!string.IsNullOrEmpty(SelectedTable))
                await UpdateDateRangeFromDbAsync();
        }

        private async Task LoadDataWithDebounce()
        {
            // 1. Cancel existing request
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                // 2. Wait 300ms (Debounce) to see if user moves slider again
                await Task.Delay(300, token);
                if (token.IsCancellationRequested) return;

                await ExecuteLoadDataAsync(token);
            }
            catch (TaskCanceledException) { /* Expected */ }
        }

        private async Task ExecuteLoadDataAsync(CancellationToken token)
        {
            if (string.IsNullOrEmpty(SelectedTable)) return;

            try
            {
                // Background Fetch
                var rawData = await _repository.GetErrorDataAsync(StartDate, EndDate, SelectedTable);

                if (token.IsCancellationRequested) return;

                // Background Processing of Charts
                await ProcessChartsAsync(rawData, token);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load Error: {ex.Message}");
            }
        }

        private async Task ProcessChartsAsync(List<ErrorEventModel> rawData, CancellationToken token)
        {
            await Task.Run(() =>
            {
                if (token.IsCancellationRequested) return;

                _cachedRawData = rawData;

                // 1. Filter Data
                var baseFiltered = rawData.Where(x => !string.IsNullOrWhiteSpace(x.ErrorDescription));
                if (IsMachine00Excluded)
                {
                    baseFiltered = baseFiltered.Where(x => x.MachineCode != "00" && x.MachineCode != "0" && x.MachineCode != "MA-00");
                }
                var filteredList = baseFiltered.ToList();

                // 2. Prepare Categories (UI Thread needed for ObservableCollection usually, but we prep data here)
                var uniqueCategories = filteredList
                    .Select(x => _mappingService.GetMappedCategory(x.ErrorDescription, _activeRules))
                    .Distinct().OrderBy(x => x).ToList();

                // 3. REASON CHART (Pareto-ish: Top 5 + Others)
                var reasonStats = filteredList
                    .GroupBy(x => _mappingService.GetMappedCategory(x.ErrorDescription, _activeRules))
                    .Select(g => new { Reason = g.Key, MaxDuration = g.Max(x => x.DurationMinutes) })
                    .OrderByDescending(x => x.MaxDuration)
                    .ToList();

                var topReasons = reasonStats.Take(5).ToList();
                var othersCount = reasonStats.Skip(5).Sum(x => x.MaxDuration); // Sum or Max depending on KPI. Using Max of the rest is weird, maybe Sum? Let's stick to Max for consistency of "Longest Incident"
                // Actually for "Longest Incident", grouping "Others" is tricky. Let's just show Top 8.
                topReasons = reasonStats.Take(8).ToList();

                var reasonValues = new ChartValues<int>(topReasons.Select(x => x.MaxDuration));
                var reasonNames = topReasons.Select(x => x.Reason).ToList();
                var reasonLabels = topReasons.Select((x, index) => (index % 2 != 0) ? "\n" + x.Reason : x.Reason).ToArray();

                // 4. MACHINE CHART
                var machineStats = filteredList.GroupBy(x => x.MachineCode)
                    .Select(g => new { Machine = g.Key, TotalTime = g.Sum(x => x.DurationMinutes) })
                    .OrderByDescending(x => x.TotalTime).Take(9).ToList();

                // Build Series Collections (Must happen on UI thread or constructed here and assigned there)
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (token.IsCancellationRequested) return;

                    // Update Categories Dropdown
                    string prevCat = SelectedErrorCategory;
                    ErrorCategories.Clear();
                    foreach (var cat in uniqueCategories) ErrorCategories.Add(cat);

                    if (!string.IsNullOrEmpty(prevCat) && ErrorCategories.Contains(prevCat))
                        _selectedErrorCategory = prevCat; // Bypass setter to avoid loop
                    else if (ErrorCategories.Any())
                        _selectedErrorCategory = ErrorCategories[0];
                    OnPropertyChanged(nameof(SelectedErrorCategory));

                    // Update Reason Chart
                    _fullReasonNames = reasonNames;
                    ReasonLabels = reasonLabels;
                    ReasonSeries = new SeriesCollection
                    {
                        new ColumnSeries
                        {
                            Title = Resources.Chart_LongestIncident,
                            Values = reasonValues,
                            DataLabels = true,
                            Fill = Brushes.DodgerBlue
                        }
                    };

                    // Update Machine Chart
                    var machineColl = new SeriesCollection();
                    foreach (var item in machineStats)
                        machineColl.Add(new PieSeries { Title = $"MA-{item.Machine}", Values = new ChartValues<int> { item.TotalTime }, PushOut = 2 });
                    MachineSeries = machineColl;

                    // Update Shift/Category Chart
                    UpdateCategoryMachineChart(); // This runs on filtered data
                });
            });
        }

        private void UpdateCategoryMachineChart()
        {
            if (string.IsNullOrEmpty(SelectedErrorCategory) || _cachedRawData == null) return;

            // Reuse the existing cached data, no DB call needed
            var baseFiltered = _cachedRawData.Where(x => !string.IsNullOrWhiteSpace(x.ErrorDescription));
            if (IsMachine00Excluded) baseFiltered = baseFiltered.Where(x => x.MachineCode != "00" && x.MachineCode != "MA-00");

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
                    LabelPoint = p => $"{p.Y} min ({p.Participation:P0})"
                });
            }
            ShiftSeries = catColl;
        }

        private void ExecuteChartClick(object obj)
        {
            if (obj is ChartPoint point)
            {
                string filterText = point.SeriesView.Title;
                // If it's a Column Series (Reason), get text from list
                if (point.SeriesView is ColumnSeries && _fullReasonNames != null)
                {
                    int idx = (int)point.X;
                    if (idx >= 0 && idx < _fullReasonNames.Count) filterText = _fullReasonNames[idx];
                }

                // If it's the Category-Machine Pie Chart, append Context
                bool isCategoryChart = ShiftSeries != null && ShiftSeries.Cast<object>().Any(s => s == point.SeriesView);
                if (isCategoryChart)
                {
                    // Format: "MA-01-CategoryName"
                    filterText = $"{filterText}-{SelectedErrorCategory}";
                }

                DrillDownRequested?.Invoke(SelectedTable, StartDate, EndDate, filterText);
            }
        }

        // ... (ExecuteMoveDate, ExecuteConfigureCategories, SaveState, LoadState, RecalculateSliderValues, UpdateDatesFromSlider, UpdateDateRangeFromDbAsync remain mostly same as previous version but call LoadDataWithDebounce)

        private async Task UpdateDateRangeFromDbAsync()
        {
            try
            {
                var result = await _repository.GetTableDataAsync(SelectedTable, 1);
                if (result.Data == null) return;
                string dateCol = result.Data.Columns.Cast<DataColumn>().FirstOrDefault(c => c.ColumnName.ToLower().Contains("date"))?.ColumnName;
                if (dateCol == null) return;

                var (min, max) = await _repository.GetDateRangeAsync(SelectedTable, dateCol);
                if (min == DateTime.MinValue) { min = DateTime.Today.AddDays(-30); max = DateTime.Today; }

                _absoluteMinDate = min;
                SliderMinimum = 0;
                SliderMaximum = (max - min).TotalDays;
                if (SliderMaximum < 1) SliderMaximum = 1;

                StartDate = min; EndDate = max; // Will trigger slider recalc
            }
            catch { }
        }

        private void RecalculateSliderValues()
        {
            _isUpdatingFromSlider = true;
            SliderLowValue = Math.Max(0, (StartDate - _absoluteMinDate).TotalDays);
            SliderHighValue = Math.Min(SliderMaximum, (EndDate - _absoluteMinDate).TotalDays);
            _isUpdatingFromSlider = false;
        }

        private void UpdateDatesFromSlider()
        {
            _isUpdatingFromSlider = true;
            StartDate = _absoluteMinDate.AddDays(SliderLowValue);
            EndDate = _absoluteMinDate.AddDays(SliderHighValue);
            _isUpdatingFromSlider = false;
            _ = LoadDataWithDebounce();
        }

        private void ExecuteMoveDate(object parameter)
        {
            // Simple Logic: Move window by X days
            if (parameter is string s && int.TryParse(s.Split('|')[1], out int d))
            {
                if (s.StartsWith("Start")) StartDate = StartDate.AddDays(d);
                else EndDate = EndDate.AddDays(d);
                if (StartDate > EndDate) { if (s.StartsWith("Start")) EndDate = StartDate; else StartDate = EndDate; }
            }
        }

        private void ExecuteConfigureCategories(object obj)
        {
            var win = new CategoryConfigWindow();
            if (Application.Current.MainWindow != null) win.Owner = Application.Current.MainWindow;
            if (win.ShowDialog() == true)
            {
                _activeRules = _mappingService.LoadRules();
                _ = LoadDataWithDebounce();
            }
        }

        private void SaveState()
        { try { File.WriteAllText(StateFileName, JsonConvert.SerializeObject(new { SelectedTable, StartDate, EndDate, IsMachine00Excluded, SelectedErrorCategory })); } catch { } }

        private void LoadState()
        { try { if (File.Exists(StateFileName)) { dynamic state = JsonConvert.DeserializeObject(File.ReadAllText(StateFileName)); if (state != null) { _selectedTable = state.SelectedTable; _startDate = state.StartDate; _endDate = state.EndDate; _isMachine00Excluded = state.IsMachine00Excluded; _selectedErrorCategory = state.SelectedErrorCategory; } } } catch { } }
    }
}