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
            Load();
        }

        public string GetResolvedConfigPath()
        {
            if (File.Exists(_configLocationPointerFile))
            {
                string customPath = File.ReadAllText(_configLocationPointerFile).Trim();
                if (!string.IsNullOrEmpty(customPath))
                {
                    // Could be a directory or a full file path. If it's a directory, append the filename.
                    if (Directory.Exists(customPath))
                        return Path.Combine(customPath, "general_config.json");
                    else if (customPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        return customPath;
                }
            }

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "general_config.json");
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

        private void LoadFromLegacyBackup()
        {
            Current = new GeneralSettings
            {
                DbProvider = Settings.Default.DbProvider,
                SqlAuthConnString = Settings.Default.SqlAuthConnString,
                SqlDataConnString = Settings.Default.SqlDataConnString,
                PostgresDataConnString = Settings.Default.PostgresDataConnString,
                PostgresAuthConnString = Settings.Default.PostgresAuthConnString,
                AppLanguage = Settings.Default.AppLanguage,
                AutoImportEnabled = Settings.Default.AutoImportEnabled,
                ImportIsRelative = Settings.Default.ImportIsRelative,
                ImportFileName = Settings.Default.ImportFileName,
                ImportAbsolutePath = Settings.Default.ImportAbsolutePath,
                ShowDashboardDateFilter = Settings.Default.ShowDashboardDateFilter,
                DashboardDateTickSize = Settings.Default.DashboardDateTickSize,
                DefaultRowLimit = Settings.Default.DefaultRowLimit,
                ConnectionTimeout = Settings.Default.ConnectionTimeout,
                TrustServerCertificate = Settings.Default.TrustServerCertificate,
                DbServerName = Settings.Default.DbServerName,
                DbHost = Settings.Default.DbHost,
                DbPort = Settings.Default.DbPort,
                DbUser = Settings.Default.DbUser
            };

            // Load offline path
            string offlineConfig = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "offline_path.txt");
            if (File.Exists(offlineConfig))
                Current.OfflineFolderPath = File.ReadAllText(offlineConfig).Trim();
            else
                Current.OfflineFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OfflineData");

            // Load category rules
            string categoryRulesPath = "category_rules.json";
            if (File.Exists(categoryRulesPath))
            {
                try
                {
                    string json = File.ReadAllText(categoryRulesPath);
                    Current.CategoryRules = JsonConvert.DeserializeObject<List<CategoryRule>>(json) ?? new List<CategoryRule>();
                }
                catch { }
            }
        }

        public void Save()
        {
            if (Current == null) return;

            string configPath = GetResolvedConfigPath();

            try
            {
                // Ensure directory exists
                string dir = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                // Save to main general_config.json
                string json = JsonConvert.SerializeObject(Current, Formatting.Indented);
                File.WriteAllText(configPath, json);

                // Save to legacy backup (Properties.Settings)
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
                Settings.Default.Save();

                // Save legacy offline path
                string offlineConfig = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "offline_path.txt");
                File.WriteAllText(offlineConfig, Current.OfflineFolderPath);

                // Save legacy category rules
                string categoryRulesPath = "category_rules.json";
                if (Current.CategoryRules != null)
                {
                    var orderedRules = Current.CategoryRules.OrderByDescending(r => r.Priority).ToList();
                    string rulesJson = JsonConvert.SerializeObject(orderedRules, Formatting.Indented);
                    File.WriteAllText(categoryRulesPath, rulesJson);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving general config: {ex.Message}");
            }
        }
    }
}
