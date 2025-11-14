// In WPF_LoginForm.Views/ImportTableWindow.cs
using System.Windows;

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
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}