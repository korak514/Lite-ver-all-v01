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
using WPF_LoginForm.Models;
using WPF_LoginForm.Repositories;
using WPF_LoginForm.Services;

namespace WPF_LoginForm.ViewModels
{
    public class ErrorManagementViewModel : ViewModelBase
    {
        private readonly IDataRepository _repository;
        private readonly CategoryMappingService _mappingService;
        private readonly string StateFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WPF_LoginForm", "analytics_state.json");
        private CancellationTokenSource _cts;

        public bool IsActiveView { get; set; } = false;

        public event Action<string, DateTime, DateTime, string> DrillDownRequested;

        private List<ErrorEventModel> _cachedRawData = new List<ErrorEventModel>();
        private List<string> _fullReasonNames = new List<string>();
        private List<CategoryRule> _activeRules;

        // --- Selection ---
        public ObservableCollection<string> TableNames { get; set; } = new ObservableCollection<string>();

        private string _selectedTable;
        public string SelectedTable
        { get => _selectedTable; set { if (SetProperty(ref _selectedTable, value)) { SaveState(); if (!string.IsNullOrEmpty(_selectedTable)) _ = UpdateDateRangeFromDbAsync(); } } }

        // --- Categories ---
        public ObservableCollection<string> ErrorCategories { get; set; } = new ObservableCollection<string>();

        private string _selectedErrorCategory;
        public string SelectedErrorCategory
        { get => _selectedErrorCategory; set { if (SetProperty(ref _selectedErrorCategory, value)) { SaveState(); UpdateCategoryMachineChart(); } } }
        private bool _isMachine00Excluded = false;
        public bool IsMachine00Excluded
        { get => _isMachine00Excluded; set { if (SetProperty(ref _isMachine00Excluded, value)) { SaveState(); _ = LoadDataWithDebounce(); } } }

        // --- Date ---
        private DateTime _startDate = DateTime.Today.AddDays(-7);

        private DateTime _endDate = DateTime.Today;
        private bool _isUpdatingFromSlider = false;
        public DateTime StartDate
        { get => _startDate; set { if (SetProperty(ref _startDate, value)) { if (!_isUpdatingFromSlider) RecalculateSliderValues(); SaveState(); _ = LoadDataWithDebounce(); } } }
        public DateTime EndDate
        { get => _endDate; set { if (SetProperty(ref _endDate, value)) { if (!_isUpdatingFromSlider) RecalculateSliderValues(); SaveState(); _ = LoadDataWithDebounce(); } } }

        private double _sliderMin = 0; private double _sliderMax = 100; private double _sliderLow; private double _sliderHigh; private DateTime _absoluteMinDate = DateTime.Today.AddYears(-1);
        public double SliderMinimum { get => _sliderMin; set => SetProperty(ref _sliderMin, value); }
        public double SliderMaximum { get => _sliderMax; set => SetProperty(ref _sliderMax, value); }
        public double SliderLowValue
        { get => _sliderLow; set { if (SetProperty(ref _sliderLow, value) && !_isUpdatingFromSlider) UpdateDatesFromSlider(); } }
        public double SliderHighValue
        { get => _sliderHigh; set { if (SetProperty(ref _sliderHigh, value) && !_isUpdatingFromSlider) UpdateDatesFromSlider(); } }

        // --- PAGE 1 CHARTS ---
        private SeriesCollection _reasonSeries; public SeriesCollection ReasonSeries { get => _reasonSeries; set => SetProperty(ref _reasonSeries, value); }

        private string[] _reasonLabels; public string[] ReasonLabels { get => _reasonLabels; set => SetProperty(ref _reasonLabels, value); }
        private SeriesCollection _machineSeries; public SeriesCollection MachineSeries { get => _machineSeries; set => SetProperty(ref _machineSeries, value); }
        private SeriesCollection _shiftSeries; public SeriesCollection ShiftSeries { get => _shiftSeries; set => SetProperty(ref _shiftSeries, value); }
        public Func<double, string> NumberFormatter { get; set; }

        // --- PAGE 2 CHARTS ---
        private bool _isSecondPageActive;

        public bool IsSecondPageActive { get => _isSecondPageActive; set => SetProperty(ref _isSecondPageActive, value); }

        private SeriesCollection _efficiencySeries; public SeriesCollection EfficiencySeries { get => _efficiencySeries; set => SetProperty(ref _efficiencySeries, value); }
        private SeriesCollection _shiftImpactSeries; public SeriesCollection ShiftImpactSeries { get => _shiftImpactSeries; set => SetProperty(ref _shiftImpactSeries, value); }

