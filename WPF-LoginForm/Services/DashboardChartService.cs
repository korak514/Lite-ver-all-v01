using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using LiveCharts.Defaults;
using WPF_LoginForm.Models;   // Use the existing DashboardDataPoint from Models

namespace WPF_LoginForm.Services
{
    public class DashboardChartService
    {
        private readonly Random _random = new Random();
        private readonly object _randomLock = new object();

        public class ChartResultDto
        {
            public List<SeriesDto> Series { get; set; } = new List<SeriesDto>();
            public List<string> XAxisLabels { get; set; }
            public bool IsDateAxis { get; set; }
        }

        public class SeriesDto
        {
            public string Title { get; set; }
            public string FullName { get; set; }          // Restored – stores original (uncleaned) name for tooltips
            public string SeriesType { get; set; }
            public List<object> Values { get; set; }      // Kept for backward compatibility with existing chart rendering
            public List<DashboardDataPoint> Points { get; set; } = new List<DashboardDataPoint>(); // New – for smart labels
            public List<double> PieValues { get; set; } = new List<double>(); // For pie charts
            public string ColorHex { get; set; }
        }

        public static string FormatKiloMega(double value)
        {
            double abs = Math.Abs(value);
            if (abs >= 1_000_000) return (value / 1_000_000D).ToString("0.##") + "M";
            if (abs >= 10_000) return (value / 1_000D).ToString("0.##") + "K";
            if (value % 1 == 0) return value.ToString("N0");
            return value.ToString("N2");
        }

        public ChartResultDto ProcessChartData(DataTable dataTable, DashboardConfiguration config,
                                               bool isFilterByDate, bool ignoreNonDateData,
                                               double sliderStart, double sliderEnd, double sliderMax,
                                               Dictionary<(int, string), string> colorMap,
                                               bool globalIgnoreAfterHyphen)
        {
            var result = new ChartResultDto();
            if (dataTable == null) return result;

            if (config.RowsToIgnore > 0 && dataTable.Rows.Count > 0)
            {
                int rowsToRemove = Math.Min(config.RowsToIgnore, dataTable.Rows.Count);
                for (int i = 0; i < rowsToRemove; i++) dataTable.Rows.RemoveAt(dataTable.Rows.Count - 1);
            }

            if (ignoreNonDateData && !string.IsNullOrEmpty(config.DateColumn) && dataTable.Columns.Contains(config.DateColumn))
            {
                for (int i = dataTable.Rows.Count - 1; i >= 0; i--)
                {
                    if (dataTable.Rows[i][config.DateColumn] == DBNull.Value) dataTable.Rows.RemoveAt(i);
                }
            }

            if (config.DataStructureType == "Monthly Date" && dataTable.Rows.Count > 100)
            {
                config.DataStructureType = "General";
            }

            bool isDateBasedChart = config.DataStructureType == "Daily Date" && !string.IsNullOrEmpty(config.DateColumn);
            if (!isDateBasedChart || !isFilterByDate)
            {
                ApplySliderFilter(ref dataTable, sliderStart, sliderEnd, sliderMax);
            }

            Func<object, double> safeConvertToDouble = (obj) =>
            {
                if (obj == null || obj == DBNull.Value) return 0.0;
                string strVal = obj.ToString().Replace("₺", "").Replace("TL", "").Replace("%", "").Trim();

                if (string.IsNullOrWhiteSpace(strVal)) return 0.0;

                int lastComma = strVal.LastIndexOf(',');
                int lastDot = strVal.LastIndexOf('.');

                if (lastComma > lastDot)
                {
                    strVal = strVal.Replace(".", "").Replace(",", ".");
                }
                else if (lastDot > lastComma && lastComma != -1)
                {
                    strVal = strVal.Replace(",", "");
                }
                else if (lastComma != -1 && lastDot == -1)
                {
                    strVal = strVal.Replace(",", ".");
                }

                double.TryParse(strVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double res);
                return res;
            };

            string GetColor(string title)
            {
                if (colorMap != null && colorMap.TryGetValue((config.ChartPosition, title), out string hex)) return hex;
                lock (_randomLock)
                {
                    var color = Color.FromRgb((byte)_random.Next(50, 240), (byte)_random.Next(50, 240), (byte)_random.Next(50, 240));
                    return color.ToString();
                }
            }

            bool isAvg = string.Equals(config.ValueAggregation, "Average", StringComparison.OrdinalIgnoreCase);

            if (string.Equals(config.ChartType, "Pie", StringComparison.OrdinalIgnoreCase))
            {
                ProcessPieChart(dataTable, config, result, GetColor, safeConvertToDouble, globalIgnoreAfterHyphen, isAvg);
            }
            else
            {
                switch (config.DataStructureType)
                {
                    case "Daily Date":
                        ProcessDailyDateChart(dataTable, config, result, GetColor, safeConvertToDouble, globalIgnoreAfterHyphen, isAvg);
                        break;

                    case "Monthly Date":
                    case "ID":
                    case "General":
                    default:
                        ProcessPivotCategoricalChart(dataTable, config, result, GetColor, safeConvertToDouble, globalIgnoreAfterHyphen, isAvg);
                        break;
                }
            }

            return result;
        }

