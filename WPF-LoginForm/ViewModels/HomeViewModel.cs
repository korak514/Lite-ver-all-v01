// ViewModels/HomeViewModel.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using Microsoft.Win32;
using Newtonsoft.Json;
using WPF_LoginForm.Models;
using WPF_LoginForm.Properties;
using WPF_LoginForm.Repositories;
using WPF_LoginForm.Services;

namespace WPF_LoginForm.ViewModels
{
    public class KpiModel : ViewModelBase
    {
        public string Title { get; set; }
        public string Value { get; set; }
        public string ColorHex { get; set; }
    }

    public class HomeViewModel : ViewModelBase
    {
        private readonly IDataRepository _dataRepository;
        private readonly IDialogService _dialogService;
        private readonly ILogger _logger;
        private readonly DashboardStorageService _storageService;
        private readonly DashboardChartService _chartService;

        private List<DashboardConfiguration> _dashboardConfigurations;
        private bool _isUpdatingDates = false;
        private DateTime _minSliderDate;
        private DateTime _maxSliderDate;
        private bool _isActive = false;

        private CancellationTokenSource _cts;
        private ConcurrentDictionary<string, double> _kpiTotals;

        // --- NEW: Maximize Button Switch ---
        private bool _showMaximizeButtons;

        public bool ShowMaximizeButtons
        {
            get => _showMaximizeButtons;
            set => SetProperty(ref _showMaximizeButtons, value);
        }

        // -----------------------------------

        private int _maximizedChartIndex = 0;

        public int MaximizedChartIndex
        {
            get => _maximizedChartIndex;
            set
            {
                if (SetProperty(ref _maximizedChartIndex, value))
                {
                    OnPropertyChanged(nameof(IsAnyChartMaximized));
                }
            }
        }

        public bool IsAnyChartMaximized => MaximizedChartIndex > 0;

        public ICommand MaximizeChartCommand { get; }
        public ICommand CloseMaximizeCommand { get; }

        private bool _isSecondPageActive;

        public bool IsSecondPageActive
        {
            get => _isSecondPageActive;
            set => SetProperty(ref _isSecondPageActive, value);
        }

