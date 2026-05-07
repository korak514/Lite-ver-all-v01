// ViewModels/ErrorManagementViewModel.cs
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

        public event Action<string, DateTime, DateTime, string> NavigateToDataReportRequested;

        private List<ErrorEventModel> _cachedRawData = new List<ErrorEventModel>();
        private List<string> _fullReasonNames = new List<string>();
        private List<CategoryRule> _activeRules;

        private bool _isSyncing = false;

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

        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                if (_startDate != value)
                {
                    DateTime safeValue = value;
                    if (_endDate != DateTime.MinValue && safeValue > _endDate) safeValue = _endDate;
                    _startDate = safeValue;
                    OnPropertyChanged();
                    if (!_isSyncing)
                    {
                        RecalculateSliderValues(true);
                    }
                }
            }
        }

        public DateTime EndDate
        {
            get => _endDate;
            set
            {
                if (_endDate != value)
                {
                    DateTime safeValue = value;
                    if (_startDate != DateTime.MinValue && safeValue < _startDate) safeValue = _startDate;
                    _endDate = safeValue;
                    OnPropertyChanged();
                    if (!_isSyncing)
                    {
                        RecalculateSliderValues(true);
                    }
                }
            }
        }

        private double _sliderMin = 0; private double _sliderMax = 100; private double _sliderLow; private double _sliderHigh; private DateTime _absoluteMinDate = DateTime.Today.AddYears(-1);
        public double SliderMinimum { get => _sliderMin; set => SetProperty(ref _sliderMin, value); }
        public double SliderMaximum { get => _sliderMax; set => SetProperty(ref _sliderMax, value); }

        public double SliderLowValue
        {
            get => _sliderLow;
            set
            {
                if (_sliderLow != value)
                {
                    _sliderLow = value;
                    OnPropertyChanged();
                    if (!_isSyncing) UpdateDatesFromSlider();
                }
            }
        }

        public double SliderHighValue
        {
            get => _sliderHigh;
            set
            {
                if (_sliderHigh != value)
                {
                    _sliderHigh = value;
                    OnPropertyChanged();
                    if (!_isSyncing) UpdateDatesFromSlider();
                }
            }
        }

        // --- Formatting Setting ---
        private bool _isMinToClockFormat;

        public bool IsMinToClockFormat
        {
            get => _isMinToClockFormat;
            set { if (SetProperty(ref _isMinToClockFormat, value)) { SaveState(); UpdateFormatterLogic(); _ = LoadDataWithDebounce(); } }
        }

        // --- PAGE 1 CHARTS ---
        private SeriesCollection _reasonSeries; public SeriesCollection ReasonSeries { get => _reasonSeries; set => SetProperty(ref _reasonSeries, value); }

        private string[] _reasonLabels; public string[] ReasonLabels { get => _reasonLabels; set => SetProperty(ref _reasonLabels, value); }
        private SeriesCollection _machineSeries; public SeriesCollection MachineSeries { get => _machineSeries; set => SetProperty(ref _machineSeries, value); }
        private SeriesCollection _shiftSeries; public SeriesCollection ShiftSeries { get => _shiftSeries; set => SetProperty(ref _shiftSeries, value); }

        private Func<double, string> _numberFormatter;
        public Func<double, string> NumberFormatter { get => _numberFormatter; set => SetProperty(ref _numberFormatter, value); }

        // --- Axis Steps & Limits for Granularity ---
        private double _reasonAxisStep = double.NaN;

        public double ReasonAxisStep { get => _reasonAxisStep; set => SetProperty(ref _reasonAxisStep, value); }

        private double _reasonAxisMax = double.NaN;
        public double ReasonAxisMax { get => _reasonAxisMax; set => SetProperty(ref _reasonAxisMax, value); }

        private double _severityAxisStep = double.NaN;
        public double SeverityAxisStep { get => _severityAxisStep; set => SetProperty(ref _severityAxisStep, value); }

        private double _severityAxisMax = double.NaN;
        public double SeverityAxisMax { get => _severityAxisMax; set => SetProperty(ref _severityAxisMax, value); }

        // --- PAGE 2 CHARTS ---
        private bool _isSecondPageActive;

        public bool IsSecondPageActive { get => _isSecondPageActive; set => SetProperty(ref _isSecondPageActive, value); }

        private SeriesCollection _efficiencySeries; public SeriesCollection EfficiencySeries { get => _efficiencySeries; set => SetProperty(ref _efficiencySeries, value); }
        private SeriesCollection _shiftImpactSeries; public SeriesCollection ShiftImpactSeries { get => _shiftImpactSeries; set => SetProperty(ref _shiftImpactSeries, value); }
        private SeriesCollection _severitySeries; public SeriesCollection SeveritySeries { get => _severitySeries; set => SetProperty(ref _severitySeries, value); }
        private string[] _severityLabels; public string[] SeverityLabels { get => _severityLabels; set => SetProperty(ref _severityLabels, value); }

        // --- PAGE 2 STATS ---
        private string _avgNetStopPerDay; public string AvgNetStopPerDay { get => _avgNetStopPerDay; set => SetProperty(ref _avgNetStopPerDay, value); }

        private string _avgErrorPerDay; public string AvgErrorPerDay { get => _avgErrorPerDay; set => SetProperty(ref _avgErrorPerDay, value); }
        private string _savedMaintenanceTime; public string SavedMaintenanceTime { get => _savedMaintenanceTime; set => SetProperty(ref _savedMaintenanceTime, value); }
        private string _avgWorkingPerDay; public string AvgWorkingPerDay { get => _avgWorkingPerDay; set => SetProperty(ref _avgWorkingPerDay, value); }
        private string _avgGrossStopPerDay; public string AvgGrossStopPerDay { get => _avgGrossStopPerDay; set => SetProperty(ref _avgGrossStopPerDay, value); }
        private string _mostFrequentMachine; public string MostFrequentMachine { get => _mostFrequentMachine; set => SetProperty(ref _mostFrequentMachine, value); }
        private string _savedNonCriticalTime; public string SavedNonCriticalTime { get => _savedNonCriticalTime; set => SetProperty(ref _savedNonCriticalTime, value); }

        // --- Commands ---
        public ICommand CopyCategoriesCommand { get; }

        public ICommand LoadDataCommand { get; set; }

        public ICommand ChartClickCommand { get; set; }
        public ICommand MoveDateCommand { get; set; }
        public ICommand ConfigureCategoriesCommand { get; }
        public ICommand TogglePageCommand { get; }
        public ICommand OpenDailyTimelineCommand { get; }

        // NEW: Command to open Print 15-Day Report Window
        public ICommand OpenPrintReportCommand { get; }

        private void ExecuteCopyCategories()
        {
            try
            {
                if (ErrorCategories != null && ErrorCategories.Count > 0)
                {
                    // Join the categories line by line
                    string textToCopy = string.Join(Environment.NewLine, ErrorCategories);

                    // Force execution on the UI thread and use SetDataObject for better reliability
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Clipboard.SetDataObject(textToCopy, true);
                    });

                    MessageBox.Show(WPF_LoginForm.Properties.Resources.Msg_CopySuccess,
                                    WPF_LoginForm.Properties.Resources.Msg_CopySuccess,
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(WPF_LoginForm.Properties.Resources.Msg_CopyNoData,
                                    WPF_LoginForm.Properties.Resources.PleaseSelect,
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(WPF_LoginForm.Properties.Resources.Msg_CopyError, ex.Message),
                                WPF_LoginForm.Properties.Resources.Str_Error,
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

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
            OpenDailyTimelineCommand = new ViewModelCommand(ExecuteOpenDailyTimeline);

            // FIX: Initialize Copy Command
            CopyCategoriesCommand = new ViewModelCommand(p => ExecuteCopyCategories());

            // NEW: Initialize Print Command
            OpenPrintReportCommand = new ViewModelCommand(ExecuteOpenPrintReport);

            UpdateFormatterLogic();
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

        public void Deactivate()
        {
            IsActiveView = false;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private void UpdateFormatterLogic()
        {
            NumberFormatter = (val) => TimeFormatHelper.FormatDuration(val, IsMinToClockFormat);
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
                if (token.IsCancellationRequested || !IsActiveView) return;
                _cachedRawData = rawData;

                var filteredList = InputHelper.PreProcessData(rawData, IsMachine00Excluded);
                var strictNoM00List = InputHelper.PreProcessData(rawData, true);

                var reasonStats = InputHelper.GetTopReasons(filteredList, _mappingService, _activeRules, 5);
                var reasonValues = new ChartValues<double>(reasonStats.Select(x => x.Value));
                var reasonLabels = reasonStats.Select((x, index) => { string l = x.Label; if (l.Length > 15) l = l.Substring(0, 12) + "..."; return (index % 2 != 0) ? "\n" + l : l; }).ToArray();
                var reasonFullNames = reasonStats.Select(x => x.Label).ToList();
                var machineStats = InputHelper.GetMachineStats(filteredList);

                var page2Stats = InputHelper.CalculatePage2Stats(rawData, filteredList, strictNoM00List, StartDate, EndDate, NumberFormatter, IsMachine00Excluded);
                var shiftStats = InputHelper.GetShiftStats(rawData, filteredList);
                var severityStats = InputHelper.GetSeverityStats(filteredList);

                var uniqueCategories = filteredList.Select(x => _mappingService.GetMappedCategory(x.ErrorDescription, _activeRules)).Distinct().OrderBy(x => x).ToList();

                double maxReasonVal = reasonStats.Any() ? reasonStats.Max(x => x.Value) : 0;
                double calculatedReasonStep = CalculateCleanStep(maxReasonVal);
                double targetReasonMax = Math.Ceiling((maxReasonVal * 1.15) / calculatedReasonStep) * calculatedReasonStep;
                if (targetReasonMax <= 0) targetReasonMax = 60;

                double maxSeverityVal = severityStats.Any() ? severityStats.Max(x => x.Value) : 0;
                double calculatedSeverityStep = CalculateCleanStep(maxSeverityVal);
                double targetSeverityMax = Math.Ceiling((maxSeverityVal * 1.15) / calculatedSeverityStep) * calculatedSeverityStep;
                if (targetSeverityMax <= 0) targetSeverityMax = 60;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (token.IsCancellationRequested || !IsActiveView) return;

                    ReasonAxisStep = calculatedReasonStep;
                    ReasonAxisMax = targetReasonMax;

                    SeverityAxisStep = calculatedSeverityStep;
                    SeverityAxisMax = targetSeverityMax;

                    string prevCat = SelectedErrorCategory;
                    ErrorCategories.Clear();
                    foreach (var c in uniqueCategories) ErrorCategories.Add(c);
                    if (!string.IsNullOrEmpty(prevCat) && ErrorCategories.Contains(prevCat)) _selectedErrorCategory = prevCat;
                    else if (ErrorCategories.Any()) _selectedErrorCategory = ErrorCategories[0];
                    OnPropertyChanged(nameof(SelectedErrorCategory));

                    _fullReasonNames = reasonFullNames;
                    ReasonLabels = reasonLabels;

                    ReasonSeries = new SeriesCollection {
                        new ColumnSeries {
                            Title = WPF_LoginForm.Properties.Resources.Chart_LongestIncident,
                            Values = reasonValues,
                            DataLabels = true,
                            Fill = Brushes.DodgerBlue,
                            Foreground = Brushes.LightSkyBlue
                        }
                    };

                    var machineColl = new SeriesCollection();
                    foreach (var item in machineStats) machineColl.Add(new PieSeries { Title = $"MA-{item.Label}", Values = new ChartValues<double> { item.Value }, PushOut = 2 });
                    MachineSeries = machineColl;
                    UpdateCategoryMachineChart();

                    AvgNetStopPerDay = page2Stats.AvgNetStopPerDay;
                    AvgErrorPerDay = page2Stats.AvgErrorPerDay;
                    SavedMaintenanceTime = page2Stats.SavedMaintenanceTime;
                    AvgWorkingPerDay = page2Stats.AvgWorkingPerDay;
                    AvgGrossStopPerDay = page2Stats.AvgGrossStopPerDay;
                    MostFrequentMachine = page2Stats.MostFrequentMachine;
                    SavedNonCriticalTime = page2Stats.SavedNonCriticalTime;

                    string errorText = WPF_LoginForm.Properties.Resources.AnalyticsP2_Error ?? "Errors";
                    string stopText = WPF_LoginForm.Properties.Resources.AnalyticsP2_Stop ?? "Stops";

                    EfficiencySeries = new SeriesCollection {
                        new PieSeries { Title = errorText, Values = new ChartValues<double> { page2Stats.TotalErrorDuration }, DataLabels = true, Fill = Brushes.OrangeRed, LabelPoint = p => TimeFormatHelper.FormatDuration(p.Y, IsMinToClockFormat) },
                        new PieSeries { Title = stopText, Values = new ChartValues<double> { page2Stats.TotalStopDuration }, DataLabels = true, Fill = Brushes.DodgerBlue, LabelPoint = p => TimeFormatHelper.FormatDuration(p.Y, IsMinToClockFormat) }
                    };

                    var shiftColl = new SeriesCollection();
                    foreach (var s in shiftStats) shiftColl.Add(new PieSeries { Title = s.Label, Values = new ChartValues<double> { s.Value }, DataLabels = true, LabelPoint = p => $"{TimeFormatHelper.FormatDuration(p.Y, IsMinToClockFormat)} ({p.Participation:P0})" });
                    ShiftImpactSeries = shiftColl;

                    var sevColl = new SeriesCollection();
                    foreach (var s in severityStats)
                    {
                        Brush fill = Brushes.Gray;
                        if (s.Label.Contains("Micro")) fill = Brushes.MediumSeaGreen;
                        else if (s.Label.Contains("Minor")) fill = Brushes.Orange;
                        else if (s.Label.Contains("Major")) fill = Brushes.Crimson;

                        sevColl.Add(new ColumnSeries
                        {
                            Title = s.Label,
                            Values = new ChartValues<double> { s.Value },
                            DataLabels = true,
                            Fill = fill,
                            Foreground = Brushes.LightSkyBlue,
                            ColumnPadding = 30,
                            MaxColumnWidth = 70,
                            LabelPoint = p => TimeFormatHelper.FormatDuration(p.Y, IsMinToClockFormat)
                        });
                    }
                    SeveritySeries = sevColl;
                    SeverityLabels = new string[] { "" };
                });
            });
        }

        private double CalculateCleanStep(double maxMinutes)
        {
            double maxHours = maxMinutes / 60.0;

            if (maxHours <= 1) return 10;        // 10m, 20m, 30m
            if (maxHours <= 3) return 30;        // 30m, 1h, 1h30m
            if (maxHours <= 10) return 2 * 60;   // 2h, 4h, 6h
            if (maxHours <= 15) return 3 * 60;   // 3h, 6h, 9h, 12h, 15h
            if (maxHours <= 30) return 5 * 60;   // 5h, 10h, 15h, 20h
            if (maxHours <= 60) return 10 * 60;  // 10h, 20h, 30h
            if (maxHours <= 120) return 20 * 60; // 20h, 40h, 60h
            if (maxHours <= 300) return 50 * 60; // 50h, 100h
            if (maxHours <= 600) return 100 * 60;// 100h, 200h

            return 200 * 60;
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

        private void ExecuteConfigureCategories(object obj)
        {
            var win = new WPF_LoginForm.Views.CategoryConfigWindow();
            if (Application.Current.MainWindow != null) win.Owner = Application.Current.MainWindow;

            if (win.ShowDialog() == true)
            {
                _activeRules = _mappingService.LoadRules();
                _ = LoadDataWithDebounce();
            }
        }

        private void ExecuteChartClick(object obj)
        {
            if (obj is ChartPoint point)
            {
                string filterText = point.SeriesView.Title;

                if (point.SeriesView.Title == "MA-Others")
                {
                    SeriesCollection activeColl = (MachineSeries?.Cast<object>().Any(s => s == point.SeriesView) == true) ? MachineSeries : ShiftSeries;
                    if (activeColl != null)
                    {
                        var visible = activeColl.Cast<PieSeries>().Select(s => s.Title.Replace("MA-", "")).Where(t => t != "Others").ToList();
                        if (activeColl == ShiftSeries)
                            filterText = $"MACHINE_CATEGORY_OTHERS|{string.Join(",", visible)}|{SelectedErrorCategory}";
                        else
                            filterText = "MACHINE_OTHERS|" + string.Join(",", visible);
                    }
                }
                else if (point.SeriesView is ColumnSeries && _fullReasonNames.Count > (int)point.X)
                {
                    filterText = _fullReasonNames[(int)point.X];
                }
                else if (ShiftSeries?.Cast<object>().Any(s => s == point.SeriesView) == true)
                {
                    filterText = $"MACHINE_CATEGORY|{point.SeriesView.Title.Replace("MA-", "")}|{SelectedErrorCategory}";
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var drillDownVm = new ErrorDrillDownViewModel(
                        _cachedRawData,
                        $"{WPF_LoginForm.Properties.Resources.Str_DetailedAnalysis} {SelectedTable}",
                        filterText,
                        new List<string>(),
                        IsMinToClockFormat,
                        IsMachine00Excluded
                    );

                    var win = new WPF_LoginForm.Views.ErrorDrillDownWindow();
                    win.SetViewModel(drillDownVm);

                    drillDownVm.OnNavigateRequested = (errorEvent) =>
                    {
                        win.DialogResult = true;
                        win.Close();

                        NavigateToDataReportRequested?.Invoke(
                            SelectedTable,
                            errorEvent.Date.Date,
                            errorEvent.Date.Date.AddDays(1).AddSeconds(-1),
                            errorEvent.RawData ?? errorEvent.ErrorDescription);
                    };

                    if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
                    {
                        win.Owner = Application.Current.MainWindow;
                    }

                    win.ShowDialog();
                });
            }
        }

        private void ExecuteOpenDailyTimeline(object obj)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var win = new WPF_LoginForm.Views.DailyTimelineWindow(_repository, SelectedTable, EndDate);
                if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
                {
                    win.Owner = Application.Current.MainWindow;
                }
                win.Show();
            });
        }

        private void ExecuteOpenPrintReport(object obj)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var vm = new PrintTimelineSetupViewModel(_repository, new DialogService());

                // Pre-select the table and date if we are already viewing a specific range
                vm.SelectedTable = this.SelectedTable;
                vm.EndDate = this.EndDate;
                vm.StartDate = this.EndDate.AddDays(-14); // 15 days logic

                var win = new WPF_LoginForm.Views.PrintTimelineSetupWindow(vm);
                if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
                {
                    win.Owner = Application.Current.MainWindow;
                }
                win.ShowDialog();
            });
        }

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

        private void RecalculateSliderValues(bool triggerLoad = false)
        {
            if (_isSyncing) return;
            _isSyncing = true;

            _sliderLow = Math.Max(0, (StartDate - _absoluteMinDate).TotalDays);
            _sliderHigh = Math.Min(SliderMaximum, (EndDate - _absoluteMinDate).TotalDays);

            OnPropertyChanged(nameof(SliderLowValue));
            OnPropertyChanged(nameof(SliderHighValue));

            _isSyncing = false;

            if (triggerLoad)
            {
                SaveState();
                _ = LoadDataWithDebounce();
            }
        }

        private void UpdateDatesFromSlider()
        {
            if (_isSyncing) return;
            _isSyncing = true;

            if (_sliderLow > _sliderHigh)
            {
                double tmp = _sliderLow;
                _sliderLow = _sliderHigh;
                _sliderHigh = tmp;
                OnPropertyChanged(nameof(SliderLowValue));
                OnPropertyChanged(nameof(SliderHighValue));
            }

            _startDate = _absoluteMinDate.AddDays(_sliderLow);
            _endDate = _absoluteMinDate.AddDays(_sliderHigh);
            OnPropertyChanged(nameof(StartDate));
            OnPropertyChanged(nameof(EndDate));

            _isSyncing = false;

            SaveState();
            _ = LoadDataWithDebounce();
        }

        private void ExecuteMoveDate(object p)
        { if (p is string s && s.Contains("|")) { var parts = s.Split('|'); if (int.TryParse(parts[1], out int d)) { if (parts[0] == "Start") StartDate = StartDate.AddDays(d); else EndDate = EndDate.AddDays(d); if (StartDate > EndDate) { if (parts[0] == "Start") EndDate = StartDate; else StartDate = EndDate; } } } }

        private void SaveState()
        { try { string dir = Path.GetDirectoryName(StateFilePath); if (!Directory.Exists(dir)) Directory.CreateDirectory(dir); var state = new { SelectedTable, StartDate, EndDate, IsMachine00Excluded, SelectedErrorCategory, IsMinToClockFormat }; File.WriteAllText(StateFilePath, JsonConvert.SerializeObject(state)); } catch { } }

        private void LoadState()
        { try { if (File.Exists(StateFilePath)) { dynamic state = JsonConvert.DeserializeObject(File.ReadAllText(StateFilePath)); if (state != null) { _selectedTable = state.SelectedTable; _startDate = state.StartDate; _endDate = state.EndDate; _isMachine00Excluded = state.IsMachine00Excluded; _selectedErrorCategory = state.SelectedErrorCategory; _isMinToClockFormat = state.IsMinToClockFormat ?? false; OnPropertyChanged(nameof(SelectedTable)); OnPropertyChanged(nameof(StartDate)); OnPropertyChanged(nameof(EndDate)); OnPropertyChanged(nameof(IsMachine00Excluded)); OnPropertyChanged(nameof(SelectedErrorCategory)); OnPropertyChanged(nameof(IsMinToClockFormat)); } } } catch { } }
    }
}