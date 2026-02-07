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
using WPF_LoginForm.Repositories;

namespace WPF_LoginForm.ViewModels
{
    public class ErrorManagementViewModel : ViewModelBase
    {
        private readonly IDataRepository _repository;
        private const string StateFileName = "analytics_state.json";

        public event Action<string, DateTime, DateTime, string> DrillDownRequested;

        private List<ErrorEventModel> _cachedRawData = new List<ErrorEventModel>();
        private List<string> _fullReasonNames = new List<string>();

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
                    if (!string.IsNullOrEmpty(_selectedTable))
                        _ = OnTableChangedAsync();
                }
            }
        }

        // --- 2. Error Category Properties ---
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
                    ExecuteLoadData(null);
                }
            }
        }

        // --- 3. Date & Slider Logic ---
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

        public ErrorManagementViewModel(IDataRepository repository)
        {
            _repository = repository;
            LoadDataCommand = new ViewModelCommand(ExecuteLoadData);
            ChartClickCommand = new ViewModelCommand(ExecuteChartClick);
            MoveDateCommand = new ViewModelCommand(ExecuteMoveDate);
            NumberFormatter = value => value.ToString("N0") + " min";

            LoadTableList();
        }

        private async void LoadTableList()
        {
            var tables = await _repository.GetTableNamesAsync();
            TableNames.Clear();
            foreach (var t in tables) TableNames.Add(t);

            LoadState();

            if (string.IsNullOrEmpty(SelectedTable) && TableNames.Count > 0)
            {
                _selectedTable = TableNames[0];
                OnPropertyChanged(nameof(SelectedTable));
            }
            if (!string.IsNullOrEmpty(SelectedTable)) await OnTableChangedAsync();
        }

        private async Task OnTableChangedAsync()
        {
            await UpdateDateRangeFromDbAsync();
            ExecuteLoadData(null);
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
            ExecuteLoadData(null);
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

        private async void ExecuteLoadData(object obj)
        {
            if (string.IsNullOrEmpty(SelectedTable)) return;

            _cachedRawData = await _repository.GetErrorDataAsync(StartDate, EndDate, SelectedTable);

            if (_cachedRawData == null || !_cachedRawData.Any())
            {
                ReasonSeries = null; MachineSeries = null; ShiftSeries = null;
                ErrorCategories.Clear();
                return;
            }

            var baseFiltered = _cachedRawData.Where(x => !string.IsNullOrWhiteSpace(x.ErrorDescription));
            if (IsMachine00Excluded)
            {
                baseFiltered = baseFiltered.Where(x => x.MachineCode != "00" && x.MachineCode != "0" && x.MachineCode != "MA-00" && !(x.SectionCode == "MA" && x.MachineCode == "00"));
            }
            var filteredList = baseFiltered.ToList();

            var currentCat = SelectedErrorCategory;
            var uniqueCategories = filteredList
                .Select(x => x.ErrorDescription.Split('-')[0].Trim().ToUpper())
                .Distinct().OrderBy(x => x).ToList();

            ErrorCategories.Clear();
            foreach (var cat in uniqueCategories) ErrorCategories.Add(cat);

            if (!string.IsNullOrEmpty(currentCat) && ErrorCategories.Contains(currentCat))
                _selectedErrorCategory = currentCat;
            else if (ErrorCategories.Any())
                _selectedErrorCategory = ErrorCategories[0];

            OnPropertyChanged(nameof(SelectedErrorCategory));

            if (!filteredList.Any()) { ReasonSeries = null; MachineSeries = null; ShiftSeries = null; return; }

            // --- REVERTED BAR CHART LOGIC (Take 5, Staggered Pattern) ---
            var reasonStats = filteredList.GroupBy(x => x.ErrorDescription)
                .Select(g => new { Reason = g.Key, MaxDuration = g.Max(x => x.DurationMinutes) })
                .OrderByDescending(x => x.MaxDuration)
                .Take(5) // Limit changed from 6 to 5
                .ToList();

            _fullReasonNames = reasonStats.Select(x => x.Reason).ToList();

            ReasonLabels = reasonStats.Select((x, index) =>
            {
                string label = x.Reason.Length > 22 ? x.Reason.Substring(0, 19) + "..." : x.Reason;
                // Pattern: __--__--
                return (index % 2 != 0) ? "\n" + label : label;
            }).ToArray();

            ReasonSeries = new SeriesCollection
            {
                new ColumnSeries
                {
                    Title = "Longest Incident",
                    Values = new ChartValues<int>(reasonStats.Select(x => x.MaxDuration)),
                    DataLabels = true,
                    Fill = Brushes.DodgerBlue
                }
            };
            OnPropertyChanged(nameof(ReasonLabels));

            // Machine Pie
            var machineStats = filteredList.GroupBy(x => x.MachineCode)
                .Select(g => new { Machine = g.Key, TotalTime = g.Sum(x => x.DurationMinutes) })
                .OrderByDescending(x => x.TotalTime).Take(9).ToList();

            var machineColl = new SeriesCollection();
            foreach (var item in machineStats) machineColl.Add(new PieSeries { Title = $"MA-{item.Machine}", Values = new ChartValues<int> { item.TotalTime }, PushOut = 2 });
            MachineSeries = machineColl;

            UpdateCategoryMachineChart();
        }

        private void UpdateCategoryMachineChart()
        {
            if (string.IsNullOrEmpty(SelectedErrorCategory) || _cachedRawData == null) return;

            var baseFiltered = _cachedRawData.Where(x => !string.IsNullOrWhiteSpace(x.ErrorDescription));
            if (IsMachine00Excluded) baseFiltered = baseFiltered.Where(x => x.MachineCode != "00" && x.MachineCode != "0" && x.MachineCode != "MA-00");

            var categoryStats = baseFiltered
                .Where(x => x.ErrorDescription.StartsWith(SelectedErrorCategory, StringComparison.OrdinalIgnoreCase))
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
                    LabelPoint = p => $"{p.Y}m ({p.Participation:P0})"
                });
            }
            ShiftSeries = catColl;
        }

        private void ExecuteChartClick(object obj)
        {
            if (obj is ChartPoint point)
            {
                string filterText = point.SeriesView.Title;
                if (filterText == "Others" || string.IsNullOrEmpty(SelectedTable)) return;

                if (point.SeriesView is PieSeries && !string.IsNullOrEmpty(SelectedErrorCategory) && filterText.StartsWith("MA-"))
                {
                    filterText = $"{filterText}-{SelectedErrorCategory}";
                }
                else if (point.SeriesView is ColumnSeries)
                {
                    int index = (int)point.X;
                    if (index >= 0 && index < _fullReasonNames.Count) filterText = _fullReasonNames[index];
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