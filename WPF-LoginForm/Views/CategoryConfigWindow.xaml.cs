// Views/CategoryConfigWindow.xaml.cs
using System.Collections.Generic;
using System.Windows;
using WPF_LoginForm.ViewModels;

namespace WPF_LoginForm.Views
{
    public partial class CategoryConfigWindow : Window
    {
        public CategoryConfigWindow() : this(null)
        {
        }

        public CategoryConfigWindow(List<string> distinctDescriptions)
        {
            InitializeComponent();
            var vm = new CategoryConfigViewModel(distinctDescriptions);

            vm.CloseAction = () =>
            {
                this.DialogResult = true;
                this.Close();
            };

            this.DataContext = vm;
        }
    }
}