        // ==================== New Smart Label Logic ====================
        private void ApplySmartLabels(List<DashboardDataPoint> points, bool isDateAxis, List<string> xAxisLabels, string seriesType)
        {
            if (points.Count == 0) return;

            // 1. Assign formatted text
            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                string vStr = FormatKiloMega(p.Y);
                string dStr = isDateAxis
                    ? new DateTime((long)p.X).ToString("MMM yyyy")
                    : (xAxisLabels != null && p.X >= 0 && p.X < xAxisLabels.Count ? xAxisLabels[(int)p.X] : "");
                p.Label = $"{vStr}\n{dStr}";
                p.ShowLabel = false;
            }

            // 2. Volatility filtering
            int maxLabels = 12;
            int minIndexDist = Math.Max(1, points.Count / maxLabels);
            var importantIndices = new HashSet<int>();

            if (points.Count <= maxLabels)
            {
                for (int i = 0; i < points.Count; i++)
                {
                    importantIndices.Add(i);
                    points[i].IsImportant = true;
                }
            }
            else
            {
                double avgY = points.Average(p => p.Y);
                var scoredPoints = points.Select((p, i) =>
                {
                    double score = 0;
                    if (i == 0 || i == points.Count - 1)
                        score = double.MaxValue;
                    else
                    {
                        double diffPrev = Math.Abs(points[i].Y - points[i - 1].Y);
                        double diffNext = Math.Abs(points[i].Y - points[i + 1].Y);
                        double diffAvg = Math.Abs(points[i].Y - avgY);
                        score = (diffPrev + diffNext) * 2.0 + diffAvg;
                    }
                    return new { Index = i, Score = score };
                }).ToList();

                foreach (var item in scoredPoints.OrderByDescending(x => x.Score))
                {
                    if (importantIndices.Count >= maxLabels) break;
                    if (importantIndices.Contains(item.Index)) continue;

                    bool tooClose = false;
                    foreach (var idx in importantIndices)
                    {
                        if (Math.Abs(idx - item.Index) < minIndexDist)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    if (!tooClose || item.Index == 0 || item.Index == points.Count - 1)
                    {
                        importantIndices.Add(item.Index);
                        points[item.Index].IsImportant = true;
                    }
                }
            }

            // 3. Compute Y range
            double minY = points.Min(p => p.Y);
            double maxY = points.Max(p => p.Y);
            double range = maxY - minY;
            const double offsetPixels = 30;

            // 4. Set margins only for important points
            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                if (!importantIndices.Contains(i))
                {
                    p.ShowLabel = false;
                    continue;
                }

                p.ShowLabel = true;

                if (seriesType != "Line")
                {
                    p.LabelMargin = new Thickness(0, -offsetPixels, 0, 0);
                    continue;
                }

                double prevY = i > 0 ? points[i - 1].Y : p.Y;
                double nextY = i < points.Count - 1 ? points[i + 1].Y : p.Y;

                bool isPeak = p.Y >= prevY && p.Y >= nextY && !(p.Y == prevY && p.Y == nextY);
                bool isValley = p.Y <= prevY && p.Y <= nextY && !(p.Y == prevY && p.Y == nextY);

                if (isPeak)
                {
                    p.LabelMargin = new Thickness(0, -offsetPixels, 0, 0);
                }
                else if (isValley)
                {
                    p.LabelMargin = new Thickness(0, offsetPixels, 0, 0);
                }
                else
                {
                    bool isInTopHalf = (p.Y - minY) > (range / 2);
                    if (isInTopHalf)
                        p.LabelMargin = new Thickness(0, offsetPixels, 0, 0);
                    else
                        p.LabelMargin = new Thickness(0, -offsetPixels, 0, 0);
                }
            }
        }

