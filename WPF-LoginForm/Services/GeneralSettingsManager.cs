using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;
using WPF_LoginForm.Models;
using WPF_LoginForm.Properties;

namespace WPF_LoginForm.Services
{
    public class GeneralSettingsManager
    {
        private static GeneralSettingsManager _instance;
        public static GeneralSettingsManager Instance => _instance ?? (_instance = new GeneralSettingsManager());

        private readonly string _configLocationPointerFile;
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        private volatile GeneralSettings _current;
        public GeneralSettings Current
        {
            get => _current;
            private set => _current = value;
        }

        private GeneralSettingsManager()
        {
            _configLocationPointerFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config_location.txt");
            _current = new GeneralSettings();
        }

        public static string ResolveOfflineFolderPath()
        {
            // 1. Check offline_path.txt FIRST (legacy pointer file, original behavior)
            string pointerFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "offline_path.txt");
            if (File.Exists(pointerFile))
            {
                try
                {
                    string savedPath = File.ReadAllText(pointerFile).Trim();
                    if (!string.IsNullOrEmpty(savedPath) && Directory.Exists(savedPath))
                        return savedPath;
                }
                catch { }
            }

            // 2. Check config.OfflineFolderPath as fallback
            var config = Instance.Current;
            if (config != null && !string.IsNullOrEmpty(config.OfflineFolderPath) && Directory.Exists(config.OfflineFolderPath))
                return config.OfflineFolderPath;

            // 3. Default to OfflineData\ in app directory
            string defaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OfflineData");
            if (!Directory.Exists(defaultPath)) Directory.CreateDirectory(defaultPath);
            return defaultPath;
        }

        private string GetOfflineBackupPath()
        {
            string folder = ResolveOfflineFolderPath();
            return Path.Combine(folder, "offline_users.enc");
        }

        private void SaveOfflineBackup()
        {
            try
            {
                var backup = new
                {
                    Users = Current.OfflineUsers,
                    AdminHash = Current.OfflineAdminPasswordHash
                };
                string json = JsonConvert.SerializeObject(backup);
                byte[] plainBytes = System.Text.Encoding.UTF8.GetBytes(json);
                byte[] encrypted = OfflineDataEncryption.Encrypt(plainBytes, OfflineDataEncryption.MasterPassword);
                string path = GetOfflineBackupPath();
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllBytes(path, encrypted);
            }
            catch { }
        }

        private bool TryRestoreOfflineBackup()
        {
            try
            {
                string path = GetOfflineBackupPath();
                if (!File.Exists(path)) return false;
                byte[] encrypted = File.ReadAllBytes(path);
                byte[] plainBytes = OfflineDataEncryption.Decrypt(encrypted, OfflineDataEncryption.MasterPassword);
                string json = System.Text.Encoding.UTF8.GetString(plainBytes);
                var backup = JsonConvert.DeserializeAnonymousType(json, new { Users = "", AdminHash = "" });
                if (backup != null)
                {
                    if (!string.IsNullOrEmpty(backup.Users)) Current.OfflineUsers = backup.Users;
                    if (!string.IsNullOrEmpty(backup.AdminHash)) Current.OfflineAdminPasswordHash = backup.AdminHash;
                    return true;
                }
            }
            catch { }
            return false;
        }

        public string GetResolvedConfigPath()
        {
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "general_config.json");
            if (File.Exists(localPath)) return localPath;

            if (File.Exists(_configLocationPointerFile))
            {
                try
                {
                    string customPath = File.ReadAllText(_configLocationPointerFile).Trim();
                    if (!string.IsNullOrEmpty(customPath))
                    {
                        if (Directory.Exists(customPath)) return Path.Combine(customPath, "general_config.json");
                        else if (customPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) return customPath;
                    }
                }
                catch { }
            }

