using System.Globalization;
using System.Threading;
using System.Windows;
using WPF_LoginForm.Properties; // To access Settings
using WPF_LoginForm.Services;
using WPF_LoginForm.Services.Database;
using WPF_LoginForm.Views;

namespace WPF_LoginForm
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static ILogger GlobalLogger { get; private set; }

        protected void ApplicationStart(object sender, StartupEventArgs e)
        {
            // 1. APPLY LANGUAGE FROM SETTINGS
            // Read the setting (defaults to "en-US" if empty)
            string languageCode = Settings.Default.AppLanguage;
            if (string.IsNullOrEmpty(languageCode))
            {
                languageCode = "en-US";
            }

            var culture = new CultureInfo(languageCode);

            // Set the culture for date/number formatting
            Thread.CurrentThread.CurrentCulture = culture;
            // Set the culture for UI Strings (ResX)
            Thread.CurrentThread.CurrentUICulture = culture;

            // Ensure WPF framework controls (like DatePicker) match
            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(
                    System.Windows.Markup.XmlLanguage.GetLanguage(culture.IetfLanguageTag)));

            // 2. Initialize Database Configuration
            // This reads the connection strings from Settings via the property logic we added earlier
            var dbType = DbConnectionFactory.CurrentDatabaseType;

            // 3. Initialize Logger
            var fileLogger = new FileLogger("AppLog");
            GlobalLogger = new DatabaseLogger(fileLogger);

            GlobalLogger.LogInfo($"Application Starting... Language: {languageCode}, DB: {dbType}");

            // 4. Show Login View
            var loginView = new LoginView();
            loginView.Show();

            loginView.IsVisibleChanged += (s, ev) =>
            {
                if (loginView.IsVisible == false && loginView.IsLoaded)
                {
                    var mainView = new MainView();
                    mainView.Show();
                    loginView.Close();
                }
            };
        }
    }
}