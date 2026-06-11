// ViewModels/SettingsViewModel.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
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
using System.Text;
using Newtonsoft.Json;

namespace WPF_LoginForm.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private static readonly CultureInfo TurkishCulture = new CultureInfo("tr-TR");
        private static bool TurkishIgnoreCaseEquals(string a, string b) =>
            string.Compare(a, b, ignoreCase: true, culture: TurkishCulture) == 0;
        private readonly IDataRepository _dataRepository;

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

        private const string _authDbName = "LoginDb";
        private const string _dataDbName = "MainDataDb";

        private string _sqlAuthString;
        private string _sqlDataString;
        private string _postgresAuthString;
        private string _postgresDataString;

        // Import Settings
        private bool _autoImportEnabled;

        private bool _importIsRelative;
        private string _importFileName;
        private string _importAbsolutePath;

        // Dashboard Settings
        private bool _showDashboardDateFilter;

        private int _dashboardDateTickSize;

        // AI Settings
        private bool _aiAssistantEnabled;
        private string _aiApiKey;
        private string _aiProvider;
        public List<string> AvailableAiProviders { get; } = new List<string> { "gemini", "xai", "openrouter" };
        private int _defaultRowLimit;

        // Offline Settings
        private string _offlineFolderPath;

        private ObservableCollection<SelectableTable> _backupTables;

        // User Management
        private ObservableCollection<UserModel> _users;

        private string _newUserUsername;
        private string _newUserName;
        private string _newUserLastName;
        private string _newUserEmail;
        private SecureString _newUserPassword;
        private string _newUserRole;
        private readonly IUserRepository _userRepository;
        private readonly ILogger _logger;

        // Offline Encryption
        private SecureString _masterPassword;
        private bool _encryptBackup;
        private bool _useCustomMasterPassword;

        // --- Properties ---
        public IEnumerable<DatabaseType> DatabaseTypes => Enum.GetValues(typeof(DatabaseType)).Cast<DatabaseType>();

        public Dictionary<string, string> Languages { get; } = new Dictionary<string, string>
        {
            { "English", "en-US" },
            { "Türkçe", "tr-TR" }
        };

        public DatabaseType SelectedDatabaseType
        {
            get => _selectedDatabaseType;
            set
            {
                if (SetProperty(ref _selectedDatabaseType, value))
                {
                    OnPropertyChanged(nameof(IsWindowsAuthVisible));
                    LoadFromCurrentProvider();
                }
            }
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
                    (LoadBackupTablesCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
                    (CreateOfflineBackupCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
                    (DecryptOfflineDataCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public string DbHost { get => _dbHost; set => SetProperty(ref _dbHost, value); }
        public string DbPort { get => _dbPort; set => SetProperty(ref _dbPort, value); }
        public string DbUser { get => _dbUser; set => SetProperty(ref _dbUser, value); }
        public SecureString DbPassword { get => _dbPassword; set => SetProperty(ref _dbPassword, value); }

        public bool UseWindowsAuth
        {
            get => _useWindowsAuth;
            set { SetProperty(ref _useWindowsAuth, value); OnPropertyChanged(nameof(IsCredentialsEnabled)); }
        }

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

        public string OfflineFolderPath { get => _offlineFolderPath; set => SetProperty(ref _offlineFolderPath, value); }
        public ObservableCollection<SelectableTable> BackupTables { get => _backupTables; set => SetProperty(ref _backupTables, value); }
        public bool IsOnlineMode => !(_dataRepository is OfflineDataRepository);
        public bool IsOfflineMode => _dataRepository is OfflineDataRepository;
        public SecureString MasterPassword { get => _masterPassword; set => SetProperty(ref _masterPassword, value); }
        public bool EncryptBackup { get => _encryptBackup; set => SetProperty(ref _encryptBackup, value); }
        public bool UseCustomMasterPassword { get => _useCustomMasterPassword; set { SetProperty(ref _useCustomMasterPassword, value); OnPropertyChanged(nameof(IsMasterPasswordEnabled)); } }
        public bool IsMasterPasswordEnabled => _useCustomMasterPassword;

        private ObservableCollection<OfflineUser> _offlineUsers;
        public ObservableCollection<OfflineUser> OfflineUsers
        {
            get => _offlineUsers;
            set => SetProperty(ref _offlineUsers, value);
        }

        private ObservableCollection<OfflineUserDisplayModel> _offlineUserDisplayList;
        public ObservableCollection<OfflineUserDisplayModel> OfflineUserDisplayList
        {
            get => _offlineUserDisplayList;
            set => SetProperty(ref _offlineUserDisplayList, value);
        }

        private string _newOfflineUsername;
        public string NewOfflineUsername
        {
            get => _newOfflineUsername;
            set { _newOfflineUsername = value; OnPropertyChanged(); }
        }

        private SecureString _newOfflinePassword;
        public SecureString NewOfflinePassword
        {
            get => _newOfflinePassword;
            set { _newOfflinePassword = value; OnPropertyChanged(); }
        }

        private string _newOfflineRole = "User";
        public string NewOfflineRole
        {
            get => _newOfflineRole;
            set { _newOfflineRole = value; OnPropertyChanged(); }
        }

        public bool CanManageOfflineUsers => UserSessionService.IsAdmin && IsOfflineMode;
        public List<string> AvailableOfflineRoles { get; } = new List<string> { "User", "Admin" };

        private bool _hideOfflineReminder;
        public bool HideOfflineReminder
        {
            get => _hideOfflineReminder;
            set
            {
                if (SetProperty(ref _hideOfflineReminder, value))
                {
                    GeneralSettingsManager.Instance.Current.SuppressOfflineReminder = value;
                    GeneralSettingsManager.Instance.Save();
                }
            }
        }

        public ObservableCollection<UserModel> Users { get => _users; set => SetProperty(ref _users, value); }

        public string NewUserUsername { get => _newUserUsername; set => SetProperty(ref _newUserUsername, value); }
        public string NewUserName { get => _newUserName; set => SetProperty(ref _newUserName, value); }
        public string NewUserLastName { get => _newUserLastName; set => SetProperty(ref _newUserLastName, value); }
        public string NewUserEmail { get => _newUserEmail; set => SetProperty(ref _newUserEmail, value); }
        public SecureString NewUserPassword { get => _newUserPassword; set => SetProperty(ref _newUserPassword, value); }
        public string NewUserRole { get => _newUserRole; set => SetProperty(ref _newUserRole, value); }

        public List<string> AvailableRoles { get; } = new List<string> { "User", "Admin" };

        public bool CanManageUsers => UserSessionService.IsAdmin && !IsBusy;

        // AI Settings Properties
        public bool AiAssistantEnabled { get => _aiAssistantEnabled; set => SetProperty(ref _aiAssistantEnabled, value); }
        public string AiApiKey { get => _aiApiKey; set => SetProperty(ref _aiApiKey, value); }
        public string AiProvider { get => _aiProvider; set => SetProperty(ref _aiProvider, value); }

        public ICommand OpenAiAssistantCommand { get; }

        // Commands
        public ICommand SaveCommand { get; }

        public ICommand TestConnectionCommand { get; }
        public ICommand BrowseImportFileCommand { get; }
        public ICommand BrowseOfflineFolderCommand { get; }
        public ICommand LoadBackupTablesCommand { get; }
        public ICommand CreateOfflineBackupCommand { get; }
        public ICommand DecryptOfflineDataCommand { get; }
        public ICommand LoadUsersCommand { get; }
        public ICommand AddUserCommand { get; }
        public ICommand DeleteUserCommand { get; }
        public ICommand ChangeConfigLocationCommand { get; }
        public ICommand ExportConfigCommand { get; }
        public ICommand AddOfflineUserCommand { get; }
        public ICommand RemoveOfflineUserCommand { get; }
        public ICommand ChangeAdminPasswordCommand { get; }
        public ICommand BackupPasswordsCommand { get; }
        public ICommand RestorePasswordsCommand { get; }

        public string GeneralConfigPath => GeneralSettingsManager.Instance.GetResolvedConfigPath();

        public SettingsViewModel() : this(null)
        {
        }

        public SettingsViewModel(IDataRepository dataRepository)
        {
            _dataRepository = dataRepository;
            _userRepository = new UserRepository();
            _logger = App.GlobalLogger ?? new FileLogger("SettingsLog");
            Users = new ObservableCollection<UserModel>();
            BackupTables = new ObservableCollection<SelectableTable>();

            var config = GeneralSettingsManager.Instance.Current;

            _sqlAuthString = config.SqlAuthConnString;
            _sqlDataString = config.SqlDataConnString;
            _postgresAuthString = config.PostgresAuthConnString;
            _postgresDataString = config.PostgresDataConnString;
            SelectedDatabaseType = DbConnectionFactory.CurrentDatabaseType;

            string currentLang = config.AppLanguage;
            SelectedLanguage = string.IsNullOrEmpty(currentLang) ? "en-US" : currentLang;

            AutoImportEnabled = config.AutoImportEnabled;
            ImportIsRelative = config.ImportIsRelative;
            ImportFileName = config.ImportFileName;
            ImportAbsolutePath = config.ImportAbsolutePath;
            ShowDashboardDateFilter = config.ShowDashboardDateFilter;
            DashboardDateTickSize = config.DashboardDateTickSize;
            if (DashboardDateTickSize < 1) DashboardDateTickSize = 1;

            ConnectionTimeout = config.ConnectionTimeout;
            TrustServerCertificate = config.TrustServerCertificate;
            DbServerName = config.DbServerName;
            DefaultRowLimit = config.DefaultRowLimit;
            if (DefaultRowLimit < 0) DefaultRowLimit = 500;

            NewUserRole = "User";

            LoadFromCurrentProvider();

            OfflineFolderPath = config.OfflineFolderPath;
            _hideOfflineReminder = config.SuppressOfflineReminder;

            // Load AI Settings
            AiAssistantEnabled = config.AiAssistantEnabled;
            AiApiKey = config.AiApiKey;
            AiProvider = config.AiProvider;

            // Load offline users
            OfflineUsers = new ObservableCollection<OfflineUser>(OfflineUserStore.GetUserList());
            OnPropertyChanged(nameof(CanManageOfflineUsers));
            RefreshOfflineUserDisplay();

            // Default master password
            _masterPassword = ToSecureString(OfflineDataEncryption.MasterPassword);

            SaveCommand = new ViewModelCommand(ExecuteSaveCommand, (o) => !IsBusy);
            TestConnectionCommand = new ViewModelCommand(ExecuteTestConnection, (o) => !IsBusy);
            BrowseImportFileCommand = new ViewModelCommand(ExecuteBrowseImportFile);
            BrowseOfflineFolderCommand = new ViewModelCommand(ExecuteBrowseOfflineFolder);

            LoadBackupTablesCommand = new ViewModelCommand(ExecuteLoadBackupTables, (o) => !IsBusy && IsOnlineMode);
            CreateOfflineBackupCommand = new ViewModelCommand(ExecuteCreateOfflineBackup, (o) => !IsBusy && IsOnlineMode && BackupTables.Any(t => t.IsSelected));
            DecryptOfflineDataCommand = new ViewModelCommand(ExecuteDecryptOfflineData, (o) => !IsBusy);

            LoadUsersCommand = new ViewModelCommand(ExecuteLoadUsers);
            AddUserCommand = new ViewModelCommand(ExecuteAddUser);
            DeleteUserCommand = new ViewModelCommand(ExecuteDeleteUser);
            ChangeConfigLocationCommand = new ViewModelCommand(ExecuteChangeConfigLocation);
            ExportConfigCommand = new ViewModelCommand(ExecuteExportConfig);
            AddOfflineUserCommand = new ViewModelCommand(ExecuteAddOfflineUser, (o) => CanManageOfflineUsers);
            RemoveOfflineUserCommand = new ViewModelCommand(ExecuteRemoveOfflineUser, (o) => CanManageOfflineUsers);
            ChangeAdminPasswordCommand = new ViewModelCommand(ExecuteChangeAdminPassword, (o) => CanManageOfflineUsers);
            BackupPasswordsCommand = new ViewModelCommand(ExecuteBackupPasswords, (o) => CanManageOfflineUsers);
            RestorePasswordsCommand = new ViewModelCommand(ExecuteRestorePasswords, (o) => CanManageOfflineUsers);
            OpenAiAssistantCommand = new ViewModelCommand(p => ExecuteOpenAiAssistant());
        }

        private void ExecuteOpenAiAssistant()
        {
            // Save API key + provider before opening assistant so it reads the latest values
            var config = GeneralSettingsManager.Instance.Current;
            config.AiApiKey = AiApiKey;
            config.AiProvider = AiProvider;
            GeneralSettingsManager.Instance.Save();

            Application.Current.Dispatcher.Invoke(() =>
            {
                var win = new Views.AiAssistantWindow(_dataRepository);
                if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
                    win.Owner = Application.Current.MainWindow;
                win.ShowDialog();
            });
        }

        private void ExecuteExportConfig(object obj)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = Resources.Title_ExportConfig,
                Filter = Resources.Filter_JsonFiles,
                FileName = "exported_general_config.json"
            };

            if (dialog.ShowDialog() == true)
            {
                GeneralSettingsManager.Instance.ExportGeneralConfig(dialog.FileName);
                MessageBox.Show(Resources.Msg_ConfigExportSuccess, Resources.Title_Export, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ExecuteChangeConfigLocation(object obj)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = Resources.Title_SelectConfigLocation,
                Filter = Resources.Filter_JsonFiles,
                FileName = "general_config.json"
            };

            if (dialog.ShowDialog() == true)
            {
                GeneralSettingsManager.Instance.SetCustomConfigPath(dialog.FileName);
            }
        }

        private void ExecuteBrowseImportFile(object obj)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Title = Resources.Title_SelectFileInFolder, Filter = Resources.Filter_AllFiles, CheckFileExists = true };
            if (dialog.ShowDialog() == true) ImportAbsolutePath = Path.GetDirectoryName(dialog.FileName);
        }

        private void ExecuteBrowseOfflineFolder(object obj)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Title = Resources.Title_SelectOfflineFolder, Filter = Resources.Filter_AllFiles, CheckFileExists = true };
            if (dialog.ShowDialog() == true) OfflineFolderPath = Path.GetDirectoryName(dialog.FileName);
        }

        private async void ExecuteLoadBackupTables(object obj)
        {
            if (_dataRepository == null) return;
            IsBusy = true;
            StatusMessage = Resources.Status_FetchingTables;
            try
            {
                var tables = await _dataRepository.GetTableNamesAsync(true);
                BackupTables.Clear();
                foreach (var t in tables)
                {
                    var st = new SelectableTable { Name = t, DisplayName = t.Replace("_", " "), IsSelected = true };
                    st.PropertyChanged += (s, e) => { (CreateOfflineBackupCommand as ViewModelCommand)?.RaiseCanExecuteChanged(); };
                    BackupTables.Add(st);
                }
                StatusMessage = Resources.Status_ReadyBackup;
                (CreateOfflineBackupCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(Resources.Status_ErrorFetchingTables, ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void ExecuteCreateOfflineBackup(object obj)
        {
            var selected = BackupTables.Where(t => t.IsSelected).ToList();
            if (!selected.Any()) return;

            if (string.IsNullOrWhiteSpace(OfflineFolderPath))
            {
                MessageBox.Show(Resources.Msg_OfflineFolderRequired, Resources.Title_ValidationError, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool doEncrypt = EncryptBackup;
            string password = null;
            if (doEncrypt)
            {
                if (UseCustomMasterPassword)
                {
                    if (MasterPassword == null || MasterPassword.Length == 0)
                    {
                        MessageBox.Show(Resources.Msg_PasswordRequired, Resources.Title_ValidationError, MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    password = new NetworkCredential("", MasterPassword).Password;
                    if (password.Length < 8)
                    {
                        MessageBox.Show(Resources.Msg_PasswordMinLength, Resources.Title_ValidationError, MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                else
                {
                    password = OfflineDataEncryption.MasterPassword;
                }
            }

            IsBusy = true;
            try
            {
                if (!Directory.Exists(OfflineFolderPath)) Directory.CreateDirectory(OfflineFolderPath);

                int count = 0;
                foreach (var tbl in selected)
                {
                    StatusMessage = string.Format(Resources.Status_DownloadingTable, tbl.Name);

                    var result = await _dataRepository.GetTableDataAsync(tbl.Name, 0);

                    if (result.Data != null)
                    {
                        // Always export CSV (existing behavior)
                        string csvPath = Path.Combine(OfflineFolderPath, $"{tbl.Name}.csv");
                        await Task.Run(() => DataImportExportHelper.ExportTable(csvPath, result.Data, tbl.Name));

                        // Also encrypt if requested
                        if (doEncrypt && password != null)
                        {
                            StatusMessage = string.Format(Resources.Status_EncryptingTable, tbl.Name);
                            string encPath = Path.Combine(OfflineFolderPath, $"{tbl.Name}.enc");
                            string csvContent = File.ReadAllText(csvPath);
                            byte[] plainBytes = System.Text.Encoding.UTF8.GetBytes(csvContent);
                            await Task.Run(() => OfflineDataEncryptionFile.EncryptFile(encPath, plainBytes, password));
                        }

                        count++;
                    }
                }

                if (doEncrypt && password != null)
                {
                    string obfuscatedPw = OfflineDataEncryption.ObfuscatePassword(password);
                    byte[] dummyKey = OfflineDataEncryption.DeriveKey(password, OfflineDataEncryption.GenerateSalt());
                    string obfuscatedKey = OfflineDataEncryption.ObfuscateBase64(Convert.ToBase64String(dummyKey));
                    _logger.LogInfo($"[ENCRYPTION_BACKUP] Backup created. MasterPW: {obfuscatedPw}, DerivedKey: {obfuscatedKey}");
                }

                StatusMessage = string.Format(Resources.Status_OfflineImageCreated, count);
                MessageBox.Show(string.Format(Resources.Msg_OfflineBackupSuccess, count, OfflineFolderPath), Resources.Title_BackupComplete, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(Resources.Status_BackupFailed, ex.Message);
                MessageBox.Show(string.Format(Resources.Msg_OfflineBackupFailed, ex.Message), Resources.Title_Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void ExecuteDecryptOfflineData(object obj)
        {
            if (string.IsNullOrWhiteSpace(OfflineFolderPath) || !Directory.Exists(OfflineFolderPath))
            {
                MessageBox.Show(Resources.Msg_OfflineFolderRequired, Resources.Title_ValidationError, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string password = null;
            if (UseCustomMasterPassword)
            {
                if (MasterPassword == null || MasterPassword.Length == 0)
                {
                    MessageBox.Show(Resources.Msg_PasswordRequired, Resources.Title_ValidationError, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                password = new NetworkCredential("", MasterPassword).Password;
                if (password.Length < 8)
                {
                    MessageBox.Show(Resources.Msg_PasswordMinLength, Resources.Title_ValidationError, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            else
            {
                password = OfflineDataEncryption.MasterPassword;
            }

            IsBusy = true;
            StatusMessage = Resources.Status_Decrypting;
            try
            {
                var encFiles = Directory.GetFiles(OfflineFolderPath, "*.enc");
                if (encFiles.Length == 0)
                {
                    MessageBox.Show(Resources.Msg_NoEncFilesFound, Resources.Title_ValidationError, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                OfflineDataCache.Clear();
                int count = 0;

                foreach (var encPath in encFiles)
                {
                    string tableName = Path.GetFileNameWithoutExtension(encPath);
                    StatusMessage = string.Format(Resources.Status_DecryptingTable, tableName);

                    byte[] plainBytes = await Task.Run(() => OfflineDataEncryptionFile.DecryptFile(encPath, password));
                    string csvContent = System.Text.Encoding.UTF8.GetString(plainBytes);

                    DataTable dt = CsvParser.ParseToDataTable(csvContent, tableName);
                    if (dt.Columns.Count == 0) continue;

                    OfflineDataCache.DecryptedTables[tableName] = dt;
                    count++;
                }

                StatusMessage = string.Format(Resources.Msg_DecryptSuccess, count);
                MessageBox.Show(string.Format(Resources.Msg_DecryptSuccess, count), Resources.Title_Success, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"{Resources.Str_Error}: {ex.Message}";
                MessageBox.Show(string.Format(Resources.Msg_OfflineBackupFailed, ex.Message), Resources.Title_Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ExecuteSaveCommand(object obj)
        {
            if (!int.TryParse(DbPort, out int portNum) || portNum < 0 || portNum > 65535)
            {
                MessageBox.Show(Resources.Msg_InvalidPortDetail, Resources.Title_ValidationError, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                RebuildConnectionStrings();
                DbConnectionFactory.CurrentDatabaseType = SelectedDatabaseType;

                var config = GeneralSettingsManager.Instance.Current;
                config.DbProvider = SelectedDatabaseType.ToString();
                config.SqlAuthConnString = _sqlAuthString;
                config.SqlDataConnString = _sqlDataString;
                config.PostgresAuthConnString = _postgresAuthString;
                config.PostgresDataConnString = _postgresDataString;

                config.AppLanguage = SelectedLanguage;
                config.AutoImportEnabled = AutoImportEnabled;
                config.ImportIsRelative = ImportIsRelative;
                config.ImportFileName = ImportFileName;
                config.ImportAbsolutePath = ImportAbsolutePath;
                config.ShowDashboardDateFilter = ShowDashboardDateFilter;
                config.DashboardDateTickSize = DashboardDateTickSize;

                config.ConnectionTimeout = ConnectionTimeout;
                config.TrustServerCertificate = TrustServerCertificate;
                config.DbServerName = DbServerName;

                if (DefaultRowLimit < 0) DefaultRowLimit = 500;
                config.DefaultRowLimit = DefaultRowLimit;

                config.DbHost = DbHost;
                config.DbPort = DbPort;
                config.DbUser = DbUser;
                
                config.OfflineFolderPath = OfflineFolderPath;

                // Save AI Settings
                config.AiAssistantEnabled = AiAssistantEnabled;
                config.AiApiKey = AiApiKey;
                config.AiProvider = AiProvider;

                GeneralSettingsManager.Instance.Save();

                var cacheService = new CacheService();
                cacheService.Clear();

                StatusMessage = Resources.Msg_SettingsSavedRestart;
                MessageBox.Show(Resources.Msg_SettingsSavedRestart, Resources.Title_Saved, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { StatusMessage = $"{Resources.Str_Error}: {ex.Message}"; }
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
                    if (!string.IsNullOrEmpty(builder.Password)) DbPassword = new NetworkCredential("", builder.Password).SecurePassword;
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
                    if (!string.IsNullOrEmpty(builder.Password)) DbPassword = new NetworkCredential("", builder.Password).SecurePassword;
                    UseWindowsAuth = false;
                    ConnectionTimeout = builder.Timeout;
                    TrustServerCertificate = builder.TrustServerCertificate;
                }
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
                if (IsOfflineMode)
                {
                    var offlineUsers = OfflineUserStore.GetUserList();
                    Users.Clear();
                    foreach (var ou in offlineUsers)
                        Users.Add(new UserModel { Username = ou.Username, Role = ou.Role, Name = "", LastName = "", Email = "" });
                    StatusMessage = string.Format(Resources.Status_LoadedUsers, Users.Count);
                }
                else
                {
                    var usersList = await Task.Run(() => _userRepository.GetByAll());
                    Users.Clear();
                    foreach (var user in usersList) Users.Add(user);
                    StatusMessage = string.Format(Resources.Status_LoadedUsers, Users.Count);
                }
            }
            catch (Exception ex) { StatusMessage = string.Format(Resources.Status_ErrorLoadingUsers, ex.Message); }
            finally { IsBusy = false; }
        }

        private async void ExecuteAddUser(object obj)
        {
            if (string.IsNullOrWhiteSpace(NewUserUsername) || NewUserPassword == null || NewUserPassword.Length < 3)
            {
                MessageBox.Show(Resources.Msg_UsernamePasswordRequired, Resources.Title_ValidationError, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            IsBusy = true;
            try
            {
                string safePass = new NetworkCredential("", NewUserPassword).Password;
                if (IsOfflineMode)
                {
                    var users = OfflineUserStore.GetUserList();
                    if (users.Any(u => TurkishIgnoreCaseEquals(u.Username, NewUserUsername)))
                    {
                        StatusMessage = "Username already exists.";
                        return;
                    }
                    users.Add(new OfflineUser
                    {
                        Username = NewUserUsername,
                        PasswordHash = OfflineUserStore.HashPassword(safePass),
                        Role = NewUserRole ?? "User"
                    });
                    OfflineUserStore.SaveUserList(users);
                    OfflineUsers = new ObservableCollection<OfflineUser>(users);
                    OnPropertyChanged(nameof(OfflineUsers));
                    RefreshOfflineUserDisplay();
                    NewUserUsername = ""; NewUserName = ""; NewUserLastName = ""; NewUserEmail = ""; NewUserPassword = null; NewUserRole = "User";
                    OnPropertyChanged(nameof(NewUserPassword));
                    StatusMessage = Resources.Msg_UserAdded;
                    ExecuteLoadUsers(null);
                }
                else
                {
                    var newUser = new UserModel { Username = NewUserUsername, Name = NewUserName ?? "", LastName = NewUserLastName ?? "", Email = NewUserEmail ?? "", Role = NewUserRole ?? "User", Password = safePass };
                    await Task.Run(() => _userRepository.Add(newUser));
                    NewUserUsername = ""; NewUserName = ""; NewUserLastName = ""; NewUserEmail = ""; NewUserPassword = null; NewUserRole = "User";
                    OnPropertyChanged(nameof(NewUserPassword));
                    StatusMessage = Resources.Msg_UserAdded;
                    ExecuteLoadUsers(null);
                }
            }
            catch (Exception ex) { StatusMessage = $"{Resources.Str_Error}: {ex.Message}"; MessageBox.Show(ex.Message, Resources.Title_Error, MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { IsBusy = false; }
        }

        private async void ExecuteDeleteUser(object obj)
        {
            if (obj is UserModel user)
            {
                if (TurkishIgnoreCaseEquals(user.Username, "admin"))
                {
                    MessageBox.Show(Resources.Msg_CannotDeleteAdmin, Resources.Title_Stop, MessageBoxButton.OK, MessageBoxImage.Stop);
                    return;
                }
                if (MessageBox.Show(string.Format(Resources.Msg_ConfirmDeleteUser, user.Username), Resources.Title_Confirm, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    IsBusy = true;
                    try
                    {
                        if (IsOfflineMode)
                        {
                            var users = OfflineUserStore.GetUserList();
                            users.RemoveAll(u => TurkishIgnoreCaseEquals(u.Username, user.Username));
                            OfflineUserStore.SaveUserList(users);
                            OfflineUsers = new ObservableCollection<OfflineUser>(users);
                            OnPropertyChanged(nameof(OfflineUsers));
                            RefreshOfflineUserDisplay();
                            StatusMessage = Resources.Msg_UserDeleted;
                            ExecuteLoadUsers(null);
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(user.Id)) { await Task.Run(() => _userRepository.Remove(user.Id)); StatusMessage = Resources.Msg_UserDeleted; ExecuteLoadUsers(null); }
                        }
                    }
                    catch (Exception ex) { StatusMessage = $"{Resources.Str_Error}: {ex.Message}"; }
                    finally { IsBusy = false; }
                }
            }
        }

        private async void ExecuteTestConnection(object obj)
        {
            IsBusy = true;
            StatusMessage = Resources.Status_CheckingNetwork;
            string host = DbHost;

            if (!int.TryParse(DbPort, out int portNum) || portNum < 0 || portNum > 65535)
            {
                IsBusy = false;
                StatusMessage = Resources.Msg_InvalidPort;
                MessageBox.Show(Resources.Msg_InvalidPortDetail, Resources.Title_ValidationError, MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    MessageBox.Show(string.Format(Resources.Msg_PingFailed, host), Resources.Title_NetworkError, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            RebuildConnectionStrings();
            StatusMessage = Resources.Status_TestingConnections;
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
                    StatusMessage = Resources.Status_DbMissing;
                    MessageBox.Show(Resources.Msg_DbMissingSaveRestart, Resources.Title_Connected, MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    StatusMessage = "✅ " + Resources.Status_Ready;
                    MessageBox.Show(Resources.Msg_ConnectionSuccess, Resources.Title_Success, MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                StatusMessage = "❌ " + Resources.Msg_ConnectionFailed;
                MessageBox.Show(string.Format(Resources.Msg_ConnectionFailedDetail, authError, dataError), Resources.Title_Failed, MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void ExecuteAddOfflineUser(object obj)
        {
            string username = NewOfflineUsername;
            if (string.IsNullOrWhiteSpace(username))
            {
                MessageBox.Show("Username is required.", Resources.Title_ValidationError, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string password = NewOfflinePassword != null ? new NetworkCredential("", NewOfflinePassword).Password : "";
            if (password.Length < 6)
            {
                MessageBox.Show(Resources.Str_InvalidPasswordFormat, Resources.Title_ValidationError, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var users = OfflineUserStore.GetUserList();
            if (users.Any(u => TurkishIgnoreCaseEquals(u.Username, username)))
            {
                MessageBox.Show("Username already exists.", Resources.Title_ValidationError, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            users.Add(new OfflineUser
            {
                Username = username,
                PasswordHash = OfflineUserStore.HashPassword(password),
                Role = NewOfflineRole ?? "User"
            });

            OfflineUserStore.SaveUserList(users);
            OfflineUsers = new ObservableCollection<OfflineUser>(users);
            RefreshOfflineUserDisplay();
            NewOfflineUsername = "";
            NewOfflinePassword = null;
            OnPropertyChanged(nameof(NewOfflineUsername));
            OnPropertyChanged(nameof(NewOfflinePassword));

            // Also authenticate the user against the store to create a session entry
            if (IsOfflineMode && UserSessionService.IsAdmin && !TurkishIgnoreCaseEquals(username, "admin"))
            {
                StatusMessage = $"User '{username}' added successfully.";
            }
        }

        private void ExecuteRemoveOfflineUser(object obj)
        {
            if (!(obj is OfflineUser user)) return;

            if (TurkishIgnoreCaseEquals(user.Username, "admin"))
            {
                MessageBox.Show("Cannot remove the admin user.", Resources.Title_ValidationError, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show($"Remove user '{user.Username}'?", Resources.Title_Confirm,
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            var users = OfflineUserStore.GetUserList();
            users.RemoveAll(u => TurkishIgnoreCaseEquals(u.Username, user.Username));
            OfflineUserStore.SaveUserList(users);
            OfflineUsers = new ObservableCollection<OfflineUser>(users);
            RefreshOfflineUserDisplay();
        }

        private void ExecuteChangeAdminPassword(object obj)
        {
            var changePwWindow = new Views.PasswordChangeView(false, "admin");
            changePwWindow.Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
            changePwWindow.ShowDialog();
        }

        private void ExecuteBackupPasswords(object obj)
        {
            var users = OfflineUserStore.GetUserList();
            if (users.Count == 0)
            {
                MessageBox.Show(Resources.Msg_NoOfflineUsers, Resources.Title_PasswordBackup,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string defaultHash = OfflineUserStore.HashPassword("1234");
            string adminDefaultHash = OfflineUserStore.HashPassword(OfflineUserStore.DefaultAdminPassword);

            int changedCount = users.Count(u =>
                !string.Equals(u.PasswordHash,
                    TurkishIgnoreCaseEquals(u.Username, "admin") ? adminDefaultHash : defaultHash,
                    StringComparison.OrdinalIgnoreCase));

            if (changedCount == 0)
            {
                MessageBox.Show(Resources.Msg_BackupPasswordsNone, Resources.Title_PasswordBackup,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                string.Format(Resources.Msg_BackupPasswordsCount, changedCount),
                Resources.Title_PasswordBackup,
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            string folder = OfflineFolderPath;
            if (string.IsNullOrWhiteSpace(folder))
            {
                MessageBox.Show("Offline folder path is not set.", Resources.Str_Error,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            string encPath = Path.Combine(folder, "offline_users.enc");
            string json = JsonConvert.SerializeObject(users, Formatting.Indented);
            byte[] plainBytes = Encoding.UTF8.GetBytes(json);

            try
            {
                OfflineDataEncryptionFile.EncryptFile(encPath, plainBytes, OfflineDataEncryption.MasterPassword);
                MessageBox.Show(string.Format(Resources.Msg_PasswordBackupSuccess, changedCount, encPath),
                    Resources.Title_PasswordBackup, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Resources.Msg_PasswordBackupFailed, ex.Message),
                    Resources.Str_Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteRestorePasswords(object obj)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = Resources.Title_PasswordRestore,
                Filter = Resources.Filter_EncFiles,
                CheckFileExists = true
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                byte[] plainBytes = OfflineDataEncryptionFile.DecryptFile(dialog.FileName, OfflineDataEncryption.MasterPassword);
                string json = Encoding.UTF8.GetString(plainBytes);
                var users = JsonConvert.DeserializeObject<List<OfflineUser>>(json);
                if (users == null || users.Count == 0)
                {
                    MessageBox.Show("Backup file contains no users.", Resources.Title_PasswordRestore,
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var adminUser = users.FirstOrDefault(u => TurkishIgnoreCaseEquals(u.Username, "admin"));
                if (adminUser != null)
                {
                    GeneralSettingsManager.Instance.Current.OfflineAdminPasswordHash = adminUser.PasswordHash;
                }

                OfflineUserStore.SaveUserList(users);
                OfflineUsers = new ObservableCollection<OfflineUser>(users);
                OnPropertyChanged(nameof(OfflineUsers));
                RefreshOfflineUserDisplay();

                MessageBox.Show(Resources.Msg_RestorePasswordsSuccess, Resources.Title_PasswordRestore,
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Resources.Msg_PasswordRestoreFailed, ex.Message), Resources.Str_Error,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshOfflineUserDisplay()
        {
            var users = OfflineUserStore.GetUserList();
            OfflineUserDisplayList = new ObservableCollection<OfflineUserDisplayModel>(
                users.Select(u => new OfflineUserDisplayModel
                {
                    Username = u.Username,
                    Role = u.Role,
                    PasswordStatus = OfflineUserStore.IsDefaultPassword(u) ? "✓" : "❌",
                    Source = u
                }));
        }

        private static SecureString ToSecureString(string str)
        {
            var ss = new SecureString();
            foreach (char c in str) ss.AppendChar(c);
            ss.MakeReadOnly();
            return ss;
        }
    }

    public class SelectableTable : ViewModelBase
    {
        private bool _isSelected;
        public string Name { get; set; }
        public string DisplayName { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }

    public class OfflineUserDisplayModel
    {
        public string Username { get; set; }
        public string Role { get; set; }
        public string PasswordStatus { get; set; }
        public OfflineUser Source { get; set; }
    }
}