        // Severity is now a Bar Chart, so we generally use SeriesCollection + Labels
        private SeriesCollection _severitySeries; public SeriesCollection SeveritySeries { get => _severitySeries; set => SetProperty(ref _severitySeries, value); }

        private string[] _severityLabels; public string[] SeverityLabels { get => _severityLabels; set => SetProperty(ref _severityLabels, value); }

        // --- PAGE 2 STATS ---
        private string _dailyAvgStop; public string DailyAvgStop { get => _dailyAvgStop; set => SetProperty(ref _dailyAvgStop, value); }

        private string _dailyAvgError; public string DailyAvgError { get => _dailyAvgError; set => SetProperty(ref _dailyAvgError, value); }
        private string _timeSavedBreak; public string TimeSavedBreak { get => _timeSavedBreak; set => SetProperty(ref _timeSavedBreak, value); }
        private string _timeSavedMaintenance; public string TimeSavedMaintenance { get => _timeSavedMaintenance; set => SetProperty(ref _timeSavedMaintenance, value); }
        private string _extraStat1; public string ExtraStat1 { get => _extraStat1; set => SetProperty(ref _extraStat1, value); }
        private string _extraStat2; public string ExtraStat2 { get => _extraStat2; set => SetProperty(ref _extraStat2, value); }
        private string _extraStat3; public string ExtraStat3 { get => _extraStat3; set => SetProperty(ref _extraStat3, value); }

        // --- Commands ---
        public ICommand LoadDataCommand { get; set; }

        public ICommand ChartClickCommand { get; set; }
        public ICommand MoveDateCommand { get; set; }
        public ICommand ConfigureCategoriesCommand { get; }
        public ICommand TogglePageCommand { get; }

