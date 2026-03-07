// Services/TimeFormatHelper.cs
using System;
using System.Globalization;
using WPF_LoginForm.Properties; // Access to Resources

namespace WPF_LoginForm.Services
{
    public static class TimeFormatHelper
    {
        public static string FormatDuration(double minutes, bool useClockFormat)
        {
            // 1. If formatting is disabled OR value is small (< 60 min), return standard "90 min"
            if (!useClockFormat || minutes < 60)
            {
                // Resources.Unit_Minutes usually contains "min" or "dk"
                string unit = Resources.Unit_Minutes ?? "min";
                return $"{minutes:N0} {unit}";
            }

            // 2. Convert to Clock Format (e.g., "1 h 30 m")
            TimeSpan ts = TimeSpan.FromMinutes(minutes);
            int h = (int)ts.TotalHours;
            int m = ts.Minutes;

            // Localization Logic
            string currentLang = Resources.Culture?.Name ?? CultureInfo.CurrentCulture.Name;
            bool isTurkish = currentLang == "tr-TR";

            string hLabel = isTurkish ? "sa" : "h";
            string mLabel = isTurkish ? "dk" : "m";

            if (m == 0)
                return $"{h} {hLabel}";

            return $"{h} {hLabel} {m} {mLabel}";
        }
    }
}