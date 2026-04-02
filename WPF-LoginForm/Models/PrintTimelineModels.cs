// Models/PrintTimelineModels.cs
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WPF_LoginForm.Models
{
    public enum PrintBlockType
    { Running, Error, Break, FacilityStop, Cleaning, OtherIdle }

    public class PrintTimeBlock : INotifyPropertyChanged
    {
        public double StartMinute { get; set; }
        public double DurationMinutes { get; set; }
        public PrintBlockType BlockType { get; set; }

        private string _colorHex;
        public string ColorHex
        { get => _colorHex; set { _colorHex = value; OnPropertyChanged(); } }

        private string _label;
        public string Label
        { get => _label; set { _label = value; OnPropertyChanged(); } }

        private bool _isSelected;
        public bool IsSelected
        { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }

        private bool _isFootnote;
        public bool IsFootnote
        { get => _isFootnote; set { _isFootnote = value; OnPropertyChanged(); } }

        public List<string> MachineCodes { get; set; } = new List<string>();

        private string _textDescription;
        public string TextDescription
        { get => _textDescription; set { _textDescription = value; OnPropertyChanged(); } }

        private string _machineCode;
        public string MachineCode
        { get => _machineCode; set { _machineCode = value; OnPropertyChanged(); } }

        private string _originalDescription;
        public string OriginalDescription
        { get => _originalDescription; set { _originalDescription = value; OnPropertyChanged(); } }

        // Real-time data mapped for the Editor Panel
        public string DisplayStartTime { get; set; }

        public string DisplayEndTime { get; set; }

        public double WidthMultiplier { get; set; } = 1.0;
        public double HeightMultiplier { get; set; } = 1.0;
        public double TopOffset { get; set; } = 0.0;

        public double PixelLeft => StartMinute * WidthMultiplier;
        public double PixelWidth => DurationMinutes * WidthMultiplier;

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class PrintAxisTick
    {
        public string TopLabel { get; set; }
        public string BottomLabel { get; set; }
        public double PositionPercent { get; set; }
        public double TimelineWidth { get; set; }
        public double RowHeight { get; set; }
        public double PixelLeft => TimelineWidth * PositionPercent;
        public double TextPixelLeft => PixelLeft - 20;
    }

    public class PrintShiftRow
    {
        public DateTime Date { get; set; }
        public string ShiftName { get; set; }
        public string DisplayDate => Date.ToString("dd.MM");
        public string DisplayShift => ShiftName.Contains("Day") || ShiftName.Contains("Gündüz") ? "☼" : "☾";
        public bool ShowDate { get; set; }
        public bool IsAlternateBackground { get; set; }
        public string ExtraValue1 { get; set; }
        public string ExtraValue2 { get; set; }
        public List<PrintTimeBlock> Blocks { get; set; } = new List<PrintTimeBlock>();
    }

    public class MachineLegendItem
    { public string Code { get; set; } public string Name { get; set; } }

    public class PrintReportConfig
    {
        public string ReportTitle { get; set; } = "17-Day Shift Timeline Report";
        public string DateRangeText { get; set; }
        public string ExtraTitle1 { get; set; }
        public string ExtraTitle2 { get; set; }
        public bool ShowExtra1 { get; set; }
        public bool ShowExtra2 { get; set; }
        public bool ShowMajorErrorLabels { get; set; }
        public bool ShowMachineLegend { get; set; }
        public string RunningColor { get; set; }
        public string ErrorColor { get; set; }
        public string ErrorColor2 { get; set; }
        public string BreakColor { get; set; }
        public string FacilityStopColor { get; set; }
        public string ColorMa00Genel { get; set; }
        public string ColorMa00Cay { get; set; }
        public string ColorMa00Yemek { get; set; }
        public string ColorMa00Other { get; set; }
        public string InnerLabelColor { get; set; }

        public double RowHeight { get; set; } = 14;
        public double FootnoteFontSize { get; set; } = 10;

        public double TimelineWidth { get; set; } = 778;
        public double TextFontSize => Math.Max(7, RowHeight * 0.75);
        public double SymbolFontSize => Math.Max(9, RowHeight * 0.90);
        public double InnerLabelFontSize => Math.Max(5.5, RowHeight * 0.50);

        public string HeaderDate { get; set; }
        public string HeaderShift { get; set; }
        public string LegendRunning { get; set; }
        public string LegendBreak { get; set; }
        public string LegendError { get; set; }
        public string LegendError2 { get; set; }
        public string LegendFacilityStop { get; set; }
        public string LegendMa00Genel { get; set; }
        public string LegendMa00Cay { get; set; }
        public string LegendMa00Yemek { get; set; }
        public string LegendMa00Other { get; set; }

        public List<MachineLegendItem> LegendItems { get; set; } = new List<MachineLegendItem>();
        public List<PrintAxisTick> AxisTicks { get; set; } = new List<PrintAxisTick>();
        public List<string> Footnotes { get; set; } = new List<string>();
    }

    public class PrintReportSettingsState
    {
        public string SelectedTable { get; set; }
        public string ExcelFilePath { get; set; }
        public string RunningColor { get; set; }
        public string ErrorColor { get; set; }
        public string ErrorColor2 { get; set; }
        public string BreakColor { get; set; }
        public string FacilityStopColor { get; set; }
        public string ColorMa00Genel { get; set; }
        public string ColorMa00Cay { get; set; }
        public string ColorMa00Yemek { get; set; }
        public string ColorMa00Other { get; set; }
        public string InnerLabelColor { get; set; }
        public double RowHeight { get; set; }
        public double FootnoteFontSize { get; set; }
        public bool ShowValuesInHours { get; set; }
        public bool ShowMajorErrorLabels { get; set; }
        public bool ShowMachineLegend { get; set; }
        public bool DetailedOverlapView { get; set; }
        public bool HideGenelTemizlik { get; set; }
        public string ExtraCol1Selection { get; set; }
        public string ExtraCol2Selection { get; set; }
    }
}