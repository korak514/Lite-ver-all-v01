using System.Collections.Generic;
using System.Windows;
using System.Linq; // For Any()
using WPF_LoginForm.ViewModels; // For AddRowLongViewModel

namespace WPF_LoginForm.Views
{
    public partial class AddRowLongWindow : Window
    {
        public AddRowLongWindow()
        {
            InitializeComponent();
            // Optional: Set Owner if not done by DialogService or calling code
            // if (Application.Current.MainWindow != this && Application.Current.MainWindow != null)
            // {
            // Owner = Application.Current.MainWindow;
            // }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is AddRowLongViewModel viewModel)
            {
                var newRowData = viewModel.GetEnteredData(out List<string> validationErrors);

                if (validationErrors != null && validationErrors.Any())
                {
                    // Display errors to the user
                    string errorMessages = string.Join("\n", validationErrors);
                    MessageBox.Show(this, $"Please correct the following errors:\n\n{errorMessages}", "Validation Errors", MessageBoxButton.OK, MessageBoxImage.Error);
                    // DialogResult is not set, so window stays open
                }
                else if (newRowData != null) // Success and data is present
                {
                    DialogResult = true;
                }
                else // GetEnteredData returned null but no explicit errors (should ideally not happen if logic is sound)
                {
                    MessageBox.Show(this, "No data was entered or an unexpected issue occurred.", "No Data", MessageBoxButton.OK, MessageBoxImage.Warning);
                    // DialogResult remains null, window stays open or handle as needed
                }
            }
            else
            {
                // Should not happen if DataContext is correctly set
                MessageBox.Show(this, "ViewModel not available. Cannot process data.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}