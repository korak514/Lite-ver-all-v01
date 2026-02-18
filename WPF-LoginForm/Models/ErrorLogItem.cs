using System;
using System.Data;
using WPF_LoginForm.Properties; // Required for Resources

namespace WPF_LoginForm.Models
{
    public class ErrorLogItem
    {
        // --- Context Data (From DB Columns) ---
        public DateTime Date { get; set; }

        public string Shift { get; set; }
        public string Operator { get; set; }

        // --- Parsed Data ---
        public string StartTime { get; set; }  // e.g. "04:35"

        public string EndTime { get; set; }    // e.g. "04:38"
        public int DurationMinutes { get; set; } // Raw integer value
        public string MachineCode { get; set; }
        public string ErrorMessage { get; set; }

        // --- Formatting for UI ---
        public string TimeRange => $"{StartTime} ➝ {EndTime}";

        // Legacy static text (e.g. "40 min")
        public string DurationText => $"{DurationMinutes} {Resources.Unit_Minutes}";

        // NEW: Dynamic formatted text (set by ViewModel based on Min/Clock setting)
        public string DisplayDuration { get; set; }

        // --- Parsing Logic (Legacy Support for other views) ---
        public static ErrorLogItem Parse(DataRow row, string rawCodeColumnName = "Code")
        {
            var item = new ErrorLogItem();

            // 1. Get Context
            if (row.Table.Columns.Contains("Date") && row["Date"] != DBNull.Value)
                item.Date = Convert.ToDateTime(row["Date"]);

            if (row.Table.Columns.Contains("Shift"))
                item.Shift = row["Shift"]?.ToString();

            // 2. Parse Raw String: "0435-3-MA-01-YUKSEK-AMPER-HATASI"
            string raw = "";
            if (row.Table.Columns.Contains(rawCodeColumnName))
                raw = row[rawCodeColumnName]?.ToString();

            if (string.IsNullOrEmpty(raw)) return item;

            var parts = raw.Split('-');
            if (parts.Length >= 2)
            {
                // Part 1: Start Time
                string rawTime = parts[0];
                if (rawTime.Length == 4 && !rawTime.Contains(":"))
                    item.StartTime = $"{rawTime.Substring(0, 2)}:{rawTime.Substring(2, 2)}";
                else
                    item.StartTime = rawTime;

                // Part 2: Duration
                if (int.TryParse(parts[1], out int dur))
                    item.DurationMinutes = dur;

                // Calculate End Time
                if (TimeSpan.TryParse(item.StartTime, out TimeSpan tsStart))
                {
                    TimeSpan tsEnd = tsStart.Add(TimeSpan.FromMinutes(item.DurationMinutes));
                    item.EndTime = tsEnd.ToString(@"hh\:mm");
                }
                else
                {
                    item.EndTime = "?";
                }

                // Part 3 & 4: Machine & Error
                if (parts.Length > 3 && parts[2] == "MA")
                {
                    item.MachineCode = $"MA-{parts[3]}";
                    if (parts.Length > 4)
                        item.ErrorMessage = string.Join("-", parts, 4, parts.Length - 4);
                }
                else if (parts.Length > 2)
                {
                    item.MachineCode = parts[2];
                    if (parts.Length > 3)
                        item.ErrorMessage = string.Join("-", parts, 3, parts.Length - 3);
                }
            }
            else
            {
                item.ErrorMessage = raw;
            }

            // Default DisplayDuration to standard text if not set later
            item.DisplayDuration = item.DurationText;

            return item;
        }
    }
}