// ViewModels/ChartDetailViewModel.cs
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Wpf;
using WPF_LoginForm.Models;
using WPF_LoginForm.Repositories;
using WPF_LoginForm.Services;

namespace WPF_LoginForm.ViewModels
{
    public class ChartDetailViewModel : ViewModelBase
    {
        private readonly IDataRepository _dataRepository;
        private readonly ILogger _logger;
        private readonly DashboardChartService _chartService;
        private readonly DataTemplate _smartLabelTemplate;
        private DashboardConfiguration _config;

        public Action GoBackAction { get; set; }

        private string _title;
        public string Title { get => _title; set => SetProperty(ref _title, value); }

        private SeriesCollection _chartSeries;
        public SeriesCollection ChartSeries { get => _chartSeries; set => SetProperty(ref _chartSeries, value); }

        private AxesCollection _chartX;
        public AxesCollection ChartX { get => _chartX; set => SetProperty(ref _chartX, value); }

        private AxesCollection _chartY;
        public AxesCollection ChartY { get => _chartY; set => SetProperty(ref _chartY, value); }

        public ICommand GoBackCommand { get; }

        public Func<double, string> DateFormatter { get; }
        public Func<double, string> NumberFormatter { get; }

        public ChartDetailViewModel(IDataRepository repo, ILogger logger)
        {
            _dataRepository = repo;
            _logger = logger;
            _chartService = new DashboardChartService();

            ChartSeries = new SeriesCollection();
            ChartX = new AxesCollection { new Axis() };
            ChartY = new AxesCollection { new Axis() };

            DateFormatter = val =>
            {
                if (double.IsNaN(val) || double.IsInfinity(val)) return "";
                if (val < DateTime.MinValue.Ticks || val > DateTime.MaxValue.Ticks) return "";
                try { return new DateTime((long)val).ToString("d"); } catch { return ""; }
            };

            NumberFormatter = val => DashboardChartService.FormatKiloMega(val);

            try
            {
                if (Application.Current != null)
                {
                    _smartLabelTemplate = Application.Current.TryFindResource("SmartLabelTemplate") as DataTemplate;
                }
            }
            catch (Exception ex) { _logger?.LogError("Failed to initialize SmartLabel Template in Detail View", ex); }

            GoBackCommand = new ViewModelCommand(p => GoBackAction?.Invoke());
        }

        public async void Initialize(DashboardConfiguration config, DateTime startDate, DateTime endDate)
        {
            if (config == null || string.IsNullOrEmpty(config.TableName)) return;

            _config = config;

            // --- CUSTOM TITLE LOGIC APPLIED ---
            Title = $"Detailed Chart Analysis: {config.TableName}";
            if (_config.Series != null && _config.Series.Any())
            {
                var firstSeries = _config.Series.FirstOrDefault();
                if (firstSeries != null && !string.IsNullOrWhiteSpace(firstSeries.CustomDetailTitle))
                {
                    Title = firstSeries.CustomDetailTitle;
                }
            }

            // Clean instantiation to avoid LiveCharts layout crashes
            ChartSeries = new SeriesCollection();

            await LoadDataAsync(startDate, endDate);
        }

        private async Task LoadDataAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                if (_config.Series == null || _config.Series.Count == 0) return;

                var columnsToFetch = new HashSet<string>();
                if (!string.IsNullOrEmpty(_config.DateColumn)) columnsToFetch.Add(_config.DateColumn);
                if (!string.IsNullOrEmpty(_config.SplitByColumn)) columnsToFetch.Add(_config.SplitByColumn);

                foreach (var series in _config.Series)
                {
                    if (series == null) continue;

                    if (series.IsCombinationLabel && series.SavedStates != null && series.ActiveStateIndex >= 0 && series.SavedStates.Count > series.ActiveStateIndex)
                    {
                        var state = series.SavedStates[series.ActiveStateIndex];
                        if (state != null && state.Nodes != null)
                        {
                            foreach (var node in state.Nodes.Where(n => n != null && n.NodeType == "DataColumn"))
                            {
                                if (!string.IsNullOrEmpty(node.Value)) columnsToFetch.Add(node.Value);
                            }
                        }
                    }
                    else if (!string.IsNullOrEmpty(series.ColumnName))
                    {
                        columnsToFetch.Add(series.ColumnName);
                    }
                }

                DateTime? filterStart = (_config.DataStructureType == "Daily Date") ? (DateTime?)startDate : null;
                DateTime? filterEnd = (_config.DataStructureType == "Daily Date") ? (DateTime?)endDate : null;

                var dt = await _dataRepository.GetDataAsync(_config.TableName, columnsToFetch.ToList(), _config.DateColumn, filterStart, filterEnd);

                if (dt == null || dt.Rows.Count == 0) return;

                var chartResult = await Task.Run(() => _chartService.ProcessChartData(
                    dt, _config, true, true, 0, 100, 100, null, false));

                Application.Current.Dispatcher.Invoke(() => ApplyChartResultToUI(chartResult));
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load detail chart data.", ex);
            }
        }

        private void ApplyChartResultToUI(DashboardChartService.ChartResultDto result)
        {
            if (result == null || result.Series == null) return;

            var newSeries = new SeriesCollection();
            var axisColor = Brushes.WhiteSmoke;

            var xyMapper = Mappers.Xy<DashboardDataPoint>()
                .X(p => p != null ? p.X : 0)
                .Y(p => p != null ? p.Y : 0);

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
                    if (s.Points != null)
                    {
                        foreach (var pt in s.Points) cv.Add(pt);
                    }

                    // --- HOVER LOGIC APPLIED ---
                    bool showLabels = cv.Count <= 35 && !s.ShowOnlyHoverLabels;

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
                double minVal = double.MaxValue, maxVal = double.MinValue;
                double minX = double.MaxValue, maxX = double.MinValue;
                int maxPointCount = 0;
                bool hasValues = false;

                foreach (var s in result.Series)
                {
                    if (s.Points != null && s.Points.Any())
                    {
                        // NaN FILTERING TO PREVENT MATH COLLAPSE
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

                    // --- NEW NEGATIVE VALUE ROUNDING FIX ---
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

                    // FAILSAFE: Ensure strictly positive range for LiveCharts step calculator
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

            ChartX = newX;
            ChartY = newY;
            ChartSeries = newSeries;
        }
    }
}