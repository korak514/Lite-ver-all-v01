using System;
using System.Collections.ObjectModel; // Required
using System.Linq;
using System.Windows;
using WPF_LoginForm.Models;
using WPF_LoginForm.Services;

namespace WPF_LoginForm.Views
{
    public partial class CategoryConfigWindow : Window
    {
        private readonly CategoryMappingService _service;

        // The DataGrid binds to this Property
        public ObservableCollection<CategoryRule> Rules { get; set; }

        public CategoryConfigWindow()
        {
            InitializeComponent();
            _service = new CategoryMappingService();

            // 1. Load from JSON
            var loadedList = _service.LoadRules();

            // 2. Initialize Collection
            Rules = new ObservableCollection<CategoryRule>(loadedList);

            // 3. IF EMPTY: Add a sample row so the user understands what to do
            if (Rules.Count == 0)
            {
                Rules.Add(new CategoryRule { StartsWith = "ACIL-STOP", MapTo = "Acil Durum" });
            }

            // 4. Set DataContext (CRITICAL: Must be done last)
            this.DataContext = this;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Remove empty rows before saving
                var cleanList = Rules.Where(r => !string.IsNullOrWhiteSpace(r.StartsWith)).ToList();

                _service.SaveRules(cleanList);

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving: {ex.Message}");
            }
        }
    }
}