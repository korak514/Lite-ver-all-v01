// ViewModels/DatarepViewModel.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WPF_LoginForm.Models;
using WPF_LoginForm.Properties;
using WPF_LoginForm.Repositories;
using WPF_LoginForm.Services;
using WPF_LoginForm.Services.Database;
using WPF_LoginForm.Views;

namespace WPF_LoginForm.ViewModels
{
    public class DatarepViewModel : ViewModelBase
    {
        private readonly IDialogService _dialogService;
        private readonly IDataRepository _dataRepository;
        private readonly ILogger _logger;
        private CancellationTokenSource _cts;

        // --- State ---
        private DataView _dataTableView;

        private string _selectedTable;
        private bool _isBusy;
        private string _errorMessage;
        private bool _isDirty;
        private DataTable _currentDataTable;
        private double _dataGridFontSize = 12;
        private int _longRunningOperationCount = 0;
        private bool _isIdHidden = true;
        private string _dateFilterColumnName;
        private readonly List<string> _dateColumnAliases = new List<string> { "Tarih", "Date", "EntryDate" };
        private readonly List<DataRow> _rowChangeHistory = new List<DataRow>();

        // --- Filter State ---
        private string _searchText;

        private string _selectedSearchColumn;
        private bool _isGlobalSearchActive;
        private bool _isUpdatingDates;
        private DateTime _minSliderDate;

        // --- Collections ---
        public ObservableCollection<string> TableNames { get; } = new ObservableCollection<string>();

        public ObservableCollection<DataRowView> EditableRows { get; } = new ObservableCollection<DataRowView>();
        public ObservableCollection<string> SearchableColumns { get; } = new ObservableCollection<string>();

        // --- Properties ---
        public bool IsAdmin => UserSessionService.IsAdmin;

        public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
        public bool IsProgressBarVisible => _isBusy;

