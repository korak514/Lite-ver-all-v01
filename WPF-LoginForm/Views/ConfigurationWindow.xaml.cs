// Views/ConfigurationWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using WPF_LoginForm.ViewModels;

namespace WPF_LoginForm.Views
{
    /// <summary>
    /// Interaction logic for ConfigurationWindow.xaml
    /// </summary>
    public partial class ConfigurationWindow : Window
    {
        public ConfigurationWindow()
        {
            InitializeComponent();

            // Subscribe to DataContext changes to wire up the Node Editor dialog
            this.DataContextChanged += ConfigurationWindow_DataContextChanged;
        }

        private void ConfigurationWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is ConfigurationViewModel viewModel)
            {
                // Wire up the action to open the Combination Label (Node Editor)
                viewModel.OpenNodeEditorAction = (series, availableColumns) =>
                {
                    // Create the ViewModel for the Node Editor
                    var nodeEditorVM = new NodeEditorViewModel(series, availableColumns);

                    // Create the Window
                    var nodeEditorWin = new NodeEditorWindow
                    {
                        DataContext = nodeEditorVM,
                        Owner = this // Keep it modal to the configuration window
                    };

                    // Bind the Close action
                    nodeEditorVM.CloseAction = () => nodeEditorWin.Close();

                    // Show as dialog
                    nodeEditorWin.ShowDialog();
                };
            }
        }
    }
}