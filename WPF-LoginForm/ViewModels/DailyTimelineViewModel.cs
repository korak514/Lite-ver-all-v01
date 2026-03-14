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

        // Database Tracking
        public DataRow SourceRow { get; set; }

        public string SourceColumn { get; set; }
        public bool IsNewUnsaved { get; set; }

        private double _pixelX;
        public double PixelX { get => _pixelX; set => SetProperty(ref _pixelX, value); }

        private double _pixelWidth;
        public double PixelWidth { get => _pixelWidth; set => SetProperty(ref _pixelWidth, value); }

        private double _pixelY;
        public double PixelY { get => _pixelY; set => SetProperty(ref _pixelY, value); }

        public string DisplayText => OriginalEvent != null ? $"MA-{OriginalEvent.MachineCode}: {OriginalEvent.ErrorDescription}" : "";
        public string BlockColor => DetermineColor();

        public bool IsTextVisible => DurationMinutes >= 10 || CurrentZoomHours <= 2.0;
        public double TextFontSize => (DurationMinutes < 10 && CurrentZoomHours <= 2.0) ? 8.0 : 11.0;

        private string DetermineColor()
        {
            if (OriginalEvent == null) return "#808080";
            string desc = OriginalEvent.ErrorDescription?.ToLower() ?? "";
            if (desc.Contains("mola") || desc.Contains("break") || desc.Contains("yemek")) return "#2ECC71";
            if (desc.Contains("bakım") || desc.Contains("maint")) return "#F39C12";
            return "#E74C3C";
        }

        public void UpdateZoom(double zoomHours)
        {
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

        // DB Data
        private DataTable _currentData;

        private List<string> _errorColumns = new List<string>();

        // Date State
        private DateTime _targetDate;

        public DateTime TargetDate
        {
            get => _targetDate;
            set { if (SetProperty(ref _targetDate, value)) { UpdateHeaderStrings(); _ = LoadDataAsync(); } }
        }

        // Shift State
        private bool _isNightShift;

        public bool IsNightShift
        {
            get => _isNightShift;
            set { if (SetProperty(ref _isNightShift, value)) { UpdateHeaderStrings(); ParseDataToTimeline(); } }
        }

        // DB States
        private bool _isDirty;

        public bool IsDirty { get => _isDirty; set => SetProperty(ref _isDirty, value); }
        public bool IsOnlineMode => !(_repository is OfflineDataRepository);

        // Zoom State
        private double _timeWindowHours = 4.0;

        public double TimeWindowHours
        {
            get => _timeWindowHours;
            set { if (SetProperty(ref _timeWindowHours, value)) RefreshDimensions(); }
        }

        private double _calculatedCanvasWidth = 800;
        public double CalculatedCanvasWidth { get => _calculatedCanvasWidth; set => SetProperty(ref _calculatedCanvasWidth, value); }

        // Header Properties
        private string _formattedDateNumber;

        public string FormattedDateNumber { get => _formattedDateNumber; set => SetProperty(ref _formattedDateNumber, value); }

        private string _formattedDateMonthYear;
        public string FormattedDateMonthYear { get => _formattedDateMonthYear; set => SetProperty(ref _formattedDateMonthYear, value); }

        private string _formattedDay;
        public string FormattedDay { get => _formattedDay; set => SetProperty(ref _formattedDay, value); }

        private string _shiftTitle;
        public string ShiftTitle { get => _shiftTitle; set => SetProperty(ref _shiftTitle, value); }

        // UI Panels
        private bool _isFavoritesVisible = false;

        public bool IsFavoritesVisible { get => _isFavoritesVisible; set => SetProperty(ref _isFavoritesVisible, value); }
        public ObservableCollection<FavoriteEvent> FavoriteEvents { get; } = new ObservableCollection<FavoriteEvent>();

        public ObservableCollection<string> AvailableMachineCodes { get; } = new ObservableCollection<string>();

        // Edit Panel Properties
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
                    IsEditPanelVisible = _selectedBlock != null;
                    if (_selectedBlock != null) SyncEditFields();
                }
            }
        }

        // NEW: Machine Code Grouping State
        private bool _isMachineTypeExtra;

        public bool IsMachineTypeExtra
        {
            get => _isMachineTypeExtra;
            set
            {
                if (SetProperty(ref _isMachineTypeExtra, value))
                {
                    OnPropertyChanged(nameof(IsMachineTypeNormal));

                    // If toggled to Extra and the current code isn't in the list, set to default
                    if (value && SelectedBlock?.OriginalEvent != null && !AvailableMachineCodes.Contains(SelectedBlock.OriginalEvent.MachineCode))
                    {
                        SelectedBlock.OriginalEvent.MachineCode = AvailableMachineCodes.FirstOrDefault() ?? "00";
                        OnPropertyChanged(nameof(SelectedBlock));
                    }
                }
            }
        }

        public bool IsMachineTypeNormal => !_isMachineTypeExtra;

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

        public ObservableCollection<TimelineBlockModel> TimelineBlocks { get; } = new ObservableCollection<TimelineBlockModel>();
        public ObservableCollection<HourMarker> HourMarkers { get; } = new ObservableCollection<HourMarker>();

        // Commands
        public ICommand ToggleEditPanelCommand { get; }

        public ICommand AddNewEventCommand { get; } // NEW
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
            RefreshCommand = new ViewModelCommand(p => _ = LoadDataAsync());

            LoadConfigData();
            UpdateHeaderStrings();
            _ = LoadDataAsync();
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
                        return;
                    }
                }
            }
            catch { }

            if (!AvailableMachineCodes.Any()) { AvailableMachineCodes.Add("00"); AvailableMachineCodes.Add("01"); }
            if (!FavoriteEvents.Any()) { FavoriteEvents.Add(new FavoriteEvent { Title = "Lunch Break", MachineCode = "00", Description = "Yemek Molası", DefaultDuration = 60, ColorHex = "#2ECC71" }); }
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
            if (_dialogService.ShowInputDialog("Add Machine Code", "Enter a new EXTRAS machine code:", "", out string newCode))
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

        private void ExecuteAddNewEvent()
        {
            // By passing 0, 0, it creates the event exactly at the start of the shift
            HandleDropOrClickAdd(null, 40, 30); // Uses canvas padding coordinates
        }

        public void HandleDropOrClickAdd(FavoriteEvent fav, double dropPixelX, double dropPixelY)
        {
            if (CalculatedCanvasWidth <= 0) return;

            // Offset by padding so dropping aligns mathematically
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

            SelectedBlock = new TimelineBlockModel
            {
                OriginalEvent = new ErrorEventModel
                {
                    Date = TargetDate,
                    StartTime = eventStart.ToString("HH:mm"),
                    EndTime = eventEnd.ToString("HH:mm"),
                    DurationMinutes = dur,
                    MachineCode = fav?.MachineCode ?? "00",
                    ErrorDescription = fav?.Description ?? "New Event"
                },
                LaneIndex = lane,
                StartMinuteInShift = startMin,
                DurationMinutes = dur,
                CurrentZoomHours = this.TimeWindowHours,
                IsNewUnsaved = true
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
                if (MessageBox.Show($"Remove '{fav.Title}' from favorites?", "Remove", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    FavoriteEvents.Remove(fav);
                    SaveConfigData();
                }
            }
        }

        // ==========================================
        // DATABASE INTERACTION LOGIC
        // ==========================================

        private string GenerateDbString(TimelineBlockModel block)
        {
            var ev = block.OriginalEvent;
            string rawTime = ev.StartTime.Replace(":", "");
            string machine = ev.MachineCode.Replace("MA-", "");
            return $"{rawTime}-{ev.DurationMinutes}-MA-{machine}-{ev.ErrorDescription}";
        }

        private void ExecuteApplyToTimeline()
        {
            if (SelectedBlock == null || _currentData == null) return;
            CalculateTime();
            SelectedBlock.RefreshUI();

            if (SelectedBlock.SourceRow == null || string.IsNullOrEmpty(SelectedBlock.SourceColumn))
            {
                var dateCol = _currentData.Columns.Cast<DataColumn>().FirstOrDefault(c => c.ColumnName.ToLower().Contains("date") || c.ColumnName.ToLower().Contains("tarih"));
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
        }

        private void ExecuteDeleteEvent()
        {
            if (SelectedBlock != null && TimelineBlocks.Contains(SelectedBlock))
            {
                if (SelectedBlock.SourceRow != null && !string.IsNullOrEmpty(SelectedBlock.SourceColumn))
                {
                    SelectedBlock.SourceRow[SelectedBlock.SourceColumn] = DBNull.Value;
                    SetDirty();
                }
                TimelineBlocks.Remove(SelectedBlock);
                SelectedBlock = null;
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
                MessageBox.Show("Changes saved to database successfully.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"Failed to save: {result.ErrorMessage}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteUndo()
        {
            _currentData?.RejectChanges();
            IsDirty = false;
            ParseDataToTimeline();
        }

        private void SetDirty()
        {
            IsDirty = true;
            (SaveToDatabaseCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
            (UndoCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
        }

        // ==========================================

        private void SyncEditFields()
        {
            if (SelectedBlock?.OriginalEvent == null) return;

            _editStartTime = SelectedBlock.OriginalEvent.StartTime;
            _editEndTime = SelectedBlock.OriginalEvent.EndTime;
            _editDuration = SelectedBlock.OriginalEvent.DurationMinutes;

            // Determine if the code belongs to Extra or Normal
            if (AvailableMachineCodes.Contains(SelectedBlock.OriginalEvent.MachineCode))
            {
                IsMachineTypeExtra = true;
            }
            else
            {
                IsMachineTypeExtra = false;
            }

            OnPropertyChanged(nameof(EditStartTime)); OnPropertyChanged(nameof(EditEndTime)); OnPropertyChanged(nameof(EditDuration));
        }

        private void CalculateTime()
        {
            if (SelectedBlock?.OriginalEvent == null) return;
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

            ShiftTitle = IsNightShift ? "Night Shift (20:00 - 08:00)" : "Day Shift (08:00 - 20:00)";
            RefreshDimensions();
        }

        private async Task LoadDataAsync()
        {
            if (string.IsNullOrEmpty(_tableName)) return;

            var result = await _repository.GetTableDataAsync(_tableName, 0);
            if (result.Data != null)
            {
                _currentData = result.Data;

                _errorColumns.Clear();
                foreach (DataColumn c in _currentData.Columns)
                {
                    string n = c.ColumnName.ToLower();
                    if (n.StartsWith("hata_kodu") || n.StartsWith("error_code") || n.StartsWith("code"))
                        _errorColumns.Add(c.ColumnName);
                }

                IsDirty = false;
                ParseDataToTimeline();
            }
        }

        private void ParseDataToTimeline()
        {
            if (_currentData == null) return;
            var shiftBlocks = new List<TimelineBlockModel>();

            var dateCol = _currentData.Columns.Cast<DataColumn>().FirstOrDefault(c => c.ColumnName.ToLower().Contains("date") || c.ColumnName.ToLower().Contains("tarih"));
            var shiftCol = _currentData.Columns.Cast<DataColumn>().FirstOrDefault(c => c.ColumnName.ToLower().Contains("shift") || c.ColumnName.ToLower().Contains("vardiya"));

            foreach (DataRow row in _currentData.Rows)
            {
                if (row.RowState == DataRowState.Deleted) continue;

                DateTime rDate = dateCol != null && row[dateCol] != DBNull.Value ? Convert.ToDateTime(row[dateCol]) : DateTime.MinValue;
                string rShift = shiftCol != null && row[shiftCol] != DBNull.Value ? row[shiftCol].ToString() : "";

                if (rDate.Date >= TargetDate.AddDays(-1) && rDate.Date <= TargetDate.AddDays(1))
                {
                    foreach (var eCol in _errorColumns)
                    {
                        string cellData = row[eCol]?.ToString();
                        if (string.IsNullOrWhiteSpace(cellData)) continue;

                        var item = ErrorEventModel.Parse(cellData, rDate, rShift, 0, 0, 0, 0, Guid.NewGuid().ToString());
                        if (item == null || string.IsNullOrEmpty(item.StartTime) || item.ErrorDescription == "NO_ERROR") continue;

                        if (!TimeSpan.TryParse(item.StartTime, out TimeSpan ts)) continue;

                        DateTime eventStartFull = item.Date.Date.Add(ts);
                        DateTime shiftStart = IsNightShift ? TargetDate.Date.AddHours(20) : TargetDate.Date.AddHours(8);
                        DateTime shiftEnd = shiftStart.AddHours(12);

                        if (IsNightShift && ts.Hours < 12) eventStartFull = eventStartFull.AddDays(1);

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
                                IsNewUnsaved = false
                            });
                        }
                    }
                }
            }

            AssignLanes(shiftBlocks);

            Application.Current.Dispatcher.Invoke(() =>
            {
                TimelineBlocks.Clear();
                foreach (var b in shiftBlocks) TimelineBlocks.Add(b);
                RefreshDimensions();
            });
        }

        private void AssignLanes(List<TimelineBlockModel> blocks)
        {
            var sorted = blocks.OrderBy(b => b.StartMinuteInShift).ToList();
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

            // PADDING CALCULATION SO ENDS AREN'T CUT OFF
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
        }

        private void ExecuteBlockClick(object parameter)
        {
            if (parameter is TimelineBlockModel clickedBlock) SelectedBlock = clickedBlock;
        }
    }
}