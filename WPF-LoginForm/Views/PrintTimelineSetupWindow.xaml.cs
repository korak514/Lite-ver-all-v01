// Views/PrintTimelineSetupWindow.xaml.cs
using System;
using System.Printing;
using System.Windows;
using System.Windows.Media;
using WPF_LoginForm.ViewModels;

namespace WPF_LoginForm.Views
{
    public partial class PrintTimelineSetupWindow : Window
    {
        public PrintTimelineSetupWindow(PrintTimelineSetupViewModel viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;

            // Subscribe to the print event from the ViewModel
            viewModel.PrintRequested += ViewModel_PrintRequested;
        }

        private void ViewModel_PrintRequested()
        {
            // Create a standard WPF Print Dialog
            var printDialog = new System.Windows.Controls.PrintDialog();

            // FIX: Safely assign orientation. If PrintTicket is null (happens with some virtual PDF printers), bypass it to avoid crash.
            if (printDialog.PrintTicket != null)
            {
                printDialog.PrintTicket.PageOrientation = System.Printing.PageOrientation.Landscape;
            }

            if (printDialog.ShowDialog() == true)
            {
                // Remove the shadow effect temporarily for printing so it looks clean on paper
                var originalEffect = PrintArea.Effect;
                PrintArea.Effect = null;

                try
                {
                    // Measure and arrange the PrintArea to ensure it's sized exactly at A4 (1123x794)
                    Size pageSize = new Size(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight);

                    PrintArea.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    PrintArea.Arrange(new Rect(new Point(0, 0), PrintArea.DesiredSize));

                    // Execute Print
                    printDialog.PrintVisual(PrintArea, "Timeline Report");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to print: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    // Restore the drop shadow effect for the screen preview
                    PrintArea.Effect = originalEffect;
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Clean up event subscription to prevent memory leaks
            if (this.DataContext is PrintTimelineSetupViewModel vm)
            {
                vm.PrintRequested -= ViewModel_PrintRequested;
            }
        }
    }
}