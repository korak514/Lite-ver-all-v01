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

namespace WPF_LoginForm.ViewModels
{
    public class HomeViewModel : ViewModelBase
    {
        private readonly IDataRepository _dataRepository;
        private readonly IDialogService _dialogService;
        private List<DashboardConfiguration> _dashboardConfigurations;
        private bool _isUpdatingDates = false;
        private DateTime _minSliderDate;
        private DateTime _maxSliderDate;
        private bool _isActive = false;

        public ICommand ConfigureCommand { get; }
        public ICommand ImportCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand RecolorChartsCommand { get; }

        public bool IsSnapshotExport { get; set; } = true;

        private bool _isDateFilterEnabled;
        public bool IsDateFilterEnabled { get => _isDateFilterEnabled; private set => SetProperty(ref _isDateFilterEnabled, value); }

        private DateTime _startDate;

        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                if (_startDate == value) return;
                _startDate = value;
                OnPropertyChanged();
                if (!_isUpdatingDates && _isActive)
                {
                    LoadAllChartsData();
                    UpdateSlidersFromDates();
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
                if (!_isUpdatingDates && _isActive)
                {
                    LoadAllChartsData();
                    UpdateSlidersFromDates();
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
                if (SetProperty(ref _startMonthSliderValue, value) && !_isUpdatingDates && _isActive)
                {
                    UpdateDatesFromSliders();
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
                    UpdateDatesFromSliders();
                }
            }
        }

        private string _startSliderTooltip;
        public string StartSliderTooltip { get => _startSliderTooltip; set => SetProperty(ref _startSliderTooltip, value); }

        private string _endSliderTooltip;
        public string EndSliderTooltip { get => _endSliderTooltip; set => SetProperty(ref _endSliderTooltip, value); }

        // Initialize collections in constructor
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

        public HomeViewModel(IDataRepository dataRepository, IDialogService dialogService)
        {
            _dataRepository = dataRepository ?? throw new ArgumentNullException(nameof(dataRepository));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _dashboardConfigurations = new List<DashboardConfiguration>();

            ConfigureCommand = new ViewModelCommand(p => ShowConfigurationWindow());
            ImportCommand = new ViewModelCommand(p => ImportConfiguration());
            ExportCommand = new ViewModelCommand(p => ExportConfiguration());
            RecolorChartsCommand = new ViewModelCommand(p => LoadAllChartsData());

            _startDate = DateTime.Now.AddMonths(-1);
            _endDate = DateTime.Now;

            DateFormatter = value =>
            {
                if (value < DateTime.MinValue.Ticks || value > DateTime.MaxValue.Ticks) return "";
                return new DateTime((long)value).ToString("d");
            };

            // Initialize with empty axes to prevent NRE
            InitializeEmptyAxes();
        }

        private void InitializeEmptyAxes()
        {
            // Create a minimal, hidden axis for empty charts
            var hiddenAxis = new Axis
            {
                ShowLabels = false,
                Separator = new Separator { IsEnabled = false },
                Foreground = Brushes.Transparent,
                Visibility = Visibility.Collapsed
            };

            // Add to all axis collections
            Chart1X.Add(hiddenAxis);
            Chart1Y.Add(hiddenAxis);
            Chart2X.Add(hiddenAxis);
            Chart2Y.Add(hiddenAxis);
            Chart3X.Add(hiddenAxis);
            Chart3Y.Add(hiddenAxis);
            Chart5X.Add(hiddenAxis);
            Chart5Y.Add(hiddenAxis);
        }

        public async void Activate()
        {
            _isActive = true;
            UpdateDateFilterState();
            await FindGlobalDateRangeAsync();
            InitializeSliders();
            LoadAllChartsData();
        }

        public void Deactivate()
        {
            _isActive = false;
            // Don't clear collections - just mark as inactive
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
                var (min, max) = await _dataRepository.GetDateRangeAsync(config.TableName, config.DateColumn);
                if (min != DateTime.MinValue && min < overallMin) overallMin = min;
                if (max != DateTime.MinValue && max > overallMax) overallMax = max;
            }

