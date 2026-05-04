// Models/GeneralSettings.cs
using System.Collections.Generic;

namespace WPF_LoginForm.Models
{
    public class GeneralSettings
    {
        // Database Connection Logic
        public string DbProvider { get; set; } = "SqlServer";
        public string SqlAuthConnString { get; set; } = "Server=(local); Database=LoginDb; Integrated Security=true";
        public string SqlDataConnString { get; set; } = "Server=(local); Database=MainDataDb; Integrated Security=true";
        public string PostgresDataConnString { get; set; } = "Host=localhost; Username=postgres; Password=password; Database=MainDataDb";
        public string PostgresAuthConnString { get; set; } = "Host=localhost; Username=postgres; Password=password; Database=LoginDb";

        // App General Settings
        public string AppLanguage { get; set; } = "en-US";
        public string OfflineFolderPath { get; set; } = "";

        // Auto Import Settings
        public bool AutoImportEnabled { get; set; } = false;
        public bool ImportIsRelative { get; set; } = true;
        public string ImportFileName { get; set; } = "dashboard_config.json";
        public string ImportAbsolutePath { get; set; } = "";

        // Dashboard Settings
        public bool ShowDashboardDateFilter { get; set; } = true;
        public int DashboardDateTickSize { get; set; } = 1;
        public int DefaultRowLimit { get; set; } = 500;

        // Network & Resilience Settings
        public int ConnectionTimeout { get; set; } = 15;
        public bool TrustServerCertificate { get; set; } = true;
        public string DbServerName { get; set; } = "";

        // Manual Connection Fields
        public string DbHost { get; set; } = "localhost";
        public string DbPort { get; set; } = "1433";
        public string DbUser { get; set; } = "admin";

        // Category Rules (Analytics)
        public List<CategoryRule> CategoryRules { get; set; } = new List<CategoryRule>();
    }
}
