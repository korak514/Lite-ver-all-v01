using System.Globalization;
using System.Threading;
using System.Windows;
using WPF_LoginForm.Views;

namespace WPF_LoginForm
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected void ApplicationStart(object sender, StartupEventArgs e)
        {

            var culture = new CultureInfo("tr-TR");
            // var culture = new CultureInfo("en-GB"); // Alternative for dd/MM/yyyy
            // var culture = CultureInfo.InvariantCulture; // For culture-invariant formatting (often good for machine-to-machine)

            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            // This is important for WPF controls like DatePicker, validation messages, etc., 
            // to use the correct regional settings and language.
            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(
                    System.Windows.Markup.XmlLanguage.GetLanguage(culture.IetfLanguageTag)));

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
