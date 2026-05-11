// Services/DashboardChartService.cs
// PART 1 OF 2

using LiveCharts.Defaults;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Media;
using WPF_LoginForm.Models;

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
            public string FullName { get; set; }
            public string SeriesType { get; set; }
            public List<object> Values { get; set; }
            public List<DashboardDataPoint> Points { get; set; } = new List<DashboardDataPoint>();
            public List<double> PieValues { get; set; } = new List<double>();
            public string ColorHex { get; set; }
            public bool ShowOnlyHoverLabels { get; set; }
        }

        public static string FormatKiloMega(double value)
        {
            return SmartLabelService.FormatKiloMega(value);
        }

        public ChartResultDto ProcessChartData(DataTable dataTable, DashboardConfiguration config,
                                               bool isFilterByDate, bool ignoreNonDateData,
                                               double sliderStart, double sliderEnd, double sliderMax,
                                               ConcurrentDictionary<(int, string), string> colorMap,
                                               ConcurrentDictionary<(int, string), string> userColorMap,
                                               bool globalIgnoreAfterHyphen,
                                               bool globalIgnoreNumbers)
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

            string effectiveDataStructureType = (config.DataStructureType == "Monthly Date" && dataTable.Rows.Count > 100) ? "General" : config.DataStructureType;

            bool isDateBasedChart = effectiveDataStructureType == "Daily Date" && !string.IsNullOrEmpty(config.DateColumn);
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
                int dotCount = strVal.Count(c => c == '.');

                if (lastComma > lastDot) strVal = strVal.Replace(".", "").Replace(",", ".");
                else if (lastDot > lastComma && lastComma != -1) strVal = strVal.Replace(",", "");
                else if (lastComma != -1 && lastDot == -1) strVal = strVal.Replace(",", ".");
                else if (lastDot != -1 && lastComma == -1 && dotCount > 1) strVal = strVal.Replace(".", "");

                double.TryParse(strVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double res);
                return res;
            };

            Func<string, string, string> colorProvider = GetColor;

            string GetColor(string title, string seriesTitle = null)
            {
                // 0a. Direct match in user-specified colors (always takes priority)
                if (userColorMap != null && userColorMap.TryGetValue((config.ChartPosition, title), out string userExactHex))
                    return userExactHex;

                // 0b. Fallback via seriesTitle to user color — return exact color, not a variation
                if (seriesTitle != null && userColorMap != null && userColorMap.TryGetValue((config.ChartPosition, seriesTitle), out string userSeriesHex))
                    return userSeriesHex;

                if (colorMap != null)
                {
                    // 1. Direct match (cached color)
                    if (colorMap.TryGetValue((config.ChartPosition, title), out string hex)) return hex;

                    // 2. Fallback to Series Color (if this is a split category)
                    if (seriesTitle != null && colorMap.TryGetValue((config.ChartPosition, seriesTitle), out string seriesHex))
                    {
                        // Generate a stable variation of the series color based on the title
                        return GetColorVariation(seriesHex, title);
                    }

                    // 3. Generate random but STORE it in the map so it remains stable
                    lock (_randomLock)
                    {
                        var color = Color.FromRgb((byte)_random.Next(50, 240), (byte)_random.Next(50, 240), (byte)_random.Next(50, 240));
                        string newHex = color.ToString();
                        colorMap.TryAdd((config.ChartPosition, title), newHex);
                        return newHex;
                    }
                }
                
                lock (_randomLock)
                {
                    return Color.FromRgb((byte)_random.Next(50, 240), (byte)_random.Next(50, 240), (byte)_random.Next(50, 240)).ToString();
                }
            }

            string GetColorVariation(string baseHex, string variationKey)
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(baseHex);
                    // Use hash of variationKey to get a stable offset
                    int hash = variationKey.GetHashCode();
                    int r = Math.Max(30, Math.Min(225, color.R + (hash % 40)));
                    int g = Math.Max(30, Math.Min(225, color.G + ((hash >> 8) % 40)));
                    int b = Math.Max(30, Math.Min(225, color.B + ((hash >> 16) % 40)));
                    return Color.FromRgb((byte)r, (byte)g, (byte)b).ToString();
                }
                catch { return baseHex; }
            }

            bool isAvg = string.Equals(config.ValueAggregation, "Average", StringComparison.OrdinalIgnoreCase);

            if (string.Equals(config.ChartType, "Pie", StringComparison.OrdinalIgnoreCase))
            {
                ProcessPieChart(dataTable, config, result, colorProvider, safeConvertToDouble, globalIgnoreAfterHyphen, globalIgnoreNumbers, isAvg);
            }
            else
            {
                switch (effectiveDataStructureType)
                {
                    case "Daily Date":
                        ProcessDailyDateChart(dataTable, config, result, colorProvider, safeConvertToDouble, globalIgnoreAfterHyphen, globalIgnoreNumbers, isAvg);
                        break;

                    case "Monthly Date":
                    case "ID":
                    case "General":
                    default:
                        ProcessPivotCategoricalChart(dataTable, config, result, colorProvider, safeConvertToDouble, globalIgnoreAfterHyphen, globalIgnoreNumbers, isAvg);
                        break;
                }
            }

            return result;
        }

        // --- UPDATED: Evaluates the series and specifically targets the Blueprint Zones for Tooltips ---
        private (double YValue, string LabelText, string TooltipHeader, string TooltipLeft, string TooltipRight) EvaluateSeriesForGroup(
            SeriesConfiguration ser, IEnumerable<DataRow> rows, Func<object, double> numberParser,
            bool globalIsAvg, string splitCategory, string rawSeriesName, DateTime? xDate, string xCategory, bool isDateAxis, int totalSeriesCount,
            DashboardConfiguration config = null)
        {
            if (rows == null || !rows.Any()) return (0.0, "", "", "", "");

            bool useTemplate = false;
            NodeFlowState state = null;

            if (ser.IsCombinationLabel && ser.SavedStates != null && ser.ActiveStateIndex >= 0 && ser.SavedStates.Count > ser.ActiveStateIndex)
            {
                state = ser.SavedStates[ser.ActiveStateIndex];
            }

            if ((state == null || state.Nodes == null || !state.Nodes.Any()) && config != null && config.Series.Count > 0 && ser != config.Series[0])
            {
                var templateSer = config.Series[0];
                if (templateSer.IsCombinationLabel && templateSer.SavedStates != null && 
                    templateSer.ActiveStateIndex >= 0 && templateSer.SavedStates.Count > templateSer.ActiveStateIndex)
                {
                    var tmplState = templateSer.SavedStates[templateSer.ActiveStateIndex];
                    if (tmplState != null && tmplState.Nodes != null && tmplState.Nodes.Any())
                    {
                        state = tmplState;
                        useTemplate = true;
                    }
                }
            }

            if (state != null && state.Nodes != null && state.Nodes.Any())
            {
                string tHeader = "";
                string tLeft = "";
                string tRight = "";
                double? firstYValue = null;

                foreach (var node in state.Nodes)
                {
                    string nodeString = "";

                    if (node.NodeType == "StaticText") nodeString = node.Value;
                    else if (node.NodeType == "NewLine") nodeString = "\n";
                    else if (node.NodeType == "SeriesName")
                    {
                        if (totalSeriesCount > 1 && !string.IsNullOrEmpty(splitCategory))
                        {
                            if (node.Zone == 2) nodeString = splitCategory;
                            else if (node.Zone == 3) nodeString = rawSeriesName;
                            else nodeString = $"{splitCategory} - {rawSeriesName}";
                        }
                        else
                        {
                            nodeString = (!string.IsNullOrEmpty(splitCategory) ? (totalSeriesCount > 1 ? $"{splitCategory} - {rawSeriesName}" : splitCategory) : rawSeriesName) ?? "";
                        }
                    }
                    else if (node.NodeType == "XAxis")
                    {
                        if (isDateAxis && xDate.HasValue)
                        {
                            string fmt = string.IsNullOrWhiteSpace(node.Value) ? "MMM yyyy" : node.Value;
                            nodeString = xDate.Value.ToString(fmt, new CultureInfo("tr-TR"));
                        }
                        else nodeString = xCategory ?? "";
                    }
                    else if (node.NodeType == "DataColumn")
                    {
                        string col = useTemplate ? ser.ColumnName : node.Value;

                        double val = 0;
                        if (!string.IsNullOrEmpty(col) && rows.First().Table.Columns.Contains(col))
                        {
                            bool isNodeAvg = node.Aggregation == "Average" || (globalIsAvg && node.Aggregation != "Sum");
                            val = isNodeAvg ? rows.Average(r => numberParser(r[col])) : rows.Sum(r => numberParser(r[col]));
                        }
                        if (firstYValue == null) firstYValue = val;
                        nodeString = SmartLabelService.FormatKiloMega(val);
                    }

                    if (node.Zone == 1) tHeader += nodeString;
                    else if (node.Zone == 2) tLeft += nodeString;
                    else tRight += nodeString;
                }

                string combinedLabel = "";
                if (!string.IsNullOrWhiteSpace(tHeader)) combinedLabel += tHeader + "\n";
                if (!string.IsNullOrWhiteSpace(tLeft) || !string.IsNullOrWhiteSpace(tRight))
                    combinedLabel += $"{tLeft} {tRight}".Trim();

                return (firstYValue ?? 0.0, combinedLabel.Trim(), tHeader.Trim(), tLeft.Trim(), tRight.Trim());
            }

            if (string.IsNullOrEmpty(ser.ColumnName) || !rows.First().Table.Columns.Contains(ser.ColumnName))
                return (0.0, "", "", "", "");

            double fallbackVal = globalIsAvg ? rows.Average(r => numberParser(r[ser.ColumnName])) : rows.Sum(r => numberParser(r[ser.ColumnName]));
            string defHeader = isDateAxis && xDate.HasValue ? xDate.Value.ToString("dd.MM.yyyy") : (xCategory ?? "");
            string formattedVal = SmartLabelService.FormatKiloMega(fallbackVal);

            string defLeft;
            string defRight;

            if (totalSeriesCount > 1 && !string.IsNullOrEmpty(splitCategory))
            {
                defLeft = splitCategory;
                defRight = $"{rawSeriesName} {formattedVal}";
            }
            else
            {
                defLeft = !string.IsNullOrEmpty(splitCategory) ? (totalSeriesCount > 1 ? $"{splitCategory} - {rawSeriesName}" : splitCategory) : rawSeriesName;
                defRight = formattedVal;
            }

            string defaultLabel = string.IsNullOrWhiteSpace(defHeader)
                ? $"{defLeft} {defRight}".Trim()
                : $"{defHeader}\n{defLeft} {defRight}".Trim();

            return (fallbackVal, defaultLabel, defHeader, defLeft, defRight);
        }

        private void ProcessPieChart(DataTable dataTable, DashboardConfiguration config, ChartResultDto result,
            Func<string, string, string> colorProvider, Func<object, double> numberParser, bool globalIgnoreAfterHyphen, bool globalIgnoreNumbers, bool isAvg)
        {
            string groupByCol = !string.IsNullOrEmpty(config.SplitByColumn) ? config.SplitByColumn : config.DateColumn;

            if (!string.IsNullOrEmpty(groupByCol) && dataTable.Columns.Contains(groupByCol) && config.Series.Any())
            {
                var seriesConfig = config.Series.FirstOrDefault();
                if (seriesConfig != null)
                {
                    bool hasSplit = !string.IsNullOrEmpty(config.SplitByColumn);
                    List<string> allowedCategories = null;

                    if (hasSplit && config.SelectedSplitCategories != null && config.SelectedSplitCategories.Any())
                        allowedCategories = config.SelectedSplitCategories;

                    var grouped = dataTable.AsEnumerable()
                        .Where(r => r[groupByCol] != DBNull.Value)
                        .Select(r => new { RawCategory = FormatCategoryKey(r[groupByCol]), Row = r })
                        .Where(x => allowedCategories == null || (!allowedCategories.Contains("__NONE__") && allowedCategories.Contains(x.RawCategory)))
                        .GroupBy(x => x.RawCategory)
                        .Select(g => new
                        {
                            RawCategory = g.Key,
                            CleanCategory = CleanLabelString(g.Key, config.HideNumbersInLabels || globalIgnoreNumbers, config.SimplifyLabels, globalIgnoreAfterHyphen),
                             Total = EvaluateSeriesForGroup(seriesConfig, g.Select(x => x.Row), numberParser, isAvg, null, seriesConfig.ColumnName, null, g.Key, false, config.Series.Count, config).YValue
                        })
                        .GroupBy(x => x.CleanCategory)
                        .Select(g => new
                        {
                            Category = g.Key,
                            OriginalNames = g.Select(x => x.RawCategory).Distinct().ToList(),
                            Total = g.Sum(x => x.Total)
                        })
                        .Where(x => x.Total > 0)
                        .OrderByDescending(x => x.Total)
                        .ToList();

                    int maxSlices = 8;
                    var topItems = grouped.Take(maxSlices).ToList();
                    var othersSum = grouped.Skip(maxSlices).Sum(x => x.Total);

                    string pieSeriesTitle = seriesConfig?.ColumnName;
                    foreach (var g in topItems)
                    {
                        string tooltipName = g.OriginalNames.Count > 1 ? $"{g.Category} (Grouped)" : g.OriginalNames.FirstOrDefault() ?? g.Category;
                        result.Series.Add(new SeriesDto
                        {
                            Title = g.Category,
                            FullName = tooltipName,
                            SeriesType = "Pie",
                            Values = new List<object> { g.Total },
                            PieValues = new List<double> { g.Total },
                            ColorHex = colorProvider(g.Category, pieSeriesTitle),
                            ShowOnlyHoverLabels = !config.ShowLabelsOnChart
                        });
                    }

                    if (othersSum > 0)
                        result.Series.Add(new SeriesDto { Title = "Others", FullName = "Others (Minor Values)", SeriesType = "Pie", Values = new List<object> { othersSum }, PieValues = new List<double> { othersSum }, ColorHex = "#808080" });
                }
            }
            else
            {
                foreach (var seriesConfig in config.Series)
                {
                    if (!seriesConfig.IsCombinationLabel && (string.IsNullOrEmpty(seriesConfig.ColumnName) || !dataTable.Columns.Contains(seriesConfig.ColumnName))) continue;

                    var evalResult = EvaluateSeriesForGroup(seriesConfig, dataTable.AsEnumerable(), numberParser, isAvg, null, seriesConfig.ColumnName, null, "", false, config.Series.Count, config);
                    if (evalResult.YValue <= 0) continue;

                    string rawName = seriesConfig.ColumnName ?? "Series";
                    string cleanName = CleanLabelString(rawName, config.HideNumbersInLabels, config.SimplifyLabels, globalIgnoreAfterHyphen);

                    result.Series.Add(new SeriesDto
                    {
                        Title = cleanName,
                        FullName = rawName,
                        SeriesType = "Pie",
                        Values = new List<object> { evalResult.YValue },
                        PieValues = new List<double> { evalResult.YValue },
                        ColorHex = colorProvider(cleanName, null),
                        ShowOnlyHoverLabels = !config.ShowLabelsOnChart
                    });
                }
            }
        }

        // Services/DashboardChartService.cs
        // PART 2 OF 2

        private void ProcessDailyDateChart(DataTable dt, DashboardConfiguration config, ChartResultDto res,
            Func<string, string, string> colorProvider, Func<object, double> numberParser, bool globalIgnoreHyphen, bool globalIgnoreNumbers, bool isAvg)
        {
            if (!dt.Columns.Contains(config.DateColumn)) return;

            var points = dt.AsEnumerable()
                .Select(r => new { Date = ParseDateSafe(r[config.DateColumn]), Row = r })
                .Where(p => p.Date != DateTime.MinValue)
                .ToList();

            if (!points.Any()) return;

            DateTime minDate = points.Min(p => p.Date);
            DateTime maxDate = points.Max(p => p.Date);
            double totalDays = (maxDate - minDate).TotalDays;

            string effectiveAgg = config.AggregationType;
            if (effectiveAgg == "Daily" && totalDays > 90) effectiveAgg = "Weekly";
            if ((effectiveAgg == "Daily" || effectiveAgg == "Weekly") && totalDays > 365) effectiveAgg = "Monthly";

            IEnumerable<IGrouping<DateTime, dynamic>> groups;
            if (effectiveAgg == "Weekly") groups = points.GroupBy(p => GetStartOfWeek(p.Date));
            else if (effectiveAgg == "Monthly") groups = points.GroupBy(p => new DateTime(p.Date.Year, p.Date.Month, 1));
            else groups = points.GroupBy(p => p.Date.Date);

            var sortedGroups = groups.OrderBy(g => g.Key).ToList();

            bool hasSplit = !string.IsNullOrEmpty(config.SplitByColumn) && dt.Columns.Contains(config.SplitByColumn);
            List<string> splitCategories = hasSplit
                ? dt.AsEnumerable().Select(r => FormatCategoryKey(r[config.SplitByColumn])).Distinct().OrderBy(x => x).ToList()
                : new List<string> { "Default" };

            if (hasSplit && config.SelectedSplitCategories != null && config.SelectedSplitCategories.Any(c => c != "__NONE__"))
                splitCategories = splitCategories.Where(c => config.SelectedSplitCategories.Contains(c)).ToList();
            else if (hasSplit && config.SelectedSplitCategories != null && config.SelectedSplitCategories.Contains("__NONE__"))
                splitCategories.Clear();

            var cleanSplitGroups = splitCategories.GroupBy(c => CleanLabelString(c, config.HideNumbersInLabels || globalIgnoreNumbers, config.SimplifyLabels, globalIgnoreHyphen)).ToList();

            bool isPriceTrend = string.Equals(config.ChartType, "Price Trend (Line)", StringComparison.OrdinalIgnoreCase);
            bool isLine = isPriceTrend || config.ChartType == "Line";
            bool isDailyLine = isLine && effectiveAgg == "Daily";

            if (isDailyLine)
            {
                res.IsDateAxis = true;

                if (isPriceTrend && config.Series.Count >= 2)
                {
                    string numCol = config.Series[0].ColumnName;
                    string denCol = config.Series[1].ColumnName;
                    bool showHoverOnly = config.Series[0].ShowOnlyHoverLabels;

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

                            string dateStr = g.Key.ToString("dd.MM.yyyy");
                            string defaultLabel = $"{dateStr}\n{cleanTitle} {SmartLabelService.FormatKiloMega(avg)}".Trim();

                            pts.Add(new DashboardDataPoint
                            {
                                X = g.Key.Ticks,
                                Y = avg,
                                Label = defaultLabel,
                                TooltipHeader = dateStr,
                                TooltipLeft = cleanTitle,
                                TooltipRight = SmartLabelService.FormatKiloMega(avg)
                            });
                        }

                        if (!hasSplit || vals.Any(v => !double.IsNaN(((DateTimePoint)v).Value) && ((DateTimePoint)v).Value != 0))
                        {
                             if (config.ShowLabelsOnChart && pts.Count <= 35) SmartLabelService.ApplyLabels(pts, true, null, "Line");
                             res.Series.Add(new SeriesDto { Title = cleanTitle, FullName = cleanTitle, SeriesType = "Line", Values = vals, Points = pts, ColorHex = colorProvider(cleanTitle, hasSplit ? config.Series[0].ColumnName : null), ShowOnlyHoverLabels = !config.ShowLabelsOnChart });
                        }
                    }
                }
                else if (!isPriceTrend)
                {
                    foreach (var ser in config.Series)
                    {
                        if (!ser.IsCombinationLabel && (string.IsNullOrEmpty(ser.ColumnName) || !dt.Columns.Contains(ser.ColumnName)))
                            continue;

                         foreach (var splitGroup in cleanSplitGroups)
                        {
                            var rawValsInGroup = splitGroup.ToList();
                            string rawSerName = ser.ColumnName ?? "Series";
                            string displaySerName = !string.IsNullOrEmpty(ser.LegendLabel) ? ser.LegendLabel : rawSerName;
                            string cleanTitle = hasSplit ? (config.Series.Count > 1 ? $"{splitGroup.Key} - {displaySerName}" : splitGroup.Key) : displaySerName;

                            var vals = new List<object>();
                            var pts = new List<DashboardDataPoint>();

                            foreach (var g in sortedGroups)
                            {
                                var filteredRows = hasSplit ? g.Where(x => rawValsInGroup.Contains(FormatCategoryKey(x.Row[config.SplitByColumn]))) : g;
                                if (!filteredRows.Any())
                                {
                                    vals.Add(new DateTimePoint(g.Key, double.NaN));
                                    pts.Add(new DashboardDataPoint { X = g.Key.Ticks, Y = double.NaN, Label = "" });
                                    continue;
                                }

                                var evalResult = EvaluateSeriesForGroup(ser, filteredRows.Select(r => (DataRow)r.Row), numberParser, isAvg, hasSplit ? splitGroup.Key : null, rawSerName, g.Key, null, true, config.Series.Count * (hasSplit ? splitCategories.Count : 1), config);

                                vals.Add(new DateTimePoint(g.Key, evalResult.YValue));
                                pts.Add(new DashboardDataPoint
                                {
                                    X = g.Key.Ticks,
                                    Y = evalResult.YValue,
                                    Label = evalResult.LabelText,
                                    TooltipHeader = evalResult.TooltipHeader,
                                    TooltipLeft = evalResult.TooltipLeft,
                                    TooltipRight = evalResult.TooltipRight
                                });
                            }

                            if (!hasSplit || vals.Any(v => !double.IsNaN(((DateTimePoint)v).Value) && ((DateTimePoint)v).Value != 0))
                            {
                                if (config.ShowLabelsOnChart && pts.Count <= 35) SmartLabelService.ApplyLabels(pts, true, null, "Line");
                                res.Series.Add(new SeriesDto { Title = cleanTitle, FullName = cleanTitle, SeriesType = "Line", Values = vals, Points = pts, ColorHex = colorProvider(cleanTitle, hasSplit ? rawSerName : null), ShowOnlyHoverLabels = !config.ShowLabelsOnChart });
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
                    if (effectiveAgg == "Monthly") return g.Key.ToString("MMM yyyy", new CultureInfo("tr-TR"));
                    if (effectiveAgg == "Weekly") return g.Key.ToString("dd.MM.yyyy") + " (Wk)";
                    return g.Key.ToString("dd.MM.yyyy");
                }).ToList();

                string sType = isLine ? "Line" : "Bar";

                if (isPriceTrend && config.Series.Count >= 2)
                {
                    string numCol = config.Series[0].ColumnName;
                    string denCol = config.Series[1].ColumnName;
                    bool showHoverOnly = config.Series[0].ShowOnlyHoverLabels;

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

                            string dateStr = res.XAxisLabels[idx];
                            string defaultLabel = $"{dateStr}\n{cleanTitle} {SmartLabelService.FormatKiloMega(val)}".Trim();

                            pts.Add(new DashboardDataPoint
                            {
                                X = idx,
                                Y = val,
                                Label = defaultLabel,
                                TooltipHeader = dateStr,
                                TooltipLeft = cleanTitle,
                                TooltipRight = SmartLabelService.FormatKiloMega(val)
                            });
                            idx++;
                        }

                        if (!hasSplit || vals.Any(v => !double.IsNaN((double)v) && (double)v != 0))
                        {
                            if (pts.Count <= 35) SmartLabelService.ApplyLabels(pts, false, res.XAxisLabels, sType);
                            res.Series.Add(new SeriesDto { Title = cleanTitle, FullName = cleanTitle, SeriesType = sType, Values = vals, Points = pts, ColorHex = colorProvider(cleanTitle, hasSplit ? (isPriceTrend ? config.Series[0].ColumnName : null) : null), ShowOnlyHoverLabels = showHoverOnly });
                        }
                    }
                }
                else if (!isPriceTrend)
                {
                    foreach (var ser in config.Series)
                    {
                        if (!ser.IsCombinationLabel && (string.IsNullOrEmpty(ser.ColumnName) || !dt.Columns.Contains(ser.ColumnName)))
                            continue;

                         foreach (var splitGroup in cleanSplitGroups)
                        {
                            var rawValsInGroup = splitGroup.ToList();
                            string rawSerName = ser.ColumnName ?? "Series";
                            string displaySerName = !string.IsNullOrEmpty(ser.LegendLabel) ? ser.LegendLabel : rawSerName;
                            string cleanTitle = hasSplit ? (config.Series.Count > 1 ? $"{splitGroup.Key} - {displaySerName}" : splitGroup.Key) : displaySerName;

                            var vals = new List<object>();
                            var pts = new List<DashboardDataPoint>();
                            int idx = 0;

                            foreach (var g in sortedGroups)
                            {
                                var filteredRows = hasSplit ? g.Where(x => rawValsInGroup.Contains(FormatCategoryKey(x.Row[config.SplitByColumn]))) : g;
                                if (!filteredRows.Any())
                                {
                                    vals.Add(double.NaN);
                                    pts.Add(new DashboardDataPoint { X = idx, Y = double.NaN, Label = "" });
                                    idx++;
                                    continue;
                                }

                                var evalResult = EvaluateSeriesForGroup(ser, filteredRows.Select(r => (DataRow)r.Row), numberParser, isAvg, hasSplit ? splitGroup.Key : null, rawSerName, null, res.XAxisLabels[idx], false, config.Series.Count * (hasSplit ? splitCategories.Count : 1), config);

                                vals.Add(evalResult.YValue);
                                pts.Add(new DashboardDataPoint
                                {
                                    X = idx,
                                    Y = evalResult.YValue,
                                    Label = evalResult.LabelText,
                                    TooltipHeader = evalResult.TooltipHeader,
                                    TooltipLeft = evalResult.TooltipLeft,
                                    TooltipRight = evalResult.TooltipRight
                                });
                                idx++;
                            }

                            if (!hasSplit || vals.Any(v => !double.IsNaN((double)v) && (double)v != 0))
                            {
                                if (config.ShowLabelsOnChart && pts.Count <= 35) SmartLabelService.ApplyLabels(pts, false, res.XAxisLabels, sType);
                                res.Series.Add(new SeriesDto { Title = cleanTitle, FullName = cleanTitle, SeriesType = sType, Values = vals, Points = pts, ColorHex = colorProvider(cleanTitle, hasSplit ? rawSerName : null), ShowOnlyHoverLabels = !config.ShowLabelsOnChart });
                            }
                        }
                    }
                }
            }
        }

        private void ProcessPivotCategoricalChart(DataTable dt, DashboardConfiguration config, ChartResultDto res,
            Func<string, string, string> colorProvider, Func<object, double> numberParser, bool globalIgnoreHyphen, bool globalIgnoreNumbers, bool isAvg)
        {
            if (!dt.Columns.Contains(config.DateColumn)) return;

            var grouped = dt.AsEnumerable()
                .Select(r => new
                {
                    RawVal = r[config.DateColumn],
                    FormattedKey = FormatCategoryKey(r[config.DateColumn]),
                    CleanKey = CleanLabelString(FormatCategoryKey(r[config.DateColumn]), config.HideNumbersInLabels || globalIgnoreNumbers, config.SimplifyLabels, globalIgnoreHyphen),
                    Row = r
                })
                .GroupBy(x => x.CleanKey)
                .Select(g => new
                {
                    Key = g.Key,
                    SortDate = ParseDateSafe(g.First().RawVal),
                    IsDate = ParseDateSafe(g.First().RawVal) != DateTime.MinValue,
                     PrimarySum = config.Series.Any()
                                 ? EvaluateSeriesForGroup(config.Series.First(), g.Select(x => x.Row), numberParser, isAvg, null, config.Series.First().ColumnName, null, g.Key, false, config.Series.Count, config).YValue
                                 : 0,
                    Rows = g.Select(x => x.Row).ToList()
                })
                .OrderBy(g => g.IsDate ? g.SortDate.Ticks : 0)
                .ThenByDescending(g => !g.IsDate ? Math.Abs(g.PrimarySum) : 0)
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
                splitCategories = splitCategories.Where(c => config.SelectedSplitCategories.Contains(c)).ToList();
            else if (hasSplit && config.SelectedSplitCategories != null && config.SelectedSplitCategories.Contains("__NONE__"))
                splitCategories.Clear();

            var cleanSplitGroups = splitCategories.GroupBy(c => CleanLabelString(c, config.HideNumbersInLabels || globalIgnoreNumbers, config.SimplifyLabels, globalIgnoreHyphen)).ToList();

            bool isPriceTrend = string.Equals(config.ChartType, "Price Trend (Line)", StringComparison.OrdinalIgnoreCase);
            string baseChartType = isPriceTrend ? "Line" : config.ChartType;

            if (isPriceTrend && config.Series.Count >= 2)
            {
                string numCol = config.Series[0].ColumnName;
                string denCol = config.Series[1].ColumnName;
                bool showHoverOnly = config.Series[0].ShowOnlyHoverLabels;

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

                        string catStr = res.XAxisLabels[idx];
                        string defaultLabel = $"{catStr}\n{cleanTitle} {SmartLabelService.FormatKiloMega(val)}".Trim();

                        pts.Add(new DashboardDataPoint
                        {
                            X = idx,
                            Y = val,
                            Label = defaultLabel,
                            TooltipHeader = catStr,
                            TooltipLeft = cleanTitle,
                            TooltipRight = SmartLabelService.FormatKiloMega(val)
                        });
                        idx++;
                    }

                    if (!hasSplit || vals.Any(v => !double.IsNaN((double)v) && (double)v != 0))
                    {
                        if (config.ShowLabelsOnChart && pts.Count <= 35) SmartLabelService.ApplyLabels(pts, false, res.XAxisLabels, "Line");
                        res.Series.Add(new SeriesDto { Title = cleanTitle, FullName = cleanTitle, SeriesType = "Line", Values = vals, Points = pts, ColorHex = colorProvider(cleanTitle, hasSplit ? config.Series[0].ColumnName : null), ShowOnlyHoverLabels = !config.ShowLabelsOnChart });
                    }
                }
            }
            else if (!isPriceTrend)
            {
                foreach (var ser in config.Series)
                {
                    if (!ser.IsCombinationLabel && (string.IsNullOrEmpty(ser.ColumnName) || !dt.Columns.Contains(ser.ColumnName)))
                        continue;

                         foreach (var splitGroup in cleanSplitGroups)
                        {
                            var rawValsInGroup = splitGroup.ToList();
                            string rawSerName = ser.ColumnName ?? "Series";
                            string displaySerName = !string.IsNullOrEmpty(ser.LegendLabel) ? ser.LegendLabel : rawSerName;
                            string cleanTitle = hasSplit ? (config.Series.Count > 1 ? $"{splitGroup.Key} - {displaySerName}" : splitGroup.Key) : displaySerName;

                        var vals = new List<object>();
                        var pts = new List<DashboardDataPoint>();
                        int idx = 0;

                        foreach (var g in topGroups)
                        {
                            var rows = hasSplit ? g.Rows.Where(r => rawValsInGroup.Contains(FormatCategoryKey(r[config.SplitByColumn]))) : g.Rows;
                            if (!rows.Any())
                            {
                                vals.Add(double.NaN);
                                pts.Add(new DashboardDataPoint { X = idx, Y = double.NaN, Label = "" });
                                idx++;
                                continue;
                            }

                                var evalResult = EvaluateSeriesForGroup(ser, rows, numberParser, isAvg, hasSplit ? splitGroup.Key : null, rawSerName, null, res.XAxisLabels[idx], false, config.Series.Count * (hasSplit ? splitCategories.Count : 1), config);

                                vals.Add(evalResult.YValue);
                                pts.Add(new DashboardDataPoint
                                {
                                    X = idx,
                                    Y = evalResult.YValue,
                                    Label = evalResult.LabelText,
                                    TooltipHeader = evalResult.TooltipHeader,
                                    TooltipLeft = evalResult.TooltipLeft,
                                    TooltipRight = evalResult.TooltipRight
                                });
                            idx++;
                        }

                        if (!hasSplit || vals.Any(v => !double.IsNaN((double)v) && (double)v != 0))
                        {
                            if (config.ShowLabelsOnChart && pts.Count <= 35) SmartLabelService.ApplyLabels(pts, false, res.XAxisLabels, baseChartType);
                            res.Series.Add(new SeriesDto { Title = cleanTitle, FullName = cleanTitle, SeriesType = baseChartType, Values = vals, Points = pts, ColorHex = colorProvider(cleanTitle, hasSplit ? rawSerName : null), ShowOnlyHoverLabels = !config.ShowLabelsOnChart });
                        }
                    }
                }
            }
        }

        private string CleanLabelString(string original, bool hideNumbers, bool simplify, bool ignoreAfterHyphen)
        {
            if (string.IsNullOrWhiteSpace(original)) return "NA";
            string s = original;
            if (ignoreAfterHyphen && s.Contains("-")) s = s.Split('-')[0];
            if (hideNumbers) s = Regex.Replace(s, @"\d+", "");
            s = s.Replace("_", " ").Replace("-", " ").Trim();
            s = Regex.Replace(s, @"\s+", " ");
            if (simplify) s = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? s;
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