        public ObservableCollection<string> ErrorCategories { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> DashboardFiles { get; } = new ObservableCollection<string>();
        public ObservableCollection<KpiModel> KpiCards { get; } = new ObservableCollection<KpiModel>();

        private string _selectedErrorCategory;

        public string SelectedErrorCategory
        {
            get => _selectedErrorCategory;
            set
            {
                if (SetProperty(ref _selectedErrorCategory, value))
                {
                    InitializeSliders();
                    LoadAllChartsData();
                    AutoSave();
                }
            }
        }

        private string _selectedDashboardFile;

        public string SelectedDashboardFile
        {
            get => _selectedDashboardFile;
            set
            {
                if (SetProperty(ref _selectedDashboardFile, value) && _isActive && !string.IsNullOrEmpty(value))
                {
                    Task.Delay(50).ContinueWith(_ => Application.Current.Dispatcher.Invoke(() => LoadSelectedDashboardFile(value)));
                }
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
        public ICommand TogglePageCommand { get; }

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
        {
            get => Settings.Default.DashboardDateTickSize;
            set { if (Settings.Default.DashboardDateTickSize != value) { Settings.Default.DashboardDateTickSize = value; Settings.Default.Save(); OnPropertyChanged(); } }
        }

        public class TickOption
        { public string Label { get; set; } public int Value { get; set; } }

        public List<TickOption> TickOptions { get; } = new List<TickOption>
        {
            new TickOption { Label = "1 Month", Value = 1 }, new TickOption { Label = "3 Months", Value = 3 },
            new TickOption { Label = "6 Months", Value = 6 }, new TickOption { Label = "1 Year", Value = 12 }
        };

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
        public SeriesCollection Chart6Series { get; } = new SeriesCollection();

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
            _chartService = new DashboardChartService();

            _dashboardConfigurations = new List<DashboardConfiguration>();
            _kpiTotals = new ConcurrentDictionary<string, double>();

            ConfigureCommand = new ViewModelCommand(p => ShowConfigurationWindow());
            ImportCommand = new ViewModelCommand(p => ImportConfiguration());
            ExportCommand = new ViewModelCommand(p => ExportConfiguration());
            RecolorChartsCommand = new ViewModelCommand(p => LoadAllChartsData());
            ToggleFilterModeCommand = new ViewModelCommand(p => IsFilterByDate = !IsFilterByDate);
            ChartClickCommand = new ViewModelCommand(ExecuteChartClick);
            TogglePageCommand = new ViewModelCommand(p => IsSecondPageActive = !IsSecondPageActive);

            MaximizeChartCommand = new ViewModelCommand(p => { if (int.TryParse(p?.ToString(), out int i)) MaximizedChartIndex = i; });
            CloseMaximizeCommand = new ViewModelCommand(p => MaximizedChartIndex = 0);

            _startDate = DateTime.Today.AddYears(-1);
            _endDate = DateTime.Today;

            DateFormatter = value => (value < DateTime.MinValue.Ticks || value > DateTime.MaxValue.Ticks) ? "" : new DateTime((long)value).ToString("d");

            ClearAndReinitializeAxes();
            TryLoadAutoSave();
        }

        private async void LoadAllChartsData(Dictionary<(int, string), string> colorMap = null)
        {
            if (_cts != null) { _cts.Cancel(); _cts.Dispose(); }
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            if (_dashboardConfigurations == null || !_isActive) return;

            if (!_dashboardConfigurations.Any())
            {
                for (int i = 1; i <= 6; i++) _dashboardConfigurations.Add(new DashboardConfiguration { ChartPosition = i, IsEnabled = false });
            }

            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Chart1Series.Clear(); Chart2Series.Clear(); Chart3Series.Clear();
                    Chart4Series.Clear(); Chart5Series.Clear(); Chart6Series.Clear();
                    ClearAndReinitializeAxes();
                    _kpiTotals.Clear();
                });

                var validConfigs = _dashboardConfigurations.Where(c => c.IsEnabled && !string.IsNullOrEmpty(c.TableName) && c.Series.Any(s => !string.IsNullOrEmpty(s.ColumnName))).ToList();
                var tasks = validConfigs.Select(config => ProcessChartConfigurationAsync(config, colorMap, token)).ToList();
                await Task.WhenAll(tasks);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    KpiCards.Clear();
                    var colors = new[] { "#3498DB", "#2ECC71", "#9B59B6", "#F1C40F", "#E67E22", "#E74C3C" };
                    int cIdx = 0;
                    foreach (var kvp in _kpiTotals.OrderByDescending(x => x.Value))
                    {
                        KpiCards.Add(new KpiModel { Title = $"Total {kvp.Key}", Value = kvp.Value.ToString("N0"), ColorHex = colors[cIdx % colors.Length] });
                        cIdx++;
                    }
                });
            }
            catch (Exception ex) { _logger?.LogError($"Error loading charts: {ex.Message}"); }
        }

        private async Task ProcessChartConfigurationAsync(DashboardConfiguration config, Dictionary<(int, string), string> colorMap, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;
            try
            {
                var cols = config.Series.Select(ser => ser.ColumnName).ToList();
                if (!string.IsNullOrEmpty(config.DateColumn)) cols.Add(config.DateColumn);

                DateTime safeStart = StartDate < new DateTime(1753, 1, 1) ? new DateTime(1753, 1, 1) : StartDate;
                DateTime safeEnd = EndDate < safeStart ? safeStart : EndDate;

                DateTime? filterStart = (IsFilterByDate && config.DataStructureType == "Daily Date") ? (DateTime?)safeStart : null;
                DateTime? filterEnd = (IsFilterByDate && config.DataStructureType == "Daily Date") ? (DateTime?)safeEnd : null;

                var dt = await _dataRepository.GetDataAsync(config.TableName, cols.Distinct().ToList(), config.DateColumn, filterStart, filterEnd);
                if (token.IsCancellationRequested || dt == null) return;

                foreach (var seriesConfig in config.Series)
                {
                    if (dt.Columns.Contains(seriesConfig.ColumnName))
                    {
                        var culture = config.UseInvariantCultureForNumbers ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture;
                        double total = dt.AsEnumerable().Sum(r =>
                        {
                            if (r[seriesConfig.ColumnName] == DBNull.Value) return 0.0;
                            string strVal = r[seriesConfig.ColumnName].ToString().Replace("₺", "").Replace("TL", "").Replace("%", "").Trim();
                            double.TryParse(strVal, NumberStyles.Any, culture, out double res);
                            return res;
                        });
                        _kpiTotals.AddOrUpdate(seriesConfig.ColumnName, total, (key, old) => total);
                    }
                }

                var chartResult = await Task.Run(() => _chartService.ProcessChartData(dt, config, IsFilterByDate, IgnoreNonDateData, StartMonthSliderValue, EndMonthSliderValue, SliderMaximum, colorMap), token);
                if (token.IsCancellationRequested) return;

                Application.Current.Dispatcher.Invoke(() => { if (!token.IsCancellationRequested) ApplyChartResultToUI(config.ChartPosition, chartResult); });
            }
            catch (Exception ex) { _logger?.LogError($"Error processing chart position {config.ChartPosition}", ex); }
        }

        private void ApplyChartResultToUI(int position, DashboardChartService.ChartResultDto result)
        {
            var targetSeries = GetSeriesCollection(position);
            var targetX = GetXAxes(position);
            var targetY = GetYAxes(position);

            if (targetSeries == null) return;

            var newSeries = new SeriesCollection();
            var axisColor = Brushes.WhiteSmoke;

            foreach (var s in result.Series)
            {
                Brush colorBrush = (Brush)new BrushConverter().ConvertFrom(s.ColorHex);

                if (s.SeriesType == "Pie")
                {
                    newSeries.Add(new PieSeries { Title = s.Title, Values = new ChartValues<double>(s.Values.Cast<double>()), DataLabels = true, LabelPoint = p => string.Format("{0:P0}", p.Participation), Fill = colorBrush });
                }
                else if (s.SeriesType == "Line")
                {
                    if (result.IsDateAxis)
                    {
                        var cv = new ChartValues<DateTimePoint>(); cv.AddRange(s.Values.Cast<DateTimePoint>());
                        newSeries.Add(new LineSeries { Title = s.Title, Values = cv, PointGeometry = DefaultGeometries.None, Stroke = colorBrush, Fill = Brushes.Transparent });
                    }
                    else
                    {
                        var cv = new ChartValues<double>(); cv.AddRange(s.Values.Cast<double>());
                        newSeries.Add(new LineSeries { Title = s.Title, Values = cv, PointGeometry = DefaultGeometries.Circle, Stroke = colorBrush, Fill = Brushes.Transparent });
                    }
                }
                else
                {
                    var cv = new ChartValues<double>(); cv.AddRange(s.Values.Cast<double>());
                    newSeries.Add(new ColumnSeries { Title = s.Title, Values = cv, Fill = colorBrush });
                }
            }

            targetSeries.AddRange(newSeries);

            if (targetX != null && targetY != null && result.Series.Any(x => x.SeriesType != "Pie"))
            {
                targetY.Add(new Axis { Title = "Values", Foreground = axisColor });

                if (result.IsDateAxis) targetX.Add(new Axis { LabelFormatter = DateFormatter, Separator = new Separator { IsEnabled = false }, Foreground = axisColor });
                else targetX.Add(new Axis { Labels = result.XAxisLabels, Separator = new Separator { IsEnabled = false }, Foreground = axisColor });
            }
        }

        private SeriesCollection GetSeriesCollection(int pos)
        {
            switch (pos) { case 1: return Chart1Series; case 2: return Chart2Series; case 3: return Chart3Series; case 4: return Chart4Series; case 5: return Chart5Series; case 6: return Chart6Series; default: return null; }
        }

        private AxesCollection GetXAxes(int pos)
        {
            switch (pos) { case 1: return Chart1X; case 2: return Chart2X; case 3: return Chart3X; case 5: return Chart5X; default: return null; }
        }

        private AxesCollection GetYAxes(int pos)
        {
            switch (pos) { case 1: return Chart1Y; case 2: return Chart2Y; case 3: return Chart3Y; case 5: return Chart5Y; default: return null; }
        }

        private void ClearAndReinitializeAxes()
        {
            Chart1X.Clear(); Chart1Y.Clear(); Chart2X.Clear(); Chart2Y.Clear(); Chart3X.Clear(); Chart3Y.Clear(); Chart5X.Clear(); Chart5Y.Clear();
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
                try
                {
                    var range = await _dataRepository.GetDateRangeAsync(c.TableName, c.DateColumn);
                    if (range.Min != DateTime.MinValue && range.Min < min) min = range.Min;
                    if (range.Max != DateTime.MinValue && range.Max > max) max = range.Max;
                }
                catch { }
            }
            if (min == DateTime.MaxValue) min = DateTime.Now.AddYears(-1);
            if (max == DateTime.MinValue) max = DateTime.Now;

            _minSliderDate = min;
            _maxSliderDate = new DateTime(max.Year, max.Month, DateTime.DaysInMonth(max.Year, max.Month));
        }

        private void InitializeSliders()
        {
            if (!IsFilterByDate) { SliderMaximum = 100; StartMonthSliderValue = 0; EndMonthSliderValue = 100; UpdateTooltips(); return; }
            if (_minSliderDate == default) return;
            if (_startDate < _minSliderDate || _startDate == DateTime.MinValue) _startDate = _minSliderDate;
            if (_endDate > _maxSliderDate || _endDate == DateTime.MinValue) _endDate = _maxSliderDate;
            if (_startDate > _endDate) { _startDate = _minSliderDate; _endDate = _maxSliderDate; }
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
            if (StartMonthSliderValue < 0) StartMonthSliderValue = 0;
            if (EndMonthSliderValue > SliderMaximum) EndMonthSliderValue = SliderMaximum;
            UpdateTooltips();
            _isUpdatingDates = false;
        }

        private void UpdateDatesFromSliders()
        {
            if (_isUpdatingDates || _minSliderDate == default || !IsFilterByDate) return;
            if (StartMonthSliderValue > EndMonthSliderValue) { double tmp = StartMonthSliderValue; StartMonthSliderValue = EndMonthSliderValue; EndMonthSliderValue = tmp; }
            _isUpdatingDates = true;
            var s = _minSliderDate.AddMonths((int)StartMonthSliderValue); var e = _minSliderDate.AddMonths((int)EndMonthSliderValue);
            _startDate = new DateTime(s.Year, s.Month, 1); _endDate = new DateTime(e.Year, e.Month, DateTime.DaysInMonth(e.Year, e.Month));
            OnPropertyChanged(nameof(StartDate)); OnPropertyChanged(nameof(EndDate)); UpdateTooltips(); _isUpdatingDates = false;
            LoadAllChartsData(); AutoSave();
        }

        private void UpdateTooltips()
        {
            if (IsFilterByDate && _minSliderDate != default) { StartSliderTooltip = _minSliderDate.AddMonths((int)_startMonthSliderValue).ToString("MMM yyyy"); EndSliderTooltip = _minSliderDate.AddMonths((int)_endMonthSliderValue).ToString("MMM yyyy"); }
            else { StartSliderTooltip = $"{StartMonthSliderValue:F0}%"; EndSliderTooltip = $"{EndMonthSliderValue:F0}%"; }
        }

        private void RefreshDashboardFiles()
        { DashboardFiles.Clear(); string path = Settings.Default.ImportIsRelative ? AppDomain.CurrentDomain.BaseDirectory : Settings.Default.ImportAbsolutePath; if (Directory.Exists(path)) { try { foreach (var f in Directory.GetFiles(path, "*.json")) DashboardFiles.Add(Path.GetFileName(f)); } catch { } } OnPropertyChanged(nameof(IsDashboardSelectorVisible)); }

        private void LoadSelectedDashboardFile(string f)
        { try { LoadSnapshotFromFile(Path.Combine(Settings.Default.ImportIsRelative ? AppDomain.CurrentDomain.BaseDirectory : Settings.Default.ImportAbsolutePath, f)); } catch { } }

        private void LoadSnapshotFromFile(string p)
        { var s = _storageService.LoadSnapshot(p); if (s != null) LoadDashboardFromSnapshot(s); }

        private void AutoSave()
        {
            var snap = new DashboardSnapshot { StartDate = StartDate, EndDate = EndDate, Configurations = _dashboardConfigurations, IsFilterByDate = IsFilterByDate, IgnoreNonDateData = IgnoreNonDateData, UseIdToDateConversion = UseIdToDateConversion, InitialDateForConversion = InitialDateForConversion, SeriesData = new List<ChartSeriesSnapshot>() };
            _storageService.SaveSnapshot(snap);
        }

        private void TryLoadAutoSave()
        { try { if (Settings.Default.AutoImportEnabled) { RefreshDashboardFiles(); string def = Settings.Default.ImportFileName; string path = Settings.Default.ImportIsRelative ? AppDomain.CurrentDomain.BaseDirectory : Settings.Default.ImportAbsolutePath; if (!string.IsNullOrEmpty(def) && File.Exists(Path.Combine(path, def))) { LoadSnapshotFromFile(Path.Combine(path, def)); if (DashboardFiles.Contains(def)) { _selectedDashboardFile = def; OnPropertyChanged(nameof(SelectedDashboardFile)); } return; } } var s = _storageService.LoadSnapshot(); if (s != null) LoadDashboardFromSnapshot(s); } catch { } }

        private void ImportConfiguration()
        { var d = new OpenFileDialog { Filter = "JSON|*.json" }; if (d.ShowDialog() == true) LoadSnapshotFromFile(d.FileName); }

        private void ExportConfiguration()
        { if (_dashboardConfigurations == null) return; var s = new DashboardSnapshot { StartDate = StartDate, EndDate = EndDate, Configurations = _dashboardConfigurations }; var d = new SaveFileDialog { Filter = "JSON|*.json" }; if (d.ShowDialog() == true) File.WriteAllText(d.FileName, JsonConvert.SerializeObject(s, Formatting.Indented)); }

        private void LoadDashboardFromSnapshot(DashboardSnapshot s)
        {
            if (s == null) return; bool wasActive = _isActive; _isActive = false;
            try
            {
                _dashboardConfigurations = s.Configurations; IsFilterByDate = s.IsFilterByDate; IgnoreNonDateData = s.IgnoreNonDateData; UseIdToDateConversion = s.UseIdToDateConversion;
                if (s.InitialDateForConversion != default) InitialDateForConversion = s.InitialDateForConversion;
                if (s.StartDate != default && s.EndDate != default) { _startDate = s.StartDate; _endDate = s.EndDate; OnPropertyChanged(nameof(StartDate)); OnPropertyChanged(nameof(EndDate)); }
                Application.Current.Dispatcher.Invoke(() => { ClearAndReinitializeAxes(); Chart1Series.Clear(); Chart2Series.Clear(); Chart3Series.Clear(); Chart4Series.Clear(); Chart5Series.Clear(); Chart6Series.Clear(); });
                if (wasActive) { _isActive = true; _ = InitializeDashboardAsync(); }
            }
            finally { if (wasActive && !_isActive) _isActive = true; }
        }

        private void ExecuteChartClick(object parameter)
        {
            if (parameter is ChartPoint point && point.SeriesView is Series series)
            {
                int pos = 0;
                if (Chart1Series.Contains(series)) pos = 1; else if (Chart2Series.Contains(series)) pos = 2; else if (Chart3Series.Contains(series)) pos = 3; else if (Chart4Series.Contains(series)) pos = 4; else if (Chart5Series.Contains(series)) pos = 5; else if (Chart6Series.Contains(series)) pos = 6;
                if (pos == 0) return;
                var config = _dashboardConfigurations.FirstOrDefault(c => c.ChartPosition == pos);
                if (config != null) DrillDownRequested?.Invoke(config.TableName, StartDate, EndDate);
            }
        }
    }
}