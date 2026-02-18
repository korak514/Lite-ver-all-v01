using System;
using System.Text.RegularExpressions;

namespace WPF_LoginForm.Models
{
    public class ErrorEventModel
    {
        // --- Identity ---
        // Used to sum Shift-wide totals (like Total Stop Time) only once per row,
        // even if the row has 10 errors.
        public string UniqueRowId { get; set; }

        // --- Data Properties ---
        public DateTime Date { get; set; }

        public string Shift { get; set; } // "Vardiya"
        public string RawData { get; set; }

        // --- Metric Columns from Image ---
        public double RowTotalStopMinutes { get; set; } // "Duraklama_Süresi"

        public double RowSavedTimeBreak { get; set; }   // Col G: "Duruşu Engelemeyen..."
        public double RowSavedTimeMaint { get; set; }   // Col H: "Mola-Bakım..."

        // --- Parsed Properties ---
        public string StartTime { get; set; }

        public string EndTime { get; set; }
        public int DurationMinutes { get; set; }
        public string SectionCode { get; set; }
        public string MachineCode { get; set; }
        public string ErrorDescription { get; set; }

        public string TimeRange => $"{StartTime} ➝ {EndTime}";
        public string MachineDisplay => $"MA-{MachineCode}";

        private static readonly Regex LogPattern = new Regex(
            @"^(\d{4})\s*-\s*(\d+)\s*-\s*([A-Za-z]+)\s*-\s*([A-Za-z0-9]+)\s*-\s*(.*)$",
            RegexOptions.Compiled);

        public static ErrorEventModel Parse(string cellData, DateTime rowDate, string rowShift, double totalStop, double savedBreak, double savedMaint, string uniqueId)
        {
            if (string.IsNullOrWhiteSpace(cellData)) return null;

            var model = new ErrorEventModel
            {
                RawData = cellData,
                Date = rowDate,
                Shift = rowShift,
                RowTotalStopMinutes = totalStop,
                RowSavedTimeBreak = savedBreak,
                RowSavedTimeMaint = savedMaint,
                UniqueRowId = uniqueId
            };

            // Parse Logic: 0800-43-MA-00-GENEL-TEMİZLİK
            var match = LogPattern.Match(cellData);
            if (match.Success)
            {
                model.StartTime = FormatTime(match.Groups[1].Value);
                if (int.TryParse(match.Groups[2].Value, out int dur)) model.DurationMinutes = dur;
                model.SectionCode = match.Groups[3].Value;
                model.MachineCode = match.Groups[4].Value;
                model.ErrorDescription = match.Groups[5].Value.Trim();
            }
            else
            {
                // Fallback split
                var parts = cellData.Split('-');
                if (parts.Length >= 4)
                {
                    model.StartTime = FormatTime(parts[0]);
                    int.TryParse(parts[1], out int dur);
                    model.DurationMinutes = dur;
                    model.MachineCode = parts[3];
                    if (parts.Length > 4) model.ErrorDescription = string.Join("-", parts, 4, parts.Length - 4);
                }
                else return null;
            }

            model.EndTime = CalculateEndTime(model.StartTime, model.DurationMinutes);
            return model;
        }

        private static string FormatTime(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "00:00";
            if (raw.Contains(":")) return raw;
            if (raw.Length == 4) return raw.Insert(2, ":");
            return raw;
        }

        private static string CalculateEndTime(string startStr, int duration)
        {
            if (TimeSpan.TryParse(startStr, out TimeSpan ts))
                return ts.Add(TimeSpan.FromMinutes(duration)).ToString(@"hh\:mm");
            return "??:??";
        }
    }
}