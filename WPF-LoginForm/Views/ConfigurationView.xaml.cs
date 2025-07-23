using System.Windows;
using WPF_LoginForm.ViewModels;

namespace WPF_LoginForm.Views
{
    public partial class ConfigurationView : Window
    {
        public ConfigurationView()
        {
            InitializeComponent();
            Loaded += (sender, args) =>
            {
                if (DataContext is ConfigurationViewModel viewModel)
                {
                    viewModel.CloseAction = () =>
                    {
                        DialogResult = true;
                        Close();
                    };
                }
            };
        }
    }
}
