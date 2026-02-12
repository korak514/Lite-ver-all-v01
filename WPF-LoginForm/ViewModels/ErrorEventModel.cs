using System;
using System.Text.RegularExpressions;
using WPF_LoginForm.Properties; // For Resources

namespace WPF_LoginForm.Models
{
    public class ErrorEventModel
    {
        // --- Data Properties ---
        public DateTime Date { get; set; }

        public string Shift { get; set; }
        public string RawData { get; set; }
        public int RowTotalStopMinutes { get; set; } // From DB "Durus" column

        // --- Parsed Properties ---
        public string StartTime { get; set; }      // "08:00"

        public string EndTime { get; set; }        // "08:40"
        public int DurationMinutes { get; set; }   // 40
        public string SectionCode { get; set; }    // "MA"
        public string MachineCode { get; set; }    // "01"
        public string ErrorDescription { get; set; } // "ARIZA DETAYI"

        // --- Formatted for UI ---
        public string TimeRange => $"{StartTime} ➝ {EndTime}";

        public string MachineDisplay => $"MA-{MachineCode}";

        // --- REGEX PATTERN ---
        // Matches: 0800-40-MA-01-RestOfText
        // Group 1: StartTime (4 digits)
        // Group 2: Duration (digits)
        // Group 3: Section (Letters, e.g. MA)
        // Group 4: Machine (Digits or Alphanumeric)
        // Group 5: Description (The rest, including hyphens)
        private static readonly Regex LogPattern = new Regex(
            @"^(\d{4})\s*-\s*(\d+)\s*-\s*([A-Za-z]+)\s*-\s*([A-Za-z0-9]+)\s*-\s*(.*)$",
            RegexOptions.Compiled);

        public static ErrorEventModel Parse(string cellData, DateTime rowDate, string rowShift, int rowTotalStop)
        {
            if (string.IsNullOrWhiteSpace(cellData)) return null;

            var model = new ErrorEventModel
            {
                RawData = cellData,
                Date = rowDate,
                Shift = rowShift,
                RowTotalStopMinutes = rowTotalStop
            };

            // 1. Try Regex Match (Preferred)
            var match = LogPattern.Match(cellData);
            if (match.Success)
            {
                string rawTime = match.Groups[1].Value;
                model.StartTime = FormatTime(rawTime);

                if (int.TryParse(match.Groups[2].Value, out int dur))
                    model.DurationMinutes = dur;

                model.SectionCode = match.Groups[3].Value; // e.g. MA
                model.MachineCode = match.Groups[4].Value; // e.g. 01
                model.ErrorDescription = match.Groups[5].Value.Trim();
            }
            else
            {
                // 2. Fallback: Simple Split (Legacy support for non-standard formats)
                var parts = cellData.Split('-');
                if (parts.Length >= 4)
                {
                    model.StartTime = FormatTime(parts[0]);
                    int.TryParse(parts[1], out int dur);
                    model.DurationMinutes = dur;
                    model.MachineCode = parts[3]; // Assuming index 3 is machine in "Time-Dur-Sec-Mach-Desc"

                    if (parts.Length > 4)
                        model.ErrorDescription = string.Join("-", parts, 4, parts.Length - 4);
                    else
                        model.ErrorDescription = "Unknown Error";
                }
                else
                {
                    return null; // Unparseable format
                }
            }

            // 3. Calculate End Time
            model.EndTime = CalculateEndTime(model.StartTime, model.DurationMinutes);

            return model;
        }

        private static string FormatTime(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "00:00";
            if (raw.Contains(":")) return raw;
            if (raw.Length == 4) return raw.Insert(2, ":"); // 0800 -> 08:00
            if (raw.Length == 3) return "0" + raw.Insert(1, ":"); // 800 -> 08:00
            return raw;
        }

        private static string CalculateEndTime(string startStr, int duration)
        {
            if (TimeSpan.TryParse(startStr, out TimeSpan ts))
            {
                return ts.Add(TimeSpan.FromMinutes(duration)).ToString(@"hh\:mm");
            }
            return "??:??";
        }
    }
}