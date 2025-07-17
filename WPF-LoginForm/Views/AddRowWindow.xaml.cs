using System.Windows;
using System.Windows.Controls; // Needed for TextBox, ItemsControl, ContentPresenter
using System.Windows.Media; // Needed for VisualTreeHelper

namespace WPF_LoginForm.Views // Ensure namespace is correct
{
    public partial class AddRowWindow : Window
    {
        private bool _firstTextBoxFocused = false;

        public AddRowWindow()
        {
            InitializeComponent();
             Owner = Application.Current?.MainWindow; // Optional: Set owner here or in DialogService
        }

        // --- ADD THESE MISSING EVENT HANDLERS ---
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ValueTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_firstTextBoxFocused && sender is TextBox textBox)
            {
                // Attempt to find if it's the first item
                ItemsControl itemsControl = FindAncestor<ItemsControl>(textBox);
                if (itemsControl != null)
                {
                    ContentPresenter cp = FindAncestor<ContentPresenter>(textBox);
                    if (cp != null && itemsControl.ItemContainerGenerator.IndexFromContainer(cp) == 0)
                    {
                        textBox.Focus();
                        _firstTextBoxFocused = true;
                    }
                }
                // Fallback if ancestor search fails (less reliable)
                else if (VisualTreeHelper.GetChildrenCount(this) > 0) // Basic check
                {
                    textBox.Focus();
                    _firstTextBoxFocused = true;
                }
            }
        }
        // --- END ADDED HANDLERS ---

        // Helper method to find visual ancestor (keep this)
        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            do
            {
                if (current is T specificAncestor)
                {
                    return specificAncestor;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            while (current != null);
            return null;
        }
    }
}