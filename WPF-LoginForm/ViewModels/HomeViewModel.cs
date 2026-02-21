// ViewModels/HomeViewModel.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading; // Required for CancellationToken
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WPF_LoginForm.Models;
using WPF_LoginForm.Properties;
using WPF_LoginForm.Repositories;
using WPF_LoginForm.Services;

namespace WPF_LoginForm.ViewModels
{
    public class HomeViewModel : ViewModelBase
    {
        private readonly IDataRepository _dataRepository;
        private readonly IDialogService _dialogService;
        private readonly ILogger _logger;
        private readonly DashboardStorageService _storageService;
        private readonly Random _random = new Random();

        // NEW: Lock object for thread-safe random colors
        private readonly object _randomLock = new object();

        private List<DashboardConfiguration> _dashboardConfigurations;
        private bool _isUpdatingDates = false;
        private DateTime _minSliderDate;
        private DateTime _maxSliderDate;
        private bool _isActive = false;

        // Cancellation Token
        private CancellationTokenSource _cts;

        public ObservableCollection<string> ErrorCategories { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> DashboardFiles { get; } = new ObservableCollection<string>();

        private string _selectedErrorCategory;
        public string SelectedErrorCategory
        { get => _selectedErrorCategory; set { if (SetProperty(ref _selectedErrorCategory, value)) { InitializeSliders(); LoadAllChartsData(); AutoSave(); } } }

        private string _selectedDashboardFile;

        public string SelectedDashboardFile
        {
            get => _selectedDashboardFile;
            set
            {
                if (SetProperty(ref _selectedDashboardFile, value) && _isActive && !string.IsNullOrEmpty(value))
                    Task.Delay(50).ContinueWith(_ => Application.Current.Dispatcher.Invoke(() => LoadSelectedDashboardFile(value)));
            }
        }

        public bool IsDashboardSelectorVisible => Settings.Default.AutoImportEnabled && DashboardFiles.Any();

        public event Action<string, DateTime, DateTime> DrillDownRequested;

        public ICommand ConfigureCommand { get; }
        public ICommand ImportCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand RecolorChartsCommand { get; }
        public ICommand ToggleFilterModeCommand { get; }
        public ICommand ChartClickCommand { get; }

        public bool IsSnapshotExport { get; set; } = true;

        private bool _isFilterByDate = true;
        public bool IsFilterByDate
        { get => _isFilterByDate; set { if (SetProperty(ref _isFilterByDate, value)) { InitializeSliders(); LoadAllChartsData(); AutoSave(); } } }

        private bool _ignoreNonDateData = true;
        public bool IgnoreNonDateData
        { get => _ignoreNonDateData; set { if (SetProperty(ref _ignoreNonDateData, value)) { LoadAllChartsData(); AutoSave(); } } }

        private bool _useIdToDateConversion = false;
        public bool UseIdToDateConversion
        { get => _useIdToDateConversion; set { if (SetProperty(ref _useIdToDateConversion, value)) AutoSave(); } }

        private DateTime _initialDateForConversion = DateTime.Today.AddYears(-1);
        public DateTime InitialDateForConversion
        { get => _initialDateForConversion; set { if (SetProperty(ref _initialDateForConversion, value)) AutoSave(); } }

        public bool IsDateFilterVisible => Settings.Default.ShowDashboardDateFilter;
        public int SliderTickFrequency
        { get => Settings.Default.DashboardDateTickSize; set { if (Settings.Default.DashboardDateTickSize != value) { Settings.Default.DashboardDateTickSize = value; Settings.Default.Save(); OnPropertyChanged(); } } }

        public class TickOption
        { public string Label { get; set; } public int Value { get; set; } }

        public List<TickOption> TickOptions { get; } = new List<TickOption> { new TickOption { Label = "1 Month", Value = 1 }, new TickOption { Label = "3 Months", Value = 3 }, new TickOption { Label = "6 Months", Value = 6 }, new TickOption { Label = "1 Year", Value = 12 } };

        private bool _isDateFilterEnabled;
        public bool IsDateFilterEnabled { get => _isDateFilterEnabled; private set => SetProperty(ref _isDateFilterEnabled, value); }

        private DateTime _startDate;
        public DateTime StartDate
        { get => _startDate; set { if (_startDate != value) { _startDate = value; OnPropertyChanged(); if (!_isUpdatingDates && _isActive && IsFilterByDate) { LoadAllChartsData(); UpdateSlidersFromDates(); AutoSave(); } } } }

        private DateTime _endDate;
        public DateTime EndDate
        { get => _endDate; set { if (_endDate != value) { _endDate = value; OnPropertyChanged(); if (!_isUpdatingDates && _isActive && IsFilterByDate) { LoadAllChartsData(); UpdateSlidersFromDates(); AutoSave(); } } } }

        private double _sliderMaximum;
        public double SliderMaximum { get => _sliderMaximum; set => SetProperty(ref _sliderMaximum, value); }

        private double _startMonthSliderValue;
        public double StartMonthSliderValue
        { get => _startMonthSliderValue; set { if (SetProperty(ref _startMonthSliderValue, value) && !_isUpdatingDates && _isActive) { if (IsFilterByDate) UpdateDatesFromSliders(); else { UpdateTooltips(); LoadAllChartsData(); } } } }

        private double _endMonthSliderValue;
        public double EndMonthSliderValue
        { get => _endMonthSliderValue; set { if (SetProperty(ref _endMonthSliderValue, value) && !_isUpdatingDates && _isActive) { if (IsFilterByDate) UpdateDatesFromSliders(); else { UpdateTooltips(); LoadAllChartsData(); } } } }

        private string _startSliderTooltip; public string StartSliderTooltip { get => _startSliderTooltip; set => SetProperty(ref _startSliderTooltip, value); }
        private string _endSliderTooltip; public string EndSliderTooltip { get => _endSliderTooltip; set => SetProperty(ref _endSliderTooltip, value); }

        public SeriesCollection Chart1Series { get; } = new SeriesCollection();
        public SeriesCollection Chart2Series { get; } = new SeriesCollection();
        public SeriesCollection Chart3Series { get; } = new SeriesCollection();
        public SeriesCollection Chart4Series { get; } = new SeriesCollection();
        public SeriesCollection Chart5Series { get; } = new SeriesCollection();

        public AxesCollection Chart1X { get; } = new AxesCollection(); public AxesCollection Chart1Y { get; } = new AxesCollection();
        public AxesCollection Chart2X { get; } = new AxesCollection(); public AxesCollection Chart2Y { get; } = new AxesCollection();
        public AxesCollection Chart3X { get; } = new AxesCollection(); public AxesCollection Chart3Y { get; } = new AxesCollection();
        public AxesCollection Chart5X { get; } = new AxesCollection(); public AxesCollection Chart5Y { get; } = new AxesCollection();

        public Func<double, string> DateFormatter { get; }

        public HomeViewModel(IDataRepository dataRepository, IDialogService dialogService, ILogger logger)
        {
            _dataRepository = dataRepository;
            _dialogService = dialogService;
            _logger = logger;
            _storageService = new DashboardStorageService();

            _dashboardConfigurations = new List<DashboardConfiguration>();

            ConfigureCommand = new ViewModelCommand(p => ShowConfigurationWindow());
            ImportCommand = new ViewModelCommand(p => ImportConfiguration());
            ExportCommand = new ViewModelCommand(p => ExportConfiguration());
            RecolorChartsCommand = new ViewModelCommand(p => LoadAllChartsData());
            ToggleFilterModeCommand = new ViewModelCommand(p => IsFilterByDate = !IsFilterByDate);
            ChartClickCommand = new ViewModelCommand(ExecuteChartClick);

            _startDate = DateTime.Today.AddYears(-1);
            _endDate = DateTime.Today;

            DateFormatter = value => (value < DateTime.MinValue.Ticks || value > DateTime.MaxValue.Ticks) ? "" : new DateTime((long)value).ToString("d");

            InitializeEmptyAxes();
            TryLoadAutoSave();
        }

        private async void LoadAllChartsData(Dictionary<(int, string), string> colorMap = null)
        {
            // FIX: Properly dispose old token to prevent memory leaks (Bug #10)
            if (_cts != null) { _cts.Cancel(); _cts.Dispose(); }
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            if (_dashboardConfigurations == null || !_isActive) return;

            // FIX: Ensure configs exist to prevent empty loop
            if (!_dashboardConfigurations.Any())
                for (int i = 1; i <= 5; i++) _dashboardConfigurations.Add(new DashboardConfiguration { ChartPosition = i, IsEnabled = false });

            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Chart1Series.Clear(); Chart2Series.Clear(); Chart3Series.Clear(); Chart4Series.Clear(); Chart5Series.Clear();
                    ClearAndReinitializeAxes();
                });

