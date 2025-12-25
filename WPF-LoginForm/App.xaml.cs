using System.Globalization;
using System.Threading;
using System.Windows;
using WPF_LoginForm.Properties;
using WPF_LoginForm.Services;
using WPF_LoginForm.Services.Database;
using WPF_LoginForm.ViewModels;
using WPF_LoginForm.Views;

namespace WPF_LoginForm
{
    public partial class App : Application
    {
        public static ILogger GlobalLogger { get; private set; }

        protected void ApplicationStart(object sender, StartupEventArgs e)
        {
            // 1. Apply Language
            string languageCode = Settings.Default.AppLanguage;
            if (string.IsNullOrEmpty(languageCode)) languageCode = "en-US";

            var culture = new CultureInfo(languageCode);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(System.Windows.Markup.XmlLanguage.GetLanguage(culture.IetfLanguageTag)));

            // 2. Initialize Logger
            var dbType = DbConnectionFactory.CurrentDatabaseType;
            var fileLogger = new FileLogger("AppLog");
            GlobalLogger = new DatabaseLogger(fileLogger);
            GlobalLogger.LogInfo($"App Starting... Lang: {languageCode} | Provider: {dbType}");

            // --- RE-ENABLED: Database Bootstrapper ---
            // This checks if the DB exists. If not, it asks to create it.
            // We ignore the return value (true/false).
            // If it succeeds -> Good.
            // If it fails/cancels -> We still show LoginView so you can go to Settings.
            DatabaseBootstrapper.Run();
            // -----------------------------------------

            // 3. Show Login
            var loginView = new LoginView();
            loginView.Show();

            loginView.IsVisibleChanged += (s, ev) =>
            {
                if (loginView.IsVisible == false && loginView.IsLoaded)
                {
                    // Logic to switch to MainView or SettingsView based on ViewModel state
                    var loginVM = loginView.DataContext as LoginViewModel;
                    var mainView = new MainView();

                    if (mainView.DataContext is MainViewModel mainVM)
                    {
                        if (loginVM != null && loginVM.IsSettingsModeOnly)
                        {
                            mainVM.Initialize(AppMode.SettingsOnly);
                        }
                        else
                        {
                            bool isReport = loginVM?.IsReportModeOnly ?? false;
                            mainVM.Initialize(isReport ? AppMode.ReportOnly : AppMode.Normal);
                        }
                    }

                    mainView.Show();
                    loginView.Close();
                }
            };
        }
    }
}