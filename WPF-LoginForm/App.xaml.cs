using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
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
            // 1. Initialize Logging Service FIRST so we can catch startup errors
            // Use FileLogger initially because DB might not be ready
            var fileLogger = new FileLogger("AppLog");

            // DatabaseLogger wraps the file logger. If DB fails, it falls back to file.
            GlobalLogger = new DatabaseLogger(fileLogger);

            // 2. Setup Global Exception Handling
            SetupExceptionHandling();

            base.OnStartup(e);

            // 3. Apply Language Settings
            ApplyLocalization();

            var dbType = DbConnectionFactory.CurrentDatabaseType;
            GlobalLogger.LogInfo($"App Starting... Lang: {Settings.Default.AppLanguage} | Provider: {dbType}");
        }

        private void ApplyLocalization()
        {
            try
            {
                string languageCode = Settings.Default.AppLanguage;
                if (string.IsNullOrEmpty(languageCode)) languageCode = "en-US";

                var culture = new CultureInfo(languageCode);

                // UI Thread Culture
                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;

                // Background Thread Culture (.NET 4.5+)
                CultureInfo.DefaultThreadCurrentCulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;

                // WPF Framework Element Localization
                FrameworkElement.LanguageProperty.OverrideMetadata(
                    typeof(FrameworkElement),
                    new FrameworkPropertyMetadata(System.Windows.Markup.XmlLanguage.GetLanguage(culture.IetfLanguageTag)));
            }
            catch (Exception ex)
            {
                GlobalLogger.LogError("Error applying localization", ex);
            }
        }

        private void SetupExceptionHandling()
        {
            // 1. Catch exceptions on the main UI dispatcher thread
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;

            // 2. Catch exceptions in non-UI threads (Background Tasks)
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            // 3. Catch any other catastrophic exceptions (AppDomain level)
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            GlobalLogger.LogError("CRITICAL UI ERROR", e.Exception);

            ShowCrashMessage(e.Exception);

            // Prevent immediate crash if possible, but state might be corrupted
            e.Handled = true;
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            GlobalLogger.LogError("BACKGROUND TASK ERROR", e.Exception);

            // Prevent process termination
            e.SetObserved();
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                GlobalLogger.LogError("FATAL APP ERROR", ex);
                MessageBox.Show($"A fatal error occurred and the application must close.\n\nError: {ex.Message}",
                    "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowCrashMessage(Exception ex)
        {
            string msg = $"An unexpected error occurred.\n\nDetails: {ex.Message}\n\nThe error has been logged.";
            MessageBox.Show(msg, "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            GlobalLogger.LogInfo("App Shutting Down.");
            base.OnExit(e);
        }
    }
}