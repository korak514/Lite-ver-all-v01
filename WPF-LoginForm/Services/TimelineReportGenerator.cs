// Services/TimelineReportGenerator.cs
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
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

        public int LayerRunning { get; set; } = 1;
        public int LayerBypass { get; set; } = 2;
        public int LayerBreaks { get; set; } = 10;
        public int LayerErrors { get; set; } = 20;

        public double OverlapCascadeStep { get; set; } = 0.20;
        public double MinLabelMinutes { get; set; } = 90.0;
        public double MinFootnoteMinutes { get; set; } = 20.0;

        public double RowHeight { get; set; } = 14.0;
        public bool EnableSoftCorners { get; set; }
        public bool DisableSoftCornersUnder5Min { get; set; }
        public bool EnableBlockBorders { get; set; }
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
                                        catch { /* skip */ }
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
                                        RowHeight = context.RowHeight,
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

                    var rawStopIntervals = MergeIntervals(normalEvents);
                    var combinedStopIntervals = new List<(double Start, double End)>(rawStopIntervals);
                    if (activeShiftMinutes < 720) combinedStopIntervals.Add((activeShiftMinutes, 720));

                    var mergedCombinedStops = MergeIntervals(combinedStopIntervals.Select(x => new PrintTimeBlock { StartMinute = x.Start, DurationMinutes = x.End - x.Start }).ToList());
                    var shiftFullInterval = new List<(double Start, double End)> { (0, 720) };
                    var trueRunningIntervals = SubtractIntervals(shiftFullInterval, mergedCombinedStops);

                    foreach (var interval in trueRunningIntervals)
                    {
                        if (interval.End - interval.Start <= 0.001) continue;
                        bool touchesRightEdge = interval.End >= 719.9;

                        shiftRow.Blocks.Add(new PrintTimeBlock
                        {
                            Fingerprint = $"{group.Key.Date:yyyyMMdd}_{group.Key.Shift}_RUNNING_{interval.Start}",
                            BaseFingerprint = "RUNNING_BASE",
                            OriginalDurationMinutes = interval.End - interval.Start,
                            StartMinute = interval.Start,
                            DurationMinutes = interval.End - interval.Start,
                            BlockType = PrintBlockType.Running,
                            ColorHex = context.RunningColor,
                            WidthMultiplier = widthMultiplier,
                            HeightMultiplier = 1.0,
                            RowHeight = context.RowHeight,
                            TopOffset = 0,
                            PanelZIndex = 1,
                            VisualLeftOffset = 0,
                            VisualWidthOffset = touchesRightEdge ? 4.0 : 0.5,
                            DisplayStartTime = shiftStart.AddMinutes(interval.Start).ToString("HH:mm"),
                            DisplayEndTime = shiftStart.AddMinutes(interval.End).ToString("HH:mm"),
                            CornerRadius = new CornerRadius(0),
                            BlockBorder = new Thickness(0)
                        });
                    }

                    if (activeShiftMinutes < 720)
                    {
                        shiftRow.Blocks.Add(new PrintTimeBlock
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
                            RowHeight = context.RowHeight,
                            TopOffset = 0,
                            PanelZIndex = 3,
                            VisualLeftOffset = 0,
                            VisualWidthOffset = 4.0,
                            DisplayStartTime = shiftStart.AddMinutes(activeShiftMinutes).ToString("HH:mm"),
                            DisplayEndTime = shiftStart.AddMinutes(720).ToString("HH:mm"),
                            CornerRadius = new CornerRadius(0),
                            BlockBorder = new Thickness(0)
                        });
                    }

                    // --- DETERMINE OVERLAP LEVELS ---
                    var sortedNormalEvents = normalEvents
                        .OrderByDescending(e => e.DurationMinutes)
                        .ThenBy(e => e.StartMinute)
                        .ToList();

                    var eventLevels = new Dictionary<PrintTimeBlock, int>();

                    foreach (var ev in sortedNormalEvents)
                    {
                        var overlaps = eventLevels.Keys.Where(oEv =>
                            Math.Max(ev.StartMinute, oEv.StartMinute) < Math.Min(ev.StartMinute + ev.DurationMinutes, oEv.StartMinute + oEv.DurationMinutes) - 0.001
                        ).ToList();

                        int level = overlaps.Any() ? overlaps.Max(o => eventLevels[o]) + 1 : 0;
                        eventLevels[ev] = level;
                    }

                    // --- TASK 2: DYNAMIC COVER BLOCKS (GAP FILLERS) ---
                    // Identify areas covered by Level 0 (Base blocks)
                    var coveredIntervalsList = new List<(double Start, double End)>();
                    foreach (var ev in sortedNormalEvents.Where(e => eventLevels[e] == 0))
                    {
                        coveredIntervalsList.Add((ev.StartMinute, ev.StartMinute + ev.DurationMinutes));
                    }
                    var coveredIntervals = MergeIntervals(coveredIntervalsList.Select(x => new PrintTimeBlock { StartMinute = x.Start, DurationMinutes = x.End - x.Start }).ToList());

                    int maxLevel = eventLevels.Any() ? eventLevels.Values.Max() : 0;

                    // If a higher-level block overhangs empty canvas, generate a 100% height Cover Block
                    for (int currLevel = 1; currLevel <= maxLevel; currLevel++)
                    {
                        var levelEvents = sortedNormalEvents.Where(e => eventLevels[e] == currLevel).ToList();
                        foreach (var ev in levelEvents)
                        {
                            var eventInterval = new List<(double Start, double End)> { (ev.StartMinute, ev.StartMinute + ev.DurationMinutes) };
                            var gaps = SubtractIntervals(eventInterval, coveredIntervals);

                            // Determine final color (Smart Error check applies to Fillers too)
                            string finalColor = ev.ColorHex;
                            if (ev.BlockType == PrintBlockType.Error)
                            {
                                bool overlapsLargerError = eventLevels.Keys.Any(oEv =>
                                    oEv.BlockType == PrintBlockType.Error &&
                                    eventLevels[oEv] < currLevel &&
                                    Math.Max(ev.StartMinute, oEv.StartMinute) < Math.Min(ev.StartMinute + ev.DurationMinutes, oEv.StartMinute + oEv.DurationMinutes) - 0.001
                                );
                                finalColor = overlapsLargerError ? context.ErrorColor2 : context.ErrorColor;
                            }

                            foreach (var gap in gaps)
                            {
                                if (gap.End - gap.Start <= 0.001) continue;

                                shiftRow.Blocks.Add(new PrintTimeBlock
                                {
                                    Fingerprint = ev.Fingerprint + "_COVER_" + gap.Start,
                                    BaseFingerprint = "COVER_BASE",
                                    OriginalDurationMinutes = gap.End - gap.Start,
                                    StartMinute = gap.Start,
                                    DurationMinutes = gap.End - gap.Start,
                                    BlockType = PrintBlockType.Running, // Dummy type
                                    ColorHex = finalColor,
                                    WidthMultiplier = widthMultiplier,
                                    HeightMultiplier = 1.0, // Full 100% cover
                                    RowHeight = context.RowHeight,
                                    TopOffset = 0,
                                    PanelZIndex = 8, // Safety wallpaper layer (Behind all real blocks, above Running)
                                    VisualLeftOffset = 0,
                                    VisualWidthOffset = 0.5,
                                    DisplayStartTime = "",
                                    DisplayEndTime = "",
                                    CornerRadius = new CornerRadius(0),
                                    BlockBorder = new Thickness(0),
                                    Label = "",
                                    IsFootnote = false
                                });
                            }
                            coveredIntervalsList.Add((ev.StartMinute, ev.StartMinute + ev.DurationMinutes));
                        }
                        coveredIntervals = MergeIntervals(coveredIntervalsList.Select(x => new PrintTimeBlock { StartMinute = x.Start, DurationMinutes = x.End - x.Start }).ToList());
                    }

                    // --- GENERATE ACTUAL BLOCKS ---
                    foreach (var orig in sortedNormalEvents)
                    {
                        int level = eventLevels[orig];

                        // TASK 1: Smart Alternating Top/Bottom Alignment
                        double heightScale = 1.0;
                        double topOffset = 0;

                        if (level == 1)
                        {
                            heightScale = 0.65;
                            topOffset = context.RowHeight * (1.0 - heightScale); // Bottom Aligned
                        }
                        else if (level == 2)
                        {
                            heightScale = 0.40;
                            topOffset = 0; // Top Aligned
                        }
                        else if (level >= 3)
                        {
                            heightScale = 0.25;
                            topOffset = context.RowHeight * (1.0 - heightScale); // Bottom Aligned
                        }

                        int baseLayer = (orig.BlockType == PrintBlockType.Break || orig.BlockType == PrintBlockType.Cleaning) ? context.LayerBreaks : context.LayerErrors;
                        int finalZ = Math.Min(90, baseLayer + (level * 15));

                        string finalColor = orig.ColorHex;
                        if (orig.BlockType == PrintBlockType.Error)
                        {
                            bool overlapsLargerError = eventLevels.Keys.Any(oEv =>
                                oEv.BlockType == PrintBlockType.Error &&
                                eventLevels[oEv] < level &&
                                Math.Max(orig.StartMinute, oEv.StartMinute) < Math.Min(orig.StartMinute + orig.DurationMinutes, oEv.StartMinute + oEv.DurationMinutes) - 0.001
                            );
                            finalColor = overlapsLargerError ? context.ErrorColor2 : context.ErrorColor;
                        }

                        Thickness finalBorder = new Thickness(0);
                        if (level == 0 && context.EnableBlockBorders) finalBorder = new Thickness(0.5);

                        var block = new PrintTimeBlock
                        {
                            Fingerprint = orig.Fingerprint,
                            BaseFingerprint = orig.BaseFingerprint,
                            OriginalDurationMinutes = orig.OriginalDurationMinutes,
                            StartMinute = orig.StartMinute,
                            DurationMinutes = orig.DurationMinutes,
                            BlockType = orig.BlockType,
                            OriginalDescription = orig.OriginalDescription,
                            MachineCode = orig.MachineCode,
                            WidthMultiplier = widthMultiplier,
                            HeightMultiplier = heightScale,
                            RowHeight = context.RowHeight,
                            TopOffset = topOffset,
                            PanelZIndex = finalZ,
                            DisplayStartTime = orig.DisplayStartTime,
                            DisplayEndTime = orig.DisplayEndTime,
                            ColorHex = finalColor,
                            BlockBorder = finalBorder,
                            CornerRadius = context.EnableSoftCorners && (orig.DurationMinutes >= 5.0 || !context.DisableSoftCornersUnder5Min) ? new CornerRadius(3) : new CornerRadius(0)
                        };

                        if (!string.IsNullOrEmpty(orig.OriginalDescription))
                        {
                            var words = orig.OriginalDescription.Split(new char[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
                            block.TextDescription = words.Length >= 2 ? $"{words[0]} {words[1]}" : words[0];
                        }
                        if (!string.IsNullOrEmpty(orig.MachineCode)) block.MachineCodes.Add(orig.MachineCode);

                        shiftRow.Blocks.Add(block);
                    }

                    // --- BYPASS LOGIC ---
                    if (bypassEvents.Any())
                    {
                        var rawBypassIntervals = MergeIntervals(bypassEvents);
                        var validBypassIntervals = SubtractIntervals(rawBypassIntervals, rawStopIntervals);

                        foreach (var interval in validBypassIntervals)
                        {
                            if (interval.End - interval.Start <= 0.001) continue;

                            shiftRow.Blocks.Add(new PrintTimeBlock
                            {
                                Fingerprint = $"{group.Key.Date:yyyyMMdd}_{group.Key.Shift}_BYPASS_{interval.Start}",
                                BaseFingerprint = "BYPASS_BASE",
                                OriginalDurationMinutes = interval.End - interval.Start,
                                StartMinute = interval.Start,
                                DurationMinutes = interval.End - interval.Start,
                                BlockType = PrintBlockType.Break,
                                ColorHex = context.BreakColor,
                                WidthMultiplier = widthMultiplier,
                                HeightMultiplier = 0.35,
                                RowHeight = context.RowHeight,
                                TopOffset = 0,
                                PanelZIndex = 50,
                                DisplayStartTime = shiftStart.AddMinutes(interval.Start).ToString("HH:mm"),
                                DisplayEndTime = shiftStart.AddMinutes(interval.End).ToString("HH:mm"),
                                BlockBorder = new Thickness(context.EnableBlockBorders ? 0.5 : 0),
                                CornerRadius = context.EnableSoftCorners && (!context.DisableSoftCornersUnder5Min || (interval.End - interval.Start) >= 5.0) ? new CornerRadius(3) : new CornerRadius(0)
                            });
                        }
                    }

                    // --- FOOTNOTE AND TEXT ENFORCEMENT ---
                    foreach (var block in shiftRow.Blocks)
                    {
                        if (block.BlockType == PrintBlockType.Running || block.BlockType == PrintBlockType.FacilityStop || block.BaseFingerprint == "BYPASS_BASE" || block.BaseFingerprint == "COVER_BASE") continue;

                        if (block.BlockType == PrintBlockType.Cleaning || block.BlockType == PrintBlockType.Break || block.BlockType == PrintBlockType.OtherIdle)
                        {
                            block.IsFootnote = false;
                            block.Label = "";
                            continue;
                        }

                        double evalDur = block.OriginalDurationMinutes > 0 ? block.OriginalDurationMinutes : block.DurationMinutes;

                        if (evalDur < context.MinFootnoteMinutes)
                        {
                            block.IsFootnote = false;
                            block.Label = "";
                        }
                        else if (evalDur >= context.MinFootnoteMinutes && evalDur < context.MinLabelMinutes)
                        {
                            block.IsFootnote = true;
                            block.Label = "";
                        }
                        else
                        {
                            block.IsFootnote = false;

                            var distinctCodes = new List<string>();
                            if (!string.IsNullOrEmpty(block.MachineCode)) distinctCodes.Add(block.MachineCode);
                            if (context.HideGenelTemizlik) distinctCodes.RemoveAll(c => c == "00" || c == "0");

                            string codesStr = distinctCodes.Any() ? string.Join(" & ", distinctCodes) : "";

                            string cleanText = "";
                            if (!string.IsNullOrEmpty(block.OriginalDescription))
                            {
                                var words = block.OriginalDescription.Split(new char[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
                                cleanText = words.Length >= 2 ? $"{words[0]} {words[1]}" : words[0];
                            }

                            if (string.IsNullOrWhiteSpace(cleanText)) block.Label = codesStr;
                            else if (string.IsNullOrWhiteSpace(codesStr)) block.Label = cleanText;
                            else block.Label = $"{codesStr} {cleanText}";
                        }
                    }

                    double totalStopMins = 0;
                    foreach (var interval in rawStopIntervals)
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