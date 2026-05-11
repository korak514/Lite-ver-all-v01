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

        public static List<ChartDataPoint> GetTopSingleErrors(List<ErrorEventModel> data, int topN = 5)
        {
            return data
                .GroupBy(x => x.ErrorDescription)
                .Select(g => new ChartDataPoint { Label = g.Key, Value = g.Max(x => x.DurationMinutes) })
                .Where(x => !string.IsNullOrEmpty(x.Label))
                .OrderByDescending(x => x.Value)
                .Take(topN)
                .ToList();
        }

        public static List<ChartDataPoint> GetCategoryStats(List<ErrorEventModel> data, string category, CategoryMappingService mappingService, List<CategoryRule> rules, int topN = 5)
        {
            var grouped = data
                .Where(x => mappingService.GetMappedCategory(x.ErrorDescription, rules).Equals(category, StringComparison.OrdinalIgnoreCase))
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

        // FIX: Added 'strictNoM00Errors' parameter so Cards 1 & 2 can remain completely static regarding MA-00
        public static Page2Stats CalculatePage2Stats(List<ErrorEventModel> allData, List<ErrorEventModel> filteredErrors, List<ErrorEventModel> strictNoM00Errors, DateTime start, DateTime end, Func<double, string> formatter, bool excludeBreaks)
        {
            var stats = new Page2Stats();

            // --- 1. DYNAMIC CALCULATIONS (Used for Pie Charts & Other UI) ---
            stats.TotalErrorDuration = filteredErrors.Sum(x => x.DurationMinutes);

            var distinctRows = allData.GroupBy(x => x.UniqueRowId).Select(g => g.First()).ToList();

            double totalSavedBreak = distinctRows.Sum(x => x.RowSavedTimeBreak);
            double totalSavedMaint = distinctRows.Sum(x => x.RowSavedTimeMaint);
            double rawStopDuration = distinctRows.Sum(x => x.RowTotalStopMinutes);
            double totalActualWork = distinctRows.Sum(x => x.RowActualWorkingMinutes);

            if (excludeBreaks)
            {
                double calculatedStops = stats.TotalErrorDuration - totalSavedBreak - totalSavedMaint;
                stats.TotalStopDuration = calculatedStops > 0 ? calculatedStops : 0;
            }
            else
            {
                stats.TotalStopDuration = rawStopDuration;
            }

            // --- 2. STRICT CALCULATIONS FOR CARDS 1 & 2 (Always Exclude MA-00) ---
            double strictErrorDuration = strictNoM00Errors.Sum(x => x.DurationMinutes);
            // Apply the net-stop formula (Duration - Saved) to the strict duration.
            double strictCalculatedStops = strictErrorDuration - totalSavedBreak - totalSavedMaint;
            double strictTotalStopDuration = strictCalculatedStops > 0 ? strictCalculatedStops : 0;

            // --- 3. AVERAGES ---
            double totalDays = (end.Date - start.Date).TotalDays + 1;
            if (totalDays < 1) totalDays = 1;

            // Cards 1 & 2 (Strict)
            double avgNetStop = strictTotalStopDuration / totalDays;
            double avgErr = strictErrorDuration / totalDays;

            // Cards 4 & 5 (Dynamic / All Data)
            double avgGrossStop = rawStopDuration / totalDays;
            double avgWork = totalActualWork / totalDays;

            // Most Frequent Machine (Dynamic)
            var frequentMachine = filteredErrors.GroupBy(x => x.MachineCode)
                                      .Select(g => new { Machine = g.Key, Count = g.Count() })
                                      .OrderByDescending(x => x.Count)
                                      .FirstOrDefault();

            // --- 4. POPULATE STATS ---
            stats.AvgNetStopPerDay = formatter(avgNetStop);         // Card 1 (Strict)
            stats.AvgErrorPerDay = formatter(avgErr);               // Card 2 (Strict)
            stats.SavedMaintenanceTime = formatter(totalSavedMaint); // Card 3
            stats.AvgWorkingPerDay = formatter(avgWork);            // Card 4
            stats.AvgGrossStopPerDay = formatter(avgGrossStop);     // Card 5

            if (frequentMachine != null && frequentMachine.Count > 0)
                stats.MostFrequentMachine = $"MA-{frequentMachine.Machine} ({frequentMachine.Count})"; // Card 6
            else
                stats.MostFrequentMachine = "-";

            stats.SavedNonCriticalTime = formatter(totalSavedBreak); // Card 7

            return stats;
        }

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