        // ==================== Original Chart Processing (adapted to fill Points) ====================

        private void ProcessPieChart(DataTable dataTable, DashboardConfiguration config, ChartResultDto result,
            Func<string, string> colorProvider, Func<object, double> numberParser, bool globalIgnoreAfterHyphen, bool isAvg)
        {
            string groupByCol = !string.IsNullOrEmpty(config.SplitByColumn) ? config.SplitByColumn : config.DateColumn;

            if (!string.IsNullOrEmpty(groupByCol) && dataTable.Columns.Contains(groupByCol) && config.Series.Any())
            {
                var valueCol = config.Series.FirstOrDefault(s => dataTable.Columns.Contains(s.ColumnName))?.ColumnName;
                if (valueCol != null)
                {
                    bool hasSplit = !string.IsNullOrEmpty(config.SplitByColumn);
                    List<string> allowedCategories = null;

                    if (hasSplit && config.SelectedSplitCategories != null && config.SelectedSplitCategories.Any())
                        allowedCategories = config.SelectedSplitCategories;

                    var grouped = dataTable.AsEnumerable()
                        .Select(r => new
                        {
                            RawCategory = FormatCategoryKey(r[groupByCol]),
                            Value = numberParser(r[valueCol])
                        })
                        .Where(x => allowedCategories == null || (!allowedCategories.Contains("__NONE__") && allowedCategories.Contains(x.RawCategory)))
                        .Select(x => new
                        {
                            RawCategory = x.RawCategory,
                            CleanCategory = CleanLabelString(x.RawCategory, config.HideNumbersInLabels, config.SimplifyLabels, globalIgnoreAfterHyphen),
                            Value = x.Value
                        })
                        .GroupBy(x => x.CleanCategory)
                        .Select(g => new
                        {
                            Category = g.Key,
                            OriginalNames = g.Select(x => x.RawCategory).Distinct().ToList(),
                            Total = isAvg ? g.Average(x => x.Value) : g.Sum(x => x.Value)
                        })
                        .OrderByDescending(x => x.Total)
                        .ToList();

                    int maxSlices = 8;
                    var topItems = grouped.Take(maxSlices).ToList();
                    var othersSum = grouped.Skip(maxSlices).Sum(x => x.Total);

                    foreach (var g in topItems)
                    {
                        string tooltipName = g.OriginalNames.Count > 1
                            ? $"{g.Category} (Grouped)"
                            : g.OriginalNames.FirstOrDefault() ?? g.Category;

                        result.Series.Add(new SeriesDto
                        {
                            Title = g.Category,
                            FullName = tooltipName,
                            SeriesType = "Pie",
                            Values = new List<object> { g.Total },
                            PieValues = new List<double> { g.Total },
                            ColorHex = colorProvider(g.Category)
                        });
                    }

                    if (othersSum > 0)
                    {
                        result.Series.Add(new SeriesDto
                        {
                            Title = "Others",
                            FullName = "Others (Minor Values)",
                            SeriesType = "Pie",
                            Values = new List<object> { othersSum },
                            PieValues = new List<double> { othersSum },
                            ColorHex = "#808080"
                        });
                    }
                }
            }
            else
            {
                foreach (var seriesConfig in config.Series.Where(s => !string.IsNullOrEmpty(s.ColumnName) && dataTable.Columns.Contains(s.ColumnName)))
                {
                    var validRows = dataTable.AsEnumerable().Where(r => r[seriesConfig.ColumnName] != DBNull.Value);
                    double total = 0;
                    if (validRows.Any())
                    {
                        total = isAvg ? validRows.Average(row => numberParser(row[seriesConfig.ColumnName]))
                                      : validRows.Sum(row => numberParser(row[seriesConfig.ColumnName]));
                    }

                    string cleanName = CleanLabelString(seriesConfig.ColumnName, config.HideNumbersInLabels, config.SimplifyLabels, globalIgnoreAfterHyphen);
                    result.Series.Add(new SeriesDto
                    {
                        Title = cleanName,
                        FullName = seriesConfig.ColumnName,
                        SeriesType = "Pie",
                        Values = new List<object> { total },
                        PieValues = new List<double> { total },
                        ColorHex = colorProvider(cleanName)
                    });
                }
            }
        }

