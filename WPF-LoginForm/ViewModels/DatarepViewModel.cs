using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions; // Required for CSV Regex
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using OfficeOpenXml; // Required for EPPlus
using WPF_LoginForm.Models;
using WPF_LoginForm.Properties;
using WPF_LoginForm.Repositories;
using WPF_LoginForm.Services;
using WPF_LoginForm.Services.Database;

namespace WPF_LoginForm.ViewModels
{
    public class DatarepViewModel : ViewModelBase
    {
        private readonly IDialogService _dialogService;
        private readonly IDataRepository _dataRepository;
        private readonly ILogger _logger;

        // --- State Fields ---
        private DataView _dataTableView;

        private string _selectedTable;
        private ObservableCollection<string> _tableNames;
        private bool _isBusy;
        private bool _isProgressBarVisible;
        private string _errorMessage;
        private bool _isDirty;
        private DataTable _currentDataTable;
        private double _dataGridFontSize = 12;

        // Thread Safety & History
        private int _longRunningOperationCount = 0;

        private ObservableCollection<DataRowView> _editableRows = new ObservableCollection<DataRowView>();
        private readonly List<DataRow> _rowChangeHistory = new List<DataRow>();

        // --- Filtering Fields ---
        private string _searchText;

        private bool _isColumnSelectorVisible;
        private string _selectedSearchColumn;
        private string _filterStatus;
        private bool _loadAllData = false;

        // NEW: Global Search Toggle State
        private bool _isGlobalSearchActive = false;

        // --- Date Filter Fields ---
        private bool _isDateFilterVisible;

        private bool _isDateFilterPanelVisible;
        private string _dateFilterColumnName;
        private DateTime? _filterStartDate;
        private DateTime? _filterEndDate;
        private double _sliderMaximum;
        private double _startMonthSliderValue;
        private double _endMonthSliderValue;
        private DateTime _minSliderDate;
        private bool _isUpdatingDates = false;

        // --- Configuration ---
        private readonly List<string> _dateColumnAliases = new List<string> { "Tarih", "Date", "EntryDate" };

        private readonly List<Type> _numericTypes = new List<Type> { typeof(int), typeof(double), typeof(decimal), typeof(float), typeof(long), typeof(short), typeof(byte), typeof(sbyte), typeof(uint), typeof(ulong), typeof(ushort) };

        private bool _isIdHidden = true;
        private bool _isIdEditable = false;
        private bool _isAdvancedImportVisible = false;

        public bool IsAdmin
        {
            get { return UserSessionService.IsAdmin; }
        }

        // --- Properties (Verbose) ---

        public ObservableCollection<string> TableNames
        {
            get { return _tableNames; }
            private set { SetProperty(ref _tableNames, value); }
        }

        public string SelectedTable
        {
            get { return _selectedTable; }
            set
            {
                if (SetProperty(ref _selectedTable, value))
                {
                    // Fix: Clear Ghost Edits to prevent crashes
                    EditableRows.Clear();

                    // Reset Date Filter State
                    IsDateFilterVisible = false;
                    IsDateFilterPanelVisible = false;
                    _dateFilterColumnName = null;

                    UnsubscribeFromTableEvents();

                    _selectedTable = value;
                    OnPropertyChanged();

                    LoadAllData = false;
                    RefreshCommandStates();

                    if (!string.IsNullOrEmpty(_selectedTable))
                    {
                        LoadDataForSelectedTableAsync();
                    }
                    else
                    {
                        DataTableView = null;
                        SetErrorMessage(null);
                        IsDirty = false;
                    }
                }
            }
        }

        public bool LoadAllData
        {
            get { return _loadAllData; }
            set
            {
                if (SetProperty(ref _loadAllData, value))
                {
                    if (!string.IsNullOrEmpty(SelectedTable))
                        LoadDataForSelectedTableAsync();
                }
            }
        }

        public DataView DataTableView
        {
            get { return _dataTableView; }
            private set
            {
                if (SetProperty(ref _dataTableView, value))
                {
                    UnsubscribeFromTableEvents();
                    _dataTableView = value;
                    _currentDataTable = _dataTableView?.Table;
                    SubscribeToTableEvents();

                    IsDirty = false;
                    EditableRows.Clear();
                    _rowChangeHistory.Clear();

                    // Reset Search but DO NOT trigger filter (prevents lag on load)
                    _searchText = string.Empty;
                    OnPropertyChanged(nameof(SearchText));

                    PopulateSearchableColumns();

                    OnPropertyChanged();
                    OnPropertyChanged(nameof(EditableRows));
                }
            }
        }

        // NEW: Global Search Property
        public bool IsGlobalSearchActive
        {
            get { return _isGlobalSearchActive; }
            set
            {
                if (SetProperty(ref _isGlobalSearchActive, value))
                {
                    // Re-apply filters when toggle changes
                    ApplyCombinedFilters();
                }
            }
        }

        public bool IsBusy
        {
            get { return _isBusy; }
            private set { SetProperty(ref _isBusy, value); }
        }

        public bool IsProgressBarVisible
        {
            get { return _isProgressBarVisible; }
            private set { SetProperty(ref _isProgressBarVisible, value); }
        }

