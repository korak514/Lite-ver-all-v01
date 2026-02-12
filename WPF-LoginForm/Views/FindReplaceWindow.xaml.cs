using System.Windows;

namespace WPF_LoginForm.Views
{
    /// <summary>
    /// Interaction logic for FindReplaceWindow.xaml
    /// </summary>
    public partial class FindReplaceWindow : Window
    {
        // Properties to expose UI values to the ViewModel
        public string FindText => txtFind.Text;

        public string ReplaceText => txtReplace.Text;
        public bool MatchCase => chkMatchCase.IsChecked == true;

        // Events to notify the ViewModel when buttons are clicked
        public event RoutedEventHandler FindRequested;

        public event RoutedEventHandler ReplaceRequested;

        public FindReplaceWindow()
        {
            InitializeComponent();
        }

        // Event Handler for "Find" Button
        private void Find_Click(object sender, RoutedEventArgs e)
        {
            FindRequested?.Invoke(this, e);
        }

        // Event Handler for "Replace" Button
        private void Replace_Click(object sender, RoutedEventArgs e)
        {
            ReplaceRequested?.Invoke(this, e);
        }
    }
}