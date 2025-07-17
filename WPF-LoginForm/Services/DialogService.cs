using System.Collections.Generic;
using System.Windows;
using WPF_LoginForm.Models;
using WPF_LoginForm.ViewModels;
using WPF_LoginForm.Views;
using System;
using System.Diagnostics;
using Microsoft.Win32;

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

                Window mainWindow = Application.Current?.MainWindow;
                if (mainWindow != null && mainWindow.IsVisible)
                {
                    dialogWindow.Owner = mainWindow;
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

        public bool ShowConfirmationDialog(string title, string message)
        {
            MessageBoxResult result = MessageBox.Show(
                message, title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
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

        // --- NEW METHOD IMPLEMENTATION ---
        public bool ShowOpenFileDialog(string title, string filter, out string selectedFilePath)
        {
            selectedFilePath = null;
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = title,
                Filter = filter,
                CheckFileExists = true, // Ensure the file selected actually exists
                CheckPathExists = true  // Ensure the path exists
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