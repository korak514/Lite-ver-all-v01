using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OfficeOpenXml;
using WPF_LoginForm.Models;

namespace WPF_LoginForm.Services
{
    public class TimelineGenerationContext
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double TimelineWidth { get; set; }
        public string ExcelFilePath { get; set; }
        public bool HasExcelFile { get; set; }
        public bool ShowValuesInHours { get; set; }
        public bool HideGenelTemizlik { get; set; }
        public bool DetailedOverlapView { get; set; }
        public string ExtraCol1Selection { get; set; }
        public string ExtraCol2Selection { get; set; }

        public string ColorMa00Genel { get; set; }
        public string ColorMa00Cay { get; set; }
        public string ColorMa00Yemek { get; set; }
        public string ColorMa00Other { get; set; }
        public string BreakColor { get; set; }
        public string ErrorColor { get; set; }
        public string ErrorColor2 { get; set; }
        public string RunningColor { get; set; }
        public string FacilityStopColor { get; set; }
    }

    public interface ITimelineReportGenerator
    {
        Task<List<PrintShiftRow>> GenerateReportAsync(DataTable dbData, TimelineGenerationContext context);
    }

    public class TimelineReportGenerator : ITimelineReportGenerator
    {
        public async Task<List<PrintShiftRow>> GenerateReportAsync(DataTable dbData, TimelineGenerationContext context)
        {
            return await Task.Run(() =>
            {
                var dateCol = dbData.Columns.Cast<DataColumn>().FirstOrDefault(c => c.ColumnName.IndexOf("date", StringComparison.OrdinalIgnoreCase) >= 0 || c.ColumnName.IndexOf("tarih", StringComparison.OrdinalIgnoreCase) >= 0);
                var shiftCol = dbData.Columns.Cast<DataColumn>().FirstOrDefault(c => c.ColumnName.IndexOf("shift", StringComparison.OrdinalIgnoreCase) >= 0 || c.ColumnName.IndexOf("vardiya", StringComparison.OrdinalIgnoreCase) >= 0);
                var endCol = dbData.Columns.Cast<DataColumn>().FirstOrDefault(c => c.ColumnName.IndexOf("bitiş", StringComparison.OrdinalIgnoreCase) >= 0 || c.ColumnName.IndexOf("bitis", StringComparison.OrdinalIgnoreCase) >= 0 || c.ColumnName.IndexOf("end", StringComparison.OrdinalIgnoreCase) >= 0);
                var errorCols = dbData.Columns.Cast<DataColumn>().Where(c => c.ColumnName.StartsWith("hata_kodu", StringComparison.OrdinalIgnoreCase) || c.ColumnName.StartsWith("error_code", StringComparison.OrdinalIgnoreCase)).ToList();

                if (dateCol == null || shiftCol == null) return new List<PrintShiftRow>();

                var excelLookup = new Dictionary<(DateTime date, string shift), Dictionary<string, string>>();
                if (context.HasExcelFile && File.Exists(context.ExcelFilePath))
                {
                    try
                    {
                        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                        using (var stream = new FileStream(context.ExcelFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var package = new ExcelPackage(stream))
                        {
                            var ws = package.Workbook.Worksheets.FirstOrDefault();
                            if (ws != null && ws.Dimension != null)
                            {
                                int colDate = -1, colShift = -1;
                                var headers = new Dictionary<int, string>();
                                for (int c = 1; c <= ws.Dimension.End.Column; c++)
                                {
                                    string header = ws.Cells[1, c].Text?.Trim();
                                    headers[c] = header;
                                    if (header.IndexOf("date", StringComparison.OrdinalIgnoreCase) >= 0 || header.IndexOf("tarih", StringComparison.OrdinalIgnoreCase) >= 0) colDate = c;
                                    if (header.IndexOf("shift", StringComparison.OrdinalIgnoreCase) >= 0 || header.IndexOf("vardiya", StringComparison.OrdinalIgnoreCase) >= 0) colShift = c;
                                }

                                if (colDate != -1 && colShift != -1)
                                {
                                    for (int r = 2; r <= ws.Dimension.End.Row; r++)
                                    {
                                        try
                                        {
                                            DateTime exDate = ws.Cells[r, colDate].GetValue<DateTime>();
                                            if (exDate != DateTime.MinValue && exDate != default(DateTime))
                                            {
                                                var key = (exDate.Date, FormatShiftName(ws.Cells[r, colShift].Text));
                                                var rowData = new Dictionary<string, string>();
                                                for (int c = 1; c <= ws.Dimension.End.Column; c++)
                                                    if (c != colDate && c != colShift) rowData[headers[c]] = ws.Cells[r, c].Text;
                                                excelLookup[key] = rowData;
                                            }
                                        }
                                        catch { /* skip unparseable rows */ }
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }

                var groupedRows = dbData.AsEnumerable()
                    .Where(r => r.RowState != DataRowState.Deleted && r[dateCol] != DBNull.Value)
                    .Select(r => new { Date = Convert.ToDateTime(r[dateCol]).Date, Shift = r[shiftCol].ToString(), Row = r })
                    .Where(x => x.Date >= context.StartDate.Date && x.Date <= context.EndDate.Date)
                    .GroupBy(x => new { x.Date, Shift = FormatShiftName(x.Shift) })
                    .OrderBy(g => g.Key.Date).ThenBy(g => g.Key.Shift)
                    .ToList();

                var processedRows = new List<PrintShiftRow>();
                DateTime? lastDateProcessed = null;
                bool altBackgroundToggle = false;
                double widthMultiplier = context.TimelineWidth / 720.0;

                foreach (var group in groupedRows)
                {
                    if (lastDateProcessed != null && lastDateProcessed != group.Key.Date) altBackgroundToggle = !altBackgroundToggle;

                    bool isNight = group.Key.Shift == "Night";
                    DateTime shiftStart = isNight ? group.Key.Date.AddHours(20) : group.Key.Date.AddHours(8);

                    var shiftRow = new PrintShiftRow { Date = group.Key.Date, ShiftName = group.Key.Shift, ShowDate = lastDateProcessed != group.Key.Date, IsAlternateBackground = altBackgroundToggle };
                    lastDateProcessed = group.Key.Date;

                    double activeShiftMinutes = 720;
                    if (endCol != null)
                    {
                        var firstRowWithEnd = group.FirstOrDefault(x => x.Row[endCol] != DBNull.Value);
                        if (firstRowWithEnd != null)
                        {
                            string eTime = firstRowWithEnd.Row[endCol].ToString();
                            if (TimeSpan.TryParse(eTime, out TimeSpan tsEnd) || (DateTime.TryParse(eTime, out DateTime dEnd) && (tsEnd = dEnd.TimeOfDay) != default))
                            {
                                DateTime eventEndFull = group.Key.Date.Add(tsEnd);
                                if (isNight && tsEnd.Hours < 12) eventEndFull = eventEndFull.AddDays(1);
                                else if (!isNight && tsEnd.Hours < 8) eventEndFull = eventEndFull.AddDays(1);
                                double diff = (eventEndFull - shiftStart).TotalMinutes;
                                if (diff > 0 && diff < 720) activeShiftMinutes = diff;
                            }
                        }
                    }

                    var breaks = new List<PrintTimeBlock>();
                    var rawErrors = new List<PrintTimeBlock>();

                    foreach (var item in group)
                    {
                        foreach (var eCol in errorCols)
                        {
                            string cellData = item.Row[eCol]?.ToString();
                            if (string.IsNullOrWhiteSpace(cellData)) continue;

                            var errModel = ErrorEventModel.Parse(cellData, item.Date, item.Shift, 0, 0, 0, 0, "");
                            if (errModel == null || errModel.ErrorDescription == "NO_ERROR") continue;

                            if (TimeSpan.TryParse(errModel.StartTime, out TimeSpan ts))
                            {
                                DateTime eventStartFull = item.Date.Add(ts);
                                if (isNight && ts.Hours < 12) eventStartFull = eventStartFull.AddDays(1);
                                double startMin = (eventStartFull - shiftStart).TotalMinutes;
                                double duration = errModel.DurationMinutes;

                                if (startMin < 0) { duration += startMin; startMin = 0; }
                                if (startMin + duration > 720) duration = 720 - startMin;

                                if (duration > 0 && startMin < 720)
                                {
                                    string machine = errModel.MachineCode?.Replace("MA-", "") ?? "";
                                    if (machine == "0") machine = "00";

                                    bool isMa00Group1 = false, isMa00Group2 = false, isMa00Group3 = false, isMa00Group4 = false, isNormalBreak = false;

                                    if (machine == "00")
                                    {
                                        string cleanDesc = errModel.ErrorDescription.ToUpper(new CultureInfo("tr-TR")).Replace(" ", "").Replace("-", "").Replace("MA00", "").Replace("00", "").Trim();
                                        if (ComputeLevenshteinDistance(cleanDesc, "GENELTEMİZLİK") <= 1) isMa00Group1 = true;
                                        else if (ComputeLevenshteinDistance(cleanDesc, "ÇAYMOLASI") <= 1) isMa00Group2 = true;
                                        else if (ComputeLevenshteinDistance(cleanDesc, "YEMEKMOLASI") <= 1) isMa00Group3 = true;
                                        else isMa00Group4 = true;
                                    }
                                    else
                                    {
                                        isNormalBreak = errModel.ErrorDescription.IndexOf("mola", StringComparison.OrdinalIgnoreCase) >= 0 || errModel.ErrorDescription.IndexOf("yemek", StringComparison.OrdinalIgnoreCase) >= 0;
                                    }

                                    var block = new PrintTimeBlock
                                    {
                                        StartMinute = startMin,
                                        DurationMinutes = duration,
                                        BlockType = isMa00Group1 ? PrintBlockType.Cleaning : (isMa00Group2 || isMa00Group3 || isNormalBreak ? PrintBlockType.Break : (isMa00Group4 ? PrintBlockType.OtherIdle : PrintBlockType.Error)),
                                        OriginalDescription = errModel.ErrorDescription,
                                        MachineCode = machine,
                                        WidthMultiplier = widthMultiplier,
                                        HeightMultiplier = 1.0,
                                        TopOffset = 0,
                                        DisplayStartTime = shiftStart.AddMinutes(startMin).ToString("HH:mm"),
                                        DisplayEndTime = shiftStart.AddMinutes(startMin + duration).ToString("HH:mm")
                                    };

                                    if (isMa00Group1) { block.ColorHex = context.ColorMa00Genel; breaks.Add(block); }
                                    else if (isMa00Group2) { block.ColorHex = context.ColorMa00Cay; breaks.Add(block); }
                                    else if (isMa00Group3) { block.ColorHex = context.ColorMa00Yemek; breaks.Add(block); }
                                    else if (isMa00Group4) { block.ColorHex = context.ColorMa00Other; breaks.Add(block); }
                                    else if (isNormalBreak) { block.ColorHex = context.BreakColor; breaks.Add(block); }
                                    else rawErrors.Add(block);
                                }
                            }
                        }
                    }

                    // Precise Error Interval Slicing
                    rawErrors = rawErrors.OrderByDescending(e => e.DurationMinutes).ToList();
                    var placedPrimaries = new List<PrintTimeBlock>();
                    var errorBlocksList = new List<PrintTimeBlock>();

                    foreach (var err in rawErrors)
                    {
                        double eStart = err.StartMinute;
                        double eEnd = err.StartMinute + err.DurationMinutes;

                        var boundaries = new List<double> { eStart, eEnd };
                        foreach (var p in placedPrimaries)
                        {
                            double pStart = p.StartMinute;
                            double pEnd = p.StartMinute + p.DurationMinutes;
                            if (pStart > eStart && pStart < eEnd) boundaries.Add(pStart);
                            if (pEnd > eStart && pEnd < eEnd) boundaries.Add(pEnd);
                        }
                        boundaries = boundaries.Distinct().OrderBy(x => x).ToList();

                        for (int i = 0; i < boundaries.Count - 1; i++)
                        {
                            double s = boundaries[i];
                            double e = boundaries[i + 1];
                            if (e - s <= 0.001) continue;

                            double mid = (s + e) / 2.0;
                            var overlappingPrimary = placedPrimaries.FirstOrDefault(p => mid >= p.StartMinute && mid <= (p.StartMinute + p.DurationMinutes));

                            var slice = new PrintTimeBlock
                            {
                                StartMinute = s,
                                DurationMinutes = e - s,
                                BlockType = err.BlockType,
                                OriginalDescription = err.OriginalDescription,
                                MachineCode = err.MachineCode,
                                WidthMultiplier = err.WidthMultiplier,
                                HeightMultiplier = 1.0,
                                TopOffset = 0,
                                DisplayStartTime = shiftStart.AddMinutes(s).ToString("HH:mm"),
                                DisplayEndTime = shiftStart.AddMinutes(e).ToString("HH:mm")
                            };

                            if (overlappingPrimary != null)
                            {
                                slice.ColorHex = context.ErrorColor2;
                                slice.HeightMultiplier = 0.35;

                                if (err.DurationMinutes >= 20 && !string.IsNullOrEmpty(err.MachineCode))
                                {
                                    if (!overlappingPrimary.MachineCodes.Contains(err.MachineCode))
                                        overlappingPrimary.MachineCodes.Add(err.MachineCode);
                                }
                            }
                            else
                            {
                                slice.ColorHex = context.ErrorColor;

                                if (err.DurationMinutes >= 20 && err.DurationMinutes < 30)
                                {
                                    if (!string.IsNullOrEmpty(slice.MachineCode)) slice.MachineCodes.Add(slice.MachineCode);
                                }
                                else if (err.DurationMinutes >= 30 && err.DurationMinutes < 90)
                                {
                                    if (!string.IsNullOrEmpty(slice.MachineCode)) slice.MachineCodes.Add(slice.MachineCode);
                                    slice.IsFootnote = true;
                                }
                                else if (err.DurationMinutes >= 90)
                                {
                                    if (!string.IsNullOrEmpty(slice.MachineCode)) slice.MachineCodes.Add(slice.MachineCode);
                                    if (!string.IsNullOrEmpty(slice.OriginalDescription))
                                    {
                                        var words = slice.OriginalDescription.Split(new char[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
                                        if (words.Length >= 2) slice.TextDescription = $"{words[0]} {words[1]}";
                                        else if (words.Length == 1) slice.TextDescription = words[0];
                                    }
                                }

                                placedPrimaries.Add(slice);
                            }
                            errorBlocksList.Add(slice);
                        }
                    }

                    var breakBlocksList = new List<PrintTimeBlock>();
                    foreach (var b in breaks)
                    {
                        b.HeightMultiplier = 1.0;
                        b.TopOffset = 0;
                        breakBlocksList.Add(b);
                    }

                    var allEventsForGaps = breakBlocksList.Concat(errorBlocksList).OrderBy(e => e.StartMinute).ToList();
                    var mergedIntervals = new List<(double Start, double End)>();

                    foreach (var ev in allEventsForGaps)
                    {
                        double start = ev.StartMinute;
                        double end = start + ev.DurationMinutes;
                        if (mergedIntervals.Count == 0) mergedIntervals.Add((start, end));
                        else
                        {
                            var last = mergedIntervals.Last();
                            if (start <= last.End) mergedIntervals[mergedIntervals.Count - 1] = (last.Start, Math.Max(last.End, end));
                            else mergedIntervals.Add((start, end));
                        }
                    }

                    // ✨ NEW LOGIC FIX: Accurately calculate STOP TIME directly from merged overlaps!
                    double totalStopMins = 0;
                    foreach (var interval in mergedIntervals)
                    {
                        double start = Math.Max(0, interval.Start);
                        double end = Math.Min(720, interval.End);
                        if (end > start)
                        {
                            totalStopMins += (end - start);
                        }
                    }

                    var backgroundBlocks = new List<PrintTimeBlock>();
                    double currentMin = 0;

                    foreach (var interval in mergedIntervals)
                    {
                        if (interval.Start > currentMin)
                        {
                            double duration = interval.Start - currentMin;
                            if (duration > 0 && currentMin < activeShiftMinutes)
                            {
                                double actualDur = Math.Min(duration, activeShiftMinutes - currentMin);
                                if (actualDur > 0)
                                {
                                    backgroundBlocks.Add(new PrintTimeBlock
                                    {
                                        StartMinute = currentMin,
                                        DurationMinutes = actualDur,
                                        BlockType = PrintBlockType.Running,
                                        ColorHex = context.RunningColor,
                                        WidthMultiplier = widthMultiplier,
                                        HeightMultiplier = 1.0,
                                        DisplayStartTime = shiftStart.AddMinutes(currentMin).ToString("HH:mm"),
                                        DisplayEndTime = shiftStart.AddMinutes(currentMin + actualDur).ToString("HH:mm")
                                    });
                                }
                            }
                        }
                        currentMin = Math.Max(currentMin, interval.End);
                    }

                    if (currentMin < activeShiftMinutes)
                    {
                        double duration = activeShiftMinutes - currentMin;
                        backgroundBlocks.Add(new PrintTimeBlock
                        {
                            StartMinute = currentMin,
                            DurationMinutes = duration,
                            BlockType = PrintBlockType.Running,
                            ColorHex = context.RunningColor,
                            WidthMultiplier = widthMultiplier,
                            HeightMultiplier = 1.0,
                            DisplayStartTime = shiftStart.AddMinutes(currentMin).ToString("HH:mm"),
                            DisplayEndTime = shiftStart.AddMinutes(currentMin + duration).ToString("HH:mm")
                        });
                    }

                    if (activeShiftMinutes < 720)
                    {
                        backgroundBlocks.Add(new PrintTimeBlock
                        {
                            StartMinute = activeShiftMinutes,
                            DurationMinutes = 720 - activeShiftMinutes,
                            BlockType = PrintBlockType.FacilityStop,
                            ColorHex = context.FacilityStopColor,
                            WidthMultiplier = widthMultiplier,
                            HeightMultiplier = 1.0,
                            DisplayStartTime = shiftStart.AddMinutes(activeShiftMinutes).ToString("HH:mm"),
                            DisplayEndTime = shiftStart.AddMinutes(720).ToString("HH:mm")
                        });
                    }

                    foreach (var bg in backgroundBlocks) shiftRow.Blocks.Add(bg);
                    foreach (var err in errorBlocksList) shiftRow.Blocks.Add(err);
                    foreach (var brk in breakBlocksList) shiftRow.Blocks.Add(brk);

                    foreach (var block in shiftRow.Blocks)
                    {
                        if (block.IsFootnote) continue;

                        if ((block.MachineCodes != null && block.MachineCodes.Any()) || !string.IsNullOrEmpty(block.TextDescription))
                        {
                            var distinctCodes = block.MachineCodes.Distinct().ToList();
                            if (context.HideGenelTemizlik) distinctCodes.RemoveAll(c => c == "00" || c == "0");

                            string codesStr = "";
                            if (distinctCodes.Any())
                            {
                                if (context.DetailedOverlapView && distinctCodes.Count > 1) codesStr = string.Join(", ", distinctCodes.Take(distinctCodes.Count - 1)) + " & " + distinctCodes.Last();
                                else codesStr = string.Join(" & ", distinctCodes);
                            }

                            if (string.IsNullOrWhiteSpace(block.TextDescription)) block.Label = codesStr;
                            else if (string.IsNullOrWhiteSpace(codesStr)) block.Label = block.TextDescription;
                            else block.Label = $"{codesStr} {block.TextDescription}";
                        }
                    }

                    shiftRow.ExtraValue1 = CalculateExtraValue(context.ExtraCol1Selection, totalStopMins, group.Key.Date, group.Key.Shift, excelLookup, context.ShowValuesInHours);
                    shiftRow.ExtraValue2 = CalculateExtraValue(context.ExtraCol2Selection, totalStopMins, group.Key.Date, group.Key.Shift, excelLookup, context.ShowValuesInHours);

                    processedRows.Add(shiftRow);
                }

                return processedRows;
            });
        }

        private string CalculateExtraValue(string selection, double totalStop, DateTime date, string shift, Dictionary<(DateTime, string), Dictionary<string, string>> excelLookup, bool showHours)
        {
            if (selection == "None" || string.IsNullOrEmpty(selection)) return "";

            string locTotalStop = WPF_LoginForm.Properties.Resources.P_Total_Stop ?? "Total Stop / Toplam Duruş";
            string locActualWork = WPF_LoginForm.Properties.Resources.P_Working_Time ?? "Actual Work / Çalışma Süresi";

            if (selection == locTotalStop || selection == "Total Stop" || selection == locActualWork || selection == "Actual Work")
            {
                double val = (selection == locTotalStop || selection == "Total Stop") ? totalStop : Math.Max(0, 720 - totalStop);

                if (showHours)
                {
                    TimeSpan ts = TimeSpan.FromMinutes(val);
                    return $"{(int)ts.TotalHours} sa {ts.Minutes} dk";
                }
                return val.ToString("F0");
            }

            if (selection.StartsWith("[Excel] "))
            {
                string headerName = selection.Replace("[Excel] ", "");
                var key = (date, shift);
                if (excelLookup.ContainsKey(key) && excelLookup[key].ContainsKey(headerName))
                {
                    string rawExcelVal = excelLookup[key][headerName];
                    if (double.TryParse(rawExcelVal, out double numVal)) return numVal.ToString("G");
                    return rawExcelVal;
                }
                return "-";
            }
            return "";
        }

        private string FormatShiftName(string raw)
        {
            return string.IsNullOrEmpty(raw) ? "Day" : (raw.IndexOf("gece", StringComparison.OrdinalIgnoreCase) >= 0 || raw.IndexOf("night", StringComparison.OrdinalIgnoreCase) >= 0 ? "Night" : "Day");
        }

        private int ComputeLevenshteinDistance(string s, string t)
        {
            if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
            if (string.IsNullOrEmpty(t)) return s.Length;
            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];
            for (int i = 0; i <= n; d[i, 0] = i++) { }
            for (int j = 0; j <= m; d[0, j] = j++) { }
            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }
            }
            return d[n, m];
        }
    }
}