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
        // Global Logger instance accessible throughout the app
        public static ILogger GlobalLogger { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. Apply Language Settings
            // This ensures the app starts in English or Turkish based on previous settings
            string languageCode = Settings.Default.AppLanguage;
            if (string.IsNullOrEmpty(languageCode)) languageCode = "en-US";

            var culture = new CultureInfo(languageCode);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            // Fix for WPF Frame/Element localization
            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(System.Windows.Markup.XmlLanguage.GetLanguage(culture.IetfLanguageTag)));

            // 2. Initialize Logging Service
            // We set up the logger here so even startup errors can be recorded
            var dbType = DbConnectionFactory.CurrentDatabaseType;

            // Fallback file logger (in case DB is down)
            var fileLogger = new FileLogger("AppLog");

            // Smart DB Logger (writes to DB if connected, File if not)
            GlobalLogger = new DatabaseLogger(fileLogger);

            GlobalLogger.LogInfo($"App Starting... Lang: {languageCode} | Provider: {dbType}");

            // Note: The visual startup sequence (checking DB connection, Bootstrapper)
            // is now handled by Views/StartupView.xaml.cs, which is launched via StartupUri in App.xaml.
        }
    }
}