using System.Collections.Generic;
using WPF_LoginForm.Models;

namespace WPF_LoginForm.Services
{
    public interface IDialogService
    {
        bool ShowAddRowDialog(IEnumerable<string> columnNames, string tableName,
                              Dictionary<string, object> initialValues,
                              out NewRowData newRowData);

        bool ShowConfirmationDialog(string title, string message);

        bool ShowSaveFileDialog(string title, string defaultFileName, string defaultExtension, string filter, out string selectedFilePath);

       
        bool ShowOpenFileDialog(string title, string filter, out string selectedFilePath);
    }
}