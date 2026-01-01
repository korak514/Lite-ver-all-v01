using System;
using System.Collections.Generic;
using System.Collections.ObjectModel; // Required for ObservableCollection
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Security;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Npgsql;
using WPF_LoginForm.Properties;
using WPF_LoginForm.Services;
using WPF_LoginForm.Services.Database;
using WPF_LoginForm.Models;
using WPF_LoginForm.Repositories;

namespace WPF_LoginForm.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        // --- Private Fields ---
        private DatabaseType _selectedDatabaseType;

        private string _statusMessage;
        private bool _isBusy;
        private string _selectedLanguage;

        // DB UI Fields
        private string _dbHost;

        private string _dbPort;
        private string _dbUser;
        private SecureString _dbPassword;
        private bool _useWindowsAuth;

        private string _authDbName = "LoginDb";
        private string _dataDbName = "MainDataDb";

        // Raw Strings
        private string _sqlAuthString;

        private string _sqlDataString;
        private string _postgresAuthString;
        private string _postgresDataString;

        // Auto Import Fields
        private bool _autoImportEnabled;

        private bool _importIsRelative;
        private string _importFileName;
        private string _importAbsolutePath;

        // Dashboard Config Fields
        private bool _showDashboardDateFilter;

        private int _dashboardDateTickSize;

        // User Management Fields
        private ObservableCollection<UserModel> _users;

        private string _newUserUsername;
        private string _newUserName;
        private string _newUserLastName;
        private string _newUserEmail;
        private SecureString _newUserPassword;

        // NEW: Role Fields
        private string _newUserRole;

        private readonly IUserRepository _userRepository;

        // --- Properties ---
        public IEnumerable<DatabaseType> DatabaseTypes => Enum.GetValues(typeof(DatabaseType)).Cast<DatabaseType>();

        public Dictionary<string, string> Languages { get; } = new Dictionary<string, string> { { "English", "en-US" }, { "Türkçe", "tr-TR" } };

        public DatabaseType SelectedDatabaseType
        {
            get => _selectedDatabaseType;
            set { if (SetProperty(ref _selectedDatabaseType, value)) LoadFromCurrentProvider(); }
        }

        public string SelectedLanguage { get => _selectedLanguage; set => SetProperty(ref _selectedLanguage, value); }
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        // DB Binding
        public string DbHost { get => _dbHost; set => SetProperty(ref _dbHost, value); }

        public string DbPort { get => _dbPort; set => SetProperty(ref _dbPort, value); }
        public string DbUser { get => _dbUser; set => SetProperty(ref _dbUser, value); }
        public SecureString DbPassword { get => _dbPassword; set => SetProperty(ref _dbPassword, value); }

        public bool UseWindowsAuth
        {
            get => _useWindowsAuth;
            set { SetProperty(ref _useWindowsAuth, value); OnPropertyChanged(nameof(IsCredentialsEnabled)); }
        }

        public bool IsCredentialsEnabled => !UseWindowsAuth;
        public bool IsWindowsAuthVisible => SelectedDatabaseType == DatabaseType.SqlServer;

        // Auto Import Properties
        public bool AutoImportEnabled
        { get => _autoImportEnabled; set { SetProperty(ref _autoImportEnabled, value); OnPropertyChanged(nameof(IsImportConfigEnabled)); } }

        public bool IsImportConfigEnabled => AutoImportEnabled;
        public bool ImportIsRelative
        { get => _importIsRelative; set { SetProperty(ref _importIsRelative, value); OnPropertyChanged(nameof(IsRelativeInputVisible)); OnPropertyChanged(nameof(IsAbsoluteInputVisible)); } }
        public bool ImportIsAbsolute
        { get => !ImportIsRelative; set { ImportIsRelative = !value; } }
        public bool IsRelativeInputVisible => ImportIsRelative;
        public bool IsAbsoluteInputVisible => !ImportIsRelative;
        public string ImportFileName { get => _importFileName; set => SetProperty(ref _importFileName, value); }
        public string ImportAbsolutePath { get => _importAbsolutePath; set => SetProperty(ref _importAbsolutePath, value); }

        // Dashboard Config Properties
        public bool ShowDashboardDateFilter { get => _showDashboardDateFilter; set => SetProperty(ref _showDashboardDateFilter, value); }

        public int DashboardDateTickSize { get => _dashboardDateTickSize; set => SetProperty(ref _dashboardDateTickSize, value); }

        // User Management Properties
        public ObservableCollection<UserModel> Users { get => _users; set => SetProperty(ref _users, value); }

        public string NewUserUsername { get => _newUserUsername; set => SetProperty(ref _newUserUsername, value); }
        public string NewUserName { get => _newUserName; set => SetProperty(ref _newUserName, value); }
        public string NewUserLastName { get => _newUserLastName; set => SetProperty(ref _newUserLastName, value); }
        public string NewUserEmail { get => _newUserEmail; set => SetProperty(ref _newUserEmail, value); }
        public SecureString NewUserPassword { get => _newUserPassword; set => SetProperty(ref _newUserPassword, value); }

        // NEW: Role Selection
        public string NewUserRole { get => _newUserRole; set => SetProperty(ref _newUserRole, value); }

        // NEW: List of Roles to populate ComboBox
        public List<string> AvailableRoles { get; } = new List<string> { "User", "Admin" };

        // --- Commands ---
        public ICommand SaveCommand { get; }

        public ICommand TestConnectionCommand { get; }
        public ICommand BrowseImportFileCommand { get; }
        public ICommand LoadUsersCommand { get; }
        public ICommand AddUserCommand { get; }
        public ICommand DeleteUserCommand { get; }

        public SettingsViewModel()
        {
            _userRepository = new UserRepository();
            Users = new ObservableCollection<UserModel>();

            _sqlAuthString = Settings.Default.SqlAuthConnString;
            _sqlDataString = Settings.Default.SqlDataConnString;
            _postgresAuthString = Settings.Default.PostgresAuthConnString;
            _postgresDataString = Settings.Default.PostgresDataConnString;
            SelectedDatabaseType = DbConnectionFactory.CurrentDatabaseType;

            string currentLang = Settings.Default.AppLanguage;
            SelectedLanguage = string.IsNullOrEmpty(currentLang) ? "en-US" : currentLang;

            AutoImportEnabled = Settings.Default.AutoImportEnabled;
            ImportIsRelative = Settings.Default.ImportIsRelative;
            ImportFileName = Settings.Default.ImportFileName;
            ImportAbsolutePath = Settings.Default.ImportAbsolutePath;
            ShowDashboardDateFilter = Settings.Default.ShowDashboardDateFilter;
            DashboardDateTickSize = Settings.Default.DashboardDateTickSize;
            if (DashboardDateTickSize < 1) DashboardDateTickSize = 1;

            // Default new user role
            NewUserRole = "User";

            LoadFromCurrentProvider();

            SaveCommand = new ViewModelCommand(ExecuteSaveCommand, (o) => !IsBusy);
            TestConnectionCommand = new ViewModelCommand(ExecuteTestConnection, (o) => !IsBusy);
            BrowseImportFileCommand = new ViewModelCommand(ExecuteBrowseImportFile);
            LoadUsersCommand = new ViewModelCommand(ExecuteLoadUsers);
            AddUserCommand = new ViewModelCommand(ExecuteAddUser);
            DeleteUserCommand = new ViewModelCommand(ExecuteDeleteUser);
        }

        private void ExecuteBrowseImportFile(object obj)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Filter = "Dashboard JSON|*.json|All files|*.*";
            if (dialog.ShowDialog() == true) { ImportAbsolutePath = dialog.FileName; }
        }

        private void LoadFromCurrentProvider()
        {
            try
            {
                string raw = (SelectedDatabaseType == DatabaseType.SqlServer) ? _sqlAuthString : _postgresAuthString;
                if (SelectedDatabaseType == DatabaseType.SqlServer)
                {
                    var builder = new SqlConnectionStringBuilder(raw);
                    DbHost = builder.DataSource; DbUser = builder.UserID;
                    var tempPass = builder.Password; if (!string.IsNullOrEmpty(tempPass)) DbPassword = new NetworkCredential("", tempPass).SecurePassword;
                    UseWindowsAuth = builder.IntegratedSecurity; DbPort = "";
                }
                else
                {
                    var builder = new NpgsqlConnectionStringBuilder(raw);
                    DbHost = builder.Host; DbPort = builder.Port.ToString(); DbUser = builder.Username;
                    var tempPass = builder.Password; if (!string.IsNullOrEmpty(tempPass)) DbPassword = new NetworkCredential("", tempPass).SecurePassword;
                    UseWindowsAuth = false;
                }
                OnPropertyChanged(nameof(IsWindowsAuthVisible));
            }
            catch { DbHost = "localhost"; DbUser = "admin"; }
        }

        private void RebuildConnectionStrings()
        {
            string password = (DbPassword != null) ? new NetworkCredential("", DbPassword).Password : "";
            if (SelectedDatabaseType == DatabaseType.SqlServer)
            {
                var builder = new SqlConnectionStringBuilder();
                builder.DataSource = DbHost + (string.IsNullOrEmpty(DbPort) ? "" : "," + DbPort);
                builder.IntegratedSecurity = UseWindowsAuth; builder.TrustServerCertificate = true;
                builder.PersistSecurityInfo = true;
                if (!UseWindowsAuth) { builder.UserID = DbUser; builder.Password = password; }
                builder.InitialCatalog = _authDbName; _sqlAuthString = builder.ConnectionString;
                builder.InitialCatalog = _dataDbName; _sqlDataString = builder.ConnectionString;
            }
            else
            {
                var builder = new NpgsqlConnectionStringBuilder();
                builder.Host = DbHost; if (int.TryParse(DbPort, out int port)) builder.Port = port;
                builder.Username = DbUser; builder.Password = password;
                builder.PersistSecurityInfo = true;
                builder.Database = _authDbName; _postgresAuthString = builder.ConnectionString;
                builder.Database = _dataDbName; _postgresDataString = builder.ConnectionString;
            }
        }

        private void ExecuteSaveCommand(object obj)
        {
            try
            {
                RebuildConnectionStrings();
                DbConnectionFactory.CurrentDatabaseType = SelectedDatabaseType;
                Settings.Default.SqlAuthConnString = _sqlAuthString;
                Settings.Default.SqlDataConnString = _sqlDataString;
                Settings.Default.PostgresAuthConnString = _postgresAuthString;
                Settings.Default.PostgresDataConnString = _postgresDataString;
                Settings.Default.AppLanguage = SelectedLanguage;
                Settings.Default.AutoImportEnabled = AutoImportEnabled;
                Settings.Default.ImportIsRelative = ImportIsRelative;
                Settings.Default.ImportFileName = ImportFileName;
                Settings.Default.ImportAbsolutePath = ImportAbsolutePath;
                Settings.Default.ShowDashboardDateFilter = ShowDashboardDateFilter;
                Settings.Default.DashboardDateTickSize = DashboardDateTickSize;
                Settings.Default.Save();
                StatusMessage = "Settings saved successfully.";
                MessageBox.Show("Settings have been saved.\n\nPLEASE RESTART THE APPLICATION to apply changes.", "Restart Required", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { StatusMessage = $"Error saving: {ex.Message}"; }
        }

        // --- User Logic ---
        private async void ExecuteLoadUsers(object obj)
        {
            IsBusy = true; StatusMessage = "Loading users...";
            try { var usersList = await Task.Run(() => _userRepository.GetByAll()); Users.Clear(); foreach (var user in usersList) Users.Add(user); StatusMessage = $"Loaded {Users.Count} users."; }
            catch (Exception ex) { StatusMessage = $"Error loading users: {ex.Message}"; }
            finally { IsBusy = false; }
        }

        private async void ExecuteAddUser(object obj)
        {
            if (string.IsNullOrWhiteSpace(NewUserUsername) || NewUserPassword == null || NewUserPassword.Length < 3)
            {
                MessageBox.Show("Username and Password (min 3 chars) are required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            try
            {
                var newUser = new UserModel
                {
                    Username = NewUserUsername,
                    Name = NewUserName ?? "",
                    LastName = NewUserLastName ?? "",
                    Email = NewUserEmail ?? "",
                    // NEW: Pass selected Role
                    Role = NewUserRole ?? "User",
                    Password = new NetworkCredential("", NewUserPassword).Password
                };

                await Task.Run(() => _userRepository.Add(newUser));

                // Clear Form
                NewUserUsername = ""; NewUserName = ""; NewUserLastName = ""; NewUserEmail = ""; NewUserPassword = null;
                NewUserRole = "User"; // Reset role
                OnPropertyChanged(nameof(NewUserPassword));

                StatusMessage = "User added successfully.";
                ExecuteLoadUsers(null);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error adding user: {ex.Message}";
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { IsBusy = false; }
        }

        private async void ExecuteDeleteUser(object obj)
        {
            if (obj is UserModel user) { if (user.Username.Equals("admin", StringComparison.OrdinalIgnoreCase)) { MessageBox.Show("Cannot delete the default 'admin' user.", "Restricted", MessageBoxButton.OK, MessageBoxImage.Stop); return; } if (MessageBox.Show($"Are you sure you want to delete user '{user.Username}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) { IsBusy = true; try { if (int.TryParse(user.Id, out int id)) { await Task.Run(() => _userRepository.Remove(id)); StatusMessage = $"User '{user.Username}' deleted."; ExecuteLoadUsers(null); } } catch (Exception ex) { StatusMessage = $"Error deleting user: {ex.Message}"; } finally { IsBusy = false; } } }
        }

        // --- Connection Tester ---
        private async void ExecuteTestConnection(object obj)
        {
            IsBusy = true; RebuildConnectionStrings(); StatusMessage = $"Testing {SelectedDatabaseType} connections..."; await Task.Delay(100);
            bool authSuccess = false; string authError = ""; bool dataSuccess = false; string dataError = "";
            await Task.Run(() =>
            {
                string authStr = (SelectedDatabaseType == DatabaseType.SqlServer) ? _sqlAuthString : _postgresAuthString;
                string dataStr = (SelectedDatabaseType == DatabaseType.SqlServer) ? _sqlDataString : _postgresDataString;
                (authSuccess, authError) = TestSingleConnection(authStr, SelectedDatabaseType);
                (dataSuccess, dataError) = TestSingleConnection(dataStr, SelectedDatabaseType);
            });
            IsBusy = false;

            if (authSuccess && dataSuccess)
            {
                bool authMissing = authError.Contains("missing");
                bool dataMissing = dataError.Contains("missing");

                if (authMissing || dataMissing)
                {
                    StatusMessage = "⚠️ SERVER FOUND. DATABASES MISSING.";
                    MessageBox.Show("Configuration Valid!\n\nThe Server is reachable and credentials are correct.\nThe database files ('LoginDb' / 'MainDataDb') do not exist yet.\n\nPLEASE SAVE AND RESTART to allow the app to create them automatically.", "Server Connected", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    StatusMessage = "✅ SUCCESS! Both connections are valid.";
                    MessageBox.Show("Connection Test Successful!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                StatusMessage = "❌ CONNECTION FAILED.";
                MessageBox.Show($"[Auth]: {authError}\n\n[Data]: {dataError}", "Connection Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private (bool success, string error) TestSingleConnection(string connString, DatabaseType type)
        {
            if (string.IsNullOrWhiteSpace(connString)) return (false, "Empty string.");
            try
            {
                using (IDbConnection conn = (type == DatabaseType.SqlServer) ? (IDbConnection)new SqlConnection(connString) : new NpgsqlConnection(connString))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand()) { cmd.CommandText = "SELECT 1"; cmd.ExecuteScalar(); }
                }
                return (true, "");
            }
            catch (Exception ex)
            {
                bool isMissingDb = false;
                if (ex is SqlException sqlEx && sqlEx.Number == 4060) isMissingDb = true;
                if (ex is PostgresException pgEx && pgEx.SqlState == "3D000") isMissingDb = true;

                if (isMissingDb)
                {
                    try
                    {
                        string systemDbName = (type == DatabaseType.SqlServer) ? "master" : "postgres";
                        var builder = (type == DatabaseType.SqlServer) ? (System.Data.Common.DbConnectionStringBuilder)new SqlConnectionStringBuilder(connString) : new NpgsqlConnectionStringBuilder(connString);
                        if (type == DatabaseType.SqlServer) ((SqlConnectionStringBuilder)builder).InitialCatalog = systemDbName;
                        else ((NpgsqlConnectionStringBuilder)builder).Database = systemDbName;

                        using (IDbConnection sysConn = (type == DatabaseType.SqlServer) ? (IDbConnection)new SqlConnection(builder.ConnectionString) : new NpgsqlConnection(builder.ConnectionString))
                        {
                            sysConn.Open();
                        }
                        return (true, "Database missing (Server OK)");
                    }
                    catch { return (false, ex.Message); }
                }
                return (false, ex.Message);
            }
        }
    }
}