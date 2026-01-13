using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
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
using System.IO;

namespace WPF_LoginForm.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        // --- Fields ---
        private DatabaseType _selectedDatabaseType;

        private string _statusMessage;
        private bool _isBusy;
        private string _selectedLanguage;

        private string _dbHost;
        private string _dbPort;
        private string _dbUser;
        private SecureString _dbPassword;
        private bool _useWindowsAuth;

        private int _connectionTimeout = 15;
        private bool _trustServerCertificate = true;
        private string _dbServerName;

        private string _authDbName = "LoginDb";
        private string _dataDbName = "MainDataDb";
        private string _sqlAuthString;
        private string _sqlDataString;
        private string _postgresAuthString;
        private string _postgresDataString;

        private bool _autoImportEnabled;
        private bool _importIsRelative;
        private string _importFileName;
        private string _importAbsolutePath;

        private bool _showDashboardDateFilter;
        private int _dashboardDateTickSize;
        private int _defaultRowLimit;

        private ObservableCollection<UserModel> _users;
        private string _newUserUsername;
        private string _newUserName;
        private string _newUserLastName;
        private string _newUserEmail;
        private SecureString _newUserPassword;
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

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    OnPropertyChanged(nameof(IsDbConfigEditable));
                    OnPropertyChanged(nameof(CanManageUsers));
                    (SaveCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
                    (TestConnectionCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public string DbHost { get => _dbHost; set => SetProperty(ref _dbHost, value); }
        public string DbPort { get => _dbPort; set => SetProperty(ref _dbPort, value); }
        public string DbUser { get => _dbUser; set => SetProperty(ref _dbUser, value); }
        public SecureString DbPassword { get => _dbPassword; set => SetProperty(ref _dbPassword, value); }

        public bool UseWindowsAuth
        { get => _useWindowsAuth; set { SetProperty(ref _useWindowsAuth, value); OnPropertyChanged(nameof(IsCredentialsEnabled)); } }

        public int ConnectionTimeout { get => _connectionTimeout; set => SetProperty(ref _connectionTimeout, value); }
        public bool TrustServerCertificate { get => _trustServerCertificate; set => SetProperty(ref _trustServerCertificate, value); }
        public string DbServerName { get => _dbServerName; set => SetProperty(ref _dbServerName, value); }

        public bool IsCredentialsEnabled => !UseWindowsAuth;
        public bool IsWindowsAuthVisible => SelectedDatabaseType == DatabaseType.SqlServer;
        public bool IsDbConfigEditable => !IsBusy;

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
        public bool ShowDashboardDateFilter { get => _showDashboardDateFilter; set => SetProperty(ref _showDashboardDateFilter, value); }
        public int DashboardDateTickSize { get => _dashboardDateTickSize; set => SetProperty(ref _dashboardDateTickSize, value); }
        public int DefaultRowLimit { get => _defaultRowLimit; set => SetProperty(ref _defaultRowLimit, value); }

        public ObservableCollection<UserModel> Users { get => _users; set => SetProperty(ref _users, value); }
        public string NewUserUsername { get => _newUserUsername; set => SetProperty(ref _newUserUsername, value); }
        public string NewUserName { get => _newUserName; set => SetProperty(ref _newUserName, value); }
        public string NewUserLastName { get => _newUserLastName; set => SetProperty(ref _newUserLastName, value); }
        public string NewUserEmail { get => _newUserEmail; set => SetProperty(ref _newUserEmail, value); }
        public SecureString NewUserPassword { get => _newUserPassword; set => SetProperty(ref _newUserPassword, value); }
        public string NewUserRole { get => _newUserRole; set => SetProperty(ref _newUserRole, value); }
        public List<string> AvailableRoles { get; } = new List<string> { "User", "Admin" };

        public bool CanManageUsers => UserSessionService.IsAdmin || IsBusy;

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

            ConnectionTimeout = Settings.Default.ConnectionTimeout;
            TrustServerCertificate = Settings.Default.TrustServerCertificate;
            DbServerName = Settings.Default.DbServerName;
            DefaultRowLimit = Settings.Default.DefaultRowLimit;
            if (DefaultRowLimit < 0) DefaultRowLimit = 500;

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
            var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Select file in target folder", Filter = "All files|*.*", CheckFileExists = true };
            if (dialog.ShowDialog() == true) ImportAbsolutePath = Path.GetDirectoryName(dialog.FileName);
        }

        private void ExecuteSaveCommand(object obj)
        {
            if (!int.TryParse(DbPort, out int portNum) || portNum < 0 || portNum > 65535)
            {
                MessageBox.Show("Please enter a valid Port number (0-65535).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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

                Settings.Default.ConnectionTimeout = ConnectionTimeout;
                Settings.Default.TrustServerCertificate = TrustServerCertificate;
                Settings.Default.DbServerName = DbServerName;

                if (DefaultRowLimit < 0) DefaultRowLimit = 500;
                Settings.Default.DefaultRowLimit = DefaultRowLimit;

                Settings.Default.DbHost = DbHost;
                Settings.Default.DbPort = DbPort;
                Settings.Default.DbUser = DbUser;

                Settings.Default.Save();

                StatusMessage = Resources.Msg_SettingsSavedRestart;
                MessageBox.Show(Resources.Msg_SettingsSavedRestart, "Restart Required", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { StatusMessage = $"Error saving: {ex.Message}"; }
        }

        private void LoadFromCurrentProvider()
        {
            try
            {
                string raw = (SelectedDatabaseType == DatabaseType.SqlServer) ? _sqlAuthString : _postgresAuthString;

                if (SelectedDatabaseType == DatabaseType.SqlServer)
                {
                    var builder = new SqlConnectionStringBuilder(raw);
                    DbHost = builder.DataSource;
                    if (DbHost.Contains(","))
                    {
                        var parts = DbHost.Split(',');
                        DbHost = parts[0];
                        if (parts.Length > 1) DbPort = parts[1];
                    }
                    else DbPort = "1433";

                    DbUser = builder.UserID;
                    if (!string.IsNullOrEmpty(builder.Password))
                        DbPassword = new NetworkCredential("", builder.Password).SecurePassword;

                    UseWindowsAuth = builder.IntegratedSecurity;
                    ConnectionTimeout = builder.ConnectTimeout;
                    TrustServerCertificate = builder.TrustServerCertificate;
                }
                else
                {
                    var builder = new NpgsqlConnectionStringBuilder(raw);
                    DbHost = builder.Host;
                    DbPort = builder.Port.ToString();
                    DbUser = builder.Username;
                    if (!string.IsNullOrEmpty(builder.Password))
                        DbPassword = new NetworkCredential("", builder.Password).SecurePassword;

                    UseWindowsAuth = false;
                    ConnectionTimeout = builder.Timeout;
                    TrustServerCertificate = builder.TrustServerCertificate;
                }
                OnPropertyChanged(nameof(IsWindowsAuthVisible));
                OnPropertyChanged(nameof(ConnectionTimeout));
                OnPropertyChanged(nameof(TrustServerCertificate));
            }
            catch
            {
                DbHost = "localhost";
                DbUser = "admin";
            }
        }

        private void RebuildConnectionStrings()
        {
            string password = (DbPassword != null) ? new NetworkCredential("", DbPassword).Password : "";

            if (SelectedDatabaseType == DatabaseType.SqlServer)
            {
                var builder = new SqlConnectionStringBuilder();
                builder.DataSource = DbHost + (string.IsNullOrEmpty(DbPort) ? "" : "," + DbPort);
                builder.IntegratedSecurity = UseWindowsAuth;
                builder.TrustServerCertificate = TrustServerCertificate;
                builder.ConnectTimeout = ConnectionTimeout;
                builder.PersistSecurityInfo = true;

                if (!UseWindowsAuth) { builder.UserID = DbUser; builder.Password = password; }

                builder.InitialCatalog = _authDbName; _sqlAuthString = builder.ConnectionString;
                builder.InitialCatalog = _dataDbName; _sqlDataString = builder.ConnectionString;
            }
            else
            {
                var builder = new NpgsqlConnectionStringBuilder();
                builder.Host = DbHost;
                if (int.TryParse(DbPort, out int port)) builder.Port = port;
                builder.Username = DbUser;
                builder.Password = password;
                builder.TrustServerCertificate = TrustServerCertificate;
                builder.Timeout = ConnectionTimeout;
                builder.PersistSecurityInfo = true;

                builder.Database = _authDbName; _postgresAuthString = builder.ConnectionString;
                builder.Database = _dataDbName; _postgresDataString = builder.ConnectionString;
            }
        }

        private async void ExecuteLoadUsers(object obj)
        {
            IsBusy = true;
            StatusMessage = Resources.Status_Loading;
            try
            {
                var usersList = await Task.Run(() => _userRepository.GetByAll());
                Users.Clear();
                foreach (var user in usersList) Users.Add(user);
                StatusMessage = $"Loaded {Users.Count} users.";
            }
            catch (Exception ex) { StatusMessage = $"Error loading users: {ex.Message}"; }
            finally { IsBusy = false; }
        }

        private async void ExecuteAddUser(object obj)
        {
            if (string.IsNullOrWhiteSpace(NewUserUsername) || NewUserPassword == null || NewUserPassword.Length < 3)
            {
                MessageBox.Show("Username and Password (min 3 chars) required.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    Role = NewUserRole ?? "User",
                    Password = new NetworkCredential("", NewUserPassword).Password
                };

                await Task.Run(() => _userRepository.Add(newUser));

                NewUserUsername = ""; NewUserName = ""; NewUserLastName = ""; NewUserEmail = "";
                NewUserPassword = null; NewUserRole = "User";
                OnPropertyChanged(nameof(NewUserPassword));

                StatusMessage = Resources.Msg_UserAdded;
                ExecuteLoadUsers(null);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { IsBusy = false; }
        }

        private async void ExecuteDeleteUser(object obj)
        {
            if (obj is UserModel user)
            {
                if (user.Username.Equals("admin", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Cannot delete 'admin'.", "Stop", MessageBoxButton.OK, MessageBoxImage.Stop);
                    return;
                }

                if (MessageBox.Show($"Delete user '{user.Username}'?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    IsBusy = true;
                    try
                    {
                        if (!string.IsNullOrEmpty(user.Id))
                        {
                            await Task.Run(() => _userRepository.Remove(user.Id));
                            StatusMessage = Resources.Msg_UserDeleted;
                            ExecuteLoadUsers(null);
                        }
                    }
                    catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
                    finally { IsBusy = false; }
                }
            }
        }

        private async void ExecuteTestConnection(object obj)
        {
            IsBusy = true;
            StatusMessage = "Checking Network...";
            string host = DbHost;

            // FIX: Validate Port Number in Test Connection too
            if (!int.TryParse(DbPort, out int portNum) || portNum < 0 || portNum > 65535)
            {
                IsBusy = false;
                StatusMessage = "Invalid Port";
                MessageBox.Show("Please enter a valid Port number (0-65535).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!string.IsNullOrWhiteSpace(host) && host.ToLower() != "localhost" && host != "." && host != "(local)")
            {
                bool pingSuccess = await Task.Run(() =>
                {
                    try { return new Ping().Send(host, 2000).Status == IPStatus.Success; }
                    catch { return false; }
                });

                if (!pingSuccess)
                {
                    IsBusy = false;
                    StatusMessage = "❌ " + Resources.Msg_NetworkUnreachable;
                    MessageBox.Show($"Could not Ping the server '{host}'.\n\n1. Check IP.\n2. Check Firewall.\n3. Check if host is on.", "Network Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            RebuildConnectionStrings();
            StatusMessage = "Testing connections...";
            bool authSuccess = false, dataSuccess = false;
            string authError = "", dataError = "";

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
                    StatusMessage = "⚠️ DB MISSING.";
                    MessageBox.Show("Server Connected! Databases missing. Save & Restart to create them.", "Connected", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    StatusMessage = "✅ " + Resources.Status_Ready;
                    MessageBox.Show("Connection Successful!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                StatusMessage = "❌ FAILED.";
                MessageBox.Show($"Auth: {authError}\nData: {dataError}", "Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private (bool success, string error) TestSingleConnection(string connString, DatabaseType type)
        {
            if (string.IsNullOrWhiteSpace(connString)) return (false, "Empty");
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
                bool isMissing = (ex is SqlException s && s.Number == 4060) || (ex is PostgresException p && p.SqlState == "3D000");
                if (isMissing) return (true, "Database missing");
                return (false, ex.Message);
            }
        }
    }
}