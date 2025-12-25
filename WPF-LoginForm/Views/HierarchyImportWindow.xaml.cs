using System.Windows;
using WPF_LoginForm.ViewModels;

namespace WPF_LoginForm.Views
{
    public partial class HierarchyImportWindow : Window
    {
        public HierarchyImportWindow()
        {
            InitializeComponent();
        }

        private void UpdatePreview_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is HierarchyImportViewModel vm)
            {
                vm.GeneratePreview();
            }
        }
    }
}