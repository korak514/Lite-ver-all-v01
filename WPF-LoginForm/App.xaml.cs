using System.Globalization;
using System.Threading;
using System.Windows;
using WPF_LoginForm.Services;
using WPF_LoginForm.Services.Database; // Namespace for DbConnectionFactory
using WPF_LoginForm.Views;

namespace WPF_LoginForm
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // Global Logger Instance
        public static ILogger GlobalLogger { get; private set; }

        protected void ApplicationStart(object sender, StartupEventArgs e)
        {
            // 1. Setup Culture
            var culture = new CultureInfo("tr-TR");
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(
                    System.Windows.Markup.XmlLanguage.GetLanguage(culture.IetfLanguageTag)));

            // 2. Set Database Mode (Change this later to test Postgres)
            DbConnectionFactory.CurrentDatabaseType = DatabaseType.SqlServer;

            // 3. Initialize Logger (Database wrapped around File)
            var fileLogger = new FileLogger("AppLog");
            GlobalLogger = new DatabaseLogger(fileLogger);

            GlobalLogger.LogInfo("Application Starting...");

            // 4. Launch Login View
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