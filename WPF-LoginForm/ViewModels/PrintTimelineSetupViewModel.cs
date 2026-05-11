// ViewModels/PrintTimelineSetupViewModel.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Newtonsoft.Json;
using OfficeOpenXml;
using WPF_LoginForm.Models;
using WPF_LoginForm.Repositories;
using WPF_LoginForm.Services;

namespace WPF_LoginForm.ViewModels
{
    public class BlockOverride
    {
        public string ColorHex { get; set; }
        public string Label { get; set; }
        public string MachineCode { get; set; }
        public string Description { get; set; }
        public bool IsFootnote { get; set; }
        public string DisplayStartTime { get; set; }
        public string DisplayEndTime { get; set; }
        public double HeightMultiplier { get; set; }
        public double TopOffset { get; set; }
        public int PanelZIndex { get; set; }
        public double LabelOffsetX { get; set; }
        public double LabelOffsetY { get; set; }
        public double BlockFontSize { get; set; }
    }

    public class PrintTimelineSetupViewModel : ViewModelBase
    {
        private readonly IDataRepository _dataRepository;
        private readonly IDialogService _dialogService;
        private readonly ITimelineReportGenerator _reportGenerator;
        private readonly string _settingsFilePath;

        private readonly Dictionary<string, BlockOverride> _blockOverrides = new Dictionary<string, BlockOverride>();

        private BlockOverride _originalBlockState;
        private DataTable _cachedData;
        private string _cachedTableName;

        private string LocTotalStop => WPF_LoginForm.Properties.Resources.P_Total_Stop ?? "Total Stop / Toplam Duruş";
        private string LocActualWork => WPF_LoginForm.Properties.Resources.P_Working_Time ?? "Actual Work / Çalışma Süresi";

        public event Action PrintRequested;

        public ObservableCollection<string> AvailableTables { get; } = new ObservableCollection<string>();

        private string _selectedTable;
        public string SelectedTable
        { get => _selectedTable; set { SetProperty(ref _selectedTable, value); _cachedData = null; } }

        private string _excelFilePath;
        public string ExcelFilePath
        { get => _excelFilePath; set { if (SetProperty(ref _excelFilePath, value)) OnPropertyChanged(nameof(HasExcelFile)); } }
        public bool HasExcelFile => !string.IsNullOrEmpty(ExcelFilePath);

        private bool _isUpdatingDates = false;

