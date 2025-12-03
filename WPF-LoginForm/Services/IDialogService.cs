using System.Collections.Generic;
using System.Data; // Added for DataTable
using WPF_LoginForm.Models;
using WPF_LoginForm.ViewModels;

namespace WPF_LoginForm.Services
{
    public interface IDialogService
    {
        // --- MODIFIED: Added sourceTable and hideId ---
        bool ShowAddRowDialog(IEnumerable<string> columnNames, string tableName,
                              Dictionary<string, object> initialValues,
                              DataTable sourceTable,
                              bool hideId,
                              out NewRowData newRowData);

        bool ShowAddRowLongDialog(AddRowLongViewModel viewModel, out NewRowData newRowData);

        bool ShowConfigurationDialog(ConfigurationViewModel viewModel);

        bool ShowImportTableDialog(ImportTableViewModel viewModel, out ImportSettings settings);

        void ShowCreateTableDialog(CreateTableViewModel viewModel);

        bool ShowConfirmationDialog(string title, string message);

        bool ShowSaveFileDialog(string title, string defaultFileName, string defaultExtension, string filter, out string selectedFilePath);

        bool ShowOpenFileDialog(string title, string filter, out string selectedFilePath);
    }
}