        private void ProcessDailyDateChart(DataTable dt, DashboardConfiguration config, ChartResultDto res,
            Func<string, string> colorProvider, Func<object, double> numberParser, bool globalIgnoreHyphen, bool isAvg)
        {
            if (!dt.Columns.Contains(config.DateColumn)) return;

            var points = dt.AsEnumerable()
                .Select(r => new { Date = ParseDateSafe(r[config.DateColumn]), Row = r })
                .Where(p => p.Date != DateTime.MinValue)
                .ToList();

            IEnumerable<IGrouping<DateTime, dynamic>> groups;
            if (config.AggregationType == "Weekly")
                groups = points.GroupBy(p => GetStartOfWeek(p.Date));
            else if (config.AggregationType == "Monthly")
                groups = points.GroupBy(p => new DateTime(p.Date.Year, p.Date.Month, 1));
            else
                groups = points.GroupBy(p => p.Date.Date);

            var sortedGroups = groups.OrderBy(g => g.Key).ToList();

            bool hasSplit = !string.IsNullOrEmpty(config.SplitByColumn) && dt.Columns.Contains(config.SplitByColumn);
            List<string> splitCategories = hasSplit
                ? dt.AsEnumerable().Select(r => FormatCategoryKey(r[config.SplitByColumn])).Distinct().OrderBy(x => x).ToList()
                : new List<string> { "Default" };

            if (hasSplit && config.SelectedSplitCategories != null && config.SelectedSplitCategories.Any(c => c != "__NONE__"))
            {
                splitCategories = splitCategories.Where(c => config.SelectedSplitCategories.Contains(c)).ToList();
            }
            else if (hasSplit && config.SelectedSplitCategories != null && config.SelectedSplitCategories.Contains("__NONE__"))
            {
                splitCategories.Clear();
            }

            var cleanSplitGroups = splitCategories
                .GroupBy(c => CleanLabelString(c, config.HideNumbersInLabels, config.SimplifyLabels, globalIgnoreHyphen))
                .ToList();

            bool isPriceTrend = string.Equals(config.ChartType, "Price Trend (Line)", StringComparison.OrdinalIgnoreCase);
            bool isLine = isPriceTrend || config.ChartType == "Line";
            bool isDailyLine = isLine && config.AggregationType == "Daily";

            if (isDailyLine)
            {
                res.IsDateAxis = true;

                if (isPriceTrend && config.Series.Count >= 2)
                {
                    string numCol = config.Series[0].ColumnName;
                    string denCol = config.Series[1].ColumnName;

                    foreach (var splitGroup in cleanSplitGroups)
                    {
                        var rawValsInGroup = splitGroup.ToList();
                        string cleanTitle = hasSplit ? splitGroup.Key : "Efficiency Trend";

                        var vals = new List<object>();
                        var pts = new List<DashboardDataPoint>();

                        foreach (var g in sortedGroups)
                        {
                            var filteredRows = hasSplit ? g.Where(x => rawValsInGroup.Contains(FormatCategoryKey(x.Row[config.SplitByColumn]))) : g;
                            if (!filteredRows.Any())
                            {
                                vals.Add(new DateTimePoint(g.Key, double.NaN));
                                pts.Add(new DashboardDataPoint { X = g.Key.Ticks, Y = double.NaN });
                                continue;
                            }

                            double numSum = filteredRows.Sum(x => (double)numberParser(x.Row[numCol]));
                            double denSum = filteredRows.Sum(x => (double)numberParser(x.Row[denCol]));
                            double avg = denSum != 0 ? numSum / denSum : double.NaN;

                            vals.Add(new DateTimePoint(g.Key, avg));
                            pts.Add(new DashboardDataPoint { X = g.Key.Ticks, Y = avg });
                        }

                        if (!hasSplit || vals.Any(v => !double.IsNaN(((DateTimePoint)v).Value) && ((DateTimePoint)v).Value > 0))
                        {
                            ApplySmartLabels(pts, true, null, "Line");
                            res.Series.Add(new SeriesDto
                            {
                                Title = cleanTitle,
                                FullName = cleanTitle,
                                SeriesType = "Line",
                                Values = vals,
                                Points = pts,
                                ColorHex = colorProvider(cleanTitle)
                            });
                        }
                    }
                }
                else if (!isPriceTrend)
                {
                    foreach (var ser in config.Series.Where(s => !string.IsNullOrEmpty(s.ColumnName) && dt.Columns.Contains(s.ColumnName)))
                    {
                        foreach (var splitGroup in cleanSplitGroups)
                        {
                            var rawValsInGroup = splitGroup.ToList();
                            string cleanTitle = hasSplit ? (config.Series.Count > 1 ? $"{splitGroup.Key} - {ser.ColumnName}" : splitGroup.Key) : ser.ColumnName;

                            var vals = new List<object>();
                            var pts = new List<DashboardDataPoint>();

                            foreach (var g in sortedGroups)
                            {
                                var filteredRows = hasSplit ? g.Where(x => rawValsInGroup.Contains(FormatCategoryKey(x.Row[config.SplitByColumn]))) : g;
                                if (!filteredRows.Any())
                                {
                                    vals.Add(new DateTimePoint(g.Key, 0));
                                    pts.Add(new DashboardDataPoint { X = g.Key.Ticks, Y = 0 });
                                    continue;
                                }

                                double aggVal = isAvg
                                    ? filteredRows.Average(x => (double)numberParser(x.Row[ser.ColumnName]))
                                    : filteredRows.Sum(x => (double)numberParser(x.Row[ser.ColumnName]));

                                vals.Add(new DateTimePoint(g.Key, aggVal));
                                pts.Add(new DashboardDataPoint { X = g.Key.Ticks, Y = aggVal });
                            }

                            if (!hasSplit || vals.Any(v => ((DateTimePoint)v).Value > 0))
                            {
                                ApplySmartLabels(pts, true, null, "Line");
                                res.Series.Add(new SeriesDto
                                {
                                    Title = cleanTitle,
                                    FullName = cleanTitle,
                                    SeriesType = "Line",
                                    Values = vals,
                                    Points = pts,
                                    ColorHex = colorProvider(cleanTitle)
                                });
                            }
                        }
                    }
                }
            }
            else
            {
                res.IsDateAxis = false;
                res.XAxisLabels = sortedGroups.Select(g =>
                {
                    if (config.AggregationType == "Monthly") return g.Key.ToString("MMM yyyy");
                    if (config.AggregationType == "Weekly") return g.Key.ToString("dd.MM.yyyy") + " (Wk)";
                    return g.Key.ToString("dd.MM.yyyy");
                }).ToList();

                string sType = isLine ? "Line" : "Bar";

                if (isPriceTrend && config.Series.Count >= 2)
                {
                    string numCol = config.Series[0].ColumnName;
                    string denCol = config.Series[1].ColumnName;

                    foreach (var splitGroup in cleanSplitGroups)
                    {
                        var rawValsInGroup = splitGroup.ToList();
                        string cleanTitle = hasSplit ? splitGroup.Key : "Efficiency Trend";

                        var vals = new List<object>();
                        var pts = new List<DashboardDataPoint>();
                        int idx = 0;

                        foreach (var g in sortedGroups)
                        {
                            var filteredRows = hasSplit ? g.Where(x => rawValsInGroup.Contains(FormatCategoryKey(x.Row[config.SplitByColumn]))) : g;
                            if (!filteredRows.Any())
                            {
                                vals.Add(double.NaN);
                                pts.Add(new DashboardDataPoint { X = idx, Y = double.NaN });
                                idx++;
                                continue;
                            }

                            double numSum = filteredRows.Sum(x => (double)numberParser(x.Row[numCol]));
                            double denSum = filteredRows.Sum(x => (double)numberParser(x.Row[denCol]));
                            double val = denSum != 0 ? numSum / denSum : double.NaN;

                            vals.Add(val);
                            pts.Add(new DashboardDataPoint { X = idx, Y = val });
                            idx++;
                        }

                        if (!hasSplit || vals.Any(v => !double.IsNaN((double)v) && (double)v > 0))
                        {
                            ApplySmartLabels(pts, false, res.XAxisLabels, sType);
                            res.Series.Add(new SeriesDto
                            {
                                Title = cleanTitle,
                                FullName = cleanTitle,
                                SeriesType = sType,
                                Values = vals,
                                Points = pts,
                                ColorHex = colorProvider(cleanTitle)
                            });
                        }
                    }
                }
                else if (!isPriceTrend)
                {
                    foreach (var ser in config.Series.Where(s => !string.IsNullOrEmpty(s.ColumnName) && dt.Columns.Contains(s.ColumnName)))
                    {
                        foreach (var splitGroup in cleanSplitGroups)
                        {
                            var rawValsInGroup = splitGroup.ToList();
                            string cleanTitle = hasSplit ? (config.Series.Count > 1 ? $"{splitGroup.Key} - {ser.ColumnName}" : splitGroup.Key) : ser.ColumnName;

                            var vals = new List<object>();
                            var pts = new List<DashboardDataPoint>();
                            int idx = 0;

                            foreach (var g in sortedGroups)
                            {
                                var filteredRows = hasSplit ? g.Where(x => rawValsInGroup.Contains(FormatCategoryKey(x.Row[config.SplitByColumn]))) : g;
                                if (!filteredRows.Any())
                                {
                                    vals.Add(0.0);
                                    pts.Add(new DashboardDataPoint { X = idx, Y = 0 });
                                    idx++;
                                    continue;
                                }

                                double aggVal = isAvg
                                    ? filteredRows.Average(x => (double)numberParser(x.Row[ser.ColumnName]))
                                    : filteredRows.Sum(x => (double)numberParser(x.Row[ser.ColumnName]));

                                vals.Add(aggVal);
                                pts.Add(new DashboardDataPoint { X = idx, Y = aggVal });
                                idx++;
                            }

                            if (!hasSplit || vals.Any(v => (double)v > 0))
                            {
                                ApplySmartLabels(pts, false, res.XAxisLabels, sType);
                                res.Series.Add(new SeriesDto
                                {
                                    Title = cleanTitle,
                                    FullName = cleanTitle,
                                    SeriesType = sType,
                                    Values = vals,
                                    Points = pts,
                                    ColorHex = colorProvider(cleanTitle)
                                });
                            }
                        }
                    }
                }
            }
        }

