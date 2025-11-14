// In WPF_LoginForm.ViewModels/CreateTableViewModel.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data; // Keep this for general Data types
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using OfficeOpenXml;
using WPF_LoginForm.Repositories;
using WPF_LoginForm.Services;

namespace WPF_LoginForm.ViewModels
{
    public class CreateTableViewModel : ViewModelBase
    {
        private readonly IDialogService _dialogService;
        private readonly ILogger _logger;
        private readonly IDataRepository _dataRepository;
        private readonly ExcelAnalysisService _excelAnalysisService;

        private string _filePath;
        public string FilePath
        {
            get => _filePath;
            set
            {
                if (SetProperty(ref _filePath, value))
                {
                    ProposedTableName = SanitizeSqlName(Path.GetFileNameWithoutExtension(value));
                    ProposedSchema.Clear();
                    WorksheetNames.Clear();
                    IsSchemaReady = false;
                    IsFileSelected = !string.IsNullOrEmpty(value);
                    (AnalyzeCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
                    LoadWorksheetNamesAsync();
                }
            }
        }

        private string _proposedTableName;
        public string ProposedTableName { get => _proposedTableName; set => SetProperty(ref _proposedTableName, value); }

        public ObservableCollection<string> WorksheetNames { get; }
        private string _selectedWorksheetName;
        public string SelectedWorksheetName { get => _selectedWorksheetName; set => SetProperty(ref _selectedWorksheetName, value); }

        public List<int> AvailableHeaderRows { get; }
        private int _headerRowNumber;
        public int HeaderRowNumber { get => _headerRowNumber; set => SetProperty(ref _headerRowNumber, value); }

        private bool _autoAddIdColumn;
        public bool AutoAddIdColumn
        {
            get => _autoAddIdColumn;
            set
            {
                if (SetProperty(ref _autoAddIdColumn, value))
                {
                    HandleAutoIdColumn();
                }
            }
        }

        private bool _isFileSelected;
        public bool IsFileSelected { get => _isFileSelected; private set => SetProperty(ref _isFileSelected, value); }

        private bool _isSchemaReady;
        public bool IsSchemaReady { get => _isSchemaReady; private set => SetProperty(ref _isSchemaReady, value); }

        public ObservableCollection<ColumnSchemaViewModel> ProposedSchema { get; }

        public ICommand BrowseCommand { get; }
        public ICommand AnalyzeCommand { get; }
        public ICommand CreateTableCommand { get; }
        public Action CloseAction { get; set; }

        public CreateTableViewModel(IDialogService dialogService, ILogger logger, IDataRepository dataRepository)
        {
            _dialogService = dialogService;
            _logger = logger;
            _dataRepository = dataRepository;
            _excelAnalysisService = new ExcelAnalysisService();

            ProposedSchema = new ObservableCollection<ColumnSchemaViewModel>();
            WorksheetNames = new ObservableCollection<string>();
            AvailableHeaderRows = Enumerable.Range(1, 9).ToList();

            HeaderRowNumber = 1;
            AutoAddIdColumn = false;

            BrowseCommand = new ViewModelCommand(ExecuteBrowseCommand);
            AnalyzeCommand = new ViewModelCommand(ExecuteAnalyzeCommand, (obj) => IsFileSelected && !string.IsNullOrEmpty(SelectedWorksheetName));
            CreateTableCommand = new ViewModelCommand(ExecuteCreateTableCommand, (obj) => IsSchemaReady && !string.IsNullOrEmpty(ProposedTableName));
        }

        private async void LoadWorksheetNamesAsync()
        {
            if (string.IsNullOrEmpty(FilePath)) return;
            try
            {
                var names = await _excelAnalysisService.GetWorksheetNamesAsync(FilePath);
                WorksheetNames.Clear();
                foreach (var name in names) { WorksheetNames.Add(name); }
                SelectedWorksheetName = WorksheetNames.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to get worksheet names.", ex);
                MessageBox.Show($"Could not read worksheets from file: {ex.Message}", "File Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteBrowseCommand(object obj)
        {
            string filter = "Excel Files (*.xlsx)|*.xlsx|All files (*.*)|*.*";
            if (_dialogService.ShowOpenFileDialog("Select Excel File", filter, out string selectedPath))
            {
                FilePath = selectedPath;
            }
        }

        private async void ExecuteAnalyzeCommand(object obj)
        {
            _logger.LogInfo($"Analyzing file: {FilePath}, Sheet: {SelectedWorksheetName}, Header Row: {HeaderRowNumber}");
            IsSchemaReady = false;
            ProposedSchema.Clear();
            (CreateTableCommand as ViewModelCommand)?.RaiseCanExecuteChanged();

            try
            {
                var schemaResults = await _excelAnalysisService.AnalyzeFileAsync(FilePath, SelectedWorksheetName, HeaderRowNumber);

                foreach (var item in schemaResults)
                {
                    ProposedSchema.Add(item);
                }

                HandleAutoIdColumn();

                IsSchemaReady = ProposedSchema.Count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to analyze Excel file.", ex);
                MessageBox.Show($"An error occurred while analyzing the file: {ex.Message}", "Analysis Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                (CreateTableCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
            }
        }

        private void HandleAutoIdColumn()
        {
            var existingIdCol = ProposedSchema.FirstOrDefault(c => c.SourceColumnName == "ID (Auto-Generated)");
            if (AutoAddIdColumn)
            {
                if (existingIdCol == null)
                {
                    foreach (var col in ProposedSchema.Where(c => c.IsPrimaryKey)) { col.IsPrimaryKey = false; }
                    ProposedSchema.Insert(0, new ColumnSchemaViewModel
                    {
                        SourceColumnName = "ID (Auto-Generated)",
                        DestinationColumnName = "ID",
                        SelectedDataType = "Number (int) IDENTITY(1,1)",
                        IsPrimaryKey = true
                    });
                }
            }
            else
            {
                if (existingIdCol != null)
                {
                    ProposedSchema.Remove(existingIdCol);
                }
            }
        }

        private async void ExecuteCreateTableCommand(object obj)
        {
            if (string.IsNullOrWhiteSpace(ProposedTableName)) { MessageBox.Show("Table name cannot be empty.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (await _dataRepository.TableExistsAsync(ProposedTableName)) { MessageBox.Show($"A table named '{ProposedTableName}' already exists.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (ProposedSchema.Count(s => s.IsPrimaryKey) != 1) { MessageBox.Show("You must select exactly one primary key.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            var creationResult = await _dataRepository.CreateTableAsync(ProposedTableName, ProposedSchema.ToList());
            if (!creationResult.Success) { MessageBox.Show($"Failed to create table.\n\n{creationResult.ErrorMessage}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error); return; }

            // --- FIX: Use fully qualified name ---
            System.Data.DataTable dataToImport = await LoadExcelToDataTableAsync();
            if (dataToImport == null) return;

            var importResult = await _dataRepository.BulkImportDataAsync(ProposedTableName, dataToImport);
            if (!importResult.Success) { MessageBox.Show($"Table created, but data import failed.\n\n{importResult.ErrorMessage}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error); return; }

            MessageBox.Show($"Successfully created table '{ProposedTableName}' and imported {dataToImport.Rows.Count} rows.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            CloseAction?.Invoke();
        }

        // --- FIX: Use fully qualified name ---
        private async Task<System.Data.DataTable> LoadExcelToDataTableAsync()
        {
            // --- FIX: Use fully qualified name ---
            var dt = new System.Data.DataTable();
            try
            {
                ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
                var fileInfo = new FileInfo(FilePath);

                using (var package = new ExcelPackage(fileInfo))
                {
                    var worksheet = await Task.Run(() => package.Workbook.Worksheets[SelectedWorksheetName]);
                    if (worksheet == null || worksheet.Dimension == null) return null;

                    var schemaToImport = ProposedSchema.Where(s => s.SourceColumnName != "ID (Auto-Generated)").ToList();

                    foreach (var colSchema in schemaToImport)
                    {
                        dt.Columns.Add(colSchema.DestinationColumnName);
                    }

                    worksheet.Workbook.Calculate();

                    for (int rowNum = HeaderRowNumber + 1; rowNum <= worksheet.Dimension.End.Row; rowNum++)
                    {
                        var rowData = new object[schemaToImport.Count];
                        bool hasValues = false;

                        for (int i = 0; i < schemaToImport.Count; i++)
                        {
                            var colSchema = schemaToImport[i];
                            var excelCol = worksheet.Cells[HeaderRowNumber, 1, HeaderRowNumber, worksheet.Dimension.End.Column]
                                .FirstOrDefault(c => c.Text.Trim() == colSchema.SourceColumnName);

                            if (excelCol != null)
                            {
                                var cellValue = worksheet.Cells[rowNum, excelCol.Start.Column].Value;
                                rowData[i] = cellValue ?? DBNull.Value;
                                if (cellValue != null) hasValues = true;
                            }
                        }

                        if (hasValues)
                        {
                            dt.Rows.Add(rowData);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[LoadExcelToDataTableAsync] Error reading Excel file for import.", ex);
                MessageBox.Show($"Error reading Excel file: {ex.Message}", "File Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
            return dt;
        }

        private string SanitizeSqlName(string rawName) { if (string.IsNullOrWhiteSpace(rawName)) return "New_Table"; string sanitized = System.Text.RegularExpressions.Regex.Replace(rawName, @"[^\w]", "_"); if (char.IsDigit(sanitized[0])) { sanitized = "_" + sanitized; } return sanitized; }
    }
}