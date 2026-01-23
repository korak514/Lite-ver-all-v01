using System.Collections.Generic;
using System.Data;
using WPF_LoginForm.Models;
using WPF_LoginForm.ViewModels;

namespace WPF_LoginForm.Services
{
    public interface IDialogService
    {
        bool ShowAddRowDialog(IEnumerable<string> columnNames, string tableName,
                              Dictionary<string, object> initialValues,
                              DataTable sourceTable,
                              bool hideId,
                              out NewRowData newRowData);

        bool ShowAddRowLongDialog(AddRowLongViewModel viewModel, out NewRowData newRowData);

        bool ShowConfigurationDialog(ConfigurationViewModel viewModel);

        bool ShowImportTableDialog(ImportTableViewModel viewModel, out ImportSettings settings);

        void ShowCreateTableDialog(CreateTableViewModel viewModel);

        // --- NEW: Hierarchy Importer ---
        void ShowHierarchyImportDialog(HierarchyImportViewModel viewModel);

        // -------------------------------

        bool ShowConfirmationDialog(string title, string message);

        bool ShowSaveFileDialog(string title, string defaultFileName, string defaultExtension, string filter, out string selectedFilePath);

        bool ShowOpenFileDialog(string title, string filter, out string selectedFilePath);

        // Add to interface
        bool ShowInputDialog(string title, string message, string defaultValue, out string result);
    }
}