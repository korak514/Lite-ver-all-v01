using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using WPF_LoginForm.Models;

namespace WPF_LoginForm.ViewModels
{
    public class MonthlyErrorsViewModel : ViewModelBase
    {
        private static readonly string TrackerFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WPF_LoginForm", "monthly_tracker.json");

        private readonly DailyTimelineViewModel _dailyVm;
        private string _summaryText;
        private string _monthYearText;
        public string SummaryText { get => _summaryText; set => SetProperty(ref _summaryText, value); }
        public string MonthYearText { get => _monthYearText; set => SetProperty(ref _monthYearText, value); }

        public ObservableCollection<MonthlyValidationItem> Checks { get; } = new ObservableCollection<MonthlyValidationItem>();
        public ObservableCollection<CalendarDayModel> CalendarDays { get; } = new ObservableCollection<CalendarDayModel>();
        public ObservableCollection<string> DayHeaders { get; } = new ObservableCollection<string> { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

        public MonthlyErrorsViewModel(DailyTimelineViewModel dailyVm)
        {
            _dailyVm = dailyVm;
            BuildMonthCalendar();
            LoadTrackerData();
            RunAllChecks();
        }

        private static Dictionary<string, string> LoadTracker()
        {
            try
            {
                if (File.Exists(TrackerFilePath))
                    return JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(TrackerFilePath))
                           ?? new Dictionary<string, string>();
            }
            catch { }
            return new Dictionary<string, string>();
        }