        private void ProcessPivotCategoricalChart(DataTable dt, DashboardConfiguration config, ChartResultDto res,
            Func<string, string> colorProvider, Func<object, double> numberParser, bool globalIgnoreHyphen, bool isAvg)
        {
            if (!dt.Columns.Contains(config.DateColumn)) return;

            var grouped = dt.AsEnumerable()
                .Select(r => new
                {
                    RawVal = r[config.DateColumn],
                    FormattedKey = FormatCategoryKey(r[config.DateColumn]),
                    CleanKey = CleanLabelString(FormatCategoryKey(r[config.DateColumn]), config.HideNumbersInLabels, config.SimplifyLabels, globalIgnoreHyphen),
                    Row = r
                })
                .GroupBy(x => x.CleanKey)
                .Select(g => new
                {
                    Key = g.Key,
                    SortDate = ParseDateSafe(g.First().RawVal),
                    IsDate = ParseDateSafe(g.First().RawVal) != DateTime.MinValue,
                    PrimarySum = config.Series.Any() && dt.Columns.Contains(config.Series.First().ColumnName)
                                 ? (isAvg ? g.Average(x => numberParser(x.Row[config.Series.First().ColumnName]))
                                          : g.Sum(x => numberParser(x.Row[config.Series.First().ColumnName])))
                                 : 0,
                    Rows = g.Select(x => x.Row).ToList()
                })
                .OrderBy(g => g.IsDate ? g.SortDate.Ticks : 0)
                .ThenByDescending(g => !g.IsDate ? g.PrimarySum : 0)
                .ToList();

            int topN = 30;
            var topGroups = grouped.Take(topN).ToList();

            res.XAxisLabels = topGroups.Select(g => g.Key).ToList();
            res.IsDateAxis = false;

            bool hasSplit = !string.IsNullOrEmpty(config.SplitByColumn) && dt.Columns.Contains(config.SplitByColumn);
            List<string> splitCategories = hasSplit
                ? dt.AsEnumerable().Select(r => FormatCategoryKey(r[config.SplitByColumn])).Distinct().OrderBy(x => x).ToList()
                : new List<string> { "Default" };

            if (hasSplit && config.SelectedSplitCategories != null && config.SelectedSplitCategories.Any(c => c != "__NONE__"))
            {
                splitCategories = splitCategories.Where(c => config.SelectedSplitCategories.Contains(c)).ToList();
            }
            else if (hasSplit && config.SelectedSplitCategories != null && config.SelectedSplitCategories.Contains("__NONE__"))
            {
                splitCategories.Clear();
            }

            var cleanSplitGroups = splitCategories
                .GroupBy(c => CleanLabelString(c, config.HideNumbersInLabels, config.SimplifyLabels, globalIgnoreHyphen))
                .ToList();

            bool isPriceTrend = string.Equals(config.ChartType, "Price Trend (Line)", StringComparison.OrdinalIgnoreCase);
            string baseChartType = isPriceTrend ? "Line" : config.ChartType;

            if (isPriceTrend && config.Series.Count >= 2)
            {
                string numCol = config.Series[0].ColumnName;
                string denCol = config.Series[1].ColumnName;

                foreach (var splitGroup in cleanSplitGroups)
                {
                    var rawValsInGroup = splitGroup.ToList();
                    string cleanTitle = hasSplit ? splitGroup.Key : "Efficiency Trend";

                    var vals = new List<object>();
                    var pts = new List<DashboardDataPoint>();
                    int idx = 0;

                    foreach (var g in topGroups)
                    {
                        var rows = hasSplit ? g.Rows.Where(r => rawValsInGroup.Contains(FormatCategoryKey(r[config.SplitByColumn]))) : g.Rows;
                        if (!rows.Any())
                        {
                            vals.Add(double.NaN);
                            pts.Add(new DashboardDataPoint { X = idx, Y = double.NaN });
                            idx++;
                            continue;
                        }

                        double numSum = rows.Sum(r => numberParser(r[numCol]));
                        double denSum = rows.Sum(r => numberParser(r[denCol]));
                        double val = denSum != 0 ? numSum / denSum : double.NaN;

                        vals.Add(val);
                        pts.Add(new DashboardDataPoint { X = idx, Y = val });
                        idx++;
                    }

                    if (!hasSplit || vals.Any(v => !double.IsNaN((double)v) && (double)v > 0))
                    {
                        ApplySmartLabels(pts, false, res.XAxisLabels, "Line");
                        res.Series.Add(new SeriesDto
                        {
                            Title = cleanTitle,
                            FullName = cleanTitle,
                            SeriesType = "Line",
                            Values = vals,
                            Points = pts,
                            ColorHex = colorProvider(cleanTitle)
                        });
                    }
                }
            }
            else if (!isPriceTrend)
            {
                foreach (var ser in config.Series.Where(s => !string.IsNullOrEmpty(s.ColumnName) && dt.Columns.Contains(s.ColumnName)))
                {
                    foreach (var splitGroup in cleanSplitGroups)
                    {
                        var rawValsInGroup = splitGroup.ToList();
                        string cleanTitle = hasSplit ? (config.Series.Count > 1 ? $"{splitGroup.Key} - {ser.ColumnName}" : splitGroup.Key) : ser.ColumnName;

                        var vals = new List<object>();
                        var pts = new List<DashboardDataPoint>();
                        int idx = 0;

                        foreach (var g in topGroups)
                        {
                            var rows = hasSplit ? g.Rows.Where(r => rawValsInGroup.Contains(FormatCategoryKey(r[config.SplitByColumn]))) : g.Rows;
                            if (!rows.Any())
                            {
                                vals.Add(0.0);
                                pts.Add(new DashboardDataPoint { X = idx, Y = 0 });
                                idx++;
                                continue;
                            }

                            double aggVal = isAvg
                                ? rows.Average(r => numberParser(r[ser.ColumnName]))
                                : rows.Sum(r => numberParser(r[ser.ColumnName]));

                            vals.Add(aggVal);
                            pts.Add(new DashboardDataPoint { X = idx, Y = aggVal });
                            idx++;
                        }

                        if (!hasSplit || vals.Any(v => (double)v > 0))
                        {
                            ApplySmartLabels(pts, false, res.XAxisLabels, baseChartType);
                            res.Series.Add(new SeriesDto
                            {
                                Title = cleanTitle,
                                FullName = cleanTitle,
                                SeriesType = baseChartType,
                                Values = vals,
                                Points = pts,
                                ColorHex = colorProvider(cleanTitle)
                            });
                        }
                    }
                }
            }
        }

