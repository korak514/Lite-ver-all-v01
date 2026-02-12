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

        /// <summary>
        /// Optional: Helper to set the ViewModel easily from other parts of the app.
        /// </summary>
        public void SetViewModel(ErrorDrillDownViewModel viewModel)
        {
            this.DataContext = viewModel;
        }
    }
}