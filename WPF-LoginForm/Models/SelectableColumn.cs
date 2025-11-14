using System.ComponentModel;

namespace WPF_LoginForm.Models
{
    public class SelectableColumn
    {
        public string Name { get; set; }  // Column name (e.g., "ProductID")
        public bool IsSelected { get; set; } // For checkbox binding
    }
}