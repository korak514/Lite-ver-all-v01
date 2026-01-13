using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using OfficeOpenXml;
using WPF_LoginForm.Models;
using WPF_LoginForm.Repositories;
using WPF_LoginForm.Services;

namespace WPF_LoginForm.ViewModels
{
    public class HierarchyImportViewModel : ViewModelBase
    {
        private readonly IDataRepository _dataRepository;
        private readonly IDialogService _dialogService;
        private readonly ILogger _logger;

        private string _selectedTableName;
        private string _filePath;
        private bool _isAnalyzing;
        private bool _isImporting;
        private DataTable _previewData;

        public ObservableCollection<string> TableNames { get; } = new ObservableCollection<string>();

        public string SelectedTableName
        {
            get => _selectedTableName;
            set => SetProperty(ref _selectedTableName, value);
        }

        public string FilePath
        {
            get => _filePath;
            set
            {
                if (SetProperty(ref _filePath, value))
                {
                    AnalyzeFile();
                }
            }
        }

        public bool IsAnalyzing { get => _isAnalyzing; set => SetProperty(ref _isAnalyzing, value); }
        public bool IsImporting { get => _isImporting; set => SetProperty(ref _isImporting, value); }
        public DataTable PreviewData { get => _previewData; set => SetProperty(ref _previewData, value); }

        public ObservableCollection<MappingItem> Mappings { get; } = new ObservableCollection<MappingItem>();
        public ObservableCollection<string> ExcelHeaders { get; } = new ObservableCollection<string>();

        public ICommand BrowseCommand { get; }
        public ICommand ImportCommand { get; }
        public ICommand DownloadTemplateCommand { get; }

        public HierarchyImportViewModel(IDataRepository dataRepository, IDialogService dialogService, ILogger logger)
        {
            _dataRepository = dataRepository;
            _dialogService = dialogService;
            _logger = logger;

            BrowseCommand = new ViewModelCommand(ExecuteBrowse);
            ImportCommand = new ViewModelCommand(ExecuteImport, CanExecuteImport);
            DownloadTemplateCommand = new ViewModelCommand(ExecuteDownloadTemplate);

            Initialize();
        }

        private async void Initialize()
        {
            var tables = await _dataRepository.GetTableNamesAsync();
            foreach (var t in tables) TableNames.Add(t);
        }

