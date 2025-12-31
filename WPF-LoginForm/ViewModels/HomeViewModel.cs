using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using Microsoft.Win32;
using Newtonsoft.Json;
using WPF_LoginForm.Models;
using WPF_LoginForm.Repositories;
using WPF_LoginForm.Services;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using WPF_LoginForm.Properties;
using System.Collections.ObjectModel;

namespace WPF_LoginForm.ViewModels
{
    public class HomeViewModel : ViewModelBase
    {
        private readonly IDataRepository _dataRepository;
        private readonly IDialogService _dialogService;
        private readonly ILogger _logger;
        private readonly DashboardStorageService _storageService;
        private readonly Random _random = new Random();

        private List<DashboardConfiguration> _dashboardConfigurations;
        private bool _isUpdatingDates = false;
        private DateTime _minSliderDate;
        private DateTime _maxSliderDate;
        private bool _isActive = false;
        private bool _isLoading = false;

        // --- Dashboard Selection Logic ---
        public ObservableCollection<string> DashboardFiles { get; } = new ObservableCollection<string>();

        private string _selectedDashboardFile;

        public string SelectedDashboardFile
        {
            get => _selectedDashboardFile;
            set
            {
                if (SetProperty(ref _selectedDashboardFile, value))
                {
                    if (_isActive && !string.IsNullOrEmpty(_selectedDashboardFile))
                    {
                        // Add a small delay to prevent rapid consecutive changes
                        Task.Delay(50).ContinueWith(_ =>
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                LoadSelectedDashboardFile(_selectedDashboardFile);
                            });
                        }, TaskScheduler.Default);
                    }
                }
            }
        }

        public bool IsDashboardSelectorVisible => Settings.Default.AutoImportEnabled && DashboardFiles.Any();

        // --- Event: Request MainViewModel to switch tabs ---
        public event Action<string, DateTime, DateTime> DrillDownRequested;

        public ICommand ConfigureCommand { get; }
        public ICommand ImportCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand RecolorChartsCommand { get; }
        public ICommand ToggleFilterModeCommand { get; }
        public ICommand ChartClickCommand { get; }

        public bool IsSnapshotExport { get; set; } = true;

        // --- Filter Mode Properties ---
        private bool _isFilterByDate = true;

        public bool IsFilterByDate
        {
            get => _isFilterByDate;
            set
            {
                if (SetProperty(ref _isFilterByDate, value))
                {
                    InitializeSliders();
                    LoadAllChartsData();
                    AutoSave();
                }
            }
        }

        private bool _ignoreNonDateData = true;

        public bool IgnoreNonDateData
        {
            get => _ignoreNonDateData;
            set
            {
                if (SetProperty(ref _ignoreNonDateData, value))
                {
                    LoadAllChartsData();
                    AutoSave();
                }
            }
        }

        // --- Virtual ID-to-Date Properties ---
        private bool _useIdToDateConversion = false;

        public bool UseIdToDateConversion
        {
            get => _useIdToDateConversion;
            set { if (SetProperty(ref _useIdToDateConversion, value)) AutoSave(); }
        }

        private DateTime _initialDateForConversion = DateTime.Today.AddYears(-1);

        public DateTime InitialDateForConversion
        {
            get => _initialDateForConversion;
            set { if (SetProperty(ref _initialDateForConversion, value)) AutoSave(); }
        }

        // --- Settings Bindings ---
        public bool IsDateFilterVisible => Settings.Default.ShowDashboardDateFilter;

        public int SliderTickFrequency
        {
            get => Settings.Default.DashboardDateTickSize;
            set
            {
                if (Settings.Default.DashboardDateTickSize != value)
                {
                    Settings.Default.DashboardDateTickSize = value;
                    Settings.Default.Save();
                    OnPropertyChanged();
                }
            }
        }

        public class TickOption
        { public string Label { get; set; } public int Value { get; set; } }

        public List<TickOption> TickOptions { get; } = new List<TickOption>
        {
            new TickOption { Label = "1 Month", Value = 1 },
            new TickOption { Label = "3 Months", Value = 3 },
            new TickOption { Label = "6 Months", Value = 6 },
            new TickOption { Label = "1 Year", Value = 12 }
        };

        private bool _isDateFilterEnabled;
        public bool IsDateFilterEnabled { get => _isDateFilterEnabled; private set => SetProperty(ref _isDateFilterEnabled, value); }

        // --- Date Range Properties ---
        private DateTime _startDate;

        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                if (_startDate == value) return;
                _startDate = value;
                OnPropertyChanged();
                if (!_isUpdatingDates && _isActive && IsFilterByDate)
                {
                    LoadAllChartsData();
                    UpdateSlidersFromDates();
                    AutoSave();
                }
            }
        }

        private DateTime _endDate;

        public DateTime EndDate
        {
            get => _endDate;
            set
            {
                if (_endDate == value) return;
                _endDate = value;
                OnPropertyChanged();
                if (!_isUpdatingDates && _isActive && IsFilterByDate)
                {
                    LoadAllChartsData();
                    UpdateSlidersFromDates();
                    AutoSave();
                }
            }
        }

        // --- Slider Properties ---
        private double _sliderMaximum;

        public double SliderMaximum { get => _sliderMaximum; set => SetProperty(ref _sliderMaximum, value); }

        private double _startMonthSliderValue;

        public double StartMonthSliderValue
        {
            get => _startMonthSliderValue;
            set
            {
                if (SetProperty(ref _startMonthSliderValue, value) && !_isUpdatingDates && _isActive)
                {
                    if (IsFilterByDate) UpdateDatesFromSliders();
                    else { UpdateTooltips(); LoadAllChartsData(); }
                }
            }
        }

        private double _endMonthSliderValue;

        public double EndMonthSliderValue
        {
            get => _endMonthSliderValue;
            set
            {
                if (SetProperty(ref _endMonthSliderValue, value) && !_isUpdatingDates && _isActive)
                {
                    if (IsFilterByDate) UpdateDatesFromSliders();
                    else { UpdateTooltips(); LoadAllChartsData(); }
                }
            }
        }

        private string _startSliderTooltip;
        public string StartSliderTooltip { get => _startSliderTooltip; set => SetProperty(ref _startSliderTooltip, value); }

        private string _endSliderTooltip;
        public string EndSliderTooltip { get => _endSliderTooltip; set => SetProperty(ref _endSliderTooltip, value); }

        // --- Chart Collections ---
        public SeriesCollection Chart1Series { get; } = new SeriesCollection();

        public SeriesCollection Chart2Series { get; } = new SeriesCollection();
        public SeriesCollection Chart3Series { get; } = new SeriesCollection();
        public SeriesCollection Chart4Series { get; } = new SeriesCollection();
        public SeriesCollection Chart5Series { get; } = new SeriesCollection();

        public AxesCollection Chart1X { get; } = new AxesCollection();
        public AxesCollection Chart1Y { get; } = new AxesCollection();
        public AxesCollection Chart2X { get; } = new AxesCollection();
        public AxesCollection Chart2Y { get; } = new AxesCollection();
        public AxesCollection Chart3X { get; } = new AxesCollection();
        public AxesCollection Chart3Y { get; } = new AxesCollection();
        public AxesCollection Chart5X { get; } = new AxesCollection();
        public AxesCollection Chart5Y { get; } = new AxesCollection();

        public Func<double, string> DateFormatter { get; }

        // --- Constructor ---
        public HomeViewModel(IDataRepository dataRepository, IDialogService dialogService, ILogger logger)
        {
            _dataRepository = dataRepository ?? throw new ArgumentNullException(nameof(dataRepository));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _logger = logger;
            _storageService = new DashboardStorageService();

            _dashboardConfigurations = new List<DashboardConfiguration>();

            ConfigureCommand = new ViewModelCommand(p => ShowConfigurationWindow());
            ImportCommand = new ViewModelCommand(p => ImportConfiguration());
            ExportCommand = new ViewModelCommand(p => ExportConfiguration());
            RecolorChartsCommand = new ViewModelCommand(p => LoadAllChartsData());
            ToggleFilterModeCommand = new ViewModelCommand(p => IsFilterByDate = !IsFilterByDate);
            ChartClickCommand = new ViewModelCommand(ExecuteChartClick);

            // Explicitly set to MinValue so InitializeSliders knows to use the full range
            _startDate = DateTime.MinValue;
            _endDate = DateTime.MinValue;

            DateFormatter = value =>
            {
                if (value < DateTime.MinValue.Ticks || value > DateTime.MaxValue.Ticks) return "";
                return new DateTime((long)value).ToString("d");
            };

            InitializeEmptyAxes();
            TryLoadAutoSave();
        }

        private void ExecuteChartClick(object parameter)
        {
            if (parameter is ChartPoint point && point.SeriesView is Series series)
            {
                int chartPosition = 0;
                if (Chart1Series.Contains(series)) chartPosition = 1;
                else if (Chart2Series.Contains(series)) chartPosition = 2;
                else if (Chart3Series.Contains(series)) chartPosition = 3;
                else if (Chart4Series.Contains(series)) chartPosition = 4;
                else if (Chart5Series.Contains(series)) chartPosition = 5;

                if (chartPosition == 0) return;

                var config = _dashboardConfigurations.FirstOrDefault(c => c.ChartPosition == chartPosition);
                if (config == null || string.IsNullOrEmpty(config.TableName)) return;

                DateTime reqStart = DateTime.MinValue;
                DateTime reqEnd = DateTime.MinValue;
                bool dateFound = false;

                if (config.DataStructureType == "Daily Date")
                {
                    try
                    {
                        DateTime baseDate = _minSliderDate;
                        double xValue = point.X;

                        if (IsFilterByDate)
                        {
                            if (config.AggregationType != "Daily")
                            {
                                int monthOffset = (int)xValue;
                                reqStart = baseDate.AddMonths(monthOffset);
                            }
                            else
                            {
                                if (xValue > 1000000)
                                    reqStart = new DateTime((long)xValue);
                            }
                            reqStart = new DateTime(reqStart.Year, reqStart.Month, 1);
                            reqEnd = new DateTime(reqStart.Year, reqStart.Month, DateTime.DaysInMonth(reqStart.Year, reqStart.Month));
                            dateFound = true;
                        }
                        else if (UseIdToDateConversion)
                        {
                            double days = xValue;
                            reqStart = InitialDateForConversion.AddDays(days);
                            reqEnd = reqStart.AddDays(1);
                            dateFound = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning($"Error calculating date from click: {ex.Message}");
                    }
                }

                if (!dateFound && config.DataStructureType == "Monthly Date")
                {
                    try
                    {
                        int positionIndex = (int)point.X;
                        AxesCollection xAxisCollection = null;
                        switch (chartPosition)
                        {
                            case 1: xAxisCollection = Chart1X; break;
                            case 2: xAxisCollection = Chart2X; break;
                            case 3: xAxisCollection = Chart3X; break;
                            case 5: xAxisCollection = Chart5X; break;
                        }

                        if (xAxisCollection != null && xAxisCollection.Count > 0 &&
                            xAxisCollection[0].Labels != null && positionIndex < xAxisCollection[0].Labels.Count)
                        {
                            string monthLabel = xAxisCollection[0].Labels[positionIndex];
                            if (DateTime.TryParseExact(monthLabel, "MMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
                            {
                                reqStart = new DateTime(parsedDate.Year, parsedDate.Month, 1);
                                reqEnd = new DateTime(parsedDate.Year, parsedDate.Month, DateTime.DaysInMonth(parsedDate.Year, parsedDate.Month));
                                dateFound = true;
                            }
                        }
                    }
                    catch { }
                }

                if (!dateFound)
                {
                    reqStart = this.StartDate;
                    reqEnd = this.EndDate;
                    dateFound = true;
                }

                if (dateFound && reqStart.Year > 1900)
                {
                    DrillDownRequested?.Invoke(config.TableName, reqStart, reqEnd);
                }
            }
        }

        private void InitializeEmptyAxes()
        {
            var hiddenAxis = new Axis { ShowLabels = false, Separator = new Separator { IsEnabled = false }, Foreground = Brushes.Transparent, Visibility = Visibility.Collapsed };
            Chart1X.Add(hiddenAxis); Chart1Y.Add(hiddenAxis);
            Chart2X.Add(hiddenAxis); Chart2Y.Add(hiddenAxis);
            Chart3X.Add(hiddenAxis); Chart3Y.Add(hiddenAxis);
            Chart5X.Add(hiddenAxis); Chart5Y.Add(hiddenAxis);
        }

        private void ClearAndReinitializeAxes()
        {
            Chart1X.Clear(); Chart1Y.Clear(); Chart2X.Clear(); Chart2Y.Clear();
            Chart3X.Clear(); Chart3Y.Clear(); Chart5X.Clear(); Chart5Y.Clear();
            InitializeEmptyAxes();
        }

        private void TryLoadAutoSave()
        {
            try
            {
                if (Settings.Default.AutoImportEnabled)
                {
                    string folderPath = Settings.Default.ImportIsRelative
                        ? AppDomain.CurrentDomain.BaseDirectory
                        : Settings.Default.ImportAbsolutePath;

                    RefreshDashboardFiles(folderPath);

                    string defaultFile = Settings.Default.ImportFileName;
                    if (!string.IsNullOrEmpty(defaultFile))
                    {
                        string fullPath = Path.Combine(folderPath, defaultFile);
                        if (File.Exists(fullPath))
                        {
                            LoadSnapshotFromFile(fullPath);
                            if (DashboardFiles.Contains(defaultFile))
                            {
                                _selectedDashboardFile = defaultFile;
                                OnPropertyChanged(nameof(SelectedDashboardFile));
                            }
                            return;
                        }
                    }
                }

                var snapshot = _storageService.LoadSnapshot();
                if (snapshot != null)
                {
                    LoadDashboardFromSnapshot(snapshot);
                }
            }
            catch (Exception ex) { _logger.LogWarning($"Failed to load dashboard: {ex.Message}"); }
        }

        private void RefreshDashboardFiles(string folderPath)
        {
            DashboardFiles.Clear();
            if (Directory.Exists(folderPath))
            {
                try
                {
                    var files = Directory.GetFiles(folderPath, "*.json");
                    foreach (var f in files)
                    {
                        DashboardFiles.Add(Path.GetFileName(f));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Error listing dashboard files: {ex.Message}");
                }
            }
            OnPropertyChanged(nameof(IsDashboardSelectorVisible));
        }

        private void LoadSelectedDashboardFile(string fileName)
        {
            try
            {
                string folderPath = Settings.Default.ImportIsRelative
                        ? AppDomain.CurrentDomain.BaseDirectory
                        : Settings.Default.ImportAbsolutePath;

                string fullPath = Path.Combine(folderPath, fileName);
                if (File.Exists(fullPath))
                {
                    LoadSnapshotFromFile(fullPath);
                    _logger.LogInfo($"Switched dashboard to: {fileName}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load dashboard: {ex.Message}");
            }
        }

        private void LoadSnapshotFromFile(string path)
        {
            var snapshot = _storageService.LoadSnapshot(path);
            if (snapshot != null)
            {
                LoadDashboardFromSnapshot(snapshot);
            }
        }

        private void AutoSave()
        {
            if (_dashboardConfigurations == null || !_dashboardConfigurations.Any()) return;
            var snapshot = new DashboardSnapshot
            {
                StartDate = this.StartDate,
                EndDate = this.EndDate,
                Configurations = _dashboardConfigurations,
                SeriesData = new List<ChartSeriesSnapshot>(),
                IsFilterByDate = this.IsFilterByDate,
                IgnoreNonDateData = this.IgnoreNonDateData,
                UseIdToDateConversion = this.UseIdToDateConversion,
                InitialDateForConversion = this.InitialDateForConversion
            };
            _storageService.SaveSnapshot(snapshot);
        }

        public async void Activate()
        {
            // Prevent multiple activations
            if (_isActive) return;

            _isActive = true;
            OnPropertyChanged(nameof(IsDateFilterVisible));
            OnPropertyChanged(nameof(SliderTickFrequency));

            if (Settings.Default.AutoImportEnabled)
            {
                string folderPath = Settings.Default.ImportIsRelative
                       ? AppDomain.CurrentDomain.BaseDirectory
                       : Settings.Default.ImportAbsolutePath;
                if (!string.IsNullOrEmpty(folderPath)) RefreshDashboardFiles(folderPath);
            }

            await InitializeDashboardAsync();
        }

        public void Deactivate()
        {
            _isActive = false;
            AutoSave();
        }

        private void UpdateDateFilterState()
        {
            IsDateFilterEnabled = _dashboardConfigurations.Any(c => c.IsEnabled && c.DataStructureType == "Daily Date");
        }

        private async Task FindGlobalDateRangeAsync()
        {
            DateTime overallMin = DateTime.MaxValue;
            DateTime overallMax = DateTime.MinValue;

            var enabledConfigs = _dashboardConfigurations.Where(c => c.IsEnabled && c.DataStructureType == "Daily Date" && !string.IsNullOrEmpty(c.TableName) && !string.IsNullOrEmpty(c.DateColumn)).ToList();

            if (!enabledConfigs.Any())
            {
                _minSliderDate = DateTime.Now.AddYears(-1);
                _maxSliderDate = DateTime.Now;
                return;
            }

            foreach (var config in enabledConfigs)
            {
                try
                {
                    var (min, max) = await _dataRepository.GetDateRangeAsync(config.TableName, config.DateColumn);
                    if (min != DateTime.MinValue && min < overallMin) overallMin = min;
                    if (max != DateTime.MinValue && max > overallMax) overallMax = max;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning($"Failed to get date range for table '{config.TableName}': {ex.Message}");
                }
            }

            if (overallMin == DateTime.MaxValue) overallMin = DateTime.Now.AddYears(-1);
            if (overallMax == DateTime.MinValue) overallMax = DateTime.Now;

            _minSliderDate = overallMin;
            _maxSliderDate = new DateTime(overallMax.Year, overallMax.Month, DateTime.DaysInMonth(overallMax.Year, overallMax.Month));
        }

        private async Task InitializeDashboardAsync()
        {
            UpdateDateFilterState();
            await FindGlobalDateRangeAsync();
            InitializeSliders();
            LoadAllChartsData();
        }

        private void InitializeSliders()
        {
            if (IsFilterByDate)
            {
                if (_minSliderDate == default || _maxSliderDate == default) return;

                // Only reset to full range if truly uninitialized
                if (_startDate == DateTime.MinValue || _endDate == DateTime.MinValue)
                {
                    _startDate = _minSliderDate;
                    _endDate = _maxSliderDate;
                }
                else
                {
                    // Clamp to valid range
                    if (_startDate < _minSliderDate) _startDate = _minSliderDate;
                    if (_endDate > _maxSliderDate) _endDate = _maxSliderDate;
                    if (_startDate > _endDate)
                    {
                        _startDate = _minSliderDate;
                        _endDate = _maxSliderDate;
                    }
                }

                // Use dispatcher to update UI properties
                Application.Current.Dispatcher.Invoke(() =>
                {
                    OnPropertyChanged(nameof(StartDate));
                    OnPropertyChanged(nameof(EndDate));
                });

                SliderMaximum = ((_maxSliderDate.Year - _minSliderDate.Year) * 12) + _maxSliderDate.Month - _minSliderDate.Month;
                if (SliderMaximum < 0) SliderMaximum = 0;

                UpdateSlidersFromDates();
            }
            else
            {
                SliderMaximum = 100;
                StartMonthSliderValue = 0;
                EndMonthSliderValue = 100;
                UpdateTooltips();
            }
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

            if (StartMonthSliderValue > EndMonthSliderValue)
            {
                double diffStart = Math.Abs(StartMonthSliderValue - (((StartDate.Year - _minSliderDate.Year) * 12) + StartDate.Month - _minSliderDate.Month));
                double diffEnd = Math.Abs(EndMonthSliderValue - (((EndDate.Year - _minSliderDate.Year) * 12) + EndDate.Month - _minSliderDate.Month));
                if (diffStart < diffEnd) StartMonthSliderValue = EndMonthSliderValue;
                else EndMonthSliderValue = StartMonthSliderValue;
            }

            _isUpdatingDates = true;
            var newStartDate = _minSliderDate.AddMonths((int)StartMonthSliderValue);
            var newEndDate = _minSliderDate.AddMonths((int)EndMonthSliderValue);

            _startDate = new DateTime(newStartDate.Year, newStartDate.Month, 1);
            _endDate = new DateTime(newEndDate.Year, newEndDate.Month, DateTime.DaysInMonth(newEndDate.Year, newEndDate.Month));

            OnPropertyChanged(nameof(StartDate));
            OnPropertyChanged(nameof(EndDate));
            UpdateTooltips();
            _isUpdatingDates = false;
            LoadAllChartsData();
            AutoSave();
        }

        private void UpdateTooltips()
        {
            if (IsFilterByDate)
            {
                if (_minSliderDate == default) return;
                var sliderStartDate = _minSliderDate.AddMonths((int)_startMonthSliderValue);
                StartSliderTooltip = sliderStartDate.ToString("MMMM yyyy");
                var sliderEndDate = _minSliderDate.AddMonths((int)_endMonthSliderValue);
                EndSliderTooltip = sliderEndDate.ToString("MMMM yyyy");
            }
            else
            {
                StartSliderTooltip = $"Data: {StartMonthSliderValue:F0}%";
                EndSliderTooltip = $"Data: {EndMonthSliderValue:F0}%";
            }
        }

        private async void LoadAllChartsData(Dictionary<(int, string), string> colorMap = null)
        {
            // Prevent loading if not active or no configurations
            if (_dashboardConfigurations == null || !_isActive) return;

            // Prevent concurrent loads
            if (_isLoading) return;

            _isLoading = true;

            try
            {
                // Clear existing data on UI thread
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        Chart1Series.Clear();
                        Chart2Series.Clear();
                        Chart3Series.Clear();
                        Chart4Series.Clear();
                        Chart5Series.Clear();
                        ClearAndReinitializeAxes();
                    }
                    catch (Exception) { }
                });

                var validConfigs = _dashboardConfigurations
                    .Where(c => c.IsEnabled &&
                               !string.IsNullOrEmpty(c.TableName) &&
                               c.Series.Any(s => !string.IsNullOrEmpty(s.ColumnName)))
                    .ToList();

                // Process charts sequentially to prevent overlap
                foreach (var config in validConfigs)
                {
                    try
                    {
                        await ProcessChartConfiguration(config, colorMap);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning($"Failed to load chart {config.ChartPosition} (Table: {config.TableName}). Error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error in LoadAllChartsData: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
            }
        }

        private async Task ProcessChartConfiguration(DashboardConfiguration config, Dictionary<(int, string), string> colorMap)
        {
            var columnsToFetch = config.Series.Select(s => s.ColumnName).ToList();
            if (!string.IsNullOrEmpty(config.DateColumn)) columnsToFetch.Add(config.DateColumn);
            columnsToFetch = columnsToFetch.Distinct().ToList();

            DateTime? startDate = null;
            DateTime? endDate = null;

            if (IsFilterByDate && config.DataStructureType == "Daily Date")
            {
                startDate = this.StartDate;
                endDate = this.EndDate;
            }

            DataTable dataTable = await _dataRepository.GetDataAsync(config.TableName, columnsToFetch, config.DateColumn, startDate, endDate);

            await Application.Current.Dispatcher.InvokeAsync(() => UpdateChartData(dataTable, config, colorMap));
        }

        private void UpdateChartData(DataTable dataTable, DashboardConfiguration config, Dictionary<(int, string), string> colorMap = null)
        {
            if (dataTable == null) return;

            if (config.RowsToIgnore > 0 && dataTable.Rows.Count > 0)
            {
                int rowsToRemove = Math.Min(config.RowsToIgnore, dataTable.Rows.Count);
                for (int i = 0; i < rowsToRemove; i++)
                {
                    dataTable.Rows.RemoveAt(dataTable.Rows.Count - 1);
                }
            }

            if (IgnoreNonDateData && !string.IsNullOrEmpty(config.DateColumn) && dataTable.Columns.Contains(config.DateColumn))
            {
                for (int i = dataTable.Rows.Count - 1; i >= 0; i--)
                {
                    if (dataTable.Rows[i][config.DateColumn] == DBNull.Value)
                    {
                        dataTable.Rows.RemoveAt(i);
                    }
                }
            }

            double ratioStart = StartMonthSliderValue / SliderMaximum;
            double ratioEnd = EndMonthSliderValue / SliderMaximum;
            bool isDateBasedChart = config.DataStructureType == "Daily Date" && !string.IsNullOrEmpty(config.DateColumn);

            if (!isDateBasedChart || !IsFilterByDate)
            {
                int totalRows = dataTable.Rows.Count;
                if (totalRows > 0)
                {
                    int startIndex = (int)(totalRows * ratioStart);
                    int endIndex = (int)(totalRows * ratioEnd);

                    if (startIndex < 0) startIndex = 0;
                    if (endIndex >= totalRows) endIndex = totalRows - 1;
                    if (startIndex > endIndex) startIndex = endIndex;

                    var rowsToKeep = new List<DataRow>();
                    for (int i = startIndex; i <= endIndex; i++)
                    {
                        rowsToKeep.Add(dataTable.Rows[i]);
                    }

                    DataTable filteredTable = dataTable.Clone();
                    foreach (var row in rowsToKeep) filteredTable.ImportRow(row);
                    dataTable = filteredTable;
                }
            }

            Func<object, double> safeConvertToDouble = (obj) =>
            {
                if (obj == null || obj == DBNull.Value) return 0.0;
                var culture = config.UseInvariantCultureForNumbers ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture;
                double.TryParse(obj.ToString(), NumberStyles.Any, culture, out double result);
                return result;
            };

            var newYAxis = new AxesCollection();
            var newSeries = new SeriesCollection();
            var newXAxis = new AxesCollection();
            var axisForegroundColor = Brushes.WhiteSmoke;

            Brush GetSeriesBrush(string seriesTitle) { if (colorMap != null && colorMap.TryGetValue((config.ChartPosition, seriesTitle), out string hexColor)) { return HexToBrush(hexColor); } return GetNewBrush(); }

            if (string.Equals(config.ChartType, "Pie", StringComparison.OrdinalIgnoreCase))
            {
                newXAxis = null; newYAxis = null;
                foreach (var seriesConfig in config.Series.Where(s => !string.IsNullOrEmpty(s.ColumnName) && dataTable.Columns.Contains(s.ColumnName)))
                {
                    double total = dataTable.AsEnumerable().Sum(row => safeConvertToDouble(row[seriesConfig.ColumnName]));
                    newSeries.Add(new PieSeries { Title = seriesConfig.ColumnName, Values = new ChartValues<double> { total }, DataLabels = true, LabelPoint = chartPoint => string.Format("{0:P0}", chartPoint.Participation), Fill = GetSeriesBrush(seriesConfig.ColumnName) });
                }
            }
            else
            {
                newYAxis.Add(new Axis { Title = "Values", MinValue = 0, Foreground = axisForegroundColor });

                switch (config.DataStructureType)
                {
                    case "Daily Date":
                        ProcessDailyDateChart(dataTable, config, newSeries, newXAxis, GetSeriesBrush, safeConvertToDouble);
                        break;

                    case "Monthly Date":
                        ProcessCategoricalChart(dataTable, config, newSeries, newXAxis, GetSeriesBrush, safeConvertToDouble, SortDataByMonth);
                        break;

                    case "ID":
                    case "General":
                        ProcessCategoricalChart(dataTable, config, newSeries, newXAxis, GetSeriesBrush, safeConvertToDouble);
                        break;
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

        private void ProcessDailyDateChart(DataTable dataTable, DashboardConfiguration config, SeriesCollection newSeries, AxesCollection newXAxis, Func<string, Brush> brushProvider, Func<object, double> numberParser)
        {
            var allDataPoints = dataTable.AsEnumerable().Select(r => new { Date = (DateTime)r[config.DateColumn], Row = r }).OrderBy(p => p.Date).ToList();

            if (config.AggregationType != "Daily")
            {
                IEnumerable<IGrouping<string, dynamic>> aggregatedGroups;
                if (config.AggregationType == "Weekly") { aggregatedGroups = allDataPoints.GroupBy(p => GetStartOfWeek(p.Date, DayOfWeek.Monday).ToString("d")); }
                else { aggregatedGroups = allDataPoints.GroupBy(p => new DateTime(p.Date.Year, p.Date.Month, 1).ToString("MMM yyyy")); }

                var labels = aggregatedGroups.Select(g => g.Key).ToList();
                foreach (var seriesConfig in config.Series.Where(s => !string.IsNullOrEmpty(s.ColumnName) && dataTable.Columns.Contains(s.ColumnName)))
                {
                    var values = new ChartValues<double>(aggregatedGroups.Select(g => g.Sum(item => (double)numberParser(item.Row[seriesConfig.ColumnName]))));

                    if (config.ChartType == "Line")
                        newSeries.Add(new LineSeries { Title = seriesConfig.ColumnName, Values = values, PointGeometry = DefaultGeometries.Circle, Stroke = brushProvider(seriesConfig.ColumnName) });
                    else
                        newSeries.Add(new ColumnSeries { Title = seriesConfig.ColumnName, Values = values, Fill = brushProvider(seriesConfig.ColumnName) });
                }
                newXAxis.Add(new Axis { Labels = labels, Separator = new Separator { IsEnabled = false }, Foreground = Brushes.WhiteSmoke });
            }
            else
            {
                if (config.ChartType == "Line")
                {
                    foreach (var seriesConfig in config.Series.Where(s => !string.IsNullOrEmpty(s.ColumnName) && dataTable.Columns.Contains(s.ColumnName)))
                    {
                        var values = allDataPoints.Select(p => new DateTimePoint(p.Date, numberParser(p.Row[seriesConfig.ColumnName])));
                        newSeries.Add(new LineSeries { Title = seriesConfig.ColumnName, Values = new ChartValues<DateTimePoint>(values), PointGeometry = DefaultGeometries.None, Stroke = brushProvider(seriesConfig.ColumnName) });
                    }
                    newXAxis.Add(new Axis { LabelFormatter = DateFormatter, Separator = new Separator { IsEnabled = false }, Foreground = Brushes.WhiteSmoke });
                }
                else
                {
                    var simplifiedLabels = allDataPoints.Select(p => p.Date.ToString("d")).ToArray();
                    foreach (var seriesConfig in config.Series.Where(s => !string.IsNullOrEmpty(s.ColumnName) && dataTable.Columns.Contains(s.ColumnName)))
                    {
                        var values = allDataPoints.Select(p => numberParser(p.Row[seriesConfig.ColumnName]));
                        newSeries.Add(new ColumnSeries { Title = seriesConfig.ColumnName, Values = new ChartValues<double>(values), Fill = brushProvider(seriesConfig.ColumnName) });
                    }
                    newXAxis.Add(new Axis { Labels = simplifiedLabels, Separator = new Separator { IsEnabled = false }, Foreground = Brushes.WhiteSmoke });
                }
            }
        }

        private void ProcessCategoricalChart(DataTable dataTable, DashboardConfiguration config, SeriesCollection newSeries, AxesCollection newXAxis, Func<string, Brush> brushProvider, Func<object, double> numberParser, Func<IEnumerable<DataRow>, string, IEnumerable<DataRow>> sorter = null)
        {
            IEnumerable<DataRow> dataRows = dataTable.AsEnumerable();

            if (sorter != null)
            {
                dataRows = sorter(dataRows, config.DateColumn);
            }

            var labels = dataRows.Select(r => r[config.DateColumn]?.ToString()).ToArray();

            foreach (var seriesConfig in config.Series.Where(s => !string.IsNullOrEmpty(s.ColumnName) && dataTable.Columns.Contains(s.ColumnName)))
            {
                var values = new ChartValues<double>(dataRows.Select(r => numberParser(r[seriesConfig.ColumnName])));
                if (config.ChartType == "Line")
                {
                    newSeries.Add(new LineSeries { Title = seriesConfig.ColumnName, Values = values, Stroke = brushProvider(seriesConfig.ColumnName), PointGeometry = DefaultGeometries.Circle });
                }
                else
                {
                    newSeries.Add(new ColumnSeries { Title = seriesConfig.ColumnName, Values = values, Fill = brushProvider(seriesConfig.ColumnName) });
                }
            }
            newXAxis.Add(new Axis { Labels = labels, Separator = new Separator { IsEnabled = false }, Foreground = Brushes.WhiteSmoke });
        }

        private IEnumerable<DataRow> SortDataByMonth(IEnumerable<DataRow> data, string monthColumnName)
        {
            var culture = CultureInfo.CurrentCulture;
            var monthNames = culture.DateTimeFormat.MonthNames.Take(12).Select(m => m.ToLower(culture)).ToList();

            return data.OrderBy(row =>
            {
                string monthStr = row[monthColumnName]?.ToString()?.ToLower(culture) ?? "";
                int index = monthNames.IndexOf(monthStr);
                return index == -1 ? 99 : index;
            });
        }

        private DateTime GetStartOfWeek(DateTime dt, DayOfWeek startOfWeek) => dt.AddDays(-1 * ((7 + (dt.DayOfWeek - startOfWeek)) % 7)).Date;

        private Brush GetNewBrush() => new SolidColorBrush(Color.FromRgb((byte)_random.Next(100, 256), (byte)_random.Next(100, 256), (byte)_random.Next(100, 256)));

        private string BrushToHex(Brush brush)
        {
            if (brush is SolidColorBrush scb) { return scb.Color.ToString(); }
            return "#808080";
        }

        private Brush HexToBrush(string hexColor)
        {
            if (string.IsNullOrEmpty(hexColor)) return new SolidColorBrush(Colors.Gray);
            try { return (Brush)new BrushConverter().ConvertFrom(hexColor); }
            catch { return new SolidColorBrush(Colors.Gray); }
        }

        private void RebuildAxesFromConfigs()
        {
            var axisColor = Brushes.WhiteSmoke;
            var configs = _dashboardConfigurations;
            Action<int, AxesCollection, AxesCollection> setupAxes = (pos, xAxis, yAxis) =>
            {
                var config = configs.FirstOrDefault(c => c.ChartPosition == pos && c.IsEnabled);
                if (config == null || config.ChartType == "Pie") return;
                yAxis.Clear(); xAxis.Clear();
                yAxis.Add(new Axis { Title = "Values", MinValue = 0, Foreground = axisColor });
                if (config.DataStructureType == "Daily Date")
                {
                    var seriesCollection = (SeriesCollection)this.GetType().GetProperty($"Chart{pos}Series").GetValue(this);
                    if (seriesCollection.Any() && seriesCollection[0].Values.Count > 0 && seriesCollection[0].Values[0] is DateTimePoint)
                    {
                        xAxis.Add(new Axis { LabelFormatter = DateFormatter, Separator = new Separator { IsEnabled = false }, Foreground = axisColor });
                    }
                    else { xAxis.Add(new Axis { Separator = new Separator { IsEnabled = false }, Foreground = axisColor }); }
                }
                else { xAxis.Add(new Axis { Separator = new Separator { IsEnabled = false }, Foreground = axisColor }); }
            };
            setupAxes(1, Chart1X, Chart1Y);
            setupAxes(2, Chart2X, Chart2Y);
            setupAxes(3, Chart3X, Chart3Y);
            setupAxes(5, Chart5X, Chart5Y);
        }

        private async void ShowConfigurationWindow()
        {
            var configViewModel = new ConfigurationViewModel(_dataRepository);
            await configViewModel.InitializeAsync();
            configViewModel.LoadConfigurations(_dashboardConfigurations);
            if (_dialogService.ShowConfigurationDialog(configViewModel))
            {
                _dashboardConfigurations = configViewModel.GetFinalConfigurations();
                Activate();
                AutoSave();
            }
        }

        private void ImportConfiguration()
        {
            var openFileDialog = new OpenFileDialog { Filter = "JSON Files (*.json)|*.json|All files (*.*)|*.*", Title = "Import Dashboard" };
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string json = File.ReadAllText(openFileDialog.FileName);
                    var snapshot = JsonConvert.DeserializeObject<DashboardSnapshot>(json);
                    if (snapshot.SeriesData != null && snapshot.SeriesData.Any()) LoadDashboardFromSnapshot(snapshot);
                    else { _dashboardConfigurations = snapshot.Configurations; Activate(); AutoSave(); }
                }
                catch (Exception ex) { MessageBox.Show($"Error loading: {ex.Message}"); }
            }
        }

        private void ExportConfiguration()
        {
            if (_dashboardConfigurations == null || !_dashboardConfigurations.Any()) return;
            var snapshot = new DashboardSnapshot { StartDate = this.StartDate, EndDate = this.EndDate, Configurations = _dashboardConfigurations };
            // Capture logic
            Action<SeriesCollection, int> captureSeries = (seriesCollection, position) =>
            {
                foreach (var series in seriesCollection.Cast<Series>())
                {
                    var seriesSnapshot = new ChartSeriesSnapshot { ChartPosition = position, SeriesTitle = series.Title, DataPoints = IsSnapshotExport ? series.Values.Cast<object>().ToList() : new List<object>() };
                    if (series is LineSeries lineSeries) seriesSnapshot.HexColor = BrushToHex(lineSeries.Stroke);
                    else if (series is ColumnSeries columnSeries) seriesSnapshot.HexColor = BrushToHex(columnSeries.Fill);
                    else if (series is PieSeries pieSeries) seriesSnapshot.HexColor = BrushToHex(pieSeries.Fill);
                    snapshot.SeriesData.Add(seriesSnapshot);
                }
            };
            captureSeries(Chart1Series, 1); captureSeries(Chart2Series, 2); captureSeries(Chart3Series, 3); captureSeries(Chart4Series, 4); captureSeries(Chart5Series, 5);

            var saveFileDialog = new SaveFileDialog { Filter = "JSON Files (*.json)|*.json", DefaultExt = ".json" };
            if (saveFileDialog.ShowDialog() == true)
            {
                try { string json = JsonConvert.SerializeObject(snapshot, Formatting.Indented); File.WriteAllText(saveFileDialog.FileName, json); }
                catch (Exception ex) { MessageBox.Show($"Error saving: {ex.Message}"); }
            }
        }

        private void LoadDashboardFromSnapshot(DashboardSnapshot snapshot)
        {
            if (snapshot == null) return;

            // Store the active state before clearing
            bool wasActive = _isActive;

            // Temporarily deactivate to prevent automatic reloads
            _isActive = false;

            try
            {
                // Clear current state
                _dashboardConfigurations = snapshot.Configurations;

                // --- BUG FIX: Force dates to MinValue to ignore JSON saved state and use full data range ---
                _startDate = DateTime.MinValue;
                _endDate = DateTime.MinValue;

                // Clear all chart data
                ClearAndReinitializeAxes();
                Chart1Series.Clear();
                Chart2Series.Clear();
                Chart3Series.Clear();
                Chart4Series.Clear();
                Chart5Series.Clear();

                // Load series from snapshot if available
                if (snapshot.SeriesData != null && snapshot.SeriesData.Any())
                {
                    foreach (var seriesSnapshot in snapshot.SeriesData)
                    {
                        var config = _dashboardConfigurations.FirstOrDefault(c => c.ChartPosition == seriesSnapshot.ChartPosition);
                        if (config == null) continue;

                        Series newSeries = null;
                        var seriesColor = HexToBrush(seriesSnapshot.HexColor);

                        try
                        {
                            if (config.ChartType == "Line" && config.DataStructureType == "Daily Date")
                            {
                                var values = new ChartValues<DateTimePoint>();
                                values.AddRange(seriesSnapshot.DataPoints.Cast<JObject>().Select(jo => jo.ToObject<DateTimePoint>()));
                                newSeries = new LineSeries
                                {
                                    Title = seriesSnapshot.SeriesTitle,
                                    Values = values,
                                    PointGeometry = DefaultGeometries.None,
                                    Stroke = seriesColor
                                };
                            }
                            else
                            {
                                var values = new ChartValues<double>();
                                values.AddRange(seriesSnapshot.DataPoints.Select(p => Convert.ToDouble(p)));

                                if (config.ChartType == "Line")
                                    newSeries = new LineSeries
                                    {
                                        Title = seriesSnapshot.SeriesTitle,
                                        Values = values,
                                        PointGeometry = DefaultGeometries.None,
                                        Stroke = seriesColor
                                    };
                                else if (config.ChartType == "Bar")
                                    newSeries = new ColumnSeries
                                    {
                                        Title = seriesSnapshot.SeriesTitle,
                                        Values = values,
                                        Fill = seriesColor
                                    };
                                else if (config.ChartType == "Pie")
                                    newSeries = new PieSeries
                                    {
                                        Title = seriesSnapshot.SeriesTitle,
                                        Values = values,
                                        DataLabels = true,
                                        LabelPoint = chartPoint => string.Format("{0:P0}", chartPoint.Participation),
                                        Fill = seriesColor
                                    };
                            }
                        }
                        catch { continue; }

                        if (newSeries == null) continue;

                        switch (seriesSnapshot.ChartPosition)
                        {
                            case 1: Chart1Series.Add(newSeries); break;
                            case 2: Chart2Series.Add(newSeries); break;
                            case 3: Chart3Series.Add(newSeries); break;
                            case 4: Chart4Series.Add(newSeries); break;
                            case 5: Chart5Series.Add(newSeries); break;
                        }
                    }
                    RebuildAxesFromConfigs();
                }

                // Only reload data if we were active AND we didn't load snapshot data
                if (wasActive && (snapshot.SeriesData == null || !snapshot.SeriesData.Any()))
                {
                    _isActive = true; // Restore active state
                    UpdateDateFilterState();

                    // Initialize without triggering LoadAllChartsData multiple times
                    _ = InitializeDashboardAsync();
                }
                else if (wasActive)
                {
                    _isActive = true; // Restore active state
                }
            }
            finally
            {
                // Ensure _isActive is restored if something goes wrong
                if (wasActive && !_isActive)
                {
                    _isActive = true;
                }
            }
        }
    }
}