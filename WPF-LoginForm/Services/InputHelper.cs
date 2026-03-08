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

            // Ignore dummy NO_ERROR entries for actual error analysis
            var query = rawData.Where(x => !string.IsNullOrWhiteSpace(x.ErrorDescription) && x.ErrorDescription != "NO_ERROR");

            if (excludeMachine00)
            {
                query = query.Where(x => x.MachineCode != "00" && x.MachineCode != "0" && x.MachineCode != "MA-00");
            }
            return query.ToList();
        }

        public static List<ChartDataPoint> GetTopReasons(List<ErrorEventModel> data, CategoryMappingService mappingService, List<CategoryRule> rules, int topN = 5)
        {
            var grouped = data
                .GroupBy(x => mappingService.GetMappedCategory(x.ErrorDescription, rules))
                .Select(g => new
                {
                    Category = g.Key,
                    TotalDuration = g.Sum(x => x.DurationMinutes),
                    TopMachine = g.GroupBy(x => x.MachineCode)
                                  .Select(m => new { Machine = m.Key, Total = m.Sum(x => x.DurationMinutes) })
                                  .OrderByDescending(m => m.Total)
                                  .FirstOrDefault()?.Machine
                })
                .Where(x => !string.IsNullOrEmpty(x.Category))
                .OrderByDescending(x => x.TotalDuration)
                .Take(topN)
                .ToList();

            return grouped.Select(x => new ChartDataPoint
            {
                Label = x.Category,
                Value = x.TotalDuration,
                ExtraInfo = x.TopMachine
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

        // FIX: Now requires both ALL Data and FILTERED Data to accurately calculate Working Hours across empty shifts
        public static Page2Stats CalculatePage2Stats(List<ErrorEventModel> allData, List<ErrorEventModel> filteredErrors, DateTime start, DateTime end, Func<double, string> formatter, bool excludeBreaks)
        {
            var stats = new Page2Stats();

            // 1. Total Error Duration (Sum of filtered errors only)
            stats.TotalErrorDuration = filteredErrors.Sum(x => x.DurationMinutes);

            // 2. Get Distinct Rows for column-based sums from ALL Data (To include shifts that had 0 errors)
            var distinctRows = allData.GroupBy(x => x.UniqueRowId).Select(g => g.First()).ToList();

            double totalSavedBreak = distinctRows.Sum(x => x.RowSavedTimeBreak);
            double totalSavedMaint = distinctRows.Sum(x => x.RowSavedTimeMaint);
            double rawStopDuration = distinctRows.Sum(x => x.RowTotalStopMinutes);
            double totalActualWork = distinctRows.Sum(x => x.RowActualWorkingMinutes);

            // 3. Logic Correction: Determine "Stops" (Production Loss)
            if (excludeBreaks)
            {
                double calculatedStops = stats.TotalErrorDuration - totalSavedBreak - totalSavedMaint;
                stats.TotalStopDuration = calculatedStops > 0 ? calculatedStops : 0;
            }
            else
            {
                stats.TotalStopDuration = rawStopDuration;
            }

            // 4. Averages
            // FIX: Dates are inclusive! (e.g. 21.02 to 22.02 is TWO days, not 1)
            double totalDays = (end.Date - start.Date).TotalDays + 1;
            if (totalDays < 1) totalDays = 1;

            double avgErr = stats.TotalErrorDuration / totalDays;
            double avgNetStop = stats.TotalStopDuration / totalDays;
            double avgGrossStop = rawStopDuration / totalDays;
            double avgWork = totalActualWork / totalDays;

            var frequentMachine = filteredErrors.GroupBy(x => x.MachineCode)
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

        // FIX: Now constructs Shift List from ALL data, then maps durations from FILTERED data
        public static List<ChartDataPoint> GetShiftStats(List<ErrorEventModel> allData, List<ErrorEventModel> filteredErrors)
        {
            var distinctDates = allData.Select(x => x.Date.Date).Distinct().Count();

            // If looking at a short timeframe (<= 7 days), break it down into explicit shifts per day
            if (distinctDates > 0 && distinctDates <= 7)
            {
                var allShifts = allData.GroupBy(x => new { Date = x.Date.Date, Shift = string.IsNullOrWhiteSpace(x.Shift) ? "Unknown" : x.Shift.Trim() })
                    .Select(g => new { g.Key.Date, g.Key.Shift })
                    .ToList();

                var result = new List<ChartDataPoint>();
                foreach (var s in allShifts)
                {
                    double duration = filteredErrors.Where(x => x.Date.Date == s.Date && (string.IsNullOrWhiteSpace(x.Shift) ? "Unknown" : x.Shift.Trim()) == s.Shift)
                                                    .Sum(x => x.DurationMinutes);

                    result.Add(new ChartDataPoint
                    {
                        Label = $"{s.Date:dd.MM} {s.Shift}",
                        Value = duration
                    });
                }

                return result.OrderBy(x => x.Label).ToList();
            }

            // Large timeframe: Aggregated generally
            return allData
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Shift) ? "Unknown" : x.Shift.Trim())
                .Select(g => new ChartDataPoint
                {
                    Label = g.Key,
                    Value = filteredErrors.Where(f => (string.IsNullOrWhiteSpace(f.Shift) ? "Unknown" : f.Shift.Trim()) == g.Key).Sum(f => f.DurationMinutes)
                })
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