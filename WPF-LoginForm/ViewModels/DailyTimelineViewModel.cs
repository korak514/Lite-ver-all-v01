// ViewModels/DailyTimelineViewModel.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Newtonsoft.Json;
using WPF_LoginForm.Models;
using WPF_LoginForm.Properties;
using WPF_LoginForm.Repositories;
using WPF_LoginForm.Services;

namespace WPF_LoginForm.ViewModels
{
    public class TimelineConfigData
    {
        public List<string> MachineCodes { get; set; } = new List<string>();
        public List<FavoriteEvent> Favorites { get; set; } = new List<FavoriteEvent>();
    }

    public class FavoriteEvent : ViewModelBase
    {
        public string Title { get; set; }
        public string MachineCode { get; set; }
        public string Description { get; set; }
        public int DefaultDuration { get; set; }
        public string ColorHex { get; set; }
    }

    public class HourMarker : ViewModelBase
    {
        public string Text { get; set; }
        private double _pixelX;
        public double PixelX { get => _pixelX; set => SetProperty(ref _pixelX, value); }
    }

    public class TimelineBlockModel : ViewModelBase
    {
        public ErrorEventModel OriginalEvent { get; set; }
        public int LaneIndex { get; set; }
        public double StartMinuteInShift { get; set; }
        public double DurationMinutes { get; set; }
        public double CurrentZoomHours { get; set; } = 4.0;

        public DataRow SourceRow { get; set; }
        public string SourceColumn { get; set; }
        public bool IsNewUnsaved { get; set; }

        public bool IsReferenceBlock { get; set; }

        private bool _isBypass;

        public bool IsBypass
        {
            get => _isBypass;
            set { if (SetProperty(ref _isBypass, value)) { OnPropertyChanged(nameof(BlockColor)); } }
        }

        private double _pixelX;
        public double PixelX { get => _pixelX; set => SetProperty(ref _pixelX, value); }

        private double _pixelWidth;
        public double PixelWidth { get => _pixelWidth; set => SetProperty(ref _pixelWidth, value); }

        private double _pixelY;
        public double PixelY { get => _pixelY; set => SetProperty(ref _pixelY, value); }

        public string DisplayText => IsReferenceBlock ? $"Ref: {DurationMinutes}m" : (OriginalEvent != null ? $"MA-{OriginalEvent.MachineCode}: {OriginalEvent.ErrorDescription}" : "");
        public string BlockColor => DetermineColor();

        public bool IsTextVisible => !IsReferenceBlock && (DurationMinutes >= 10 || CurrentZoomHours <= 2.0);
        public double TextFontSize => (DurationMinutes < 10 && CurrentZoomHours <= 2.0) ? 8.0 : 11.0;

        private string DetermineColor()
        {
            if (IsReferenceBlock) return "#80F39C12"; // Amber/Orange Semi-transparent

            if (OriginalEvent == null) return "#808080";
            if (IsBypass) return "#8E44AD";

            string desc = OriginalEvent.ErrorDescription?.ToLower() ?? "";
            if (desc.Contains("mola") || desc.Contains("break") || desc.Contains("yemek")) return "#2ECC71";
            if (desc.Contains("bakım") || desc.Contains("maint")) return "#F39C12";
            return "#E74C3C";
        }

        public void UpdateZoom(double zoomHours)
        {
            // PERFORMANCE FIX: Prevent layout thrashing
            if (Math.Abs(CurrentZoomHours - zoomHours) < 0.01) return;

            CurrentZoomHours = zoomHours;
            OnPropertyChanged(nameof(IsTextVisible));
            OnPropertyChanged(nameof(TextFontSize));
        }

        public void RefreshUI()
        {
            OnPropertyChanged(nameof(DisplayText));
            OnPropertyChanged(nameof(BlockColor));
            OnPropertyChanged(nameof(IsTextVisible));
            OnPropertyChanged(nameof(TextFontSize));
        }
    }

    public class DailyTimelineViewModel : ViewModelBase
    {
        private readonly IDataRepository _repository;
        private readonly IDialogService _dialogService;
        private readonly string _tableName;
        private double _lastViewportWidth = 800;
        private readonly string _configFilePath;
        private CancellationTokenSource _loadCts;

        private DataTable _currentData;
        private List<string> _errorColumns = new List<string>();

        private DateTime _targetDate;

        public DateTime TargetDate
        {
            get => _targetDate;
            set { if (SetProperty(ref _targetDate, value)) { UpdateHeaderStrings(); ReloadData(); } }
        }

        private void ReloadData()
        {
            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            _ = LoadDataAsync(_loadCts.Token);
            _ = LoadRulerDataAsync(_loadCts.Token);
        }

        private bool _isNightShift;

        public bool IsNightShift
        {
            get => _isNightShift;
            set { if (SetProperty(ref _isNightShift, value)) { UpdateHeaderStrings(); ParseDataToTimeline(); _ = LoadRulerDataAsync(); } }
        }

        private bool _isDirty;
        public bool IsDirty { get => _isDirty; set => SetProperty(ref _isDirty, value); }
        public bool IsOnlineMode => !(_repository is OfflineDataRepository);

        private double _timeWindowHours = 4.0;

        public double TimeWindowHours
        {
            get => _timeWindowHours;
            set { if (SetProperty(ref _timeWindowHours, value)) RefreshDimensions(); }
        }

        private double _calculatedCanvasWidth = 800;
        public double CalculatedCanvasWidth { get => _calculatedCanvasWidth; set => SetProperty(ref _calculatedCanvasWidth, value); }

        private string _formattedDateNumber;
        public string FormattedDateNumber { get => _formattedDateNumber; set => SetProperty(ref _formattedDateNumber, value); }

