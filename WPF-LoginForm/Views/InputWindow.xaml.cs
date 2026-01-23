using System.Windows;

namespace WPF_LoginForm.Views
{
    public partial class InputWindow : Window
    {
        public string ResponseText { get; private set; }

        public InputWindow(string title, string message, string defaultValue = "")
        {
            InitializeComponent();
            this.Title = title;
            lblMessage.Text = message;
            txtInput.Text = defaultValue;
            txtInput.Focus();
            txtInput.SelectAll();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            ResponseText = txtInput.Text;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}