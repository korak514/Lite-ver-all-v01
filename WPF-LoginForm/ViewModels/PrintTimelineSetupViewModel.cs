// ViewModels/PrintTimelineSetupViewModel.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
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
    public class PrintTimelineSetupViewModel : ViewModelBase
    {
        private readonly IDataRepository _dataRepository;
        private readonly IDialogService _dialogService;
        private readonly CategoryMappingService _mappingService;
        private readonly List<CategoryRule> _activeRules;
        private readonly string _settingsFilePath;

        private string LocTotalStop => WPF_LoginForm.Properties.Resources.P_Total_Stop ?? "Total Stop / Toplam Duruş";
        private string LocActualWork => WPF_LoginForm.Properties.Resources.P_Working_Time ?? "Actual Work / Çalışma Süresi";

        public event Action PrintRequested;

        public ObservableCollection<string> AvailableTables { get; } = new ObservableCollection<string>();

        private string _selectedTable;
        public string SelectedTable { get => _selectedTable; set => SetProperty(ref _selectedTable, value); }

        private string _excelFilePath;
        public string ExcelFilePath
        { get => _excelFilePath; set { if (SetProperty(ref _excelFilePath, value)) OnPropertyChanged(nameof(HasExcelFile)); } }
        public bool HasExcelFile => !string.IsNullOrEmpty(ExcelFilePath);

        private DateTime _startDate = DateTime.Today.AddDays(-16);
        public DateTime StartDate { get => _startDate; set => SetProperty(ref _startDate, value); }
        private DateTime _endDate = DateTime.Today;
        public DateTime EndDate { get => _endDate; set => SetProperty(ref _endDate, value); }

        private double _zoomScale = 1.0;
        public double ZoomScale { get => _zoomScale; set => SetProperty(ref _zoomScale, value); }

        private Color _runningColor = (Color)ColorConverter.ConvertFromString("#2ECC71");
        public Color RunningColor { get => _runningColor; set => SetProperty(ref _runningColor, value); }
        private Color _errorColor = (Color)ColorConverter.ConvertFromString("#E74C3C");
        public Color ErrorColor { get => _errorColor; set => SetProperty(ref _errorColor, value); }
        private Color _errorColor2 = (Color)ColorConverter.ConvertFromString("#C0392B");
        public Color ErrorColor2 { get => _errorColor2; set => SetProperty(ref _errorColor2, value); }
        private Color _breakColor = (Color)ColorConverter.ConvertFromString("#F1C40F");
        public Color BreakColor { get => _breakColor; set => SetProperty(ref _breakColor, value); }
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

        private double _footnoteFontSize = 10.0;
        public double FootnoteFontSize { get => _footnoteFontSize; set => SetProperty(ref _footnoteFontSize, value); }

        private bool _showValuesInHours = false;
        public bool ShowValuesInHours { get => _showValuesInHours; set => SetProperty(ref _showValuesInHours, value); }
        private bool _showMajorErrorLabels = true;
        public bool ShowMajorErrorLabels { get => _showMajorErrorLabels; set => SetProperty(ref _showMajorErrorLabels, value); }
        private bool _showMachineLegend = true;
        public bool ShowMachineLegend { get => _showMachineLegend; set => SetProperty(ref _showMachineLegend, value); }
        private bool _detailedOverlapView = true;
        public bool DetailedOverlapView { get => _detailedOverlapView; set => SetProperty(ref _detailedOverlapView, value); }
        private bool _hideGenelTemizlik = true;
        public bool HideGenelTemizlik { get => _hideGenelTemizlik; set => SetProperty(ref _hideGenelTemizlik, value); }

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
        public Color EditBlockColor { get => _editBlockColor; set => SetProperty(ref _editBlockColor, value); }

        private string _editBlockLabel;
        public string EditBlockLabel { get => _editBlockLabel; set => SetProperty(ref _editBlockLabel, value); }

        private string _editBlockMachine;
        public string EditBlockMachine
        { get => _editBlockMachine; set { if (SetProperty(ref _editBlockMachine, value)) RefreshBlockCategories(false); } }

        private string _editBlockDesc;
        public string EditBlockDesc { get => _editBlockDesc; set => SetProperty(ref _editBlockDesc, value); }

        private bool _editBlockIsFootnote;
        public bool EditBlockIsFootnote { get => _editBlockIsFootnote; set => SetProperty(ref _editBlockIsFootnote, value); }

        private string _editBlockTimeData;
        public string EditBlockTimeData { get => _editBlockTimeData; set => SetProperty(ref _editBlockTimeData, value); }

        public ObservableCollection<string> AvailableBlockCategories { get; } = new ObservableCollection<string>();
        private string _selectedBlockCategory;
        public string SelectedBlockCategory
        { get => _selectedBlockCategory; set { if (SetProperty(ref _selectedBlockCategory, value)) UpdateColorFromCategory(value); } }

        public ICommand SelectBlockCommand { get; }
        public ICommand ApplyBlockEditCommand { get; }
        public ICommand CancelBlockEditCommand { get; }

        public PrintReportConfig ReportConfig { get; private set; }
        public ObservableCollection<PrintShiftRow> ReportRows { get; } = new ObservableCollection<PrintShiftRow>();

        public ICommand BrowseExcelCommand { get; }
        public ICommand ClearExcelCommand { get; }
        public ICommand GeneratePreviewCommand { get; }
        public ICommand PrintCommand { get; }

        public PrintTimelineSetupViewModel(IDataRepository dataRepository, IDialogService dialogService)
        {
            _dataRepository = dataRepository;
            _dialogService = dialogService;
            _mappingService = new CategoryMappingService();
            _activeRules = _mappingService.LoadRules();
            _settingsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WPF_LoginForm", "print_report_settings.json");

            ResetExtraOptions();
            _extraCol1Selection = LocTotalStop;
            _extraCol2Selection = LocActualWork;

            BrowseExcelCommand = new ViewModelCommand(ExecuteBrowseExcel);
            ClearExcelCommand = new ViewModelCommand(p => { ExcelFilePath = ""; ResetExtraOptions(); });
            GeneratePreviewCommand = new ViewModelCommand(ExecuteGeneratePreview, p => !string.IsNullOrEmpty(SelectedTable) && !IsBusy);
            PrintCommand = new ViewModelCommand(p => PrintRequested?.Invoke(), p => ReportRows.Count > 0 && !IsBusy);

            SelectBlockCommand = new ViewModelCommand(ExecuteSelectBlock);
            ApplyBlockEditCommand = new ViewModelCommand(ExecuteApplyBlockEdit);
            CancelBlockEditCommand = new ViewModelCommand(ExecuteCancelBlockEdit);

            _ = InitializeAsync();
        }

        private void ExecuteSelectBlock(object obj)
        {
            if (obj is PrintTimeBlock block)
            {
                if (SelectedBlock != null) SelectedBlock.IsSelected = false;

                SelectedBlock = block;
                SelectedBlock.IsSelected = true;

                _editBlockMachine = block.MachineCode;
                OnPropertyChanged(nameof(EditBlockMachine));

                EditBlockLabel = block.Label;
                EditBlockDesc = block.OriginalDescription;
                EditBlockIsFootnote = block.IsFootnote;

                EditBlockTimeData = $"{block.DisplayStartTime} - {block.DisplayEndTime} ({block.DurationMinutes:F0} mins)";

                RefreshBlockCategories(true);

                string currentCategory = "";
                string mCode = (EditBlockMachine ?? "").Replace("MA-", "").Trim();
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
                    else if (block.ColorHex == BreakColor.ToString()) currentCategory = "Normal Break";
                    else if (block.ColorHex == FacilityStopColor.ToString()) currentCategory = "Facility Stop";
                    else currentCategory = "Primary Error";
                }

                _selectedBlockCategory = currentCategory;
                OnPropertyChanged(nameof(SelectedBlockCategory));
                EditBlockColor = (Color)ColorConverter.ConvertFromString(block.ColorHex);

                IsEditingBlock = true;
            }
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
                    AvailableBlockCategories.Add("Genel Temizlik");
                    AvailableBlockCategories.Add("Çay Molası");
                    AvailableBlockCategories.Add("Yemek Molası");
                    AvailableBlockCategories.Add("Diğer MA-00");
                    if (!initialLoad) SelectedBlockCategory = "Diğer MA-00";
                }
                else
                {
                    AvailableBlockCategories.Add("Running");
                    AvailableBlockCategories.Add("Primary Error");
                    AvailableBlockCategories.Add("Overlap Error");
                    AvailableBlockCategories.Add("Normal Break");
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
            else if (category == "Normal Break") EditBlockColor = BreakColor;
            else if (category == "Facility Stop") EditBlockColor = FacilityStopColor;
        }

        private void ExecuteApplyBlockEdit(object obj)
        {
            if (SelectedBlock == null) return;
            SelectedBlock.ColorHex = EditBlockColor.ToString();
            SelectedBlock.Label = EditBlockLabel;
            SelectedBlock.MachineCode = EditBlockMachine;
            SelectedBlock.OriginalDescription = EditBlockDesc;
            SelectedBlock.IsFootnote = EditBlockIsFootnote;

            SelectedBlock.IsSelected = false;
            SelectedBlock = null;
            IsEditingBlock = false;

            RebuildFootnotesAndLabels();
        }

        private void ExecuteCancelBlockEdit(object obj)
        {
            if (SelectedBlock != null) SelectedBlock.IsSelected = false;
            SelectedBlock = null;
            IsEditingBlock = false;
        }

        private void RebuildFootnotesAndLabels()
        {
            if (ReportConfig == null) return;
            var newFootnotes = new List<string>();
            int counter = 1;

            foreach (var row in ReportRows)
            {
                foreach (var block in row.Blocks)
                {
                    if (block.IsFootnote)
                    {
                        string marker = $"[{counter}]";
                        block.Label = marker;
                        string desc = string.IsNullOrEmpty(block.OriginalDescription) ? "Arıza/Bakım" : block.OriginalDescription;
                        string mCode = string.IsNullOrEmpty(block.MachineCode) ? "" : $"{block.MachineCode} - ";
                        newFootnotes.Add($"{marker} {mCode}{desc} ({block.DurationMinutes:F0}dk)");
                        counter++;
                    }
                }
            }
            ReportConfig.Footnotes = newFootnotes;
            OnPropertyChanged(nameof(ReportConfig));
        }

        private async Task InitializeAsync()
        {
            var tables = await _dataRepository.GetTableNamesAsync();
            Application.Current.Dispatcher.Invoke(() =>
            {
                AvailableTables.Clear();
                foreach (var t in tables) AvailableTables.Add(t);
                LoadState();
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
                    BreakColor = this.BreakColor.ToString(),
                    FacilityStopColor = this.FacilityStopColor.ToString(),
                    ColorMa00Genel = this.ColorMa00Genel.ToString(),
                    ColorMa00Cay = this.ColorMa00Cay.ToString(),
                    ColorMa00Yemek = this.ColorMa00Yemek.ToString(),
                    ColorMa00Other = this.ColorMa00Other.ToString(),
                    InnerLabelColor = this.InnerLabelColor.ToString(),
                    RowHeight = this.RowHeight,
                    FootnoteFontSize = this.FootnoteFontSize,
                    ShowValuesInHours = this.ShowValuesInHours,
                    ShowMajorErrorLabels = this.ShowMajorErrorLabels,
                    ShowMachineLegend = this.ShowMachineLegend,
                    DetailedOverlapView = this.DetailedOverlapView,
                    HideGenelTemizlik = this.HideGenelTemizlik,
                    ExtraCol1Selection = this.ExtraCol1Selection,
                    ExtraCol2Selection = this.ExtraCol2Selection
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
                        if (!string.IsNullOrEmpty(state.BreakColor)) BreakColor = (Color)ColorConverter.ConvertFromString(state.BreakColor);
                        if (!string.IsNullOrEmpty(state.FacilityStopColor)) FacilityStopColor = (Color)ColorConverter.ConvertFromString(state.FacilityStopColor);
                        if (!string.IsNullOrEmpty(state.ColorMa00Genel)) ColorMa00Genel = (Color)ColorConverter.ConvertFromString(state.ColorMa00Genel);
                        if (!string.IsNullOrEmpty(state.ColorMa00Cay)) ColorMa00Cay = (Color)ColorConverter.ConvertFromString(state.ColorMa00Cay);
                        if (!string.IsNullOrEmpty(state.ColorMa00Yemek)) ColorMa00Yemek = (Color)ColorConverter.ConvertFromString(state.ColorMa00Yemek);
                        if (!string.IsNullOrEmpty(state.ColorMa00Other)) ColorMa00Other = (Color)ColorConverter.ConvertFromString(state.ColorMa00Other);
                        if (!string.IsNullOrEmpty(state.InnerLabelColor)) InnerLabelColor = (Color)ColorConverter.ConvertFromString(state.InnerLabelColor);
                        if (state.RowHeight >= 8 && state.RowHeight <= 32) RowHeight = state.RowHeight;
                        if (state.FootnoteFontSize >= 6 && state.FootnoteFontSize <= 16) FootnoteFontSize = state.FootnoteFontSize; else FootnoteFontSize = 10.0;
                        ShowValuesInHours = state.ShowValuesInHours;
                        ShowMajorErrorLabels = state.ShowMajorErrorLabels;
                        ShowMachineLegend = state.ShowMachineLegend;
                        DetailedOverlapView = state.DetailedOverlapView;
                        HideGenelTemizlik = state.HideGenelTemizlik;

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
            if (FootnoteFontSize < 6) FootnoteFontSize = 10.0;
        }

        private void ResetExtraOptions()
        {
            AvailableExtraOptions.Clear();
            AvailableExtraOptions.Add("None");
            AvailableExtraOptions.Add(LocTotalStop);
            AvailableExtraOptions.Add(LocActualWork);
            if (ExtraCol1Selection == null || !AvailableExtraOptions.Contains(ExtraCol1Selection)) ExtraCol1Selection = LocTotalStop;
            if (ExtraCol2Selection == null || !AvailableExtraOptions.Contains(ExtraCol2Selection)) ExtraCol2Selection = LocActualWork;
        }

        private async void ExecuteBrowseExcel(object obj)
        {
            if (_dialogService.ShowOpenFileDialog("Select Excel File for Extra Columns", "Excel Files|*.xlsx", out string path))
            {
                ExcelFilePath = path;
                await LoadExcelHeadersAsync(path);
            }
        }

        private async Task LoadExcelHeadersAsync(string path)
        {
            IsBusy = true;
            try
            {
                var headers = await Task.Run(() =>
                {
                    var list = new List<string>();
                    ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var package = new ExcelPackage(stream))
                    {
                        var ws = package.Workbook.Worksheets.FirstOrDefault();
                        if (ws != null && ws.Dimension != null)
                        {
                            for (int c = 1; c <= ws.Dimension.End.Column; c++)
                            {
                                string header = ws.Cells[1, c].Text.Trim();
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
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to read Excel headers: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ExcelFilePath = "";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void ExecuteGeneratePreview(object obj)
        {
            IsBusy = true;
            IsEditingBlock = false;
            if (SelectedBlock != null) SelectedBlock.IsSelected = false;
            SelectedBlock = null;
            ZoomScale = 1.0;

            ReportRows.Clear();
            SaveState();

            try
            {
                var result = await _dataRepository.GetTableDataAsync(SelectedTable, 0);
                if (result.Data == null || result.Data.Rows.Count == 0)
                {
                    MessageBox.Show("No data found in the selected database table.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                bool show1 = ExtraCol1Selection != "None";
                bool show2 = ExtraCol2Selection != "None";

                double timelineWidth = 778;
                if (!show1) timelineWidth += 90;
                if (!show2) timelineWidth += 90;

                await ProcessDataForReportAsync(result.Data, timelineWidth);

                string resDate = WPF_LoginForm.Properties.Resources.P_date ?? "Date";
                string resShift = WPF_LoginForm.Properties.Resources.P_Shift ?? "Shift";
                string resRun = WPF_LoginForm.Properties.Resources.P_Running ?? "Running";
                string resBreak = WPF_LoginForm.Properties.Resources.P_Meal_and_breaks ?? "Meal & Breaks";
                string resErr = WPF_LoginForm.Properties.Resources.P_Errors_and_Stops ?? "Errors & Stops";
                string resErr2 = WPF_LoginForm.Properties.Resources.P_Overlapping ?? "Paralel Arıza/Bakım";

                string uom = ShowValuesInHours ? "(sa)" : "(min)";
                string t1 = (ExtraCol1Selection ?? "").Replace("[Excel] ", "");
                string t2 = (ExtraCol2Selection ?? "").Replace("[Excel] ", "");

                if (show1 && (t1 == LocTotalStop || t1 == "Total Stop" || t1 == LocActualWork || t1 == "Actual Work")) t1 += $" {uom}";
                if (show2 && (t2 == LocTotalStop || t2 == "Total Stop" || t2 == LocActualWork || t2 == "Actual Work")) t2 += $" {uom}";

                var legendList = new List<MachineLegendItem>
                {
                    new MachineLegendItem { Code = "99", Name = "HABERLEŞME" },
                    new MachineLegendItem { Code = "98", Name = "POLİP" },
                    new MachineLegendItem { Code = "97", Name = "KAMYON" },
                    new MachineLegendItem { Code = "96", Name = "BESLEME KOVEYÖR" }
                };

                var ticks = new List<PrintAxisTick>
                {
                    new PrintAxisTick { PositionPercent = 0.0, TopLabel = "08:00", BottomLabel = "20:00", TimelineWidth = timelineWidth, RowHeight = this.RowHeight },
                    new PrintAxisTick { PositionPercent = 120.0 / 720.0, TopLabel = "10:00", BottomLabel = "22:00", TimelineWidth = timelineWidth, RowHeight = this.RowHeight },
                    new PrintAxisTick { PositionPercent = 270.0 / 720.0, TopLabel = "12:30", BottomLabel = "00:30", TimelineWidth = timelineWidth, RowHeight = this.RowHeight },
                    new PrintAxisTick { PositionPercent = 420.0 / 720.0, TopLabel = "15:00", BottomLabel = "03:30", TimelineWidth = timelineWidth, RowHeight = this.RowHeight },
                    new PrintAxisTick { PositionPercent = 540.0 / 720.0, TopLabel = "17:00", BottomLabel = "05:30", TimelineWidth = timelineWidth, RowHeight = this.RowHeight },
                    new PrintAxisTick { PositionPercent = 720.0 / 720.0, TopLabel = "20:00", BottomLabel = "08:00", TimelineWidth = timelineWidth, RowHeight = this.RowHeight }
                };

                ReportConfig = new PrintReportConfig
                {
                    ReportTitle = $"Shift Timeline Report ({SelectedTable})",
                    DateRangeText = $"{StartDate:dd.MM.yyyy} - {EndDate:dd.MM.yyyy}",
                    RunningColor = this.RunningColor.ToString(),
                    ErrorColor = this.ErrorColor.ToString(),
                    ErrorColor2 = this.ErrorColor2.ToString(),
                    BreakColor = this.BreakColor.ToString(),
                    FacilityStopColor = this.FacilityStopColor.ToString(),
                    ColorMa00Genel = this.ColorMa00Genel.ToString(),
                    ColorMa00Cay = this.ColorMa00Cay.ToString(),
                    ColorMa00Yemek = this.ColorMa00Yemek.ToString(),
                    ColorMa00Other = this.ColorMa00Other.ToString(),
                    InnerLabelColor = this.InnerLabelColor.ToString(),
                    RowHeight = this.RowHeight,
                    FootnoteFontSize = this.FootnoteFontSize,
                    TimelineWidth = timelineWidth,
                    ShowExtra1 = show1,
                    ShowExtra2 = show2,
                    ShowMajorErrorLabels = this.ShowMajorErrorLabels,
                    ShowMachineLegend = this.ShowMachineLegend,
                    ExtraTitle1 = show1 ? t1 : "",
                    ExtraTitle2 = show2 ? t2 : "",
                    HeaderDate = resDate,
                    HeaderShift = resShift,
                    LegendRunning = resRun,
                    LegendBreak = resBreak,
                    LegendError = resErr,
                    LegendError2 = resErr2,
                    LegendFacilityStop = "Duruş (Facility Stop)",
                    LegendMa00Genel = "00 Genel Temizlik",
                    LegendMa00Cay = "00 Çay Molası",
                    LegendMa00Yemek = "00 Yemek Molası",
                    LegendMa00Other = "00 Diğer",
                    LegendItems = legendList,
                    AxisTicks = ticks,
                    Footnotes = new List<string>()
                };

                OnPropertyChanged(nameof(ReportConfig));
                RebuildFootnotesAndLabels();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating report: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                (PrintCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
            }
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

        private async Task ProcessDataForReportAsync(DataTable dbData, double timelineWidth)
        {
            await Task.Run(() =>
            {
                var dateCol = dbData.Columns.Cast<DataColumn>().FirstOrDefault(c => c.ColumnName.IndexOf("date", StringComparison.OrdinalIgnoreCase) >= 0 || c.ColumnName.IndexOf("tarih", StringComparison.OrdinalIgnoreCase) >= 0);
                var shiftCol = dbData.Columns.Cast<DataColumn>().FirstOrDefault(c => c.ColumnName.IndexOf("shift", StringComparison.OrdinalIgnoreCase) >= 0 || c.ColumnName.IndexOf("vardiya", StringComparison.OrdinalIgnoreCase) >= 0);
                var endCol = dbData.Columns.Cast<DataColumn>().FirstOrDefault(c => c.ColumnName.IndexOf("bitiş", StringComparison.OrdinalIgnoreCase) >= 0 || c.ColumnName.IndexOf("bitis", StringComparison.OrdinalIgnoreCase) >= 0 || c.ColumnName.IndexOf("end", StringComparison.OrdinalIgnoreCase) >= 0);
                var errorCols = dbData.Columns.Cast<DataColumn>().Where(c => c.ColumnName.StartsWith("hata_kodu", StringComparison.OrdinalIgnoreCase) || c.ColumnName.StartsWith("error_code", StringComparison.OrdinalIgnoreCase)).ToList();

                if (dateCol == null || shiftCol == null) return;

                var excelLookup = new Dictionary<(DateTime date, string shift), Dictionary<string, string>>();
                if (HasExcelFile && File.Exists(ExcelFilePath))
                {
                    try
                    {
                        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                        using (var stream = new FileStream(ExcelFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var package = new ExcelPackage(stream))
                        {
                            var ws = package.Workbook.Worksheets.FirstOrDefault();
                            if (ws != null && ws.Dimension != null)
                            {
                                int colDate = -1, colShift = -1;
                                var headers = new Dictionary<int, string>();
                                for (int c = 1; c <= ws.Dimension.End.Column; c++)
                                {
                                    string header = ws.Cells[1, c].Text.Trim();
                                    headers[c] = header;
                                    if (header.IndexOf("date", StringComparison.OrdinalIgnoreCase) >= 0 || header.IndexOf("tarih", StringComparison.OrdinalIgnoreCase) >= 0) colDate = c;
                                    if (header.IndexOf("shift", StringComparison.OrdinalIgnoreCase) >= 0 || header.IndexOf("vardiya", StringComparison.OrdinalIgnoreCase) >= 0) colShift = c;
                                }
                                if (colDate != -1 && colShift != -1)
                                {
                                    for (int r = 2; r <= ws.Dimension.End.Row; r++)
                                    {
                                        if (DateTime.TryParse(ws.Cells[r, colDate].Text, out DateTime exDate))
                                        {
                                            var key = (exDate.Date, FormatShiftName(ws.Cells[r, colShift].Text));
                                            var rowData = new Dictionary<string, string>();
                                            for (int c = 1; c <= ws.Dimension.End.Column; c++)
                                                if (c != colDate && c != colShift) rowData[headers[c]] = ws.Cells[r, c].Text;
                                            excelLookup[key] = rowData;
                                        }
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
                    .Where(x => x.Date >= StartDate.Date && x.Date <= EndDate.Date)
                    .GroupBy(x => new { x.Date, Shift = FormatShiftName(x.Shift) })
                    .OrderBy(g => g.Key.Date).ThenBy(g => g.Key.Shift)
                    .ToList();

                var processedRows = new List<PrintShiftRow>();
                DateTime? lastDateProcessed = null;
                bool altBackgroundToggle = false;
                double widthMultiplier = timelineWidth / 720.0;

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
                            if (TimeSpan.TryParse(eTime, out TimeSpan tsEnd) || DateTime.TryParse(eTime, out DateTime dEnd) && (tsEnd = dEnd.TimeOfDay) != default)
                            {
                                DateTime eventEndFull = group.Key.Date.Add(tsEnd);
                                if (isNight && tsEnd.Hours < 12) eventEndFull = eventEndFull.AddDays(1);
                                else if (!isNight && tsEnd.Hours < 8) eventEndFull = eventEndFull.AddDays(1);
                                double diff = (eventEndFull - shiftStart).TotalMinutes;
                                if (diff > 0 && diff < 720) activeShiftMinutes = diff;
                            }
                        }
                    }

                    shiftRow.Blocks.Add(new PrintTimeBlock
                    {
                        StartMinute = 0,
                        DurationMinutes = activeShiftMinutes,
                        BlockType = PrintBlockType.Running,
                        ColorHex = RunningColor.ToString(),
                        WidthMultiplier = widthMultiplier,
                        DisplayStartTime = shiftStart.ToString("HH:mm"),
                        DisplayEndTime = shiftStart.AddMinutes(activeShiftMinutes).ToString("HH:mm")
                    });

                    if (activeShiftMinutes < 720)
                    {
                        shiftRow.Blocks.Add(new PrintTimeBlock
                        {
                            StartMinute = activeShiftMinutes,
                            DurationMinutes = 720 - activeShiftMinutes,
                            BlockType = PrintBlockType.FacilityStop,
                            ColorHex = FacilityStopColor.ToString(),
                            WidthMultiplier = widthMultiplier,
                            DisplayStartTime = shiftStart.AddMinutes(activeShiftMinutes).ToString("HH:mm"),
                            DisplayEndTime = shiftStart.AddMinutes(720).ToString("HH:mm")
                        });
                    }

                    double totalStopMins = 0;
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
                                    totalStopMins += duration;

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

                                    if (isMa00Group1) { block.ColorHex = ColorMa00Genel.ToString(); breaks.Add(block); }
                                    else if (isMa00Group2) { block.ColorHex = ColorMa00Cay.ToString(); breaks.Add(block); }
                                    else if (isMa00Group3) { block.ColorHex = ColorMa00Yemek.ToString(); breaks.Add(block); }
                                    else if (isMa00Group4) { block.ColorHex = ColorMa00Other.ToString(); breaks.Add(block); }
                                    else if (isNormalBreak) { block.ColorHex = BreakColor.ToString(); breaks.Add(block); }
                                    else rawErrors.Add(block);
                                }
                            }
                        }
                    }

                    rawErrors = rawErrors.OrderByDescending(e => e.DurationMinutes).ToList();
                    var placedPrimaries = new List<PrintTimeBlock>();

                    foreach (var err in rawErrors)
                    {
                        var overlappedPrimary = placedPrimaries.FirstOrDefault(p => err.StartMinute < (p.StartMinute + p.DurationMinutes) && (err.StartMinute + err.DurationMinutes) > p.StartMinute);

                        if (overlappedPrimary != null)
                        {
                            err.ColorHex = ErrorColor2.ToString();
                            err.HeightMultiplier = 0.35;
                            err.TopOffset = 0;
                            if (err.DurationMinutes >= 19 && !string.IsNullOrEmpty(err.MachineCode)) overlappedPrimary.MachineCodes.Add(err.MachineCode);
                        }
                        else
                        {
                            err.ColorHex = ErrorColor.ToString();
                            err.HeightMultiplier = 1.0;
                            err.TopOffset = 0;

                            if (err.DurationMinutes >= 19 && err.DurationMinutes < 20)
                            {
                                if (!string.IsNullOrEmpty(err.MachineCode)) err.MachineCodes.Add(err.MachineCode);
                            }
                            else if (err.DurationMinutes >= 20 && err.DurationMinutes < 90)
                            {
                                if (!string.IsNullOrEmpty(err.MachineCode)) err.MachineCodes.Add(err.MachineCode);
                                err.IsFootnote = true;
                            }
                            else if (err.DurationMinutes >= 90)
                            {
                                if (!string.IsNullOrEmpty(err.MachineCode)) err.MachineCodes.Add(err.MachineCode);
                                if (!string.IsNullOrEmpty(err.OriginalDescription))
                                {
                                    var words = err.OriginalDescription.Split(new char[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
                                    if (words.Length >= 2) err.TextDescription = $"{words[0]} {words[1]}";
                                    else if (words.Length == 1) err.TextDescription = words[0];
                                }
                            }
                            placedPrimaries.Add(err);
                        }
                        shiftRow.Blocks.Add(err);
                    }

                    foreach (var b in breaks)
                    {
                        var overlappedPrimaryError = placedPrimaries.FirstOrDefault(p => b.StartMinute < (p.StartMinute + p.DurationMinutes) && (b.StartMinute + b.DurationMinutes) > p.StartMinute);
                        if (overlappedPrimaryError != null) { b.HeightMultiplier = 0.35; b.TopOffset = 0; }
                        else { b.HeightMultiplier = 1.0; b.TopOffset = 0; }
                        shiftRow.Blocks.Add(b);
                    }

                    foreach (var block in shiftRow.Blocks)
                    {
                        if (block.IsFootnote) continue;

                        if (block.MachineCodes.Any() || !string.IsNullOrEmpty(block.TextDescription))
                        {
                            var distinctCodes = block.MachineCodes.Distinct().ToList();
                            if (HideGenelTemizlik) distinctCodes.RemoveAll(c => c == "00" || c == "0");

                            string codesStr = "";
                            if (distinctCodes.Any())
                            {
                                if (DetailedOverlapView && distinctCodes.Count > 1) codesStr = string.Join(", ", distinctCodes.Take(distinctCodes.Count - 1)) + " & " + distinctCodes.Last();
                                else codesStr = string.Join(" & ", distinctCodes);
                            }

                            if (string.IsNullOrWhiteSpace(block.TextDescription)) block.Label = codesStr;
                            else if (string.IsNullOrWhiteSpace(codesStr)) block.Label = block.TextDescription;
                            else block.Label = $"{codesStr} {block.TextDescription}";
                        }
                    }

                    shiftRow.ExtraValue1 = CalculateExtraValue(ExtraCol1Selection, totalStopMins, group.Key.Date, group.Key.Shift, excelLookup);
                    shiftRow.ExtraValue2 = CalculateExtraValue(ExtraCol2Selection, totalStopMins, group.Key.Date, group.Key.Shift, excelLookup);
                    processedRows.Add(shiftRow);
                }

                Application.Current.Dispatcher.Invoke(() => { foreach (var r in processedRows) ReportRows.Add(r); });
            });
        }

        private string CalculateExtraValue(string selection, double totalStop, DateTime date, string shift, Dictionary<(DateTime, string), Dictionary<string, string>> excelLookup)
        {
            if (selection == "None" || string.IsNullOrEmpty(selection)) return "";
            if (selection == LocTotalStop || selection == "Total Stop" || selection == LocActualWork || selection == "Actual Work")
            {
                double val = (selection == LocTotalStop || selection == "Total Stop") ? totalStop : Math.Max(0, 720 - totalStop);
                if (ShowValuesInHours) return TimeFormatHelper.FormatDuration(val, true);
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
        { return string.IsNullOrEmpty(raw) ? "Day" : (raw.IndexOf("gece", StringComparison.OrdinalIgnoreCase) >= 0 || raw.IndexOf("night", StringComparison.OrdinalIgnoreCase) >= 0 ? "Night" : "Day"); }
    }
}