        private string _formattedDateMonthYear;
        public string FormattedDateMonthYear { get => _formattedDateMonthYear; set => SetProperty(ref _formattedDateMonthYear, value); }

        private string _formattedDay;
        public string FormattedDay { get => _formattedDay; set => SetProperty(ref _formattedDay, value); }

        private string _shiftTitle;
        public string ShiftTitle { get => _shiftTitle; set => SetProperty(ref _shiftTitle, value); }

        // --- KPI Calculation Properties ---
        private double _autoCalculatedMolaKazanimi;

        public double AutoCalculatedMolaKazanimi { get => _autoCalculatedMolaKazanimi; set => SetProperty(ref _autoCalculatedMolaKazanimi, value); }

        private double _manualBypassKazanimi;
        public double ManualBypassKazanimi { get => _manualBypassKazanimi; set => SetProperty(ref _manualBypassKazanimi, value); }

        private double _rawMolaKazanimi;
        public double RawMolaKazanimi { get => _rawMolaKazanimi; set => SetProperty(ref _rawMolaKazanimi, value); }

        private double _rawBypassKazanimi;
        public double RawBypassKazanimi { get => _rawBypassKazanimi; set => SetProperty(ref _rawBypassKazanimi, value); }

        private int _corruptedDataCount;
        public int CorruptedDataCount { get => _corruptedDataCount; set => SetProperty(ref _corruptedDataCount, value); }

        private int _wrongTimeCount;
        public int WrongTimeCount { get => _wrongTimeCount; set => SetProperty(ref _wrongTimeCount, value); }

        private double _autoFiiliSure;

        public double AutoFiiliSure
        {
            get => _autoFiiliSure;
            set { if (SetProperty(ref _autoFiiliSure, value)) OnPropertyChanged(nameof(AutoFiiliSureFormatted)); }
        }

        private double _rawFiiliSure;

        public double RawFiiliSure
        {
            get => _rawFiiliSure;
            set { if (SetProperty(ref _rawFiiliSure, value)) OnPropertyChanged(nameof(RawFiiliSureFormatted)); }
        }

        public string AutoFiiliSureFormatted => $"{(int)(AutoFiiliSure / 60):D2}:{(int)(AutoFiiliSure % 60):D2}";
        public string RawFiiliSureFormatted => $"{(int)(RawFiiliSure / 60):D2}:{(int)(RawFiiliSure % 60):D2}";

        // UI Panels
        private bool _isFavoritesVisible = false;

        public bool IsFavoritesVisible { get => _isFavoritesVisible; set => SetProperty(ref _isFavoritesVisible, value); }

        public ObservableCollection<FavoriteEvent> FavoriteEvents { get; } = new ObservableCollection<FavoriteEvent>();
        public ObservableCollection<string> AvailableMachineCodes { get; } = new ObservableCollection<string>();

        private bool _isEditPanelVisible;
        public bool IsEditPanelVisible { get => _isEditPanelVisible; set => SetProperty(ref _isEditPanelVisible, value); }

        private TimelineBlockModel _selectedBlock;

        public TimelineBlockModel SelectedBlock
        {
            get => _selectedBlock;
            set
            {
                if (SetProperty(ref _selectedBlock, value))
                {
                    IsEditPanelVisible = _selectedBlock != null && !_selectedBlock.IsReferenceBlock;
                    if (_selectedBlock != null && !_selectedBlock.IsReferenceBlock) SyncEditFields();
                }
            }
        }

        private bool _useEndTimeMode;
        public bool UseEndTimeMode { get => _useEndTimeMode; set => SetProperty(ref _useEndTimeMode, value); }

        private string _editStartTime;

        public string EditStartTime
        {
            get => _editStartTime;
            set { if (SetProperty(ref _editStartTime, value)) CalculateTime(); }
        }

        private string _editEndTime;

        public string EditEndTime
        {
            get => _editEndTime;
            set { if (SetProperty(ref _editEndTime, value)) { if (UseEndTimeMode) CalculateTime(); } }
        }

        private int _editDuration;

        public int EditDuration
        {
            get => _editDuration;
            set { if (SetProperty(ref _editDuration, value)) { if (!UseEndTimeMode) CalculateTime(); } }
        }

        // --- NEW: RULER SELECTION ---
        public ObservableCollection<string> RulerTables { get; } = new ObservableCollection<string>();

        private string _selectedRulerTable;

        public string SelectedRulerTable
        {
            get => _selectedRulerTable;
            set { if (SetProperty(ref _selectedRulerTable, value)) ReloadData(); }
        }

        // --- TIMELINE COLLECTIONS ---
        public ObservableCollection<TimelineBlockModel> TimelineBlocks { get; } = new ObservableCollection<TimelineBlockModel>();

        public ObservableCollection<TimelineBlockModel> ReferenceBlocks { get; } = new ObservableCollection<TimelineBlockModel>();
        public ObservableCollection<HourMarker> HourMarkers { get; } = new ObservableCollection<HourMarker>();

        // Commands
        public ICommand ToggleEditPanelCommand { get; }

        public ICommand AddNewEventCommand { get; }
        public ICommand ToggleFavoritesCommand { get; }
        public ICommand BlockClickCommand { get; }
        public ICommand NextDayCommand { get; }
        public ICommand PrevDayCommand { get; }
        public ICommand ManageMachineCodesCommand { get; }
        public ICommand QuickAddFavoriteCommand { get; }
        public ICommand SaveAsFavoriteCommand { get; }
        public ICommand RemoveFavoriteCommand { get; }
        public ICommand ApplyToTimelineCommand { get; }
        public ICommand DeleteEventCommand { get; }
        public ICommand SaveToDatabaseCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand OpenMonthlyErrorsCommand { get; }

