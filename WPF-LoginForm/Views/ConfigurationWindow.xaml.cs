// Views/ConfigurationWindow.xaml.cs
using System.Windows;
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

                    if (nodeEditorVM.IsSaved)
                    {
                        var configVM = viewModel.CurrentConfiguration;
                        if (configVM != null)
                            configVM.ShowLabelsOnChart = !series.ShowOnlyHoverLabels;
                    }
                };
            }
        }
    }
}