            return localPath;
        }

        public void SetCustomConfigPath(string newPath)
        {
            File.WriteAllText(_configLocationPointerFile, newPath);
            MessageBox.Show("Save File successfully changed. Restart of app is needed.", "Restart Required", MessageBoxButton.OK, MessageBoxImage.Information);
            System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);
            Application.Current.Shutdown();
        }

        public void Load()
        {
            _lock.EnterWriteLock();
            try
            {
                string configPath = GetResolvedConfigPath();

                if (File.Exists(configPath))
                {
                    try
                    {
                        string json = File.ReadAllText(configPath);
                        _current = JsonConvert.DeserializeObject<GeneralSettings>(json) ?? new GeneralSettings();

                        SyncJsonToSystemSettings();
                        LoadDashboardPart();

                        if (string.IsNullOrEmpty(Current.OfflineAdminPasswordHash))
                        {
                            OfflineUserStore.SeedDefaultAdmin();
                        }

                        EnsureOfflineFolderPath();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading general config: {ex.Message}");
                        string oldUsers = _current?.OfflineUsers;
                        string oldAdminHash = _current?.OfflineAdminPasswordHash;
                        _current = new GeneralSettings();
                        if (oldUsers != null) _current.OfflineUsers = oldUsers;
                        if (oldAdminHash != null) _current.OfflineAdminPasswordHash = oldAdminHash;
                        if (!TryRestoreOfflineBackup())
                        {
                            LoadGeneralPartFromLegacy();
                            LoadDashboardPart();
                        }
                        EnsureOfflineFolderPath();
                    }
                }
                else
                {
                    _current = new GeneralSettings();
                    TryRestoreOfflineBackup();
                    LoadGeneralPartFromLegacy();
                    LoadDashboardPart();
                    EnsureOfflineFolderPath();
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        private void EnsureOfflineFolderPath()
        {
            if (string.IsNullOrEmpty(_current.OfflineFolderPath) || !Directory.Exists(_current.OfflineFolderPath))
            {
                _current.OfflineFolderPath = ResolveOfflineFolderPath();
            }
        }

        private void SyncJsonToSystemSettings()
        {
            if (Current == null) return;

            try
            {
                Settings.Default.DbProvider = Current.DbProvider ?? "SqlServer";
                Settings.Default.SqlAuthConnString = Current.SqlAuthConnString;
                Settings.Default.SqlDataConnString = Current.SqlDataConnString;
                Settings.Default.PostgresDataConnString = Current.PostgresDataConnString;
                Settings.Default.PostgresAuthConnString = Current.PostgresAuthConnString;
                Settings.Default.AppLanguage = Current.AppLanguage ?? "en-US";
                Settings.Default.AutoImportEnabled = Current.AutoImportEnabled;
                Settings.Default.ImportIsRelative = Current.ImportIsRelative;
                Settings.Default.ImportFileName = Current.ImportFileName;
                Settings.Default.ImportAbsolutePath = Current.ImportAbsolutePath;
                Settings.Default.ConnectionTimeout = Current.ConnectionTimeout;
                Settings.Default.TrustServerCertificate = Current.TrustServerCertificate;
                Settings.Default.DbServerName = Current.DbServerName;
                Settings.Default.DbHost = Current.DbHost;
                Settings.Default.DbPort = Current.DbPort;
                Settings.Default.DbUser = Current.DbUser;
                Settings.Default.PureOfflineMode = Current.PureOfflineMode;
                Settings.Default.SuppressOfflineReminder = Current.SuppressOfflineReminder;
            }
            catch { }
        }

        private void LoadGeneralPartFromLegacy()
        {
            try
            {
                Current.DbProvider = Settings.Default.DbProvider;
                Current.SqlAuthConnString = Settings.Default.SqlAuthConnString;
                Current.SqlDataConnString = Settings.Default.SqlDataConnString;
                Current.PostgresDataConnString = Settings.Default.PostgresDataConnString;
                Current.PostgresAuthConnString = Settings.Default.PostgresAuthConnString;
                Current.AppLanguage = Settings.Default.AppLanguage;
                Current.AutoImportEnabled = Settings.Default.AutoImportEnabled;
                Current.ImportIsRelative = Settings.Default.ImportIsRelative;
                Current.ImportFileName = Settings.Default.ImportFileName;
                Current.ImportAbsolutePath = Settings.Default.ImportAbsolutePath;
                Current.ConnectionTimeout = Settings.Default.ConnectionTimeout;
                Current.TrustServerCertificate = Settings.Default.TrustServerCertificate;
                Current.DbServerName = Settings.Default.DbServerName;
                Current.DbHost = Settings.Default.DbHost;
                Current.DbPort = Settings.Default.DbPort;
                Current.DbUser = Settings.Default.DbUser;

                Current.PureOfflineMode = Settings.Default.PureOfflineMode;
                Current.SuppressOfflineReminder = Settings.Default.SuppressOfflineReminder;
            }
            catch { }

            if (string.IsNullOrEmpty(Current.OfflineFolderPath))
            {
                Current.OfflineFolderPath = ResolveOfflineFolderPath();
            }
        }

        private void LoadDashboardPart()
        {
            if (Current == null) _current = new GeneralSettings();

            try
            {
                Current.ShowDashboardDateFilter = Settings.Default.ShowDashboardDateFilter;
                Current.DashboardDateTickSize = Settings.Default.DashboardDateTickSize;
                Current.DefaultRowLimit = Settings.Default.DefaultRowLimit;
            }
            catch
            {
                Current.ShowDashboardDateFilter = true;
                Current.DashboardDateTickSize = 1;
                Current.DefaultRowLimit = 500;
            }

            try
            {
                string categoryRulesPath = "category_rules.json";
                if (File.Exists(categoryRulesPath))
                {
                    string json = File.ReadAllText(categoryRulesPath);
                    Current.CategoryRules = JsonConvert.DeserializeObject<List<CategoryRule>>(json) ?? new List<CategoryRule>();
                }
            }
            catch { }
        }

        public void Save()
        {
            if (Current == null) return;

            _lock.EnterWriteLock();
            try
            {
                SaveOfflineBackup();

                string configPath = GetResolvedConfigPath();
                string dir = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string backupPath = configPath + ".bak";
                bool hadBackup = false;
                if (File.Exists(configPath))
                {
                    try
                    {
                        File.Copy(configPath, backupPath, true);
                        hadBackup = true;
                    }
                    catch { }
                }

                try
                {
                    string json = JsonConvert.SerializeObject(Current, Formatting.Indented);

                    using (FileLock.Acquire(configPath + ".lock"))
                    {
                        File.WriteAllText(configPath, json);
                    }

                    if (hadBackup)
                    {
                        try { File.Delete(backupPath); } catch { }
                    }
                }
                catch
                {
                    if (hadBackup && File.Exists(backupPath))
                    {
                        try { File.Copy(backupPath, configPath, true); File.Delete(backupPath); } catch { }
                    }
                    throw;
                }

                try
                {
                    Settings.Default.DbProvider = Current.DbProvider;
                    Settings.Default.SqlAuthConnString = Current.SqlAuthConnString;
                    Settings.Default.SqlDataConnString = Current.SqlDataConnString;
                    Settings.Default.PostgresDataConnString = Current.PostgresDataConnString;
                    Settings.Default.PostgresAuthConnString = Current.PostgresAuthConnString;
                    Settings.Default.AppLanguage = Current.AppLanguage;
                    Settings.Default.AutoImportEnabled = Current.AutoImportEnabled;
                    Settings.Default.ImportIsRelative = Current.ImportIsRelative;
                    Settings.Default.ImportFileName = Current.ImportFileName;
                    Settings.Default.ImportAbsolutePath = Current.ImportAbsolutePath;

                    Settings.Default.ShowDashboardDateFilter = Current.ShowDashboardDateFilter;
                    Settings.Default.DashboardDateTickSize = Current.DashboardDateTickSize;
                    Settings.Default.DefaultRowLimit = Current.DefaultRowLimit;

                    Settings.Default.ConnectionTimeout = Current.ConnectionTimeout;
                    Settings.Default.TrustServerCertificate = Current.TrustServerCertificate;
                    Settings.Default.DbServerName = Current.DbServerName;
                    Settings.Default.DbHost = Current.DbHost;
                    Settings.Default.DbPort = Current.DbPort;
                    Settings.Default.DbUser = Current.DbUser;
                    Settings.Default.PureOfflineMode = Current.PureOfflineMode;
                    Settings.Default.SuppressOfflineReminder = Current.SuppressOfflineReminder;
                    Settings.Default.Save();
                }
                catch { }

                try
                {
                    string offlineConfig = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "offline_path.txt");
                    File.WriteAllText(offlineConfig, Current.OfflineFolderPath);
                }
                catch { }

                try
                {
                    string categoryRulesPath = "category_rules.json";
                    if (Current.CategoryRules != null)
                    {
                        string rulesJson = JsonConvert.SerializeObject(Current.CategoryRules, Formatting.Indented);
                        File.WriteAllText(categoryRulesPath, rulesJson);
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving general config: {ex.Message}");
                throw;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void ExportGeneralConfig(string filePath)
        {
            if (Current == null) return;
            try
            {
                string json = JsonConvert.SerializeObject(Current, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