        public DailyTimelineViewModel(IDataRepository repository, string tableName, DateTime targetDate)
        {
            _repository = repository;
            _tableName = tableName;
            _targetDate = targetDate.Date;
            _isNightShift = false;
            _dialogService = new DialogService();
            _configFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WPF_LoginForm", "timeline_config.json");

            ToggleEditPanelCommand = new ViewModelCommand(p => IsEditPanelVisible = !IsEditPanelVisible);
            AddNewEventCommand = new ViewModelCommand(p => ExecuteAddNewEvent());
            ToggleFavoritesCommand = new ViewModelCommand(p => IsFavoritesVisible = !IsFavoritesVisible);
            BlockClickCommand = new ViewModelCommand(ExecuteBlockClick);
            NextDayCommand = new ViewModelCommand(p => TargetDate = TargetDate.AddDays(1));
            PrevDayCommand = new ViewModelCommand(p => TargetDate = TargetDate.AddDays(-1));

            ManageMachineCodesCommand = new ViewModelCommand(p => ExecuteManageMachineCodes());
            QuickAddFavoriteCommand = new ViewModelCommand(ExecuteQuickAddFavorite);
            SaveAsFavoriteCommand = new ViewModelCommand(p => ExecuteSaveAsFavorite());
            RemoveFavoriteCommand = new ViewModelCommand(ExecuteRemoveFavorite);

            ApplyToTimelineCommand = new ViewModelCommand(p => ExecuteApplyToTimeline());
            DeleteEventCommand = new ViewModelCommand(p => ExecuteDeleteEvent());

            SaveToDatabaseCommand = new ViewModelCommand(p => ExecuteSaveToDatabase(), p => IsDirty && IsOnlineMode);
            UndoCommand = new ViewModelCommand(p => ExecuteUndo(), p => IsDirty);
            RefreshCommand = new ViewModelCommand(p => { ReloadData(); });
            OpenMonthlyErrorsCommand = new ViewModelCommand(p => ExecuteOpenMonthlyErrors());

            _ = InitializeTablesAsync();
            LoadConfigData();
            UpdateHeaderStrings();
            ReloadData();
        }

        private async Task InitializeTablesAsync()
        {
            var tables = await _repository.GetTableNamesAsync();
            Application.Current.Dispatcher.Invoke(() =>
            {
                RulerTables.Clear();
                RulerTables.Add("None");
                foreach (var t in tables) RulerTables.Add(t);
                SelectedRulerTable = "None";
            });
        }

