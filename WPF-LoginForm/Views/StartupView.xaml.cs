using System;
using System.Threading.Tasks;
using System.Windows;
using WPF_LoginForm.Services.Database;
using System.Net.NetworkInformation;
using WPF_LoginForm.Properties;
using WPF_LoginForm.ViewModels;
using WPF_LoginForm.Services.Network;

namespace WPF_LoginForm.Views
{
    public partial class StartupView : Window
    {
        public StartupView()
        {
            InitializeComponent();
        }

        private async void Window_ContentRendered(object sender, EventArgs e)
        {
            await RunStartupSequence();
        }

        private async Task RunStartupSequence()
        {
            try
            {
                UpdateStatus("Locating Server...", 10);

                string bestHost = await ConnectionManager.ResolveBestHostAsync();

                UpdateStatus($"Pinging {bestHost}...", 20);

                if (!IsLocalHost(bestHost))
                {
                    bool pingSuccess = await Task.Run(() =>
                    {
                        try { return new Ping().Send(bestHost, 2000).Status == IPStatus.Success; }
                        catch { return false; }
                    });

                    if (!pingSuccess)
                    {
                        ShowError($"Cannot reach server: {bestHost}\nCheck your VPN or Wi-Fi.");
                        return;
                    }
                }

                UpdateStatus("Connecting to Database...", 50);
                await Task.Delay(500);

                bool dbConnected = await Task.Run(() => DatabaseBootstrapper.Run());

                if (!dbConnected)
                {
                    ShowError("Database initialization failed. Check credentials.");
                    return;
                }

                UpdateStatus("Starting Application...", 100);
                await Task.Delay(500);

                OpenLoginAndListen();
            }
            catch (Exception ex)
            {
                ShowError($"Startup Error: {ex.Message}");
            }
        }

        private void OpenLoginAndListen()
        {
            var loginView = new LoginView();

            loginView.IsVisibleChanged += (s, ev) =>
            {
                // FIX: Added WindowState check
                if (loginView.IsVisible == false && loginView.IsLoaded)
                {
                    // If user minimized the window, do NOT launch main app
                    if (loginView.WindowState == WindowState.Minimized)
                        return;

                    var loginVM = loginView.DataContext as LoginViewModel;

                    // Check if ViewModel actually signaled a successful login
                    if (loginVM != null && !loginVM.IsViewVisible)
                    {
                        var mainView = new MainView();
                        if (mainView.DataContext is MainViewModel mainVM)
                        {
                            if (loginVM.IsSettingsModeOnly)
                                mainVM.Initialize(AppMode.SettingsOnly);
                            else
                            {
                                bool isReport = loginVM.IsReportModeOnly;
                                mainVM.Initialize(isReport ? AppMode.ReportOnly : AppMode.Normal);
                            }
                        }
                        mainView.Show();
                        loginView.Close();
                    }
                }
            };

            loginView.Show();
            this.Close();
        }

        private void UpdateStatus(string message, int progress)
        {
            txtStatus.Text = message;
            progressBar.Value = progress;
        }

        private void ShowError(string message)
        {
            txtStatus.Text = message;
            txtStatus.Foreground = System.Windows.Media.Brushes.Red;
            progressBar.Visibility = Visibility.Collapsed;
            btnSettings.Visibility = Visibility.Visible;
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            OpenLoginAndListen();
        }

        private bool IsLocalHost(string host)
        {
            return string.IsNullOrEmpty(host) ||
                   host.ToLower() == "localhost" ||
                   host == "." ||
                   host.ToLower() == "(local)" ||
                   host == "127.0.0.1";
        }
    }
}