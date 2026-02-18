// Views/CategoryConfigWindow.xaml.cs
using System.Windows;
using WPF_LoginForm.ViewModels; // <--- Critical for finding CategoryConfigViewModel

namespace WPF_LoginForm.Views
{
    public partial class CategoryConfigWindow : Window
    {
        public CategoryConfigWindow()
        {
            InitializeComponent();
            var vm = new CategoryConfigViewModel(); // Should now resolve correctly

            // Allow ViewModel to close the window
            vm.CloseAction = () =>
            {
                this.DialogResult = true;
                this.Close();
            };

            this.DataContext = vm;
        }
    }
}