        private DateTime _startDate = DateTime.Today.AddDays(-12);

        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                if (SetProperty(ref _startDate, value) && !_isUpdatingDates)
                {
                    _isUpdatingDates = true; EndDate = _startDate.AddDays(12); _isUpdatingDates = false;
                }
            }
        }

        private DateTime _endDate = DateTime.Today;

        public DateTime EndDate
        {
            get => _endDate;
            set
            {
                if (SetProperty(ref _endDate, value) && !_isUpdatingDates)
                {
                    _isUpdatingDates = true;
                    if ((_endDate - _startDate).TotalDays > 20) EndDate = _startDate.AddDays(20);
                    else if (_endDate < _startDate) EndDate = _startDate;
                    _isUpdatingDates = false;
                }
            }
        }

        private double _zoomScale = 1.0;
        public double ZoomScale { get => _zoomScale; set => SetProperty(ref _zoomScale, value); }

        private Color _runningColor = (Color)ColorConverter.ConvertFromString("#2ECC71");
        public Color RunningColor { get => _runningColor; set => SetProperty(ref _runningColor, value); }
        private Color _errorColor = (Color)ColorConverter.ConvertFromString("#E74C3C");
        public Color ErrorColor { get => _errorColor; set => SetProperty(ref _errorColor, value); }
        private Color _errorColor2 = (Color)ColorConverter.ConvertFromString("#C0392B");
        public Color ErrorColor2 { get => _errorColor2; set => SetProperty(ref _errorColor2, value); }
        private Color _bypassColor = (Color)ColorConverter.ConvertFromString("#808080");
        public Color BypassColor { get => _bypassColor; set => SetProperty(ref _bypassColor, value); }
        private Color _facilityStopColor = (Color)ColorConverter.ConvertFromString("#34495E");
        public Color FacilityStopColor { get => _facilityStopColor; set => SetProperty(ref _facilityStopColor, value); }

        private Color _colorMa00Genel = (Color)ColorConverter.ConvertFromString("#3498DB");
        public Color ColorMa00Genel { get => _colorMa00Genel; set => SetProperty(ref _colorMa00Genel, value); }
        private Color _colorMa00Cay = (Color)ColorConverter.ConvertFromString("#9B59B6");
        public Color ColorMa00Cay { get => _colorMa00Cay; set => SetProperty(ref _colorMa00Cay, value); }
        private Color _colorMa00Yemek = (Color)ColorConverter.ConvertFromString("#E67E22");
        public Color ColorMa00Yemek { get => _colorMa00Yemek; set => SetProperty(ref _colorMa00Yemek, value); }
        private Color _colorMa00Other = (Color)ColorConverter.ConvertFromString("#95A5A6");
        public Color ColorMa00Other { get => _colorMa00Other; set => SetProperty(ref _colorMa00Other, value); }

        private Color _innerLabelColor = (Color)ColorConverter.ConvertFromString("#FFFFFF");
        public Color InnerLabelColor { get => _innerLabelColor; set => SetProperty(ref _innerLabelColor, value); }

        private double _rowHeight = 14.0;
        public double RowHeight { get => _rowHeight; set => SetProperty(ref _rowHeight, value); }

        private double _footnoteFontSize = 8.0;
        public double FootnoteFontSize { get => _footnoteFontSize; set => SetProperty(ref _footnoteFontSize, value); }

        private double _innerLabelFontSize = 6.5;
        public double InnerLabelFontSize { get => _innerLabelFontSize; set => SetProperty(ref _innerLabelFontSize, value); }

        private int _layerRunning = 1;
        public int LayerRunning { get => _layerRunning; set => SetProperty(ref _layerRunning, value); }
        private int _layerBypass = 2;
        public int LayerBypass { get => _layerBypass; set => SetProperty(ref _layerBypass, value); }
        private int _layerBreaks = 10;
        public int LayerBreaks { get => _layerBreaks; set => SetProperty(ref _layerBreaks, value); }
        private int _layerErrors = 20;
        public int LayerErrors { get => _layerErrors; set => SetProperty(ref _layerErrors, value); }

        private double _overlapCascadeStep = 0.20;
        public double OverlapCascadeStep { get => _overlapCascadeStep; set => SetProperty(ref _overlapCascadeStep, value); }

        private double _minLabelMinutes = 90.0;
        public double MinLabelMinutes { get => _minLabelMinutes; set => SetProperty(ref _minLabelMinutes, value); }
        private double _minFootnoteMinutes = 20.0;
        public double MinFootnoteMinutes { get => _minFootnoteMinutes; set => SetProperty(ref _minFootnoteMinutes, value); }

        private bool _showValuesInHours = false;
        public bool ShowValuesInHours { get => _showValuesInHours; set => SetProperty(ref _showValuesInHours, value); }
        private bool _showMajorErrorLabels = true;
        public bool ShowMajorErrorLabels { get => _showMajorErrorLabels; set => SetProperty(ref _showMajorErrorLabels, value); }
        private bool _showMachineLegend = true;
        public bool ShowMachineLegend { get => _showMachineLegend; set => SetProperty(ref _showMachineLegend, value); }
        private bool _showMonthSummary = true; // NEW
        public bool ShowMonthSummary { get => _showMonthSummary; set => SetProperty(ref _showMonthSummary, value); }

        private bool _detailedOverlapView = true;
        public bool DetailedOverlapView { get => _detailedOverlapView; set => SetProperty(ref _detailedOverlapView, value); }
        private bool _hideGenelTemizlik = true;
        public bool HideGenelTemizlik { get => _hideGenelTemizlik; set => SetProperty(ref _hideGenelTemizlik, value); }

        private bool _enableSoftCorners = true;
        public bool EnableSoftCorners { get => _enableSoftCorners; set => SetProperty(ref _enableSoftCorners, value); }
        private bool _enableBlockBorders = true;
        public bool EnableBlockBorders { get => _enableBlockBorders; set => SetProperty(ref _enableBlockBorders, value); }
        private bool _disableSoftCornersUnder5Min = true;
        public bool DisableSoftCornersUnder5Min { get => _disableSoftCornersUnder5Min; set => SetProperty(ref _disableSoftCornersUnder5Min, value); }

        public ObservableCollection<string> AvailableExtraOptions { get; } = new ObservableCollection<string>();
        private string _extraCol1Selection;
        public string ExtraCol1Selection { get => _extraCol1Selection; set => SetProperty(ref _extraCol1Selection, value); }
        private string _extraCol2Selection;
        public string ExtraCol2Selection { get => _extraCol2Selection; set => SetProperty(ref _extraCol2Selection, value); }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        private bool _isEditingBlock;
        public bool IsEditingBlock { get => _isEditingBlock; set => SetProperty(ref _isEditingBlock, value); }

        private PrintTimeBlock _selectedBlock;
        public PrintTimeBlock SelectedBlock { get => _selectedBlock; set => SetProperty(ref _selectedBlock, value); }

        private Color _editBlockColor;
        public Color EditBlockColor
        { get => _editBlockColor; set { if (SetProperty(ref _editBlockColor, value) && SelectedBlock != null) SelectedBlock.ColorHex = value.ToString(); } }

        private string _editBlockLabel;
        public string EditBlockLabel
        { get => _editBlockLabel; set { if (SetProperty(ref _editBlockLabel, value) && SelectedBlock != null) SelectedBlock.Label = value; } }

        private string _editBlockMachine;
        public string EditBlockMachine
        { get => _editBlockMachine; set { if (SetProperty(ref _editBlockMachine, value)) { RefreshBlockCategories(false); if (SelectedBlock != null) SelectedBlock.MachineCode = value; } } }

        private string _editBlockDesc;
        public string EditBlockDesc
        { get => _editBlockDesc; set { if (SetProperty(ref _editBlockDesc, value) && SelectedBlock != null) SelectedBlock.OriginalDescription = value; } }

        private bool _editBlockIsFootnote;
        public bool EditBlockIsFootnote
        { get => _editBlockIsFootnote; set { if (SetProperty(ref _editBlockIsFootnote, value) && SelectedBlock != null) SelectedBlock.IsFootnote = value; } }

        private string _editBlockTimeData;
        public string EditBlockTimeData { get => _editBlockTimeData; set => SetProperty(ref _editBlockTimeData, value); }

        private double _editBlockHeightMultiplier = 1.0;
        public double EditBlockHeightMultiplier
        { get => _editBlockHeightMultiplier; set { if (SetProperty(ref _editBlockHeightMultiplier, value) && SelectedBlock != null) SelectedBlock.HeightMultiplier = value; } }

        private double _editBlockTopOffset = 0;
        public double EditBlockTopOffset
        { get => _editBlockTopOffset; set { if (SetProperty(ref _editBlockTopOffset, value) && SelectedBlock != null) SelectedBlock.TopOffset = value; } }

        private int _editBlockZIndex = 20;
        public int EditBlockZIndex
        { get => _editBlockZIndex; set { if (SetProperty(ref _editBlockZIndex, value) && SelectedBlock != null) SelectedBlock.PanelZIndex = value; } }

        public ObservableCollection<string> AvailableBlockCategories { get; } = new ObservableCollection<string>();
        private string _selectedBlockCategory;
        public string SelectedBlockCategory
        { get => _selectedBlockCategory; set { if (SetProperty(ref _selectedBlockCategory, value)) UpdateColorFromCategory(value); } }

        private double _editBlockLabelOffsetX;
        public double EditBlockLabelOffsetX
        { get => _editBlockLabelOffsetX; set { if (SetProperty(ref _editBlockLabelOffsetX, value) && SelectedBlock != null) SelectedBlock.LabelOffsetX = value; } }

        private double _editBlockLabelOffsetY;
        public double EditBlockLabelOffsetY
        { get => _editBlockLabelOffsetY; set { if (SetProperty(ref _editBlockLabelOffsetY, value) && SelectedBlock != null) SelectedBlock.LabelOffsetY = value; } }

        private double _editBlockFontSize;
        public double EditBlockFontSize
        { get => _editBlockFontSize; set { if (SetProperty(ref _editBlockFontSize, value) && SelectedBlock != null) SelectedBlock.BlockFontSize = value; } }

        public ICommand SelectBlockCommand { get; }
        public ICommand ApplyBlockEditCommand { get; }
        public ICommand CancelBlockEditCommand { get; }
        public ICommand ResetOverridesCommand { get; }
        public ICommand BrowseExcelCommand { get; }
        public ICommand ClearExcelCommand { get; }
        public ICommand GeneratePreviewCommand { get; }
        public ICommand PrintCommand { get; }

        private PrintReportConfig _reportConfig;
        public PrintReportConfig ReportConfig { get => _reportConfig; set => SetProperty(ref _reportConfig, value); }
        public ObservableCollection<PrintShiftRow> ReportRows { get; } = new ObservableCollection<PrintShiftRow>();

        public PrintTimelineSetupViewModel(IDataRepository dataRepository, IDialogService dialogService)
        {
            _dataRepository = dataRepository; _dialogService = dialogService;
            _reportGenerator = new TimelineReportGenerator();
            _settingsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WPF_LoginForm", "print_report_settings.json");

            ResetExtraOptions();
            _extraCol1Selection = LocTotalStop; _extraCol2Selection = LocActualWork;

            BrowseExcelCommand = new ViewModelCommand(ExecuteBrowseExcel);
            ClearExcelCommand = new ViewModelCommand(p => { ExcelFilePath = ""; ResetExtraOptions(); });
            GeneratePreviewCommand = new ViewModelCommand(ExecuteGeneratePreview, p => !string.IsNullOrEmpty(SelectedTable) && !IsBusy);
            PrintCommand = new ViewModelCommand(p => PrintRequested?.Invoke(), p => ReportRows.Count > 0 && !IsBusy);

            SelectBlockCommand = new ViewModelCommand(ExecuteSelectBlock);
            ApplyBlockEditCommand = new ViewModelCommand(ExecuteApplyBlockEdit);
            CancelBlockEditCommand = new ViewModelCommand(ExecuteCancelBlockEdit);
            ResetOverridesCommand = new ViewModelCommand(ExecuteResetOverrides, p => _blockOverrides.Count > 0);

            _ = InitializeAsyncSafe();
        }

        private async Task InitializeAsyncSafe()
        {
            try { await InitializeAsync(); }
            catch (Exception ex) { MessageBox.Show($"Failed to initialize: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void ExecuteResetOverrides(object obj)
        {
            _blockOverrides.Clear(); (ResetOverridesCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
            if (GeneratePreviewCommand.CanExecute(null)) GeneratePreviewCommand.Execute(null);
        }

        private void ExecuteSelectBlock(object obj)
        {
            if (obj is PrintTimeBlock block)
            {
                if (SelectedBlock != null && IsEditingBlock) ExecuteCancelBlockEdit(null);

                SelectedBlock = block; SelectedBlock.IsSelected = true;

                _originalBlockState = new BlockOverride
                {
                    ColorHex = block.ColorHex,
                    Label = block.Label,
                    MachineCode = block.MachineCode,
                    Description = block.OriginalDescription,
                    IsFootnote = block.IsFootnote,
                    DisplayStartTime = block.DisplayStartTime,
                    DisplayEndTime = block.DisplayEndTime,
                    HeightMultiplier = block.HeightMultiplier,
                    TopOffset = block.TopOffset,
                    PanelZIndex = block.PanelZIndex,
                    LabelOffsetX = block.LabelOffsetX,
                    LabelOffsetY = block.LabelOffsetY,
                    BlockFontSize = block.BlockFontSize
                };

                _editBlockMachine = block.MachineCode; OnPropertyChanged(nameof(EditBlockMachine));
                EditBlockLabel = block.Label; EditBlockDesc = block.OriginalDescription; EditBlockIsFootnote = block.IsFootnote;
                EditBlockTimeData = $"{block.DisplayStartTime} - {block.DisplayEndTime} ({block.DurationMinutes:F0} mins)";
                EditBlockHeightMultiplier = block.HeightMultiplier; EditBlockTopOffset = block.TopOffset; EditBlockZIndex = block.PanelZIndex;

                _editBlockLabelOffsetX = block.LabelOffsetX; OnPropertyChanged(nameof(EditBlockLabelOffsetX));
                _editBlockLabelOffsetY = block.LabelOffsetY; OnPropertyChanged(nameof(EditBlockLabelOffsetY));
                _editBlockFontSize = block.BlockFontSize; OnPropertyChanged(nameof(EditBlockFontSize));

                RefreshBlockCategories(true);
                DetermineCurrentCategory(block);

                IsEditingBlock = true;
            }
        }

        private void DetermineCurrentCategory(PrintTimeBlock block)
        {
            string currentCategory = ""; string mCode = (EditBlockMachine ?? "").Replace("MA-", "").Trim();
            bool isMa00 = (mCode == "00" || mCode == "0");

            if (isMa00)
            {
                if (block.ColorHex == ColorMa00Genel.ToString()) currentCategory = "Genel Temizlik";
                else if (block.ColorHex == ColorMa00Cay.ToString()) currentCategory = "Çay Molası";
                else if (block.ColorHex == ColorMa00Yemek.ToString()) currentCategory = "Yemek Molası";
                else currentCategory = "Diğer MA-00";
            }
            else
            {
                if (block.ColorHex == RunningColor.ToString()) currentCategory = "Running";
                else if (block.ColorHex == ErrorColor.ToString()) currentCategory = "Primary Error";
                else if (block.ColorHex == ErrorColor2.ToString()) currentCategory = "Overlap Error";
                else if (block.ColorHex == BypassColor.ToString()) currentCategory = "Bypass";
                else if (block.ColorHex == FacilityStopColor.ToString()) currentCategory = "Facility Stop";
                else currentCategory = "Primary Error";
            }

            _selectedBlockCategory = currentCategory; OnPropertyChanged(nameof(SelectedBlockCategory));
            EditBlockColor = (Color)ColorConverter.ConvertFromString(block.ColorHex);
        }

        private void RefreshBlockCategories(bool initialLoad = false)
        {
            string mCode = (EditBlockMachine ?? "").Replace("MA-", "").Trim();
            bool isMa00 = (mCode == "00" || mCode == "0");
            bool isCurrentlyMa00List = AvailableBlockCategories.Contains("Genel Temizlik");

            if (initialLoad || (isMa00 && !isCurrentlyMa00List) || (!isMa00 && isCurrentlyMa00List) || AvailableBlockCategories.Count == 0)
            {
                AvailableBlockCategories.Clear();
                if (isMa00)
                {
                    AvailableBlockCategories.Add("Genel Temizlik"); AvailableBlockCategories.Add("Çay Molası");
                    AvailableBlockCategories.Add("Yemek Molası"); AvailableBlockCategories.Add("Diğer MA-00");
                    if (!initialLoad) SelectedBlockCategory = "Diğer MA-00";
                }
                else
                {
                    AvailableBlockCategories.Add("Running"); AvailableBlockCategories.Add("Primary Error");
                    AvailableBlockCategories.Add("Overlap Error"); AvailableBlockCategories.Add("Bypass");
                    AvailableBlockCategories.Add("Facility Stop");
                    if (!initialLoad) SelectedBlockCategory = "Primary Error";
                }
            }
        }

        private void UpdateColorFromCategory(string category)
        {
            if (string.IsNullOrEmpty(category)) return;
            if (category == "Genel Temizlik") EditBlockColor = ColorMa00Genel;
            else if (category == "Çay Molası") EditBlockColor = ColorMa00Cay;
            else if (category == "Yemek Molası") EditBlockColor = ColorMa00Yemek;
            else if (category == "Diğer MA-00") EditBlockColor = ColorMa00Other;
            else if (category == "Running") EditBlockColor = RunningColor;
            else if (category == "Primary Error") EditBlockColor = ErrorColor;
            else if (category == "Overlap Error") EditBlockColor = ErrorColor2;
            else if (category == "Bypass") EditBlockColor = BypassColor;
            else if (category == "Facility Stop") EditBlockColor = FacilityStopColor;
        }

        private void ExecuteApplyBlockEdit(object obj)
        {
            if (SelectedBlock == null || string.IsNullOrEmpty(SelectedBlock.Fingerprint)) return;

            _blockOverrides[SelectedBlock.Fingerprint] = new BlockOverride
            {
                ColorHex = SelectedBlock.ColorHex,
                Label = SelectedBlock.Label,
                MachineCode = SelectedBlock.MachineCode,
                Description = SelectedBlock.OriginalDescription,
                IsFootnote = SelectedBlock.IsFootnote,
                DisplayStartTime = SelectedBlock.DisplayStartTime,
                DisplayEndTime = SelectedBlock.DisplayEndTime,
                HeightMultiplier = SelectedBlock.HeightMultiplier,
                TopOffset = SelectedBlock.TopOffset,
                PanelZIndex = SelectedBlock.PanelZIndex,
                LabelOffsetX = SelectedBlock.LabelOffsetX,
                LabelOffsetY = SelectedBlock.LabelOffsetY,
                BlockFontSize = SelectedBlock.BlockFontSize
            };

            (ResetOverridesCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
            SelectedBlock.IsSelected = false; SelectedBlock = null; _originalBlockState = null; IsEditingBlock = false; RebuildFootnotesAndLabels();
        }

        private void ExecuteCancelBlockEdit(object obj)
        {
            if (SelectedBlock != null && _originalBlockState != null)
            {
                SelectedBlock.ColorHex = _originalBlockState.ColorHex; SelectedBlock.Label = _originalBlockState.Label;
                SelectedBlock.MachineCode = _originalBlockState.MachineCode; SelectedBlock.OriginalDescription = _originalBlockState.Description;
                SelectedBlock.IsFootnote = _originalBlockState.IsFootnote; SelectedBlock.HeightMultiplier = _originalBlockState.HeightMultiplier;
                SelectedBlock.TopOffset = _originalBlockState.TopOffset; SelectedBlock.PanelZIndex = _originalBlockState.PanelZIndex;
                SelectedBlock.LabelOffsetX = _originalBlockState.LabelOffsetX;
                SelectedBlock.LabelOffsetY = _originalBlockState.LabelOffsetY;
                SelectedBlock.BlockFontSize = _originalBlockState.BlockFontSize;
                SelectedBlock.IsSelected = false;
            }
            SelectedBlock = null; _originalBlockState = null; IsEditingBlock = false;
        }

        private void RebuildFootnotesAndLabels()
        {
            if (ReportConfig == null) return;
            var newFootnotes = new List<string>(); int counter = 1;

            foreach (var row in ReportRows)
            {
                var sortedFootnotes = row.Blocks.Where(b => b.IsFootnote).OrderBy(b => b.StartMinute + (b.DurationMinutes / 2.0)).ToList();
                if (!sortedFootnotes.Any()) continue;

                var clusters = new List<List<PrintTimeBlock>>();
                foreach (var b in sortedFootnotes)
                {
                    double center = b.StartMinute + (b.DurationMinutes / 2.0); bool added = false;
                    if (clusters.Any())
                    {
                        var lastCluster = clusters.Last();
                        var lastCenter = lastCluster.First().StartMinute + (lastCluster.First().DurationMinutes / 2.0);
                        if (Math.Abs(center - lastCenter) <= 25.0) { lastCluster.Add(b); added = true; }
                    }
                    if (!added) clusters.Add(new List<PrintTimeBlock> { b });
                }

                foreach (var cluster in clusters)
                {
                    var markers = new List<int>();
                    foreach (var b in cluster)
                    {
                        markers.Add(counter); string markerStr = $"[{counter}]";
                        string desc = string.IsNullOrEmpty(b.OriginalDescription) ? "Arıza/Bakım" : b.OriginalDescription;
                        if (desc.Length > 20) desc = desc.Substring(0, 20).TrimEnd() + "...";
                        string mCode = string.IsNullOrEmpty(b.MachineCode) ? "" : $"{b.MachineCode} - ";
                        newFootnotes.Add($"{markerStr} {mCode}{desc} ({b.DurationMinutes:F0}dk)");
                        counter++;
                    }

                    string combinedMarker = string.Join("-", markers.Select(m => $"[{m}]"));
                    var targetBlock = cluster.OrderByDescending(b => b.DurationMinutes).First();
                    foreach (var b in cluster) b.Label = (b == targetBlock) ? combinedMarker : "";
                }
            }

            ReportConfig.Footnotes.Clear();
            foreach (var footnote in newFootnotes) ReportConfig.Footnotes.Add(new PrintFootnote { Text = footnote });
        }

        private async Task InitializeAsync()
        {
            var tables = await _dataRepository.GetTableNamesAsync();
            Application.Current.Dispatcher.Invoke(() =>
            {
                AvailableTables.Clear(); foreach (var t in tables) AvailableTables.Add(t); LoadState();
                if (string.IsNullOrEmpty(SelectedTable) && AvailableTables.Any()) SelectedTable = AvailableTables[0];
            });
        }

        private void SaveState()
        {
            try
            {
                var state = new PrintReportSettingsState
                {
                    SelectedTable = this.SelectedTable,
                    ExcelFilePath = this.ExcelFilePath,
                    RunningColor = this.RunningColor.ToString(),
                    ErrorColor = this.ErrorColor.ToString(),
                    ErrorColor2 = this.ErrorColor2.ToString(),
                    BreakColor = this.BypassColor.ToString(),
                    FacilityStopColor = this.FacilityStopColor.ToString(),
                    ColorMa00Genel = this.ColorMa00Genel.ToString(),
                    ColorMa00Cay = this.ColorMa00Cay.ToString(),
                    ColorMa00Yemek = this.ColorMa00Yemek.ToString(),
                    ColorMa00Other = this.ColorMa00Other.ToString(),
                    InnerLabelColor = this.InnerLabelColor.ToString(),
                    RowHeight = this.RowHeight,
                    FootnoteFontSize = this.FootnoteFontSize,
                    InnerLabelFontSize = this.InnerLabelFontSize,
                    ShowValuesInHours = this.ShowValuesInHours,
                    ShowMajorErrorLabels = this.ShowMajorErrorLabels,
                    ShowMachineLegend = this.ShowMachineLegend,
                    ShowMonthSummary = this.ShowMonthSummary,
                    DetailedOverlapView = this.DetailedOverlapView,
                    HideGenelTemizlik = this.HideGenelTemizlik,
                    ExtraCol1Selection = this.ExtraCol1Selection,
                    ExtraCol2Selection = this.ExtraCol2Selection,
                    LayerRunning = this.LayerRunning,
                    LayerBypass = this.LayerBypass,
                    LayerBreaks = this.LayerBreaks,
                    LayerErrors = this.LayerErrors,
                    OverlapCascadeStep = this.OverlapCascadeStep,
                    MinLabelMinutes = this.MinLabelMinutes,
                    MinFootnoteMinutes = this.MinFootnoteMinutes,
                    EnableSoftCorners = this.EnableSoftCorners,
                    EnableBlockBorders = this.EnableBlockBorders,
                    DisableSoftCornersUnder5Min = this.DisableSoftCornersUnder5Min
                };
                string dir = Path.GetDirectoryName(_settingsFilePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(_settingsFilePath, JsonConvert.SerializeObject(state));
            }
            catch { }
        }

        private void LoadState()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    var state = JsonConvert.DeserializeObject<PrintReportSettingsState>(json);
                    if (state != null)
                    {
                        if (AvailableTables.Contains(state.SelectedTable)) SelectedTable = state.SelectedTable;
                        ExcelFilePath = state.ExcelFilePath;
                        if (!string.IsNullOrEmpty(state.RunningColor)) RunningColor = (Color)ColorConverter.ConvertFromString(state.RunningColor);
                        if (!string.IsNullOrEmpty(state.ErrorColor)) ErrorColor = (Color)ColorConverter.ConvertFromString(state.ErrorColor);
                        if (!string.IsNullOrEmpty(state.ErrorColor2)) ErrorColor2 = (Color)ColorConverter.ConvertFromString(state.ErrorColor2);
                        if (!string.IsNullOrEmpty(state.BreakColor)) BypassColor = (Color)ColorConverter.ConvertFromString(state.BreakColor);
                        if (!string.IsNullOrEmpty(state.FacilityStopColor)) FacilityStopColor = (Color)ColorConverter.ConvertFromString(state.FacilityStopColor);
                        if (!string.IsNullOrEmpty(state.ColorMa00Genel)) ColorMa00Genel = (Color)ColorConverter.ConvertFromString(state.ColorMa00Genel);
                        if (!string.IsNullOrEmpty(state.ColorMa00Cay)) ColorMa00Cay = (Color)ColorConverter.ConvertFromString(state.ColorMa00Cay);
                        if (!string.IsNullOrEmpty(state.ColorMa00Yemek)) ColorMa00Yemek = (Color)ColorConverter.ConvertFromString(state.ColorMa00Yemek);
                        if (!string.IsNullOrEmpty(state.ColorMa00Other)) ColorMa00Other = (Color)ColorConverter.ConvertFromString(state.ColorMa00Other);
                        if (!string.IsNullOrEmpty(state.InnerLabelColor)) InnerLabelColor = (Color)ColorConverter.ConvertFromString(state.InnerLabelColor);

                        if (state.RowHeight >= 8 && state.RowHeight <= 32) RowHeight = state.RowHeight;
                        if (state.FootnoteFontSize >= 6 && state.FootnoteFontSize <= 16) FootnoteFontSize = state.FootnoteFontSize; else FootnoteFontSize = 8.0;
                        if (state.InnerLabelFontSize >= 4 && state.InnerLabelFontSize <= 14) InnerLabelFontSize = state.InnerLabelFontSize; else InnerLabelFontSize = 6.5;

                        ShowValuesInHours = state.ShowValuesInHours; ShowMajorErrorLabels = state.ShowMajorErrorLabels;
                        ShowMachineLegend = state.ShowMachineLegend; ShowMonthSummary = state.ShowMonthSummary;
                        DetailedOverlapView = state.DetailedOverlapView; HideGenelTemizlik = state.HideGenelTemizlik;
                        EnableSoftCorners = state.EnableSoftCorners; EnableBlockBorders = state.EnableBlockBorders; DisableSoftCornersUnder5Min = state.DisableSoftCornersUnder5Min;

                        LayerRunning = state.LayerRunning > 0 ? Math.Min(state.LayerRunning, 100) : 1;
                        LayerBypass = state.LayerBypass > 0 ? Math.Min(state.LayerBypass, 100) : 2;
                        LayerBreaks = state.LayerBreaks > 0 ? Math.Min(state.LayerBreaks, 100) : 10;
                        LayerErrors = state.LayerErrors > 0 ? Math.Min(state.LayerErrors, 100) : 20;

                        if (state.OverlapCascadeStep >= 0.05 && state.OverlapCascadeStep <= 0.50) OverlapCascadeStep = state.OverlapCascadeStep; else OverlapCascadeStep = 0.20;
                        if (state.MinLabelMinutes >= 0) MinLabelMinutes = state.MinLabelMinutes; else MinLabelMinutes = 90.0;
                        if (state.MinFootnoteMinutes >= 0) MinFootnoteMinutes = state.MinFootnoteMinutes; else MinFootnoteMinutes = 20.0;

                        if (!string.IsNullOrEmpty(state.ExtraCol1Selection))
                        {
                            if (AvailableExtraOptions.Contains(state.ExtraCol1Selection)) ExtraCol1Selection = state.ExtraCol1Selection;
                            else if (state.ExtraCol1Selection.Contains("Total") || state.ExtraCol1Selection.Contains("Toplam")) ExtraCol1Selection = LocTotalStop;
                            else if (state.ExtraCol1Selection.Contains("Work") || state.ExtraCol1Selection.Contains("Çalışma")) ExtraCol1Selection = LocActualWork;
                        }
                        if (!string.IsNullOrEmpty(state.ExtraCol2Selection))
                        {
                            if (AvailableExtraOptions.Contains(state.ExtraCol2Selection)) ExtraCol2Selection = state.ExtraCol2Selection;
                            else if (state.ExtraCol2Selection.Contains("Total") || state.ExtraCol2Selection.Contains("Toplam")) ExtraCol2Selection = LocTotalStop;
                            else if (state.ExtraCol2Selection.Contains("Work") || state.ExtraCol2Selection.Contains("Çalışma")) ExtraCol2Selection = LocActualWork;
                        }
                    }
                }
            }
            catch { }
            if (!AvailableExtraOptions.Contains(ExtraCol1Selection)) ExtraCol1Selection = LocTotalStop;
            if (!AvailableExtraOptions.Contains(ExtraCol2Selection)) ExtraCol2Selection = LocActualWork;
        }

        private void ResetExtraOptions()
        {
            AvailableExtraOptions.Clear(); AvailableExtraOptions.Add("None"); AvailableExtraOptions.Add(LocTotalStop); AvailableExtraOptions.Add(LocActualWork);
            if (ExtraCol1Selection == null || !AvailableExtraOptions.Contains(ExtraCol1Selection)) ExtraCol1Selection = LocTotalStop;
            if (ExtraCol2Selection == null || !AvailableExtraOptions.Contains(ExtraCol2Selection)) ExtraCol2Selection = LocActualWork;
        }

        private async void ExecuteBrowseExcel(object obj)
        {
            if (_dialogService.ShowOpenFileDialog("Select Excel File for Extra Columns", "Excel Files|*.xlsx", out string path))
            {
                ExcelFilePath = path; await LoadExcelHeadersAsync(path);
            }
        }

        private async Task LoadExcelHeadersAsync(string path)
        {
            IsBusy = true;
            try
            {
                var headers = await Task.Run(() =>
                {
                    var list = new List<string>(); ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var package = new ExcelPackage(stream))
                    {
                        var ws = package.Workbook.Worksheets.FirstOrDefault();
                        if (ws != null && ws.Dimension != null && ws.Dimension.End != null)
                        {
                            for (int c = 1; c <= ws.Dimension.End.Column; c++)
                            {
                                var cell = ws.Cells[1, c]; string header = cell?.Text?.Trim();
                                if (!string.IsNullOrEmpty(header)) list.Add(header);
                            }
                        }
                    }
                    return list;
                });

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ResetExtraOptions();
                    foreach (var h in headers)
                        if (!h.ToLower().Contains("date") && !h.ToLower().Contains("tarih") && !h.ToLower().Contains("shift") && !h.ToLower().Contains("vardiya"))
                            AvailableExtraOptions.Add($"[Excel] {h}");

                    if (!AvailableExtraOptions.Contains(ExtraCol1Selection)) ExtraCol1Selection = LocTotalStop;
                    if (!AvailableExtraOptions.Contains(ExtraCol2Selection)) ExtraCol2Selection = LocActualWork;
                });
            }
            catch (Exception ex) { MessageBox.Show($"Failed to read Excel headers: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); ExcelFilePath = ""; }
            finally { IsBusy = false; }
        }

        private async void ExecuteGeneratePreview(object obj)
        {
            IsBusy = true; IsEditingBlock = false;
            if (SelectedBlock != null) SelectedBlock.IsSelected = false; SelectedBlock = null; ZoomScale = 1.0;

            SaveState();

            try
            {
                if (_cachedData == null || _cachedTableName != SelectedTable)
                {
                    var result = await _dataRepository.GetTableDataAsync(SelectedTable, 0);
                    if (result.Data == null || result.Data.Rows.Count == 0)
                    {
                        MessageBox.Show("No data found in the selected database table.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        IsBusy = false; return;
                    }
                    _cachedData = result.Data; _cachedTableName = SelectedTable;
                }

                ReportRows.Clear();
                bool show1 = ExtraCol1Selection != "None"; bool show2 = ExtraCol2Selection != "None";

                double timelineWidth = 958.0;
                if (show1) timelineWidth -= 90.0;
                if (show2) timelineWidth -= 90.0;

                var context = new TimelineGenerationContext
                {
                    StartDate = this.StartDate,
                    EndDate = this.EndDate,
                    TimelineWidth = timelineWidth,
                    ExcelFilePath = this.ExcelFilePath,
                    HasExcelFile = this.HasExcelFile,
                    ShowValuesInHours = this.ShowValuesInHours,
                    HideGenelTemizlik = this.HideGenelTemizlik,
                    DetailedOverlapView = this.DetailedOverlapView,
                    ExtraCol1Selection = this.ExtraCol1Selection,
                    ExtraCol2Selection = this.ExtraCol2Selection,
                    ColorMa00Genel = this.ColorMa00Genel.ToString(),
                    ColorMa00Cay = this.ColorMa00Cay.ToString(),
                    ColorMa00Yemek = this.ColorMa00Yemek.ToString(),
                    ColorMa00Other = this.ColorMa00Other.ToString(),
                    BreakColor = this.BypassColor.ToString(),
                    ErrorColor = this.ErrorColor.ToString(),
                    ErrorColor2 = this.ErrorColor2.ToString(),
                    RunningColor = this.RunningColor.ToString(),
                    FacilityStopColor = this.FacilityStopColor.ToString(),
                    LayerRunning = this.LayerRunning,
                    LayerBypass = this.LayerBypass,
                    LayerBreaks = this.LayerBreaks,
                    LayerErrors = this.LayerErrors,
                    OverlapCascadeStep = this.OverlapCascadeStep,
                    MinLabelMinutes = this.MinLabelMinutes,
                    MinFootnoteMinutes = this.MinFootnoteMinutes,
                    EnableBlockBorders = this.EnableBlockBorders,
                    RowHeight = this.RowHeight,
                    EnableSoftCorners = this.EnableSoftCorners,
                    DisableSoftCornersUnder5Min = this.DisableSoftCornersUnder5Min,
                    InnerLabelFontSize = this.InnerLabelFontSize,
                    ShowMonthSummary = this.ShowMonthSummary // NEW
                };

                var newRows = await _reportGenerator.GenerateReportAsync(_cachedData, context);

                foreach (var row in newRows)
                {
                    foreach (var block in row.Blocks)
                    {
                        string checkFp = block.BaseFingerprint ?? block.Fingerprint;
                        if (!string.IsNullOrEmpty(checkFp) && _blockOverrides.TryGetValue(checkFp, out var overrideData))
                        {
                            block.ColorHex = overrideData.ColorHex;
                            block.Label = overrideData.Label; block.MachineCode = overrideData.MachineCode;
                            block.OriginalDescription = overrideData.Description; block.IsFootnote = overrideData.IsFootnote;
                            block.DisplayStartTime = overrideData.DisplayStartTime; block.DisplayEndTime = overrideData.DisplayEndTime;
                            block.HeightMultiplier = overrideData.HeightMultiplier; block.TopOffset = overrideData.TopOffset;
                            block.PanelZIndex = overrideData.PanelZIndex;
                            block.LabelOffsetX = overrideData.LabelOffsetX; block.LabelOffsetY = overrideData.LabelOffsetY;
                            block.BlockFontSize = overrideData.BlockFontSize;
                        }
                    }
                }

                Application.Current.Dispatcher.Invoke(() => { foreach (var r in newRows) ReportRows.Add(r); });

                string resDate = WPF_LoginForm.Properties.Resources.P_date ?? "Date"; string resShift = WPF_LoginForm.Properties.Resources.P_Shift ?? "Shift";
                string resRun = WPF_LoginForm.Properties.Resources.P_Running ?? "Tesis Çalışıyor"; string resErr = WPF_LoginForm.Properties.Resources.P_Errors_and_Stops ?? "Arıza ve Duruşlar";
                string resErr2 = WPF_LoginForm.Properties.Resources.P_Overlapping ?? "Paralel Arıza/Bakım";

                string uom = ShowValuesInHours ? "(sa)" : "(min)";
                string t1 = (ExtraCol1Selection ?? "").Replace("[Excel] ", ""); string t2 = (ExtraCol2Selection ?? "").Replace("[Excel] ", "");
                if (show1 && (t1 == LocTotalStop || t1 == "Total Stop" || t1 == LocActualWork || t1 == "Actual Work")) t1 += $" {uom}";
                if (show2 && (t2 == LocTotalStop || t2 == "Total Stop" || t2 == LocActualWork || t2 == "Actual Work")) t2 += $" {uom}";

                var legendList = new List<MachineLegendItem>
                {
                    new MachineLegendItem { Code = "99", Name = "HABERLEŞME" }, new MachineLegendItem { Code = "98", Name = "POLİP" },
                    new MachineLegendItem { Code = "97", Name = "KAMYON" }, new MachineLegendItem { Code = "96", Name = "BESLEME-KONVEYÖR" }
                };

                var ticks = new List<PrintAxisTick>();
                int shiftStartHour = 8;
                int totalMinutes = 720;
                int intervalMinutes = 120;
                for (int m = 0; m <= totalMinutes; m += intervalMinutes)
                {
                    int topHour = shiftStartHour + (m / 60);
                    int bottomHour = (topHour + 12) % 24;
                    ticks.Add(new PrintAxisTick
                    {
                        PositionPercent = (double)m / totalMinutes,
                        TopLabel = $"{topHour:D2}:00",
                        BottomLabel = $"{bottomHour:D2}:00",
                        TimelineWidth = timelineWidth,
                        RowHeight = this.RowHeight
                    });
                }

                var config = new PrintReportConfig
                {
                    ReportTitle = WPF_LoginForm.Properties.Resources.Str_ShiftReport,
                    DateRangeText = $"{StartDate:dd.MM.yyyy} - {EndDate:dd.MM.yyyy}",
                    RunningColor = this.RunningColor.ToString(),
                    ErrorColor = this.ErrorColor.ToString(),
                    ErrorColor2 = this.ErrorColor2.ToString(),
                    BreakColor = this.BypassColor.ToString(),
                    FacilityStopColor = this.FacilityStopColor.ToString(),
                    ColorMa00Genel = this.ColorMa00Genel.ToString(),
                    ColorMa00Cay = this.ColorMa00Cay.ToString(),
                    ColorMa00Yemek = this.ColorMa00Yemek.ToString(),
                    ColorMa00Other = this.ColorMa00Other.ToString(),
                    InnerLabelColor = this.InnerLabelColor.ToString(),
                    RowHeight = this.RowHeight,
                    FootnoteFontSize = this.FootnoteFontSize,
                    InnerLabelFontSize = this.InnerLabelFontSize,
                    TimelineWidth = timelineWidth,
                    ShowExtra1 = show1,
                    ShowExtra2 = show2,
                    ShowMajorErrorLabels = this.ShowMajorErrorLabels,
                    ShowMachineLegend = this.ShowMachineLegend,
                    ShowMonthSummary = this.ShowMonthSummary, // NEW
                    ExtraTitle1 = show1 ? t1 : "",
                    ExtraTitle2 = show2 ? t2 : "",
                    HeaderDate = resDate,
                    HeaderShift = resShift,
                    LegendRunning = resRun,
                    LegendBreak = WPF_LoginForm.Properties.Resources.P_Bypass,
                    LegendError = resErr,
                    LegendError2 = resErr2,
                    LegendFacilityStop = WPF_LoginForm.Properties.Resources.P_Total_Stop,
                    LegendMa00Genel = WPF_LoginForm.Properties.Resources.P_GeneralCleaning,
                    LegendMa00Cay = WPF_LoginForm.Properties.Resources.P_TeaBreak,
                    LegendMa00Yemek = WPF_LoginForm.Properties.Resources.P_MealBreak,
                    LegendMa00Other = WPF_LoginForm.Properties.Resources.P_Other,
                    LegendItems = legendList,
                    AxisTicks = ticks,
                    MonthlySummary = context.MonthlySummaryData // NEW
                };

                ReportConfig = config; RebuildFootnotesAndLabels(); OnPropertyChanged(nameof(ReportConfig));
            }
            catch (Exception ex) { MessageBox.Show($"Error generating report: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { IsBusy = false; (PrintCommand as ViewModelCommand)?.RaiseCanExecuteChanged(); }
        }
    }
}