using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WPF_LoginForm.ViewModels;

namespace WPF_LoginForm.Views
{
    public partial class AddRowWindow : Window
    {
        private bool _firstTextBoxFocused = false;

        public AddRowWindow()
        {
            InitializeComponent();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // --- NEW: Validation Logic ---
            if (DataContext is AddRowViewModel vm)
            {
                if (!vm.ValidateData(out string error))
                {
                    // Show error and STOP. Keep window open so user can fix typo.
                    MessageBox.Show(error, "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            // If valid, close window and return True
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
                else if (VisualTreeHelper.GetChildrenCount(this) > 0)
                {
                    textBox.Focus();
                    _firstTextBoxFocused = true;
                }
            }
        }

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