using System.Globalization;
using System.Threading;
using System.Windows;
using WPF_LoginForm.Properties;
using WPF_LoginForm.Services;
using WPF_LoginForm.Services.Database;
using WPF_LoginForm.ViewModels; // Need this for casting
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

            // 2. Initialize DB & Logger
            var dbType = DbConnectionFactory.CurrentDatabaseType;
            var fileLogger = new FileLogger("AppLog");
            GlobalLogger = new DatabaseLogger(fileLogger);
            GlobalLogger.LogInfo($"App Starting... Lang: {languageCode}");

            // 3. Show Login
            var loginView = new LoginView();
            loginView.Show();

            loginView.IsVisibleChanged += (s, ev) =>
            {
                if (loginView.IsVisible == false && loginView.IsLoaded)
                {
                    // === PHASE 3: TRANSFER REPORT MODE STATE ===
                    var loginVM = loginView.DataContext as LoginViewModel;
                    bool reportMode = loginVM?.IsReportModeOnly ?? false;

                    var mainView = new MainView();

                    // Initialize MainViewModel with the mode
                    if (mainView.DataContext is MainViewModel mainVM)
                    {
                        mainVM.Initialize(reportMode);
                    }

                    mainView.Show();
                    loginView.Close();
                }
            };
        }
    }
}