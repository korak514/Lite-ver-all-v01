using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using WPF_LoginForm.Properties; // For Settings
using WPF_LoginForm.Services.Database;

namespace WPF_LoginForm.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        // Fields
        private DatabaseType _selectedDatabaseType;
        private string _sqlAuthString;
        private string _sqlDataString;
        private string _postgresAuthString; // New
        private string _postgresDataString; // New
        private string _statusMessage;

        // Properties
        public IEnumerable<DatabaseType> DatabaseTypes => Enum.GetValues(typeof(DatabaseType)).Cast<DatabaseType>();

        public DatabaseType SelectedDatabaseType
        {
            get => _selectedDatabaseType;
            set => SetProperty(ref _selectedDatabaseType, value);
        }

        public string SqlAuthString { get => _sqlAuthString; set => SetProperty(ref _sqlAuthString, value); }
        public string SqlDataString { get => _sqlDataString; set => SetProperty(ref _sqlDataString, value); }

        public string PostgresAuthString { get => _postgresAuthString; set => SetProperty(ref _postgresAuthString, value); }
        public string PostgresDataString { get => _postgresDataString; set => SetProperty(ref _postgresDataString, value); }

        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        // Commands
        public ICommand SaveCommand { get; }
        public ICommand TestConnectionCommand { get; }

        public SettingsViewModel()
        {
            LoadSettings();
            SaveCommand = new ViewModelCommand(ExecuteSaveCommand);
            TestConnectionCommand = new ViewModelCommand(ExecuteTestConnection);
        }

        private void LoadSettings()
        {
            // Load from Properties.Settings
            SelectedDatabaseType = DbConnectionFactory.CurrentDatabaseType;
            SqlAuthString = Settings.Default.SqlAuthConnString;
            SqlDataString = Settings.Default.SqlDataConnString;

            PostgresAuthString = Settings.Default.PostgresAuthConnString;
            PostgresDataString = Settings.Default.PostgresDataConnString;
        }

        private void ExecuteSaveCommand(object obj)
        {
            try
            {
                // Save to Properties.Settings
                DbConnectionFactory.CurrentDatabaseType = SelectedDatabaseType;

                Settings.Default.SqlAuthConnString = SqlAuthString;
                Settings.Default.SqlDataConnString = SqlDataString;

                Settings.Default.PostgresAuthConnString = PostgresAuthString;
                Settings.Default.PostgresDataConnString = PostgresDataString;

                Settings.Default.Save(); // Persist to disk

                StatusMessage = "Settings saved successfully.";
                MessageBox.Show("Settings have been saved. \n\nIf you changed the Database Type, please restart the application to ensure all services reload correctly.", "Settings Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving: {ex.Message}";
            }
        }

        private void ExecuteTestConnection(object obj)
        {
            StatusMessage = "Testing Auth connection...";
            try
            {
                IDbConnection conn = null;

                // Test the Auth connection of the selected provider
                if (SelectedDatabaseType == DatabaseType.SqlServer)
                    conn = new System.Data.SqlClient.SqlConnection(SqlAuthString);
                else
                    conn = new Npgsql.NpgsqlConnection(PostgresAuthString);

                using (conn)
                {
                    conn.Open();
                }
                StatusMessage = $"Success! Connected to {SelectedDatabaseType} (Auth DB).";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Connection Failed: {ex.Message}";
            }
        }
    }
}