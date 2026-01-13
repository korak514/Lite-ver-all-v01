using System.Globalization;
using System.Threading;
using System.Windows;
using WPF_LoginForm.Properties;
using WPF_LoginForm.Services;
using WPF_LoginForm.Services.Database;

namespace WPF_LoginForm
{
    public partial class App : Application
    {
        public static ILogger GlobalLogger { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. Apply Language Settings
            string languageCode = Settings.Default.AppLanguage;
            if (string.IsNullOrEmpty(languageCode)) languageCode = "en-US";

            var culture = new CultureInfo(languageCode);

            // Standard UI Thread Culture
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            // FIX: Ensure Background Threads (Task.Run) also use this culture (.NET 4.5+)
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            // Fix for WPF Frame/Element localization
            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(System.Windows.Markup.XmlLanguage.GetLanguage(culture.IetfLanguageTag)));

            // 2. Initialize Logging Service
            var dbType = DbConnectionFactory.CurrentDatabaseType;
            var fileLogger = new FileLogger("AppLog");
            GlobalLogger = new DatabaseLogger(fileLogger);

            GlobalLogger.LogInfo($"App Starting... Lang: {languageCode} | Provider: {dbType}");
        }
    }
}