using LiveCharts;
using LiveCharts.Wpf;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows; // Required for MessageBox
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
                        ExecuteLoadData(null);
                }
            }
        }

        // --- 2. NEW: Toggle for MA-00 ---
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

        // --- 4. Charts ---
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

            // Default selection logic: Only if SelectedTable is null/empty
            if (string.IsNullOrEmpty(SelectedTable) && TableNames.Count > 0)
            {
                _selectedTable = TableNames[0];
                OnPropertyChanged(nameof(SelectedTable));
            }

            _absoluteMinDate = DateTime.Today.AddYears(-1);
            SliderMinimum = 0; SliderMaximum = 365;
            RecalculateSliderValues();

            if (!string.IsNullOrEmpty(SelectedTable)) ExecuteLoadData(null);
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

                    if (StartDate > EndDate)
                    {
                        if (p[0] == "Start") EndDate = StartDate; else StartDate = EndDate;
                    }
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
                return;
            }

            // --- 1. GLOBAL FILTERING ---
            var filteredData = _cachedRawData.Where(x => !string.IsNullOrWhiteSpace(x.ErrorDescription)).ToList();

            if (IsMachine00Excluded)
            {
                filteredData = filteredData.Where(x =>
                    x.MachineCode != "00" &&
                    x.MachineCode != "0" &&
                    x.MachineCode != "MA-00" &&
                    !(x.SectionCode == "MA" && x.MachineCode == "00")
                ).ToList();
            }

            if (!filteredData.Any()) { ReasonSeries = null; MachineSeries = null; ShiftSeries = null; return; }

            // --- 2. BAR CHART (Max Duration, Top 6) ---
            var reasonStats = filteredData
                .GroupBy(x => x.ErrorDescription)
                .Select(g => new { Reason = g.Key, MaxDuration = g.Max(x => x.DurationMinutes) })
                .OrderByDescending(x => x.MaxDuration)
                .Take(6)
                .ToList();

            _fullReasonNames = reasonStats.Select(x => x.Reason).ToList();

            ReasonLabels = reasonStats.Select((x, index) =>
            {
                var words = x.Reason.Split(new[] { '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string label = x.Reason;
                if (words.Length >= 2) label = $"{words[0]}-{words[1]}";
                else if (words.Length == 1) label = words[0];
                return (index % 2 != 0) ? "\n" + label : label;
            }).ToArray();

            ReasonSeries = new SeriesCollection
            {
                new ColumnSeries
                {
                    Title = "Longest Incident",
                    Values = new ChartValues<int>(reasonStats.Select(x => x.MaxDuration)),
                    DataLabels = true,
                    Fill = Brushes.DodgerBlue,
                    FontSize = 11
                }
            };
            OnPropertyChanged(nameof(ReasonLabels));

            // --- 3. MACHINE PIE (Top 9) ---
            var machineStats = filteredData.GroupBy(x => x.MachineCode)
                .Select(g => new { Machine = g.Key, TotalTime = g.Sum(x => x.DurationMinutes) })
                .OrderByDescending(x => x.TotalTime).ToList();

            var topMachines = machineStats.Take(9).ToList();
            var otherMachinesSum = machineStats.Skip(9).Sum(x => x.TotalTime);

            var machineColl = new SeriesCollection();
            foreach (var item in topMachines)
            {
                machineColl.Add(new PieSeries
                {
                    Title = $"MA-{item.Machine}",
                    Values = new ChartValues<int> { item.TotalTime },
                    DataLabels = false,
                    LabelPoint = point => $"{point.Y}m",
                    PushOut = 2
                });
            }
            if (otherMachinesSum > 0)
            {
                machineColl.Add(new PieSeries { Title = "Others", Values = new ChartValues<int> { otherMachinesSum }, DataLabels = false, Fill = Brushes.Gray, PushOut = 0 });
            }
            MachineSeries = machineColl;

            // --- 4. SHIFT PIE ---
            var shiftStats = _cachedRawData
                .Where(x => !IsMachine00Excluded || (x.MachineCode != "00" && x.MachineCode != "0"))
                .Select(x => new { x.Date, x.Shift, x.RowTotalStopMinutes })
                .Distinct()
                .GroupBy(x => x.Shift)
                .Select(g => new { Shift = g.Key, TotalTime = g.Sum(x => x.RowTotalStopMinutes) })
                .ToList();

            var shiftColl = new SeriesCollection();
            foreach (var item in shiftStats)
            {
                if (item.TotalTime > 0)
                {
                    shiftColl.Add(new PieSeries
                    {
                        Title = item.Shift,
                        Values = new ChartValues<int> { item.TotalTime },
                        DataLabels = true,
                        LabelPoint = point => $"{point.Y} min ({point.Participation:P0})"
                    });
                }
            }
            ShiftSeries = shiftColl;
        }

        private void ExecuteChartClick(object obj)
        {
            if (obj is ChartPoint point)
            {
                string filterText = point.SeriesView.Title;
                if (filterText == "Others") return;

                // --- FOOLPROOF VALIDATION START ---
                string tableToSend = SelectedTable;

                // Validation 1: Check Null/Empty
                if (string.IsNullOrEmpty(tableToSend))
                {
                    if (TableNames.Count > 0)
                    {
                        tableToSend = TableNames[0];
                        SelectedTable = tableToSend; // Auto-correct UI
                    }
                    else return;
                }

                // Validation 2: Ensure consistency with available tables
                if (!TableNames.Contains(tableToSend))
                {
                    if (TableNames.Count > 0)
                    {
                        tableToSend = TableNames[0];
                        SelectedTable = tableToSend; // Auto-correct UI
                    }
                }
                // --- FOOLPROOF VALIDATION END ---

                // Handle Bar Chart clicks
                if (point.SeriesView is ColumnSeries)
                {
                    int index = (int)point.X;
                    if (_fullReasonNames != null && index >= 0 && index < _fullReasonNames.Count)
                    {
                        string description = _fullReasonNames[index];
                        var specificEvent = _cachedRawData
                            .Where(x => x.ErrorDescription == description)
                            .OrderByDescending(x => x.DurationMinutes)
                            .FirstOrDefault();

                        if (specificEvent != null)
                            filterText = $"{specificEvent.DurationMinutes}-MA-{specificEvent.MachineCode}-{specificEvent.ErrorDescription}";
                        else
                            filterText = description;
                    }
                }

                // Do NOT strip "MA-" (Ensures Global Search finds "MA-01" uniquely)
                // if (!string.IsNullOrEmpty(filterText) && filterText.StartsWith("MA-")) filterText = filterText.Replace("MA-", "");

                // Use the validated 'tableToSend'
                DrillDownRequested?.Invoke(tableToSend, StartDate, EndDate, filterText);
            }
        }

        // --- State Management ---
        private void SaveState()
        { try { var state = new AnalyticsState { SelectedTable = SelectedTable, StartDate = StartDate, EndDate = EndDate, ExcludeMachine00 = IsMachine00Excluded }; File.WriteAllText(StateFileName, JsonConvert.SerializeObject(state)); } catch { } }

        private void LoadState()
        { try { if (File.Exists(StateFileName)) { var state = JsonConvert.DeserializeObject<AnalyticsState>(File.ReadAllText(StateFileName)); if (state != null) { if (state.StartDate > DateTime.MinValue) _startDate = state.StartDate; if (state.EndDate > DateTime.MinValue) _endDate = state.EndDate; _isMachine00Excluded = state.ExcludeMachine00; OnPropertyChanged(nameof(IsMachine00Excluded)); if (!string.IsNullOrEmpty(state.SelectedTable) && TableNames.Contains(state.SelectedTable)) { _selectedTable = state.SelectedTable; OnPropertyChanged(nameof(SelectedTable)); } } } } catch { } }

        private class AnalyticsState
        { public string SelectedTable { get; set; } public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } public bool ExcludeMachine00 { get; set; } }
    }
}