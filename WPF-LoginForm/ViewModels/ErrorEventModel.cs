using System;
using System.Linq;

namespace WPF_LoginForm.Models
{
    public class ErrorEventModel
    {
        public string RawData { get; set; }
        public string StartTime { get; set; }
        public int DurationMinutes { get; set; } // Duration of specific error
        public string SectionCode { get; set; }
        public string MachineCode { get; set; }
        public string ErrorDescription { get; set; }

        public DateTime Date { get; set; }
        public string Shift { get; set; }

        // NEW: Store the total stop time from the "Duraklama Süresi" column
        public int RowTotalStopMinutes { get; set; }

        public static ErrorEventModel Parse(string cellData, DateTime rowDate, string rowShift, int rowTotalStop)
        {
            if (string.IsNullOrWhiteSpace(cellData)) return null;

            var parts = cellData.Split('-').Select(p => p.Trim()).ToArray();
            if (parts.Length < 5) return null;

            var model = new ErrorEventModel
            {
                RawData = cellData,
                Date = rowDate,
                Shift = rowShift,
                RowTotalStopMinutes = rowTotalStop, // Store the row total
                StartTime = parts[0],
                SectionCode = parts[2],
                MachineCode = parts[3]
            };

            if (int.TryParse(parts[1], out int duration))
                model.DurationMinutes = duration;
            else return null;

            model.ErrorDescription = string.Join("-", parts.Skip(4));
            return model;
        }
    }
}