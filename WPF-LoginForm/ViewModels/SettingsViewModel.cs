using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using WPF_LoginForm.Services.Database; // For DbConnectionFactory and DatabaseType

namespace WPF_LoginForm.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private DatabaseType _selectedDatabaseType;
        private string _statusMessage;

        public IEnumerable<DatabaseType> DatabaseTypes => Enum.GetValues(typeof(DatabaseType)).Cast<DatabaseType>();

        public DatabaseType SelectedDatabaseType
        {
            get => _selectedDatabaseType;
            set => SetProperty(ref _selectedDatabaseType, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ICommand SaveCommand { get; }

        public SettingsViewModel()
        {
            // Load current setting
            SelectedDatabaseType = DbConnectionFactory.CurrentDatabaseType;
            SaveCommand = new ViewModelCommand(ExecuteSaveCommand);
        }

        private void ExecuteSaveCommand(object obj)
        {
            try
            {
                // Update the global factory setting
                DbConnectionFactory.CurrentDatabaseType = SelectedDatabaseType;

                StatusMessage = $"Successfully switched to {SelectedDatabaseType}.";

                // Optional: Trigger a re-login or app restart message if connection strings are vastly different
                MessageBox.Show($"Database switched to {SelectedDatabaseType}.\n\nPlease ensure your connection string in DbConnectionFactory.cs is correct for this provider.",
                                "Settings Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }
    }
}