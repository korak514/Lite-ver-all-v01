// Services/InputHelper.cs
using System;
using System.Collections.Generic;
using System.Linq;
using WPF_LoginForm.Models;

namespace WPF_LoginForm.Services
{
    public class ChartDataPoint
    {
        public string Label { get; set; }
        public double Value { get; set; }
        public string ExtraInfo { get; set; } // Stores additional context, e.g., machine code
    }

    public class Page2Stats
    {
        public double TotalErrorDuration { get; set; }
        public double TotalStopDuration { get; set; }

        public string AvgNetStopPerDay { get; set; }
        public string AvgErrorPerDay { get; set; }
        public string SavedMaintenanceTime { get; set; }
        public string AvgWorkingPerDay { get; set; }
        public string AvgGrossStopPerDay { get; set; }
        public string MostFrequentMachine { get; set; }
        public string SavedNonCriticalTime { get; set; }
    }

    public static class InputHelper
    {
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
            // Group by mapped category and calculate total duration per category.
            // Also capture the machine that contributed the most duration to that category.
            var grouped = data
                .GroupBy(x => mappingService.GetMappedCategory(x.ErrorDescription, rules))
                .Select(g => new
                {
                    Category = g.Key,
                    TotalDuration = g.Sum(x => x.DurationMinutes),  // Use Sum for total downtime per category
                    // Machine with the highest total duration in this category
                    TopMachine = g.GroupBy(x => x.MachineCode)
                                  .Select(m => new { Machine = m.Key, Total = m.Sum(x => x.DurationMinutes) })
                                  .OrderByDescending(m => m.Total)
                                  .FirstOrDefault()?.Machine
                })
                .Where(x => !string.IsNullOrEmpty(x.Category)) // Exclude uncategorized if necessary
                .OrderByDescending(x => x.TotalDuration)
                .Take(topN)
                .ToList();

            return grouped.Select(x => new ChartDataPoint
            {
                Label = x.Category,
                Value = x.TotalDuration,
                ExtraInfo = x.TopMachine // Machine code (e.g., "22")
            }).ToList();
        }

        public static List<ChartDataPoint> GetMachineStats(List<ErrorEventModel> data, int topN = 9)
        {
            var grouped = data
                .GroupBy(x => x.MachineCode)
                .Select(g => new ChartDataPoint { Label = g.Key, Value = g.Sum(x => x.DurationMinutes) })
                .OrderByDescending(x => x.Value)
                .ToList();

            var result = grouped.Take(topN).ToList();
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

        public static Page2Stats CalculatePage2Stats(List<ErrorEventModel> data, DateTime start, DateTime end, Func<double, string> formatter, bool excludeBreaks)
        {
            var stats = new Page2Stats();

            // 1. Total Error Duration (Sum of filtered errors)
            stats.TotalErrorDuration = data.Sum(x => x.DurationMinutes);

            // 2. Get Distinct Rows for column-based sums
            var distinctRows = data.GroupBy(x => x.UniqueRowId).Select(g => g.First()).ToList();

            double totalSavedBreak = distinctRows.Sum(x => x.RowSavedTimeBreak);
            double totalSavedMaint = distinctRows.Sum(x => x.RowSavedTimeMaint);
            double rawStopDuration = distinctRows.Sum(x => x.RowTotalStopMinutes);
            double totalActualWork = distinctRows.Sum(x => x.RowActualWorkingMinutes);

            // 3. Logic Correction: Determine "Stops" (Production Loss)
            if (excludeBreaks)
            {
                // Formula: Net Loss = Total Repair Work - Work done during breaks
                double calculatedStops = stats.TotalErrorDuration - totalSavedBreak - totalSavedMaint;

                // Allow Stops to be LESS than Errors (which is the goal)
                stats.TotalStopDuration = calculatedStops > 0 ? calculatedStops : 0;
            }
            else
            {
                // If unfiltered, use the raw stop time from DB
                stats.TotalStopDuration = rawStopDuration;
            }

            // 4. Averages
            double totalDays = (end - start).TotalDays;
            if (totalDays < 1) totalDays = 1;

            double avgErr = stats.TotalErrorDuration / totalDays;
            double avgNetStop = stats.TotalStopDuration / totalDays;
            double avgGrossStop = rawStopDuration / totalDays;
            double avgWork = totalActualWork / totalDays;

            var frequentMachine = data.GroupBy(x => x.MachineCode)
                                      .Select(g => new { Machine = g.Key, Count = g.Count() })
                                      .OrderByDescending(x => x.Count)
                                      .FirstOrDefault();

            // 5. Populate
            stats.AvgNetStopPerDay = formatter(avgNetStop);
            stats.AvgErrorPerDay = formatter(avgErr);
            stats.SavedMaintenanceTime = formatter(totalSavedMaint);
            stats.AvgWorkingPerDay = formatter(avgWork);
            stats.AvgGrossStopPerDay = formatter(avgGrossStop);

            if (frequentMachine != null && frequentMachine.Count > 0)
                stats.MostFrequentMachine = $"MA-{frequentMachine.Machine} ({frequentMachine.Count})";
            else
                stats.MostFrequentMachine = "-";

            stats.SavedNonCriticalTime = formatter(totalSavedBreak);

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