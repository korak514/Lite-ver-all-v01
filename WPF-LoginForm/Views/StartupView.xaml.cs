// Views/StartupView.xaml.cs
using System;
using System.Threading.Tasks;
using System.Windows;
using WPF_LoginForm.ViewModels;
using WPF_LoginForm.Services;
using WPF_LoginForm.Properties;

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
                UpdateStatus("Initializing Application...", 20);
                await Task.Delay(400);

                UpdateStatus("Loading Resources...", 60);
                await Task.Delay(400);

                var current = GeneralSettingsManager.Instance.Current;
                bool isFirstRun = IsFirstRunWithoutDatabase();
                bool isPureOffline = current != null && current.PureOfflineMode;

                if (isFirstRun && !isPureOffline)
                {
                    var result = MessageBox.Show(
                        WPF_LoginForm.Properties.Resources.Str_FirstRunMessage,
                        WPF_LoginForm.Properties.Resources.Str_FirstRunTitle,
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question,
                        MessageBoxResult.Yes
                    );

                    if (result == MessageBoxResult.Yes)
                    {
                        OpenLoginWithSettings();
                        return;
                    }
                    else if (result == MessageBoxResult.Cancel)
                    {
                        // Always Offline Mode - save flag permanently
                        current.PureOfflineMode = true;
                        Settings.Default.PureOfflineMode = true;
                        try { Settings.Default.Save(); } catch { }

                        UpdateStatus("Starting in Offline Mode...", 100);
                        await Task.Delay(200);
                        OpenLoginAndListen();
                        return;
                    }
                    // MessageBoxResult.No = One-time Offline Mode (will ask again next run)
                }

                UpdateStatus("Starting...", 100);
                await Task.Delay(200);

                OpenLoginAndListen();
            }
            catch (Exception ex)
            {
                ShowError($"Startup Error: {ex.Message}");
            }
        }

        private bool IsFirstRunWithoutDatabase()
        {
            var current = GeneralSettingsManager.Instance.Current;
            if (current == null) return true;

            bool hasDbHost = !string.IsNullOrWhiteSpace(current.DbHost);
            bool hasDbServer = !string.IsNullOrWhiteSpace(current.DbServerName);
            bool hasSqlAuthConn = !string.IsNullOrWhiteSpace(current.SqlAuthConnString);
            bool hasSqlDataConn = !string.IsNullOrWhiteSpace(current.SqlDataConnString);
            bool hasPostgresAuthConn = !string.IsNullOrWhiteSpace(current.PostgresAuthConnString);
            bool hasPostgresDataConn = !string.IsNullOrWhiteSpace(current.PostgresDataConnString);

            return !hasDbHost && !hasDbServer && !hasSqlAuthConn && !hasSqlDataConn && !hasPostgresAuthConn && !hasPostgresDataConn;
        }

        private void OpenLoginWithSettings()
        {
            var loginView = new LoginView();
            var loginVM = loginView.DataContext as LoginViewModel;
            if (loginVM != null)
            {
                loginVM.IsSettingsModeOnly = true;
            }

            loginView.IsVisibleChanged += (s, ev) =>
            {
                if (loginView.IsVisible == false && loginView.IsLoaded)
                {
                    if (loginView.WindowState == WindowState.Minimized) return;

                    var vm = loginView.DataContext as LoginViewModel;
                    if (vm != null && !vm.IsViewVisible)
                    {
                        var mainView = new MainView();
                        if (mainView.DataContext is MainViewModel mainVM)
                        {
                            mainVM.Initialize(AppMode.SettingsOnly);
                        }
                        mainView.Show();
                        loginView.Close();
                    }
                }
            };

            loginView.Show();
            this.Close();
        }

        private void OpenLoginAndListen()
        {
            var loginView = new LoginView();

            loginView.IsVisibleChanged += (s, ev) =>
            {
                if (loginView.IsVisible == false && loginView.IsLoaded)
                {
                    if (loginView.WindowState == WindowState.Minimized)
                        return;

                    var loginVM = loginView.DataContext as LoginViewModel;

                    if (loginVM != null && !loginVM.IsViewVisible)
                    {
                        var mainView = new MainView();
                        if (mainView.DataContext is MainViewModel mainVM)
                        {
                            if (loginVM.IsSettingsModeOnly)
                                mainVM.Initialize(AppMode.SettingsOnly);

                            else if (!loginVM.IsOnlineMode)
                                mainVM.Initialize(AppMode.OfflineReadOnly);
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
    }
}