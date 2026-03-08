// Views/StartupView.xaml.cs
using System;
using System.Threading.Tasks;
using System.Windows;
using WPF_LoginForm.ViewModels;

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
            // --- UPDATED: Fast Startup for "Offline First" Approach ---
            // We skip Pings and DB Connectivity checks here.
            // Connectivity is now handled on-demand in the LoginView
            // when the user explicitly checks "Go Online".

            try
            {
                UpdateStatus("Initializing Application...", 20);
                await Task.Delay(400); // Visual delay for smooth UX

                UpdateStatus("Loading Resources...", 60);
                await Task.Delay(400);

                UpdateStatus("Starting...", 100);
                await Task.Delay(200);

                OpenLoginAndListen();
            }
            catch (Exception ex)
            {
                // Fallback catch, though unlikely to be hit with removed network logic
                ShowError($"Startup Error: {ex.Message}");
            }
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

                            // Check the ViewModel property 'IsOnlineMode'
                            // If it is FALSE (unchecked), we launch in OfflineReadOnly mode.
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