using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using WPF_LoginForm.Models;

namespace WPF_LoginForm.Services
{
    public static class DashboardChartBuilder
    {
        public class ChartBuildResult
        {
            public SeriesCollection Series { get; set; } = new SeriesCollection();
            public AxesCollection XAxes { get; set; } = new AxesCollection();
            public AxesCollection YAxes { get; set; } = new AxesCollection();
        }

        public static ChartBuildResult BuildChart(DataTable table, DashboardConfiguration config, bool isFilterByDate, double startRatio, double endRatio)
        {
            var result = new ChartBuildResult();
            if (table == null || table.Rows.Count == 0) return result;

            try
            {
                // 1. Data Parsing Helper (TR/EN Decimal Fix)
                Func<object, double> parseDouble = (obj) =>
                {
                    if (obj == null || obj == DBNull.Value) return 0.0;
                    string val = obj.ToString().Replace(',', '.');
                    return double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out double res) ? res : 0.0;
                };

                // 2. Setup Y-Axis (CRITICAL: DisableAnimations = true)
                if (config.ChartType != "Pie")
                {
                    result.YAxes.Add(new Axis
                    {
                        Title = "Values",
                        Foreground = Brushes.WhiteSmoke,
                        DisableAnimations = true // FIX: Prevents layout animation crashes
                    });
                }

                // 3. Generate Series based on Chart Type
                if (config.ChartType == "Pie")
                {
                    foreach (var s in config.Series)
                    {
                        if (!table.Columns.Contains(s.ColumnName)) continue;
                        double total = table.AsEnumerable().Sum(r => parseDouble(r[s.ColumnName]));
                        result.Series.Add(new PieSeries { Title = s.ColumnName, Values = new ChartValues<double> { total }, DataLabels = true });
                    }
                }
                else if (config.DataStructureType == "Daily Date" && !string.IsNullOrEmpty(config.DateColumn))
                {
                    ProcessTimeData(table, config, result, parseDouble);
                }
                else
                {
                    ProcessCategoricalData(table, config, result, parseDouble);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Chart Logic Error: {ex.Message}");
            }

            return result;
        }

        private static void ProcessTimeData(DataTable dt, DashboardConfiguration config, ChartBuildResult result, Func<object, double> parser)
        {
            var points = dt.AsEnumerable()
                .Where(r => r[config.DateColumn] is DateTime)
                .Select(r => new { Date = (DateTime)r[config.DateColumn], Row = r })
                .OrderBy(x => x.Date).ToList();

            if (!points.Any()) return;

            // X-Axis Setup (DisableAnimations is key here)
            var xAxis = new Axis { Foreground = Brushes.WhiteSmoke, DisableAnimations = true };

            if (config.ChartType == "Line")
            {
                foreach (var s in config.Series)
                {
                    if (!dt.Columns.Contains(s.ColumnName)) continue;
                    var vals = new ChartValues<DateTimePoint>(points.Select(p => new DateTimePoint(p.Date, parser(p.Row[s.ColumnName]))));
                    result.Series.Add(new LineSeries { Title = s.ColumnName, Values = vals, PointGeometry = null });
                }
                xAxis.LabelFormatter = val => new DateTime((long)val).ToString("dd/MM");
            }
            else // Bar Chart
            {
                xAxis.Labels = points.Select(p => p.Date.ToString("dd/MM")).ToList();
                foreach (var s in config.Series)
                {
                    if (!dt.Columns.Contains(s.ColumnName)) continue;
                    var vals = new ChartValues<double>(points.Select(p => parser(p.Row[s.ColumnName])));
                    result.Series.Add(new ColumnSeries { Title = s.ColumnName, Values = vals });
                }
            }
            result.XAxes.Add(xAxis);
        }

        private static void ProcessCategoricalData(DataTable dt, DashboardConfiguration config, ChartBuildResult result, Func<object, double> parser)
        {
            var labels = dt.AsEnumerable().Select(r => r[config.DateColumn]?.ToString() ?? "").ToList();
            var xAxis = new Axis { Labels = labels, Foreground = Brushes.WhiteSmoke, DisableAnimations = true };

            foreach (var s in config.Series)
            {
                if (!dt.Columns.Contains(s.ColumnName)) continue;
                var vals = new ChartValues<double>(dt.AsEnumerable().Select(r => parser(r[s.ColumnName])));
                if (config.ChartType == "Line") result.Series.Add(new LineSeries { Title = s.ColumnName, Values = vals });
                else result.Series.Add(new ColumnSeries { Title = s.ColumnName, Values = vals });
            }
            result.XAxes.Add(xAxis);
        }
    }
}