using WPF_LoginForm.Repositories;
using WPF_LoginForm.Services;
using WPF_LoginForm.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Data;
using Microsoft.Data.SqlClient;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Globalization;
using OfficeOpenXml;
using System.Windows;
using System.Threading;

namespace WPF_LoginForm.ViewModels
{
    public class DatarepViewModel : ViewModelBase
    {
        // --- Fields ---
        private readonly IDialogService _dialogService;
        private readonly IDataRepository _dataRepository;
        private readonly ILogger _logger;
        private DataView _dataTableView;
        private string _selectedTable;
        private ObservableCollection<string> _tableNames;
        private bool _isBusy;
        private bool _isProgressBarVisible;
        private string _errorMessage;
        private bool _isDirty;
        private DataTable _currentDataTable;
        private double _dataGridFontSize = 12;
        private ObservableCollection<DataRowView> _editableRows = new ObservableCollection<DataRowView>();
        private readonly List<DataRow> _rowChangeHistory = new List<DataRow>();
        private int _longRunningOperationCount = 0;

        private string _searchText;
        private bool _isColumnSelectorVisible;
        private string _selectedSearchColumn;
        private string _filterStatus;
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
        private readonly List<string> _dateColumnAliases = new List<string> { "Tarih", "Date", "EntryDate" };
        private readonly List<Type> _numericTypes = new List<Type> { typeof(int), typeof(double), typeof(decimal), typeof(float), typeof(long), typeof(short), typeof(byte), typeof(sbyte), typeof(uint), typeof(ulong), typeof(ushort) };


        // --- Properties ---
        public ObservableCollection<string> TableNames { get => _tableNames; private set => SetProperty(ref _tableNames, value); }
        public string SelectedTable { get => _selectedTable; set { if (_selectedTable != value) { IsDateFilterVisible = false; IsDateFilterPanelVisible = false; _dateFilterColumnName = null; UnsubscribeFromTableEvents(); _selectedTable = value; OnPropertyChanged(); (AddNewRowCommand as ViewModelCommand)?.RaiseCanExecuteChanged(); (ImportDataCommand as ViewModelCommand)?.RaiseCanExecuteChanged(); (ShowAdvancedImportCommand as ViewModelCommand)?.RaiseCanExecuteChanged(); (ExportDataCommand as ViewModelCommand)?.RaiseCanExecuteChanged(); (ReloadDataCommand as ViewModelCommand)?.RaiseCanExecuteChanged(); (UndoChangesCommand as ViewModelCommand)?.RaiseCanExecuteChanged(); (DeleteTableCommand as ViewModelCommand)?.RaiseCanExecuteChanged(); if (!string.IsNullOrEmpty(_selectedTable)) { LoadDataForSelectedTableAsync(); } else { DataTableView = null; SetErrorMessage(null); IsDirty = false; } } } }
        public DataView DataTableView { get => _dataTableView; private set { if (_dataTableView != value) { UnsubscribeFromTableEvents(); _dataTableView = value; _currentDataTable = _dataTableView?.Table; SubscribeToTableEvents(); IsDirty = false; EditableRows.Clear(); _rowChangeHistory.Clear(); ClearSearchCommand.Execute(null); PopulateSearchableColumns(); OnPropertyChanged(nameof(EditableRows)); OnPropertyChanged(); } } }
        public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
        public bool IsProgressBarVisible { get => _isProgressBarVisible; private set => SetProperty(ref _isProgressBarVisible, value); }
        public string ErrorMessage { get => _errorMessage; private set { if (SetProperty(ref _errorMessage, value)) { OnPropertyChanged(nameof(HasError)); } } }
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
        public bool IsDirty { get => _isDirty; private set { if (SetProperty(ref _isDirty, value)) { OnPropertyChanged(); } } }
        public double DataGridFontSize { get => _dataGridFontSize; set { var newSize = Math.Max(8, Math.Min(24, value)); if (SetProperty(ref _dataGridFontSize, newSize)) { (DecreaseFontSizeCommand as ViewModelCommand)?.RaiseCanExecuteChanged(); (IncreaseFontSizeCommand as ViewModelCommand)?.RaiseCanExecuteChanged(); } } }
        public ObservableCollection<DataRowView> EditableRows { get => _editableRows; }
        public ObservableCollection<string> SearchableColumns { get; } = new ObservableCollection<string>();
        public string SearchText { get => _searchText; set { if (SetProperty(ref _searchText, value)) ApplyCombinedFilters(); } }
        public bool IsColumnSelectorVisible { get => _isColumnSelectorVisible; set => SetProperty(ref _isColumnSelectorVisible, value); }
        public string SelectedSearchColumn { get => _selectedSearchColumn; set { if (SetProperty(ref _selectedSearchColumn, value)) ApplyCombinedFilters(); } }
        public string FilterStatus { get => _filterStatus; private set => SetProperty(ref _filterStatus, value); }
        public bool IsDateFilterVisible { get => _isDateFilterVisible; private set => SetProperty(ref _isDateFilterVisible, value); }
        public bool IsDateFilterPanelVisible { get => _isDateFilterPanelVisible; set => SetProperty(ref _isDateFilterPanelVisible, value); }
        public DateTime? FilterStartDate { get => _filterStartDate; set { if (SetProperty(ref _filterStartDate, value)) { if (!_isUpdatingDates) { ApplyCombinedFilters(); UpdateSlidersFromDates(); } } } }
        public DateTime? FilterEndDate { get => _filterEndDate; set { if (SetProperty(ref _filterEndDate, value)) { if (!_isUpdatingDates) { ApplyCombinedFilters(); UpdateSlidersFromDates(); } } } }
        public double SliderMaximum { get => _sliderMaximum; set => SetProperty(ref _sliderMaximum, value); }
        public double StartMonthSliderValue { get => _startMonthSliderValue; set { if (SetProperty(ref _startMonthSliderValue, value)) { if (!_isUpdatingDates) UpdateDatesFromSliders(); } } }
        public double EndMonthSliderValue { get => _endMonthSliderValue; set { if (SetProperty(ref _endMonthSliderValue, value)) { if (!_isUpdatingDates) UpdateDatesFromSliders(); } } }

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

