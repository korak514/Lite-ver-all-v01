using System.Windows;
using System.Windows.Controls;

namespace WPF_LoginForm.Views
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
        }

        private void pwNewOffline_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.SettingsViewModel vm)
            {
                vm.NewOfflinePassword = pwNewOffline.SecurePassword;
            }
        }
    }
}
