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
using ClosedXML.Excel;
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
        private bool _isBusy; // Will be true for the entire operation duration
        private bool _isProgressBarVisible; // Will only be true after 2 seconds
        private string _errorMessage;
        private bool _isDirty;
        private DataTable _currentDataTable;

        private double _dataGridFontSize = 12;
        private const double MinFontSize = 8;
        private const double MaxFontSize = 24;
        private const double FontSizeStep = 1;

        private ObservableCollection<DataRowView> _editableRows = new ObservableCollection<DataRowView>();
        private readonly List<DataRow> _rowChangeHistory = new List<DataRow>();
        private int _longRunningOperationCount = 0;


        // --- Properties ---
        public ObservableCollection<string> TableNames { get => _tableNames; private set { _tableNames = value; OnPropertyChanged(); } }
        public string SelectedTable
        {
            get => _selectedTable;
            set
            {
                if (_selectedTable != value)
                {
                    UnsubscribeFromTableEvents();
                    _selectedTable = value;
                    OnPropertyChanged();

                    (AddNewRowCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
                    (ImportDataCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
                    (ExportDataCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
                    (ReloadDataCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
                    (UndoChangesCommand as ViewModelCommand)?.RaiseCanExecuteChanged();

                    if (!string.IsNullOrEmpty(_selectedTable) && (_tableNames?.Contains(_selectedTable) ?? false))
                    {
                        LoadDataForSelectedTableAsync();
                    }
                    else if (string.IsNullOrEmpty(_selectedTable))
                    {
                        DataTableView = null;
                        SetErrorMessage(null);
                        IsDirty = false;
                    }
                }
            }
        }
        public DataView DataTableView
        {
            get => _dataTableView;
            private set
            {
                if (_dataTableView != value)
                {
                    UnsubscribeFromTableEvents();
                    _dataTableView = value;
                    _currentDataTable = _dataTableView?.Table;
                    SubscribeToTableEvents();
                    IsDirty = false;
                    EditableRows.Clear();
                    _rowChangeHistory.Clear();
                    OnPropertyChanged(nameof(EditableRows));
                    OnPropertyChanged();
                }
            }
        }

        public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value, nameof(IsBusy)); }
        public bool IsProgressBarVisible { get => _isProgressBarVisible; private set => SetProperty(ref _isProgressBarVisible, value, nameof(IsProgressBarVisible)); }

        public string ErrorMessage
        {
            get => _errorMessage;
            private set => SetProperty(ref _errorMessage, value, nameof(ErrorMessage), () => OnPropertyChanged(nameof(HasError)));
        }
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
        public bool IsDirty { get => _isDirty; private set { if (_isDirty != value) { _isDirty = value; OnPropertyChanged(); } } }

        public double DataGridFontSize
        {
            get => _dataGridFontSize;
            set
            {
                var newSize = Math.Max(MinFontSize, Math.Min(MaxFontSize, value));
                if (_dataGridFontSize != newSize)
                {
                    _dataGridFontSize = newSize;
                    OnPropertyChanged();
                    (DecreaseFontSizeCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
                    (IncreaseFontSizeCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public ObservableCollection<DataRowView> EditableRows { get => _editableRows; }

        // --- Commands ---
        public ICommand AddNewRowCommand { get; }
        public ICommand SaveChangesCommand { get; }
        public ICommand UndoChangesCommand { get; }
        public ICommand EditSelectedRowsCommand { get; }
        public ICommand ReloadDataCommand { get; }
        public ICommand DeleteSelectedRowCommand { get; }
        public ICommand ImportDataCommand { get; }
        public ICommand ExportDataCommand { get; }
        public ICommand DecreaseFontSizeCommand { get; }
        public ICommand IncreaseFontSizeCommand { get; }

        // --- Constructor ---
        public DatarepViewModel(ILogger logger, IDialogService dialogService, IDataRepository dataRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _dataRepository = dataRepository ?? throw new ArgumentNullException(nameof(dataRepository));

            TableNames = new ObservableCollection<string>();
            AddNewRowCommand = new ViewModelCommand(ExecuteAddNewRow, CanExecuteAddNewRow);
            SaveChangesCommand = new ViewModelCommand(ExecuteSaveChanges, CanExecuteSaveChanges);
            UndoChangesCommand = new ViewModelCommand(ExecuteUndoChanges, CanExecuteUndoChanges);
            EditSelectedRowsCommand = new ViewModelCommand(ExecuteEditSelectedRows, CanExecuteEditSelectedRows);
            ReloadDataCommand = new ViewModelCommand(ExecuteReloadData, CanExecuteReloadData);
            DeleteSelectedRowCommand = new ViewModelCommand(ExecuteDeleteSelectedRow, CanExecuteDeleteSelectedRow);
            ImportDataCommand = new ViewModelCommand(ExecuteImportData, CanExecuteImportData);
            ExportDataCommand = new ViewModelCommand(ExecuteExportData, CanExecuteExportData);
            DecreaseFontSizeCommand = new ViewModelCommand(ExecuteDecreaseFontSize, CanExecuteDecreaseFontSize);
            IncreaseFontSizeCommand = new ViewModelCommand(ExecuteIncreaseFontSize, CanExecuteIncreaseFontSize);

            _logger.LogInfo("DatarepViewModel initialized.");
            LoadInitialDataAsync();
        }

        public void SetErrorMessage(string message)
        {
            ErrorMessage = message;
        }

        private async Task ExecuteLongRunningOperation(Func<Task> operation)
        {
            Interlocked.Increment(ref _longRunningOperationCount);
            IsBusy =true;
            SetErrorMessage(null);
            IsProgressBarVisible = false;

            var progressTask = Task.Run(async () => {
                await Task.Delay(2000);
                if (IsBusy)
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
                _logger.LogError("[LongOp] Exception during long-running operation.", ex);
                SetErrorMessage($"An error occurred: {ex.Message}");
            }
            finally
            {
                if (Interlocked.Decrement(ref _longRunningOperationCount) == 0)
                {
                    IsBusy = false;
                    IsProgressBarVisible = false;
                }
            }
        }

        private void SubscribeToTableEvents() { if (_currentDataTable != null) { _currentDataTable.RowChanged += OnDataTableRowChanged; _currentDataTable.RowDeleted += OnDataTableRowChanged; _currentDataTable.TableNewRow += OnDataTableNewRow; } }
        private void UnsubscribeFromTableEvents() { if (_currentDataTable != null) { _currentDataTable.RowChanged -= OnDataTableRowChanged; _currentDataTable.RowDeleted -= OnDataTableRowChanged; _currentDataTable.TableNewRow -= OnDataTableNewRow; } }

        private void OnDataTableRowChanged(object sender, DataRowChangeEventArgs e)
        {
            _logger.LogInfo($"RowChanged: Action={e.Action}, RowState={e.Row.RowState}");
            if (e.Action == DataRowAction.Add || e.Action == DataRowAction.Change || e.Action == DataRowAction.Delete)
            {
                if (_rowChangeHistory.Contains(e.Row))
                {
                    _rowChangeHistory.Remove(e.Row);
                }
                _rowChangeHistory.Add(e.Row);
            }
            CheckIfDirty();
            (UndoChangesCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
        }

        private void OnDataTableNewRow(object sender, DataTableNewRowEventArgs e) { _logger.LogInfo($"TableNewRow fired."); }
        private void CheckIfDirty() { IsDirty = _currentDataTable?.GetChanges() != null; }
        private void LogNotImplemented(string featureName) { SetErrorMessage($"{featureName} functionality not yet implemented."); }

        private async void LoadInitialDataAsync()
        {
            await ExecuteLongRunningOperation(async () =>
            {
                _logger.LogInfo("Loading initial table names...");
                var fetchedTableNames = await _dataRepository.GetTableNamesAsync();

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    UnsubscribeFromTableEvents();
                    TableNames.Clear();
                    DataTableView = null;
                    if (fetchedTableNames != null)
                    {
                        foreach (var name in fetchedTableNames) { TableNames.Add(name); }
                    }

                    string tableToSelect = TableNames.FirstOrDefault();
                    SelectedTable = tableToSelect;
                    if (SelectedTable == null && (TableNames == null || !TableNames.Any()))
                    {
                        SetErrorMessage("No tables found in the database.");
                    }
                });
            });
        }

        private async void LoadDataForSelectedTableAsync()
        {
            if (string.IsNullOrEmpty(SelectedTable)) return;
            await ExecuteLongRunningOperation(async () =>
            {
                _logger.LogInfo($"Loading data for table '{SelectedTable}'...");
                DataTable dataTable = await _dataRepository.GetTableDataAsync(SelectedTable);
                await Application.Current.Dispatcher.InvokeAsync(() => DataTableView = dataTable.DefaultView);
                _logger.LogInfo($"Data loaded successfully for table '{SelectedTable}'.");
            });
        }

        private bool CanExecuteAddNewRow(object parameter) { return _currentDataTable != null && !string.IsNullOrEmpty(SelectedTable) && !IsBusy; }
        private bool CanExecuteSaveChanges(object parameter) { return IsDirty && !IsBusy; }
        private bool CanExecuteUndoChanges(object parameter) { return _rowChangeHistory.Any() && !IsBusy; }
        private bool CanExecuteEditSelectedRows(object parameter) { return parameter is IList selectedItems && selectedItems.Count > 0 && _currentDataTable != null && !IsBusy; }
        private bool CanExecuteReloadData(object parameter) { return !string.IsNullOrEmpty(SelectedTable) && !IsBusy; }
        private bool CanExecuteDeleteSelectedRow(object parameter) { return !IsBusy && _currentDataTable != null && parameter is IList selectedItems && selectedItems.Count > 0; }
        private bool CanExecuteExportData(object parameter) { return !IsBusy && _currentDataTable != null && _currentDataTable.Rows.Count > 0; }
        private bool CanExecuteImportData(object parameter) { return !IsBusy && _currentDataTable != null && !string.IsNullOrEmpty(SelectedTable); }

        private void ExecuteAddNewRow(object parameter)
        {
            if (!CanExecuteAddNewRow(parameter)) return;
            SetErrorMessage(null);
            NewRowData newRowData = null;
            bool dialogResult = false;
            Window ownerWindow = Application.Current?.MainWindow;

            if (SelectedTable.StartsWith("_Long_", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var addRowLongVM = new AddRowLongViewModel(SelectedTable, _dataRepository, _logger, _dialogService);
                    var addRowLongWindow = new Views.AddRowLongWindow { DataContext = addRowLongVM };
                    if (ownerWindow != null && ownerWindow.IsVisible && ownerWindow != addRowLongWindow) { addRowLongWindow.Owner = ownerWindow; }
                    else { addRowLongWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen; }
                    dialogResult = addRowLongWindow.ShowDialog() ?? false;
                    if (dialogResult)
                    {
                        newRowData = addRowLongVM.GetEnteredData(out List<string> validationErrors);
                        if (validationErrors != null && validationErrors.Any())
                        {
                            SetErrorMessage(validationErrors.First()); newRowData = null; dialogResult = false;
                        }
                    }
                }
                catch (Exception ex) { _logger.LogError($"Error with AddRowLongWindow: {ex.Message}", ex); SetErrorMessage($"Error opening detailed add row dialog: {ex.Message}"); dialogResult = false; }
            }
            else
            {
                try
                {
                    var columnNames = _currentDataTable.Columns.Cast<DataColumn>().Where(c => !c.AutoIncrement && !c.ReadOnly).Select(c => c.ColumnName).ToList();
                    if (!columnNames.Any()) { SetErrorMessage($"Table '{SelectedTable}' has no editable columns."); return; }
                    var initialValues = new Dictionary<string, object>();
                    DataColumn dateColumn = _currentDataTable.Columns.OfType<DataColumn>().FirstOrDefault(c => c.DataType == typeof(DateTime) && columnNames.Contains(c.ColumnName));
                    if (dateColumn != null)
                    {
                        DateTime maxDate = _currentDataTable.AsEnumerable().Select(row => row.Field<DateTime?>(dateColumn)).Where(d => d.HasValue).Select(d => d.Value).DefaultIfEmpty(DateTime.MinValue).Max();
                        initialValues[dateColumn.ColumnName] = (maxDate == DateTime.MinValue) ? DateTime.Today : maxDate.AddDays(1);
                    }
                    var addRowVM = new AddRowViewModel(columnNames, SelectedTable, initialValues);
                    var addRowWindow = new Views.AddRowWindow { DataContext = addRowVM };
                    if (ownerWindow != null && ownerWindow.IsVisible && ownerWindow != addRowWindow) { addRowWindow.Owner = ownerWindow; }
                    else { addRowWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen; }
                    dialogResult = addRowWindow.ShowDialog() ?? false;
                    if (dialogResult) { newRowData = addRowVM.GetEnteredData(); }
                }
                catch (Exception ex) { _logger.LogError($"Error with AddRowWindow: {ex.Message}", ex); SetErrorMessage($"Error opening add row dialog: {ex.Message}"); dialogResult = false; }
            }

            if (dialogResult && newRowData != null)
            {
                try
                {
                    DataRow newActualDataRow = _currentDataTable.NewRow();
                    foreach (var kvp in newRowData.Values)
                    {
                        if (_currentDataTable.Columns.Contains(kvp.Key))
                        {
                            DataColumn column = _currentDataTable.Columns[kvp.Key];
                            if (column.ReadOnly || column.AutoIncrement) continue;
                            try
                            {
                                if (kvp.Value == null || string.IsNullOrWhiteSpace(kvp.Value.ToString()))
                                {
                                    if (column.AllowDBNull) newActualDataRow[kvp.Key] = DBNull.Value;
                                    else throw new InvalidOperationException($"Column '{column.ColumnName}' cannot be null or empty.");
                                }
                                else { newActualDataRow[kvp.Key] = Convert.ChangeType(kvp.Value, column.DataType, CultureInfo.CurrentCulture); }
                            }
                            catch (Exception colEx) { SetErrorMessage($"Error setting column '{kvp.Key}': {colEx.Message}"); return; }
                        }
                    }
                    _currentDataTable.Rows.Add(newActualDataRow);
                }
                catch (Exception ex) { SetErrorMessage($"Error finalizing new row: {ex.Message}"); }
            }
        }

        private async void ExecuteSaveChanges(object parameter)
        {
            if (!CanExecuteSaveChanges(parameter)) return;
            DataTable changes = _currentDataTable?.GetChanges();
            if (changes == null || changes.Rows.Count == 0) { IsDirty = false; return; }
            string confirmMsg = $"Save {changes.Rows.Count} change(s) to '{SelectedTable}'?";
            if (!_dialogService.ShowConfirmationDialog("Confirm Save", confirmMsg)) return;

            bool success = false;
            await ExecuteLongRunningOperation(async () =>
            {
                _logger.LogInfo($"Attempting to save changes via repository for table '{SelectedTable}'...");
                success = await _dataRepository.SaveChangesAsync(changes, SelectedTable);
                if (!success && string.IsNullOrEmpty(ErrorMessage)) { SetErrorMessage("Failed to save changes. See log for details."); }
            });

            if (success)
            {
                _logger.LogInfo("Repository reported save success. Accepting changes.");
                _currentDataTable.AcceptChanges();
                _rowChangeHistory.Clear();
            }
            CheckIfDirty();
            (UndoChangesCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
        }

        private void ExecuteUndoChanges(object parameter)
        {
            if (!CanExecuteUndoChanges(parameter)) return;
            DataRow lastChangedRow = _rowChangeHistory.LastOrDefault();
            if (lastChangedRow != null)
            {
                lastChangedRow.RejectChanges();
                _rowChangeHistory.Remove(lastChangedRow);
            }
            CheckIfDirty();
            (UndoChangesCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
            (SaveChangesCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
        }

        private void ExecuteEditSelectedRows(object parameter)
        {
            if (!CanExecuteEditSelectedRows(parameter) || !(parameter is IList selectedItems) || selectedItems.Count == 0) { SetErrorMessage("Please select one or more rows to edit."); return; }
            SetErrorMessage(null);
            EditableRows.Clear();
            foreach (var item in selectedItems)
            {
                if (item is DataRowView drv) { EditableRows.Add(drv); }
            }
            OnPropertyChanged(nameof(EditableRows));
        }

        private void ExecuteReloadData(object parameter)
        {
            if (!CanExecuteReloadData(parameter)) return;
            if (IsDirty && !_dialogService.ShowConfirmationDialog("Discard Changes?", "You have unsaved changes. Are you sure you want to reload and discard them?")) return;
            LoadDataForSelectedTableAsync();
        }

        private void ExecuteDeleteSelectedRow(object parameter)
        {
            if (!CanExecuteDeleteSelectedRow(parameter) || !(parameter is IList selectedItems) || selectedItems.Count == 0) { return; }
            string confirmationMessage = selectedItems.Count == 1 ? "Are you sure you want to delete the selected row?" : $"Are you sure you want to delete these {selectedItems.Count} rows?";
            if (_dialogService.ShowConfirmationDialog("Confirm Delete", confirmationMessage))
            {
                foreach (var item in selectedItems.OfType<DataRowView>().ToList())
                {
                    try { item.Row.Delete(); }
                    catch (Exception ex) { _logger.LogError($"[ExecuteDeleteSelectedRow] Error deleting a row: {ex.Message}", ex); SetErrorMessage($"An error occurred while trying to delete a row: {ex.Message}"); }
                }
            }
        }

        private void ExecuteDecreaseFontSize(object parameter) { DataGridFontSize -= FontSizeStep; }
        private bool CanExecuteDecreaseFontSize(object parameter) { return DataGridFontSize > MinFontSize; }
        private void ExecuteIncreaseFontSize(object parameter) { DataGridFontSize += FontSizeStep; }
        private bool CanExecuteIncreaseFontSize(object parameter) { return DataGridFontSize < MaxFontSize; }

        private async void ExecuteExportData(object parameter)
        {
            if (!CanExecuteExportData(parameter)) return;
            SetErrorMessage(null);
            string defaultFileName = $"{SelectedTable}_Export_{DateTime.Now:yyyyMMddHHmmss}";
            string filter = "Excel Workbook (*.xlsx)|*.xlsx|CSV files (*.csv)|*.csv|All files (*.*)|*.*";
            if (_dialogService.ShowSaveFileDialog("Export Data", defaultFileName, ".xlsx", filter, out string selectedFilePath))
            {
                await ExecuteLongRunningOperation(async () =>
                {
                    string fileExtension = Path.GetExtension(selectedFilePath).ToLowerInvariant();
                    if (fileExtension == ".xlsx")
                    {
                        using (var workbook = new XLWorkbook())
                        {
                            var worksheet = workbook.Worksheets.Add(SanitizeSheetName(SelectedTable));
                            var rowsToExport = _currentDataTable.AsEnumerable().Where(r => r.RowState != DataRowState.Deleted);
                            if (rowsToExport.Any())
                            {
                                worksheet.Cell(1, 1).InsertTable(rowsToExport.CopyToDataTable(), true);
                            }
                            worksheet.Columns().AdjustToContents();
                            await Task.Run(() => workbook.SaveAs(selectedFilePath));
                        }
                    }
                    else
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine("sep=,");
                        IEnumerable<string> columnNames = _currentDataTable.Columns.Cast<DataColumn>().Select(column => QuoteValueIfNeeded(column.ColumnName));
                        sb.AppendLine(string.Join(",", columnNames));

                        foreach (DataRow row in _currentDataTable.Rows)
                        {
                            if (row.RowState == DataRowState.Deleted) continue;
                            IEnumerable<string> fields = row.ItemArray.Select(field => {
                                if (field is DateTime dtValue) return QuoteValueIfNeeded(dtValue.ToString("d"));
                                return QuoteValueIfNeeded(field?.ToString() ?? "");
                            });
                            sb.AppendLine(string.Join(",", fields));
                        }
                        await Task.Run(() => File.WriteAllText(selectedFilePath, sb.ToString(), Encoding.UTF8));
                    }
                    await Application.Current.Dispatcher.InvokeAsync(() => SetErrorMessage($"Exported to {Path.GetFileName(selectedFilePath)} successfully."));
                });
            }
        }

        private async void ExecuteImportData(object parameter)
        {
            if (!CanExecuteImportData(parameter)) return;
            SetErrorMessage(null);
            string filter = "Excel Workbooks (*.xlsx)|*.xlsx|CSV files (*.csv)|*.csv|All files (*.*)|*.*";
            if (_dialogService.ShowOpenFileDialog("Import Data File", filter, out string selectedFilePath))
            {
                await ExecuteLongRunningOperation(async () =>
                {
                    int rowsImported = 0, rowsSkipped = 0;
                    var importErrors = new List<string>();
                    DataTable importDataTable = null;
                    string fileExtension = Path.GetExtension(selectedFilePath).ToLowerInvariant();
                    if (fileExtension == ".xlsx") importDataTable = await Task.Run(() => LoadXlsxToDataTable(selectedFilePath, importErrors));
                    else if (fileExtension == ".csv") importDataTable = await Task.Run(() => LoadCsvToDataTable(selectedFilePath, importErrors));
                    else throw new NotSupportedException($"Unsupported file type: {fileExtension}.");

                    if (importDataTable == null) { importErrors.Add("File could not be read or is empty."); }

                    if (importDataTable != null && importDataTable.Rows.Count > 0)
                    {
                        var targetColumns = _currentDataTable.Columns.Cast<DataColumn>().ToList();
                        foreach (DataRow sourceRow in importDataTable.Rows)
                        {
                            DataRow newRow = _currentDataTable.NewRow();
                            bool rowValid = true;
                            foreach (DataColumn targetCol in targetColumns)
                            {
                                DataColumn sourceCol = importDataTable.Columns.Cast<DataColumn>().FirstOrDefault(sc => sc.ColumnName.Equals(targetCol.ColumnName, StringComparison.OrdinalIgnoreCase));
                                if (sourceCol != null)
                                {
                                    object value = sourceRow[sourceCol];
                                    try
                                    {
                                        if ((value == null || value == DBNull.Value || string.IsNullOrWhiteSpace(value.ToString())) && targetCol.AllowDBNull) newRow[targetCol] = DBNull.Value;
                                        else if ((value == null || value == DBNull.Value || string.IsNullOrWhiteSpace(value.ToString())) && !targetCol.AllowDBNull) throw new FormatException($"Column '{targetCol.ColumnName}' cannot be null/empty.");
                                        else newRow[targetCol] = Convert.ChangeType(value, targetCol.DataType, CultureInfo.CurrentCulture);
                                    }
                                    catch (Exception ex) { rowValid = false; importErrors.Add($"Row {rowsImported + rowsSkipped + 1}, Col '{targetCol.ColumnName}': Cannot convert '{value}'. Err: {ex.Message}"); break; }
                                }
                                else if (!targetCol.AllowDBNull && targetCol.DefaultValue == DBNull.Value && string.IsNullOrEmpty(targetCol.Expression)) { rowValid = false; importErrors.Add($"Row {rowsImported + rowsSkipped + 1}: Missing required column '{targetCol.ColumnName}'."); break; }
                            }
                            if (rowValid) { await Application.Current.Dispatcher.InvokeAsync(() => _currentDataTable.Rows.Add(newRow)); rowsImported++; } else { rowsSkipped++; }
                        }
                    }
                    else if (!importErrors.Any()) { importErrors.Add("No data found in file."); }

                    await Application.Current.Dispatcher.InvokeAsync(() => {
                        string summaryMessage = $"Import finished. Imported: {rowsImported}. Skipped/Failed: {rowsSkipped}.";
                        if (importErrors.Any()) summaryMessage += $" First Error: {importErrors.First()}";
                        SetErrorMessage(summaryMessage);
                        if (rowsImported > 0) CheckIfDirty();
                    });
                });
            }
        }

        protected bool SetProperty<T>(ref T storage, T value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = null, Action onChanged = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            onChanged?.Invoke();
            return true;
        }

        private DataTable LoadXlsxToDataTable(string filePath, List<string> errors)
        {
            DataTable dt = new DataTable();
            try
            {
                using (var workbook = new XLWorkbook(filePath))
                {
                    var worksheet = workbook.Worksheet(1);
                    if (worksheet == null || !worksheet.CellsUsed().Any()) { errors.Add("Excel file's first sheet is empty or has no data."); return dt; }
                    var headerRow = worksheet.Row(1);
                    foreach (var cell in headerRow.CellsUsed())
                    {
                        string columnName = cell.GetString().Trim();
                        if (string.IsNullOrEmpty(columnName)) columnName = $"Column{cell.Address.ColumnNumber}";
                        if (dt.Columns.Contains(columnName)) columnName = $"{columnName}_{dt.Columns.Count}";
                        dt.Columns.Add(columnName);
                    }
                    if (dt.Columns.Count == 0) { errors.Add("Could not determine headers from Excel sheet."); return dt; }
                    foreach (var row in worksheet.RowsUsed().Skip(1))
                    {
                        DataRow newRow = dt.NewRow();
                        bool hasValues = false;
                        for (int i = 0; i < dt.Columns.Count; i++)
                        {
                            var cell = row.Cell(i + 1);
                            if (!cell.IsEmpty(XLCellsUsedOptions.Contents))
                            {
                                if (cell.DataType == XLDataType.DateTime) { try { newRow[i] = cell.GetDateTime(); } catch { newRow[i] = cell.GetFormattedString(); } }
                                else { newRow[i] = cell.GetFormattedString(); }
                                hasValues = true;
                            }
                            else { newRow[i] = DBNull.Value; }
                        }
                        if (hasValues) dt.Rows.Add(newRow);
                    }
                }
            }
            catch (Exception ex) { errors.Add($"Error reading XLSX file: {ex.Message}"); _logger.LogError($"[LoadXlsxToDataTable] Error: {ex.Message}", ex); return null; }
            return dt;
        }

        private DataTable LoadCsvToDataTable(string filePath, List<string> errors)
        {
            DataTable dt = new DataTable();
            try
            {
                string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
                if (lines.Length == 0) { errors.Add("CSV file is empty."); return dt; }
                int dataLineStartIndex = 0;
                if (lines[0].StartsWith("sep=", StringComparison.OrdinalIgnoreCase)) { dataLineStartIndex = 1; }
                if (dataLineStartIndex >= lines.Length) { errors.Add("CSV contains no data after 'sep=' line."); return dt; }
                string[] headers = ParseCsvLine(lines[dataLineStartIndex]);
                if (headers.All(string.IsNullOrWhiteSpace)) { errors.Add("CSV header line is empty."); return dt; }
                foreach (string header in headers)
                {
                    string columnName = header.Trim();
                    if (string.IsNullOrEmpty(columnName)) columnName = $"Column{dt.Columns.Count + 1}";
                    if (dt.Columns.Contains(columnName)) columnName = $"{columnName}_{dt.Columns.Count}";
                    dt.Columns.Add(columnName);
                }
                dataLineStartIndex++;
                for (int i = dataLineStartIndex; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i])) continue;
                    string[] values = ParseCsvLine(lines[i]);
                    dt.Rows.Add(values.Take(dt.Columns.Count).ToArray());
                }
            }
            catch (Exception ex) { errors.Add($"Error reading CSV file: {ex.Message}"); _logger.LogError($"[LoadCsvToDataTable] Error: {ex.Message}", ex); return null; }
            return dt;
        }

        private string[] ParseCsvLine(string line) { return line.Split(','); }
        private string QuoteValueIfNeeded(string value) { if (string.IsNullOrEmpty(value)) return ""; return value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r") ? $"\"{value.Replace("\"", "\"\"")}\"" : value; }
        private string SanitizeSheetName(string name) { if (string.IsNullOrWhiteSpace(name)) return "Sheet1"; string invalidChars = @"[\\/\?\*\[\]:]"; string sanitized = System.Text.RegularExpressions.Regex.Replace(name, invalidChars, "_").Replace("'", ""); return sanitized.Length > 31 ? sanitized.Substring(0, 31) : (string.IsNullOrEmpty(sanitized) ? "Sheet1" : sanitized); }
    }
}