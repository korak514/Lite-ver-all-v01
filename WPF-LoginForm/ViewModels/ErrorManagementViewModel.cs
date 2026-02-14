using LiveCharts;
using LiveCharts.Wpf;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

// DispatcherTimer namespace removed as it is no longer needed
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

        private readonly string StateFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WPF_LoginForm", "analytics_state.json");

        private CancellationTokenSource _cts;
        // Removed _autoRefreshTimer

        public bool IsActiveView { get; set; } = false;

        public event Action<string, DateTime, DateTime, string> DrillDownRequested;

        private List<ErrorEventModel> _cachedRawData = new List<ErrorEventModel>();
        private List<string> _fullReasonNames = new List<string>();
        private List<CategoryRule> _activeRules;

        // --- Selection ---
        public ObservableCollection<string> TableNames { get; set; } = new ObservableCollection<string>();

        private string _selectedTable;

        public string SelectedTable
        {
            get => _selectedTable;
            set { if (SetProperty(ref _selectedTable, value)) { SaveState(); if (!string.IsNullOrEmpty(_selectedTable)) _ = UpdateDateRangeFromDbAsync(); } }
        }

        // --- Categories ---
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

        // --- Date & Slider ---
        private DateTime _startDate = DateTime.Today.AddDays(-7);

        private DateTime _endDate = DateTime.Today;
        private bool _isUpdatingFromSlider = false;

        public DateTime StartDate
        {
            get => _startDate;
            set { if (SetProperty(ref _startDate, value)) { if (!_isUpdatingFromSlider) RecalculateSliderValues(); SaveState(); _ = LoadDataWithDebounce(); } }
        }

        public DateTime EndDate
        {
            get => _endDate;
            set { if (SetProperty(ref _endDate, value)) { if (!_isUpdatingFromSlider) RecalculateSliderValues(); SaveState(); _ = LoadDataWithDebounce(); } }
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

        // --- Charts ---
        private SeriesCollection _reasonSeries;

        public SeriesCollection ReasonSeries { get => _reasonSeries; set => SetProperty(ref _reasonSeries, value); }

        private string[] _reasonLabels;
        public string[] ReasonLabels { get => _reasonLabels; set => SetProperty(ref _reasonLabels, value); }

        private SeriesCollection _machineSeries;
        public SeriesCollection MachineSeries { get => _machineSeries; set => SetProperty(ref _machineSeries, value); }

        private SeriesCollection _shiftSeries;
        public SeriesCollection ShiftSeries { get => _shiftSeries; set => SetProperty(ref _shiftSeries, value); }

        public Func<double, string> NumberFormatter { get; set; }

        // --- Commands ---
        public ICommand LoadDataCommand { get; set; }

        public ICommand ChartClickCommand { get; set; }
        public ICommand MoveDateCommand { get; set; }
        public ICommand ConfigureCategoriesCommand { get; }

        public ErrorManagementViewModel(IDataRepository repository)
        {
            _repository = repository;
            _mappingService = new CategoryMappingService();
            _activeRules = _mappingService.LoadRules();

            // Init empty collections
            ReasonSeries = new SeriesCollection();
            MachineSeries = new SeriesCollection();
            ShiftSeries = new SeriesCollection();

            LoadDataCommand = new ViewModelCommand(p => _ = LoadDataWithDebounce());
            ChartClickCommand = new ViewModelCommand(ExecuteChartClick);
            MoveDateCommand = new ViewModelCommand(ExecuteMoveDate);
            ConfigureCategoriesCommand = new ViewModelCommand(ExecuteConfigureCategories);

            NumberFormatter = value => $"{value:N0} min";

            // Removed Auto Refresh Timer per request

            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            var tables = await _repository.GetTableNamesAsync();
            TableNames.Clear();
            foreach (var t in tables) TableNames.Add(t);
            LoadState();
            if (string.IsNullOrEmpty(SelectedTable) && TableNames.Count > 0) SelectedTable = TableNames[0];
            else if (!string.IsNullOrEmpty(SelectedTable)) _ = UpdateDateRangeFromDbAsync();
        }

        // --- Lifecycle Methods ---

        public void Activate()
        {
            IsActiveView = true;
            // Repaint charts if cached data exists
            if (_cachedRawData != null && _cachedRawData.Any())
            {
                OnPropertyChanged(nameof(ReasonSeries));
                OnPropertyChanged(nameof(ReasonLabels));
                OnPropertyChanged(nameof(MachineSeries));
                OnPropertyChanged(nameof(ShiftSeries));
                _ = LoadDataWithDebounce();
            }
        }

        private async Task LoadDataWithDebounce()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            try
            {
                await Task.Delay(300, token);
                if (token.IsCancellationRequested) return;
                await ExecuteLoadDataAsync(token);
            }
            catch (TaskCanceledException) { }
        }

        private async Task ExecuteLoadDataAsync(CancellationToken token)
        {
            if (string.IsNullOrEmpty(SelectedTable)) return;
            try
            {
                var rawData = await _repository.GetErrorDataAsync(StartDate, EndDate, SelectedTable);
                if (token.IsCancellationRequested) return;
                await ProcessChartsAsync(rawData, token);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Load Error: {ex.Message}"); }
        }

        private async Task ProcessChartsAsync(List<ErrorEventModel> rawData, CancellationToken token)
        {
            await Task.Run(() =>
            {
                if (token.IsCancellationRequested) return;
                _cachedRawData = rawData;

                var baseFiltered = rawData.Where(x => !string.IsNullOrWhiteSpace(x.ErrorDescription));
                if (IsMachine00Excluded) baseFiltered = baseFiltered.Where(x => x.MachineCode != "00" && x.MachineCode != "0" && x.MachineCode != "MA-00");
                var filteredList = baseFiltered.ToList();

                var uniqueCategories = filteredList.Select(x => _mappingService.GetMappedCategory(x.ErrorDescription, _activeRules)).Distinct().OrderBy(x => x).ToList();

                // --- REASON CHART ---
                var reasonStats = filteredList
                    .GroupBy(x => _mappingService.GetMappedCategory(x.ErrorDescription, _activeRules))
                    .Select(g => new { Reason = g.Key, MaxDuration = g.Max(x => x.DurationMinutes) })
                    .OrderByDescending(x => x.MaxDuration)
                    .Take(5) // FIX: Limit to 5 bars
                    .ToList();

                var reasonValues = new ChartValues<int>(reasonStats.Select(x => x.MaxDuration));

                // FIX: Truncation Logic & Z-Style Layout
                var reasonLabels = reasonStats.Select((x, index) =>
                {
                    string label = x.Reason;

                    // Check max length
                    if (label.Length > 15)
                    {
                        var words = label.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (words.Length >= 2)
                        {
                            label = $"{words[0]} {words[1]}...";
                        }
                        else
                        {
                            // Fallback for single long word
                            label = label.Substring(0, 12) + "...";
                        }

                        // Final safety check to ensure strict limit
                        if (label.Length > 15)
                            label = label.Substring(0, 15);
                    }

                    // Apply Staggered (Z-Style) formatting
                    return (index % 2 != 0) ? "\n" + label : label;
                }).ToArray();

                var reasonNames = reasonStats.Select(x => x.Reason).ToList();

                // --- MACHINE CHART ---
                var machineStats = filteredList.GroupBy(x => x.MachineCode)
                    .Select(g => new { Machine = g.Key, TotalTime = g.Sum(x => x.DurationMinutes) })
                    .OrderByDescending(x => x.TotalTime).Take(9).ToList();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (token.IsCancellationRequested) return;

                    string prevCat = SelectedErrorCategory;
                    ErrorCategories.Clear();
                    foreach (var c in uniqueCategories) ErrorCategories.Add(c);

                    if (!string.IsNullOrEmpty(prevCat) && ErrorCategories.Contains(prevCat)) _selectedErrorCategory = prevCat;
                    else if (ErrorCategories.Any()) _selectedErrorCategory = ErrorCategories[0];
                    OnPropertyChanged(nameof(SelectedErrorCategory));

                    _fullReasonNames = reasonNames;
                    ReasonLabels = reasonLabels;

                    ReasonSeries = new SeriesCollection
                    {
                        new ColumnSeries { Title = Resources.Chart_LongestIncident, Values = reasonValues, DataLabels = true, Fill = Brushes.DodgerBlue }
                    };

                    var machineColl = new SeriesCollection();
                    foreach (var item in machineStats) machineColl.Add(new PieSeries { Title = $"MA-{item.Machine}", Values = new ChartValues<int> { item.TotalTime }, PushOut = 2 });
                    MachineSeries = machineColl;

                    UpdateCategoryMachineChart();
                });
            });
        }

        private void UpdateCategoryMachineChart()
        {
            if (string.IsNullOrEmpty(SelectedErrorCategory) || _cachedRawData == null) return;
            var baseFiltered = _cachedRawData.Where(x => !string.IsNullOrWhiteSpace(x.ErrorDescription));
            if (IsMachine00Excluded) baseFiltered = baseFiltered.Where(x => x.MachineCode != "00" && x.MachineCode != "MA-00");

            var categoryStats = baseFiltered
                .Where(x => _mappingService.GetMappedCategory(x.ErrorDescription, _activeRules).Equals(SelectedErrorCategory, StringComparison.OrdinalIgnoreCase))
                .GroupBy(x => x.MachineCode)
                .Select(g => new { Machine = g.Key, TotalTime = g.Sum(x => x.DurationMinutes) })
                .OrderByDescending(x => x.TotalTime).Take(9).ToList();

            var catColl = new SeriesCollection();
            foreach (var item in categoryStats)
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

        // --- Date & Slider ---
        private async Task UpdateDateRangeFromDbAsync()
        {
            if (string.IsNullOrEmpty(SelectedTable)) return;
            try
            {
                var result = await _repository.GetTableDataAsync(SelectedTable, 1);
                if (result.Data == null) return;
                string dateCol = result.Data.Columns.Cast<DataColumn>().FirstOrDefault(c => c.ColumnName.ToLower().Contains("date") || c.ColumnName.ToLower().Contains("tarih"))?.ColumnName;
                if (dateCol == null) return;

                var (min, max) = await _repository.GetDateRangeAsync(SelectedTable, dateCol);
                if (min == DateTime.MinValue) { min = DateTime.Today.AddDays(-30); max = DateTime.Today; }

                _absoluteMinDate = min;
                SliderMinimum = 0;
                SliderMaximum = (max - min).TotalDays;
                if (SliderMaximum < 1) SliderMaximum = 1;

                if (_startDate < min) StartDate = min;
                if (_endDate > max) EndDate = max;

                RecalculateSliderValues();
                _ = LoadDataWithDebounce();
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

        // --- Other Commands ---
        private void ExecuteChartClick(object obj)
        {
            if (obj is ChartPoint point)
            {
                string filterText = point.SeriesView.Title;
                if (point.SeriesView is ColumnSeries && _fullReasonNames != null)
                {
                    int idx = (int)point.X;
                    if (idx >= 0 && idx < _fullReasonNames.Count) filterText = _fullReasonNames[idx];
                }
                if (ShiftSeries != null && ShiftSeries.Cast<object>().Any(s => s == point.SeriesView)) filterText = $"{filterText}-{SelectedErrorCategory}";
                DrillDownRequested?.Invoke(SelectedTable, StartDate, EndDate, filterText);
            }
        }

        private void ExecuteMoveDate(object parameter)
        {
            if (parameter is string s && s.Contains("|"))
            {
                var parts = s.Split('|');
                if (int.TryParse(parts[1], out int d))
                {
                    if (parts[0] == "Start") StartDate = StartDate.AddDays(d); else EndDate = EndDate.AddDays(d);
                    if (StartDate > EndDate) { if (parts[0] == "Start") EndDate = StartDate; else StartDate = EndDate; }
                }
            }
        }

        private void ExecuteConfigureCategories(object obj)
        {
            var win = new CategoryConfigWindow();
            if (Application.Current.MainWindow != null) win.Owner = Application.Current.MainWindow;
            if (win.ShowDialog() == true) { _activeRules = _mappingService.LoadRules(); _ = LoadDataWithDebounce(); }
        }

        // --- Persistence ---
        private void SaveState()
        {
            try
            {
                string dir = Path.GetDirectoryName(StateFilePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var state = new { SelectedTable, StartDate, EndDate, IsMachine00Excluded, SelectedErrorCategory };
                File.WriteAllText(StateFilePath, JsonConvert.SerializeObject(state));
            }
            catch { }
        }

        private void LoadState()
        {
            try
            {
                if (File.Exists(StateFilePath))
                {
                    dynamic state = JsonConvert.DeserializeObject(File.ReadAllText(StateFilePath));
                    if (state != null)
                    {
                        _selectedTable = state.SelectedTable;
                        _startDate = state.StartDate;
                        _endDate = state.EndDate;
                        _isMachine00Excluded = state.IsMachine00Excluded;
                        _selectedErrorCategory = state.SelectedErrorCategory;
                        OnPropertyChanged(nameof(SelectedTable)); OnPropertyChanged(nameof(StartDate));
                        OnPropertyChanged(nameof(EndDate)); OnPropertyChanged(nameof(IsMachine00Excluded));
                        OnPropertyChanged(nameof(SelectedErrorCategory));
                    }
                }
            }
            catch { }
        }
    }
}