        private void ExecuteDownloadTemplate(object obj)
        {
            if (_dialogService.ShowSaveFileDialog("Save Template", "Hierarchy_Template", ".xlsx", "Excel Files|*.xlsx", out string path))
            {
                try
                {
                    // FIX: Set License Context to prevent Crash
                    ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                    using (var package = new ExcelPackage(new FileInfo(path)))
                    {
                        var ws = package.Workbook.Worksheets.Add("HierarchyDefinition");

                        var headers = new List<string>
                        {
                            "Part1Value (Category)",
                            "Part2Value (SubCategory)",
                            "Part3Value (Detail)",
                            "Part4Value (Extra)",
                            "CoreItemDisplayName (Dropdown Label)",
                            "ActualDataTableColumnName (DB Column)"
                        };

                        for (int i = 0; i < headers.Count; i++)
                        {
                            ws.Cells[1, i + 1].Value = headers[i];
                            ws.Cells[1, i + 1].Style.Font.Bold = true;
                        }

                        ws.Cells[2, 1].Value = "Electronics"; ws.Cells[2, 2].Value = "Laptops"; ws.Cells[2, 5].Value = "Gaming Laptop 15inch"; ws.Cells[2, 6].Value = "Laptop_Sales_Qty";
                        ws.Cells[3, 1].Value = "Electronics"; ws.Cells[3, 2].Value = "Phones"; ws.Cells[3, 5].Value = "Smartphone 5G"; ws.Cells[3, 6].Value = "Phone_Sales_Qty";

                        ws.Cells.AutoFitColumns();
                        package.Save();
                    }
                    MessageBox.Show("Template saved successfully.\n\nFill this file and then Browse to import it.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to save template: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExecuteBrowse(object obj)
        {
            if (_dialogService.ShowOpenFileDialog("Select Hierarchy Excel", "Excel|*.xlsx", out string path))
            {
                FilePath = path;
            }
        }

        private async void AnalyzeFile()
        {
            if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath)) return;

            IsAnalyzing = true;
            ExcelHeaders.Clear();
            Mappings.Clear();
            PreviewData = null;

            try
            {
                await Task.Run(() =>
                {
                    // License context also needed here
                    ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                    using (var package = new ExcelPackage(new FileInfo(FilePath)))
                    {
                        var ws = package.Workbook.Worksheets.FirstOrDefault();
                        if (ws == null || ws.Dimension == null) return;

                        int cols = ws.Dimension.End.Column;
                        var headers = new List<string>();
                        for (int i = 1; i <= cols; i++)
                        {
                            headers.Add(ws.Cells[1, i].Text.Trim());
                        }

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            foreach (var h in headers) ExcelHeaders.Add(h);
                            InitializeMappings(headers);
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error analyzing file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsAnalyzing = false;
            }
        }

        private void InitializeMappings(List<string> headers)
        {
            var systemColumns = new List<string> { "Part1Value", "Part2Value", "Part3Value", "Part4Value", "CoreItemDisplayName", "ActualDataTableColumnName" };

            foreach (var sysCol in systemColumns)
            {
                var mapping = new MappingItem { DbColumnName = sysCol };

                var match = headers.FirstOrDefault(h =>
                    h.StartsWith(sysCol, StringComparison.OrdinalIgnoreCase) ||
                    (sysCol == "ActualDataTableColumnName" && h.Contains("Column")) ||
                    (sysCol == "CoreItemDisplayName" && h.Contains("Label"))
                );

                if (match != null) mapping.SelectedExcelHeader = match;
                Mappings.Add(mapping);
            }
            GeneratePreview();
        }

        public void GeneratePreview()
        {
            if (string.IsNullOrEmpty(FilePath)) return;
            try
            {
                var dt = new DataTable();
                dt.Columns.Add("OwningDataTableName");
                foreach (var m in Mappings) dt.Columns.Add(m.DbColumnName);

                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                using (var package = new ExcelPackage(new FileInfo(FilePath)))
                {
                    var ws = package.Workbook.Worksheets.FirstOrDefault();
                    int rowCount = Math.Min(ws.Dimension.End.Row, 20);

                    var headerIndex = new Dictionary<string, int>();
                    for (int c = 1; c <= ws.Dimension.End.Column; c++) headerIndex[ws.Cells[1, c].Text.Trim()] = c;

                    for (int r = 2; r <= rowCount; r++)
                    {
                        var row = dt.NewRow();
                        row["OwningDataTableName"] = SelectedTableName ?? "(Select Table)";
                        foreach (var map in Mappings)
                        {
                            if (!string.IsNullOrEmpty(map.SelectedExcelHeader) && headerIndex.ContainsKey(map.SelectedExcelHeader))
                            {
                                int colIdx = headerIndex[map.SelectedExcelHeader];
                                row[map.DbColumnName] = ws.Cells[r, colIdx].Text;
                            }
                        }
                        dt.Rows.Add(row);
                    }
                }
                PreviewData = dt;
            }
            catch { }
        }

        private bool CanExecuteImport(object obj)
        {
            return !string.IsNullOrEmpty(SelectedTableName) && !string.IsNullOrEmpty(FilePath) && Mappings.Any(m => !string.IsNullOrEmpty(m.SelectedExcelHeader)) && !IsImporting;
        }

        private async void ExecuteImport(object obj)
        {
            if (MessageBox.Show($"This will replace the hierarchy definition for '{SelectedTableName}'. Proceed?", "Confirm Import", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            IsImporting = true;
            try
            {
                DataTable fullData = new DataTable();
                fullData.Columns.Add("OwningDataTableName");
                foreach (var m in Mappings) fullData.Columns.Add(m.DbColumnName);

                await Task.Run(() =>
                {
                    ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                    using (var package = new ExcelPackage(new FileInfo(FilePath)))
                    {
                        var ws = package.Workbook.Worksheets.FirstOrDefault();
                        int maxRow = ws.Dimension.End.Row;
                        var headerIndex = new Dictionary<string, int>();
                        for (int c = 1; c <= ws.Dimension.End.Column; c++) headerIndex[ws.Cells[1, c].Text.Trim()] = c;

                        for (int r = 2; r <= maxRow; r++)
                        {
                            var row = fullData.NewRow();
                            row["OwningDataTableName"] = SelectedTableName;
                            bool hasData = false;
                            foreach (var map in Mappings)
                            {
                                if (!string.IsNullOrEmpty(map.SelectedExcelHeader) && headerIndex.ContainsKey(map.SelectedExcelHeader))
                                {
                                    string val = ws.Cells[r, headerIndex[map.SelectedExcelHeader]].Text;
                                    row[map.DbColumnName] = val;
                                    if (!string.IsNullOrWhiteSpace(val)) hasData = true;
                                }
                            }
                            if (hasData) fullData.Rows.Add(row);
                        }
                    }
                });

                await _dataRepository.ClearHierarchyMapForTableAsync(SelectedTableName);
                var result = await _dataRepository.ImportHierarchyMapAsync(fullData);

                if (result.Success) MessageBox.Show("Hierarchy imported successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                else MessageBox.Show($"Import failed: {result.ErrorMessage}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex) { MessageBox.Show($"Critical Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { IsImporting = false; }
        }
    }

    public class MappingItem : ViewModelBase
    {
        public string DbColumnName { get; set; }
        private string _selectedExcelHeader;
        public string SelectedExcelHeader { get => _selectedExcelHeader; set => SetProperty(ref _selectedExcelHeader, value); }
    }
}