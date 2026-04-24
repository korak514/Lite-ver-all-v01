// Models/PrintModels.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace WPF_LoginForm.Models
{
    public enum PrintBlockType
    { Running, Error, Break, FacilityStop, Cleaning, OtherIdle }

    public class PrintFootnote
    {
        public string Text { get; set; }
    }

    public class PrintMonthlySummary
    {
        public string Title { get; set; }
        public string AvgDailyWorkStr { get; set; }
        public string AvgDailyStopStr { get; set; }
        public string NoOvertimeShifts { get; set; }
        public string NotWorkedShifts { get; set; } // Changed to Shifts
    }

    public class PrintTimeBlock : INotifyPropertyChanged
    {
        public string Fingerprint { get; set; }
        public string BaseFingerprint { get; set; }
        public double OriginalDurationMinutes { get; set; }
        public bool IsFirstSlice { get; set; }

        private double _startMinute;

        public double StartMinute
        { get => _startMinute; set { _startMinute = value; OnPropertyChanged(); OnPropertyChanged(nameof(PixelLeft)); OnPropertyChanged(nameof(VisualPixelLeft)); } }

        private double _durationMinutes;

        public double DurationMinutes
        { get => _durationMinutes; set { _durationMinutes = value; OnPropertyChanged(); OnPropertyChanged(nameof(PixelWidth)); OnPropertyChanged(nameof(VisualPixelWidth)); } }

        public PrintBlockType BlockType { get; set; }

        private string _colorHex;

        public string ColorHex
        { get => _colorHex; set { _colorHex = value; OnPropertyChanged(); } }

        private int _panelZIndex = 10;

        public int PanelZIndex
        { get => _panelZIndex; set { _panelZIndex = value; OnPropertyChanged(); } }

        private double _widthMultiplier = 1.0;

        public double WidthMultiplier
        { get => _widthMultiplier; set { _widthMultiplier = value; OnPropertyChanged(); OnPropertyChanged(nameof(PixelLeft)); OnPropertyChanged(nameof(PixelWidth)); OnPropertyChanged(nameof(VisualPixelLeft)); OnPropertyChanged(nameof(VisualPixelWidth)); } }

        private double _heightMultiplier = 1.0;

        public double HeightMultiplier
        { get => _heightMultiplier; set { _heightMultiplier = value; OnPropertyChanged(); OnPropertyChanged(nameof(PixelHeight)); } }

        private double _rowHeight = 14.0;

        public double RowHeight
        { get => _rowHeight; set { _rowHeight = value; OnPropertyChanged(); OnPropertyChanged(nameof(PixelHeight)); } }

        private double _topOffset = 0.0;

        public double TopOffset
        { get => _topOffset; set { _topOffset = value; OnPropertyChanged(); } }

        private double _labelOffsetX = 0.0;

        public double LabelOffsetX
        {
            get => _labelOffsetX;
            set { _labelOffsetX = value; OnPropertyChanged(); OnPropertyChanged(nameof(LabelMargin)); }
        }

        private double _labelOffsetY = 0.0;

        public double LabelOffsetY
        {
            get => _labelOffsetY;
            set { _labelOffsetY = value; OnPropertyChanged(); OnPropertyChanged(nameof(LabelMargin)); }
        }

        public Thickness LabelMargin => new Thickness(LabelOffsetX, LabelOffsetY, -LabelOffsetX, -LabelOffsetY);

        private double _blockFontSize = 7.0;

        public double BlockFontSize
        { get => _blockFontSize; set { _blockFontSize = value; OnPropertyChanged(); } }

        private VerticalAlignment _textVerticalAlignment = VerticalAlignment.Center;

        public VerticalAlignment TextVerticalAlignment
        { get => _textVerticalAlignment; set { _textVerticalAlignment = value; OnPropertyChanged(); } }

        private string _label;

        public string Label
        { get => _label; set { _label = value; OnPropertyChanged(); } }

        private bool _isSelected;

        public bool IsSelected
        { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }

        private bool _isFootnote;

        public bool IsFootnote
        { get => _isFootnote; set { _isFootnote = value; OnPropertyChanged(); } }

        private Thickness _blockBorder = new Thickness(0);

        public Thickness BlockBorder
        { get => _blockBorder; set { _blockBorder = value; OnPropertyChanged(); } }

        private CornerRadius _cornerRadius = new CornerRadius(0);

        public CornerRadius CornerRadius
        { get => _cornerRadius; set { _cornerRadius = value; OnPropertyChanged(); } }

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

        private string _displayStartTime;

        public string DisplayStartTime
        { get => _displayStartTime; set { _displayStartTime = value; OnPropertyChanged(); } }

        private string _displayEndTime;

        public string DisplayEndTime
        { get => _displayEndTime; set { _displayEndTime = value; OnPropertyChanged(); } }

        public double PixelLeft => StartMinute * WidthMultiplier;
        public double PixelWidth => DurationMinutes * WidthMultiplier;
        public double PixelHeight => RowHeight * HeightMultiplier;

        private double _visualLeftOffset = 0.0;

        public double VisualLeftOffset
        { get => _visualLeftOffset; set { _visualLeftOffset = value; OnPropertyChanged(); OnPropertyChanged(nameof(VisualPixelLeft)); OnPropertyChanged(nameof(VisualPixelWidth)); } }

        private double _visualWidthOffset = 0.0;

        public double VisualWidthOffset
        { get => _visualWidthOffset; set { _visualWidthOffset = value; OnPropertyChanged(); OnPropertyChanged(nameof(VisualPixelWidth)); } }

        public double VisualPixelLeft => PixelLeft + VisualLeftOffset;
        public double VisualPixelWidth => Math.Max(0, PixelWidth + 0.5 + VisualWidthOffset);

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
        public string ReportTitle { get; set; } = "Shift Timeline Report";
        public string DateRangeText { get; set; }
        public string ExtraTitle1 { get; set; }
        public string ExtraTitle2 { get; set; }
        public bool ShowExtra1 { get; set; }
        public bool ShowExtra2 { get; set; }
        public bool ShowMajorErrorLabels { get; set; }
        public bool ShowMachineLegend { get; set; }
        public bool ShowMonthSummary { get; set; }

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
        public double InnerLabelFontSize { get; set; } = 7.0;

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

        public PrintMonthlySummary MonthlySummary { get; set; }
        public List<MachineLegendItem> LegendItems { get; set; } = new List<MachineLegendItem>();
        public List<PrintAxisTick> AxisTicks { get; set; } = new List<PrintAxisTick>();

        public ObservableCollection<PrintFootnote> Footnotes { get; set; } = new ObservableCollection<PrintFootnote>();
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
        public double InnerLabelFontSize { get; set; }
        public bool ShowValuesInHours { get; set; }
        public bool ShowMajorErrorLabels { get; set; }
        public bool ShowMachineLegend { get; set; }
        public bool ShowMonthSummary { get; set; }
        public bool DetailedOverlapView { get; set; }
        public bool HideGenelTemizlik { get; set; }
        public string ExtraCol1Selection { get; set; }
        public string ExtraCol2Selection { get; set; }
        public int LayerRunning { get; set; }
        public int LayerBypass { get; set; }
        public int LayerBreaks { get; set; }
        public int LayerErrors { get; set; }
        public double OverlapCascadeStep { get; set; }
        public double MinLabelMinutes { get; set; }
        public double MinFootnoteMinutes { get; set; }

        public bool EnableSoftCorners { get; set; }
        public bool EnableBlockBorders { get; set; }
        public bool DisableSoftCornersUnder5Min { get; set; }
    }
}