        public string ErrorMessage
        {
            get { return _errorMessage; }
            private set { if (SetProperty(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); }
        }

        public bool HasError
        {
            get { return !string.IsNullOrEmpty(ErrorMessage); }
        }

        public bool IsDirty
        {
            get { return _isDirty; }
            private set { if (SetProperty(ref _isDirty, value)) OnPropertyChanged(); }
        }

        public double DataGridFontSize
        {
            get { return _dataGridFontSize; }
            set
            {
                var newSize = Math.Max(8, Math.Min(24, value));
                if (SetProperty(ref _dataGridFontSize, newSize))
                {
                    (DecreaseFontSizeCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
                    (IncreaseFontSizeCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public ObservableCollection<DataRowView> EditableRows
        {
            get { return _editableRows; }
        }

        public ObservableCollection<string> SearchableColumns { get; } = new ObservableCollection<string>();

        public string SearchText
        {
            get { return _searchText; }
            set
            {
                if (SetProperty(ref _searchText, value))
                    ApplyCombinedFilters();
            }
        }

        public bool IsColumnSelectorVisible
        {
            get { return _isColumnSelectorVisible; }
            set { SetProperty(ref _isColumnSelectorVisible, value); }
        }

        public string SelectedSearchColumn
        {
            get { return _selectedSearchColumn; }
            set
            {
                if (SetProperty(ref _selectedSearchColumn, value))
                {
                    // FIX: If user picks a specific column, turn OFF global search
                    _isGlobalSearchActive = false;
                    OnPropertyChanged(nameof(IsGlobalSearchActive));

                    ApplyCombinedFilters();
                    RefreshCommandStates();
                }
            }
        }

        public string FilterStatus
        {
            get { return _filterStatus; }
            private set { SetProperty(ref _filterStatus, value); }
        }

        public bool IsDateFilterVisible
        {
            get { return _isDateFilterVisible; }
            private set { SetProperty(ref _isDateFilterVisible, value); }
        }

        public bool IsDateFilterPanelVisible
        {
            get { return _isDateFilterPanelVisible; }
            set { SetProperty(ref _isDateFilterPanelVisible, value); }
        }

        public DateTime? FilterStartDate
        {
            get { return _filterStartDate; }
            set { if (SetProperty(ref _filterStartDate, value) && !_isUpdatingDates) { ApplyCombinedFilters(); UpdateSlidersFromDates(); } }
        }

        public DateTime? FilterEndDate
        {
            get { return _filterEndDate; }
            set { if (SetProperty(ref _filterEndDate, value) && !_isUpdatingDates) { ApplyCombinedFilters(); UpdateSlidersFromDates(); } }
        }

        public double SliderMaximum
        { get { return _sliderMaximum; } set { SetProperty(ref _sliderMaximum, value); } }

        public double StartMonthSliderValue
        {
            get { return _startMonthSliderValue; }
            set { if (SetProperty(ref _startMonthSliderValue, value) && !_isUpdatingDates) UpdateDatesFromSliders(); }
        }

        public double EndMonthSliderValue
        {
            get { return _endMonthSliderValue; }
            set { if (SetProperty(ref _endMonthSliderValue, value) && !_isUpdatingDates) UpdateDatesFromSliders(); }
        }

        public bool IsIdHidden
        {
            get { return _isIdHidden; }
            set { if (SetProperty(ref _isIdHidden, value)) OnPropertyChanged(nameof(IsIdVisible)); }
        }

        public bool IsIdVisible
        { get { return !_isIdHidden; } }

        public bool IsIdEditable
        { get { return _isIdEditable; } set { SetProperty(ref _isIdEditable, value); } }

        public bool IsAdvancedImportVisible
        { get { return _isAdvancedImportVisible; } set { SetProperty(ref _isAdvancedImportVisible, value); } }

        // --- Commands ---
        public ICommand AddNewRowCommand { get; }

        public ICommand SaveChangesCommand { get; }
        public ICommand UndoChangesCommand { get; }
        public ICommand EditSelectedRowsCommand { get; }
        public ICommand ReloadDataCommand { get; }
        public ICommand DeleteSelectedRowCommand { get; }
        public ICommand DeleteTableCommand { get; }
        public ICommand ImportDataCommand { get; }
        public ICommand ShowAdvancedImportCommand { get; }
        public ICommand ShowCreateTableCommand { get; }
        public ICommand ExportDataCommand { get; }
        public ICommand DecreaseFontSizeCommand { get; }
        public ICommand IncreaseFontSizeCommand { get; }
        public ICommand ClearSearchCommand { get; }
        public ICommand ClearDateFilterCommand { get; }
        public ICommand AddIdColumnCommand { get; }
        public ICommand ShowHierarchyImportCommand { get; }
        public ICommand RenameColumnCommand { get; }

        public DatarepViewModel(ILogger logger, IDialogService dialogService, IDataRepository dataRepository)
        {
            _logger = logger;
            _dialogService = dialogService;
            _dataRepository = dataRepository;
            TableNames = new ObservableCollection<string>();

            // Initialize Commands
            AddNewRowCommand = new ViewModelCommand(ExecuteAddNewRow, CanExecuteAddNewRow);
            SaveChangesCommand = new ViewModelCommand(ExecuteSaveChanges, CanExecuteSaveChanges);
            UndoChangesCommand = new ViewModelCommand(ExecuteUndoChanges, CanExecuteUndoChanges);
            EditSelectedRowsCommand = new ViewModelCommand(ExecuteEditSelectedRows, CanExecuteEditSelectedRows);
            ReloadDataCommand = new ViewModelCommand(ExecuteReloadData, CanExecuteReloadData);
            DeleteSelectedRowCommand = new ViewModelCommand(ExecuteDeleteSelectedRow, CanExecuteDeleteSelectedRow);
            DeleteTableCommand = new ViewModelCommand(ExecuteDeleteTableCommand, CanExecuteDeleteTableCommand);
            ImportDataCommand = new ViewModelCommand(ExecuteImportData, CanExecuteImportData);
            ShowAdvancedImportCommand = new ViewModelCommand(ExecuteShowAdvancedImport, CanExecuteImportData);
            ShowCreateTableCommand = new ViewModelCommand(ExecuteShowCreateTableCommand);
            ExportDataCommand = new ViewModelCommand(ExecuteExportData, CanExecuteExportData);
            AddIdColumnCommand = new ViewModelCommand(ExecuteAddIdColumn, CanExecuteAddIdColumn);

            // Rename Column: Admin Only
            RenameColumnCommand = new ViewModelCommand(ExecuteRenameColumn, p => !IsBusy && IsAdmin && !string.IsNullOrEmpty(SelectedSearchColumn));

            DecreaseFontSizeCommand = new ViewModelCommand(p => DataGridFontSize--, p => DataGridFontSize > 8);
            IncreaseFontSizeCommand = new ViewModelCommand(p => DataGridFontSize++, p => DataGridFontSize < 24);

            ClearSearchCommand = new ViewModelCommand(p =>
            {
                SearchText = string.Empty;
                IsGlobalSearchActive = false; // Reset global search
            });

            ClearDateFilterCommand = new ViewModelCommand(p =>
            {
                FilterStartDate = null;
                FilterEndDate = null;
                IsDateFilterPanelVisible = false;
                ApplyCombinedFilters();
            });

            ShowHierarchyImportCommand = new ViewModelCommand(ExecuteShowHierarchyImport);

            // Subscribe to Retry Events
            DatabaseRetryPolicy.OnRetryStatus += (msg) =>
            {
                if (Application.Current != null)
                    Application.Current.Dispatcher.Invoke(() => SetErrorMessage(msg));
            };

            LoadInitialDataAsync();
        }

        // --- PUBLIC METHOD FOR DRILL-DOWN ---
        public async void LoadTableWithFilter(string tableName, DateTime start, DateTime end, string searchText = "")
        {
            SelectedTable = tableName;

            // Wait if busy to prevent crash
            int retries = 0;
            while (IsBusy && retries < 20) { await Task.Delay(100); retries++; }

            if (DataTableView != null)
            {
                FilterStartDate = start;
                FilterEndDate = end;

                // AUTO-ACTIVATE Global Search if text is provided (from Chart click)
                if (!string.IsNullOrEmpty(searchText))
                {
                    IsGlobalSearchActive = true;
                    SearchText = searchText;
                }

                IsDateFilterVisible = true;
                IsDateFilterPanelVisible = true;

                ApplyCombinedFilters();
            }
        }

        // --- Command States ---

        private void RefreshCommandStates()
        {
            (AddNewRowCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
            (SaveChangesCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
            (DeleteTableCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
            (AddIdColumnCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
            (ReloadDataCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
            (ImportDataCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
            (ExportDataCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
            (RenameColumnCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
        }

        // Explicit CanExecute Methods (Restored for Verbose Style)
        private bool CanExecuteAddNewRow(object p)
        { return _currentDataTable != null && !IsBusy; }

        private bool CanExecuteSaveChanges(object p)
        { return IsDirty && !IsBusy; }

        private bool CanExecuteUndoChanges(object p)
        { return _rowChangeHistory.Any() && !IsBusy; }

        private bool CanExecuteEditSelectedRows(object p)
        { return p is IList i && i.Count > 0 && !IsBusy; }

        private bool CanExecuteReloadData(object p)
        { return !string.IsNullOrEmpty(SelectedTable) && !IsBusy; }

        private bool CanExecuteDeleteSelectedRow(object p)
        { return p is IList i && i.Count > 0 && !IsBusy; }

        private bool CanExecuteExportData(object p)
        { return _currentDataTable != null && _currentDataTable.Rows.Count > 0 && !IsBusy; }

        private bool CanExecuteImportData(object p)
        { return _currentDataTable != null && !IsBusy; }

        private bool CanExecuteAddIdColumn(object p)
        { return _currentDataTable != null && !IsBusy && !_currentDataTable.Columns.Contains("ID"); }

        private bool CanExecuteDeleteTableCommand(object p)
        { return !string.IsNullOrEmpty(SelectedTable) && !IsBusy && IsAdmin; }

        // --- RENAME COLUMN FEATURE ---
        private async void ExecuteRenameColumn(object p)
        {
            if (string.IsNullOrEmpty(SelectedTable) || string.IsNullOrEmpty(SelectedSearchColumn)) return;

            string oldName = SelectedSearchColumn;
            if (oldName.Equals("ID", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Renaming the ID column is restricted.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_dialogService.ShowInputDialog("Rename Column", $"Enter new name for column '{oldName}':", oldName, out string newName))
            {
                if (string.IsNullOrWhiteSpace(newName) || newName.Equals(oldName, StringComparison.OrdinalIgnoreCase)) return;

                if (!Regex.IsMatch(newName, @"^[a-zA-Z0-9_]+$"))
                {
                    MessageBox.Show("Invalid name. Use only letters, numbers, and underscores.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                await ExecuteLongRunningOperation(async () =>
                {
                    var result = await _dataRepository.RenameColumnAsync(SelectedTable, oldName, newName);
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (result.Success)
                        {
                            LoadDataForSelectedTableAsync();
                            SetErrorMessage(null);
                        }
                        else
                        {
                            SetErrorMessage($"Rename failed: {result.ErrorMessage}");
                        }
                    });
                });
            }
        }

        // --- DATA LOADING & THREADING ---

        private async Task ExecuteLongRunningOperation(Func<Task> operation)
        {
            // Thread Safety: Interlocked (Restored)
            Interlocked.Increment(ref _longRunningOperationCount);
            IsBusy = true;
            SetErrorMessage(null);
            IsProgressBarVisible = false;

            // Delayed Progress Bar (Restored)
            var progressTask = Task.Run(async () =>
            {
                await Task.Delay(2000);
                if (IsBusy && Application.Current != null)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() => IsProgressBarVisible = true);
                }
            });

            try
            {
                await operation();
            }
            catch (Exception ex)
            {
                _logger.LogError("[LongOp] Exception.", ex);
                SetErrorMessage($"An error occurred: {ex.Message}");
            }
            finally
            {
                if (Interlocked.Decrement(ref _longRunningOperationCount) == 0)
                {
                    IsBusy = false;
                    IsProgressBarVisible = false;
                }
                RefreshCommandStates();
            }
        }

        private async void LoadInitialDataAsync()
        {
            await ExecuteLongRunningOperation(async () =>
            {
                var names = await _dataRepository.GetTableNamesAsync();
                if (Application.Current == null) return;

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    TableNames.Clear();
                    foreach (var n in names ?? new List<string>()) TableNames.Add(n);

                    // CHANGED: Only select default table if one isn't already selected (e.g. by DrillDown)
                    if (string.IsNullOrEmpty(SelectedTable))
                    {
                        SelectedTable = TableNames.FirstOrDefault();
                        if (SelectedTable == null)
                            SetErrorMessage("No tables found.");
                    }
                });
            });
        }

        private async void LoadDataForSelectedTableAsync()
        {
            if (string.IsNullOrEmpty(SelectedTable)) return;

            string tableNameRequested = SelectedTable; // Capture to prevent race condition

            await ExecuteLongRunningOperation(async () =>
            {
                int limit = LoadAllData ? 0 : Settings.Default.DefaultRowLimit;

                var result = await _dataRepository.GetTableDataAsync(tableNameRequested, limit);
                DataTable dataTable = result.Data;
                bool isSortable = result.IsSortable;

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // Race condition check
                    if (SelectedTable != tableNameRequested) return;

                    // --- BUG FIX: Enforce Primary Key Logic ---
                    if (dataTable != null)
                    {
                        // 1. Find the ID column (Case Insensitive)
                        var idColumn = dataTable.Columns.Cast<DataColumn>()
                            .FirstOrDefault(c => c.ColumnName.Equals("ID", StringComparison.OrdinalIgnoreCase));

                        if (idColumn != null)
                        {
                            try
                            {
                                // 2. Force DataTable to recognize it as the Primary Key
                                // This enables the "Find" method used during Save/Delete
                                dataTable.PrimaryKey = new DataColumn[] { idColumn };

                                // 3. Ensure the ID column itself is ReadOnly in the UI
                                // (User shouldn't edit auto-increment IDs)
                                if (idColumn.DataType == typeof(int) || idColumn.DataType == typeof(long))
                                {
                                    idColumn.AutoIncrement = true; // Hints the grid
                                    idColumn.ReadOnly = true;      // Locks the cell
                                }
                            }
                            catch (Exception ex)
                            {
                                // If duplicate IDs exist (bad data), PK setting will fail.
                                // Log it, but don't crash. Editing will be risky here.
                                System.Diagnostics.Debug.WriteLine($"Could not set Primary Key: {ex.Message}");
                            }
                        }
                    }
                    // ------------------------------------------

                    DataTableView = dataTable?.DefaultView;
                    SetupDateFilterForTable();

                    // Re-apply filters (Global Search) if active
                    ApplyCombinedFilters();

                    if (!isSortable)
                    {
                        SetErrorMessage("⚠️ Table has no 'ID' or 'Date'. Editing might be restricted.");
                    }
                    else if (!LoadAllData && dataTable.Rows.Count >= limit && limit > 0)
                    {
                        FilterStatus = $"⚠️ {Resources.Tip_LoadFullHistory}";
                    }
                });
            });
        }

        // --- FILTERING LOGIC ---

        private void ApplyCombinedFilters()
        {
            if (DataTableView == null || _currentDataTable == null) return;

            var filters = new List<string>();

            // 1. Text Search Logic
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                try
                {
                    string safeSearch = SearchText.Replace("'", "''").Replace("[", "[[]").Replace("%", "[%]").Replace("*", "[*]");
                    string textFilter = string.Empty;

                    // CASE A: Global Search (Look in ALL columns)
                    if (IsGlobalSearchActive)
                    {
                        var colFilters = new List<string>();
                        foreach (DataColumn col in _currentDataTable.Columns)
                        {
                            // Convert everything to string for loose searching
                            colFilters.Add($"Convert([{col.ColumnName}], 'System.String') LIKE '%{safeSearch}%'");
                        }
                        // Construct OR query
                        if (colFilters.Any())
                            textFilter = $"({string.Join(" OR ", colFilters)})";
                    }
                    // CASE B: Specific Column Search (Default)
                    else if (!string.IsNullOrEmpty(SelectedSearchColumn))
                    {
                        string safeColName = SelectedSearchColumn.Replace("]", "]]");

                        // Numeric > < Logic
                        if (SearchText.Trim().StartsWith(">") || SearchText.Trim().StartsWith("<"))
                        {
                            DataColumn column = _currentDataTable.Columns[SelectedSearchColumn];

                            string op = SearchText.Trim().StartsWith(">=") || SearchText.Trim().StartsWith("<=")
                                ? SearchText.Trim().Substring(0, 2)
                                : SearchText.Trim().Substring(0, 1);

                            string numberPart = SearchText.Trim().Substring(op.Length).Trim();

                            if (_numericTypes.Contains(column.DataType) &&
                                double.TryParse(numberPart, NumberStyles.Any, CultureInfo.CurrentCulture, out double numValue))
                            {
                                textFilter = $"[{safeColName}] {op} {numValue.ToString(CultureInfo.InvariantCulture)}";
                            }
                        }

                        if (string.IsNullOrEmpty(textFilter))
                        {
                            textFilter = $"Convert([{safeColName}], 'System.String') LIKE '%{safeSearch}%'";
                        }
                    }

                    if (!string.IsNullOrEmpty(textFilter)) filters.Add(textFilter);
                }
                catch { }
            }

            // 2. Date Filter
            if (IsDateFilterVisible && !string.IsNullOrEmpty(_dateFilterColumnName) && FilterStartDate.HasValue && FilterEndDate.HasValue)
            {
                string safeDateCol = _dateFilterColumnName.Replace("]", "]]");
                filters.Add($"[{safeDateCol}] >= #{FilterStartDate.Value:MM/dd/yyyy}#");
                filters.Add($"[{safeDateCol}] <= #{FilterEndDate.Value:MM/dd/yyyy}#");
            }

            try
            {
                DataTableView.RowFilter = string.Join(" AND ", filters);
                UpdateFilterStatus();
            }
            catch (Exception ex) { SetErrorMessage($"Invalid filter: {ex.Message}"); }
        }

        // --- PERSISTENCE ---

        private async void ExecuteSaveChanges(object p)
        {
            if (!CanExecuteSaveChanges(p)) return;

            var changes = _currentDataTable?.GetChanges();
            if (changes == null || changes.Rows.Count == 0)
            {
                IsDirty = false;
                return;
            }

            // Check if we are adding new rows (which will generate new IDs)
            bool hasNewRows = changes.AsEnumerable().Any(r => r.RowState == DataRowState.Added);

            // --- FIX START: Robust ID Check ---
            // Check if "ID" column exists (Case Insensitive)
            bool hasIdColumn = _currentDataTable.Columns.Cast<DataColumn>()
                               .Any(c => c.ColumnName.Equals("ID", StringComparison.OrdinalIgnoreCase));

            if (!hasIdColumn)
            {
                MessageBox.Show("Cannot save: Table has no 'ID' column. Use 'Add ID Column' if needed.",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            // --- FIX END ---

            if (!_dialogService.ShowConfirmationDialog(Resources.Save, $"Save {changes.Rows.Count} changes?")) return;

            await ExecuteLongRunningOperation(async () =>
            {
                var result = await _dataRepository.SaveChangesAsync(changes, SelectedTable);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (result.Success)
                    {
                        _currentDataTable.AcceptChanges();
                        EditableRows.Clear();

                        // CRITICAL FIX: Reload data to get real IDs from DB if rows were added
                        if (hasNewRows)
                        {
                            LoadDataForSelectedTableAsync();
                        }

                        SetErrorMessage(null);
                        CheckIfDirty();
                    }
                    else
                    {
                        SetErrorMessage(GetFriendlyErrorMessage(result.ErrorMessage));
                    }
                });
            });
        }

        // --- EVENTS & SETUP ---

        private void SubscribeToTableEvents()
        {
            if (_currentDataTable != null)
            {
                _currentDataTable.RowChanged += OnDataTableRowChanged;
                _currentDataTable.RowDeleted += OnDataTableRowChanged;
                // Logic: TableNewRow event (Restored)
                _currentDataTable.TableNewRow += OnDataTableNewRow;
            }
        }

        private void UnsubscribeFromTableEvents()
        {
            if (_currentDataTable != null)
            {
                _currentDataTable.RowChanged -= OnDataTableRowChanged;
                _currentDataTable.RowDeleted -= OnDataTableRowChanged;
                _currentDataTable.TableNewRow -= OnDataTableNewRow;
            }
        }

        private void OnDataTableRowChanged(object sender, DataRowChangeEventArgs e)
        {
            if (e.Action == DataRowAction.Add || e.Action == DataRowAction.Change || e.Action == DataRowAction.Delete)
            {
                if (_rowChangeHistory.Contains(e.Row)) _rowChangeHistory.Remove(e.Row);
                _rowChangeHistory.Add(e.Row);
            }
            CheckIfDirty();
            (UndoChangesCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
        }

        private void OnDataTableNewRow(object sender, DataTableNewRowEventArgs e)
        { /* Hook for new row init if needed */ }

        private void SetupDateFilterForTable()
        {
            IsDateFilterVisible = false;
            IsDateFilterPanelVisible = false;
            _dateFilterColumnName = null;
            _filterStartDate = null;
            _filterEndDate = null;

            OnPropertyChanged(nameof(FilterStartDate));
            OnPropertyChanged(nameof(FilterEndDate));

            if (_currentDataTable == null) return;

            var foundDateColumns = _currentDataTable.Columns.Cast<DataColumn>()
                .Where(c => c.DataType == typeof(DateTime) && _dateColumnAliases.Contains(c.ColumnName, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (foundDateColumns.Count > 1) { SetErrorMessage($"Ambiguous Date Columns in '{SelectedTable}'."); return; }
            if (foundDateColumns.Count == 0) return;

            var dateColumn = foundDateColumns.Single();
            var dates = _currentDataTable.AsEnumerable()
                .Where(r => r.RowState != DataRowState.Deleted)
                .Select(r => r.Field<DateTime?>(dateColumn))
                .Where(d => d.HasValue)
                .Select(d => d.Value)
                .ToList();

            if (!dates.Any()) return;

            _minSliderDate = dates.Min();
            _dateFilterColumnName = dateColumn.ColumnName;
            SliderMaximum = ((dates.Max().Year - _minSliderDate.Year) * 12) + dates.Max().Month - _minSliderDate.Month;

            _isUpdatingDates = true; StartMonthSliderValue = 0; EndMonthSliderValue = SliderMaximum; _isUpdatingDates = false;
            IsDateFilterVisible = true;
            UpdateDatesFromSliders();
        }

        private void UpdateSlidersFromDates()
        {
            if (!IsDateFilterVisible || !FilterStartDate.HasValue || !FilterEndDate.HasValue || _isUpdatingDates) return;
            _isUpdatingDates = true;
            StartMonthSliderValue = ((FilterStartDate.Value.Year - _minSliderDate.Year) * 12) + FilterStartDate.Value.Month - _minSliderDate.Month;
            EndMonthSliderValue = ((FilterEndDate.Value.Year - _minSliderDate.Year) * 12) + FilterEndDate.Value.Month - _minSliderDate.Month;
            _isUpdatingDates = false;
        }

        private void UpdateDatesFromSliders()
        {
            if (!IsDateFilterVisible || _isUpdatingDates) return;
            _isUpdatingDates = true;

            if (StartMonthSliderValue > EndMonthSliderValue) StartMonthSliderValue = EndMonthSliderValue;

            var start = _minSliderDate.AddMonths((int)StartMonthSliderValue);
            var end = _minSliderDate.AddMonths((int)EndMonthSliderValue);
            FilterStartDate = new DateTime(start.Year, start.Month, 1);
            FilterEndDate = new DateTime(end.Year, end.Month, DateTime.DaysInMonth(end.Year, end.Month));

            _isUpdatingDates = false;
            ApplyCombinedFilters();
        }

        private void PopulateSearchableColumns()
        {
            SearchableColumns.Clear();
            if (_currentDataTable == null) return;
            foreach (DataColumn col in _currentDataTable.Columns) SearchableColumns.Add(col.ColumnName);

            // Logic: Prefer String Columns
            SelectedSearchColumn = _currentDataTable.Columns.Cast<DataColumn>()
                .FirstOrDefault(c => c.DataType == typeof(string))?.ColumnName ?? SearchableColumns.FirstOrDefault();
        }

        // --- IMPORT/EXPORT LOGIC ---

        // --- IMPORT LOGIC (FIXED) ---
        private async void ExecuteImportData(object parameter)
        {
            if (!CanExecuteImportData(parameter)) return;
            ImportSettings settings;
            if (parameter is null)
            {
                if (!_dialogService.ShowOpenFileDialog("Import Data File", "Excel/CSV|*.xlsx;*.csv|All|*.*", out string filePath)) return;
                settings = new ImportSettings { FilePath = filePath, RowsToIgnore = 0 };
            }
            else settings = parameter as ImportSettings;

            if (settings == null || string.IsNullOrEmpty(settings.FilePath)) return;

            await ExecuteLongRunningOperation(async () =>
            {
                var errors = new List<string>();
                DataTable importDt = null;

                if (Path.GetExtension(settings.FilePath).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                    importDt = await Task.Run(() => LoadXlsxToDataTable(settings.FilePath, errors, settings.RowsToIgnore));
                else
                    importDt = await Task.Run(() => LoadCsvToDataTable(settings.FilePath, errors, settings.RowsToIgnore));

                if (importDt == null) { errors.Add("Could not read file or file is empty."); }

                int imported = 0, skipped = 0;
                if (importDt != null && importDt.Rows.Count > 0)
                {
                    var targetCols = _currentDataTable.Columns.Cast<DataColumn>().ToList();

                    foreach (DataRow sRow in importDt.Rows)
                    {
                        var newRow = _currentDataTable.NewRow();
                        bool valid = true;

                        foreach (var tCol in targetCols)
                        {
                            // Skip ID (handled by DB)
                            if (tCol.ColumnName.Equals("ID", StringComparison.OrdinalIgnoreCase)) continue;

                            var sCol = importDt.Columns.Cast<DataColumn>().FirstOrDefault(c => c.ColumnName.Equals(tCol.ColumnName, StringComparison.OrdinalIgnoreCase));

                            if (sCol != null)
                            {
                                object val = sRow[sCol];
                                try
                                {
                                    // Handle DBNull
                                    if ((val == null || val == DBNull.Value || string.IsNullOrWhiteSpace(val.ToString())) && tCol.AllowDBNull)
                                        newRow[tCol] = DBNull.Value;
                                    else if ((val == null || val == DBNull.Value || string.IsNullOrWhiteSpace(val.ToString())) && !tCol.AllowDBNull)
                                        throw new FormatException("Cannot be null.");

                                    // --- FIX: Handle Excel Date (Double) ---
                                    else if (tCol.DataType == typeof(DateTime))
                                    {
                                        DateTime? dtVal = ParseDateSafe(val);
                                        if (dtVal.HasValue) newRow[tCol] = dtVal.Value;
                                        else throw new FormatException($"Invalid date: {val}");
                                    }
                                    // --- FIX: Handle GUID & Bool ---
                                    else if (tCol.DataType == typeof(Guid))
                                        newRow[tCol] = Guid.Parse(val.ToString());
                                    else if (tCol.DataType == typeof(bool))
                                        newRow[tCol] = ParseBooleanSafe(val.ToString());
                                    else
                                        newRow[tCol] = Convert.ChangeType(val, tCol.DataType, CultureInfo.CurrentCulture);
                                }
                                catch (Exception) { valid = false; errors.Add($"Row {imported + skipped + 1}: Type mismatch on '{tCol.ColumnName}' (Value: {val})."); break; }
                            }
                            else if (!tCol.AllowDBNull && tCol.DefaultValue == DBNull.Value)
                            {
                                valid = false; errors.Add($"Row {imported + skipped + 1}: Missing required column '{tCol.ColumnName}'."); break;
                            }
                        }
                        if (valid)
                        {
                            if (Application.Current != null) await Application.Current.Dispatcher.InvokeAsync(() => _currentDataTable.Rows.Add(newRow));
                            imported++;
                        }
                        else skipped++;
                    }
                }
                else if (!errors.Any()) { errors.Add("No data found in file to import."); }

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SetErrorMessage($"Import complete. Added: {imported}, Skipped: {skipped}. " + (errors.Any() ? $"First error: {errors.First()}" : ""));
                    CheckIfDirty();
                });
            });
        }

        private DateTime? ParseDateSafe(object value)
        {
            if (value == null || value == DBNull.Value) return null;

            if (value is DateTime dt) return dt;
            if (value is double dVal) return DateTime.FromOADate(dVal);

            string sVal = value.ToString();
            if (string.IsNullOrWhiteSpace(sVal)) return null;

            if (DateTime.TryParse(sVal, CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime result))
                return result;

            if (DateTime.TryParse(sVal, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
                return result;

            // Try common formats
            string[] formats = { "dd.MM.yyyy", "dd/MM/yyyy", "MM/dd/yyyy", "yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss" };
            if (DateTime.TryParseExact(sVal, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
                return result;

            return null;
        }

        private async void ExecuteExportData(object p)
        {
            if (!CanExecuteExportData(p)) return;
            if (_dialogService.ShowSaveFileDialog("Export Data", $"{SelectedTable}_Export_{DateTime.Now:yyyyMMdd}", ".xlsx", "Excel (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv", out string path))
            {
                await ExecuteLongRunningOperation(async () =>
                {
                    var rowsToExport = _currentDataTable.AsEnumerable().Where(r => r.RowState != DataRowState.Deleted);
                    if (Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                    {
                        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                        using (var package = new ExcelPackage(new FileInfo(path)))
                        {
                            var ws = package.Workbook.Worksheets.Add(SanitizeSheetName(SelectedTable));
                            if (rowsToExport.Any())
                            {
                                ws.Cells["A1"].LoadFromDataTable(rowsToExport.CopyToDataTable(), true);
                                ws.Cells[ws.Dimension.Address].AutoFitColumns();
                            }
                            await package.SaveAsync();
                        }
                    }
                    else
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine("sep=,");
                        sb.AppendLine(string.Join(",", _currentDataTable.Columns.Cast<DataColumn>().Select(c => QuoteValueIfNeeded(c.ColumnName))));
                        foreach (DataRow r in rowsToExport)
                        {
                            sb.AppendLine(string.Join(",", r.ItemArray.Select(f => QuoteValueIfNeeded(f?.ToString()))));
                        }
                        await Task.Run(() => File.WriteAllText(path, sb.ToString(), Encoding.UTF8));
                    }
                });
            }
        }

        // --- Other Commands ---

        private void ExecuteAddNewRow(object parameter)
        {
            if (!CanExecuteAddNewRow(parameter)) return;
            SetErrorMessage(null);
            NewRowData newRowData;
            bool result;

            if (SelectedTable.StartsWith("_Long_", StringComparison.OrdinalIgnoreCase))
            {
                var vm = new AddRowLongViewModel(SelectedTable, _dataRepository, _logger, _dialogService);
                result = _dialogService.ShowAddRowLongDialog(vm, out newRowData);
            }
            else
            {
                var cols = _currentDataTable.Columns.Cast<DataColumn>().Where(c => !c.AutoIncrement && !c.ReadOnly).Select(c => c.ColumnName).ToList();
                result = _dialogService.ShowAddRowDialog(cols, SelectedTable, null, _currentDataTable, IsIdHidden, out newRowData);
            }

            if (result && newRowData != null)
            {
                try
                {
                    var row = _currentDataTable.NewRow();

                    // 1. Populate fields from User Input
                    foreach (var kvp in newRowData.Values)
                    {
                        if (_currentDataTable.Columns.Contains(kvp.Key))
                        {
                            var col = _currentDataTable.Columns[kvp.Key];
                            if (col.ReadOnly || col.AutoIncrement) continue;

                            if (kvp.Value == null || string.IsNullOrWhiteSpace(kvp.Value.ToString()))
                            {
                                if (col.AllowDBNull) row[kvp.Key] = DBNull.Value;
                                else throw new InvalidOperationException($"Column '{col.ColumnName}' cannot be null.");
                            }
                            else
                            {
                                if (col.DataType == typeof(Guid)) row[kvp.Key] = Guid.Parse(kvp.Value.ToString());
                                else if (col.DataType == typeof(bool)) row[kvp.Key] = ParseBooleanSafe(kvp.Value.ToString());
                                else row[kvp.Key] = Convert.ChangeType(kvp.Value, col.DataType, CultureInfo.CurrentCulture);
                            }
                        }
                    }

                    // 2. FIX: Handle Missing ID (Constraint Violation Fix)
                    // If ID is null (because it was hidden/auto), we must provide a TEMP value to satisfy DataTable constraints.
                    // The Repository's Insert Logic ignores this value and lets the DB generate the real ID.
                    var idCol = _currentDataTable.Columns.Cast<DataColumn>()
                        .FirstOrDefault(c => c.ColumnName.Equals("ID", StringComparison.OrdinalIgnoreCase));

                    if (idCol != null && (row[idCol] == DBNull.Value || row[idCol] == null) && !idCol.AutoIncrement)
                    {
                        if (idCol.DataType == typeof(int) || idCol.DataType == typeof(long))
                        {
                            // Calculate a unique temporary negative ID (e.g. -1, -2) to avoid collisions in memory
                            long minId = 0;
                            try
                            {
                                var existingIds = _currentDataTable.AsEnumerable()
                                    .Select(r => r.RowState == DataRowState.Deleted ? 0 : Convert.ToInt64(r[idCol]));
                                if (existingIds.Any()) minId = existingIds.Min();
                            }
                            catch { }

                            // If minId is positive, start at -1. If negative, go lower.
                            row[idCol] = (minId > 0) ? -1 : minId - 1;
                        }
                        else if (idCol.DataType == typeof(Guid))
                        {
                            row[idCol] = Guid.NewGuid();
                        }
                    }

                    // 3. Add to Grid
                    _currentDataTable.Rows.Add(row);
                    CheckIfDirty();
                }
                catch (Exception ex)
                {
                    SetErrorMessage($"Error adding row: {ex.Message}");
                }
            }
        }

        private void ExecuteDeleteSelectedRow(object p)
        {
            if (!CanExecuteDeleteSelectedRow(p)) return;
            var items = ((IList)p).OfType<DataRowView>().ToList();
            if (!items.Any()) return;

            if (_dialogService.ShowConfirmationDialog(Resources.Delete, $"Delete {items.Count} row(s)?"))
            {
                foreach (var item in items)
                {
                    if (EditableRows.Contains(item)) EditableRows.Remove(item);
                    item.Row.Delete();
                }
                CheckIfDirty();
            }
        }

        private async void ExecuteDeleteTableCommand(object p)
        {
            if (!CanExecuteDeleteTableCommand(p)) return;
            if (_dialogService.ShowConfirmationDialog("Delete Table", $"PERMANENTLY delete '{SelectedTable}'?"))
            {
                bool success = false;
                await ExecuteLongRunningOperation(async () => success = await _dataRepository.DeleteTableAsync(SelectedTable));
                if (success) LoadInitialDataAsync();
                else SetErrorMessage("Failed to delete table.");
            }
        }

        private void ExecuteEditSelectedRows(object p)
        {
            if (!CanExecuteEditSelectedRows(p)) return;
            EditableRows.Clear();
            foreach (var i in (IList)p) if (i is DataRowView drv) EditableRows.Add(drv);
            OnPropertyChanged(nameof(EditableRows));
        }

        private void ExecuteReloadData(object p)
        {
            if (!CanExecuteReloadData(p)) return;
            if (IsDirty && !_dialogService.ShowConfirmationDialog("Discard Changes?", "Reloading will discard unsaved changes. Proceed?")) return;
            LoadDataForSelectedTableAsync();
        }

        private void ExecuteUndoChanges(object p)
        {
            if (!CanExecuteUndoChanges(p)) return;
            var last = _rowChangeHistory.LastOrDefault();
            if (last != null) { last.RejectChanges(); _rowChangeHistory.Remove(last); }
            CheckIfDirty();
            (SaveChangesCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
        }

        private async void ExecuteAddIdColumn(object p)
        {
            if (!CanExecuteAddIdColumn(p)) return;
            if (!_dialogService.ShowConfirmationDialog("Add ID", "Add auto-incrementing ID?")) return;
            await ExecuteLongRunningOperation(async () =>
            {
                var res = await _dataRepository.AddPrimaryKeyAsync(SelectedTable);
                if (res.Success) LoadDataForSelectedTableAsync();
                else SetErrorMessage(res.ErrorMessage);
            });
        }

        private void ExecuteShowHierarchyImport(object p)
        {
            var vm = new HierarchyImportViewModel(_dataRepository, _dialogService, _logger);
            vm.SelectedTableName = SelectedTable;
            _dialogService.ShowHierarchyImportDialog(vm);
        }

        private void ExecuteShowCreateTableCommand(object p)
        {
            var createTableVM = new CreateTableViewModel(_dialogService, _logger, _dataRepository);
            _dialogService.ShowCreateTableDialog(createTableVM);
            LoadInitialDataAsync();
        }

        private void ExecuteShowAdvancedImport(object p)
        {
            if (!CanExecuteImportData(p)) return;
            var vm = new ImportTableViewModel(SelectedTable, _dialogService);
            if (_dialogService.ShowImportTableDialog(vm, out ImportSettings s)) ExecuteImportData(s);
        }

        // --- Helpers (Verbose & Robust) ---

        private DataTable LoadXlsxToDataTable(string path, List<string> errors, int rowsToIgnore)
        {
            var dt = new DataTable();
            try
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                var fileInfo = new FileInfo(path);
                using (var package = new ExcelPackage(fileInfo))
                {
                    var worksheet = package.Workbook.Worksheets.FirstOrDefault();
                    if (worksheet == null || worksheet.Dimension == null)
                    {
                        errors.Add("Excel file is empty.");
                        return null;
                    }

                    int headerRow = 1 + rowsToIgnore;
                    if (headerRow > worksheet.Dimension.End.Row)
                    {
                        errors.Add("Header row index exceeds total rows.");
                        return null;
                    }

                    // Create Columns from Header
                    foreach (var firstRowCell in worksheet.Cells[headerRow, 1, headerRow, worksheet.Dimension.End.Column])
                    {
                        string columnName = firstRowCell.Text.Trim();
                        if (string.IsNullOrEmpty(columnName)) columnName = $"Column_{firstRowCell.Start.Column}";

                        // Deduplicate column names
                        int dupCount = 1;
                        string tempName = columnName;
                        while (dt.Columns.Contains(tempName)) tempName = $"{columnName}_{dupCount++}";

                        dt.Columns.Add(tempName);
                    }

                    // Load Data Rows
                    for (int rowNum = headerRow + 1; rowNum <= worksheet.Dimension.End.Row; rowNum++)
                    {
                        var wsRow = worksheet.Cells[rowNum, 1, rowNum, dt.Columns.Count];
                        DataRow row = dt.Rows.Add();
                        bool hasValues = false;
                        foreach (var cell in wsRow)
                        {
                            // Safety check for column index
                            if (cell.Start.Column - 1 < dt.Columns.Count)
                            {
                                row[cell.Start.Column - 1] = cell.Value ?? DBNull.Value;
                                if (cell.Value != null && !string.IsNullOrWhiteSpace(cell.Value.ToString())) hasValues = true;
                            }
                        }
                        if (!hasValues) { dt.Rows.Remove(row); }
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Error reading XLSX: {ex.Message}");
                _logger.LogError($"XLSX Import Error", ex);
                return null;
            }
            return dt;
        }

        private DataTable LoadCsvToDataTable(string path, List<string> errors, int rowsToIgnore)
        {
            var dt = new DataTable();
            try
            {
                var lines = File.ReadAllLines(path, Encoding.UTF8).Skip(rowsToIgnore).ToList();
                if (!lines.Any()) return dt;

                // Fix: Regex for splitting on comma (ignoring quotes)
                string pattern = ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)";

                // Parse Headers
                var headers = Regex.Split(lines[0], pattern);
                foreach (var h in headers)
                {
                    string colName = h.Trim().Trim('"');
                    int dupCount = 1;
                    string tempName = colName;
                    while (dt.Columns.Contains(tempName)) tempName = $"{colName}_{dupCount++}";
                    dt.Columns.Add(tempName);
                }

                // Parse Rows
                foreach (var line in lines.Skip(1))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var values = Regex.Split(line, pattern);

                    var cleanValues = new object[dt.Columns.Count];
                    for (int i = 0; i < Math.Min(values.Length, dt.Columns.Count); i++)
                    {
                        cleanValues[i] = values[i].Trim().Trim('"');
                    }
                    dt.Rows.Add(cleanValues);
                }
                return dt;
            }
            catch (Exception ex) { errors.Add(ex.Message); return null; }
        }

        private bool ParseBooleanSafe(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return false;
            val = val.Trim().ToLowerInvariant();
            return val == "1" || val == "true" || val == "yes" || val == "y" || val == "on";
        }

        private string QuoteValueIfNeeded(string v)
        {
            if (string.IsNullOrEmpty(v)) return "";
            if (v.Contains(",") || v.Contains("\"") || v.Contains("\n"))
            {
                return $"\"{v.Replace("\"", "\"\"")}\"";
            }
            return v;
        }

        private string SanitizeSheetName(string n)
        {
            if (string.IsNullOrEmpty(n)) return "Sheet1";
            var s = System.Text.RegularExpressions.Regex.Replace(n, @"[\\/\?\*\[\]:]", "_");
            return s.Length > 31 ? s.Substring(0, 31) : s;
        }

        private string GetFriendlyErrorMessage(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            if (raw.Contains("REFERENCE")) return "Record is in use elsewhere.";
            if (raw.Contains("UNIQUE") || raw.Contains("duplicate")) return "Duplicate record found.";
            return raw;
        }

        private void SetErrorMessage(string msg)
        {
            ErrorMessage = msg;
        }

        private void UpdateFilterStatus()
        {
            if (DataTableView == null) return;
            FilterStatus = DataTableView.Count == _currentDataTable.Rows.Count ? string.Empty : $"Filtered: {DataTableView.Count} / {_currentDataTable.Rows.Count}";
        }

        private void CheckIfDirty()
        {
            IsDirty = _currentDataTable?.GetChanges() != null;
            (SaveChangesCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
        }
    }
}