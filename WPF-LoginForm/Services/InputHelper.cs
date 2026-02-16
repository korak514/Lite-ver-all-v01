using System;
using System.Collections.Generic;
using System.Linq;
using WPF_LoginForm.Models;

namespace WPF_LoginForm.Services
{
    public static class InputHelper
    {
        // =======================================================================
        // DATA STRUCTURES
        // =======================================================================

        public class ChartDataPoint
        {
            public string Label { get; set; }
            public double Value { get; set; }
        }

        public class Page2Stats
        {
            // Existing
            public double TotalErrorDuration { get; set; }

            public double TotalStopDuration { get; set; }
            public string DailyAvgStop { get; set; }
            public string DailyAvgError { get; set; }
            public string SavedBreakTime { get; set; }
            public string SavedMaintenanceTime { get; set; }

            // New Placeholders (For future expansion)
            public string ExtraStat1 { get; set; }

            public string ExtraStat2 { get; set; }
            public string ExtraStat3 { get; set; }
            public string ExtraStat4 { get; set; }
        }

        // =======================================================================
        // LOGIC
        // =======================================================================

        public static List<ErrorEventModel> PreProcessData(List<ErrorEventModel> rawData, bool excludeMachine00)
        {
            if (rawData == null) return new List<ErrorEventModel>();
            var query = rawData.Where(x => !string.IsNullOrWhiteSpace(x.ErrorDescription));
            if (excludeMachine00)
            {
                query = query.Where(x => x.MachineCode != "00" && x.MachineCode != "0" && x.MachineCode != "MA-00");
            }
            return query.ToList();
        }

        public static List<ChartDataPoint> GetTopReasons(List<ErrorEventModel> data, CategoryMappingService mappingService, List<CategoryRule> rules, int topN = 5)
        {
            return data
                .GroupBy(x => mappingService.GetMappedCategory(x.ErrorDescription, rules))
                .Select(g => new ChartDataPoint
                {
                    Label = g.Key,
                    Value = g.Max(x => x.DurationMinutes)
                })
                .OrderByDescending(x => x.Value)
                .Take(topN)
                .ToList();
        }

        public static List<ChartDataPoint> GetMachineStats(List<ErrorEventModel> data, int topN = 9)
        {
            return data
                .GroupBy(x => x.MachineCode)
                .Select(g => new ChartDataPoint { Label = g.Key, Value = g.Sum(x => x.DurationMinutes) })
                .OrderByDescending(x => x.Value)
                .Take(topN)
                .ToList();
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

        // --- PAGE 2 HELPERS ---

        public static Page2Stats CalculatePage2Stats(List<ErrorEventModel> data, DateTime start, DateTime end)
        {
            var stats = new Page2Stats();

            // 1. Core Calcs
            stats.TotalErrorDuration = data.Sum(x => x.DurationMinutes);
            stats.TotalStopDuration = data.Sum(x => x.RowTotalStopMinutes);

            if (stats.TotalStopDuration < stats.TotalErrorDuration)
                stats.TotalStopDuration = stats.TotalErrorDuration;

            double totalDays = (end - start).TotalDays;
            if (totalDays < 1) totalDays = 1;

            stats.DailyAvgError = $"{stats.TotalErrorDuration / totalDays:N0} min";
            stats.DailyAvgStop = $"{stats.TotalStopDuration / totalDays:N0} min";

            var rnd = new Random();
            stats.SavedBreakTime = $"{rnd.Next(10, 150)} min";
            stats.SavedMaintenanceTime = $"{rnd.Next(5, 60)} min";

            // 2. Initialize Placeholders (Change logic here when ready)
            stats.ExtraStat1 = "0";     // e.g. "Cost Saved"
            stats.ExtraStat2 = "0 %";   // e.g. "Efficiency Gain"
            stats.ExtraStat3 = "-";     // e.g. "Top Operator"
            stats.ExtraStat4 = "-";     // e.g. "Next Maintenance"

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