        // ==================== Helper Methods (unchanged) ====================

        private string CleanLabelString(string original, bool hideNumbers, bool simplify, bool ignoreAfterHyphen)
        {
            if (string.IsNullOrWhiteSpace(original)) return "NA";
            string s = original;

            if (ignoreAfterHyphen && s.Contains("-"))
            {
                s = s.Split('-')[0];
            }

            if (hideNumbers)
            {
                s = Regex.Replace(s, @"\d+", "");
            }

            s = s.Replace("_", " ").Replace("-", " ").Trim();
            s = Regex.Replace(s, @"\s+", " ");

            if (simplify)
            {
                s = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? s;
            }

            return string.IsNullOrWhiteSpace(s) ? "NA" : s.Trim();
        }

        private void ApplySliderFilter(ref DataTable dt, double start, double end, double max)
        {
            if (max <= 0) return;
            double ratioStart = start / max; double ratioEnd = end / max;
            int totalRows = dt.Rows.Count; if (totalRows == 0) return;
            int startIndex = (int)(totalRows * ratioStart); int endIndex = (int)(totalRows * ratioEnd);
            if (startIndex < 0) startIndex = 0; if (endIndex >= totalRows) endIndex = totalRows - 1;
            if (startIndex > endIndex) startIndex = endIndex;
            var rowsToKeep = new List<DataRow>();
            for (int i = startIndex; i <= endIndex; i++) rowsToKeep.Add(dt.Rows[i]);
            DataTable filtered = dt.Clone();
            foreach (var r in rowsToKeep) filtered.ImportRow(r);
            dt = filtered;
        }

        private string FormatCategoryKey(object val)
        {
            if (val == null || val == DBNull.Value) return "NA";
            if (val is DateTime dt) return dt.ToString("dd.MM.yyyy");
            string str = val.ToString().Trim();
            if (DateTime.TryParse(str, out DateTime parsed)) return parsed.ToString("dd.MM.yyyy");
            return string.IsNullOrEmpty(str) ? "NA" : str;
        }

        private DateTime ParseDateSafe(object obj)
        {
            if (obj == null || obj == DBNull.Value) return DateTime.MinValue;
            if (obj is DateTime d) return d;
            if (DateTime.TryParse(obj.ToString(), out DateTime parsed)) return parsed;
            return DateTime.MinValue;
        }

        private DateTime GetStartOfWeek(DateTime dt) => dt.AddDays(-1 * ((7 + (dt.DayOfWeek - DayOfWeek.Monday)) % 7)).Date;
    }
}