// Models/DashboardDataPoint.cs
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace WPF_LoginForm.Models
{
    public class DashboardDataPoint : INotifyPropertyChanged
    {
        public double X { get; set; }
        public double Y { get; set; }
        public string Label { get; set; }
        public bool IsImportant { get; set; }
        public Thickness LabelMargin { get; set; }

        private bool _showLabel;

        public bool ShowLabel
        {
            get => _showLabel;
            set
            {
                if (_showLabel != value)
                {
                    _showLabel = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}