            _minSliderDate = (overallMin == DateTime.MaxValue) ? DateTime.Now.AddYears(-1) : overallMin;
            _maxSliderDate = (overallMax == DateTime.MinValue) ? DateTime.Now : overallMax;
        }

        private void InitializeSliders()
        {
            if (_minSliderDate == default || _maxSliderDate == default) return;

            _startDate = _minSliderDate;
            _endDate = _maxSliderDate;
            OnPropertyChanged(nameof(StartDate));
            OnPropertyChanged(nameof(EndDate));

            SliderMaximum = ((_maxSliderDate.Year - _minSliderDate.Year) * 12) + _maxSliderDate.Month - _minSliderDate.Month;
            UpdateSlidersFromDates();
        }

        private void UpdateSlidersFromDates()
        {
            if (_isUpdatingDates || _minSliderDate == default) return;

            _isUpdatingDates = true;
            StartMonthSliderValue = ((StartDate.Year - _minSliderDate.Year) * 12) + StartDate.Month - _minSliderDate.Month;
            EndMonthSliderValue = ((EndDate.Year - _minSliderDate.Year) * 12) + EndDate.Month - _minSliderDate.Month;
            UpdateTooltips();
            _isUpdatingDates = false;
        }

        private void UpdateDatesFromSliders()
        {
            if (_isUpdatingDates || _minSliderDate == default) return;

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
        }

        private void UpdateTooltips()
        {
            if (_minSliderDate == default) return;

            var sliderStartDate = _minSliderDate.AddMonths((int)_startMonthSliderValue);
            StartSliderTooltip = sliderStartDate.ToString("MMMM yyyy");
            var sliderEndDate = _minSliderDate.AddMonths((int)_endMonthSliderValue);
            EndSliderTooltip = sliderEndDate.ToString("MMMM yyyy");
        }

        private async void LoadAllChartsData(Dictionary<(int, string), string> colorMap = null)
        {
            if (_dashboardConfigurations == null || !_isActive) return;

            // Clear all series
            Chart1Series.Clear();
            Chart2Series.Clear();
            Chart3Series.Clear();
            Chart4Series.Clear();
            Chart5Series.Clear();

            // Clear axes and reinitialize empty ones
            ClearAndReinitializeAxes();

            var validConfigs = _dashboardConfigurations.Where(c => c.IsEnabled && !string.IsNullOrEmpty(c.TableName) && !string.IsNullOrEmpty(c.DateColumn) && c.Series.Any(s => !string.IsNullOrEmpty(s.ColumnName)));

            foreach (var config in validConfigs)
            {
                await ProcessChartConfiguration(config, colorMap);
            }
        }

        private void ClearAndReinitializeAxes()
        {
            // Clear all axes
            Chart1X.Clear();
            Chart1Y.Clear();
            Chart2X.Clear();
            Chart2Y.Clear();
            Chart3X.Clear();
            Chart3Y.Clear();
            Chart5X.Clear();
            Chart5Y.Clear();

            // Reinitialize with empty hidden axes
            var hiddenAxis = new Axis
            {
                ShowLabels = false,
                Separator = new Separator { IsEnabled = false },
                Foreground = Brushes.Transparent,
                Visibility = Visibility.Collapsed
            };

            Chart1X.Add(hiddenAxis);
            Chart1Y.Add(hiddenAxis);
            Chart2X.Add(hiddenAxis);
            Chart2Y.Add(hiddenAxis);
            Chart3X.Add(hiddenAxis);
            Chart3Y.Add(hiddenAxis);
            Chart5X.Add(hiddenAxis);
            Chart5Y.Add(hiddenAxis);
        }

