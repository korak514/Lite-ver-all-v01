using Newtonsoft.Json;
using System;
using System.IO;
using WPF_LoginForm.Models;

namespace WPF_LoginForm.Services
{
    public class DashboardStorageService
    {
        private readonly string _defaultFilePath;

        public DashboardStorageService()
        {
            // Default Save Location: %AppData%\WPF_LoginForm\dashboard_config.json
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WPF_LoginForm");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            _defaultFilePath = Path.Combine(folder, "dashboard_config.json");
        }

        public void SaveSnapshot(DashboardSnapshot snapshot)
        {
            try
            {
                string json = JsonConvert.SerializeObject(snapshot, Formatting.Indented);
                File.WriteAllText(_defaultFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to auto-save dashboard: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads the dashboard configuration.
        /// </summary>
        /// <param name="customPath">If provided, attempts to load from this specific path. If null, loads from default AppData.</param>
        public DashboardSnapshot LoadSnapshot(string customPath = null)
        {
            string targetPath = string.IsNullOrEmpty(customPath) ? _defaultFilePath : customPath;

            if (!File.Exists(targetPath)) return null;

            try
            {
                string json = File.ReadAllText(targetPath);
                return JsonConvert.DeserializeObject<DashboardSnapshot>(json);
            }
            catch
            {
                return null;
            }
        }
    }
}