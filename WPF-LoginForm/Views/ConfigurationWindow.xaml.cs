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
using WPF_LoginForm.Models;
using WPF_LoginForm.ViewModels;

namespace WPF_LoginForm.Views
{
    public partial class ConfigurationWindow : Window
    {
        public ConfigurationWindow()
        {
            InitializeComponent();
            this.DataContextChanged += ConfigurationWindow_DataContextChanged;
        }

        private void ConfigurationWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is ConfigurationViewModel viewModel)
            {
                viewModel.OpenNodeEditorAction = (series, availableColumns) =>
                {
                    var nodeEditorVM = new NodeEditorViewModel(series, availableColumns);

                    var nodeEditorWin = new NodeEditorWindow
                    {
                        DataContext = nodeEditorVM,
                        Owner = this
                    };

                    nodeEditorVM.CloseAction = () => nodeEditorWin.Close();

                    nodeEditorWin.ShowDialog();
                };
            }
        }
    }
}