        public ErrorManagementViewModel(IDataRepository repository)
        {
            _repository = repository;
            _mappingService = new CategoryMappingService();
            _activeRules = _mappingService.LoadRules();

            ReasonSeries = new SeriesCollection();
            MachineSeries = new SeriesCollection();
            ShiftSeries = new SeriesCollection();

            EfficiencySeries = new SeriesCollection();
            ShiftImpactSeries = new SeriesCollection();
            SeveritySeries = new SeriesCollection();

            LoadDataCommand = new ViewModelCommand(p => _ = LoadDataWithDebounce());
            ChartClickCommand = new ViewModelCommand(ExecuteChartClick);
            MoveDateCommand = new ViewModelCommand(ExecuteMoveDate);
            ConfigureCategoriesCommand = new ViewModelCommand(ExecuteConfigureCategories);
            TogglePageCommand = new ViewModelCommand(p => IsSecondPageActive = !IsSecondPageActive);

            NumberFormatter = value => $"{value:N0} min";
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

        public void Activate()
        {
            IsActiveView = true;
            if (_cachedRawData != null && _cachedRawData.Any()) { OnPropertyChanged(nameof(ReasonSeries)); _ = LoadDataWithDebounce(); }
        }

        private async Task LoadDataWithDebounce()
        {
            _cts?.Cancel(); _cts = new CancellationTokenSource(); var token = _cts.Token;
            try { await Task.Delay(300, token); if (token.IsCancellationRequested) return; await ExecuteLoadDataAsync(token); } catch (TaskCanceledException) { }
        }

        private async Task ExecuteLoadDataAsync(CancellationToken token)
        {
            if (string.IsNullOrEmpty(SelectedTable)) return;
            try { var rawData = await _repository.GetErrorDataAsync(StartDate, EndDate, SelectedTable); if (token.IsCancellationRequested) return; await ProcessChartsAsync(rawData, token); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Load Error: {ex.Message}"); }
        }

        private async Task ProcessChartsAsync(List<ErrorEventModel> rawData, CancellationToken token)
        {
            await Task.Run(() =>
            {
                if (token.IsCancellationRequested) return;
                _cachedRawData = rawData;

                var filteredList = InputHelper.PreProcessData(rawData, IsMachine00Excluded);

                // --- Page 1 Data ---
                var reasonStats = InputHelper.GetTopReasons(filteredList, _mappingService, _activeRules, 5);
                var reasonValues = new ChartValues<double>(reasonStats.Select(x => x.Value));
                var reasonLabels = reasonStats.Select((x, index) => { string l = x.Label; if (l.Length > 15) l = l.Substring(0, 12) + "..."; return (index % 2 != 0) ? "\n" + l : l; }).ToArray();
                var reasonFullNames = reasonStats.Select(x => x.Label).ToList();
                var machineStats = InputHelper.GetMachineStats(filteredList);

                // --- Page 2 Data ---
                var page2Stats = InputHelper.CalculatePage2Stats(filteredList, StartDate, EndDate);
                var shiftStats = InputHelper.GetShiftStats(filteredList);
                var severityStats = InputHelper.GetSeverityStats(filteredList);

                var uniqueCategories = filteredList.Select(x => _mappingService.GetMappedCategory(x.ErrorDescription, _activeRules)).Distinct().OrderBy(x => x).ToList();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (token.IsCancellationRequested) return;

                    string prevCat = SelectedErrorCategory;
                    ErrorCategories.Clear();
                    foreach (var c in uniqueCategories) ErrorCategories.Add(c);
                    if (!string.IsNullOrEmpty(prevCat) && ErrorCategories.Contains(prevCat)) _selectedErrorCategory = prevCat;
                    else if (ErrorCategories.Any()) _selectedErrorCategory = ErrorCategories[0];
                    OnPropertyChanged(nameof(SelectedErrorCategory));

                    // Update P1
                    _fullReasonNames = reasonFullNames;
                    ReasonLabels = reasonLabels;
                    ReasonSeries = new SeriesCollection { new ColumnSeries { Title = WPF_LoginForm.Properties.Resources.Chart_LongestIncident, Values = reasonValues, DataLabels = true, Fill = Brushes.DodgerBlue } };

                    var machineColl = new SeriesCollection();
                    foreach (var item in machineStats) machineColl.Add(new PieSeries { Title = $"MA-{item.Label}", Values = new ChartValues<double> { item.Value }, PushOut = 2 });
                    MachineSeries = machineColl;
                    UpdateCategoryMachineChart();

                    // Update P2 - Text
                    DailyAvgStop = page2Stats.DailyAvgStop;
                    DailyAvgError = page2Stats.DailyAvgError;
                    TimeSavedBreak = page2Stats.SavedBreakTime;
                    TimeSavedMaintenance = page2Stats.SavedMaintenanceTime;
                    ExtraStat1 = page2Stats.ExtraStat1;
                    ExtraStat2 = page2Stats.ExtraStat2;
                    ExtraStat3 = page2Stats.ExtraStat3;

                    // Update P2 - Efficiency
                    EfficiencySeries = new SeriesCollection {
                        new PieSeries { Title = "Errors", Values = new ChartValues<double> { page2Stats.TotalErrorDuration }, DataLabels = true, Fill = Brushes.OrangeRed, LabelPoint = p => $"{p.Y:N0}m" },
                        new PieSeries { Title = "Stops", Values = new ChartValues<double> { page2Stats.TotalStopDuration }, DataLabels = true, Fill = Brushes.DodgerBlue, LabelPoint = p => $"{p.Y:N0}m" }
                    };

                    // Update P2 - Shift Impact
                    var shiftColl = new SeriesCollection();
                    foreach (var s in shiftStats) shiftColl.Add(new PieSeries { Title = s.Label, Values = new ChartValues<double> { s.Value }, DataLabels = true, LabelPoint = p => $"{p.Y:N0}m ({p.Participation:P0})" });
                    ShiftImpactSeries = shiftColl;

                    // Update P2 - Severity (As Bar Chart)
                    var sevColl = new SeriesCollection();
                    var labels = new List<string>();

                    foreach (var s in severityStats)
                    {
                        Brush fill = Brushes.Gray;
                        if (s.Label.Contains("Micro")) fill = Brushes.MediumSeaGreen;
                        else if (s.Label.Contains("Minor")) fill = Brushes.Orange;
                        else if (s.Label.Contains("Major")) fill = Brushes.Crimson;

                        // For a multi-colored bar chart in LiveCharts without custom mappers,
                        // we add one ColumnSeries per category.
                        sevColl.Add(new ColumnSeries
                        {
                            Title = s.Label,
                            Values = new ChartValues<double> { s.Value },
                            DataLabels = true,
                            Fill = fill
                        });
                        labels.Add(s.Label); // X-Axis Label isn't strictly needed if we use Legend, but good for structure
                    }
                    SeveritySeries = sevColl;
                    // We only have 1 column per series (Value), so X-Axis is just "0".
                    // To make it look like a categorical bar chart, we usually hide X-axis labels
                    // and use the Legend, OR we structure data differently.
                    // Given the request for "Small Bar Chart", Series-based coloring is visually best.
                    SeverityLabels = new string[] { "" };
                });
            });
        }