                var validConfigs = _dashboardConfigurations
                    .Where(c => c.IsEnabled && !string.IsNullOrEmpty(c.TableName) && c.Series.Any(s => !string.IsNullOrEmpty(s.ColumnName)))
                    .ToList();

                var tasks = validConfigs.Select(config => ProcessChartConfigurationAsync(config, colorMap, token)).ToList();
                await Task.WhenAll(tasks);
            }
            catch (Exception ex) { _logger?.LogError($"Error loading charts: {ex.Message}"); }
        }

        private async Task ProcessChartConfigurationAsync(DashboardConfiguration config, Dictionary<(int, string), string> colorMap, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;
            try
            {
                await Task.Run(async () =>
                {
                    var cols = config.Series.Select(ser => ser.ColumnName).ToList();
                    if (!string.IsNullOrEmpty(config.DateColumn)) cols.Add(config.DateColumn);

                    // FIX: Ensure SQL Safe Dates (Min 1753) to prevent DB crash
                    DateTime safeStart = StartDate < new DateTime(1753, 1, 1) ? new DateTime(1753, 1, 1) : StartDate;
                    DateTime safeEnd = EndDate < safeStart ? safeStart : EndDate;

                    DateTime? filterStart = (IsFilterByDate && config.DataStructureType == "Daily Date") ? (DateTime?)safeStart : null;
                    DateTime? filterEnd = (IsFilterByDate && config.DataStructureType == "Daily Date") ? (DateTime?)safeEnd : null;

                    var dt = await _dataRepository.GetDataAsync(config.TableName, cols.Distinct().ToList(), config.DateColumn, filterStart, filterEnd);
                    if (token.IsCancellationRequested) return;

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (!token.IsCancellationRequested) UpdateChartData(dt, config, colorMap);
                    });
                }, token);
            }
            catch { }
        }

        private void UpdateChartData(DataTable dataTable, DashboardConfiguration config, Dictionary<(int, string), string> colorMap = null)
        {
            if (dataTable == null) return;

            // 1. Row Filter
            if (config.RowsToIgnore > 0 && dataTable.Rows.Count > 0)
            {
                int rowsToRemove = Math.Min(config.RowsToIgnore, dataTable.Rows.Count);
                for (int i = 0; i < rowsToRemove; i++) dataTable.Rows.RemoveAt(dataTable.Rows.Count - 1);
            }

            // 2. Null Date Filter
            if (IgnoreNonDateData && !string.IsNullOrEmpty(config.DateColumn) && dataTable.Columns.Contains(config.DateColumn))
            {
                for (int i = dataTable.Rows.Count - 1; i >= 0; i--)
                {
                    if (dataTable.Rows[i][config.DateColumn] == DBNull.Value) dataTable.Rows.RemoveAt(i);
                }
            }

            // 3. Slider Slice
            bool isDateBasedChart = config.DataStructureType == "Daily Date" && !string.IsNullOrEmpty(config.DateColumn);
            if (!isDateBasedChart || !IsFilterByDate)
            {
                int totalRows = dataTable.Rows.Count;
                if (totalRows > 0 && SliderMaximum > 0)
                {
                    int startIndex = (int)(totalRows * (StartMonthSliderValue / SliderMaximum));
                    int endIndex = (int)(totalRows * (EndMonthSliderValue / SliderMaximum));
                    if (startIndex < 0) startIndex = 0;
                    if (endIndex >= totalRows) endIndex = totalRows - 1;
                    if (startIndex > endIndex) startIndex = endIndex;

                    var rowsToKeep = new List<DataRow>();
                    for (int i = startIndex; i <= endIndex; i++) rowsToKeep.Add(dataTable.Rows[i]);

                    DataTable filteredTable = dataTable.Clone();
                    foreach (var row in rowsToKeep) filteredTable.ImportRow(row);
                    dataTable = filteredTable;
                }
            }

            // FIX: Safe Double Converter for Offline Mode (prevents casting crashes)
            Func<object, double> safeConvertToDouble = (obj) =>
            {
                if (obj == null || obj == DBNull.Value) return 0.0;
                var culture = config.UseInvariantCultureForNumbers ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture;
                try { return Convert.ToDouble(obj, culture); } catch { return 0.0; }
            };

            var newSeries = new SeriesCollection();
            var newXAxis = new AxesCollection();
            var newYAxis = new AxesCollection();
            var axisColor = Brushes.WhiteSmoke;

            // FIX: Thread-Safe Color Generation
            Brush GetColor(string title)
            {
                if (colorMap != null && colorMap.TryGetValue((config.ChartPosition, title), out string hex))
                    return (Brush)new BrushConverter().ConvertFrom(hex);

                lock (_randomLock)
                {
                    return new SolidColorBrush(Color.FromRgb((byte)_random.Next(100, 256), (byte)_random.Next(100, 256), (byte)_random.Next(100, 256)));
                }
            }

            if (string.Equals(config.ChartType, "Pie", StringComparison.OrdinalIgnoreCase))
            {
                newXAxis = null; newYAxis = null;
                foreach (var ser in config.Series.Where(s => !string.IsNullOrEmpty(s.ColumnName) && dataTable.Columns.Contains(s.ColumnName)))
                {
                    double total = dataTable.AsEnumerable().Sum(row => safeConvertToDouble(row[ser.ColumnName]));
                    newSeries.Add(new PieSeries { Title = ser.ColumnName, Values = new ChartValues<double> { total }, DataLabels = true, LabelPoint = p => string.Format("{0:P0}", p.Participation), Fill = GetColor(ser.ColumnName) });
                }
            }
            else
            {
                newYAxis.Add(new Axis { Title = "Values", MinValue = 0, Foreground = axisColor });
                if (config.DataStructureType == "Daily Date")
                {
                    if (dataTable.Columns.Contains(config.DateColumn))
                    {
                        var points = dataTable.AsEnumerable().Select(r => new { Date = (DateTime)r[config.DateColumn], Row = r }).OrderBy(p => p.Date).ToList();
                        if (config.AggregationType != "Daily")
                        {
                            IEnumerable<IGrouping<string, dynamic>> groups;
                            if (config.AggregationType == "Weekly") groups = points.GroupBy(p => p.Date.AddDays(-1 * ((7 + (p.Date.DayOfWeek - DayOfWeek.Monday)) % 7)).Date.ToString("d"));
                            else groups = points.GroupBy(p => new DateTime(p.Date.Year, p.Date.Month, 1).ToString("MMM yyyy"));

                            var labels = groups.Select(g => g.Key).ToList();
                            foreach (var ser in config.Series.Where(s => !string.IsNullOrEmpty(s.ColumnName) && dataTable.Columns.Contains(s.ColumnName)))
                            {
                                var vals = new ChartValues<double>(groups.Select(g => g.Sum(x => (double)safeConvertToDouble(x.Row[ser.ColumnName])))); if (config.ChartType == "Line") newSeries.Add(new LineSeries { Title = ser.ColumnName, Values = vals, PointGeometry = DefaultGeometries.Circle, Stroke = GetColor(ser.ColumnName) });
                                else newSeries.Add(new ColumnSeries { Title = ser.ColumnName, Values = vals, Fill = GetColor(ser.ColumnName) });
                            }
                            newXAxis.Add(new Axis { Labels = labels, Separator = new Separator { IsEnabled = false }, Foreground = axisColor });
                        }
                        else
                        {
                            if (config.ChartType == "Line")
                            {
                                foreach (var ser in config.Series.Where(s => !string.IsNullOrEmpty(s.ColumnName) && dataTable.Columns.Contains(s.ColumnName)))
                                {
                                    var vals = new ChartValues<DateTimePoint>(points.Select(p => new DateTimePoint(p.Date, safeConvertToDouble(p.Row[ser.ColumnName]))));
                                    newSeries.Add(new LineSeries { Title = ser.ColumnName, Values = vals, PointGeometry = DefaultGeometries.None, Stroke = GetColor(ser.ColumnName) });
                                }
                                newXAxis.Add(new Axis { LabelFormatter = DateFormatter, Separator = new Separator { IsEnabled = false }, Foreground = axisColor });
                            }
                            else
                            {
                                var labels = points.Select(p => p.Date.ToString("d")).ToArray();
                                foreach (var ser in config.Series.Where(s => !string.IsNullOrEmpty(s.ColumnName) && dataTable.Columns.Contains(s.ColumnName)))
                                {
                                    var vals = new ChartValues<double>(points.Select(p => safeConvertToDouble(p.Row[ser.ColumnName])));
                                    newSeries.Add(new ColumnSeries { Title = ser.ColumnName, Values = vals, Fill = GetColor(ser.ColumnName) });
                                }
                                newXAxis.Add(new Axis { Labels = labels, Separator = new Separator { IsEnabled = false }, Foreground = axisColor });
                            }
                        }
                    }
                }
                else
                {
                    if (dataTable.Columns.Contains(config.DateColumn))
                    {
                        var labels = dataTable.AsEnumerable().Select(r => r[config.DateColumn]?.ToString()).ToArray();
                        foreach (var ser in config.Series.Where(s => !string.IsNullOrEmpty(s.ColumnName) && dataTable.Columns.Contains(s.ColumnName)))
                        {
                            var vals = new ChartValues<double>(dataTable.AsEnumerable().Select(r => safeConvertToDouble(r[ser.ColumnName])));
                            if (config.ChartType == "Line") newSeries.Add(new LineSeries { Title = ser.ColumnName, Values = vals, Stroke = GetColor(ser.ColumnName) });
                            else newSeries.Add(new ColumnSeries { Title = ser.ColumnName, Values = vals, Fill = GetColor(ser.ColumnName) });
                        }
                        newXAxis.Add(new Axis { Labels = labels, Separator = new Separator { IsEnabled = false }, Foreground = axisColor });
                    }
                }
            }

            switch (config.ChartPosition)
            {
                case 1: Chart1Series.AddRange(newSeries); Chart1X.Clear(); if (newXAxis != null) Chart1X.AddRange(newXAxis); Chart1Y.Clear(); if (newYAxis != null) Chart1Y.AddRange(newYAxis); break;
                case 2: Chart2Series.AddRange(newSeries); Chart2X.Clear(); if (newXAxis != null) Chart2X.AddRange(newXAxis); Chart2Y.Clear(); if (newYAxis != null) Chart2Y.AddRange(newYAxis); break;
                case 3: Chart3Series.AddRange(newSeries); Chart3X.Clear(); if (newXAxis != null) Chart3X.AddRange(newXAxis); Chart3Y.Clear(); if (newYAxis != null) Chart3Y.AddRange(newYAxis); break;
                case 4: Chart4Series.AddRange(newSeries); break;
                case 5: Chart5Series.AddRange(newSeries); Chart5X.Clear(); if (newXAxis != null) Chart5X.AddRange(newXAxis); Chart5Y.Clear(); if (newYAxis != null) Chart5Y.AddRange(newYAxis); break;
            }
        }

        private void InitializeEmptyAxes()
        {
            var hidden = new Axis { ShowLabels = false, Separator = new Separator { IsEnabled = false }, Foreground = Brushes.Transparent, Visibility = Visibility.Collapsed };
            Chart1X.Add(hidden); Chart1Y.Add(hidden); Chart2X.Add(hidden); Chart2Y.Add(hidden);
            Chart3X.Add(hidden); Chart3Y.Add(hidden); Chart5X.Add(hidden); Chart5Y.Add(hidden);
        }

        private void ClearAndReinitializeAxes()
        {
            Chart1X.Clear(); Chart1Y.Clear(); Chart2X.Clear(); Chart2Y.Clear();
            Chart3X.Clear(); Chart3Y.Clear(); Chart5X.Clear(); Chart5Y.Clear();
            InitializeEmptyAxes();
        }

        private async void ShowConfigurationWindow()
        {
            var vm = new ConfigurationViewModel(_dataRepository);
            await vm.InitializeAsync();
            vm.LoadConfigurations(_dashboardConfigurations);
            if (_dialogService.ShowConfigurationDialog(vm)) { _dashboardConfigurations = vm.GetFinalConfigurations(); Activate(); AutoSave(); }
        }

        public async void Activate()
        {
            if (_isActive) return;
            _isActive = true;
            OnPropertyChanged(nameof(IsDateFilterVisible));
            if (Settings.Default.AutoImportEnabled) RefreshDashboardFiles();
            await InitializeDashboardAsync();
        }

        public void Deactivate()
        {
            _isActive = false;
            if (_cts != null) { _cts.Cancel(); _cts.Dispose(); _cts = null; }
            AutoSave();
        }

        private async Task InitializeDashboardAsync()
        {
            IsDateFilterEnabled = _dashboardConfigurations.Any(c => c.IsEnabled && c.DataStructureType == "Daily Date");
            await FindGlobalDateRangeAsync();
            InitializeSliders();
            LoadAllChartsData();
        }

        private async Task FindGlobalDateRangeAsync()
        {
            DateTime min = DateTime.MaxValue, max = DateTime.MinValue;
            var configs = _dashboardConfigurations.Where(c => c.IsEnabled && c.DataStructureType == "Daily Date" && !string.IsNullOrEmpty(c.TableName) && !string.IsNullOrEmpty(c.DateColumn)).ToList();

            if (!configs.Any()) { _minSliderDate = DateTime.Now.AddYears(-1); _maxSliderDate = DateTime.Now; return; }

            foreach (var c in configs)
            {
                try { var range = await _dataRepository.GetDateRangeAsync(c.TableName, c.DateColumn); if (range.Min != DateTime.MinValue && range.Min < min) min = range.Min; if (range.Max != DateTime.MinValue && range.Max > max) max = range.Max; } catch { }
            }
            if (min == DateTime.MaxValue) min = DateTime.Now.AddYears(-1); if (max == DateTime.MinValue) max = DateTime.Now;
            _minSliderDate = min; _maxSliderDate = new DateTime(max.Year, max.Month, DateTime.DaysInMonth(max.Year, max.Month));
        }

        private void InitializeSliders()
        {
            if (!IsFilterByDate) { SliderMaximum = 100; StartMonthSliderValue = 0; EndMonthSliderValue = 100; UpdateTooltips(); return; }
            if (_minSliderDate == default) return;

            if (_startDate == DateTime.MinValue || _endDate == DateTime.MinValue) { _startDate = _minSliderDate; _endDate = _maxSliderDate; }
            else { if (_startDate < _minSliderDate) _startDate = _minSliderDate; if (_endDate > _maxSliderDate) _endDate = _maxSliderDate; if (_startDate > _endDate) { _startDate = _minSliderDate; _endDate = _maxSliderDate; } }

            Application.Current.Dispatcher.Invoke(() => { OnPropertyChanged(nameof(StartDate)); OnPropertyChanged(nameof(EndDate)); });
            SliderMaximum = ((_maxSliderDate.Year - _minSliderDate.Year) * 12) + _maxSliderDate.Month - _minSliderDate.Month;
            if (SliderMaximum < 0) SliderMaximum = 0;
            UpdateSlidersFromDates();
        }

        private void UpdateSlidersFromDates()
        {
            if (_isUpdatingDates || _minSliderDate == default || !IsFilterByDate) return;
            _isUpdatingDates = true;
            StartMonthSliderValue = ((StartDate.Year - _minSliderDate.Year) * 12) + StartDate.Month - _minSliderDate.Month;
            EndMonthSliderValue = ((EndDate.Year - _minSliderDate.Year) * 12) + EndDate.Month - _minSliderDate.Month;
            if (StartMonthSliderValue < 0) StartMonthSliderValue = 0; if (EndMonthSliderValue > SliderMaximum) EndMonthSliderValue = SliderMaximum;
            UpdateTooltips(); _isUpdatingDates = false;
        }

        private void UpdateDatesFromSliders()
        {
            if (_isUpdatingDates || _minSliderDate == default || !IsFilterByDate) return;
            if (StartMonthSliderValue > EndMonthSliderValue) { double tmp = StartMonthSliderValue; StartMonthSliderValue = EndMonthSliderValue; EndMonthSliderValue = tmp; }
            _isUpdatingDates = true;
            var s = _minSliderDate.AddMonths((int)StartMonthSliderValue);
            var e = _minSliderDate.AddMonths((int)EndMonthSliderValue);
            _startDate = new DateTime(s.Year, s.Month, 1); _endDate = new DateTime(e.Year, e.Month, DateTime.DaysInMonth(e.Year, e.Month));
            OnPropertyChanged(nameof(StartDate)); OnPropertyChanged(nameof(EndDate)); UpdateTooltips();
            _isUpdatingDates = false; LoadAllChartsData(); AutoSave();
        }

        private void UpdateTooltips()
        {
            if (IsFilterByDate && _minSliderDate != default) { StartSliderTooltip = _minSliderDate.AddMonths((int)_startMonthSliderValue).ToString("MMM yyyy"); EndSliderTooltip = _minSliderDate.AddMonths((int)_endMonthSliderValue).ToString("MMM yyyy"); }
            else { StartSliderTooltip = $"{StartMonthSliderValue:F0}%"; EndSliderTooltip = $"{EndMonthSliderValue:F0}%"; }
        }

        private void RefreshDashboardFiles()
        {
            DashboardFiles.Clear();
            string path = Settings.Default.ImportIsRelative ? AppDomain.CurrentDomain.BaseDirectory : Settings.Default.ImportAbsolutePath;
            if (Directory.Exists(path)) { try { foreach (var f in Directory.GetFiles(path, "*.json")) DashboardFiles.Add(Path.GetFileName(f)); } catch { } }
            OnPropertyChanged(nameof(IsDashboardSelectorVisible));
        }

        private void LoadSelectedDashboardFile(string f)
        { try { LoadSnapshotFromFile(Path.Combine(Settings.Default.ImportIsRelative ? AppDomain.CurrentDomain.BaseDirectory : Settings.Default.ImportAbsolutePath, f)); } catch { } }

        private void LoadSnapshotFromFile(string p)
        { var s = _storageService.LoadSnapshot(p); if (s != null) LoadDashboardFromSnapshot(s); }

        private void AutoSave() => _storageService.SaveSnapshot(new DashboardSnapshot { StartDate = StartDate, EndDate = EndDate, Configurations = _dashboardConfigurations, IsFilterByDate = IsFilterByDate, IgnoreNonDateData = IgnoreNonDateData, UseIdToDateConversion = UseIdToDateConversion, InitialDateForConversion = InitialDateForConversion });

        private void TryLoadAutoSave()
        { try { if (Settings.Default.AutoImportEnabled) { RefreshDashboardFiles(); string def = Settings.Default.ImportFileName; string path = Settings.Default.ImportIsRelative ? AppDomain.CurrentDomain.BaseDirectory : Settings.Default.ImportAbsolutePath; if (!string.IsNullOrEmpty(def) && File.Exists(Path.Combine(path, def))) { LoadSnapshotFromFile(Path.Combine(path, def)); if (DashboardFiles.Contains(def)) { _selectedDashboardFile = def; OnPropertyChanged(nameof(SelectedDashboardFile)); } return; } } var s = _storageService.LoadSnapshot(); if (s != null) LoadDashboardFromSnapshot(s); } catch { } }

        private void ImportConfiguration()
        { var d = new OpenFileDialog { Filter = "JSON|*.json" }; if (d.ShowDialog() == true) LoadSnapshotFromFile(d.FileName); }

        private void ExportConfiguration()
        { if (_dashboardConfigurations == null) return; var s = new DashboardSnapshot { StartDate = StartDate, EndDate = EndDate, Configurations = _dashboardConfigurations }; var d = new SaveFileDialog { Filter = "JSON|*.json" }; if (d.ShowDialog() == true) File.WriteAllText(d.FileName, JsonConvert.SerializeObject(s, Formatting.Indented)); }

        private void LoadDashboardFromSnapshot(DashboardSnapshot s)
        {
            if (s == null) return;
            bool wasActive = _isActive; _isActive = false;
            try
            {
                _dashboardConfigurations = s.Configurations;
                _startDate = DateTime.MinValue; _endDate = DateTime.MinValue;
                ClearAndReinitializeAxes(); Chart1Series.Clear(); Chart2Series.Clear(); Chart3Series.Clear(); Chart4Series.Clear(); Chart5Series.Clear();
                if (wasActive) { _isActive = true; _ = InitializeDashboardAsync(); }
            }
            finally { if (wasActive && !_isActive) _isActive = true; }
        }

        private void ExecuteChartClick(object parameter)
        {
            if (parameter is ChartPoint point && point.SeriesView is Series series)
            {
                int pos = 0;
                if (Chart1Series.Contains(series)) pos = 1; else if (Chart2Series.Contains(series)) pos = 2; else if (Chart3Series.Contains(series)) pos = 3; else if (Chart4Series.Contains(series)) pos = 4; else if (Chart5Series.Contains(series)) pos = 5;
                if (pos == 0) return;
                var config = _dashboardConfigurations.FirstOrDefault(c => c.ChartPosition == pos);
                if (config != null) DrillDownRequested?.Invoke(config.TableName, StartDate, EndDate);
            }
        }
    }
}