using System;
using System.Collections.Generic;
using System.Linq;
using WPF_LoginForm.Models;

namespace WPF_LoginForm.Services
{
    public static class InputHelper
    {
        public class ChartDataPoint
        {
            public string Label { get; set; }
            public double Value { get; set; }
        }

        public class Page2Stats
        {
            public double TotalErrorDuration { get; set; }
            public double TotalStopDuration { get; set; }
            public string DailyAvgStop { get; set; }
            public string DailyAvgError { get; set; }
            public string SavedBreakTime { get; set; }
            public string SavedMaintenanceTime { get; set; }

            // Placeholders
            public string ExtraStat1 { get; set; }

            public string ExtraStat2 { get; set; }
            public string ExtraStat3 { get; set; }
            public string ExtraStat4 { get; set; }
        }

        public static List<ErrorEventModel> PreProcessData(List<ErrorEventModel> rawData, bool excludeMachine00)
        {
            if (rawData == null) return new List<ErrorEventModel>();
            var query = rawData.Where(x => !string.IsNullOrWhiteSpace(x.ErrorDescription));
            if (excludeMachine00)
            {
                // Filter out generic cleaning codes often labeled as machine 00
                query = query.Where(x => x.MachineCode != "00" && x.MachineCode != "0" && x.MachineCode != "MA-00");
            }
            return query.ToList();
        }

        public static List<ChartDataPoint> GetTopReasons(List<ErrorEventModel> data, CategoryMappingService mappingService, List<CategoryRule> rules, int topN = 5)
        {
            return data
                .GroupBy(x => mappingService.GetMappedCategory(x.ErrorDescription, rules))
                .Select(g => new ChartDataPoint { Label = g.Key, Value = g.Max(x => x.DurationMinutes) })
                .OrderByDescending(x => x.Value)
                .Take(topN)
                .ToList();
        }

        // --- TASK 1: "Others" Grouping Implemented Here ---
        public static List<ChartDataPoint> GetMachineStats(List<ErrorEventModel> data, int topN = 9)
        {
            // 1. Group and Sum
            var grouped = data
                .GroupBy(x => x.MachineCode)
                .Select(g => new ChartDataPoint { Label = g.Key, Value = g.Sum(x => x.DurationMinutes) })
                .OrderByDescending(x => x.Value)
                .ToList();

            // 2. Take Top N
            var result = grouped.Take(topN).ToList();

            // 3. Sum the rest into "Others"
            var othersSum = grouped.Skip(topN).Sum(x => x.Value);

            if (othersSum > 0)
            {
                result.Add(new ChartDataPoint { Label = "Others", Value = othersSum });
            }

            return result;
        }

        public static List<ChartDataPoint> GetCategoryStats(List<ErrorEventModel> data, string category, CategoryMappingService mappingService, List<CategoryRule> rules, int topN = 9)
        {
            return data
                .Where(x => mappingService.GetMappedCategory(x.ErrorDescription, rules).Equals(category, StringComparison.OrdinalIgnoreCase))
                .GroupBy(x => x.MachineCode)
                .Select(g => new ChartDataPoint { Label = g.Key, Value = g.Sum(x => x.DurationMinutes) })
                .OrderByDescending(x => x.Value)
                .Take(topN)
                .ToList();
        }

        // --- TASK 3: Real Data Binding ---
        public static Page2Stats CalculatePage2Stats(List<ErrorEventModel> data, DateTime start, DateTime end, Func<double, string> formatter)
        {
            var stats = new Page2Stats();

            // 1. Total Error Duration (Sum of individual errors)
            stats.TotalErrorDuration = data.Sum(x => x.DurationMinutes);

            // 2. Group by UniqueRowId to sum Shift Totals ONLY ONCE per row
            // (Because 1 row has 1 Stop Time, but might have 5 errors. We don't want to sum Stop Time 5 times)
            var distinctRows = data.GroupBy(x => x.UniqueRowId).Select(g => g.First()).ToList();

            // 3. Real Sums from distinct rows
            stats.TotalStopDuration = distinctRows.Sum(x => x.RowTotalStopMinutes);
            double totalSavedBreak = distinctRows.Sum(x => x.RowSavedTimeBreak);
            double totalSavedMaint = distinctRows.Sum(x => x.RowSavedTimeMaint);

            // Sanity Check
            if (stats.TotalStopDuration < stats.TotalErrorDuration)
                stats.TotalStopDuration = stats.TotalErrorDuration;

            double totalDays = (end - start).TotalDays;
            if (totalDays < 1) totalDays = 1;

            double avgErr = stats.TotalErrorDuration / totalDays;
            double avgStop = stats.TotalStopDuration / totalDays;

            // 4. Format Output
            stats.DailyAvgError = formatter(avgErr);
            stats.DailyAvgStop = formatter(avgStop);

            // --- NO MORE MOCK DATA ---
            stats.SavedBreakTime = formatter(totalSavedBreak);
            stats.SavedMaintenanceTime = formatter(totalSavedMaint);

            // Placeholders
            stats.ExtraStat1 = "-";
            stats.ExtraStat2 = "-";
            stats.ExtraStat3 = "-";
            stats.ExtraStat4 = "-";

            return stats;
        }

        public static List<ChartDataPoint> GetShiftStats(List<ErrorEventModel> data)
        {
            return data
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Shift) ? "Unknown" : x.Shift)
                .Select(g => new ChartDataPoint { Label = g.Key, Value = g.Sum(x => x.DurationMinutes) })
                .OrderByDescending(x => x.Value)
                .ToList();
        }

        public static List<ChartDataPoint> GetSeverityStats(List<ErrorEventModel> data)
        {
            var list = new List<ChartDataPoint>();
            double micro = data.Where(x => x.DurationMinutes < 5).Sum(x => x.DurationMinutes);
            if (micro > 0) list.Add(new ChartDataPoint { Label = "Micro (<5m)", Value = micro });

            double minor = data.Where(x => x.DurationMinutes >= 5 && x.DurationMinutes <= 30).Sum(x => x.DurationMinutes);
            if (minor > 0) list.Add(new ChartDataPoint { Label = "Minor (5-30m)", Value = minor });

            double major = data.Where(x => x.DurationMinutes > 30).Sum(x => x.DurationMinutes);
            if (major > 0) list.Add(new ChartDataPoint { Label = "Major (>30m)", Value = major });

            return list;
        }
    }
}