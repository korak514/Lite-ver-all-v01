using System;
using System.Windows;
using WPF_LoginForm.ViewModels;

namespace WPF_LoginForm.Views
{
    /// <summary>
    /// Interaction logic for ErrorDrillDownWindow.xaml
    /// </summary>
    public partial class ErrorDrillDownWindow : Window
    {
        public ErrorDrillDownWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Event handler for the Close/OK button.
        /// Matches the Click="CloseButton_Click" in the XAML.
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        public void SetTheme(bool isDark)
        {
            var themeUri = isDark
                ? new Uri("Themes/DarkTheme.xaml", UriKind.Relative)
                : new Uri("Themes/LightTheme.xaml", UriKind.Relative);

            ResourceDictionary newTheme = new ResourceDictionary { Source = themeUri };

            // Find and replace the old theme dictionary
            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(newTheme);
        }

        public void SetViewModel(ErrorDrillDownViewModel viewModel)
        {
            this.DataContext = viewModel;
        }

        private bool _isDark = true;

        private void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            _isDark = !_isDark;
            SetTheme(_isDark);
        }
    }
}