// ViewModels/CategoryConfigViewModel.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using OfficeOpenXml;
using WPF_LoginForm.Models;
using WPF_LoginForm.Services;
using WPF_LoginForm.Properties;

namespace WPF_LoginForm.ViewModels
{
    public class CategoryConfigViewModel : ViewModelBase
    {
        private readonly CategoryMappingService _service;
        private readonly IDialogService _dialogService;

        public ObservableCollection<CategoryRule> Rules { get; set; }

        private CategoryRule _selectedRule;

        public CategoryRule SelectedRule
        {
            get => _selectedRule;
            set => SetProperty(ref _selectedRule, value);
        }

        public ICommand AddCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand ImportCommand { get; }
        public ICommand ExportCommand { get; }

        public Action CloseAction { get; set; }

        public CategoryConfigViewModel()
        {
            _service = new CategoryMappingService();
            _dialogService = new DialogService();

            var loaded = _service.LoadRules();
            Rules = new ObservableCollection<CategoryRule>(loaded);

            AddCommand = new ViewModelCommand(ExecuteAdd);
            DeleteCommand = new ViewModelCommand(ExecuteDelete, (o) => SelectedRule != null);
            SaveCommand = new ViewModelCommand(ExecuteSave);
            CloseCommand = new ViewModelCommand(p => CloseAction?.Invoke());
            ImportCommand = new ViewModelCommand(ExecuteImport);
            ExportCommand = new ViewModelCommand(ExecuteExport);
        }

        private void ExecuteAdd(object obj)
        {
            var newRule = new CategoryRule { StartsWith = "", MapTo = "" };
            Rules.Add(newRule);
            SelectedRule = newRule;
        }

        private void ExecuteDelete(object obj)
        {
            if (SelectedRule != null)
            {
                Rules.Remove(SelectedRule);
            }
        }

        private void ExecuteSave(object obj)
        {
            var validRules = Rules
                .Where(r => !string.IsNullOrWhiteSpace(r.StartsWith) && !string.IsNullOrWhiteSpace(r.MapTo))
                .ToList();

            _service.SaveRules(validRules);

            MessageBox.Show(Resources.Msg_RulesSaved, Resources.Title_Saved, MessageBoxButton.OK, MessageBoxImage.Information);
            CloseAction?.Invoke();
        }

        private void ExecuteExport(object obj)
        {
            if (_dialogService.ShowSaveFileDialog(Resources.Str_ExportRulesDialog, "CategoryRules", ".xlsx", "Excel Files|*.xlsx", out string path))
            {
                try
                {
                    ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                    using (var package = new ExcelPackage(new FileInfo(path)))
                    {
                        var ws = package.Workbook.Worksheets.Add("Rules");
                        
                        ws.Cells[1, 1].Value = Resources.Str_StartsWithRaw;
                        ws.Cells[1, 2].Value = Resources.Str_GroupAsCategory;
                        ws.Cells[1, 1, 1, 2].Style.Font.Bold = true;

                        int row = 2;
                        foreach (var rule in Rules)
                        {
                            ws.Cells[row, 1].Value = rule.StartsWith;
                            ws.Cells[row, 2].Value = rule.MapTo;
                            row++;
                        }
                        ws.Cells.AutoFitColumns();
                        package.Save();
                    }
                    MessageBox.Show(Resources.Msg_ExportSuccess, Resources.Title_Success, MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"{Resources.Msg_ExportFailed} {ex.Message}", Resources.Title_Error, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExecuteImport(object obj)
        {
            if (_dialogService.ShowOpenFileDialog(Resources.Str_ImportRulesDialog, "Excel Files|*.xlsx", out string path))
            {
                try
                {
                    ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                    using (var package = new ExcelPackage(new FileInfo(path)))
                    {
                        var ws = package.Workbook.Worksheets.FirstOrDefault();
                        if (ws == null || ws.Dimension == null) return;

                        var newRules = new List<CategoryRule>();
                        int startRow = 2;

                        for (int row = startRow; row <= ws.Dimension.End.Row; row++)
                        {
                            string startsWith = ws.Cells[row, 1].Text;
                            string mapTo = ws.Cells[row, 2].Text;

                            if (!string.IsNullOrWhiteSpace(startsWith) && !string.IsNullOrWhiteSpace(mapTo))
                            {
                                newRules.Add(new CategoryRule { StartsWith = startsWith, MapTo = mapTo });
                            }
                        }

                        if (newRules.Any())
                        {
                            Rules.Clear();
                            foreach (var r in newRules) Rules.Add(r);
                            
                            string successMsg = string.Format(Resources.Msg_ImportedRules, newRules.Count);
                            MessageBox.Show(successMsg, Resources.Title_Success, MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"{Resources.Msg_ImportFailed} {ex.Message}", Resources.Title_Error, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