        private void UpdateCategoryMachineChart()
        {
            if (string.IsNullOrEmpty(SelectedErrorCategory) || _cachedRawData == null) return;
            var filteredList = InputHelper.PreProcessData(_cachedRawData, IsMachine00Excluded);
            var catStats = InputHelper.GetCategoryStats(filteredList, SelectedErrorCategory, _mappingService, _activeRules);
            var catColl = new SeriesCollection();
            foreach (var item in catStats) catColl.Add(new PieSeries { Title = $"MA-{item.Label}", Values = new ChartValues<double> { item.Value }, DataLabels = true, LabelPoint = p => $"{p.Y:N0}m" });
            ShiftSeries = catColl;
        }

        // --- Helpers ---
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
                _absoluteMinDate = min; SliderMinimum = 0; SliderMaximum = (max - min).TotalDays; if (SliderMaximum < 1) SliderMaximum = 1;
                if (_startDate < min) StartDate = min; if (_endDate > max) EndDate = max;
                RecalculateSliderValues(); _ = LoadDataWithDebounce();
            }
            catch { }
        }

        private void RecalculateSliderValues()
        { _isUpdatingFromSlider = true; SliderLowValue = Math.Max(0, (StartDate - _absoluteMinDate).TotalDays); SliderHighValue = Math.Min(SliderMaximum, (EndDate - _absoluteMinDate).TotalDays); _isUpdatingFromSlider = false; }

        private void UpdateDatesFromSlider()
        { _isUpdatingFromSlider = true; StartDate = _absoluteMinDate.AddDays(SliderLowValue); EndDate = _absoluteMinDate.AddDays(SliderHighValue); _isUpdatingFromSlider = false; _ = LoadDataWithDebounce(); }

        private void ExecuteChartClick(object obj)
        { if (obj is ChartPoint point) { string t = point.SeriesView.Title; if (point.SeriesView is ColumnSeries && _fullReasonNames.Count > (int)point.X) t = _fullReasonNames[(int)point.X]; if (ShiftSeries != null && ShiftSeries.Cast<object>().Any(s => s == point.SeriesView)) t += $"-{SelectedErrorCategory}"; DrillDownRequested?.Invoke(SelectedTable, StartDate, EndDate, t); } }

        private void ExecuteMoveDate(object p)
        { if (p is string s && s.Contains("|")) { var parts = s.Split('|'); if (int.TryParse(parts[1], out int d)) { if (parts[0] == "Start") StartDate = StartDate.AddDays(d); else EndDate = EndDate.AddDays(d); if (StartDate > EndDate) { if (parts[0] == "Start") EndDate = StartDate; else StartDate = EndDate; } } } }

        private void ExecuteConfigureCategories(object obj)
        { var win = new WPF_LoginForm.Views.CategoryConfigWindow(); if (Application.Current.MainWindow != null) win.Owner = Application.Current.MainWindow; if (win.ShowDialog() == true) { _activeRules = _mappingService.LoadRules(); _ = LoadDataWithDebounce(); } }

        private void SaveState()
        { try { string dir = Path.GetDirectoryName(StateFilePath); if (!Directory.Exists(dir)) Directory.CreateDirectory(dir); var state = new { SelectedTable, StartDate, EndDate, IsMachine00Excluded, SelectedErrorCategory }; File.WriteAllText(StateFilePath, JsonConvert.SerializeObject(state)); } catch { } }

        private void LoadState()
        { try { if (File.Exists(StateFilePath)) { dynamic state = JsonConvert.DeserializeObject(File.ReadAllText(StateFilePath)); if (state != null) { _selectedTable = state.SelectedTable; _startDate = state.StartDate; _endDate = state.EndDate; _isMachine00Excluded = state.IsMachine00Excluded; _selectedErrorCategory = state.SelectedErrorCategory; OnPropertyChanged(nameof(SelectedTable)); OnPropertyChanged(nameof(StartDate)); OnPropertyChanged(nameof(EndDate)); OnPropertyChanged(nameof(IsMachine00Excluded)); OnPropertyChanged(nameof(SelectedErrorCategory)); } } } catch { } }
    }
}