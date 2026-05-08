// App.xaml.cs
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using WPF_LoginForm.Services;
using WPF_LoginForm.Services.Database;
using WPF_LoginForm.Views;

namespace WPF_LoginForm
{
    public partial class App : Application
    {
        public static ILogger GlobalLogger { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            // 1. Initialize Logging Service FIRST
            var fileLogger = new FileLogger("AppLog");
            GlobalLogger = new DatabaseLogger(fileLogger);

            // 2. Setup Global Exception Handling
            SetupExceptionHandling();

            // 3. Apply Language Settings BEFORE showing any windows
            ApplyLocalization();

            base.OnStartup(e);

            var dbType = DbConnectionFactory.CurrentDatabaseType;
            GlobalLogger.LogInfo($"App Starting... Lang: {Thread.CurrentThread.CurrentUICulture.Name} | Provider: {dbType}");

            // 4. Manually launch the StartupView now that the culture is set
            var startupWindow = new StartupView();
            startupWindow.Show();
        }

        private void ApplyLocalization()
        {
            try
            {
                // Force load settings to ensure GeneralSettingsManager loads correctly before UI binds to it
                GeneralSettingsManager.Instance.Load();
                
                string languageCode = GeneralSettingsManager.Instance.Current.AppLanguage;
                
                if (string.IsNullOrEmpty(languageCode)) 
                    languageCode = "en-US";

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
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            GlobalLogger.LogError("CRITICAL UI ERROR", e.Exception);
            ShowCrashMessage(e.Exception);
            e.Handled = true;
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            GlobalLogger.LogError("BACKGROUND TASK ERROR", e.Exception);
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