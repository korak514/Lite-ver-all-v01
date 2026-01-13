using System.IO; // Required for File.Exists
using System.Windows;
using WPF_LoginForm.ViewModels;

namespace WPF_LoginForm.Views
{
    /// <summary>
    /// Interaction logic for ImportTableWindow.xaml
    /// </summary>
    public partial class ImportTableWindow : Window
    {
        public ImportTableWindow()
        {
            InitializeComponent();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // FIX: Validate file existence before closing the dialog
            if (DataContext is ImportTableViewModel vm)
            {
                if (string.IsNullOrWhiteSpace(vm.FilePath) || !File.Exists(vm.FilePath))
                {
                    MessageBox.Show("The specified file does not exist. Please check the path.",
                                    "File Error",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                    // Return without setting DialogResult, keeping the window open
                    return;
                }
            }

            // Only close if valid
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}