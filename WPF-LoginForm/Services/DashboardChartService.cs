// Services/DashboardChartService.cs
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Defaults;
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
            public string ColorHex { get; set; }
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

            bool isDateBasedChart = config.DataStructureType == "Daily Date" && !string.IsNullOrEmpty(config.DateColumn);
            if (!isDateBasedChart || !isFilterByDate)
            {
                ApplySliderFilter(ref dataTable, sliderStart, sliderEnd, sliderMax);
            }

            Func<object, double> safeConvertToDouble = (obj) =>
            {
                if (obj == null || obj == DBNull.Value) return 0.0;
                string strVal = obj.ToString().Replace("₺", "").Replace("TL", "").Replace("%", "").Trim();
                var culture = config.UseInvariantCultureForNumbers ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture;
                double.TryParse(strVal, NumberStyles.Any, culture, out double res);
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

            if (string.Equals(config.ChartType, "Pie", StringComparison.OrdinalIgnoreCase))
            {
                string groupByCol = !string.IsNullOrEmpty(config.SplitByColumn) ? config.SplitByColumn : config.DateColumn;

                if (!string.IsNullOrEmpty(groupByCol) && dataTable.Columns.Contains(groupByCol) && config.Series.Any())
                {
                    var valueCol = config.Series.FirstOrDefault(s => dataTable.Columns.Contains(s.ColumnName))?.ColumnName;
                    if (valueCol != null)
                    {
                        // FIXED LOGIC: Group directly by the Cleaned Label, aggregating duplicates together
                        var grouped = dataTable.AsEnumerable()
                            .Select(r => new
                            {
                                RawCategory = FormatCategoryKey(r[groupByCol]),
                                CleanCategory = CleanLabelString(FormatCategoryKey(r[groupByCol]), config.HideNumbersInLabels, config.SimplifyLabels, globalIgnoreAfterHyphen),
                                Value = safeConvertToDouble(r[valueCol])
                            })
                            .GroupBy(x => x.CleanCategory)
                            .Select(g => new
                            {
                                Category = g.Key,
                                OriginalNames = g.Select(x => x.RawCategory).Distinct().ToList(),
                                Total = g.Sum(x => x.Value)
                            })
                            .OrderByDescending(x => x.Total)
                            .ToList();

                        int maxSlices = 8;
                        var topItems = grouped.Take(maxSlices).ToList();
                        var othersSum = grouped.Skip(maxSlices).Sum(x => x.Total);

                        foreach (var g in topItems)
                        {
                            // If multiple raw categories merged into one clean category, indicate it in tooltip
                            string tooltipName = g.OriginalNames.Count > 1
                                ? $"{g.Category} (Grouped)"
                                : g.OriginalNames.FirstOrDefault() ?? g.Category;

                            result.Series.Add(new SeriesDto
                            {
                                Title = g.Category,
                                FullName = tooltipName,
                                SeriesType = "Pie",
                                Values = new List<object> { g.Total },
                                ColorHex = GetColor(g.Category)
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
                                ColorHex = "#808080"
                            });
                        }
                    }
                }
                else
                {
                    foreach (var seriesConfig in config.Series.Where(s => !string.IsNullOrEmpty(s.ColumnName) && dataTable.Columns.Contains(s.ColumnName)))
                    {
                        double total = dataTable.AsEnumerable().Sum(row => safeConvertToDouble(row[seriesConfig.ColumnName]));
                        string cleanName = CleanLabelString(seriesConfig.ColumnName, config.HideNumbersInLabels, config.SimplifyLabels, globalIgnoreAfterHyphen);
                        result.Series.Add(new SeriesDto { Title = cleanName, FullName = seriesConfig.ColumnName, SeriesType = "Pie", Values = new List<object> { total }, ColorHex = GetColor(cleanName) });
                    }
                }
            }
            else
            {
                switch (config.DataStructureType)
                {
                    case "Daily Date": ProcessDailyDateChart(dataTable, config, result, GetColor, safeConvertToDouble, globalIgnoreAfterHyphen); break;
                    case "Monthly Date":
                    case "ID":
                    case "General":
                    default: ProcessPivotCategoricalChart(dataTable, config, result, GetColor, safeConvertToDouble, globalIgnoreAfterHyphen); break;
                }
            }

            return result;
        }

        // IMPROVED: Hardened regex and string manipulation
        private string CleanLabelString(string original, bool hideNumbers, bool simplify, bool ignoreAfterHyphen)
        {
            if (string.IsNullOrWhiteSpace(original)) return "Unknown";
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
            s = Regex.Replace(s, @"\s+", " "); // Replace multiple spaces with one

            if (simplify)
            {
                s = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? s;
            }

            return string.IsNullOrWhiteSpace(s) ? "Item" : s.Trim();
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

        private void ProcessDailyDateChart(DataTable dt, DashboardConfiguration config, ChartResultDto res, Func<string, string> colorProvider, Func<object, double> numberParser, bool globalIgnoreHyphen)
        {
            if (!dt.Columns.Contains(config.DateColumn)) return;

            var points = dt.AsEnumerable()
                .Select(r => new { Date = ParseDateSafe(r[config.DateColumn]), Row = r })
                .Where(p => p.Date != DateTime.MinValue)
                .ToList();

            IEnumerable<IGrouping<DateTime, dynamic>> groups;
            if (config.AggregationType == "Weekly") groups = points.GroupBy(p => GetStartOfWeek(p.Date));
            else if (config.AggregationType == "Monthly") groups = points.GroupBy(p => new DateTime(p.Date.Year, p.Date.Month, 1));
            else groups = points.GroupBy(p => p.Date.Date);

            var sortedGroups = groups.OrderBy(g => g.Key).ToList();

            bool hasSplit = !string.IsNullOrEmpty(config.SplitByColumn) && dt.Columns.Contains(config.SplitByColumn);
            List<string> splitCategories = hasSplit
                ? dt.AsEnumerable().Select(r => r[config.SplitByColumn]?.ToString() ?? "Unknown").Distinct().OrderBy(x => x).ToList()
                : new List<string> { "Default" };

            if (hasSplit && config.SelectedSplitCategories != null && config.SelectedSplitCategories.Any(c => c != "__NONE__"))
            {
                splitCategories = splitCategories.Where(c => config.SelectedSplitCategories.Contains(c)).ToList();
            }
            else if (hasSplit && config.SelectedSplitCategories != null && config.SelectedSplitCategories.Contains("__NONE__"))
            {
                splitCategories.Clear(); // Draw nothing if unselected
            }

            // Group the selected Pivot items by their clean names to merge data automatically
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

                        var vals = sortedGroups.Select(g =>
                        {
                            var filteredRows = hasSplit ? g.Where(x => rawValsInGroup.Contains(x.Row[config.SplitByColumn]?.ToString() ?? "Unknown")) : g;
                            double numSum = filteredRows.Sum(x => (double)numberParser(x.Row[numCol]));
                            double denSum = filteredRows.Sum(x => (double)numberParser(x.Row[denCol]));
                            double avg = denSum != 0 ? numSum / denSum : 0;
                            return (object)new DateTimePoint(g.Key, avg);
                        }).ToList();

                        if (!hasSplit || vals.Any(v => ((DateTimePoint)v).Value > 0))
                        {
                            res.Series.Add(new SeriesDto { Title = cleanTitle, SeriesType = "Line", Values = vals, ColorHex = colorProvider(cleanTitle) });
                        }
                    }
                }
                else
                {
                    foreach (var ser in config.Series.Where(s => !string.IsNullOrEmpty(s.ColumnName) && dt.Columns.Contains(s.ColumnName)))
                    {
                        foreach (var splitGroup in cleanSplitGroups)
                        {
                            var rawValsInGroup = splitGroup.ToList();
                            string cleanTitle = hasSplit ? (config.Series.Count > 1 ? $"{splitGroup.Key} - {ser.ColumnName}" : splitGroup.Key) : ser.ColumnName;

                            var vals = sortedGroups.Select(g =>
                            {
                                var filteredRows = hasSplit ? g.Where(x => rawValsInGroup.Contains(x.Row[config.SplitByColumn]?.ToString() ?? "Unknown")) : g;
                                return (object)new DateTimePoint(g.Key, filteredRows.Sum(x => (double)numberParser(x.Row[ser.ColumnName])));
                            }).ToList();

                            if (!hasSplit || vals.Any(v => ((DateTimePoint)v).Value > 0))
                            {
                                res.Series.Add(new SeriesDto { Title = cleanTitle, SeriesType = "Line", Values = vals, ColorHex = colorProvider(cleanTitle) });
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

                        var vals = sortedGroups.Select(g =>
                        {
                            var filteredRows = hasSplit ? g.Where(x => rawValsInGroup.Contains(x.Row[config.SplitByColumn]?.ToString() ?? "Unknown")) : g;
                            double numSum = filteredRows.Sum(x => (double)numberParser(x.Row[numCol]));
                            double denSum = filteredRows.Sum(x => (double)numberParser(x.Row[denCol]));
                            return (object)(denSum != 0 ? numSum / denSum : 0);
                        }).ToList();

                        if (!hasSplit || vals.Any(v => (double)v > 0))
                        {
                            res.Series.Add(new SeriesDto { Title = cleanTitle, SeriesType = sType, Values = vals, ColorHex = colorProvider(cleanTitle) });
                        }
                    }
                }
                else
                {
                    foreach (var ser in config.Series.Where(s => !string.IsNullOrEmpty(s.ColumnName) && dt.Columns.Contains(s.ColumnName)))
                    {
                        foreach (var splitGroup in cleanSplitGroups)
                        {
                            var rawValsInGroup = splitGroup.ToList();
                            string cleanTitle = hasSplit ? (config.Series.Count > 1 ? $"{splitGroup.Key} - {ser.ColumnName}" : splitGroup.Key) : ser.ColumnName;

                            var vals = sortedGroups.Select(g =>
                            {
                                var filteredRows = hasSplit ? g.Where(x => rawValsInGroup.Contains(x.Row[config.SplitByColumn]?.ToString() ?? "Unknown")) : g;
                                return (object)filteredRows.Sum(x => (double)numberParser(x.Row[ser.ColumnName]));
                            }).ToList();

                            if (!hasSplit || vals.Any(v => (double)v > 0))
                            {
                                res.Series.Add(new SeriesDto { Title = cleanTitle, SeriesType = sType, Values = vals, ColorHex = colorProvider(cleanTitle) });
                            }
                        }
                    }
                }
            }
        }

        private void ProcessPivotCategoricalChart(DataTable dt, DashboardConfiguration config, ChartResultDto res, Func<string, string> colorProvider, Func<object, double> numberParser, bool globalIgnoreHyphen)
        {
            if (!dt.Columns.Contains(config.DateColumn)) return;

            bool isMonthly = config.DataStructureType == "Monthly Date";

            // Group X-Axis by CLEANED labels so Bar Charts merge properly
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
                    PrimarySum = config.Series.Any() && dt.Columns.Contains(config.Series.First().ColumnName) ? g.Sum(x => numberParser(x.Row[config.Series.First().ColumnName])) : 0,
                    MonthIndex = isMonthly ? GetMonthIndex(g.Key) : 99,
                    Rows = g.Select(x => x.Row).ToList()
                })
                .OrderBy(g => isMonthly ? g.MonthIndex : 0)
                .ThenByDescending(g => (!isMonthly && !g.IsDate) ? g.PrimarySum : 0)
                .ThenBy(g => (!isMonthly && g.IsDate) ? g.SortDate.Ticks : 0)
                .ToList();

            int topN = 30;
            var topGroups = grouped.Take(topN).ToList();

            res.XAxisLabels = topGroups.Select(g => g.Key).ToList();
            res.IsDateAxis = false;

            bool hasSplit = !string.IsNullOrEmpty(config.SplitByColumn) && dt.Columns.Contains(config.SplitByColumn);
            List<string> splitCategories = hasSplit
                ? dt.AsEnumerable().Select(r => r[config.SplitByColumn]?.ToString() ?? "Unknown").Distinct().OrderBy(x => x).ToList()
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

                    var vals = topGroups.Select(g =>
                    {
                        var rows = hasSplit ? g.Rows.Where(r => rawValsInGroup.Contains(r[config.SplitByColumn]?.ToString() ?? "Unknown")) : g.Rows;
                        double numSum = rows.Sum(r => numberParser(r[numCol]));
                        double denSum = rows.Sum(r => numberParser(r[denCol]));
                        return (object)(denSum != 0 ? numSum / denSum : 0);
                    }).ToList();

                    if (!hasSplit || vals.Any(v => (double)v > 0))
                    {
                        res.Series.Add(new SeriesDto { Title = cleanTitle, SeriesType = "Line", Values = vals, ColorHex = colorProvider(cleanTitle) });
                    }
                }
            }
            else
            {
                foreach (var ser in config.Series.Where(s => !string.IsNullOrEmpty(s.ColumnName) && dt.Columns.Contains(s.ColumnName)))
                {
                    foreach (var splitGroup in cleanSplitGroups)
                    {
                        var rawValsInGroup = splitGroup.ToList();
                        string cleanTitle = hasSplit ? (config.Series.Count > 1 ? $"{splitGroup.Key} - {ser.ColumnName}" : splitGroup.Key) : ser.ColumnName;

                        var vals = topGroups.Select(g =>
                        {
                            var rows = hasSplit ? g.Rows.Where(r => rawValsInGroup.Contains(r[config.SplitByColumn]?.ToString() ?? "Unknown")) : g.Rows;
                            return (object)rows.Sum(r => numberParser(r[ser.ColumnName]));
                        }).ToList();

                        if (!hasSplit || vals.Any(v => (double)v > 0))
                        {
                            res.Series.Add(new SeriesDto { Title = cleanTitle, SeriesType = baseChartType, Values = vals, ColorHex = colorProvider(cleanTitle) });
                        }
                    }
                }
            }
        }

        private int GetMonthIndex(string monthName)
        {
            if (string.IsNullOrWhiteSpace(monthName)) return 99;
            string m = monthName.Trim().ToLower(new CultureInfo("tr-TR"));
            switch (m)
            {
                case "ocak": case "jan": case "january": case "01": case "1": return 1;
                case "şubat": case "subat": case "feb": case "february": case "02": case "2": return 2;
                case "mart": case "mar": case "march": case "03": case "3": return 3;
                case "nisan": case "apr": case "april": case "04": case "4": return 4;
                case "mayıs": case "mayis": case "may": case "05": case "5": return 5;
                case "haziran": case "jun": case "june": case "06": case "6": return 6;
                case "temmuz": case "jul": case "july": case "07": case "7": return 7;
                case "ağustos": case "agustos": case "aug": case "august": case "08": case "8": return 8;
                case "eylül": case "eylul": case "sep": case "september": case "09": case "9": return 9;
                case "ekim": case "oct": case "october": case "10": return 10;
                case "kasım": case "kasim": case "nov": case "november": case "11": return 11;
                case "aralık": case "aralik": case "dec": case "december": case "12": return 12;
                default: return 99;
            }
        }

        private string FormatCategoryKey(object val)
        {
            if (val == null || val == DBNull.Value) return "Unknown";
            if (val is DateTime dt) return dt.ToString("dd.MM.yyyy");
            string str = val.ToString().Trim();
            if (DateTime.TryParse(str, out DateTime parsed)) return parsed.ToString("dd.MM.yyyy");
            return string.IsNullOrEmpty(str) ? "Unknown" : str;
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