        public string ErrorMessage
        {
            get => _errorMessage;
            private set { if (SetProperty(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); }
        }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        public bool IsDirty
        {
            get => _isDirty;
            private set { if (SetProperty(ref _isDirty, value)) (SaveChangesCommand as ViewModelCommand)?.RaiseCanExecuteChanged(); }
        }

        public string SelectedTable
        {
            get => _selectedTable;
            set
            {
                if (SetProperty(ref _selectedTable, value))
                {
                    _cts?.Cancel();
                    EditableRows.Clear();
                    UnsubscribeFromTableEvents();
                    LoadDataForSelectedTableAsync();
                }
            }
        }

        public bool LoadAllData { get; set; } = false;

        public DataView DataTableView
        {
            get => _dataTableView;
            private set
            {
                if (SetProperty(ref _dataTableView, value))
                {
                    _currentDataTable = _dataTableView?.Table;
                    SubscribeToTableEvents();
                    IsDirty = false;
                    PopulateSearchableColumns();
                }
            }
        }

        // --- Filter Properties ---
        public string SearchText
        { get => _searchText; set { if (SetProperty(ref _searchText, value)) ApplyCombinedFiltersAsync(); } }

        public string SelectedSearchColumn
        { get => _selectedSearchColumn; set { if (SetProperty(ref _selectedSearchColumn, value)) { _isGlobalSearchActive = false; ApplyCombinedFiltersAsync(); } } }

        public bool IsGlobalSearchActive
        { get => _isGlobalSearchActive; set { if (SetProperty(ref _isGlobalSearchActive, value)) ApplyCombinedFiltersAsync(); } }

        // --- Date Filter Properties ---
        private bool _isDateFilterVisible;

        private DateTime? _filterStartDate, _filterEndDate;
        private double _sliderMax, _sliderStart, _sliderEnd;

        public bool IsDateFilterVisible { get => _isDateFilterVisible; private set => SetProperty(ref _isDateFilterVisible, value); }
        public bool IsDateFilterPanelVisible { get; set; }

        public DateTime? FilterStartDate
        { get => _filterStartDate; set { if (SetProperty(ref _filterStartDate, value)) { ApplyCombinedFiltersAsync(); UpdateSlidersFromDates(); } } }

        public DateTime? FilterEndDate
        { get => _filterEndDate; set { if (SetProperty(ref _filterEndDate, value)) { ApplyCombinedFiltersAsync(); UpdateSlidersFromDates(); } } }

        public double SliderMaximum { get => _sliderMax; set => SetProperty(ref _sliderMax, value); }

        public double StartMonthSliderValue
        { get => _sliderStart; set { if (SetProperty(ref _sliderStart, value)) UpdateDatesFromSliders(); } }

        public double EndMonthSliderValue
        { get => _sliderEnd; set { if (SetProperty(ref _sliderEnd, value)) UpdateDatesFromSliders(); } }

        // --- Config Properties ---
        public bool IsIdHidden
        { get => _isIdHidden; set { if (SetProperty(ref _isIdHidden, value)) OnPropertyChanged(nameof(IsIdVisible)); } }

        public bool IsIdVisible => !_isIdHidden;
        public bool IsAdvancedImportVisible { get; set; }

        public double DataGridFontSize
        {
            get => _dataGridFontSize;
            set { if (SetProperty(ref _dataGridFontSize, Math.Max(8, Math.Min(24, value)))) { (DecreaseFontSizeCommand as ViewModelCommand)?.RaiseCanExecuteChanged(); (IncreaseFontSizeCommand as ViewModelCommand)?.RaiseCanExecuteChanged(); } }
        }

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
        public ICommand ClearSearchCommand { get; }
        public ICommand ClearDateFilterCommand { get; }
        public ICommand AddIdColumnCommand { get; }
        public ICommand ShowHierarchyImportCommand { get; }
        public ICommand RenameColumnCommand { get; }
        public ICommand ShowFindReplaceCommand { get; }
        public ICommand DecreaseFontSizeCommand { get; }
        public ICommand IncreaseFontSizeCommand { get; }

        public DatarepViewModel(ILogger logger, IDialogService dialogService, IDataRepository dataRepository)
        {
            _logger = logger;
            _dialogService = dialogService;
            _dataRepository = dataRepository;

            AddNewRowCommand = new ViewModelCommand(ExecuteAddNewRow, p => _currentDataTable != null && !IsBusy);
            SaveChangesCommand = new ViewModelCommand(ExecuteSaveChanges, p => IsDirty && !IsBusy);
            UndoChangesCommand = new ViewModelCommand(ExecuteUndo, p => _rowChangeHistory.Any() && !IsBusy);

            // DIAGNOSTIC CHANGE: Remove CanExecute check to debug binding
            EditSelectedRowsCommand = new ViewModelCommand(ExecuteEditRows);

            ReloadDataCommand = new ViewModelCommand(p => LoadDataForSelectedTableAsync(), p => !string.IsNullOrEmpty(SelectedTable) && !IsBusy);
            DeleteSelectedRowCommand = new ViewModelCommand(ExecuteDeleteRow, p => p is IList i && i.Count > 0 && !IsBusy);
            DeleteTableCommand = new ViewModelCommand(ExecuteDeleteTable, p => !string.IsNullOrEmpty(SelectedTable) && IsAdmin && !IsBusy);

            ImportDataCommand = new ViewModelCommand(ExecuteImportData, p => _currentDataTable != null && !IsBusy);
            ShowAdvancedImportCommand = new ViewModelCommand(p => { var vm = new ImportTableViewModel(SelectedTable, _dialogService); if (_dialogService.ShowImportTableDialog(vm, out ImportSettings s)) ExecuteImportData(s); }, p => _currentDataTable != null && !IsBusy);
            ExportDataCommand = new ViewModelCommand(ExecuteExportData, p => _currentDataTable?.Rows.Count > 0 && !IsBusy);

            AddIdColumnCommand = new ViewModelCommand(async p => await ExecuteLongRunning(async t => { var r = await _dataRepository.AddPrimaryKeyAsync(SelectedTable); if (r.Success) LoadDataForSelectedTableAsync(); }), p => _currentDataTable != null && !_currentDataTable.Columns.Contains("ID"));
            ShowCreateTableCommand = new ViewModelCommand(p => { _dialogService.ShowCreateTableDialog(new CreateTableViewModel(_dialogService, _logger, _dataRepository)); LoadInitialDataAsync(); });
            RenameColumnCommand = new ViewModelCommand(ExecuteRenameColumn, p => !IsBusy && IsAdmin && !string.IsNullOrEmpty(SelectedSearchColumn));
            ShowFindReplaceCommand = new ViewModelCommand(ExecuteShowFindReplace, p => _currentDataTable != null && !IsBusy);
            ShowHierarchyImportCommand = new ViewModelCommand(p => _dialogService.ShowHierarchyImportDialog(new HierarchyImportViewModel(_dataRepository, _dialogService, _logger) { SelectedTableName = SelectedTable }));

            DecreaseFontSizeCommand = new ViewModelCommand(p => DataGridFontSize--, p => DataGridFontSize > 8);
            IncreaseFontSizeCommand = new ViewModelCommand(p => DataGridFontSize++, p => DataGridFontSize < 24);

            ClearSearchCommand = new ViewModelCommand(p => { SearchText = ""; IsGlobalSearchActive = false; });
            ClearDateFilterCommand = new ViewModelCommand(p => { FilterStartDate = null; FilterEndDate = null; IsDateFilterPanelVisible = false; ApplyCombinedFiltersAsync(); });

            DatabaseRetryPolicy.OnRetryStatus += msg => Application.Current?.Dispatcher.Invoke(() => SetErrorMessage(msg));
            LoadInitialDataAsync();
        }

        private void ExecuteEditRows(object parameter)
        {
            // DIAGNOSTIC 1: Check if command fires at all
            // MessageBox.Show($"ExecuteEditRows Fired. Parameter Type: {parameter?.GetType().Name ?? "NULL"}");

            if (parameter == null)
            {
                MessageBox.Show("Error: Edit parameter is NULL. \nCheck XAML CommandParameter binding.");
                return;
            }

            if (parameter is IList items)
            {
                if (items.Count == 0)
                {
                    MessageBox.Show("Warning: No rows selected.");
                    return;
                }

                EditableRows.Clear();
                int successCount = 0;

                foreach (var item in items)
                {
                    if (item is DataRowView drv)
                    {
                        EditableRows.Add(drv);
                        successCount++;
                    }
                    else
                    {
                        MessageBox.Show($"Error: Selected item is not DataRowView. It is: {item.GetType().Name}");
                    }
                }

                // If we get here, the rows are in the list.
                // The binding on IsReadOnly should now update to False.
                // MessageBox.Show($"Success: {successCount} rows unlocked for editing.");
            }
            else
            {
                MessageBox.Show($"Error: Parameter is not IList. It is {parameter.GetType().Name}");
            }
        }

        private void ExecuteUndo(object parameter)
        {
            var lastRow = _rowChangeHistory.LastOrDefault();
            if (lastRow != null)
            {
                lastRow.RejectChanges();
                _rowChangeHistory.Remove(lastRow);
                CheckIfDirty();
            }
        }

        private async Task ExecuteLongRunning(Func<CancellationToken, Task> op)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            Interlocked.Increment(ref _longRunningOperationCount);
            IsBusy = true;
            SetErrorMessage(null);

            try
            {
                await op(token);
            }
            catch (Exception ex)
            {
                _logger.LogError("[Op]", ex);
                SetErrorMessage($"Error: {ex.Message}");
            }
            finally
            {
                if (Interlocked.Decrement(ref _longRunningOperationCount) == 0)
                    IsBusy = false;

                CommandManager.InvalidateRequerySuggested();
            }
        }

