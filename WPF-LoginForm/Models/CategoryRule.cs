using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WPF_LoginForm.Models
{
    // Adding INotifyPropertyChanged ensures the UI updates instantly when you type
    public class CategoryRule : INotifyPropertyChanged
    {
        private string _startsWith;
        private string _mapTo;

        // PROPERTY (Must have get/set)
        public string StartsWith
        {
            get => _startsWith;
            set { _startsWith = value; OnPropertyChanged(); }
        }

        // PROPERTY (Must have get/set)
        public string MapTo
        {
            get => _mapTo;
            set { _mapTo = value; OnPropertyChanged(); }
        }

        public CategoryRule()
        {
            StartsWith = "";
            MapTo = "";
        }

        // Boilerplate for UI updates
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}