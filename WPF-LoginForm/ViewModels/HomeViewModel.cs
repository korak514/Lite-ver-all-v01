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
using LiveCharts.Configurations;
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
        public string TrendIcon { get; set; }
        public string TrendColor { get; set; }
    }

    public class TickOption
    {
        public string Label { get; set; }
        public int Value { get; set; }
    }

    public class HomeViewModel : ViewModelBase
    {
        private readonly IDataRepository _dataRepository;
        private readonly IDialogService _dialogService;
        private readonly ILogger _logger;
        private readonly DashboardStorageService _storageService;
        private readonly DashboardChartService _chartService;
        private readonly DataTemplate _smartLabelTemplate;

        private List<DashboardConfiguration> _dashboardConfigurations;
        public List<DashboardConfiguration> DashboardConfigs => _dashboardConfigurations;

        private bool _isSyncing = false;

        private DateTime _minSliderDate;
        private DateTime _maxSliderDate;
        private bool _isActive = false;

        private CancellationTokenSource _cts;
        private CancellationTokenSource _fileLoadCts;
        private ConcurrentDictionary<string, double> _kpiTotals;
        private ConcurrentDictionary<string, double> _kpiPrevTotals;
        private ConcurrentDictionary<(int, string), string> _colorMap = new ConcurrentDictionary<(int, string), string>();
        public ConcurrentDictionary<(int, string), string> ColorMap => _colorMap;

        private ConcurrentDictionary<(int, string), string> _userColorMap = new ConcurrentDictionary<(int, string), string>();
        public ConcurrentDictionary<(int, string), string> UserColorMap => _userColorMap;

        private ConcurrentDictionary<int, DashboardChartService.ChartResultDto> _chartResultCache = new ConcurrentDictionary<int, DashboardChartService.ChartResultDto>();

        private string _currentLoadedFilePath;

        public Action<int, DashboardConfiguration, DashboardChartService.ChartResultDto> OpenChartDetailAction { get; set; }

        public Action ReturnToPortalAction { get; set; }
        public ICommand GoBackCommand { get; }
        public ICommand CopyImageCommand { get; }
        public ICommand ExportChartDataCommand { get; }

        private bool _showMaximizeButtons;
        public bool ShowMaximizeButtons { get => _showMaximizeButtons; set => SetProperty(ref _showMaximizeButtons, value); }

        private int _maximizedChartIndex = 0;

        public int MaximizedChartIndex
        {
            get => _maximizedChartIndex;
            set
            {
                if (SetProperty(ref _maximizedChartIndex, value))
                {
                    OnPropertyChanged(nameof(IsAnyChartMaximized));
                    OnPropertyChanged(nameof(IsChart4Maximized));
                    OnPropertyChanged(nameof(IsChart6Maximized));
                    UpdatePieHoverable();
                }
            }
        }

        public bool IsAnyChartMaximized => MaximizedChartIndex > 0;
        public bool IsChart4Maximized => MaximizedChartIndex == 4;
        public bool IsChart6Maximized => MaximizedChartIndex == 6;

        public ICommand MaximizeChartCommand { get; }
        public ICommand CloseMaximizeCommand { get; }

        private bool _isSecondPageActive;
        public bool IsSecondPageActive { get => _isSecondPageActive; set => SetProperty(ref _isSecondPageActive, value); }

        public ObservableCollection<string> ErrorCategories { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> DashboardFiles { get; } = new ObservableCollection<string>();
        public ObservableCollection<KpiModel> KpiCards { get; } = new ObservableCollection<KpiModel>();
        public List<TickOption> TickOptions { get; } = new List<TickOption>();

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
                    DebounceLoadDashboardFile(value);
                }
            }
        }

        private async void DebounceLoadDashboardFile(string fileName)
        {
            _fileLoadCts?.Cancel();
            _fileLoadCts?.Dispose();
            _fileLoadCts = new CancellationTokenSource();
            var token = _fileLoadCts.Token;

            _cts?.Cancel();

            try
            {
                await Task.Delay(200, token);

                if (!token.IsCancellationRequested)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (!token.IsCancellationRequested && _isActive)
                        {
                            LoadSelectedDashboardFile(fileName);
                        }
                    });
                }
            }
            catch (TaskCanceledException) { }
        }

        public bool IsDashboardSelectorVisible => GeneralSettingsManager.Instance.Current.AutoImportEnabled && DashboardFiles.Any();

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

        private bool _globalIgnoreAfterHyphen = false;
        public bool GlobalIgnoreAfterHyphen
        { get => _globalIgnoreAfterHyphen; set { if (SetProperty(ref _globalIgnoreAfterHyphen, value)) { LoadAllChartsData(); AutoSave(); } } }

        private bool _globalIgnoreNumbers = false;
        public bool GlobalIgnoreNumbers
        { get => _globalIgnoreNumbers; set { if (SetProperty(ref _globalIgnoreNumbers, value)) { LoadAllChartsData(); AutoSave(); } } }

        public bool IsDateFilterVisible => GeneralSettingsManager.Instance.Current.ShowDashboardDateFilter;

        public int SliderTickFrequency
        {
            get => GeneralSettingsManager.Instance.Current.DashboardDateTickSize;
            set { if (GeneralSettingsManager.Instance.Current.DashboardDateTickSize != value) { GeneralSettingsManager.Instance.Current.DashboardDateTickSize = value; GeneralSettingsManager.Instance.Save(); OnPropertyChanged(); } }
        }

        private bool _isDateFilterEnabled;
        public bool IsDateFilterEnabled { get => _isDateFilterEnabled; private set => SetProperty(ref _isDateFilterEnabled, value); }

        public bool IsChart6Visible => _dashboardConfigurations != null && _dashboardConfigurations.Any(c => c.ChartPosition == 6 && c.IsEnabled && !c.ShowAsKpiCards);

        private DateTime _startDate;

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
                    if (!_isSyncing && _isActive && IsFilterByDate)
                    {
                        UpdateSlidersFromDates(true);
                    }
                }
            }
        }

        private DateTime _endDate;

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
                    if (!_isSyncing && _isActive && IsFilterByDate)
                    {
                        UpdateSlidersFromDates(true);
                    }
                }
            }
        }

        private double _sliderMaximum;
        public double SliderMaximum { get => _sliderMaximum; set => SetProperty(ref _sliderMaximum, value); }

        private double _startMonthSliderValue;

        public double StartMonthSliderValue
        {
            get => _startMonthSliderValue;
            set
            {
                if (_startMonthSliderValue != value)
                {
                    _startMonthSliderValue = value;
                    OnPropertyChanged();
                    if (!_isSyncing && _isActive)
                    {
                        if (IsFilterByDate) UpdateDatesFromSliders(true);
                        else
                        {
                            UpdateTooltips();
                            LoadAllChartsData();
                        }
                    }
                }
            }
        }

        private double _endMonthSliderValue;

        public double EndMonthSliderValue
        {
            get => _endMonthSliderValue;
            set
            {
                if (_endMonthSliderValue != value)
                {
                    _endMonthSliderValue = value;
                    OnPropertyChanged();
                    if (!_isSyncing && _isActive)
                    {
                        if (IsFilterByDate) UpdateDatesFromSliders(true);
                        else
                        {
                            UpdateTooltips();
                            LoadAllChartsData();
                        }
                    }
                }
            }
        }

        private string _startSliderTooltip; public string StartSliderTooltip { get => _startSliderTooltip; set => SetProperty(ref _startSliderTooltip, value); }
        private string _endSliderTooltip; public string EndSliderTooltip { get => _endSliderTooltip; set => SetProperty(ref _endSliderTooltip, value); }

        private SeriesCollection _chart1Series = new SeriesCollection(); public SeriesCollection Chart1Series { get => _chart1Series; set => SetProperty(ref _chart1Series, value); }
        private SeriesCollection _chart2Series = new SeriesCollection(); public SeriesCollection Chart2Series { get => _chart2Series; set => SetProperty(ref _chart2Series, value); }
        private SeriesCollection _chart3Series = new SeriesCollection(); public SeriesCollection Chart3Series { get => _chart3Series; set => SetProperty(ref _chart3Series, value); }
        private SeriesCollection _chart4Series = new SeriesCollection(); public SeriesCollection Chart4Series { get => _chart4Series; set => SetProperty(ref _chart4Series, value); }
        private SeriesCollection _chart5Series = new SeriesCollection(); public SeriesCollection Chart5Series { get => _chart5Series; set => SetProperty(ref _chart5Series, value); }
        private SeriesCollection _chart6Series = new SeriesCollection(); public SeriesCollection Chart6Series { get => _chart6Series; set => SetProperty(ref _chart6Series, value); }

        private AxesCollection _chart1X = new AxesCollection { new Axis() }; public AxesCollection Chart1X { get => _chart1X; set => SetProperty(ref _chart1X, value); }
        private AxesCollection _chart1Y = new AxesCollection { new Axis() }; public AxesCollection Chart1Y { get => _chart1Y; set => SetProperty(ref _chart1Y, value); }
        private AxesCollection _chart2X = new AxesCollection { new Axis() }; public AxesCollection Chart2X { get => _chart2X; set => SetProperty(ref _chart2X, value); }
        private AxesCollection _chart2Y = new AxesCollection { new Axis() }; public AxesCollection Chart2Y { get => _chart2Y; set => SetProperty(ref _chart2Y, value); }
        private AxesCollection _chart3X = new AxesCollection { new Axis() }; public AxesCollection Chart3X { get => _chart3X; set => SetProperty(ref _chart3X, value); }
        private AxesCollection _chart3Y = new AxesCollection { new Axis() }; public AxesCollection Chart3Y { get => _chart3Y; set => SetProperty(ref _chart3Y, value); }
        private AxesCollection _chart5X = new AxesCollection { new Axis() }; public AxesCollection Chart5X { get => _chart5X; set => SetProperty(ref _chart5X, value); }
        private AxesCollection _chart5Y = new AxesCollection { new Axis() }; public AxesCollection Chart5Y { get => _chart5Y; set => SetProperty(ref _chart5Y, value); }

        public Func<double, string> DateFormatter { get; }
        public Func<double, string> NumberFormatter { get; set; }

        public HomeViewModel(IDataRepository dataRepository, IDialogService dialogService, ILogger logger)
        {
            _dataRepository = dataRepository; _dialogService = dialogService; _logger = logger;
            _storageService = new DashboardStorageService(); _chartService = new DashboardChartService();
            _dashboardConfigurations = new List<DashboardConfiguration>(); _kpiTotals = new ConcurrentDictionary<string, double>(); _kpiPrevTotals = new ConcurrentDictionary<string, double>();

            ConfigureCommand = new ViewModelCommand(p => ShowConfigurationWindow()); ImportCommand = new ViewModelCommand(p => ImportConfiguration()); ExportCommand = new ViewModelCommand(p => ExportConfiguration());
            RecolorChartsCommand = new ViewModelCommand(p => ExecuteRefreshCommand()); ToggleFilterModeCommand = new ViewModelCommand(p => IsFilterByDate = !IsFilterByDate); ChartClickCommand = new ViewModelCommand(ExecuteChartClick);
            TogglePageCommand = new ViewModelCommand(p => IsSecondPageActive = !IsSecondPageActive);

            TickOptions.Add(new TickOption { Label = "1 Month", Value = 1 });
            TickOptions.Add(new TickOption { Label = "3 Months", Value = 3 });
            TickOptions.Add(new TickOption { Label = "6 Months", Value = 6 });
            TickOptions.Add(new TickOption { Label = "1 Year", Value = 12 });

            MaximizeChartCommand = new ViewModelCommand(p =>
            {
                if (int.TryParse(p?.ToString(), out int i))
                {
                    var config = _dashboardConfigurations.FirstOrDefault(c => c.ChartPosition == i);
                    if (config != null && _chartResultCache.TryGetValue(i, out var cachedResult))
                    {
                        OpenChartDetailAction?.Invoke(i, config, cachedResult);
                    }
                }
            });

            CloseMaximizeCommand = new ViewModelCommand(p => { MaximizedChartIndex = 0; });

            CopyImageCommand = new ViewModelCommand(ExecuteCopyImage); ExportChartDataCommand = new ViewModelCommand(ExecuteExportChartData);
            GoBackCommand = new ViewModelCommand(p => { Deactivate(); ReturnToPortalAction?.Invoke(); });

            _startDate = DateTime.Today.AddYears(-1); _endDate = DateTime.Today;
            DateFormatter = value => (value < DateTime.MinValue.Ticks || value > DateTime.MaxValue.Ticks) ? "" : new DateTime((long)value).ToString("d");
            NumberFormatter = val => DashboardChartService.FormatKiloMega(val);

            try
            {
                if (Application.Current != null)
                {
                    _smartLabelTemplate = Application.Current.TryFindResource("SmartLabelTemplate") as DataTemplate;
                }
            }
            catch (Exception ex) { _logger?.LogError("Failed to initialize SmartLabel Template", ex); }

            ClearAndReinitializeAxes(); TryLoadAutoSave();
        }

        private double ParseSafeDouble(object obj, CultureInfo culture)
        {
            if (obj == null || obj == DBNull.Value) return 0.0;
            string strVal = obj.ToString().Replace("₺", "").Replace("TL", "").Replace("%", "").Trim();
            int lastComma = strVal.LastIndexOf(','); int lastDot = strVal.LastIndexOf('.');
            if (lastComma > lastDot) strVal = strVal.Replace(".", "").Replace(",", ".");
            else if (lastDot > lastComma && lastComma != -1) strVal = strVal.Replace(",", "");
            else if (lastComma != -1 && lastDot == -1) strVal = strVal.Replace(",", ".");
            else if (lastDot != -1 && lastComma == -1) strVal = strVal.Replace(".", "");
            double.TryParse(strVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double res);
            return res;
        }

        private async void LoadAllChartsData()
        {
            if (_cts != null) { _cts.Cancel(); _cts.Dispose(); }
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                await Task.Delay(300, token);
                if (token.IsCancellationRequested || !_isActive) return;

                if (_dashboardConfigurations == null) return;
                if (!_dashboardConfigurations.Any()) for (int i = 1; i <= 6; i++) _dashboardConfigurations.Add(new DashboardConfiguration { ChartPosition = i, IsEnabled = false });

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (token.IsCancellationRequested || !_isActive) return;

                    Chart1Series = new SeriesCollection(); Chart2Series = new SeriesCollection(); Chart3Series = new SeriesCollection();
                    Chart4Series = new SeriesCollection(); Chart5Series = new SeriesCollection(); Chart6Series = new SeriesCollection();
                    ClearAndReinitializeAxes(); _kpiTotals.Clear(); _kpiPrevTotals.Clear(); _chartResultCache.Clear();
                });

                var validConfigs = _dashboardConfigurations.Where(c => c.IsEnabled && !string.IsNullOrEmpty(c.TableName) && c.Series.Any(s => !string.IsNullOrEmpty(s.ColumnName) || s.IsCombinationLabel)).ToList();
                var tasks = validConfigs.Select(config => ProcessChartConfigurationAsync(config, token)).ToList();

                await Task.WhenAll(tasks);

                if (token.IsCancellationRequested || !_isActive) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (token.IsCancellationRequested || !_isActive) return;

                    UpdatePieHoverable();
                    OnPropertyChanged(nameof(IsChart6Visible));
                    KpiCards.Clear();
                    var colors = new[] { "#3498DB", "#2ECC71", "#9B59B6", "#F1C40F", "#E67E22", "#E74C3C" };
                    int cIdx = 0;

                    var kpiSource = _kpiTotals.AsEnumerable();
                    var kpiConfig = _dashboardConfigurations.FirstOrDefault(c => c.ChartPosition == 6 && c.IsEnabled && c.ShowAsKpiCards);
                    if (kpiConfig != null)
                    {
                        var allowedMetrics = kpiConfig.Series.Select(s => s.ColumnName).ToHashSet();
                        kpiSource = kpiSource.Where(kvp => allowedMetrics.Contains(kvp.Key));
                    }

                    foreach (var kvp in kpiSource.OrderByDescending(x => x.Value))
                    {
                        double prev = _kpiPrevTotals.TryGetValue(kvp.Key, out double p) ? p : 0;
                        string icon = "↔ 0%"; string tColor = "Gray";
                        if (prev > 0)
                        {
                            double change = ((kvp.Value - prev) / prev) * 100;
                            bool isBadMetric = kvp.Key.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 || kvp.Key.IndexOf("scrap", StringComparison.OrdinalIgnoreCase) >= 0;
                            if (change > 0.1) { icon = "↑ " + Math.Abs(change).ToString("N1") + "%"; tColor = isBadMetric ? "#E74C3C" : "#2ECC71"; }
                            else if (change < -0.1) { icon = "↓ " + Math.Abs(change).ToString("N1") + "%"; tColor = isBadMetric ? "#2ECC71" : "#E74C3C"; }
                        }
                        KpiCards.Add(new KpiModel { Title = kvp.Key, Value = DashboardChartService.FormatKiloMega(kvp.Value), ColorHex = colors[cIdx % colors.Length], TrendIcon = icon, TrendColor = tColor });
                        cIdx++;
                    }
                });
            }
            catch (TaskCanceledException) { }
            catch (Exception ex) { _logger?.LogError($"Error loading charts: {ex.Message}"); }
        }

        // Controls pie chart hoverable state via XAML bindings (IsChart4Maximized / IsChart6Maximized)
        // When not maximized → Hoverable=false → no tooltip/labels on hover
        // When maximized → Hoverable=true → tooltip shows LabelPoint text with %
        private void UpdatePieHoverable()
        {
            OnPropertyChanged(nameof(IsChart4Maximized));
            OnPropertyChanged(nameof(IsChart6Maximized));
        }

        private async Task ProcessChartConfigurationAsync(DashboardConfiguration config, CancellationToken token)
        {
            if (token.IsCancellationRequested || !_isActive) return;
            try
            {
                var cols = new HashSet<string>();
                if (!string.IsNullOrEmpty(config.DateColumn)) cols.Add(config.DateColumn);
                if (!string.IsNullOrEmpty(config.SplitByColumn)) cols.Add(config.SplitByColumn);

                foreach (var series in config.Series)
                {
                    if (series.IsCombinationLabel && series.SavedStates != null && series.ActiveStateIndex >= 0 && series.SavedStates.Count > series.ActiveStateIndex)
                    {
                        var state = series.SavedStates[series.ActiveStateIndex];
                        if (state != null && state.Nodes != null)
                        {
                            foreach (var node in state.Nodes.Where(n => n.NodeType == "DataColumn"))
                            {
                                if (!string.IsNullOrEmpty(node.Value)) cols.Add(node.Value);
                            }
                        }
                    }
                    else if (!string.IsNullOrEmpty(series.ColumnName))
                    {
                        cols.Add(series.ColumnName);
                    }
                }

                DateTime safeStart = StartDate < new DateTime(1753, 1, 1) ? new DateTime(1753, 1, 1) : StartDate;
                DateTime safeEnd = EndDate < safeStart ? safeStart : EndDate;
                DateTime? filterStart = (IsFilterByDate && config.DataStructureType == "Daily Date") ? (DateTime?)safeStart : null;
                DateTime? filterEnd = (IsFilterByDate && config.DataStructureType == "Daily Date") ? (DateTime?)safeEnd : null;

                var dt = await _dataRepository.GetDataAsync(config.TableName, cols.ToList(), config.DateColumn, filterStart, filterEnd);
                if (token.IsCancellationRequested || !_isActive || dt == null) return;

                DataTable dtPrev = null;
                if (IsFilterByDate && config.DataStructureType == "Daily Date")
                {
                    TimeSpan duration = safeEnd - safeStart;
                    if (duration.TotalDays > 0 && duration.TotalDays <= 180)
                        dtPrev = await _dataRepository.GetDataAsync(config.TableName, cols.ToList(), config.DateColumn, safeStart.Subtract(duration), safeStart.AddDays(-1));
                }

                bool isAvg = string.Equals(config.ValueAggregation, "Average", StringComparison.OrdinalIgnoreCase);
                var culture = config.UseInvariantCultureForNumbers ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture;

                foreach (var seriesConfig in config.Series)
                {
                    if (!seriesConfig.IsCombinationLabel && dt.Columns.Contains(seriesConfig.ColumnName))
                    {
                        var validRows = dt.AsEnumerable().Where(r => r[seriesConfig.ColumnName] != DBNull.Value).ToList();
                        if (validRows.Any())
                        {
                            double aggVal = isAvg
                                ? validRows.Average(r => ParseSafeDouble(r[seriesConfig.ColumnName], culture))
                                : validRows.Sum(r => ParseSafeDouble(r[seriesConfig.ColumnName], culture));
                            _kpiTotals.AddOrUpdate(seriesConfig.ColumnName, aggVal, (key, old) => isAvg ? (aggVal + old) / 2 : aggVal + old);
                        }

                        if (dtPrev != null && dtPrev.Columns.Contains(seriesConfig.ColumnName))
                        {
                            var validPrevRows = dtPrev.AsEnumerable().Where(r => r[seriesConfig.ColumnName] != DBNull.Value).ToList();
                            if (validPrevRows.Any())
                            {
                                double prevAggVal = isAvg
                                    ? validPrevRows.Average(r => ParseSafeDouble(r[seriesConfig.ColumnName], culture))
                                    : validPrevRows.Sum(r => ParseSafeDouble(r[seriesConfig.ColumnName], culture));
                                _kpiPrevTotals.AddOrUpdate(seriesConfig.ColumnName, prevAggVal, (key, old) => isAvg ? (prevAggVal + old) / 2 : prevAggVal + old);
                            }
                        }
                    }
                }

                // Pre-populate fixed colors for this config's series
                foreach (var ser in config.Series)
                {
                    if (!string.IsNullOrEmpty(ser.SeriesColorHex))
                    {
                        string title = ser.ColumnName;
                        if (string.IsNullOrEmpty(title)) title = "Series";

                        _colorMap[(config.ChartPosition, title)] = ser.SeriesColorHex;
                        _userColorMap[(config.ChartPosition, title)] = ser.SeriesColorHex;
                    }
                }

                var chartResult = await Task.Run(() => _chartService.ProcessChartData(dt, config, IsFilterByDate, IgnoreNonDateData, StartMonthSliderValue, EndMonthSliderValue, SliderMaximum, _colorMap, _userColorMap, GlobalIgnoreAfterHyphen, GlobalIgnoreNumbers), token);
                if (token.IsCancellationRequested || !_isActive) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (!token.IsCancellationRequested && _isActive)
                    {
                        ApplyChartResultToUI(config.ChartPosition, chartResult);
                        _chartResultCache[config.ChartPosition] = chartResult;
                    }
                });
            }
            catch (Exception ex) { _logger?.LogError($"Error processing chart position {config.ChartPosition}", ex); }
        }

        private void SetSeriesCollection(int pos, SeriesCollection collection)
        {
            switch (pos) { case 1: Chart1Series = collection; break; case 2: Chart2Series = collection; break; case 3: Chart3Series = collection; break; case 4: Chart4Series = collection; break; case 5: Chart5Series = collection; break; case 6: Chart6Series = collection; break; }
        }

        private void SetXAxes(int pos, AxesCollection ax)
        {
            switch (pos) { case 1: Chart1X = ax; break; case 2: Chart2X = ax; break; case 3: Chart3X = ax; break; case 5: Chart5X = ax; break; }
        }

        private void SetYAxes(int pos, AxesCollection ax)
        {
            switch (pos) { case 1: Chart1Y = ax; break; case 2: Chart2Y = ax; break; case 3: Chart3Y = ax; break; case 5: Chart5Y = ax; break; }
        }

        private void ApplyChartResultToUI(int position, DashboardChartService.ChartResultDto result)
        {
            if (result == null || result.Series == null) return;

            var newSeries = new SeriesCollection();
            var axisColor = Brushes.WhiteSmoke;
            var xyMapper = Mappers.Xy<DashboardDataPoint>().X(p => p != null ? p.X : 0).Y(p => p != null ? p.Y : 0);

            foreach (var s in result.Series)
            {
                if (s == null) continue;

                Brush colorBrush = (Brush)new BrushConverter().ConvertFrom(s.ColorHex);

                if (s.SeriesType == "Pie")
                {
                    string hoverName = string.IsNullOrEmpty(s.FullName) ? s.Title : s.FullName;

                    var pieMapper = Mappers.Pie<DashboardDataPoint>().Value(p => p.Y);
                    var pieCv = new ChartValues<DashboardDataPoint>
                    {
                        new DashboardDataPoint
                        {
                            Y = s.PieValues.FirstOrDefault(),
                            TooltipHeader = hoverName,
                            TooltipLeft = "Total:",
                            TooltipRight = DashboardChartService.FormatKiloMega(s.PieValues.FirstOrDefault())
                        }
                    };

                    newSeries.Add(new PieSeries
                    {
                        Title = s.Title,
                        Values = pieCv,
                        Configuration = pieMapper,
                        DataLabels = false,
                        LabelPoint = p => $"{hoverName}\n{DashboardChartService.FormatKiloMega(p.Y)} ({p.Participation:P0})",
                        Fill = colorBrush
                    });
                }
                else
                {
                    var cv = new ChartValues<DashboardDataPoint>();
                    if (s.Points != null) foreach (var pt in s.Points) cv.Add(pt);

                    bool isSmall = cv.Count <= 35;
                    bool showLabels = isSmall && !s.ShowOnlyHoverLabels;

                    if (s.SeriesType == "Line")
                    {
                        newSeries.Add(new LineSeries
                        {
                            Title = s.Title,
                            Values = cv,
                            Configuration = xyMapper,
                            PointGeometry = DefaultGeometries.Circle,
                            PointGeometrySize = 9,
                            StrokeThickness = 2.5,
                            Stroke = colorBrush,
                            Fill = Brushes.Transparent,
                            DataLabels = showLabels,
                            Foreground = Brushes.WhiteSmoke,
                            LabelPoint = p =>
                            {
                                if (p == null) return "";
                                var ddp = p.Instance as DashboardDataPoint;
                                return (ddp != null && !string.IsNullOrEmpty(ddp.Label)) ? ddp.Label : DashboardChartService.FormatKiloMega(p.Y);
                            },
                            DataLabelsTemplate = _smartLabelTemplate
                        });
                    }
                    else
                    {
                        newSeries.Add(new ColumnSeries
                        {
                            Title = s.Title,
                            Values = cv,
                            Configuration = xyMapper,
                            Fill = colorBrush,
                            DataLabels = showLabels,
                            Foreground = Brushes.WhiteSmoke,
                            LabelPoint = p =>
                            {
                                if (p == null) return "";
                                var ddp = p.Instance as DashboardDataPoint;
                                return (ddp != null && !string.IsNullOrEmpty(ddp.Label)) ? ddp.Label : DashboardChartService.FormatKiloMega(p.Y);
                            },
                            DataLabelsTemplate = _smartLabelTemplate
                        });
                    }
                }
            }

            var newX = new AxesCollection();
            var newY = new AxesCollection();

            if (result.Series.Any(x => x.SeriesType != "Pie"))
            {
                double minVal = double.MaxValue;
                double maxVal = double.MinValue;
                double minX = double.MaxValue;
                double maxX = double.MinValue;
                int maxPointCount = 0;
                bool hasValues = false;

                foreach (var s in result.Series)
                {
                    if (s.Points != null && s.Points.Any())
                    {
                        var validPts = s.Points.Where(p => !double.IsNaN(p.Y)).ToList();
                        if (validPts.Any())
                        {
                            double sMin = validPts.Min(p => p.Y);
                            double sMax = validPts.Max(p => p.Y);
                            if (sMin < minVal) minVal = sMin;
                            if (sMax > maxVal) maxVal = sMax;

                            double sMinX = validPts.Min(p => p.X);
                            double sMaxX = validPts.Max(p => p.X);
                            if (sMinX < minX) minX = sMinX;
                            if (sMaxX > maxX) maxX = sMaxX;

                            if (s.Points.Count > maxPointCount) maxPointCount = s.Points.Count;
                            hasValues = true;
                        }
                    }
                }

                var yAxis = new Axis
                {
                    Title = "Values",
                    LabelFormatter = NumberFormatter,
                    Foreground = axisColor,
                    FontSize = 12
                };

                if (hasValues && !double.IsNaN(minVal) && !double.IsNaN(maxVal))
                {
                    bool hasColumnSeries = result.Series.Any(x => x.SeriesType == "Column" || x.SeriesType == "Bar");
                    bool forceMinZero = hasColumnSeries && minVal >= 0;
                    bool forceMaxZero = hasColumnSeries && maxVal <= 0;

                    double dataRange = maxVal - minVal;
                    if (Math.Abs(dataRange) < 0.0001)
                    {
                        dataRange = Math.Abs(minVal) > 0 ? Math.Abs(minVal) * 0.2 : 10;
                    }

                    double topBuffer = 0.15 * dataRange;
                    double bottomBuffer = forceMinZero ? 0 : Math.Max(0.20 * dataRange, Math.Abs(minVal) * 0.1);

                    double desiredMin = forceMinZero ? 0 : minVal - bottomBuffer;
                    double desiredMax = maxVal + topBuffer;

                    if (maxVal <= 0 && minVal < 0)
                    {
                        double rawRequiredMax = Math.Abs(minVal) / 5.0;
                        if (rawRequiredMax > 0)
                        {
                            double magMax = Math.Pow(10, Math.Floor(Math.Log10(rawRequiredMax)));
                            double relMax = rawRequiredMax / magMax;

                            double niceMax;
                            if (relMax <= 1.5) niceMax = 1.5;
                            else if (relMax <= 3.0) niceMax = 3.0;
                            else if (relMax <= 5.0) niceMax = 5.0;
                            else niceMax = 10.0;

                            double roundedMax = niceMax * magMax;

                            if (desiredMax < roundedMax)
                            {
                                desiredMax = roundedMax;
                            }
                        }
                    }
                    else
                    {
                        if (forceMaxZero && desiredMax < 0) desiredMax = 0;
                        if (forceMinZero && desiredMin > 0) desiredMin = 0;
                    }

                    if (desiredMax <= desiredMin)
                    {
                        desiredMax = desiredMin + Math.Max(1, Math.Abs(desiredMin) * 0.1);
                    }

                    double range = desiredMax - desiredMin;

                    double targetTicks = 6.0;
                    double rawStep = range / targetTicks;
                    if (rawStep <= 0) rawStep = 1;

                    double mag = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
                    double relStep = rawStep / mag;

                    double niceStep = relStep <= 1.2 ? 1.0 : relStep <= 2.0 ? 2.0 : relStep <= 3.5 ? 3.0 : relStep <= 7.5 ? 5.0 : 10.0;
                    double finalStep = niceStep * mag;

                    yAxis.MinValue = Math.Floor(desiredMin / finalStep) * finalStep;
                    yAxis.MaxValue = Math.Ceiling(desiredMax / finalStep) * finalStep;

                    if (yAxis.MaxValue <= yAxis.MinValue)
                    {
                        yAxis.MaxValue = yAxis.MinValue + finalStep;
                    }

                    yAxis.Separator = new LiveCharts.Wpf.Separator { Step = finalStep, StrokeThickness = 1, Stroke = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)) };
                }
                else
                {
                    yAxis.MinValue = -0.5;
                    yAxis.MaxValue = 0.5;
                    yAxis.Separator = new LiveCharts.Wpf.Separator { Step = 0.5, StrokeThickness = 1, Stroke = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)) };
                }

                var xAxis = new Axis
                {
                    Foreground = axisColor,
                    Separator = new LiveCharts.Wpf.Separator { StrokeThickness = 0, Stroke = Brushes.Transparent }
                };

                if (result.IsDateAxis)
                {
                    xAxis.LabelFormatter = DateFormatter;
                    xAxis.Labels = null;

                    if (hasValues && maxPointCount > 1 && maxX > minX)
                    {
                        double avgStep = (maxX - minX) / (maxPointCount - 1);
                        xAxis.MaxValue = maxX + avgStep;
                        xAxis.MinValue = minX - (avgStep * 0.1);
                    }
                }
                else
                {
                    xAxis.LabelFormatter = null;
                    xAxis.Labels = result.XAxisLabels;

                    if (hasValues)
                    {
                        xAxis.MaxValue = maxX + 0.85;
                        xAxis.MinValue = minX - 0.15;
                    }
                }

                newY.Add(yAxis);
                newX.Add(xAxis);
            }
            else
            {
                newX.Add(new Axis());
                newY.Add(new Axis());
            }

            SetSeriesCollection(position, newSeries);
            SetXAxes(position, newX);
            SetYAxes(position, newY);
        }

        private void ClearAndReinitializeAxes()
        {
            Chart1X = new AxesCollection { new Axis() }; Chart1Y = new AxesCollection { new Axis() };
            Chart2X = new AxesCollection { new Axis() }; Chart2Y = new AxesCollection { new Axis() };
            Chart3X = new AxesCollection { new Axis() }; Chart3Y = new AxesCollection { new Axis() };
            Chart5X = new AxesCollection { new Axis() }; Chart5Y = new AxesCollection { new Axis() };
        }

        private void ExecuteCopyImage(object parameter)
        {
            if (parameter is FrameworkElement element) { try { var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap((int)element.ActualWidth, (int)element.ActualHeight, 96, 96, PixelFormats.Pbgra32); rtb.Render(element); Clipboard.SetImage(rtb); MessageBox.Show("Chart copied to clipboard!", "Success", MessageBoxButton.OK, MessageBoxImage.Information); } catch (Exception ex) { MessageBox.Show($"Could not copy image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); } }
        }

        private void ExecuteExportChartData(object parameter)
        {
            if (parameter != null && int.TryParse(parameter.ToString(), out int pos))
            {
                var targetSeries = GetSeriesCollection(pos);
                var targetX = GetXAxes(pos);

                if (targetSeries == null || targetSeries.Count == 0 || targetSeries[0].Values == null || targetSeries[0].Values.Count == 0) return;

                if (_dialogService.ShowSaveFileDialog("Export Chart Data", $"Chart_{pos}_Data_{DateTime.Now:yyyyMMdd}", ".csv", "CSV Files|*.csv", out string path))
                {
                    try
                    {
                        var sb = new System.Text.StringBuilder();
                        sb.Append("Category"); foreach (var series in targetSeries) sb.Append($",{series.Title.Replace(",", " ")}"); sb.AppendLine();

                        if (targetSeries[0] is PieSeries)
                        {
                            foreach (PieSeries p in targetSeries) if (p.Values.Count > 0) sb.AppendLine($"{p.Title.Replace(",", " ")},{(double)p.Values[0]}");
                        }
                        else
                        {
                            int pointCount = targetSeries[0].Values.Count;
                            bool hasLabels = targetX != null && targetX.Count > 0 && targetX[0].Labels != null && targetX[0].Labels.Count == pointCount;

                            for (int i = 0; i < pointCount; i++)
                            {
                                string label = hasLabels ? targetX[0].Labels[i] : $"Point {i + 1}";
                                if (targetSeries[0].Values[0] is DashboardDataPoint ddp && ddp.X > 600000000000000000) label = new DateTime((long)ddp.X).ToString("yyyy-MM-dd");
                                sb.Append(label.Replace(",", " "));
                                foreach (var series in targetSeries) { if (i < series.Values.Count && series.Values[i] is DashboardDataPoint dp) sb.Append($",{dp.Y}"); else sb.Append(",0"); }
                                sb.AppendLine();
                            }
                        }
                        File.WriteAllText(path, sb.ToString(), System.Text.Encoding.UTF8); MessageBox.Show("Chart data exported successfully.", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex) { MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
                }
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

        private async void ShowConfigurationWindow()
        {
            var vm = new ConfigurationViewModel(_dataRepository); await vm.InitializeAsync(); vm.LoadConfigurations(_dashboardConfigurations);
            if (_dialogService.ShowConfigurationDialog(vm))
            {
                _dashboardConfigurations = vm.GetFinalConfigurations(); OnPropertyChanged(nameof(DashboardConfigs));
                AutoSave();
                if (_isActive)
                {
                    _colorMap.Clear();
                    _userColorMap.Clear();
                    _chartResultCache.Clear();
                    _ = InitializeDashboardAsync();
                }
                else
                {
                    Activate();
                }
            }
        }

        public async void Activate()
        { if (_isActive) return; _isActive = true; OnPropertyChanged(nameof(IsDateFilterVisible)); if (GeneralSettingsManager.Instance.Current.AutoImportEnabled) RefreshDashboardFiles(); await InitializeDashboardAsync(); }

        public void Deactivate()
        {
            _isActive = false;
            _cts?.Cancel();
            _fileLoadCts?.Cancel();

            AutoSave();

            Application.Current?.Dispatcher.Invoke(() =>
            {
                Chart1Series = new SeriesCollection(); Chart2Series = new SeriesCollection();
                Chart3Series = new SeriesCollection(); Chart4Series = new SeriesCollection();
                Chart5Series = new SeriesCollection(); Chart6Series = new SeriesCollection();
                ClearAndReinitializeAxes();
            });
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
            foreach (var c in configs) { try { var range = await _dataRepository.GetDateRangeAsync(c.TableName, c.DateColumn); if (range.Min != DateTime.MinValue && range.Min < min) min = range.Min; if (range.Max != DateTime.MinValue && range.Max > max) max = range.Max; } catch { } }
            if (min == DateTime.MaxValue) min = DateTime.Now.AddYears(-1); if (max == DateTime.MinValue) max = DateTime.Now;
            _minSliderDate = min; _maxSliderDate = new DateTime(max.Year, max.Month, DateTime.DaysInMonth(max.Year, max.Month));
        }

        private void InitializeSliders()
        {
            if (!IsFilterByDate) { SliderMaximum = 100; StartMonthSliderValue = 0; EndMonthSliderValue = 100; UpdateTooltips(); return; }
            if (_minSliderDate == default) return;

            if (_startDate != DateTime.MinValue && _startDate < _minSliderDate) _minSliderDate = _startDate;
            if (_endDate != DateTime.MinValue && _endDate > _maxSliderDate) _maxSliderDate = _endDate;
            if (_startDate == DateTime.MinValue) _startDate = _minSliderDate;
            if (_endDate == DateTime.MinValue) _endDate = _maxSliderDate;
            if (_startDate > _endDate) { _startDate = _minSliderDate; _endDate = _maxSliderDate; }

            Application.Current.Dispatcher.Invoke(() => { OnPropertyChanged(nameof(StartDate)); OnPropertyChanged(nameof(EndDate)); });

            SliderMaximum = ((_maxSliderDate.Year - _minSliderDate.Year) * 12) + _maxSliderDate.Month - _minSliderDate.Month;
            if (SliderMaximum < 0) SliderMaximum = 0;
            UpdateSlidersFromDates(false);
        }

        private void UpdateSlidersFromDates(bool triggerLoad = true)
        {
            if (_isSyncing || _minSliderDate == default || !IsFilterByDate) return;
            _isSyncing = true;

            _startMonthSliderValue = ((_startDate.Year - _minSliderDate.Year) * 12) + _startDate.Month - _minSliderDate.Month;
            _endMonthSliderValue = ((_endDate.Year - _minSliderDate.Year) * 12) + _endDate.Month - _minSliderDate.Month;

            if (_startMonthSliderValue < 0) _startMonthSliderValue = 0;
            if (_endMonthSliderValue > _sliderMaximum) _endMonthSliderValue = _sliderMaximum;

            OnPropertyChanged(nameof(StartMonthSliderValue));
            OnPropertyChanged(nameof(EndMonthSliderValue));

            UpdateTooltips();
            _isSyncing = false;

            if (triggerLoad)
            {
                LoadAllChartsData();
                AutoSave();
            }
        }

        private void UpdateDatesFromSliders(bool triggerLoad = true)
        {
            if (_isSyncing || _minSliderDate == default || !IsFilterByDate) return;
            _isSyncing = true;

            if (_startMonthSliderValue > _endMonthSliderValue)
            {
                double tmp = _startMonthSliderValue;
                _startMonthSliderValue = _endMonthSliderValue;
                _endMonthSliderValue = tmp;
                OnPropertyChanged(nameof(StartMonthSliderValue));
                OnPropertyChanged(nameof(EndMonthSliderValue));
            }

            var s = _minSliderDate.AddMonths((int)_startMonthSliderValue);
            var e = _minSliderDate.AddMonths((int)_endMonthSliderValue);

            if (e < s) e = s;

            _startDate = new DateTime(s.Year, s.Month, 1);
            _endDate = new DateTime(e.Year, e.Month, DateTime.DaysInMonth(e.Year, e.Month));

            if (_endDate < _startDate) _endDate = _startDate;

            OnPropertyChanged(nameof(StartDate));
            OnPropertyChanged(nameof(EndDate));

            UpdateTooltips();
            _isSyncing = false;

            if (triggerLoad)
            {
                LoadAllChartsData();
                AutoSave();
            }
        }

        private void UpdateTooltips()
        { if (IsFilterByDate && _minSliderDate != default) { StartSliderTooltip = _minSliderDate.AddMonths((int)_startMonthSliderValue).ToString("MMM yyyy"); EndSliderTooltip = _minSliderDate.AddMonths((int)_endMonthSliderValue).ToString("MMM yyyy"); } else { StartSliderTooltip = $"{StartMonthSliderValue:F0}%"; EndSliderTooltip = $"{EndMonthSliderValue:F0}%"; } }

        private void RefreshDashboardFiles()
        { var cfg = GeneralSettingsManager.Instance.Current; DashboardFiles.Clear(); string path = cfg.ImportIsRelative ? AppDomain.CurrentDomain.BaseDirectory : cfg.ImportAbsolutePath; if (Directory.Exists(path)) { try { foreach (var f in Directory.GetFiles(path, "*.json")) DashboardFiles.Add(Path.GetFileName(f)); } catch { } } OnPropertyChanged(nameof(IsDashboardSelectorVisible)); }

        private void LoadSelectedDashboardFile(string f)
        { var cfg = GeneralSettingsManager.Instance.Current; try { string fullPath = Path.Combine(cfg.ImportIsRelative ? AppDomain.CurrentDomain.BaseDirectory : cfg.ImportAbsolutePath, f); LoadSnapshotFromFile(fullPath); } catch { } }

        private void LoadSnapshotFromFile(string p)
        { _currentLoadedFilePath = p; var s = _storageService.LoadSnapshot(p); if (s != null) LoadDashboardFromSnapshot(s); }

        private void AutoSave()
        { var snap = new DashboardSnapshot { StartDate = StartDate, EndDate = EndDate, Configurations = _dashboardConfigurations, IsFilterByDate = IsFilterByDate, IgnoreNonDateData = IgnoreNonDateData, UseIdToDateConversion = UseIdToDateConversion, InitialDateForConversion = InitialDateForConversion, GlobalIgnoreAfterHyphen = GlobalIgnoreAfterHyphen, SeriesData = new List<ChartSeriesSnapshot>() }; _storageService.SaveSnapshot(snap, _currentLoadedFilePath); }

        private void TryLoadAutoSave()
        { var cfg = GeneralSettingsManager.Instance.Current; try { if (cfg.AutoImportEnabled) { RefreshDashboardFiles(); string def = cfg.ImportFileName; string path = cfg.ImportIsRelative ? AppDomain.CurrentDomain.BaseDirectory : cfg.ImportAbsolutePath; if (!string.IsNullOrEmpty(def) && File.Exists(Path.Combine(path, def))) { LoadSnapshotFromFile(Path.Combine(path, def)); if (DashboardFiles.Contains(def)) { _selectedDashboardFile = def; OnPropertyChanged(nameof(SelectedDashboardFile)); } return; } } var s = _storageService.LoadSnapshot(); if (s != null) LoadDashboardFromSnapshot(s); } catch { } }

        private void ImportConfiguration()
        { var d = new OpenFileDialog { Filter = "JSON|*.json" }; if (d.ShowDialog() == true) LoadSnapshotFromFile(d.FileName); }

        private void ExportConfiguration()
        { if (_dashboardConfigurations == null) return; var s = new DashboardSnapshot { StartDate = StartDate, EndDate = EndDate, Configurations = _dashboardConfigurations, GlobalIgnoreAfterHyphen = GlobalIgnoreAfterHyphen }; var d = new SaveFileDialog { Filter = "JSON|*.json" }; if (d.ShowDialog() == true) { File.WriteAllText(d.FileName, JsonConvert.SerializeObject(s, Formatting.Indented)); _currentLoadedFilePath = d.FileName; } }

        private void LoadDashboardFromSnapshot(DashboardSnapshot s)
        {
            if (s == null) return; bool wasActive = _isActive; _isActive = false;

            if (_cts != null) { _cts.Cancel(); _cts.Dispose(); _cts = null; }

            try
            {
                if (s.Configurations != null)
                {
                    foreach (var config in s.Configurations)
                    {
                        if (config.Series != null)
                        {
                            foreach (var series in config.Series)
                            {
                                if (series.SavedStates != null && series.SavedStates.Count > 3)
                                {
                                    series.SavedStates = series.SavedStates.Skip(series.SavedStates.Count - 3).ToList();
                                }
                            }
                        }
                    }
                }

                _dashboardConfigurations = s.Configurations; IsFilterByDate = s.IsFilterByDate; IgnoreNonDateData = s.IgnoreNonDateData; UseIdToDateConversion = s.UseIdToDateConversion; GlobalIgnoreAfterHyphen = s.GlobalIgnoreAfterHyphen;
                if (s.InitialDateForConversion != default) InitialDateForConversion = s.InitialDateForConversion;

                _isSyncing = true;
                if (s.StartDate != default && s.EndDate != default) { _startDate = s.StartDate; _endDate = s.EndDate; OnPropertyChanged(nameof(StartDate)); OnPropertyChanged(nameof(EndDate)); }
                _isSyncing = false;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ClearAndReinitializeAxes();
                    Chart1Series = new SeriesCollection(); Chart2Series = new SeriesCollection(); Chart3Series = new SeriesCollection();
                    Chart4Series = new SeriesCollection(); Chart5Series = new SeriesCollection(); Chart6Series = new SeriesCollection();
                });

                if (wasActive) { _isActive = true; _ = InitializeDashboardAsync(); }
            }
            finally { if (wasActive && !_isActive) _isActive = true; }
        }

        private void ExecuteRefreshCommand()
        {
            _colorMap.Clear();
            LoadAllChartsData();
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