        private static void SaveTracker(Dictionary<string, string> data)
        {
            try
            {
                string dir = Path.GetDirectoryName(TrackerFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(TrackerFilePath, JsonConvert.SerializeObject(data, Formatting.Indented));
            }
            catch { }
        }

        private void BuildMonthCalendar()
        {
            CalendarDays.Clear();
            DateTime target = _dailyVm.TargetDate;
            MonthYearText = target.ToString("MMMM yyyy");

            DateTime firstOfMonth = new DateTime(target.Year, target.Month, 1);
            int daysInMonth = DateTime.DaysInMonth(target.Year, target.Month);
            int startOffset = ((int)firstOfMonth.DayOfWeek + 6) % 7;

            for (int i = 0; i < startOffset; i++)
                CalendarDays.Add(new CalendarDayModel { Day = 0, IsVisible = false });

            for (int d = 1; d <= daysInMonth; d++)
            {
                var dayDate = new DateTime(target.Year, target.Month, d);
                CalendarDays.Add(new CalendarDayModel
                {
                    Day = d,
                    IsCurrentDay = dayDate == target.Date,
                    DayStatus = ShiftStatus.Neutral,
                    NightStatus = ShiftStatus.Neutral
                });
            }
        }

        private void LoadTrackerData()
        {
            var tracker = LoadTracker();
            DateTime target = _dailyVm.TargetDate;

            foreach (var day in CalendarDays.Where(d => d.IsVisible && !d.IsCurrentDay))
            {
                var dateKey = new DateTime(target.Year, target.Month, day.Day).ToString("yyyy-MM-dd");
                if (tracker.TryGetValue(dateKey, out string saved))
                {
                    var parts = saved.Split('|');
                    if (parts.Length == 2)
                    {
                        if (parts[0] == "Good") day.DayStatus = ShiftStatus.Good;
                        else if (parts[0] == "Bad") day.DayStatus = ShiftStatus.Bad;

                        if (parts[1] == "Good") day.NightStatus = ShiftStatus.Good;
                        else if (parts[1] == "Bad") day.NightStatus = ShiftStatus.Bad;
                    }
                }
            }
        }

        private void RunAllChecks()
        {
            Checks.Clear();
            CheckWorkingTime();
            CheckMealBreakDuration();
            CheckMolaBakim();
            CheckBypass();
            CheckWrongTime();
            CheckCorruptedData();

            int good = Checks.Count(c => c.Status == "Good");
            int bad = Checks.Count(c => c.Status == "Bad");

            if (bad == 0)
                SummaryText = $"All {good}/{Checks.Count} checks passed — No issues found";
            else
                SummaryText = $"{bad} check(s) failed — Review needed";

            var currentDay = CalendarDays.FirstOrDefault(d => d.IsCurrentDay);
            if (currentDay != null)
            {
                ShiftStatus result = bad > 0 ? ShiftStatus.Bad : ShiftStatus.Good;
                bool isNight = _dailyVm.IsNightShift;

                if (isNight)
                    currentDay.NightStatus = result;
                else
                    currentDay.DayStatus = result;

                var tracker = LoadTracker();
                string dateKey = _dailyVm.TargetDate.ToString("yyyy-MM-dd");
                string existing = tracker.TryGetValue(dateKey, out string prev) ? prev : "Neutral|Neutral";
                var parts = existing.Split('|');
                string dayStr = !isNight ? (result == ShiftStatus.Bad ? "Bad" : "Good") : (parts.Length > 0 ? parts[0] : "Neutral");
                string nightStr = isNight ? (result == ShiftStatus.Bad ? "Bad" : "Good") : (parts.Length > 1 ? parts[1] : "Neutral");
                tracker[dateKey] = $"{dayStr}|{nightStr}";
                SaveTracker(tracker);
            }
        }

        private void CheckWorkingTime()
        {
            double auto = _dailyVm.AutoFiiliSure;
            double raw = _dailyVm.RawFiiliSure;
            double diff = Math.Abs(auto - raw);
            bool ok = diff < 1;
            Checks.Add(new MonthlyValidationItem
            {
                CheckName = "Working Time Check",
                Detail = $"Auto: {auto:F0}m, DB: {raw:F0}m, Diff: {diff:F0}m",
                Status = ok ? "Good" : "Bad"
            });
        }

        private void CheckMealBreakDuration()
        {
            var longMeals = _dailyVm.TimelineBlocks
                .Where(b => b.OriginalEvent?.ErrorDescription?.Contains("YEMEK-MOLASI") == true && b.DurationMinutes > 65)
                .ToList();
            Checks.Add(new MonthlyValidationItem
            {
                CheckName = "YEMEK-MOLASI ≤ 65 min",
                Detail = longMeals.Any()
                    ? $"{longMeals.Count} entries exceed limit (max: {longMeals.Max(b => b.DurationMinutes):F0}m)"
                    : "All within limit",
                Status = longMeals.Any() ? "Bad" : "Good"
            });
        }

        private void CheckMolaBakim()
        {
            double auto = _dailyVm.AutoCalculatedMolaKazanimi;
            double raw = _dailyVm.RawMolaKazanimi;
            double diff = Math.Abs(auto - raw);
            bool ok = diff < 1;
            Checks.Add(new MonthlyValidationItem
            {
                CheckName = "Mola/Bakım Match",
                Detail = $"Auto: {auto:F0}m, DB: {raw:F0}m, Diff: {diff:F0}m",
                Status = ok ? "Good" : "Bad"
            });
        }

        private void CheckBypass()
        {
            double manual = _dailyVm.ManualBypassKazanimi;
            double raw = _dailyVm.RawBypassKazanimi;
            double diff = Math.Abs(manual - raw);
            bool ok = diff < 1;
            Checks.Add(new MonthlyValidationItem
            {
                CheckName = "Bypass Match",
                Detail = $"Manual: {manual:F0}m, DB: {raw:F0}m, Diff: {diff:F0}m",
                Status = ok ? "Good" : "Bad"
            });
        }

        private void CheckWrongTime()
        {
            int count = _dailyVm.WrongTimeCount;
            Checks.Add(new MonthlyValidationItem
            {
                CheckName = "Wrong Time Data",
                Detail = $"{count} entries",
                Status = count == 0 ? "Good" : "Bad"
            });
        }

        private void CheckCorruptedData()
        {
            int count = _dailyVm.CorruptedDataCount;
            Checks.Add(new MonthlyValidationItem
            {
                CheckName = "Corrupted Data",
                Detail = $"{count} entries",
                Status = count == 0 ? "Good" : "Bad"
            });
        }
    }
}
