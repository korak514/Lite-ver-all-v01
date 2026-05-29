// Views/PasswordChangeView.xaml.cs
using System.Windows;
using System.Windows.Input;
using WPF_LoginForm.ViewModels;

namespace WPF_LoginForm.Views
{
    public partial class PasswordChangeView : Window
    {
        public PasswordChangeView()
        {
            InitializeComponent();
            DataContext = new PasswordChangeViewModel();
        }

        public PasswordChangeView(bool isOnline, string currentUsername)
        {
            InitializeComponent();
            DataContext = new PasswordChangeViewModel(isOnline, currentUsername);
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