        private async Task ProcessChartConfiguration(DashboardConfiguration config, Dictionary<(int, string), string> colorMap)
        {
            var columnsToFetch = config.Series.Select(s => s.ColumnName).ToList();
            columnsToFetch.Add(config.DateColumn);
            columnsToFetch = columnsToFetch.Distinct().ToList();

            DateTime? startDate = (config.DataStructureType == "Daily Date") ? this.StartDate : (DateTime?)null;
            DateTime? endDate = (config.DataStructureType == "Daily Date") ? this.EndDate : (DateTime?)null;

            DataTable dataTable = await _dataRepository.GetDataAsync(config.TableName, columnsToFetch, config.DateColumn, startDate, endDate);

            UpdateChartData(dataTable, config, colorMap);
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

            // Assign to Properties
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

        private readonly Random _random = new Random();

        private Brush GetNewBrush() => new SolidColorBrush(Color.FromRgb((byte)_random.Next(100, 256), (byte)_random.Next(100, 256), (byte)_random.Next(100, 256)));

        private DateTime GetStartOfWeek(DateTime dt, DayOfWeek startOfWeek) => dt.AddDays(-1 * ((7 + (dt.DayOfWeek - startOfWeek)) % 7)).Date;

        private void ImportConfiguration()
        { var openFileDialog = new OpenFileDialog { Filter = "JSON Files (*.json)|*.json|All files (*.*)|*.*", Title = "Import Dashboard" }; if (openFileDialog.ShowDialog() == true) { try { string json = File.ReadAllText(openFileDialog.FileName); var snapshot = JsonConvert.DeserializeObject<DashboardSnapshot>(json); if (snapshot.SeriesData != null && snapshot.SeriesData.Any(s => s.DataPoints != null && s.DataPoints.Any())) { LoadDashboardFromSnapshot(snapshot); } else { _dashboardConfigurations = snapshot.Configurations; Activate(); } } catch (Exception ex) { MessageBox.Show($"Error loading dashboard file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); } } }

        private void ExportConfiguration()
        { if (_dashboardConfigurations == null || !_dashboardConfigurations.Any()) { MessageBox.Show("No configuration to export", "Information", MessageBoxButton.OK, MessageBoxImage.Information); return; } var snapshot = new DashboardSnapshot { StartDate = this.StartDate, EndDate = this.EndDate, Configurations = _dashboardConfigurations }; Action<SeriesCollection, int> captureSeries = (seriesCollection, position) => { foreach (var series in seriesCollection.Cast<Series>()) { var seriesSnapshot = new ChartSeriesSnapshot { ChartPosition = position, SeriesTitle = series.Title, DataPoints = IsSnapshotExport ? series.Values.Cast<object>().ToList() : new List<object>() }; if (series is LineSeries lineSeries) seriesSnapshot.HexColor = BrushToHex(lineSeries.Stroke); else if (series is ColumnSeries columnSeries) seriesSnapshot.HexColor = BrushToHex(columnSeries.Fill); else if (series is PieSeries pieSeries) seriesSnapshot.HexColor = BrushToHex(pieSeries.Fill); snapshot.SeriesData.Add(seriesSnapshot); } }; captureSeries(Chart1Series, 1); captureSeries(Chart2Series, 2); captureSeries(Chart3Series, 3); captureSeries(Chart4Series, 4); captureSeries(Chart5Series, 5); var saveFileDialog = new SaveFileDialog { Filter = "JSON Files (*.json)|*.json|All files (*.*)|*.*", DefaultExt = ".json", Title = "Export Dashboard" }; if (saveFileDialog.ShowDialog() == true) { try { string json = JsonConvert.SerializeObject(snapshot, Formatting.Indented); File.WriteAllText(saveFileDialog.FileName, json); } catch (Exception ex) { MessageBox.Show($"Error saving dashboard file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); } } }

        private void LoadDashboardFromSnapshot(DashboardSnapshot snapshot)
        { if (snapshot == null) return; _dashboardConfigurations = snapshot.Configurations; Activate(); ClearAndReinitializeAxes(); foreach (var seriesSnapshot in snapshot.SeriesData) { var config = _dashboardConfigurations.FirstOrDefault(c => c.ChartPosition == seriesSnapshot.ChartPosition); if (config == null) continue; Series newSeries = null; var seriesColor = HexToBrush(seriesSnapshot.HexColor); try { if (config.ChartType == "Line" && config.DataStructureType == "Daily Date") { var values = new ChartValues<DateTimePoint>(); values.AddRange(seriesSnapshot.DataPoints.Cast<JObject>().Select(jo => jo.ToObject<DateTimePoint>())); newSeries = new LineSeries { Title = seriesSnapshot.SeriesTitle, Values = values, PointGeometry = DefaultGeometries.None, Stroke = seriesColor }; } else { var values = new ChartValues<double>(); values.AddRange(seriesSnapshot.DataPoints.Select(p => Convert.ToDouble(p))); if (config.ChartType == "Line") { newSeries = new LineSeries { Title = seriesSnapshot.SeriesTitle, Values = values, PointGeometry = DefaultGeometries.None, Stroke = seriesColor }; } else if (config.ChartType == "Bar") { newSeries = new ColumnSeries { Title = seriesSnapshot.SeriesTitle, Values = values, Fill = seriesColor }; } else if (config.ChartType == "Pie") { newSeries = new PieSeries { Title = seriesSnapshot.SeriesTitle, Values = values, DataLabels = true, LabelPoint = chartPoint => string.Format("{0:P0}", chartPoint.Participation), Fill = seriesColor }; } } } catch (Exception ex) { MessageBox.Show($"Could not load data for series '{seriesSnapshot.SeriesTitle}': {ex.Message}", "Import Warning", MessageBoxButton.OK, MessageBoxImage.Warning); continue; } if (newSeries == null) continue; switch (seriesSnapshot.ChartPosition) { case 1: Chart1Series.Add(newSeries); break; case 2: Chart2Series.Add(newSeries); break; case 3: Chart3Series.Add(newSeries); break; case 4: Chart4Series.Add(newSeries); break; case 5: Chart5Series.Add(newSeries); break; } } RebuildAxesFromConfigs(); }

        private void RebuildAxesFromConfigs()
        { var axisColor = Brushes.WhiteSmoke; var configs = _dashboardConfigurations; Action<int, AxesCollection, AxesCollection> setupAxes = (pos, xAxis, yAxis) => { var config = configs.FirstOrDefault(c => c.ChartPosition == pos && c.IsEnabled); if (config == null || config.ChartType == "Pie") return; yAxis.Clear(); xAxis.Clear(); yAxis.Add(new Axis { Title = "Values", MinValue = 0, Foreground = axisColor }); if (config.DataStructureType == "Daily Date") { var seriesCollection = (SeriesCollection)this.GetType().GetProperty($"Chart{pos}Series").GetValue(this); if (seriesCollection.Any() && seriesCollection[0].Values.Count > 0 && seriesCollection[0].Values[0] is DateTimePoint) { xAxis.Add(new Axis { LabelFormatter = DateFormatter, Separator = new Separator { IsEnabled = false }, Foreground = axisColor }); } else { xAxis.Add(new Axis { Separator = new Separator { IsEnabled = false }, Foreground = axisColor }); } } else { xAxis.Add(new Axis { Separator = new Separator { IsEnabled = false }, Foreground = axisColor }); } }; setupAxes(1, Chart1X, Chart1Y); setupAxes(2, Chart2X, Chart2Y); setupAxes(3, Chart3X, Chart3Y); setupAxes(5, Chart5X, Chart5Y); }

        private string BrushToHex(Brush brush)
        { if (brush is SolidColorBrush scb) { return scb.Color.ToString(); } return "#808080"; }

        private Brush HexToBrush(string hexColor)
        { if (string.IsNullOrEmpty(hexColor)) return new SolidColorBrush(Colors.Gray); try { return (Brush)new BrushConverter().ConvertFrom(hexColor); } catch { return new SolidColorBrush(Colors.Gray); } }

        private async void ShowConfigurationWindow()
        { var configViewModel = new ConfigurationViewModel(_dataRepository); await configViewModel.InitializeAsync(); configViewModel.LoadConfigurations(_dashboardConfigurations); if (_dialogService.ShowConfigurationDialog(configViewModel)) { _dashboardConfigurations = configViewModel.GetFinalConfigurations(); Activate(); } }
    }
}