        public DatarepViewModel(ILogger logger, IDialogService dialogService, IDataRepository dataRepository)
        {
            _logger = logger; _dialogService = dialogService; _dataRepository = dataRepository;
            TableNames = new ObservableCollection<string>();
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
            DecreaseFontSizeCommand = new ViewModelCommand(p => DataGridFontSize--, p => DataGridFontSize > 8);
            IncreaseFontSizeCommand = new ViewModelCommand(p => DataGridFontSize++, p => DataGridFontSize < 24);
            ClearSearchCommand = new ViewModelCommand(p => SearchText = string.Empty);
            ClearDateFilterCommand = new ViewModelCommand(p => { FilterStartDate = null; FilterEndDate = null; IsDateFilterPanelVisible = false; ApplyCombinedFilters(); });
            LoadInitialDataAsync();
        }

        private async void LoadDataForSelectedTableAsync() { if (string.IsNullOrEmpty(SelectedTable)) return; await ExecuteLongRunningOperation(async () => { DataTable dataTable = await _dataRepository.GetTableDataAsync(SelectedTable); await Application.Current.Dispatcher.InvokeAsync(() => { DataTableView = dataTable.DefaultView; SetupDateFilterForTable(); ApplyCombinedFilters(); }); }); }
        private void SetupDateFilterForTable() { IsDateFilterVisible = false; IsDateFilterPanelVisible = false; _dateFilterColumnName = null; _filterStartDate = null; _filterEndDate = null; OnPropertyChanged(nameof(FilterStartDate)); OnPropertyChanged(nameof(FilterEndDate)); if (_currentDataTable == null) return; var foundDateColumns = _currentDataTable.Columns.Cast<DataColumn>().Where(c => c.DataType == typeof(DateTime) && _dateColumnAliases.Contains(c.ColumnName, StringComparer.OrdinalIgnoreCase)).ToList(); if (foundDateColumns.Count > 1) { SetErrorMessage($"Ambiguous Date Columns in '{SelectedTable}'."); return; } if (foundDateColumns.Count == 0) return; var dateColumn = foundDateColumns.Single(); var dates = _currentDataTable.AsEnumerable().Select(r => r.Field<DateTime?>(dateColumn)).Where(d => d.HasValue).Select(d => d.Value).ToList(); if (!dates.Any()) return; DateTime minDate = dates.Min(); DateTime maxDate = dates.Max(); _minSliderDate = minDate; _dateFilterColumnName = dateColumn.ColumnName; SliderMaximum = ((maxDate.Year - minDate.Year) * 12) + maxDate.Month - minDate.Month; _isUpdatingDates = true; StartMonthSliderValue = 0; EndMonthSliderValue = SliderMaximum; _isUpdatingDates = false; UpdateDatesFromSliders(); IsDateFilterVisible = true; }
        private void UpdateSlidersFromDates() { if (!IsDateFilterVisible || !FilterStartDate.HasValue || !FilterEndDate.HasValue || _isUpdatingDates) return; _isUpdatingDates = true; StartMonthSliderValue = ((FilterStartDate.Value.Year - _minSliderDate.Year) * 12) + FilterStartDate.Value.Month - _minSliderDate.Month; EndMonthSliderValue = ((FilterEndDate.Value.Year - _minSliderDate.Year) * 12) + FilterEndDate.Value.Month - _minSliderDate.Month; _isUpdatingDates = false; }
        private void UpdateDatesFromSliders() { if (!IsDateFilterVisible || _isUpdatingDates) return; if (StartMonthSliderValue > EndMonthSliderValue) { StartMonthSliderValue = EndMonthSliderValue; } _isUpdatingDates = true; var newStartDate = _minSliderDate.AddMonths((int)StartMonthSliderValue); var newEndDate = _minSliderDate.AddMonths((int)EndMonthSliderValue); _filterStartDate = new DateTime(newStartDate.Year, newStartDate.Month, 1); _filterEndDate = new DateTime(newEndDate.Year, newEndDate.Month, DateTime.DaysInMonth(newEndDate.Year, newEndDate.Month)); OnPropertyChanged(nameof(FilterStartDate)); OnPropertyChanged(nameof(FilterEndDate)); _isUpdatingDates = false; ApplyCombinedFilters(); }
        private void PopulateSearchableColumns() { SearchableColumns.Clear(); if (_currentDataTable == null) return; foreach (DataColumn col in _currentDataTable.Columns) SearchableColumns.Add(col.ColumnName); SelectedSearchColumn = _currentDataTable.Columns.Cast<DataColumn>().FirstOrDefault(c => c.DataType == typeof(string))?.ColumnName ?? SearchableColumns.FirstOrDefault(); }
        private void ApplyCombinedFilters() { if (DataTableView == null) return; var filters = new List<string>(); if (!string.IsNullOrWhiteSpace(SearchText) && !string.IsNullOrEmpty(SelectedSearchColumn)) { try { string sanitizedSearchText = SearchText.Replace("'", "''"); string textFilter = string.Empty; if (SearchText.Trim().StartsWith(">") || SearchText.Trim().StartsWith("<")) { DataColumn column = _currentDataTable.Columns[SelectedSearchColumn]; string numberPart = SearchText.Trim().Substring(1); if (_numericTypes.Contains(column.DataType) && double.TryParse(numberPart, NumberStyles.Any, CultureInfo.InvariantCulture, out double numValue)) { textFilter = $"[{SelectedSearchColumn}] {SearchText.Trim().First()} {numValue.ToString(CultureInfo.InvariantCulture)}"; } } if (string.IsNullOrEmpty(textFilter)) { textFilter = $"CONVERT([{SelectedSearchColumn}], 'System.String') LIKE '%{sanitizedSearchText}%'"; } filters.Add(textFilter); } catch (Exception ex) { _logger.LogError($"Could not apply text filter.", ex); } } if (IsDateFilterVisible && !string.IsNullOrEmpty(_dateFilterColumnName) && FilterStartDate.HasValue && FilterEndDate.HasValue) { filters.Add($"[{_dateFilterColumnName}] >= #{FilterStartDate.Value:yyyy-MM-dd}#"); filters.Add($"[{_dateFilterColumnName}] <= #{FilterEndDate.Value:yyyy-MM-dd}#"); } try { DataTableView.RowFilter = string.Join(" AND ", filters); } catch (Exception ex) { SetErrorMessage($"Invalid filter: {ex.Message}"); } UpdateFilterStatus(); }
        private void UpdateFilterStatus() { if (DataTableView == null) { FilterStatus = string.Empty; return; } var total = DataTableView.Table.Rows.Count; var visible = DataTableView.Count; FilterStatus = total == visible ? string.Empty : $"Filtered: Showing {visible} of {total} rows"; }
        public void SetErrorMessage(string message) { ErrorMessage = message; }
        private async Task ExecuteLongRunningOperation(Func<Task> operation) { Interlocked.Increment(ref _longRunningOperationCount); IsBusy = true; SetErrorMessage(null); IsProgressBarVisible = false; var progressTask = Task.Run(async () => { await Task.Delay(2000); if (IsBusy) { await Application.Current.Dispatcher.InvokeAsync(() => IsProgressBarVisible = true); } }); try { await operation(); } catch (Exception ex) { _logger.LogError("[LongOp] Exception.", ex); SetErrorMessage($"An error occurred: {ex.Message}"); } finally { if (Interlocked.Decrement(ref _longRunningOperationCount) == 0) { IsBusy = false; IsProgressBarVisible = false; } } }
        private void SubscribeToTableEvents() { if (_currentDataTable != null) { _currentDataTable.RowChanged += OnDataTableRowChanged; _currentDataTable.RowDeleted += OnDataTableRowChanged; _currentDataTable.TableNewRow += OnDataTableNewRow; } }
        private void UnsubscribeFromTableEvents() { if (_currentDataTable != null) { _currentDataTable.RowChanged -= OnDataTableRowChanged; _currentDataTable.RowDeleted -= OnDataTableRowChanged; _currentDataTable.TableNewRow -= OnDataTableNewRow; } }
        private void OnDataTableRowChanged(object sender, DataRowChangeEventArgs e) { _logger.LogInfo($"RowChanged: Action={e.Action}, RowState={e.Row.RowState}"); if (e.Action == DataRowAction.Add || e.Action == DataRowAction.Change || e.Action == DataRowAction.Delete) { if (_rowChangeHistory.Contains(e.Row)) { _rowChangeHistory.Remove(e.Row); } _rowChangeHistory.Add(e.Row); } CheckIfDirty(); (UndoChangesCommand as ViewModelCommand)?.RaiseCanExecuteChanged(); }
        private void OnDataTableNewRow(object sender, DataTableNewRowEventArgs e) { _logger.LogInfo($"TableNewRow fired."); }
        private void CheckIfDirty() { IsDirty = _currentDataTable?.GetChanges() != null; }
        private async void LoadInitialDataAsync() { await ExecuteLongRunningOperation(async () => { var names = await _dataRepository.GetTableNamesAsync(); await Application.Current.Dispatcher.InvokeAsync(() => { TableNames.Clear(); foreach (var n in names ?? new List<string>()) TableNames.Add(n); SelectedTable = TableNames.FirstOrDefault(); if (SelectedTable == null) SetErrorMessage("No tables found."); }); }); }
        private bool CanExecuteAddNewRow(object p) => _currentDataTable != null && !IsBusy;
        private bool CanExecuteSaveChanges(object p) => IsDirty && !IsBusy;
        private bool CanExecuteUndoChanges(object p) => _rowChangeHistory.Any() && !IsBusy;
        private bool CanExecuteEditSelectedRows(object p) => p is IList i && i.Count > 0 && !IsBusy;
        private bool CanExecuteReloadData(object p) => !string.IsNullOrEmpty(SelectedTable) && !IsBusy;
        private bool CanExecuteDeleteSelectedRow(object p) => p is IList i && i.Count > 0 && !IsBusy;
        private bool CanExecuteDeleteTableCommand(object p) => !string.IsNullOrEmpty(SelectedTable) && !IsBusy;
        private bool CanExecuteExportData(object p) => _currentDataTable != null && _currentDataTable.Rows.Count > 0 && !IsBusy;
        private bool CanExecuteImportData(object p) => _currentDataTable != null && !IsBusy;
        private void ExecuteAddNewRow(object parameter) { if (!CanExecuteAddNewRow(parameter)) return; SetErrorMessage(null); NewRowData newRowData; bool dialogResult; if (SelectedTable.StartsWith("_Long_", StringComparison.OrdinalIgnoreCase)) { var addRowLongVM = new AddRowLongViewModel(SelectedTable, _dataRepository, _logger, _dialogService); dialogResult = _dialogService.ShowAddRowLongDialog(addRowLongVM, out newRowData); } else { var columnNames = _currentDataTable.Columns.Cast<DataColumn>().Where(c => !c.AutoIncrement && !c.ReadOnly).Select(c => c.ColumnName).ToList(); if (!columnNames.Any()) { SetErrorMessage($"Table '{SelectedTable}' has no editable columns."); return; } var initialValues = new Dictionary<string, object>(); var dateColumn = _currentDataTable.Columns.OfType<DataColumn>().FirstOrDefault(c => c.DataType == typeof(DateTime) && columnNames.Contains(c.ColumnName)); if (dateColumn != null) { var maxDate = _currentDataTable.AsEnumerable().Select(r => r.Field<DateTime?>(dateColumn)).Where(d => d.HasValue).Select(d => d.Value).DefaultIfEmpty(DateTime.MinValue).Max(); initialValues[dateColumn.ColumnName] = (maxDate == DateTime.MinValue) ? DateTime.Today : maxDate.AddDays(1); } dialogResult = _dialogService.ShowAddRowDialog(columnNames, SelectedTable, initialValues, out newRowData); } if (dialogResult && newRowData != null) { try { DataRow newActualDataRow = _currentDataTable.NewRow(); foreach (var kvp in newRowData.Values) { if (_currentDataTable.Columns.Contains(kvp.Key)) { var col = _currentDataTable.Columns[kvp.Key]; if (col.ReadOnly || col.AutoIncrement) continue; try { if (kvp.Value == null || string.IsNullOrWhiteSpace(kvp.Value.ToString())) { if (col.AllowDBNull) newActualDataRow[kvp.Key] = DBNull.Value; else throw new InvalidOperationException($"Column '{col.ColumnName}' cannot be null."); } else newActualDataRow[kvp.Key] = Convert.ChangeType(kvp.Value, col.DataType, CultureInfo.CurrentCulture); } catch (Exception ex) { SetErrorMessage($"Error setting column '{kvp.Key}': {ex.Message}"); return; } } } _currentDataTable.Rows.Add(newActualDataRow); } catch (Exception ex) { SetErrorMessage($"Error finalizing new row: {ex.Message}"); } } }
        private async void ExecuteSaveChanges(object p) { if (!CanExecuteSaveChanges(p)) return; var changes = _currentDataTable?.GetChanges(); if (changes == null || changes.Rows.Count == 0) { IsDirty = false; return; } if (!_dialogService.ShowConfirmationDialog("Confirm Save", $"Save {changes.Rows.Count} change(s) to '{SelectedTable}'?")) return; bool success = false; await ExecuteLongRunningOperation(async () => { success = await _dataRepository.SaveChangesAsync(changes, SelectedTable); if (!success && string.IsNullOrEmpty(ErrorMessage)) SetErrorMessage("Failed to save changes."); }); if (success) { _currentDataTable.AcceptChanges(); _rowChangeHistory.Clear(); } CheckIfDirty(); (UndoChangesCommand as ViewModelCommand)?.RaiseCanExecuteChanged(); }
        private void ExecuteUndoChanges(object p) { if (!CanExecuteUndoChanges(p)) return; var last = _rowChangeHistory.LastOrDefault(); if (last != null) { last.RejectChanges(); _rowChangeHistory.Remove(last); } CheckIfDirty(); (SaveChangesCommand as ViewModelCommand)?.RaiseCanExecuteChanged(); }
        private void ExecuteEditSelectedRows(object p) { if (!CanExecuteEditSelectedRows(p)) return; EditableRows.Clear(); foreach (var i in (IList)p) if (i is DataRowView drv) EditableRows.Add(drv); OnPropertyChanged(nameof(EditableRows)); }
        private void ExecuteReloadData(object p) { if (!CanExecuteReloadData(p)) return; if (IsDirty && !_dialogService.ShowConfirmationDialog("Discard Changes?", "You have unsaved changes. Reload and discard them?")) return; LoadDataForSelectedTableAsync(); }
        private void ExecuteDeleteSelectedRow(object p) { if (!CanExecuteDeleteSelectedRow(p)) return; var items = ((IList)p).OfType<DataRowView>().ToList(); if (_dialogService.ShowConfirmationDialog("Confirm Delete", $"Delete {items.Count} row(s)?")) { foreach (var i in items) i.Row.Delete(); } }
        private async void ExecuteDeleteTableCommand(object parameter) { if (!CanExecuteDeleteTableCommand(parameter)) return; string message = $"Are you sure you want to permanently delete the table '{SelectedTable}'?\n\nThis action CANNOT be undone."; if (_dialogService.ShowConfirmationDialog("Confirm Permanent Delete", message)) { bool success = false; await ExecuteLongRunningOperation(async () => { success = await _dataRepository.DeleteTableAsync(SelectedTable); }); if (success) { LoadInitialDataAsync(); } else { SetErrorMessage($"Failed to delete the table '{SelectedTable}'. Check logs for details."); } } }
        private async void ExecuteExportData(object p) { if (!CanExecuteExportData(p)) return; if (_dialogService.ShowSaveFileDialog("Export Data", $"{SelectedTable}_Export_{DateTime.Now:yyyyMMdd}", ".xlsx", "Excel (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv", out string path)) { await ExecuteLongRunningOperation(async () => { if (Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase)) { ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial; var fileInfo = new FileInfo(path); using (var package = new ExcelPackage(fileInfo)) { var ws = package.Workbook.Worksheets.Add(SanitizeSheetName(SelectedTable)); var rows = _currentDataTable.AsEnumerable().Where(r => r.RowState != DataRowState.Deleted); if (rows.Any()) { ws.Cells["A1"].LoadFromDataTable(rows.CopyToDataTable(), true); ws.Cells[ws.Dimension.Address].AutoFitColumns(); } await package.SaveAsAsync(fileInfo); } } else { var sb = new StringBuilder(); sb.AppendLine("sep=,"); sb.AppendLine(string.Join(",", _currentDataTable.Columns.Cast<DataColumn>().Select(c => QuoteValueIfNeeded(c.ColumnName)))); foreach (DataRow r in _currentDataTable.Rows) { if (r.RowState == DataRowState.Deleted) continue; sb.AppendLine(string.Join(",", r.ItemArray.Select(f => QuoteValueIfNeeded(f?.ToString())))); } await Task.Run(() => File.WriteAllText(path, sb.ToString(), Encoding.UTF8)); } }); } }
        private void ExecuteShowAdvancedImport(object parameter) { if (!CanExecuteImportData(parameter)) return; var importVM = new ImportTableViewModel(SelectedTable, _dialogService); if (_dialogService.ShowImportTableDialog(importVM, out ImportSettings settings)) { ExecuteImportData(settings); } }
        private void ExecuteShowCreateTableCommand(object parameter) { var createTableVM = new CreateTableViewModel(_dialogService, _logger, _dataRepository); _dialogService.ShowCreateTableDialog(createTableVM); LoadInitialDataAsync(); }
        private async void ExecuteImportData(object parameter) { ImportSettings settings; if (parameter is null) { if (!_dialogService.ShowOpenFileDialog("Import Data File", "Excel/CSV|*.xlsx;*.csv|All|*.*", out string filePath)) { return; } settings = new ImportSettings { FilePath = filePath, RowsToIgnore = 0 }; } else { settings = parameter as ImportSettings; } if (settings == null || string.IsNullOrEmpty(settings.FilePath)) return; await ExecuteLongRunningOperation(async () => { var errors = new List<string>(); DataTable importDt = Path.GetExtension(settings.FilePath).Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ? await Task.Run(() => LoadXlsxToDataTable(settings.FilePath, errors, settings.RowsToIgnore)) : await Task.Run(() => LoadCsvToDataTable(settings.FilePath, errors, settings.RowsToIgnore)); if (importDt == null) { errors.Add("Could not read file or file is empty."); } int imported = 0, skipped = 0; if (importDt != null && importDt.Rows.Count > 0) { var targetCols = _currentDataTable.Columns.Cast<DataColumn>().ToList(); foreach (DataRow sRow in importDt.Rows) { var newRow = _currentDataTable.NewRow(); bool valid = true; foreach (var tCol in targetCols) { if (tCol.ColumnName.Equals("ID", StringComparison.OrdinalIgnoreCase)) continue; var sCol = importDt.Columns.Cast<DataColumn>().FirstOrDefault(c => c.ColumnName.Equals(tCol.ColumnName, StringComparison.OrdinalIgnoreCase)); if (sCol != null) { object val = sRow[sCol]; try { if ((val == null || val == DBNull.Value || string.IsNullOrWhiteSpace(val.ToString())) && tCol.AllowDBNull) newRow[tCol] = DBNull.Value; else if ((val == null || val == DBNull.Value || string.IsNullOrWhiteSpace(val.ToString())) && !tCol.AllowDBNull) throw new FormatException("Cannot be null."); else newRow[tCol] = Convert.ChangeType(val, tCol.DataType, CultureInfo.CurrentCulture); } catch (Exception) { valid = false; errors.Add($"Row {imported + skipped + 1}, Col '{tCol.ColumnName}': Type mismatch."); break; } } else if (!tCol.AllowDBNull && tCol.DefaultValue == DBNull.Value && string.IsNullOrEmpty(tCol.Expression)) { valid = false; errors.Add($"Row {imported + skipped + 1}: Missing required column '{tCol.ColumnName}'."); break; } } if (valid) { await Application.Current.Dispatcher.InvokeAsync(() => _currentDataTable.Rows.Add(newRow)); imported++; } else { skipped++; } } } else if (!errors.Any()) { errors.Add("No data found in file to import."); } await Application.Current.Dispatcher.InvokeAsync(() => { SetErrorMessage($"Import complete. Added: {imported}, Skipped: {skipped}. " + (errors.Any() ? $"First error: {errors.First()}" : "")); }); }); }
        private DataTable LoadXlsxToDataTable(string path, List<string> errors, int rowsToIgnore) { var dt = new DataTable(); try { ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial; var fileInfo = new FileInfo(path); using (var package = new ExcelPackage(fileInfo)) { var worksheet = package.Workbook.Worksheets.FirstOrDefault(); if (worksheet == null || worksheet.Dimension == null) { errors.Add("Excel file is empty or contains no worksheets."); return null; } int headerRow = 1 + rowsToIgnore; if (headerRow > worksheet.Dimension.End.Row) { errors.Add("Rows to ignore exceeds the total number of rows in the sheet."); return null; } foreach (var firstRowCell in worksheet.Cells[headerRow, 1, headerRow, worksheet.Dimension.End.Column]) { string columnName = firstRowCell.Text.Trim(); if (string.IsNullOrEmpty(columnName)) columnName = $"Column_{firstRowCell.Start.Column}"; if (dt.Columns.Contains(columnName)) columnName = $"{columnName}_{dt.Columns.Count}"; dt.Columns.Add(columnName); } for (int rowNum = headerRow + 1; rowNum <= worksheet.Dimension.End.Row; rowNum++) { var wsRow = worksheet.Cells[rowNum, 1, rowNum, dt.Columns.Count]; DataRow row = dt.Rows.Add(); bool hasValues = false; foreach (var cell in wsRow) { row[cell.Start.Column - 1] = cell.Value ?? DBNull.Value; if (cell.Value != null) hasValues = true; } if (!hasValues) { dt.Rows.Remove(row); } } } } catch (Exception ex) { errors.Add($"Error reading XLSX file: {ex.Message}"); _logger.LogError($"[LoadXlsxToDataTable] Error: {ex.Message}", ex); return null; } return dt; }
        private DataTable LoadCsvToDataTable(string path, List<string> errors, int rowsToIgnore) { try { var dt = new DataTable(); var lines = File.ReadAllLines(path).Skip(rowsToIgnore).ToList(); if (!lines.Any()) return dt; var headers = lines[0].Split(','); foreach (var h in headers) dt.Columns.Add(h.Trim()); foreach (var line in lines.Skip(1)) { var values = line.Split(','); dt.Rows.Add(values); } return dt; } catch (Exception ex) { errors.Add(ex.Message); return null; } }
        private string QuoteValueIfNeeded(string v) => (v != null && (v.Contains(",") || v.Contains("\""))) ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
        private string SanitizeSheetName(string n) { var s = System.Text.RegularExpressions.Regex.Replace(n, @"[\\/\?\*\[\]:]", "_"); return s.Length > 31 ? s.Substring(0, 31) : (string.IsNullOrEmpty(s) ? "Sheet1" : s); }
    }
}