        private async void LoadInitialDataAsync() => await ExecuteLongRunning(async t =>
        {
            var names = await _dataRepository.GetTableNamesAsync();
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                TableNames.Clear();
                foreach (var n in names) TableNames.Add(n);
                if (!TableNames.Contains(SelectedTable)) SelectedTable = TableNames.FirstOrDefault();
            });
        });

        private async void LoadDataForSelectedTableAsync()
        {
            if (string.IsNullOrEmpty(SelectedTable)) return;
            string target = SelectedTable;

            await ExecuteLongRunning(async token =>
            {
                var res = await _dataRepository.GetTableDataAsync(target, LoadAllData ? 0 : Settings.Default.DefaultRowLimit);
                if (token.IsCancellationRequested) return;

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (SelectedTable != target) return;

                    if (res.Data != null && res.Data.Columns.Contains("ID"))
                        res.Data.PrimaryKey = new[] { res.Data.Columns["ID"] };

                    DataTableView = res.Data?.DefaultView;

                    if (!res.IsSortable) SetErrorMessage("⚠️ No ID/Date column. Editing limited.");

                    SetupDateFilter();
                    ApplyCombinedFiltersAsync();
                });
            });
        }

        private async void ApplyCombinedFiltersAsync()
        {
            if (DataTableView == null) return;
            string txt = SearchText, col = SelectedSearchColumn, dateCol = _dateFilterColumnName;
            bool global = IsGlobalSearchActive, hasDate = IsDateFilterVisible && _filterStartDate.HasValue && _filterEndDate.HasValue;
            DateTime? s = _filterStartDate, e = _filterEndDate;

            await Task.Run(() =>
            {
                string filter = DataImportExportHelper.BuildFilterString(_currentDataTable, txt, col, global, hasDate, dateCol, s, e);
                Application.Current.Dispatcher.Invoke(() => { try { DataTableView.RowFilter = filter; } catch { } });
            });
        }

        private async void ExecuteSaveChanges(object p)
        {
            var changes = _currentDataTable?.GetChanges();
            if (changes == null) return;
            if (!_currentDataTable.Columns.Contains("ID")) { MessageBox.Show("Table needs ID column.", "Error"); return; }
            if (!_dialogService.ShowConfirmationDialog(Resources.Save, $"Save {changes.Rows.Count} changes?")) return;

            bool newRows = changes.AsEnumerable().Any(r => r.RowState == DataRowState.Added);

            await ExecuteLongRunning(async t =>
            {
                var r = await _dataRepository.SaveChangesAsync(changes, SelectedTable);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (r.Success)
                    {
                        _currentDataTable.AcceptChanges();
                        EditableRows.Clear();
                        CheckIfDirty();
                        if (newRows) LoadDataForSelectedTableAsync();
                        SetErrorMessage("Changes Saved Successfully.");
                    }
                    else
                    {
                        SetErrorMessage(r.ErrorMessage);
                    }
                });
            });
        }

        private async void ExecuteImportData(object p)
        {
            ImportSettings s = p as ImportSettings;
            if (s == null && p == null)
            {
                if (_dialogService.ShowOpenFileDialog("Import", "Excel/CSV|*.xlsx;*.csv", out string f))
                    s = new ImportSettings { FilePath = f };
                else return;
            }
            if (s == null) return;

            await ExecuteLongRunning(async t =>
            {
                var res = DataImportExportHelper.ImportDataToTable(s.FilePath, _currentDataTable, s.RowsToIgnore);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SetErrorMessage(res.Message);
                    CheckIfDirty();
                });
            });
        }

        private async void ExecuteExportData(object p)
        {
            if (_dialogService.ShowSaveFileDialog("Export", $"{SelectedTable}_{DateTime.Now:yyyyMMdd}", ".xlsx", "Excel|*.xlsx|CSV|*.csv", out string path))
            {
                await ExecuteLongRunning(async t => await Task.Run(() => DataImportExportHelper.ExportTable(path, _currentDataTable, SelectedTable)));
            }
        }

        // --- Date Logic ---
        private void SetupDateFilter()
        {
            IsDateFilterVisible = false; _minSliderDate = default;
            var dc = _currentDataTable?.Columns.Cast<DataColumn>().FirstOrDefault(c => c.DataType == typeof(DateTime) && _dateColumnAliases.Contains(c.ColumnName));
            if (dc != null)
            {
                var dates = _currentDataTable.AsEnumerable().Where(r => r.RowState != DataRowState.Deleted && r[dc] != DBNull.Value).Select(r => (DateTime)r[dc]).ToList();
                if (dates.Any())
                {
                    _minSliderDate = dates.Min(); _dateFilterColumnName = dc.ColumnName;
                    SliderMaximum = ((dates.Max().Year - _minSliderDate.Year) * 12) + dates.Max().Month - _minSliderDate.Month;
                    IsDateFilterVisible = true;
                    FilterStartDate = dates.Min(); FilterEndDate = dates.Max();
                }
            }
        }

        private void UpdateSlidersFromDates()
        {
            if (!IsDateFilterVisible || _isUpdatingDates || _minSliderDate == default || FilterStartDate == null || FilterEndDate == null) return;
            _isUpdatingDates = true;
            try
            {
                StartMonthSliderValue = Math.Max(0, ((FilterStartDate.Value.Year - _minSliderDate.Year) * 12) + FilterStartDate.Value.Month - _minSliderDate.Month);
                EndMonthSliderValue = Math.Min(SliderMaximum, ((FilterEndDate.Value.Year - _minSliderDate.Year) * 12) + FilterEndDate.Value.Month - _minSliderDate.Month);
            }
            finally { _isUpdatingDates = false; }
        }

        private void UpdateDatesFromSliders()
        {
            if (!IsDateFilterVisible || _isUpdatingDates || _minSliderDate == default) return;
            _isUpdatingDates = true;
            try
            {
                if (StartMonthSliderValue > EndMonthSliderValue) StartMonthSliderValue = EndMonthSliderValue;
                FilterStartDate = _minSliderDate.AddMonths((int)StartMonthSliderValue);
                var endBase = _minSliderDate.AddMonths((int)EndMonthSliderValue);
                FilterEndDate = new DateTime(endBase.Year, endBase.Month, DateTime.DaysInMonth(endBase.Year, endBase.Month));
                ApplyCombinedFiltersAsync();
            }
            finally { _isUpdatingDates = false; }
        }

        // --- Helpers ---
        private void ExecuteAddNewRow(object p)
        {
            NewRowData data; bool ok;
            if (SelectedTable.StartsWith("_Long_", StringComparison.OrdinalIgnoreCase))
                ok = _dialogService.ShowAddRowLongDialog(new AddRowLongViewModel(SelectedTable, _dataRepository, _logger, _dialogService), out data);
            else
                ok = _dialogService.ShowAddRowDialog(_currentDataTable.Columns.Cast<DataColumn>().Where(c => !c.AutoIncrement && !c.ReadOnly).Select(c => c.ColumnName), SelectedTable, null, _currentDataTable, IsIdHidden, out data);

            if (ok && data != null)
            {
                try
                {
                    var r = _currentDataTable.NewRow();
                    foreach (var k in data.Values.Keys) if (_currentDataTable.Columns.Contains(k)) r[k] = data.Values[k] ?? DBNull.Value;
                    if (_currentDataTable.Columns.Contains("ID") && (r["ID"] == DBNull.Value || r["ID"] == null))
                    {
                        // Handle auto-increment gap or Guid
                        if (_currentDataTable.Columns["ID"].DataType == typeof(Guid)) r["ID"] = Guid.NewGuid();
                        else r["ID"] = -1; // Placeholder
                    }
                    _currentDataTable.Rows.Add(r);
                    CheckIfDirty();
                }
                catch (Exception ex) { SetErrorMessage(ex.Message); }
            }
        }

        private void ExecuteDeleteRow(object p)
        { if (_dialogService.ShowConfirmationDialog("Delete", "Delete selected rows?")) { foreach (var r in ((IList)p).OfType<DataRowView>().ToList()) { r.Row.Delete(); } CheckIfDirty(); } }

        private async void ExecuteDeleteTable(object p)
        { if (_dialogService.ShowConfirmationDialog("Delete", "Permanently Delete Table?")) await ExecuteLongRunning(async t => { if (await _dataRepository.DeleteTableAsync(SelectedTable)) LoadInitialDataAsync(); }); }

        private async void ExecuteRenameColumn(object p)
        { if (_dialogService.ShowInputDialog("Rename", $"Rename '{SelectedSearchColumn}' to:", SelectedSearchColumn, out string n)) await ExecuteLongRunning(async t => { var r = await _dataRepository.RenameColumnAsync(SelectedTable, SelectedSearchColumn, n); if (r.Success) LoadDataForSelectedTableAsync(); else SetErrorMessage(r.ErrorMessage); }); }

        private void ExecuteShowFindReplace(object p)
        { var w = new FindReplaceWindow(); w.FindRequested += (s, e) => { SearchText = w.FindText; IsGlobalSearchActive = true; }; if (Application.Current.MainWindow != null) w.Owner = Application.Current.MainWindow; w.Show(); }

        private void SubscribeToTableEvents()
        { if (_currentDataTable != null) { _currentDataTable.RowChanged += (s, e) => { if (e.Action != DataRowAction.Commit) { _rowChangeHistory.Add(e.Row); CheckIfDirty(); } }; } }

        private void UnsubscribeFromTableEvents()
        { if (_currentDataTable != null) _currentDataTable.RowChanged -= null; }

        private void CheckIfDirty() => IsDirty = _currentDataTable?.GetChanges() != null;

        private void SetErrorMessage(string m) => ErrorMessage = m;

        private void PopulateSearchableColumns()
        { SearchableColumns.Clear(); if (_currentDataTable != null) foreach (DataColumn c in _currentDataTable.Columns) SearchableColumns.Add(c.ColumnName); SelectedSearchColumn = SearchableColumns.FirstOrDefault(); }

        public async void LoadTableWithFilter(string t, DateTime s, DateTime e, string txt = "")
        { SelectedTable = t; await Task.Delay(100); if (DataTableView != null) { FilterStartDate = s; FilterEndDate = e; if (!string.IsNullOrEmpty(txt)) { IsGlobalSearchActive = true; SearchText = txt; } IsDateFilterVisible = true; ApplyCombinedFiltersAsync(); } }
    }
}