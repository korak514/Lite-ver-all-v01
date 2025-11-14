
using System.Collections.Generic;
using System.Windows;
using WPF_LoginForm.Models;
using WPF_LoginForm.ViewModels;
using WPF_LoginForm.Views;
using System;
using System.Diagnostics;
using Microsoft.Win32;
using System.Linq;

namespace WPF_LoginForm.Services
{
    public class DialogService : IDialogService
    {
        public bool ShowAddRowDialog(IEnumerable<string> columnNames, string tableName,
                                     Dictionary<string, object> initialValues,
                                     out NewRowData newRowData)
        {
            newRowData = null;
            try
            {
                var viewModel = new AddRowViewModel(columnNames, tableName, initialValues);
                var dialogWindow = new AddRowWindow { DataContext = viewModel };

                Window ownerWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
                if (ownerWindow != null && ownerWindow != dialogWindow)
                {
                    dialogWindow.Owner = ownerWindow;
                }
                else
                {
                    dialogWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }

                bool? result = dialogWindow.ShowDialog();
                if (result == true)
                {
                    newRowData = viewModel.GetEnteredData();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing Add Row dialog: {ex.ToString()}");
                MessageBox.Show($"Error opening Add Row dialog:\n{ex.Message}", "Dialog Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public bool ShowAddRowLongDialog(AddRowLongViewModel viewModel, out NewRowData newRowData)
        {
            newRowData = null;
            try
            {
                var dialogWindow = new AddRowLongWindow { DataContext = viewModel };

                Window ownerWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
                if (ownerWindow != null && ownerWindow != dialogWindow)
                {
                    dialogWindow.Owner = ownerWindow;
                }
                else
                {
                    dialogWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }

                bool? result = dialogWindow.ShowDialog();
                if (result == true)
                {
                    newRowData = viewModel.GetEnteredData(out List<string> validationErrors);
                    if (validationErrors != null && validationErrors.Any())
                    {
                        Debug.WriteLine($"Validation errors from AddRowLongDialog: {string.Join(", ", validationErrors)}");
                        return false;
                    }
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing Add Row Long dialog: {ex.ToString()}");
                MessageBox.Show($"Error opening detailed Add Row dialog:\n{ex.Message}", "Dialog Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public bool ShowConfigurationDialog(ConfigurationViewModel viewModel)
        {
            try
            {
                var configWindow = new ConfigurationWindow { DataContext = viewModel };

                Window ownerWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
                if (ownerWindow != null && ownerWindow != configWindow)
                {
                    configWindow.Owner = ownerWindow;
                }
                else
                {
                    configWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }

                viewModel.CloseAction = () => configWindow.Close();
                configWindow.ShowDialog();
                return viewModel.WasApplied;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing Configuration dialog: {ex.ToString()}");
                MessageBox.Show($"Error opening Configuration dialog:\n{ex.Message}", "Dialog Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public bool ShowImportTableDialog(ImportTableViewModel viewModel, out ImportSettings settings)
        {
            settings = null;
            try
            {
                var dialogWindow = new ImportTableWindow { DataContext = viewModel };

                Window ownerWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
                if (ownerWindow != null && ownerWindow != dialogWindow)
                {
                    dialogWindow.Owner = ownerWindow;
                }
                else
                {
                    dialogWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }

                if (dialogWindow.ShowDialog() == true)
                {
                    settings = new ImportSettings
                    {
                        FilePath = viewModel.FilePath,
                        RowsToIgnore = viewModel.RowsToIgnore
                    };
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing Import Table dialog: {ex.ToString()}");
                MessageBox.Show($"Error opening Import Table dialog:\n{ex.Message}", "Dialog Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // --- NEW METHOD IMPLEMENTATION ---
        public void ShowCreateTableDialog(CreateTableViewModel viewModel)
        {
            try
            {
                var dialogWindow = new CreateTableWindow { DataContext = viewModel };

                Window ownerWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
                if (ownerWindow != null && ownerWindow != dialogWindow)
                {
                    dialogWindow.Owner = ownerWindow;
                }
                else
                {
                    dialogWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }

                dialogWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing Create Table dialog: {ex.ToString()}");
                MessageBox.Show($"Error opening Create Table dialog:\n{ex.Message}", "Dialog Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public bool ShowConfirmationDialog(string title, string message)
        {
            Window owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);

            MessageBoxResult result = owner != null
                ? MessageBox.Show(owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
                : MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

            return result == MessageBoxResult.Yes;
        }

        public bool ShowSaveFileDialog(string title, string defaultFileName, string defaultExtension, string filter, out string selectedFilePath)
        {
            selectedFilePath = null;
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Title = title,
                FileName = defaultFileName,
                DefaultExt = defaultExtension,
                Filter = filter,
                AddExtension = true
            };

            bool? result = saveFileDialog.ShowDialog();

            if (result == true)
            {
                selectedFilePath = saveFileDialog.FileName;
                return true;
            }
            return false;
        }

        public bool ShowOpenFileDialog(string title, string filter, out string selectedFilePath)
        {
            selectedFilePath = null;
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = title,
                Filter = filter,
                CheckFileExists = true,
                CheckPathExists = true
            };

            bool? result = openFileDialog.ShowDialog();

            if (result == true)
            {
                selectedFilePath = openFileDialog.FileName;
                return true;
            }
            return false;
        }
    }
}