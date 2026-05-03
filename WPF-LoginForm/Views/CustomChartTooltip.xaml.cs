// Views/CustomChartTooltip.xaml.cs
using System.ComponentModel;
using System.Windows.Controls;
using LiveCharts;
using LiveCharts.Wpf;

namespace WPF_LoginForm.Views
{
    public partial class CustomChartTooltip : UserControl, IChartTooltip
    {
        private TooltipData _data;

        public CustomChartTooltip()
        {
            InitializeComponent();
            // DataContext binds to itself so the ItemsControl can read "Data.Points"
            DataContext = this;
        }

        public TooltipData Data
        {
            get => _data;
            set
            {
                _data = value;
                OnPropertyChanged(nameof(Data));
            }
        }

        public TooltipSelectionMode? SelectionMode { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}