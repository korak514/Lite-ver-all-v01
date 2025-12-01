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
        private string _postgresAuthString;
        private string _postgresDataString;
        private string _statusMessage;

        // Language Fields
        private string _selectedLanguage;

        public IEnumerable<DatabaseType> DatabaseTypes => Enum.GetValues(typeof(DatabaseType)).Cast<DatabaseType>();

        // Dictionary for Language ComboBox (Display Name -> Culture Code)
        public Dictionary<string, string> Languages { get; } = new Dictionary<string, string>
        {
            { "English", "en-US" },
            { "Türkçe", "tr-TR" }
        };

        public DatabaseType SelectedDatabaseType
        {
            get => _selectedDatabaseType;
            set => SetProperty(ref _selectedDatabaseType, value);
        }

        public string SelectedLanguage
        {
            get => _selectedLanguage;
            set => SetProperty(ref _selectedLanguage, value);
        }

        public string SqlAuthString { get => _sqlAuthString; set => SetProperty(ref _sqlAuthString, value); }
        public string SqlDataString { get => _sqlDataString; set => SetProperty(ref _sqlDataString, value); }
        public string PostgresAuthString { get => _postgresAuthString; set => SetProperty(ref _postgresAuthString, value); }
        public string PostgresDataString { get => _postgresDataString; set => SetProperty(ref _postgresDataString, value); }
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

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
            SelectedDatabaseType = DbConnectionFactory.CurrentDatabaseType;
            SqlAuthString = Settings.Default.SqlAuthConnString;
            SqlDataString = Settings.Default.SqlDataConnString;
            PostgresAuthString = Settings.Default.PostgresAuthConnString;
            PostgresDataString = Settings.Default.PostgresDataConnString;

            // Load Language (Default to 'en-US' if empty)
            string currentLang = Settings.Default.AppLanguage;
            SelectedLanguage = string.IsNullOrEmpty(currentLang) ? "en-US" : currentLang;
        }

        private void ExecuteSaveCommand(object obj)
        {
            try
            {
                // Save DB Selection
                DbConnectionFactory.CurrentDatabaseType = SelectedDatabaseType;

                // Save Connection Strings
                Settings.Default.SqlAuthConnString = SqlAuthString;
                Settings.Default.SqlDataConnString = SqlDataString;
                Settings.Default.PostgresAuthConnString = PostgresAuthString;
                Settings.Default.PostgresDataConnString = PostgresDataString;

                // Save Language
                Settings.Default.AppLanguage = SelectedLanguage;

                Settings.Default.Save();

                StatusMessage = "Settings saved successfully.";
                MessageBox.Show("Settings have been saved. \n\nPLEASE RESTART THE APPLICATION for Language and Database changes to take full effect.", "Restart Required", MessageBoxButton.OK, MessageBoxImage.Information);
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
                if (SelectedDatabaseType == DatabaseType.SqlServer)
                    conn = new System.Data.SqlClient.SqlConnection(SqlAuthString);
                else
                    conn = new Npgsql.NpgsqlConnection(PostgresAuthString);

                using (conn) { conn.Open(); }
                StatusMessage = $"Success! Connected to {SelectedDatabaseType} (Auth DB).";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Connection Failed: {ex.Message}";
            }
        }
    }
}