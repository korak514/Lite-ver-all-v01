// Services/DashboardChartService.cs
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
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

        // --- DTOs (Data Transfer Objects) ---
        // These are safe to create on background threads
        public class ChartResultDto
        {
            public List<SeriesDto> Series { get; set; } = new List<SeriesDto>();
            public List<string> XAxisLabels { get; set; }
            public bool IsDateAxis { get; set; }
        }

        public class SeriesDto
        {
            public string Title { get; set; }
            public string SeriesType { get; set; } // "Line", "Bar", "Pie"
            public List<object> Values { get; set; } // Can hold double or DateTimePoint
            public string ColorHex { get; set; }
        }

        public ChartResultDto ProcessChartData(DataTable dataTable, DashboardConfiguration config,
                                               bool isFilterByDate, bool ignoreNonDateData,
                                               double sliderStart, double sliderEnd, double sliderMax,
                                               Dictionary<(int, string), string> colorMap)
        {
            var result = new ChartResultDto();
            if (dataTable == null) return result;

            // 1. Filter Rows (Ignore Last N)
            if (config.RowsToIgnore > 0 && dataTable.Rows.Count > 0)
            {
                int rowsToRemove = Math.Min(config.RowsToIgnore, dataTable.Rows.Count);
                for (int i = 0; i < rowsToRemove; i++)
                    dataTable.Rows.RemoveAt(dataTable.Rows.Count - 1);
            }

            // 2. Filter Null Dates
            if (ignoreNonDateData && !string.IsNullOrEmpty(config.DateColumn) && dataTable.Columns.Contains(config.DateColumn))
            {
                for (int i = dataTable.Rows.Count - 1; i >= 0; i--)
                {
                    if (dataTable.Rows[i][config.DateColumn] == DBNull.Value)
                        dataTable.Rows.RemoveAt(i);
                }
            }

            // 3. Apply Slider Filter
            bool isDateBasedChart = config.DataStructureType == "Daily Date" && !string.IsNullOrEmpty(config.DateColumn);
            if (!isDateBasedChart || !isFilterByDate)
            {
                ApplySliderFilter(ref dataTable, sliderStart, sliderEnd, sliderMax);
            }

            // 4. Conversion Helpers
            Func<object, double> safeConvertToDouble = (obj) =>
            {
                if (obj == null || obj == DBNull.Value) return 0.0;
                var culture = config.UseInvariantCultureForNumbers ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture;
                double.TryParse(obj.ToString(), NumberStyles.Any, culture, out double res);
                return res;
            };

            string GetColor(string title)
            {
                if (colorMap != null && colorMap.TryGetValue((config.ChartPosition, title), out string hex))
                    return hex;
                // Generate random color hex safely
                lock (_randomLock)
                {
                    var color = Color.FromRgb((byte)_random.Next(80, 200), (byte)_random.Next(80, 200), (byte)_random.Next(80, 200));
                    return color.ToString();
                }
            }

            // 5. Generate Data
            if (string.Equals(config.ChartType, "Pie", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var seriesConfig in config.Series.Where(s => !string.IsNullOrEmpty(s.ColumnName) && dataTable.Columns.Contains(s.ColumnName)))
                {
                    double total = dataTable.AsEnumerable().Sum(row => safeConvertToDouble(row[seriesConfig.ColumnName]));
                    result.Series.Add(new SeriesDto
                    {
                        Title = seriesConfig.ColumnName,
                        SeriesType = "Pie",
                        Values = new List<object> { total },
                        ColorHex = GetColor(seriesConfig.ColumnName)
                    });
                }
            }
            else
            {
                switch (config.DataStructureType)
                {
                    case "Daily Date":
                        ProcessDailyDateChart(dataTable, config, result, GetColor, safeConvertToDouble);
                        break;

                    case "Monthly Date":
                        ProcessCategoricalChart(dataTable, config, result, GetColor, safeConvertToDouble, SortDataByMonth);
                        break;

                    case "ID":
                    case "General":
                    default:
                        ProcessCategoricalChart(dataTable, config, result, GetColor, safeConvertToDouble, null);
                        break;
                }
            }

            return result;
        }

        private void ApplySliderFilter(ref DataTable dt, double start, double end, double max)
        {
            if (max <= 0) return;
            double ratioStart = start / max;
            double ratioEnd = end / max;
            int totalRows = dt.Rows.Count;
            if (totalRows == 0) return;

            int startIndex = (int)(totalRows * ratioStart);
            int endIndex = (int)(totalRows * ratioEnd);

            if (startIndex < 0) startIndex = 0;
            if (endIndex >= totalRows) endIndex = totalRows - 1;
            if (startIndex > endIndex) startIndex = endIndex;

            var rowsToKeep = new List<DataRow>();
            for (int i = startIndex; i <= endIndex; i++) rowsToKeep.Add(dt.Rows[i]);

            DataTable filtered = dt.Clone();
            foreach (var r in rowsToKeep) filtered.ImportRow(r);
            dt = filtered;
        }

        private void ProcessDailyDateChart(DataTable dt, DashboardConfiguration config, ChartResultDto res, Func<string, string> colorProvider, Func<object, double> numberParser)
        {
            if (!dt.Columns.Contains(config.DateColumn)) return;

            var points = dt.AsEnumerable()
                .Select(r => new { Date = (DateTime)r[config.DateColumn], Row = r })
                .OrderBy(p => p.Date)
                .ToList();

            if (config.AggregationType != "Daily")
            {
                IEnumerable<IGrouping<string, dynamic>> groups;
                if (config.AggregationType == "Weekly")
                    groups = points.GroupBy(p => GetStartOfWeek(p.Date).ToString("d"));
                else
                    groups = points.GroupBy(p => new DateTime(p.Date.Year, p.Date.Month, 1).ToString("MMM yyyy"));

                res.XAxisLabels = groups.Select(g => g.Key).ToList();
                res.IsDateAxis = false;

                foreach (var ser in config.Series.Where(s => !string.IsNullOrEmpty(s.ColumnName) && dt.Columns.Contains(s.ColumnName)))
                {
                    var vals = groups.Select(g => g.Sum(x => (double)numberParser(x.Row[ser.ColumnName]))).Cast<object>().ToList();
                    res.Series.Add(new SeriesDto { Title = ser.ColumnName, SeriesType = config.ChartType, Values = vals, ColorHex = colorProvider(ser.ColumnName) });
                }
            }
            else
            {
                if (config.ChartType == "Line")
                {
                    res.IsDateAxis = true;
                    foreach (var ser in config.Series.Where(s => !string.IsNullOrEmpty(s.ColumnName) && dt.Columns.Contains(s.ColumnName)))
                    {
                        var vals = points.Select(p => (object)new DateTimePoint(p.Date, numberParser(p.Row[ser.ColumnName]))).ToList();
                        res.Series.Add(new SeriesDto { Title = ser.ColumnName, SeriesType = "Line", Values = vals, ColorHex = colorProvider(ser.ColumnName) });
                    }
                }
                else
                {
                    res.IsDateAxis = false;
                    res.XAxisLabels = points.Select(p => p.Date.ToString("d")).ToList();
                    foreach (var ser in config.Series.Where(s => !string.IsNullOrEmpty(s.ColumnName) && dt.Columns.Contains(s.ColumnName)))
                    {
                        var vals = points.Select(p => (object)numberParser(p.Row[ser.ColumnName])).ToList();
                        res.Series.Add(new SeriesDto { Title = ser.ColumnName, SeriesType = "Bar", Values = vals, ColorHex = colorProvider(ser.ColumnName) });
                    }
                }
            }
        }

        private void ProcessCategoricalChart(DataTable dt, DashboardConfiguration config, ChartResultDto res, Func<string, string> colorProvider, Func<object, double> numberParser, Func<IEnumerable<DataRow>, string, IEnumerable<DataRow>> sorter)
        {
            if (!dt.Columns.Contains(config.DateColumn)) return;

            IEnumerable<DataRow> rows = dt.AsEnumerable();
            if (sorter != null) rows = sorter(rows, config.DateColumn);

            res.XAxisLabels = rows.Select(r => r[config.DateColumn]?.ToString()).ToList();
            res.IsDateAxis = false;

            foreach (var ser in config.Series.Where(s => !string.IsNullOrEmpty(s.ColumnName) && dt.Columns.Contains(s.ColumnName)))
            {
                var vals = rows.Select(r => (object)numberParser(r[ser.ColumnName])).ToList();
                res.Series.Add(new SeriesDto { Title = ser.ColumnName, SeriesType = config.ChartType, Values = vals, ColorHex = colorProvider(ser.ColumnName) });
            }
        }

        private IEnumerable<DataRow> SortDataByMonth(IEnumerable<DataRow> data, string monthCol)
        {
            var culture = CultureInfo.CurrentCulture;
            var monthNames = culture.DateTimeFormat.MonthNames.Take(12).Select(m => m.ToLower(culture)).ToList();
            return data.OrderBy(r =>
            {
                string m = r[monthCol]?.ToString()?.ToLower(culture) ?? "";
                int idx = monthNames.IndexOf(m);
                return idx == -1 ? 99 : idx;
            });
        }

        private DateTime GetStartOfWeek(DateTime dt) => dt.AddDays(-1 * ((7 + (dt.DayOfWeek - DayOfWeek.Monday)) % 7)).Date;
    }
}