        private void LoadConfigData()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    string json = File.ReadAllText(_configFilePath);
                    var config = JsonConvert.DeserializeObject<TimelineConfigData>(json);
                    if (config != null)
                    {
                        foreach (var mc in config.MachineCodes) AvailableMachineCodes.Add(mc);
                        foreach (var f in config.Favorites) FavoriteEvents.Add(f);

                        if (!AvailableMachineCodes.Contains("99")) AvailableMachineCodes.Add("99");
                        return;
                    }
                }
            }
            catch { }

            if (!AvailableMachineCodes.Any()) { AvailableMachineCodes.Add("00"); AvailableMachineCodes.Add("01"); }
            if (!AvailableMachineCodes.Contains("99")) AvailableMachineCodes.Add("99");

            if (!FavoriteEvents.Any()) { FavoriteEvents.Add(new FavoriteEvent { Title = Resources.Str_LunchBreak, MachineCode = "00", Description = Resources.Str_MA00_3, DefaultDuration = 60, ColorHex = "#2ECC71" }); }
        }

        private void SaveConfigData()
        {
            try
            {
                string dir = Path.GetDirectoryName(_configFilePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var config = new TimelineConfigData { MachineCodes = AvailableMachineCodes.ToList(), Favorites = FavoriteEvents.ToList() };
                File.WriteAllText(_configFilePath, JsonConvert.SerializeObject(config, Formatting.Indented));
            }
            catch { }
        }

        private void ExecuteManageMachineCodes()
        {
            if (_dialogService.ShowInputDialog(Resources.Str_MachineCode, Resources.Str_MachineCode + ":", "", out string newCode))
            {
                string cleanCode = newCode.Trim().ToUpper();
                if (!string.IsNullOrWhiteSpace(cleanCode) && !AvailableMachineCodes.Contains(cleanCode))
                {
                    AvailableMachineCodes.Add(cleanCode);
                    SaveConfigData();
                    if (SelectedBlock?.OriginalEvent != null)
                    {
                        SelectedBlock.OriginalEvent.MachineCode = cleanCode;
                        OnPropertyChanged(nameof(SelectedBlock));
                    }
                }
            }
        }

        private void ExecuteOpenMonthlyErrors()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var vm = new MonthlyErrorsViewModel(this);
                var win = new Views.MonthlyErrorsWindow(vm);
                if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
                    win.Owner = Application.Current.MainWindow;
                win.ShowDialog();
            });
        }

        private void ExecuteAddNewEvent()
        {
            HandleDropOrClickAdd(null, 40, 30);
        }

        public void HandleDropOrClickAdd(FavoriteEvent fav, double dropPixelX, double dropPixelY)
        {
            if (CalculatedCanvasWidth <= 0) return;

            double padding = 40.0;
            double usableWidth = CalculatedCanvasWidth - (padding * 2);
            if (usableWidth <= 0) usableWidth = 100;

            double relativeX = dropPixelX - padding;
            if (relativeX < 0) relativeX = 0;
            if (relativeX > usableWidth) relativeX = usableWidth;

            double startMin = (relativeX / usableWidth) * 720.0;

            int lane = (int)((dropPixelY - 30) / 80.0);
            if (lane < 0) lane = 0; if (lane > 5) lane = 5;

            DateTime shiftStart = IsNightShift ? TargetDate.Date.AddHours(20) : TargetDate.Date.AddHours(8);
            DateTime eventStart = shiftStart.AddMinutes(startMin);
            int dur = fav?.DefaultDuration ?? 15;
            DateTime eventEnd = eventStart.AddMinutes(dur);
            string mCode = fav?.MachineCode ?? "01";
            string cleanMCode = mCode.Replace("MA-", "");
            bool isBypassDef = cleanMCode == "33" || cleanMCode == "34" || cleanMCode == "35" || cleanMCode == "36" || cleanMCode == "37";

            SelectedBlock = new TimelineBlockModel
            {
                OriginalEvent = new ErrorEventModel
                {
                    Date = TargetDate,
                    StartTime = eventStart.ToString("HH:mm"),
                    EndTime = eventEnd.ToString("HH:mm"),
                    DurationMinutes = dur,
                    MachineCode = mCode,
                    ErrorDescription = fav?.Description ?? "New Event"
                },
                LaneIndex = lane,
                StartMinuteInShift = startMin,
                DurationMinutes = dur,
                CurrentZoomHours = this.TimeWindowHours,
                IsNewUnsaved = true,
                IsReferenceBlock = false,
                IsBypass = isBypassDef
            };

            SyncEditFields();
            IsEditPanelVisible = true;

            ExecuteApplyToTimeline();
        }

        public void ExecuteQuickAddFavorite(object obj)
        {
            if (obj is FavoriteEvent fav)
            {
                HandleDropOrClickAdd(fav, 40, 30);
            }
        }

        private void ExecuteSaveAsFavorite()
        {
            if (SelectedBlock?.OriginalEvent == null) return;
            string title = SelectedBlock.OriginalEvent.ErrorDescription;
            if (string.IsNullOrWhiteSpace(title)) title = "New Favorite";
            if (title.Length > 15) title = title.Substring(0, 15) + "...";

            FavoriteEvents.Add(new FavoriteEvent
            {
                Title = title,
                Description = SelectedBlock.OriginalEvent.ErrorDescription,
                MachineCode = SelectedBlock.OriginalEvent.MachineCode,
                DefaultDuration = SelectedBlock.DurationMinutes > 0 ? (int)SelectedBlock.DurationMinutes : 15,
                ColorHex = SelectedBlock.BlockColor
            });
            SaveConfigData();
        }

        private void ExecuteRemoveFavorite(object obj)
        {
            if (obj is FavoriteEvent fav)
            {
                if (MessageBox.Show($"{Resources.Tip_RemoveFavorite} '{fav.Title}'?", Resources.Str_Cancel, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    FavoriteEvents.Remove(fav);
                    SaveConfigData();
                }
            }
        }

        private string GenerateDbString(TimelineBlockModel block)
        {
            var ev = block.OriginalEvent;
            string rawTime = ev.StartTime.Replace(":", "");
            string machine = ev.MachineCode.Replace("MA-", "");
            return $"{rawTime}-{ev.DurationMinutes}-MA-{machine}-{ev.ErrorDescription}";
        }

        private void ExecuteApplyToTimeline()
        {
            if (SelectedBlock == null || _currentData == null || SelectedBlock.IsReferenceBlock) return;
            CalculateTime();

            string cleanMCode = SelectedBlock.OriginalEvent.MachineCode?.Replace("MA-", "") ?? "";
            if (cleanMCode == "33" || cleanMCode == "34" || cleanMCode == "35" || cleanMCode == "36" || cleanMCode == "37")
            {
                SelectedBlock.IsBypass = true;
            }

            SelectedBlock.RefreshUI();

            if (SelectedBlock.SourceRow == null || string.IsNullOrEmpty(SelectedBlock.SourceColumn))
            {
                var dateCol = _currentData.Columns.Cast<DataColumn>().FirstOrDefault(c => c.ColumnName.IndexOf("date", StringComparison.OrdinalIgnoreCase) >= 0 || c.ColumnName.IndexOf("tarih", StringComparison.OrdinalIgnoreCase) >= 0);
                DataRow targetRow = null;

                if (dateCol != null)
                {
                    targetRow = _currentData.AsEnumerable().FirstOrDefault(r =>
                        r.RowState != DataRowState.Deleted &&
                        r[dateCol] != DBNull.Value &&
                        Convert.ToDateTime(r[dateCol]).Date == TargetDate.Date);
                }

                if (targetRow == null)
                {
                    targetRow = _currentData.NewRow();
                    if (dateCol != null) targetRow[dateCol] = TargetDate.Date;
                    _currentData.Rows.Add(targetRow);
                }

                SelectedBlock.SourceRow = targetRow;

                foreach (string col in _errorColumns)
                {
                    if (targetRow[col] == DBNull.Value || string.IsNullOrWhiteSpace(targetRow[col].ToString()))
                    {
                        SelectedBlock.SourceColumn = col;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(SelectedBlock.SourceColumn) && _errorColumns.Any())
                {
                    SelectedBlock.SourceColumn = _errorColumns.Last();
                }
            }

            if (SelectedBlock.SourceRow != null && !string.IsNullOrEmpty(SelectedBlock.SourceColumn))
            {
                SelectedBlock.SourceRow[SelectedBlock.SourceColumn] = GenerateDbString(SelectedBlock);
                SetDirty();
            }

            if (!TimelineBlocks.Contains(SelectedBlock)) TimelineBlocks.Add(SelectedBlock);

            AssignLanes(TimelineBlocks.ToList());
            RefreshDimensions();
            RecalculateSavings();
        }

        private void ExecuteDeleteEvent()
        {
            if (SelectedBlock != null && TimelineBlocks.Contains(SelectedBlock) && !SelectedBlock.IsReferenceBlock)
            {
                if (SelectedBlock.SourceRow != null && !string.IsNullOrEmpty(SelectedBlock.SourceColumn))
                {
                    SelectedBlock.SourceRow[SelectedBlock.SourceColumn] = DBNull.Value;
                    SetDirty();
                }
                TimelineBlocks.Remove(SelectedBlock);
                SelectedBlock = null;
                RecalculateSavings();
            }
        }

        private async void ExecuteSaveToDatabase()
        {
            var changes = _currentData?.GetChanges();
            if (changes == null) return;

            var result = await _repository.SaveChangesAsync(changes, _tableName);
            if (result.Success)
            {
                _currentData.AcceptChanges();
                IsDirty = false;
                MessageBox.Show(Resources.Title_Saved, Resources.Title_Saved, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"{Resources.Msg_ExportFailed} {result.ErrorMessage}", Resources.Str_Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteUndo()
        {
            _currentData?.RejectChanges();
            IsDirty = false;
            ParseDataToTimeline();
        }

        private void RecalculateSavings()
        {
            if (TimelineBlocks == null) return;

            double shiftTotalMinutes = 720;
            var firstValidBlock = TimelineBlocks.FirstOrDefault(b => b.SourceRow != null && b.SourceRow.Table != null);
            DataRow dbRow = firstValidBlock?.SourceRow;

            if (dbRow != null)
            {
                var dt = dbRow.Table;
                DataColumn startCol = dt.Columns.Cast<DataColumn>().FirstOrDefault(c =>
                    c.ColumnName.IndexOf("başlangıç", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    c.ColumnName.IndexOf("baslangic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    c.ColumnName.IndexOf("start", StringComparison.OrdinalIgnoreCase) >= 0);

                DataColumn endCol = dt.Columns.Cast<DataColumn>().FirstOrDefault(c =>
                    c.ColumnName.IndexOf("bitiş", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    c.ColumnName.IndexOf("bitis", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    c.ColumnName.IndexOf("end", StringComparison.OrdinalIgnoreCase) >= 0);

                if (startCol != null && endCol != null && dbRow[startCol] != DBNull.Value && dbRow[endCol] != DBNull.Value)
                {
                    string sTime = dbRow[startCol].ToString();
                    string eTime = dbRow[endCol].ToString();

                    TimeSpan tsStart, tsEnd;

                    if (!TimeSpan.TryParse(sTime, out tsStart) && DateTime.TryParse(sTime, out DateTime dStart)) tsStart = dStart.TimeOfDay;
                    if (!TimeSpan.TryParse(eTime, out tsEnd) && DateTime.TryParse(eTime, out DateTime dEnd)) tsEnd = dEnd.TimeOfDay;

                    if (tsEnd <= tsStart) tsEnd = tsEnd.Add(TimeSpan.FromHours(24));
                    shiftTotalMinutes = (tsEnd - tsStart).TotalMinutes;
                }
            }

            int arraySize = (int)Math.Max(1440, shiftTotalMinutes + 120);
            bool[] isStoppedMinute = new bool[arraySize];
            double manualBypass = 0;
            double sumOfDurations = 0;

            foreach (var block in TimelineBlocks)
            {
                if (block.IsReferenceBlock) continue;

                if (block.IsBypass)
                {
                    manualBypass += block.DurationMinutes;
                    continue;
                }

                sumOfDurations += block.DurationMinutes;

                int start = (int)Math.Max(0, block.StartMinuteInShift);
                int end = (int)Math.Min(arraySize, block.StartMinuteInShift + block.DurationMinutes);

                for (int i = start; i < end; i++)
                {
                    isStoppedMinute[i] = true;
                }
            }

            int totalStoppedWithinShift = 0;
            for (int i = 0; i < (int)shiftTotalMinutes; i++)
            {
                if (isStoppedMinute[i]) totalStoppedWithinShift++;
            }

            AutoCalculatedMolaKazanimi = Math.Max(0, sumOfDurations - totalStoppedWithinShift);
            ManualBypassKazanimi = manualBypass;
            AutoFiiliSure = Math.Max(0, shiftTotalMinutes - totalStoppedWithinShift);

            if (dbRow != null)
            {
                var dt = dbRow.Table;
                DataColumn colSavedBreak = null, colSavedMaint = null, colFiili = null;

                foreach (DataColumn c in dt.Columns)
                {
                    string n = c.ColumnName.ToLowerInvariant().Trim();
                    bool hasKazanim = n.Contains("kazanım") || n.Contains("kazanim");
                    
                    if (n.Contains("engelemeyen") || (n.Contains("zaman") && hasKazanim && !n.Contains("mola"))) colSavedBreak = c;
                    else if ((n.Contains("mola") || n.Contains("bakım") || n.Contains("bakim") || n.Contains("mola/bakım") || n.Contains("mola / bakım")) && hasKazanim) colSavedMaint = c;
                    else if (n.Contains("fiili") || n.Contains("çalışılan") || n.Contains("calisilan") || n.Contains("work")) colFiili = c;
                }

                RawMolaKazanimi = ParseDoubleSafe(colSavedMaint != null && dbRow[colSavedMaint] != DBNull.Value ? dbRow[colSavedMaint].ToString() : "");
                RawBypassKazanimi = ParseDoubleSafe(colSavedBreak != null && dbRow[colSavedBreak] != DBNull.Value ? dbRow[colSavedBreak].ToString() : "");

                if (colFiili != null && dbRow[colFiili] != DBNull.Value)
                {
                    string fStr = dbRow[colFiili].ToString().Trim();
                    if (TimeSpan.TryParse(fStr, out TimeSpan fTs)) RawFiiliSure = fTs.TotalMinutes;
                    else if (DateTime.TryParse(fStr, out DateTime fDt)) RawFiiliSure = fDt.TimeOfDay.TotalMinutes;
                    else RawFiiliSure = ParseDoubleSafe(fStr);

                    if (RawFiiliSure < 0)
                    {
                        MessageBox.Show(string.Format(Resources.Msg_NegativeFiiliSure, RawFiiliSure), Resources.Str_Error, MessageBoxButton.OK, MessageBoxImage.Warning);
                        RawFiiliSure = 0;
                    }
                }
                else RawFiiliSure = 0;
            }
            else
            {
                RawMolaKazanimi = 0;
                RawBypassKazanimi = 0;
                RawFiiliSure = 0;
            }
        }

        private double ParseDoubleSafe(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return 0;
            string strVal = val.Trim();

            int lastComma = strVal.LastIndexOf(',');
            int lastDot = strVal.LastIndexOf('.');

            if (lastComma > lastDot)
                strVal = strVal.Replace(".", "").Replace(",", ".");
            else if (lastDot > lastComma && lastComma != -1)
                strVal = strVal.Replace(",", "");
            else if (lastComma != -1 && lastDot == -1)
                strVal = strVal.Replace(",", ".");

            double.TryParse(strVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double res);
            return res;
        }

        private void SetDirty()
        {
            IsDirty = true;
            (SaveToDatabaseCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
            (UndoCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
        }

        private void SyncEditFields()
        {
            if (SelectedBlock?.OriginalEvent == null || SelectedBlock.IsReferenceBlock) return;

            _editStartTime = SelectedBlock.OriginalEvent.StartTime;
            _editEndTime = SelectedBlock.OriginalEvent.EndTime;
            _editDuration = SelectedBlock.OriginalEvent.DurationMinutes;

            OnPropertyChanged(nameof(EditStartTime)); OnPropertyChanged(nameof(EditEndTime)); OnPropertyChanged(nameof(EditDuration));
        }

        private void CalculateTime()
        {
            if (SelectedBlock?.OriginalEvent == null || SelectedBlock.IsReferenceBlock) return;

            if (TimeSpan.TryParse(EditStartTime, out TimeSpan startTs))
            {
                if (UseEndTimeMode)
                {
                    if (TimeSpan.TryParse(EditEndTime, out TimeSpan endTs))
                    {
                        if (endTs < startTs) endTs = endTs.Add(TimeSpan.FromHours(24));
                        _editDuration = (int)(endTs - startTs).TotalMinutes;
                        OnPropertyChanged(nameof(EditDuration));
                    }
                }
                else
                {
                    TimeSpan endTs = startTs.Add(TimeSpan.FromMinutes(EditDuration));
                    _editEndTime = endTs.ToString(@"hh\:mm");
                    OnPropertyChanged(nameof(EditEndTime));
                }

                SelectedBlock.OriginalEvent.StartTime = EditStartTime;
                SelectedBlock.OriginalEvent.EndTime = EditEndTime;
                SelectedBlock.OriginalEvent.DurationMinutes = EditDuration;
                SelectedBlock.DurationMinutes = EditDuration;

                DateTime shiftStart = IsNightShift ? TargetDate.Date.AddHours(20) : TargetDate.Date.AddHours(8);
                DateTime eventStartFull = TargetDate.Date.Add(startTs);
                if (IsNightShift && startTs.Hours < 12) eventStartFull = eventStartFull.AddDays(1);

                SelectedBlock.StartMinuteInShift = (eventStartFull - shiftStart).TotalMinutes;
                RefreshDimensions();
            }
        }

        private void UpdateHeaderStrings()
        {
            FormattedDateNumber = TargetDate.ToString("dd");
            FormattedDateMonthYear = TargetDate.ToString("MMM yyyy");
            string enDay = TargetDate.ToString("dddd", new CultureInfo("en-US"));
            string trDay = TargetDate.ToString("dddd", new CultureInfo("tr-TR"));
            FormattedDay = $"{enDay} / {trDay}";

            HourMarkers.Clear();
            for (int i = 0; i <= 12; i++)
            {
                int hour = IsNightShift ? (20 + i) % 24 : (8 + i);
                HourMarkers.Add(new HourMarker { Text = $"{hour:D2}:00" });
            }

            ShiftTitle = IsNightShift ? Resources.Str_NightShift : Resources.Str_DayShift;
            RefreshDimensions();
        }

        private async Task LoadDataAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(_tableName)) return;
            if (ct.IsCancellationRequested) return;

            var result = await _repository.GetTableDataAsync(_tableName, 0);
            if (ct.IsCancellationRequested || result.Data == null) return;

            _currentData = result.Data;

            _errorColumns.Clear();
            foreach (DataColumn c in _currentData.Columns)
            {
                string n = c.ColumnName.ToLower();
                if (n.StartsWith("hata_kodu") || n.StartsWith("error_code") || n.StartsWith("code"))
                    _errorColumns.Add(c.ColumnName);
            }

            IsDirty = false;
            if (!ct.IsCancellationRequested) ParseDataToTimeline();
        }

        private async Task LoadRulerDataAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(SelectedRulerTable) || SelectedRulerTable == "None")
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ReferenceBlocks.Clear();
                    RefreshDimensions();
                });
                return;
            }

            if (ct.IsCancellationRequested) return;

            var result = await _repository.GetTableDataAsync(SelectedRulerTable, 0);
            if (ct.IsCancellationRequested || result.Data == null) return;

            var dt = result.Data;
            var masterReferenceBlocks = new List<TimelineBlockModel>();

            var dateCol = dt.Columns.Cast<DataColumn>().FirstOrDefault(c => c.ColumnName.IndexOf("date", StringComparison.OrdinalIgnoreCase) >= 0 || c.ColumnName.IndexOf("tarih", StringComparison.OrdinalIgnoreCase) >= 0);
            var shiftCol = dt.Columns.Cast<DataColumn>().FirstOrDefault(c => c.ColumnName.IndexOf("shift", StringComparison.OrdinalIgnoreCase) >= 0 || c.ColumnName.IndexOf("vardiya", StringComparison.OrdinalIgnoreCase) >= 0);

            var durusColumns = dt.Columns.Cast<DataColumn>()
                .Where(c => c.ColumnName.StartsWith("duruş", StringComparison.OrdinalIgnoreCase) ||
                            c.ColumnName.StartsWith("durus", StringComparison.OrdinalIgnoreCase))
                .ToList();

            DateTime shiftStart = IsNightShift ? TargetDate.Date.AddHours(20) : TargetDate.Date.AddHours(8);

            foreach (DataRow row in dt.Rows)
            {
                if (row.RowState == DataRowState.Deleted) continue;

                DateTime rDate = dateCol != null && row[dateCol] != DBNull.Value ? Convert.ToDateTime(row[dateCol]) : DateTime.MinValue;
                string rShift = shiftCol != null && row[shiftCol] != DBNull.Value ? row[shiftCol].ToString() : "";

                bool isTargetShiftRow = false;
                if (rDate.Date == TargetDate.Date)
                {
                    bool isRowNightShift = rShift.IndexOf("gece", StringComparison.OrdinalIgnoreCase) >= 0 || rShift.IndexOf("night", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool isRowDayShift = rShift.IndexOf("gündüz", StringComparison.OrdinalIgnoreCase) >= 0 || rShift.IndexOf("gunduz", StringComparison.OrdinalIgnoreCase) >= 0 || rShift.IndexOf("day", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (IsNightShift && isRowNightShift) isTargetShiftRow = true;
                    else if (!IsNightShift && isRowDayShift) isTargetShiftRow = true;
                }

                if (isTargetShiftRow)
                {
                    foreach (var col in durusColumns)
                    {
                        string rawVal = row[col]?.ToString();
                        if (string.IsNullOrWhiteSpace(rawVal)) continue;

                        if (rawVal.StartsWith("F-", StringComparison.OrdinalIgnoreCase))
                            rawVal = rawVal.Substring(2);

                        var parts = rawVal.Split('-');
                        if (parts.Length >= 2)
                        {
                            string timePart = parts[0].Trim();
                            string durPart = parts[1].Trim();

                            if (TimeSpan.TryParse(timePart, out TimeSpan ts) && int.TryParse(durPart, out int duration))
                            {
                                DateTime eventStartFull = TargetDate.Date.Add(ts);
                                if (IsNightShift && ts.Hours < 12) eventStartFull = eventStartFull.AddDays(1);

                                masterReferenceBlocks.Add(new TimelineBlockModel
                                {
                                    IsReferenceBlock = true,
                                    StartMinuteInShift = (eventStartFull - shiftStart).TotalMinutes,
                                    DurationMinutes = duration,
                                    CurrentZoomHours = this.TimeWindowHours
                                });
                            }
                        }
                    }
                }
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                ReferenceBlocks.Clear();
                foreach (var rb in masterReferenceBlocks) ReferenceBlocks.Add(rb);
                RefreshDimensions();
            });
        }

        private void ParseDataToTimeline()
        {
            if (_currentData == null) return;
            var shiftBlocks = new List<TimelineBlockModel>();

            var dateCol = _currentData.Columns.Cast<DataColumn>().FirstOrDefault(c => c.ColumnName.IndexOf("date", StringComparison.OrdinalIgnoreCase) >= 0 || c.ColumnName.IndexOf("tarih", StringComparison.OrdinalIgnoreCase) >= 0);
            var shiftCol = _currentData.Columns.Cast<DataColumn>().FirstOrDefault(c => c.ColumnName.IndexOf("shift", StringComparison.OrdinalIgnoreCase) >= 0 || c.ColumnName.IndexOf("vardiya", StringComparison.OrdinalIgnoreCase) >= 0);

            int wrongTimeCounter = 0;
            int corruptedDataCounter = 0;

            DateTime shiftStart = IsNightShift ? TargetDate.Date.AddHours(20) : TargetDate.Date.AddHours(8);
            DateTime shiftEnd = shiftStart.AddHours(12);

            foreach (DataRow row in _currentData.Rows)
            {
                if (row.RowState == DataRowState.Deleted) continue;

                DateTime rDate = dateCol != null && row[dateCol] != DBNull.Value ? Convert.ToDateTime(row[dateCol]) : DateTime.MinValue;
                string rShift = shiftCol != null && row[shiftCol] != DBNull.Value ? row[shiftCol].ToString() : "";

                bool isTargetShiftRow = false;
                if (rDate.Date == TargetDate.Date)
                {
                    bool isRowNightShift = rShift.IndexOf("gece", StringComparison.OrdinalIgnoreCase) >= 0 || rShift.IndexOf("night", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool isRowDayShift = rShift.IndexOf("gündüz", StringComparison.OrdinalIgnoreCase) >= 0 || rShift.IndexOf("gunduz", StringComparison.OrdinalIgnoreCase) >= 0 || rShift.IndexOf("day", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (IsNightShift && isRowNightShift) isTargetShiftRow = true;
                    else if (!IsNightShift && isRowDayShift) isTargetShiftRow = true;
                }

                if (rDate.Date >= TargetDate.AddDays(-1) && rDate.Date <= TargetDate.AddDays(1))
                {
                    foreach (var eCol in _errorColumns)
                    {
                        string cellData = row[eCol]?.ToString();
                        if (string.IsNullOrWhiteSpace(cellData)) continue;

                        var parts = cellData.Split('-');
                        if (parts.Length < 4 || parts[0].Length != 4)
                        {
                            if (isTargetShiftRow) corruptedDataCounter++;
                            continue;
                        }

                        if (!int.TryParse(parts[0].Substring(0, 2), out int hh) || !int.TryParse(parts[0].Substring(2, 2), out int mm) || hh > 23 || mm > 59)
                        {
                            if (isTargetShiftRow) corruptedDataCounter++;
                            continue;
                        }

                        var item = ErrorEventModel.Parse(cellData, rDate, rShift, 0, 0, 0, 0, Guid.NewGuid().ToString());
                        if (item == null || string.IsNullOrEmpty(item.StartTime))
                        {
                            if (isTargetShiftRow) corruptedDataCounter++;
                            continue;
                        }

                        if (item.ErrorDescription == "NO_ERROR") continue;

                        if (!TimeSpan.TryParse(item.StartTime, out TimeSpan ts))
                        {
                            if (isTargetShiftRow) corruptedDataCounter++;
                            continue;
                        }

                        DateTime eventStartFull = item.Date.Date.Add(ts);
                        if (IsNightShift && ts.Hours < 12) eventStartFull = eventStartFull.AddDays(1);

                        string loadedMCode = item.MachineCode?.Replace("MA-", "") ?? "";
                        bool isBypassDef = loadedMCode == "33" || loadedMCode == "34" || loadedMCode == "35" || loadedMCode == "36" || loadedMCode == "37";

                        if (eventStartFull >= shiftStart && eventStartFull < shiftEnd)
                        {
                            shiftBlocks.Add(new TimelineBlockModel
                            {
                                OriginalEvent = item,
                                StartMinuteInShift = (eventStartFull - shiftStart).TotalMinutes,
                                DurationMinutes = item.DurationMinutes <= 0 ? 15 : item.DurationMinutes,
                                CurrentZoomHours = this.TimeWindowHours,
                                SourceRow = row,
                                SourceColumn = eCol,
                                IsNewUnsaved = false,
                                IsReferenceBlock = false,
                                IsBypass = isBypassDef
                            });
                        }
                        else
                        {
                            if (isTargetShiftRow) wrongTimeCounter++;
                        }
                    }
                }
            }

            AssignLanes(shiftBlocks);

            Application.Current.Dispatcher.Invoke(() =>
            {
                WrongTimeCount = wrongTimeCounter;
                CorruptedDataCount = corruptedDataCounter;

                TimelineBlocks.Clear();
                foreach (var b in shiftBlocks) TimelineBlocks.Add(b);

                RefreshDimensions();
                RecalculateSavings();
            });
        }

        private void AssignLanes(List<TimelineBlockModel> blocks)
        {
            var sorted = blocks.Where(b => !b.IsReferenceBlock).OrderBy(b => b.StartMinuteInShift).ToList();
            double[] laneEndTimes = new double[6];

            foreach (var block in sorted)
            {
                bool placed = false;
                for (int i = 0; i < 6; i++)
                {
                    if (laneEndTimes[i] <= block.StartMinuteInShift)
                    {
                        block.LaneIndex = i;
                        laneEndTimes[i] = block.StartMinuteInShift + block.DurationMinutes;
                        placed = true; break;
                    }
                }
                if (!placed) block.LaneIndex = 5;
            }
        }

        public void UpdateViewportWidth(double viewportWidth)
        {
            if (viewportWidth > 0)
            {
                _lastViewportWidth = viewportWidth;
                RefreshDimensions();
            }
        }

        private void RefreshDimensions()
        {
            if (_lastViewportWidth <= 0 || TimeWindowHours <= 0) return;

            CalculatedCanvasWidth = _lastViewportWidth * (12.0 / TimeWindowHours);

            double padding = 40.0;
            double usableWidth = CalculatedCanvasWidth - (padding * 2);
            if (usableWidth <= 0) usableWidth = 100;

            for (int i = 0; i < HourMarkers.Count; i++)
            {
                HourMarkers[i].PixelX = padding + (i / 12.0) * usableWidth;
            }

            double laneHeight = 80.0;

            foreach (var block in TimelineBlocks)
            {
                block.UpdateZoom(TimeWindowHours);
                block.PixelX = padding + (block.StartMinuteInShift / 720.0) * usableWidth;
                double w = (block.DurationMinutes / 720.0) * usableWidth;
                block.PixelWidth = Math.Max(w, 5.0);
                block.PixelY = block.LaneIndex * laneHeight + 35;
            }

            foreach (var rb in ReferenceBlocks)
            {
                rb.UpdateZoom(TimeWindowHours);
                rb.PixelX = padding + (rb.StartMinuteInShift / 720.0) * usableWidth;
                double w = (rb.DurationMinutes / 720.0) * usableWidth;
                rb.PixelWidth = Math.Max(w, 2.0);
                rb.PixelY = 5;
            }
        }

        private void ExecuteBlockClick(object parameter)
        {
            if (parameter is TimelineBlockModel clickedBlock)
            {
                SelectedBlock = clickedBlock;
            }
        }
    }
}