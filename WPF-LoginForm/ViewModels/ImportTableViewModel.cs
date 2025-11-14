// In WPF_LoginForm.ViewModels/ImportTableViewModel.cs
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using WPF_LoginForm.Services; // Required for IDialogService

namespace WPF_LoginForm.ViewModels
{
    /// <summary>
    /// ViewModel for the advanced import table dialog.
    /// Manages the settings for importing a new table from a file.
    /// </summary>
    public class ImportTableViewModel : ViewModelBase
    {
        private readonly IDialogService _dialogService;

        private string _filePath;
        /// <summary>
        /// The full path of the file selected for import.
        /// </summary>
        public string FilePath
        {
            get => _filePath;
            set => SetProperty(ref _filePath, value);
        }

        private int _rowsToIgnore;
        /// <summary>
        /// The number of rows to skip at the beginning of the file.
        /// </summary>
        public int RowsToIgnore
        {
            get => _rowsToIgnore;
            set => SetProperty(ref _rowsToIgnore, value);
        }

        /// <summary>
        /// Provides the list of options (0 through 8) for the "Rows to Ignore" ComboBox.
        /// </summary>
        public List<int> AvailableRowCounts { get; }

        private string _windowTitle;
        public string WindowTitle
        {
            get => _windowTitle;
            set => SetProperty(ref _windowTitle, value);
        }

        // --- NEW COMMAND ---
        public ICommand BrowseCommand { get; }

        /// <summary>
        /// Constructor for the ImportTableViewModel.
        /// </summary>
        public ImportTableViewModel(string tableName, IDialogService dialogService)
        {
            _dialogService = dialogService;
            WindowTitle = $"Advanced Import for '{tableName}'";

            AvailableRowCounts = Enumerable.Range(0, 9).ToList();
            RowsToIgnore = 0;

            // Initialize the command
            BrowseCommand = new ViewModelCommand(ExecuteBrowseCommand);
        }

        private void ExecuteBrowseCommand(object parameter)
        {
            string filter = "Excel/CSV|*.xlsx;*.csv|All files (*.*)|*.*";
            if (_dialogService.ShowOpenFileDialog("Select a file to import", filter, out string selectedFilePath))
            {
                FilePath = selectedFilePath;
            }
        }
    }
}