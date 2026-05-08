// Services/GeneralSettingsManager.cs
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public GeneralSettings Current { get; private set; }

        private GeneralSettingsManager()
        {
            _configLocationPointerFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config_location.txt");
            Current = new GeneralSettings();
            // REMOVED: Load() call here. Prevents double loading.
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
            string configPath = GetResolvedConfigPath();

            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath);
                    Current = JsonConvert.DeserializeObject<GeneralSettings>(json) ?? new GeneralSettings();
                    
                    SyncJsonToSystemSettings();
                    LoadDashboardPart();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading general config: {ex.Message}");
                    LoadFromLegacyBackup();
                }
            }
            else
            {
                LoadFromLegacyBackup();
            }
        }

        private void SyncJsonToSystemSettings()
        {
            if (Current == null) return;

            try
            {
                // Inject JSON settings into the memory state of Properties.Settings
                // REMOVED: Settings.Default.Save() to prevent crash on read-only environments
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
            }
            catch { }

            try
            {
                string offlineConfig = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "offline_path.txt");
                File.WriteAllText(offlineConfig, Current.OfflineFolderPath ?? "");
            }
            catch { }
        }

        private void LoadFromLegacyBackup()
        {
            Current = new GeneralSettings();
            LoadGeneralPartFromLegacy();
            LoadDashboardPart();
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

                // Pure Offline Mode flag
                Current.PureOfflineMode = Settings.Default.PureOfflineMode;
            }
            catch { }
            
            try
            {
                string offlineConfig = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "offline_path.txt");
                if (File.Exists(offlineConfig)) Current.OfflineFolderPath = File.ReadAllText(offlineConfig).Trim();
                else Current.OfflineFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OfflineData");
            }
            catch { }
        }

        private void LoadDashboardPart()
        {
            if (Current == null) Current = new GeneralSettings();

            try
            {
                Current.ShowDashboardDateFilter = Settings.Default.ShowDashboardDateFilter;
                Current.DashboardDateTickSize = Settings.Default.DashboardDateTickSize;
                Current.DefaultRowLimit = Settings.Default.DefaultRowLimit;
            }
            catch
            {
                // Fallbacks if Settings properties don't exist
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
            string configPath = GetResolvedConfigPath();

            try
            {
                string dir = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string json = JsonConvert.SerializeObject(Current, Formatting.Indented);
                File.WriteAllText(configPath, json);

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
                    Settings.Default.Save();
                }
                catch { } // Ignore save failure for legacy settings if restricted

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
                        var orderedRules = Current.CategoryRules.OrderByDescending(r => r.Priority).ToList();
                        string rulesJson = JsonConvert.SerializeObject(orderedRules, Formatting.Indented);
                        File.WriteAllText(categoryRulesPath, rulesJson);
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving general config: {ex.Message}");
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