// Services/TimelineReportGenerator.cs
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

        // User configured Layers
        public int LayerRunning { get; set; } = 1;

        public int LayerBypass { get; set; } = 2;
        public int LayerBreaks { get; set; } = 3;
        public int LayerErrors { get; set; } = 4;

        // User configured Thresholds
        public double OverlapCascadeStep { get; set; } = 0.20;

        public double MinLabelMinutes { get; set; } = 90.0;
        public double MinFootnoteMinutes { get; set; } = 20.0;
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

                // PRE-CALCULATE DYNAMIC CASCADE HEIGHTS
                double[] cascadeHeights = new double[6];
                for (int i = 0; i < 6; i++)
                {
                    cascadeHeights[i] = Math.Max(0.05, 1.0 - (i * context.OverlapCascadeStep));
                }

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

                    var allRawEvents = new List<PrintTimeBlock>();

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

                                    string dStartStr = shiftStart.AddMinutes(startMin).ToString("HH:mm");
                                    string dEndStr = shiftStart.AddMinutes(startMin + duration).ToString("HH:mm");
                                    string uniqueFP = $"{group.Key.Date:yyyyMMdd}_{group.Key.Shift}_{dStartStr}_{dEndStr}_{errModel.ErrorDescription}";

                                    var block = new PrintTimeBlock
                                    {
                                        Fingerprint = uniqueFP,
                                        BaseFingerprint = uniqueFP,
                                        OriginalDurationMinutes = duration,
                                        StartMinute = startMin,
                                        DurationMinutes = duration,
                                        OriginalDescription = errModel.ErrorDescription,
                                        MachineCode = machine,
                                        WidthMultiplier = widthMultiplier,
                                        DisplayStartTime = dStartStr,
                                        DisplayEndTime = dEndStr
                                    };

                                    if (isMa00Group1) { block.ColorHex = context.ColorMa00Genel; block.BlockType = PrintBlockType.Cleaning; }
                                    else if (isMa00Group2) { block.ColorHex = context.ColorMa00Cay; block.BlockType = PrintBlockType.Break; }
                                    else if (isMa00Group3) { block.ColorHex = context.ColorMa00Yemek; block.BlockType = PrintBlockType.Break; }
                                    else if (isMa00Group4) { block.ColorHex = context.ColorMa00Other; block.BlockType = PrintBlockType.OtherIdle; }
                                    else if (isNormalBreak) { block.ColorHex = context.BreakColor; block.BlockType = PrintBlockType.Break; }
                                    else { block.ColorHex = context.ErrorColor; block.BlockType = PrintBlockType.Error; }

                                    allRawEvents.Add(block);
                                }
                            }
                        }
                    }

                    var bypassEvents = new List<PrintTimeBlock>();
                    var normalEvents = new List<PrintTimeBlock>();

                    foreach (var e in allRawEvents)
                    {
                        if (e.MachineCode == "33" || e.MachineCode == "34" || e.MachineCode == "35" || e.MachineCode == "36" || e.MachineCode == "37")
                            bypassEvents.Add(e);
                        else
                            normalEvents.Add(e);
                    }

                    var finalEventBlocks = new List<PrintTimeBlock>();

                    if (normalEvents.Any())
                    {
                        var boundaries = new List<double>();
                        foreach (var e in normalEvents)
                        {
                            boundaries.Add(e.StartMinute);
                            boundaries.Add(e.StartMinute + e.DurationMinutes);
                        }
                        boundaries = boundaries.Distinct().OrderBy(x => x).ToList();

                        for (int i = 0; i < boundaries.Count - 1; i++)
                        {
                            double s = boundaries[i];
                            double e = boundaries[i + 1];
                            if (e - s <= 0.001) continue;
                            double mid = (s + e) / 2.0;

                            var activeEvents = normalEvents.Where(x => x.StartMinute <= mid && (x.StartMinute + x.DurationMinutes) >= mid)
                                                           .OrderByDescending(x => x.DurationMinutes)
                                                           .Take(6)
                                                           .ToList();

                            for (int j = 0; j < activeEvents.Count; j++)
                            {
                                var orig = activeEvents[j];

                                int baseZ = (orig.BlockType == PrintBlockType.Break || orig.BlockType == PrintBlockType.Cleaning)
                                            ? context.LayerBreaks
                                            : context.LayerErrors;

                                var slice = new PrintTimeBlock
                                {
                                    Fingerprint = orig.Fingerprint + $"_slice_{i}_{j}",
                                    BaseFingerprint = orig.BaseFingerprint,
                                    OriginalDurationMinutes = orig.OriginalDurationMinutes,
                                    StartMinute = s,
                                    DurationMinutes = e - s,
                                    BlockType = orig.BlockType,
                                    OriginalDescription = orig.OriginalDescription,
                                    MachineCode = orig.MachineCode,
                                    WidthMultiplier = widthMultiplier,
                                    HeightMultiplier = cascadeHeights[j],
                                    TopOffset = 0,
                                    PanelZIndex = baseZ + j,
                                    DisplayStartTime = shiftStart.AddMinutes(s).ToString("HH:mm"),
                                    DisplayEndTime = shiftStart.AddMinutes(e).ToString("HH:mm")
                                };

                                if (j > 0 && slice.BlockType == PrintBlockType.Error)
                                    slice.ColorHex = context.ErrorColor2;
                                else
                                    slice.ColorHex = orig.ColorHex;

                                if (j == activeEvents.Count - 1)
                                {
                                    if (!string.IsNullOrEmpty(orig.OriginalDescription))
                                    {
                                        var words = orig.OriginalDescription.Split(new char[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
                                        slice.TextDescription = words.Length >= 2 ? $"{words[0]} {words[1]}" : words[0];
                                    }
                                    if (!string.IsNullOrEmpty(orig.MachineCode))
                                        slice.MachineCodes.Add(orig.MachineCode);
                                }

                                finalEventBlocks.Add(slice);
                            }
                        }
                    }

                    var finalBypassBlocks = new List<PrintTimeBlock>();

                    if (bypassEvents.Any())
                    {
                        var rawBypassIntervals = MergeIntervals(bypassEvents);
                        var normalIntervals = MergeIntervals(normalEvents);
                        var validBypassIntervals = SubtractIntervals(rawBypassIntervals, normalIntervals);

                        foreach (var interval in validBypassIntervals)
                        {
                            if (interval.End - interval.Start <= 0.001) continue;

                            finalBypassBlocks.Add(new PrintTimeBlock
                            {
                                Fingerprint = $"{group.Key.Date:yyyyMMdd}_{group.Key.Shift}_BYPASS_{interval.Start}",
                                BaseFingerprint = "BYPASS_BASE",
                                OriginalDurationMinutes = interval.End - interval.Start,
                                StartMinute = interval.Start,
                                DurationMinutes = interval.End - interval.Start,
                                BlockType = PrintBlockType.Break,
                                ColorHex = context.BreakColor,
                                WidthMultiplier = widthMultiplier,
                                HeightMultiplier = 0.30,
                                TopOffset = 0,
                                PanelZIndex = context.LayerBypass,
                                DisplayStartTime = shiftStart.AddMinutes(interval.Start).ToString("HH:mm"),
                                DisplayEndTime = shiftStart.AddMinutes(interval.End).ToString("HH:mm")
                            });
                        }
                    }

                    // FIX: Create one SOLID running canvas to perfectly eliminate white holes
                    var backgroundBlocks = new List<PrintTimeBlock>
                    {
                        new PrintTimeBlock
                        {
                            Fingerprint = $"{group.Key.Date:yyyyMMdd}_{group.Key.Shift}_RUNNING_CANVAS",
                            BaseFingerprint = "RUNNING_BASE",
                            OriginalDurationMinutes = activeShiftMinutes,
                            StartMinute = 0,
                            DurationMinutes = activeShiftMinutes,
                            BlockType = PrintBlockType.Running,
                            ColorHex = context.RunningColor,
                            WidthMultiplier = widthMultiplier,
                            HeightMultiplier = 1.0,
                            TopOffset = 0,
                            PanelZIndex = context.LayerRunning,
                            DisplayStartTime = shiftStart.ToString("HH:mm"),
                            DisplayEndTime = shiftStart.AddMinutes(activeShiftMinutes).ToString("HH:mm")
                        }
                    };

                    if (activeShiftMinutes < 720)
                    {
                        backgroundBlocks.Add(new PrintTimeBlock
                        {
                            Fingerprint = $"{group.Key.Date:yyyyMMdd}_{group.Key.Shift}_FacStop",
                            BaseFingerprint = "FACSTOP_BASE",
                            OriginalDurationMinutes = 720 - activeShiftMinutes,
                            StartMinute = activeShiftMinutes,
                            DurationMinutes = 720 - activeShiftMinutes,
                            BlockType = PrintBlockType.FacilityStop,
                            ColorHex = context.FacilityStopColor,
                            WidthMultiplier = widthMultiplier,
                            HeightMultiplier = 1.0,
                            TopOffset = 0,
                            PanelZIndex = 20,
                            DisplayStartTime = shiftStart.AddMinutes(activeShiftMinutes).ToString("HH:mm"),
                            DisplayEndTime = shiftStart.AddMinutes(720).ToString("HH:mm")
                        });
                    }

                    foreach (var bg in backgroundBlocks) shiftRow.Blocks.Add(bg);
                    foreach (var bp in finalBypassBlocks) shiftRow.Blocks.Add(bp);
                    foreach (var ev in finalEventBlocks) shiftRow.Blocks.Add(ev);

                    // --- STEP 4: Rule Enforcement (<20 Min, <90 Min, Cleaning Strips) ---
                    foreach (var block in shiftRow.Blocks)
                    {
                        if (block.BlockType == PrintBlockType.Running || block.BlockType == PrintBlockType.FacilityStop) continue;

                        if (block.BlockType == PrintBlockType.Cleaning || block.BlockType == PrintBlockType.Break || block.BlockType == PrintBlockType.OtherIdle)
                        {
                            block.IsFootnote = false;
                            block.Label = "";
                            block.TextDescription = "";
                            block.MachineCodes.Clear();
                            continue;
                        }

                        double evalDur = block.OriginalDurationMinutes > 0 ? block.OriginalDurationMinutes : block.DurationMinutes;

                        if (evalDur < context.MinFootnoteMinutes)
                        {
                            block.IsFootnote = false;
                            block.Label = "";
                            block.TextDescription = "";
                            block.MachineCodes.Clear();
                        }
                        else if (evalDur >= context.MinFootnoteMinutes && evalDur < context.MinLabelMinutes)
                        {
                            if (block.HeightMultiplier >= 0.8) block.IsFootnote = true;
                            else block.IsFootnote = false;

                            block.Label = "";
                            block.TextDescription = "";
                        }
                        else
                        {
                            block.IsFootnote = false;
                        }
                    }

                    foreach (var block in shiftRow.Blocks)
                    {
                        if (block.IsFootnote || string.IsNullOrEmpty(block.Fingerprint)) continue;

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

                    // --- STEP 5: Add Soft Vertical Separators ---
                    var groupedByZ = shiftRow.Blocks.GroupBy(b => b.PanelZIndex);
                    foreach (var zGroup in groupedByZ)
                    {
                        var sorted = zGroup.OrderBy(b => b.StartMinute).ToList();
                        for (int i = 0; i < sorted.Count - 1; i++)
                        {
                            var current = sorted[i];
                            var next = sorted[i + 1];

                            if (Math.Abs((current.StartMinute + current.DurationMinutes) - next.StartMinute) < 1.0 &&
                                current.ColorHex == next.ColorHex &&
                                current.BaseFingerprint != next.BaseFingerprint &&
                                current.BlockType != PrintBlockType.Running &&
                                current.BlockType != PrintBlockType.FacilityStop)
                            {
                                current.BlockBorder = new System.Windows.Thickness(0, 0, 1, 0);
                            }
                        }
                    }

                    double totalStopMins = 0;
                    var stopIntervals = MergeIntervals(normalEvents);
                    foreach (var interval in stopIntervals)
                    {
                        double start = Math.Max(0, interval.Start);
                        double end = Math.Min(activeShiftMinutes, interval.End);
                        if (end > start) totalStopMins += (end - start);
                    }

                    shiftRow.ExtraValue1 = CalculateExtraValue(context.ExtraCol1Selection, totalStopMins, activeShiftMinutes, group.Key.Date, group.Key.Shift, excelLookup, context.ShowValuesInHours);
                    shiftRow.ExtraValue2 = CalculateExtraValue(context.ExtraCol2Selection, totalStopMins, activeShiftMinutes, group.Key.Date, group.Key.Shift, excelLookup, context.ShowValuesInHours);

                    processedRows.Add(shiftRow);
                }

                return processedRows;
            });
        }

        private List<(double Start, double End)> MergeIntervals(List<PrintTimeBlock> events)
        {
            if (!events.Any()) return new List<(double Start, double End)>();
            var sorted = events.OrderBy(e => e.StartMinute).ToList();
            var merged = new List<(double Start, double End)>();

            foreach (var ev in sorted)
            {
                double s = ev.StartMinute;
                double e = s + ev.DurationMinutes;
                if (merged.Count == 0) merged.Add((s, e));
                else
                {
                    var last = merged.Last();
                    if (s <= last.End) merged[merged.Count - 1] = (last.Start, Math.Max(last.End, e));
                    else merged.Add((s, e));
                }
            }
            return merged;
        }

        private List<(double Start, double End)> SubtractIntervals(List<(double Start, double End)> sources, List<(double Start, double End)> subtractions)
        {
            var result = new List<(double Start, double End)>();
            foreach (var src in sources)
            {
                var currentPieces = new List<(double Start, double End)> { src };
                foreach (var sub in subtractions)
                {
                    var nextPieces = new List<(double Start, double End)>();
                    foreach (var p in currentPieces)
                    {
                        if (sub.End <= p.Start || sub.Start >= p.End) nextPieces.Add(p);
                        else
                        {
                            if (p.Start < sub.Start) nextPieces.Add((p.Start, sub.Start));
                            if (p.End > sub.End) nextPieces.Add((sub.End, p.End));
                        }
                    }
                    currentPieces = nextPieces;
                }
                result.AddRange(currentPieces);
            }
            return result;
        }

        private string CalculateExtraValue(string selection, double totalStop, double activeShiftMins, DateTime date, string shift, Dictionary<(DateTime, string), Dictionary<string, string>> excelLookup, bool showHours)
        {
            if (selection == "None" || string.IsNullOrEmpty(selection)) return "";

            string locTotalStop = WPF_LoginForm.Properties.Resources.P_Total_Stop ?? "Total Stop / Toplam Duruş";
            string locActualWork = WPF_LoginForm.Properties.Resources.P_Working_Time ?? "Actual Work / Çalışma Süresi";

            if (selection == locTotalStop || selection == "Total Stop" || selection == locActualWork || selection == "Actual Work")
            {
                double val = (selection == locTotalStop || selection == "Total Stop") ? totalStop : Math.Max(0, activeShiftMins - totalStop);
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